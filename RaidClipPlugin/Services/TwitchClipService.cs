using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class TwitchClipService
{
    private readonly ITwitchClipClient _client;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;

    public TwitchClipService(
        ITwitchClipClient client,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null)
    {
        _client = client;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    public async Task<PublishedClip> CreateAndWaitAsync(
        TwitchClipRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _client.CreateClipAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(created.Id))
            throw new InvalidOperationException(
                "Twitch hat keine Clip-ID zurückgegeben.");

        Console.WriteLine("Twitch-Clip angefordert: " + created.Id);
        var deadline = DateTimeOffset.UtcNow + _timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clip = await _client.GetClipByIdAsync(
                created.Id, cancellationToken);
            if (clip is not null && !string.IsNullOrWhiteSpace(clip.Url))
                return clip;

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(
                remaining < _pollInterval ? remaining : _pollInterval,
                cancellationToken);
        }

        throw new TimeoutException(
            "Twitch hat den Clip nicht innerhalb von 60 Sekunden veröffentlicht.");
    }
}
