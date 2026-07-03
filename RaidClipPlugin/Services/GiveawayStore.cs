using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class GiveawayStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;

    public GiveawayStore(string? storagePath = null)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin", "giveaways");
        Directory.CreateDirectory(directory);
        _filePath = storagePath ?? Path.Combine(directory, "current-giveaway.json");
    }

    public async Task<GiveawayRuntimeState> LoadAsync(
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath)) return new GiveawayRuntimeState();
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<GiveawayRuntimeState>(
                stream, JsonOptions, cancellationToken) ?? new GiveawayRuntimeState();
            state.Participants ??= new List<GiveawayParticipant>();
            state.Winners ??= new List<GiveawayWinner>();
            return state;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            Console.WriteLine("Giveaway-Daten konnten nicht geladen werden: " +
                              exception.Message);
            return new GiveawayRuntimeState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(
        GiveawayRuntimeState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var temporary = _filePath + ".tmp";
            await using (var stream = new FileStream(
                temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    stream, state, JsonOptions, cancellationToken);
            }
            File.Move(temporary, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(new GiveawayRuntimeState(), cancellationToken);
    }
}
