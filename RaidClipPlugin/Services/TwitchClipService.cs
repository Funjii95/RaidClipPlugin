using System.Net;
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
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<PublishedClip> CreateAndWaitAsync(
        TwitchClipRequest request,
        CancellationToken cancellationToken)
    {
        var created = await CreateWithRetryAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(created.Id))
            throw new InvalidOperationException(
                "Twitch hat keine Clip-ID zurückgegeben.");

        Console.WriteLine("Twitch-Clip angefordert: " + created.Id);
        var deadline = DateTimeOffset.UtcNow + _timeout;
        var pollAttempt = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pollAttempt++;
            try
            {
                var clip = await _client.GetClipByIdAsync(
                    created.Id, cancellationToken);
                if (clip is not null && !string.IsNullOrWhiteSpace(clip.Url))
                {
                    Console.WriteLine(
                        $"Twitch-Clip {created.Id} nach {pollAttempt} Abfragen veröffentlicht.");
                    return clip;
                }
            }
            catch (HttpRequestException exception)
                when (IsTransient(exception))
            {
                Console.WriteLine(
                    $"Twitch-Clipstatus vorübergehend nicht abrufbar: {exception.Message}");
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(
                remaining < _pollInterval ? remaining : _pollInterval,
                cancellationToken);
        }

        throw new TimeoutException(
            $"Twitch hat den Clip {created.Id} nicht innerhalb von " +
            $"{Math.Ceiling(_timeout.TotalSeconds)} Sekunden veröffentlicht. " +
            "Prüfe in Twitch, ob Clips für den Kanal aktiviert sind.");
    }

    private async Task<TwitchCreatedClip> CreateWithRetryAsync(
        TwitchClipRequest request,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                Console.WriteLine(
                    $"Sende Clip-Anfrage an Twitch ({attempt}/{maximumAttempts}) " +
                    $"für Broadcaster {request.BroadcasterId}.");
                return await _client.CreateClipAsync(request, cancellationToken);
            }
            catch (HttpRequestException exception)
                when (attempt < maximumAttempts && IsTransient(exception))
            {
                var delay = TimeSpan.FromSeconds(attempt * 2);
                Console.WriteLine(
                    $"Twitch-Clip-Anfrage vorübergehend fehlgeschlagen: " +
                    $"{exception.Message}. Neuer Versuch in {delay.TotalSeconds:0} Sekunden.");
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Die Twitch-Clip-Anfrage konnte nicht ausgeführt werden.");
    }

    private static bool IsTransient(HttpRequestException exception) =>
        exception.StatusCode is HttpStatusCode.TooManyRequests or
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
}
