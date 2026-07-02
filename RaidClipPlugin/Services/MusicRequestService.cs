using System.Collections.Concurrent;
using System.Threading.Channels;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class MusicRequestService : IDisposable
{
    private static readonly HttpClient SpotifyLinkClient = new(
        new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private readonly TwitchService _twitch;
    private readonly SpotifyService _spotify;
    private readonly MusicRequestStore _store;
    private readonly Channel<MusicRequestRedemption> _queue =
        Channel.CreateUnbounded<MusicRequestRedemption>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly ConcurrentDictionary<string, byte> _inFlight =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private MusicRequestConfig _config;
    private bool _disposed;

    public event Action<MusicRequestEntry>? RequestUpdated;

    public MusicRequestService(
        string broadcasterId, string chatUserId,
        MusicRequestConfig config, TwitchService twitch,
        SpotifyService spotify, MusicRequestStore store)
    {
        _broadcasterId = broadcasterId;
        _chatUserId = chatUserId;
        _config = config;
        _twitch = twitch;
        _spotify = spotify;
        _store = store;
    }

    public void UpdateConfig(MusicRequestConfig config)
    {
        _config = config;
        _spotify.UpdateConfig(config);
    }

    public async Task<bool> EnqueueAsync(
        MusicRequestRedemption redemption,
        CancellationToken cancellationToken)
    {
        if (!MusicRequestRules.ShouldAcceptRedemption(_config, redemption))
            return false;
        if (!_inFlight.TryAdd(redemption.RedemptionId, 0)) return false;
        if (await _store.IsProcessedAsync(
                redemption.RedemptionId, cancellationToken))
        {
            _inFlight.TryRemove(redemption.RedemptionId, out _);
            return false;
        }
        Console.WriteLine(
            $"Kanalpunkte-Einlösung von Benutzer {redemption.DisplayName} empfangen.");
        try
        {
            await _queue.Writer.WriteAsync(redemption, cancellationToken);
            return true;
        }
        catch
        {
            _inFlight.TryRemove(redemption.RedemptionId, out _);
            throw;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var queueTask = ProcessQueueAsync(cancellationToken);
        var monitorTask = MonitorPlaybackAsync(cancellationToken);
        await Task.WhenAll(queueTask, monitorTask);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var redemption in _queue.Reader.ReadAllAsync(
                           cancellationToken))
        {
            try
            {
                await ProcessMusicRequestAsync(redemption, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Musikwunsch-Verarbeitung fehlgeschlagen: " +
                    exception.Message);
            }
            finally
            {
                _inFlight.TryRemove(redemption.RedemptionId, out _);
            }
        }
    }

    private async Task MonitorPlaybackAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                if (!_spotify.IsConnected) continue;
                var current = await _spotify.GetCurrentTrackAsync(cancellationToken);
                var entries = await _store.GetEntriesAsync(cancellationToken);
                foreach (var entry in entries.Where(item => item.Status ==
                             MusicRequestStatus.Playing &&
                             item.Track?.Id != current?.Id))
                {
                    entry.Status = MusicRequestStatus.Completed;
                    entry.FailureReason = "";
                    await SaveAndNotifyAsync(entry, true, cancellationToken);
                }
                if (current is null || entries.Any(item =>
                        item.Status == MusicRequestStatus.Playing &&
                        item.Track?.Id == current.Id)) continue;
                var playing = entries.OrderBy(item => item.RedeemedAt)
                    .FirstOrDefault(item =>
                    item.Status == MusicRequestStatus.Queued &&
                    item.Track?.Id == current.Id);
                if (playing is not null)
                {
                    playing.Status = MusicRequestStatus.Playing;
                    await SaveAndNotifyAsync(playing, true, cancellationToken);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Spotify-Wiedergabestatus konnte nicht aktualisiert werden: " +
                    exception.Message);
            }
        }
    }

    public async Task<MusicRequestResult> ProcessMusicRequestAsync(
        MusicRequestRedemption redemption,
        CancellationToken cancellationToken)
    {
        await _processingLock.WaitAsync(cancellationToken);
        try
        {
            return await ProcessMusicRequestCoreAsync(
                redemption, cancellationToken);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task<MusicRequestResult> ProcessMusicRequestCoreAsync(
        MusicRequestRedemption redemption,
        CancellationToken cancellationToken)
    {
        var entry = new MusicRequestEntry
        {
            RedemptionId = redemption.RedemptionId,
            RewardId = redemption.RewardId,
            RewardName = redemption.RewardName,
            UserId = redemption.UserId,
            UserLogin = redemption.UserLogin,
            DisplayName = redemption.DisplayName,
            UserInput = redemption.UserInput.Trim(),
            RedeemedAt = redemption.RedeemedAt,
            PlaybackMode = _config.PlaybackMode,
            Status = MusicRequestStatus.Checking
        };
        await SaveAndNotifyAsync(entry, false, cancellationToken);
        Console.WriteLine("Musikwunsch wird verarbeitet: " + entry.UserInput + ".");

        MusicRequestResult result;
        try
        {
            result = await ResolveAndPlayAsync(entry, cancellationToken);
        }
        catch (SpotifyApiException exception)
        {
            result = Fail(entry,
                exception.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "no-device" : "spotify-api",
                exception.Message, exception.IsTemporary);
        }
        catch (HttpRequestException exception)
        {
            result = Fail(entry, "network",
                "Spotify ist vorübergehend nicht erreichbar.", true);
            Console.WriteLine("Spotify-Netzwerkfehler: " + exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            result = Fail(entry, "spotify", exception.Message, true);
        }

        entry.Track = result.Track;
        entry.FailureReason = result.FailureReason;
        entry.Status = result.Success
            ? _config.PlaybackMode == MusicPlaybackMode.AddToQueue
                ? MusicRequestStatus.Queued : MusicRequestStatus.Playing
            : result.IsTemporaryFailure
                ? MusicRequestStatus.Failed : MusicRequestStatus.Rejected;
        await SaveAndNotifyAsync(entry, true, cancellationToken);
        await SendChatOnceAsync(result.UserMessage, cancellationToken);
        await TryUpdateRedemptionAsync(entry, result, cancellationToken);
        return result;
    }

    private async Task<MusicRequestResult> ResolveAndPlayAsync(
        MusicRequestEntry entry, CancellationToken cancellationToken)
    {
        if (MusicRequestRules.IsUserBlacklisted(_config, entry.UserLogin))
            return Reject(entry, "user-blacklist", _config.ChatMessages.Blacklisted);

        var existing = (await _store.GetEntriesAsync(cancellationToken))
            .Where(item => !item.RedemptionId.Equals(
                entry.RedemptionId, StringComparison.Ordinal))
            .ToArray();
        var open = existing.Where(MusicRequestRules.IsOpen).ToArray();
        var limitFailure = MusicRequestRules.ValidateLimits(
            _config, existing, entry.UserId, DateTimeOffset.UtcNow,
            out var remaining);
        if (limitFailure == "queue-full")
            return Reject(entry, limitFailure, _config.ChatMessages.QueueFull);
        if (limitFailure == "user-limit")
            return Reject(entry, limitFailure,
                $"❌ @{entry.DisplayName}, du hast bereits {_config.MaximumRequestsPerUser} offene Musikwünsche.");
        if (limitFailure == "cooldown")
            return Reject(entry, limitFailure, Template(
                _config.ChatMessages.Cooldown, entry, null,
                remainingCooldown: $"{Math.Ceiling(remaining.TotalMinutes)} Min."));

        SpotifyTrack? track;
        var input = entry.UserInput.Trim();
        if (LooksLikeSpotifyInput(input))
        {
            if (!_config.AllowSpotifyLinks)
                return Reject(entry, "invalid-link", _config.ChatMessages.InvalidInput);

            var trackId = await ResolveSpotifyTrackIdAsync(
                input, cancellationToken);
            if (string.IsNullOrWhiteSpace(trackId))
                return Reject(entry, "invalid-link", _config.ChatMessages.InvalidInput);

            track = await _spotify.GetTrackAsync(trackId, cancellationToken);
        }
        else
        {
            if (!_config.AllowTextSearch || string.IsNullOrWhiteSpace(input))
                return Reject(entry, "text-search-disabled",
                    _config.ChatMessages.InvalidInput);
            track = await _spotify.SearchTrackAsync(input, cancellationToken);
        }

        if (track is null)
            return Reject(entry, "not-found", _config.ChatMessages.NotFound);
        Console.WriteLine(
            $"Spotify-Track gefunden: {track.Name} von {track.Artist}.");
        var validationReason = MusicRequestRules.ValidateTrack(
            _config, track, existing.Where(item =>
                    item.Track is not null &&
                    (MusicRequestRules.IsOpen(item) ||
                     item.UpdatedAt > DateTimeOffset.UtcNow.AddHours(-6)))
                .Select(item => item.Track!.Id));
        if (validationReason is not null)
            return CreateValidationFailure(validationReason, entry, track);

        var devices = await _spotify.GetDevicesAsync(cancellationToken);
        var device = SelectDevice(devices);
        if (device is null)
            return Fail(entry, "no-device",
                Template(_config.ChatMessages.NoDevice, entry, track), true, track);
        if (!device.IsActive && _config.ActivateSelectedDevice)
            await _spotify.TransferPlaybackAsync(device.Id, cancellationToken);

        if (_config.PlaybackMode == MusicPlaybackMode.AddToQueue)
        {
            await _spotify.AddToQueueAsync(track, device.Id, cancellationToken);
            Console.WriteLine("Track wurde zur Spotify-Warteschlange hinzugefügt.");
            return Accept(entry, track, Template(
                _config.ChatMessages.Queued, entry, track,
                queuePosition: (open.Length + 1).ToString()));
        }

        await _spotify.PlayAsync(track, device.Id, cancellationToken);
        Console.WriteLine("Spotify-Track wird sofort abgespielt.");
        return Accept(entry, track, Template(
            _config.ChatMessages.Playing, entry, track));
    }

    private MusicRequestResult CreateValidationFailure(
        string reason, MusicRequestEntry entry, SpotifyTrack track) =>
        reason switch
        {
            "too-long" => Reject(entry, reason, Template(
                _config.ChatMessages.TooLong, entry, track), track),
            "explicit" => Reject(entry, reason, Template(
                _config.ChatMessages.ExplicitBlocked, entry, track), track),
            "blacklist" => Reject(entry, reason, Template(
                _config.ChatMessages.Blacklisted, entry, track), track),
            "duplicate" => Reject(entry, reason,
                $"❌ @{entry.DisplayName}, dieser Song ist bereits in der Warteschlange.", track),
            _ => Reject(entry, reason, _config.ChatMessages.NotFound, track)
        };

    private SpotifyDevice? SelectDevice(IReadOnlyList<SpotifyDevice> devices)
    {
        if (_config.UseActiveDevice)
            return devices.FirstOrDefault(device => device.IsActive) ??
                   devices.FirstOrDefault();
        return devices.FirstOrDefault(device =>
            device.Id.Equals(_config.SelectedDeviceId, StringComparison.Ordinal));
    }

    public async Task RetryAsync(
        MusicRequestEntry entry, CancellationToken cancellationToken)
    {
        var retry = new MusicRequestRedemption(
            entry.RedemptionId, entry.RewardId, entry.RewardName,
            entry.UserId, entry.UserLogin, entry.DisplayName,
            entry.UserInput, entry.RedeemedAt, "unfulfilled");
        await ProcessMusicRequestAsync(retry, cancellationToken);
    }

    public async Task ProcessModeratorCommandAsync(
        ChatMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessModeratorCommandCoreAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine("Spotify-Mod-Command fehlgeschlagen: " + exception.Message);
        }
    }

    private async Task ProcessModeratorCommandCoreAsync(
        ChatMessage message, CancellationToken cancellationToken)
    {
        if (!message.IsBroadcaster && !message.IsModerator) return;
        var parts = message.Text.Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var command = parts[0].ToLowerInvariant();
        var commands = _config.ModeratorCommands;
        if (commands.SongEnabled && command == commands.Song)
        {
            var track = await _spotify.GetCurrentTrackAsync(cancellationToken);
            await SendChatOnceAsync(track is null
                ? "🎵 Aktuell läuft kein Spotify-Track."
                : $"🎵 Aktuell läuft: {track.Name} von {track.Artist}.",
                cancellationToken);
        }
        else if (commands.SkipEnabled && command == commands.Skip)
        {
            await _spotify.SkipAsync(cancellationToken);
            Console.WriteLine($"Spotify-Track wurde von {message.UserName} übersprungen.");
        }
        else if (commands.PauseEnabled && command == commands.Pause)
            await _spotify.PauseAsync(cancellationToken);
        else if (commands.ResumeEnabled && command == commands.Resume)
            await _spotify.ResumeAsync(cancellationToken);
        else if (commands.QueueEnabled && command == commands.Queue)
        {
            var entries = await _store.GetEntriesAsync(cancellationToken);
            var count = entries.Count(MusicRequestRules.IsOpen);
            await SendChatOnceAsync(
                $"🎵 Aktuell sind {count} Musikwünsche offen.", cancellationToken);
        }
        else if (commands.RemoveEnabled && command == commands.Remove &&
                 parts.Length == 2 && int.TryParse(parts[1], out var position))
        {
            var entries = (await _store.GetEntriesAsync(cancellationToken))
                .Where(IsOpen).OrderBy(item => item.RedeemedAt).ToArray();
            if (position > 0 && position <= entries.Length)
            {
                var entry = entries[position - 1];
                entry.Status = MusicRequestStatus.Skipped;
                entry.FailureReason = "Von Moderator entfernt";
                await SaveAndNotifyAsync(entry, true, cancellationToken);
                Console.WriteLine(
                    $"Musikwunsch wurde von {message.UserName} entfernt.");
            }
        }
    }

    public Task<IReadOnlyList<MusicRequestEntry>> GetEntriesAsync(
        CancellationToken cancellationToken) =>
        _store.GetEntriesAsync(cancellationToken);

    private async Task TryUpdateRedemptionAsync(
        MusicRequestEntry entry, MusicRequestResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (result.Success && _config.AutoFulfillRedemptions)
            {
                await _twitch.UpdateRedemptionStatusAsync(
                    _broadcasterId, entry.RewardId, entry.RedemptionId,
                    true, cancellationToken);
                Console.WriteLine("Twitch-Einlösung wurde als erfüllt markiert.");
            }
            else if (!result.Success && !result.IsTemporaryFailure &&
                     _config.AutoCancelRejectedRedemptions)
            {
                await _twitch.UpdateRedemptionStatusAsync(
                    _broadcasterId, entry.RewardId, entry.RedemptionId,
                    false, cancellationToken);
                Console.WriteLine("Twitch-Einlösung wurde storniert.");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Twitch-Einlösung konnte nicht aktualisiert werden: " +
                exception.Message);
        }
    }

    private async Task SaveAndNotifyAsync(
        MusicRequestEntry entry, bool processed,
        CancellationToken cancellationToken)
    {
        await _store.AddOrUpdateAsync(entry, processed, cancellationToken);
        RequestUpdated?.Invoke(entry);
    }

    private async Task SendChatOnceAsync(
        string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            await _twitch.SendChatMessageAsync(
                _broadcasterId, _chatUserId, message, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Musikwunsch-Chatantwort konnte nicht gesendet werden: " +
                exception.Message);
        }
    }

    private string Template(
        string template, MusicRequestEntry entry, SpotifyTrack? track,
        string remainingCooldown = "", string queuePosition = "") =>
        template.Replace("{user}", entry.DisplayName, StringComparison.Ordinal)
            .Replace("{track}", track?.Name ?? "", StringComparison.Ordinal)
            .Replace("{artist}", track?.Artist ?? "", StringComparison.Ordinal)
            .Replace("{duration}", track is null ? "" :
                TimeSpan.FromMilliseconds(track.DurationMs).ToString("m\\:ss"),
                StringComparison.Ordinal)
            .Replace("{maxDuration}",
                _config.MaximumTrackDurationMinutes.ToString(),
                StringComparison.Ordinal)
            .Replace("{remainingCooldown}", remainingCooldown,
                StringComparison.Ordinal)
            .Replace("{queuePosition}", queuePosition,
                StringComparison.Ordinal)
            .Replace("{rewardName}", entry.RewardName,
                StringComparison.Ordinal);

    private static MusicRequestResult Accept(
        MusicRequestEntry entry, SpotifyTrack track, string message) =>
        new(true, "", message, track, entry.RedemptionId);

    private static MusicRequestResult Reject(
        MusicRequestEntry entry, string reason, string message,
        SpotifyTrack? track = null) =>
        new(false, reason, message, track, entry.RedemptionId);

    private static MusicRequestResult Fail(
        MusicRequestEntry entry, string reason, string message,
        bool temporary, SpotifyTrack? track = null) =>
        new(false, reason, message, track, entry.RedemptionId, temporary);

    private static bool IsOpen(MusicRequestEntry entry) => entry.Status is
        MusicRequestStatus.Checking or MusicRequestStatus.Accepted or
        MusicRequestStatus.Queued or MusicRequestStatus.Playing or
        MusicRequestStatus.Failed;

    private static string NormalizeUser(string user) =>
        user.Trim().TrimStart('@').ToLowerInvariant();

    private static bool LooksLikeSpotifyInput(string input) =>
        input.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase) ||
        Uri.TryCreate(input, UriKind.Absolute, out _);

    public static async Task<string?> ResolveSpotifyTrackIdAsync(
        string input, CancellationToken cancellationToken = default)
    {
        if (TryExtractSpotifyTrackId(input, out var trackId))
            return trackId;

        if (!TryCreateSpotifyShortLink(input, out var shortLink))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, shortLink);
        using var response = await SpotifyLinkClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode ||
            response.RequestMessage?.RequestUri is not { } resolvedUri)
            return null;

        return TryExtractSpotifyTrackId(
            resolvedUri.AbsoluteUri, out trackId)
            ? trackId
            : null;
    }

    public static bool TryExtractSpotifyTrackId(
        string input, out string trackId)
    {
        trackId = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (trimmed.StartsWith("spotify:track:",
                StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(':');
            if (parts.Length == 3) trackId = parts[2];
        }
        else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                 uri.Scheme == Uri.UriSchemeHttps &&
                 uri.Host.Equals("open.spotify.com",
                     StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            var trackSegment = Array.FindIndex(
                segments,
                segment => segment.Equals(
                    "track", StringComparison.OrdinalIgnoreCase));
            if (trackSegment >= 0 && trackSegment + 1 < segments.Length)
                trackId = segments[trackSegment + 1];
        }
        if (trackId.Length != 22 ||
            trackId.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            trackId = "";
            return false;
        }
        return true;
    }

    private static bool TryCreateSpotifyShortLink(
        string input, out Uri shortLink)
    {
        shortLink = null!;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2048 ||
            !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (!uri.Host.Equals("spotify.link", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Equals("www.spotify.link", StringComparison.OrdinalIgnoreCase))
            return false;

        shortLink = uri;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.Writer.TryComplete();
        _processingLock.Dispose();
    }
}
