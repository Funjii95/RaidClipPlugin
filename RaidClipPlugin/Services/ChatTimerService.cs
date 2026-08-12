using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public interface IChatTimerClient
{
    Task<int> GetViewerCountAsync(
        string broadcasterId,
        CancellationToken cancellationToken);

    Task SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string message,
        CancellationToken cancellationToken);
}

public sealed class ChatTimerService
{
    private readonly string _broadcasterId;
    private readonly string _senderId;
    private readonly ChatTimerConfig _config;
    private readonly IChatTimerClient _client;

    public event Action<string>? StatusChanged;
    public event Action<ChatTimerEntryConfig, int>? MessageSent;

    public ChatTimerService(
        string broadcasterId,
        string senderId,
        ChatTimerConfig config,
        IChatTimerClient client)
    {
        _broadcasterId = broadcasterId;
        _senderId = senderId;
        _config = config;
        _client = client;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var entries = _config.Entries
            .Where(entry => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Message))
            .ToArray();

        if (!_config.Enabled || entries.Length == 0)
        {
            StatusChanged?.Invoke("Deaktiviert");
            return;
        }

        StatusChanged?.Invoke($"Aktiv · {entries.Length} Timer");
        await Task.WhenAll(entries.Select(entry =>
            RunEntryAsync(entry, cancellationToken)));
    }

    private async Task RunEntryAsync(
        ChatTimerEntryConfig entry,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMinutes(entry.IntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var viewers = await _client.GetViewerCountAsync(
                    _broadcasterId,
                    cancellationToken);
                if (!ShouldPost(entry.MinimumViewers, viewers))
                {
                    StatusChanged?.Invoke(
                        $"Wartet · {viewers}/{entry.MinimumViewers} Zuschauer");
                    continue;
                }

                await _client.SendChatMessageAsync(
                    _broadcasterId,
                    _senderId,
                    entry.Message.Trim(),
                    cancellationToken);
                MessageSent?.Invoke(entry, viewers);
                StatusChanged?.Invoke(
                    $"Gesendet · {viewers} Zuschauer · {DateTime.Now:HH:mm:ss}");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("Fehler: " + exception.Message);
                Console.WriteLine("Chat-Timer fehlgeschlagen: " + exception.Message);
            }
        }
    }

    public static bool ShouldPost(int minimumViewers, int currentViewers) =>
        Math.Max(0, currentViewers) >= Math.Max(0, minimumViewers);
}
