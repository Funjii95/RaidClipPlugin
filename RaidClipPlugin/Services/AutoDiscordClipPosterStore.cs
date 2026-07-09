using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class AutoDiscordClipPosterStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AutoDiscordClipPosterStore(string? path = null)
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(dataDir, "auto-discord-clip-posts.json")
            : path;
    }

    public async Task<bool> WasPostedAsync(
        string clipId,
        string destination,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadCoreAsync(cancellationToken);
            return entries.Any(item =>
                item.ClipId.Equals(clipId, StringComparison.Ordinal) &&
                DestinationKey(item).Equals(destination, StringComparison.Ordinal) &&
                item.Status.Equals("Posted", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(
        AutoDiscordClipPosterEntry entry,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadCoreAsync(cancellationToken);
            var destination = DestinationKey(entry);
            entries.RemoveAll(item =>
                item.ClipId.Equals(entry.ClipId, StringComparison.Ordinal) &&
                DestinationKey(item).Equals(destination, StringComparison.Ordinal));
            entries.Add(entry);
            await SaveCoreAsync(entries, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static string DestinationKey(string webhookUrl, string channelId)
    {
        if (!string.IsNullOrWhiteSpace(channelId))
            return "channel:" + channelId.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return "webhook:";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(webhookUrl.Trim()));
        return "webhook:" + Convert.ToHexString(hash);
    }

    private static string DestinationKey(AutoDiscordClipPosterEntry entry) =>
        DestinationKey(entry.DiscordWebhookUrl, entry.DiscordChannelId);

    private async Task<List<AutoDiscordClipPosterEntry>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return new List<AutoDiscordClipPosterEntry>();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<AutoDiscordClipPosterEntry>>(
                   stream, JsonOptions, cancellationToken) ??
               new List<AutoDiscordClipPosterEntry>();
    }

    private async Task SaveCoreAsync(
        List<AutoDiscordClipPosterEntry> entries,
        CancellationToken cancellationToken)
    {
        var tmp = _path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(
                stream, entries, JsonOptions, cancellationToken);
        }
        File.Copy(tmp, _path, overwrite: true);
        File.Delete(tmp);
    }
}
