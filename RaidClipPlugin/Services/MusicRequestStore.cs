using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class MusicRequestStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path;
    private MusicRequestStoreData? _data;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public MusicRequestStore(string? storagePath = null)
    {
        var directory = storagePath is null
            ? Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "RaidClipPlugin")
            : Path.GetDirectoryName(Path.GetFullPath(storagePath))!;
        Directory.CreateDirectory(directory);
        _path = storagePath is null
            ? Path.Combine(directory, "music-requests.json")
            : Path.GetFullPath(storagePath);
    }

    public async Task<bool> IsProcessedAsync(
        string redemptionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _data!.ProcessedRedemptionIds.Contains(
                redemptionId, StringComparer.Ordinal);
        }
        finally { _lock.Release(); }
    }

    public async Task AddOrUpdateAsync(
        MusicRequestEntry entry, bool markProcessed,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var index = _data!.Entries.FindIndex(item =>
                item.RedemptionId.Equals(entry.RedemptionId,
                    StringComparison.Ordinal));
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            if (index < 0) _data.Entries.Insert(0, entry);
            else _data.Entries[index] = entry;
            if (markProcessed &&
                !_data.ProcessedRedemptionIds.Contains(entry.RedemptionId,
                    StringComparer.Ordinal))
                _data.ProcessedRedemptionIds.Insert(0, entry.RedemptionId);
            if (_data.ProcessedRedemptionIds.Count > 1000)
                _data.ProcessedRedemptionIds.RemoveRange(
                    1000, _data.ProcessedRedemptionIds.Count - 1000);
            if (_data.Entries.Count > 1000)
                _data.Entries.RemoveRange(1000, _data.Entries.Count - 1000);
            await SaveAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<MusicRequestEntry>> GetEntriesAsync(
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _data!.Entries.Select(Clone).ToArray();
        }
        finally { _lock.Release(); }
    }

    public async Task RemoveAsync(
        string redemptionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _data!.Entries.RemoveAll(item =>
                item.RedemptionId.Equals(redemptionId,
                    StringComparison.Ordinal));
            await SaveAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task ClearQueueAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _data!.Entries.RemoveAll(item => item.Status is
                MusicRequestStatus.Accepted or MusicRequestStatus.Queued or
                MusicRequestStatus.Failed or MusicRequestStatus.Checking);
            await SaveAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_data is not null) return;
        if (!File.Exists(_path))
        {
            _data = new MusicRequestStoreData();
            return;
        }
        try
        {
            await using var stream = File.OpenRead(_path);
            _data = await JsonSerializer.DeserializeAsync<MusicRequestStoreData>(
                stream, JsonOptions, cancellationToken) ??
                new MusicRequestStoreData();
        }
        catch
        {
            var backup = _path + ".invalid-" +
                         DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(_path, backup, true);
            _data = new MusicRequestStoreData();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var temporary = _path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(
                stream, _data, JsonOptions, cancellationToken);
        File.Move(temporary, _path, true);
    }

    private static MusicRequestEntry Clone(MusicRequestEntry entry) => new()
    {
        RedemptionId = entry.RedemptionId,
        RewardId = entry.RewardId,
        RewardName = entry.RewardName,
        UserId = entry.UserId,
        UserLogin = entry.UserLogin,
        DisplayName = entry.DisplayName,
        UserInput = entry.UserInput,
        RedeemedAt = entry.RedeemedAt,
        UpdatedAt = entry.UpdatedAt,
        Status = entry.Status,
        PlaybackMode = entry.PlaybackMode,
        Track = entry.Track,
        FailureReason = entry.FailureReason
    };

    private sealed class MusicRequestStoreData
    {
        public List<MusicRequestEntry> Entries { get; set; } = new();
        public List<string> ProcessedRedemptionIds { get; set; } = new();
    }
}
