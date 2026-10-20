using Google.Cloud.Firestore;
using SnapshotProtocol;

namespace SnapshotFirestore;

// Requires an explicitly supplied client. Never obtains credentials or connects on construction.
public sealed class FirestoreSnapshotSink : ISnapshotSink
{
    private readonly FirestoreDb database;
    private readonly string channel;
    private readonly string collection;
    private readonly bool sequentialUpdatesOnly;
    private readonly bool allowSequentialDeletes;
    private readonly bool allowInitialProfiles;
    public FirestoreSnapshotSink(FirestoreDb database, string channel, string collection = "rpgReadSnapshots", bool sequentialUpdatesOnly = false,bool allowSequentialDeletes=false,bool allowInitialProfiles=false)
    {
        if (channel.Length is < 1 or > 25 || channel.Any(c => c is < '0' or > '9') ||
            string.IsNullOrWhiteSpace(collection) || collection.Length > 64 ||
            collection.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            throw new ArgumentException("Invalid trusted snapshot scope.");
        this.database = database;
        this.channel = channel;
        this.collection = collection;
        this.sequentialUpdatesOnly = sequentialUpdatesOnly;
        this.allowSequentialDeletes=allowSequentialDeletes;
        this.allowInitialProfiles=allowInitialProfiles;
    }

    // Operator bootstrap inspection: validates exactly the same persisted contract as writes.
    // This does not reserve a revision; callers must re-check before enabling delivery.
    public async Task<StoredSnapshot?> ReadCurrentAsync(string userId, CancellationToken ct = default)
    {
        if (userId.Length is < 1 or > 25 || userId.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Invalid snapshot user.");
        var document = await database.Collection(collection).Document(userId).GetSnapshotAsync(ct);
        return document.Exists ? ReadStored(document, userId) : null;
    }

    public Task<SnapshotDecision> ApplyAsync(SnapshotMessage message, CancellationToken ct = default)
    {
        var incoming = SnapshotValidation.Validate(message, channel);
        ct.ThrowIfCancellationRequested();
        var reference = database.Collection(collection).Document(incoming.UserId);
        return database.RunTransactionAsync(async transaction =>
        {
            var document = await transaction.GetSnapshotAsync(reference, ct);
            var current = document.Exists ? ReadStored(document, incoming.UserId) : null;
            var decision = SnapshotRules.Decide(current, incoming);
            if (decision == SnapshotDecision.Applied)
            {
                var initial = allowInitialProfiles && current is null && incoming.Kind == SnapshotKind.Upsert && incoming.Revision == 1;
                if (sequentialUpdatesOnly && !initial && (current is null || current.Deleted || (incoming.Kind != SnapshotKind.Upsert && !(allowSequentialDeletes && incoming.Kind==SnapshotKind.Delete)) ||
                    current.Revision == long.MaxValue || incoming.Revision != current.Revision + 1))
                    return SnapshotDecision.Conflict;
                var fields = new Dictionary<string, object>
                {
                    ["contractVersion"] = 1L, ["channelId"] = channel, ["userId"] = incoming.UserId,
                    ["revision"] = incoming.Revision, ["snapshotId"] = incoming.SnapshotId.ToString(),
                    ["capturedAt"] = Timestamp.FromDateTimeOffset(incoming.CapturedAt),
                    ["capturedAtIso"] = incoming.CapturedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    ["kind"] = (long)incoming.Kind, ["deleted"] = incoming.Kind == SnapshotKind.Delete,
                    ["contentHash"] = incoming.ContentHash
                };
                if (incoming.ExpectedDeletedRevision is { } deletedRevision)
                    fields["expectedDeletedRevision"] = deletedRevision;
                if (incoming.AccountJson is not null) fields["accountJson"] = incoming.AccountJson;
                // Full replacement removes accountJson on Delete and old import metadata on update.
                transaction.Set(reference, fields);
            }
            return decision;
        }, TransactionOptions.ForMaxAttempts(5), ct);
    }

    private StoredSnapshot ReadStored(DocumentSnapshot document, string userId)
    {
        try
        {
            var fields = document.ToDictionary();
            if (document.Id != userId || Required<string>(fields, "channelId") != channel)
                throw new InvalidDataException("Stored channel mismatch.");
            var revision = Required<long>(fields, "revision");
            var id = Guid.Parse(Required<string>(fields, "snapshotId"));
            var captured = Required<Timestamp>(fields, "capturedAt").ToDateTimeOffset();
            ValidatedSnapshot validated;
            if (!fields.ContainsKey("contractVersion"))
            {
                // Exactly the documented one-time import, not an arbitrary legacy account blob.
                var sourceHash = Required<string>(fields, "importSourceHash");
                if (revision != 1 || Required<string>(fields, "importKind") != "explicit-initial-profile-import" ||
                    sourceHash.Length != 64 || sourceHash.Any(c => !Uri.IsHexDigit(c)) ||
                    fields.ContainsKey("deleted") || fields.ContainsKey("contentHash") || fields.ContainsKey("kind"))
                    throw new InvalidDataException("Unsupported initial import metadata.");
                validated = SnapshotValidation.Validate(new(1, channel, userId, revision, id, captured,
                    SnapshotKind.Upsert, Required<string>(fields, "accountJson")), channel);
            }
            else
            {
                if (Required<long>(fields, "contractVersion") != 1 || Required<string>(fields, "userId") != userId)
                    throw new InvalidDataException("Unsupported stored contract.");
                var kindValue = Required<long>(fields, "kind");
                if (kindValue is < 0 or > 2) throw new InvalidDataException("Invalid stored kind.");
                var kind = (SnapshotKind)kindValue;
                var deleted = Required<bool>(fields, "deleted");
                if (deleted != (kind == SnapshotKind.Delete) || (deleted && fields.ContainsKey("accountJson")))
                    throw new InvalidDataException("Invalid tombstone.");
                long? expected = fields.ContainsKey("expectedDeletedRevision")
                    ? Required<long>(fields, "expectedDeletedRevision") : null;
                var account = deleted ? null : Required<string>(fields, "accountJson");
                var storedHash = Required<string>(fields, "contentHash");
                if (fields.ContainsKey("capturedAtIso"))
                {
                    var exact = DateTimeOffset.ParseExact(Required<string>(fields, "capturedAtIso"), "O",
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (exact.UtcTicks / 10 != captured.UtcTicks / 10)
                        throw new InvalidDataException("Timestamp representations disagree.");
                    captured = exact;
                    validated = SnapshotValidation.Validate(new(1, channel, userId, revision, id, captured, kind, account, expected), channel);
                }
                else
                {
                    // Compatibility for the initial synthetic deployment: Firestore truncates
                    // timestamps to microseconds. Recover only a hash-proven original 100ns tick.
                    ValidatedSnapshot? recovered = null;
                    var floor = captured.AddTicks(-(captured.Ticks % 10));
                    for (var tick = 0; tick < 10; tick++)
                    {
                        var candidate = SnapshotValidation.Validate(new(1, channel, userId, revision, id,
                            floor.AddTicks(tick), kind, account, expected), channel);
                        if (candidate.ContentHash == storedHash) { recovered = candidate; break; }
                    }
                    validated = recovered ?? throw new InvalidDataException("Stored content hash mismatch.");
                    captured = validated.CapturedAt;
                }
                if (storedHash != validated.ContentHash) throw new InvalidDataException("Stored content hash mismatch.");
            }
            return new(channel, userId, revision, id, captured, validated.Kind == SnapshotKind.Delete,
                validated.AccountJson, validated.ContentHash);
        }
        catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("Invalid existing snapshot document."); }
    }

    private static T Required<T>(Dictionary<string, object> fields, string name) =>
        fields.TryGetValue(name, out var value) && value is T result ? result :
            throw new InvalidDataException("Missing or invalid snapshot field.");
}
