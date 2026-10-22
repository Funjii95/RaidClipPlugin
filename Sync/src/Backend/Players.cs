using System.Text.Json;
using Google.Cloud.Firestore;
namespace Backend;
public sealed record PlayerView(string UserId, string DisplayName, long Points, RpgView Rpg,
    DateTimeOffset UpdatedAt, string Source);
public sealed class RpgView
{
    public int SchemaVersion { get; init; }
    public bool LegacyFishImported { get; init; }
    public int? PlayerLevel { get; init; }
    public long? Experience { get; init; }
    public int? SushiVersion { get; init; }
    public MaterialView? Materials { get; init; }
    public HealthView? Health { get; init; }
    public JsonElement[] Inventory { get; init; } = [];
    public Dictionary<string, string> Equipment { get; init; } = [];
    public Dictionary<string, int> Perks { get; init; } = [];
    public Dictionary<string, int> TalentRanks { get; init; } = [];
    public Dictionary<string, JsonElement> Fish { get; init; } = [];
    public Dictionary<string, int> Sushi { get; init; } = [];
    public JsonElement[] Buffs { get; init; } = [];
    public Dictionary<string, DateTimeOffset> Cooldowns { get; init; } = [];
    // CompletedActions are private plugin idempotency tokens and never exposed.
}
public sealed record MaterialView(int Shells, int WhitePearls, int BluePearls, int RedPearls, int StoneChips);
public sealed class HealthView
{
    public long? CurrentHp { get; init; }
    public long? AppliedMaximumHp { get; init; }
    public long? AppliedBaseMaximumHp { get; init; }
    public DateTimeOffset? LastPassiveRegen { get; init; }
    public decimal? SushiRegenRemainder { get; init; }
    public DateTimeOffset? LastChatHeal { get; init; }
    public string? BossId { get; init; }
    public bool? Defeated { get; init; }
    public long? BossDamage { get; init; }
    public long? BossRemainingHp { get; init; }
}
public interface IPlayerReader
{
    Task<PlayerView?> GetAsync(string userId, CancellationToken ct);
    Task CheckAsync(CancellationToken ct);
}
public sealed class MockPlayerReader : IPlayerReader
{
    public Task<PlayerView?> GetAsync(string userId, CancellationToken ct) => Task.FromResult<PlayerView?>(
        userId == "1001" ? new("1001", "Demo Otter", 1234, new() { SchemaVersion = 1 }, DateTimeOffset.UnixEpoch, "mock") : null);
    public Task CheckAsync(CancellationToken ct) => Task.CompletedTask;
}
public sealed class FirestorePlayerReader : IPlayerReader
{
    private readonly FirestoreDb db;
    private readonly string collection;
    private readonly string? expectedChannel;
    public FirestorePlayerReader(DataSettings settings,string? expectedChannel=null)
        : this(new FirestoreDbBuilder
        {
            ProjectId = settings.ProjectId, DatabaseId = settings.DatabaseId,
            EmulatorDetection = Google.Api.Gax.EmulatorDetection.ProductionOnly
        }.Build(), settings.Collection,expectedChannel) { }
    public FirestorePlayerReader(FirestoreDb database, string collectionName,string? expectedChannel=null)
    {
        db = database;
        collection = collectionName;
        this.expectedChannel=expectedChannel;
    }
    public async Task<PlayerView?> GetAsync(string userId, CancellationToken ct)
    {
        if (!Ids.IsNumeric(userId)) throw new ArgumentException("Invalid Twitch user ID.", nameof(userId));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var document = await GetDocumentAsync(userId, ct, deadline.Token);
        if (!document.Exists) return null;
        if(expectedChannel is not null && (!document.TryGetValue<string>("channelId",out var ownerChannel)||ownerChannel!=expectedChannel))throw new InvalidDataException("Snapshot channel mismatch.");
        if (document.ContainsField("deleted"))
        {
            if (!document.TryGetValue<bool>("deleted", out var deleted))
                throw new InvalidDataException("Invalid snapshot deletion flag.");
            if (deleted)
            {
                var fields = document.ToDictionary();
                if (document.ContainsField("accountJson") ||
                    !fields.TryGetValue("contractVersion", out var version) || version is not 1L ||
                    !fields.TryGetValue("kind", out var kind) || kind is not 1L ||
                    !fields.TryGetValue("revision", out var revision) || revision is not long number || number <= 0 ||
                    !fields.TryGetValue("userId", out var owner) || owner is not string id || id != userId ||
                    !fields.TryGetValue("channelId", out var channel) || channel is not string channelId || !Ids.IsNumeric(channelId) ||
                    !fields.TryGetValue("snapshotId", out var snapshot) || snapshot is not string snapshotText ||
                    !Guid.TryParse(snapshotText, out var snapshotId) || snapshotId == Guid.Empty ||
                    !fields.TryGetValue("capturedAt", out var captured) || captured is not Timestamp ||
                    !fields.TryGetValue("contentHash", out var hash) || hash is not string hashText ||
                    hashText.Length != 64 || hashText.Any(c => !Uri.IsHexDigit(c)))
                    throw new InvalidDataException("Invalid or ambiguous tombstone.");
                return null;
            }
        }
        return SnapshotParser.Parse(document.GetValue<string>("accountJson"), userId);
    }
    public async Task CheckAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        await GetDocumentAsync("_readiness", ct, deadline.Token);
    }
    private async Task<DocumentSnapshot> GetDocumentAsync(string documentId, CancellationToken callerToken,
        CancellationToken deadlineToken)
    {
        callerToken.ThrowIfCancellationRequested();
        try { return await db.Collection(collection).Document(documentId).GetSnapshotAsync(deadlineToken); }
        catch (Grpc.Core.RpcException) when (callerToken.IsCancellationRequested)
        { throw new OperationCanceledException(callerToken); }
    }
}
public static class SnapshotParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static PlayerView Parse(string json, string expectedId)
    {
        var account = JsonSerializer.Deserialize<AccountSnapshot>(json, Options) ?? throw new InvalidDataException();
        if (account.UserId != expectedId || account.Points is null or < 0 || account.UpdatedAt is null ||
            account.Rpg?.SchemaVersion != 1 || string.IsNullOrWhiteSpace(account.DisplayName))
            throw new InvalidDataException("Invalid snapshot identity, balance or schema.");
        if(account.Rpg.Materials is {} m && (m.Shells<0 || m.WhitePearls<0 || m.BluePearls<0 || m.RedPearls<0 || m.StoneChips<0))throw new InvalidDataException("Invalid material balance.");
        return new(account.UserId, account.DisplayName, account.Points.Value, account.Rpg,
            account.UpdatedAt.Value, "plugin-snapshot");
    }
    private sealed class AccountSnapshot
    {
        public string UserId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public long? Points { get; init; }
        public RpgView? Rpg { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
