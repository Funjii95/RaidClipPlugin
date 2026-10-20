using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SnapshotProtocol;

public sealed record OutboxState(int Version, string ChannelId, string UserId, long Revision,
    Guid CommitId, string CommitHash, bool Deleted, string? PrivateAccountJson,
    SnapshotMessage LastMessage, SnapshotMessage[] Pending);

// Synthetic integration prototype only. A future plugin must replace its account persistence
// with ONE transaction containing account, revision and outbox. Calling this after SaveAsync
// would not make the two stores atomic. No gameplay code or network transport belongs here.
public sealed class LocalOutboxStore
{
    public const int MaxPending = 128;
    // Privacy deletion deliberately cancels queued profile payloads. It never rebases gameplay.
    public async Task QueuePrivacyDeletionAsync(StoredSnapshot head,Guid operationId,CancellationToken ct=default)
    {
        if(head.ChannelId!=channelId || head.Deleted || head.Revision is <=0 or long.MaxValue || operationId==Guid.Empty)throw new InvalidDataException("Invalid privacy head.");
        var path=PathFor(head.UserId);await using var lease=await LockAsync(path,ct);
        var current=await LoadAsync(path,head.UserId,ct);
        if(current?.LastMessage.Kind==SnapshotKind.Delete){
            if(current.CommitId!=operationId || current.Revision!=head.Revision+1)throw new InvalidDataException("Privacy revision conflict.");return;
        }
        var message=new SnapshotMessage(1,channelId,head.UserId,head.Revision+1,operationId,DateTimeOffset.UtcNow,SnapshotKind.Delete,null);
        var clean=SnapshotValidation.Validate(message,channelId);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clean.ContentHash+"\n")));
        await SaveAsync(path,new OutboxState(1,channelId,head.UserId,message.Revision,operationId,hash,true,null,message,[message]),ct);
    }
    public async Task ForgetPrivacyPayloadAsync(string userId,CancellationToken ct=default)
    {
        var path=PathFor(userId);await using var lease=await LockAsync(path,ct);if(File.Exists(path))File.Delete(path);
        foreach(var temp in Directory.GetFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".*.tmp"))File.Delete(temp);
    }
    private const int MaxStateBytes = 16 * 1024 * 1024;
    private readonly string directory;
    private readonly string channelId;

    public LocalOutboxStore(string directory, string channelId)
    {
        if (!Numeric(channelId)) throw new ArgumentException("Invalid channel.");
        this.directory = Path.GetFullPath(directory);
        this.channelId = channelId;
    }

    // The caller supplies a detached complete account after its local mutation; never a delta.
    public async Task<OutboxState> InitializeBaselineAsync(SnapshotMessage baseline, string cloudHash, CancellationToken ct = default)
    {
        var valid=SnapshotValidation.Validate(baseline,channelId);
        if(baseline.Kind!=SnapshotKind.Upsert || valid.ContentHash!=cloudHash)
            throw new InvalidDataException("Cloud baseline does not match the local committed profile.");
        var path=PathFor(baseline.UserId);
        await using var lease=await LockAsync(path,ct);
        if(await LoadAsync(path,baseline.UserId,ct) is not null)throw new InvalidDataException("Baseline already exists.");
        var clean=baseline with {AccountJson=valid.AccountJson};
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valid.ContentHash+"\n"+valid.AccountJson)));
        var state=new OutboxState(1,channelId,baseline.UserId,baseline.Revision,baseline.SnapshotId,hash,false,valid.AccountJson,clean,[]);
        await SaveAsync(path,state,ct);return state;
    }

    // expectedRevision prevents two writers from committing against the same previous state.
    public async Task<OutboxState> CommitAsync(string userId, long expectedRevision, Guid commitId,
        DateTimeOffset capturedAt, SnapshotKind kind, string? privateAccountJson,
        long? expectedDeletedRevision = null, CancellationToken ct = default)
    {
        if (expectedRevision < 0 || expectedRevision == long.MaxValue || commitId == Guid.Empty)
            throw new InvalidDataException("Invalid commit identity or revision.");
        var candidate = new SnapshotMessage(1, channelId, userId, expectedRevision + 1, commitId,
            capturedAt, kind, privateAccountJson, expectedDeletedRevision);
        var validated = SnapshotValidation.Validate(candidate, channelId);
        // Full local account is retained privately, but only this projected copy can be sent.
        var message = candidate with { AccountJson = validated.AccountJson };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            validated.ContentHash + "\n" + privateAccountJson)));
        var path = PathFor(userId);
        await using var accountLock = await LockAsync(path, ct);
        var current = await LoadAsync(path, userId, ct);
        if (current is not null && current.CommitId == commitId)
        {
            if (current.Revision != candidate.Revision || current.CommitHash != hash)
                throw new InvalidDataException("Commit identity reused with different contents.");
            return current; // Includes retries after acknowledgement; never enqueue again.
        }
        if ((current?.Revision ?? 0) != expectedRevision)
            throw new InvalidDataException("Revision changed; reload without replaying gameplay.");
        if (current?.Pending.Any(p => p.SnapshotId == commitId) == true)
            throw new InvalidDataException("Pending commit identity reused.");
        if (current?.Deleted == true && kind == SnapshotKind.Upsert)
            throw new InvalidDataException("Explicit restore required.");
        if (kind == SnapshotKind.Restore &&
            (current is not { Deleted: true } || current.Revision != expectedDeletedRevision))
            throw new InvalidDataException("Restore precondition failed.");
        if ((current?.Pending.Length ?? 0) >= MaxPending)
            throw new InvalidDataException("Local prototype outbox is full.");
        var next = new OutboxState(1, channelId, userId, candidate.Revision, commitId, hash,
            kind == SnapshotKind.Delete, privateAccountJson, message, [.. current?.Pending ?? [], message]);
        await SaveAsync(path, next, ct);
        return next;
    }

    public async Task<OutboxState?> ReadAsync(string userId, CancellationToken ct = default)
    {
        var path = PathFor(userId);
        await using var accountLock = await LockAsync(path, ct);
        return await LoadAsync(path, userId, ct);
    }

    // Separate lease serializes senders across store instances without blocking new commits.
    public async Task<IAsyncDisposable> AcquireDispatchLeaseAsync(string userId, CancellationToken ct = default)
        => await LockAsync(PathFor(userId) + ".dispatch", ct);

    // A failed or unknown result never removes a message. Acknowledgement is head-only.
    public async Task AcknowledgeAsync(string userId, Guid snapshotId, long revision,
        SnapshotDecision decision, CancellationToken ct = default)
    {
        if (decision is not (SnapshotDecision.Applied or SnapshotDecision.Duplicate or SnapshotDecision.Stale))
            throw new InvalidDataException("Publisher did not accept the message.");
        var path = PathFor(userId);
        await using var accountLock = await LockAsync(path, ct);
        var state = await LoadAsync(path, userId, ct)
            ?? throw new InvalidDataException("Missing outbox.");
        if (state.Pending.FirstOrDefault() is not { } head ||
            head.SnapshotId != snapshotId || head.Revision != revision)
            throw new InvalidDataException("Acknowledgement does not match pending head.");
        await SaveAsync(path, state with { Pending = state.Pending[1..] }, ct);
    }

    private string PathFor(string userId)
    {
        if (!Numeric(userId)) throw new ArgumentException("Invalid user.");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{channelId}-{userId}.outbox.json");
    }

    private async Task<OutboxState?> LoadAsync(string path, string userId, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        if (stream.Length > MaxStateBytes) throw new InvalidDataException("Local state too large.");
        var state = await JsonSerializer.DeserializeAsync<OutboxState>(stream, cancellationToken: ct)
            ?? throw new InvalidDataException("Corrupt outbox.");
        if (state.Version != 1 || state.ChannelId != channelId || state.UserId != userId ||
            state.Revision <= 0 || state.CommitId == Guid.Empty || state.CommitHash is not { Length: 64 } ||
            state.Deleted != (state.PrivateAccountJson is null) ||
            state.Pending is null || state.Pending.Length > MaxPending)
            throw new InvalidDataException("Corrupt outbox metadata.");
        if (state.LastMessage is not { } committed || committed.UserId != userId ||
            committed.Revision != state.Revision || committed.SnapshotId != state.CommitId ||
            (committed.Kind == SnapshotKind.Delete) != state.Deleted)
            throw new InvalidDataException("Corrupt committed message.");
        var lastValidated = SnapshotValidation.Validate(committed with { AccountJson = state.PrivateAccountJson }, channelId);
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            lastValidated.ContentHash + "\n" + state.PrivateAccountJson)));
        if (committed.AccountJson != lastValidated.AccountJson || state.CommitHash != expectedHash)
            throw new InvalidDataException("Account and message do not match committed contents.");
        long previous = 0;
        var ids = new HashSet<Guid>();
        foreach (var message in state.Pending)
        {
            if (message is null || message.UserId != userId || message.Revision <= previous ||
                (previous > 0 && message.Revision != previous + 1) ||
                message.Revision > state.Revision || !ids.Add(message.SnapshotId))
                throw new InvalidDataException("Corrupt outbox sequence.");
            var validated = SnapshotValidation.Validate(message, channelId);
            // Loaded messages must already be projected, including no private root fields.
            if (validated.AccountJson != message.AccountJson)
                throw new InvalidDataException("Outbox contains unprojected contents.");
            previous = message.Revision;
        }
        if (state.Pending.LastOrDefault() is { } last && last != committed)
            throw new InvalidDataException("Outbox does not match committed revision.");
        return state;
    }

    private static async Task SaveAsync(string path, OutboxState state, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        if (bytes.Length > MaxStateBytes) throw new InvalidDataException("Local state too large.");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static async Task<FileStream> LockAsync(string path, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None); }
            catch (IOException) { await Task.Delay(25, deadline.Token); }
        }
    }

    private static bool Numeric(string? value) => value is { Length: > 0 and <= 25 } &&
        value.All(c => c is >= '0' and <= '9');
}
