using System.Text.Json;

namespace SnapshotProtocol;

public enum SnapshotDecision { Applied, Duplicate, Stale, Conflict, RestoreRequired }
public sealed record StoredSnapshot(string ChannelId, string UserId, long Revision, Guid SnapshotId,
    DateTimeOffset CapturedAt, bool Deleted, string? AccountJson, string ContentHash);

// Local prototype only. The future Firestore publisher must perform this compare-and-replace in a transaction.
public sealed class LocalSnapshotStore : ISnapshotSink
{
    private readonly string directory;
    private readonly string trustedChannelId;
    public LocalSnapshotStore(string directory, string trustedChannelId)
    {
        if (trustedChannelId.Length is < 1 or > 25 || trustedChannelId.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Invalid trusted channel.");
        this.directory = Path.GetFullPath(directory);
        this.trustedChannelId = trustedChannelId;
    }
    public async Task<SnapshotDecision> ApplyAsync(SnapshotMessage message, CancellationToken ct = default)
    {
        var validated = SnapshotValidation.Validate(message, trustedChannelId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{validated.ChannelId}-{validated.UserId}.json");
        // OS file lock serializes separate store instances/processes sharing this directory.
        using var lockDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lockDeadline.CancelAfter(TimeSpan.FromSeconds(5));
        await using var accountLock = await LockAsync(path + ".lock", lockDeadline.Token);
        StoredSnapshot? current = null;
        if (File.Exists(path))
            current = JsonSerializer.Deserialize<StoredSnapshot>(await File.ReadAllTextAsync(path, ct))
                ?? throw new InvalidDataException("Corrupt local snapshot state.");
        var decision = SnapshotRules.Decide(current, validated);
        if (decision != SnapshotDecision.Applied) return decision;
        var next = new StoredSnapshot(validated.ChannelId, validated.UserId, validated.Revision,
            validated.SnapshotId, validated.CapturedAt, validated.Kind == SnapshotKind.Delete,
            validated.AccountJson, validated.ContentHash);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, next, cancellationToken: ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return SnapshotDecision.Applied;
    }
    private static async Task<FileStream> LockAsync(string path, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(25, ct); }
        }
    }
}
