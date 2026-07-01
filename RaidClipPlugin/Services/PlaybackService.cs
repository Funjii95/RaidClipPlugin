using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class PlaybackService
{
    private readonly TwitchService _twitch;
    private readonly ObsService _obs;
    private readonly LocalPlayerServer _player;
    private readonly AppConfig _config;
    private readonly ClipHistoryService _history;
    private readonly ClipMediaResolver _mediaResolver = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PlaybackService(
        TwitchService twitch,
        ObsService obs,
        LocalPlayerServer player,
        AppConfig config,
        ClipHistoryService history)
    {
        _twitch = twitch;
        _obs = obs;
        _player = player;
        _config = config;
        _history = history;
    }

    public async Task<bool> PlayRandomClipAsync(
        string broadcasterId,
        string broadcasterName,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            Console.WriteLine();
            Console.WriteLine($"🎬 Suche Clips von {broadcasterName}...");

            var clips = await _twitch.GetClipsAsync(
                broadcasterId,
                _config.Twitch.ClipLookbackDays,
                cancellationToken);

            var blacklist = new HashSet<string>(
                _config.Player.BlacklistedClipIds,
                StringComparer.OrdinalIgnoreCase);
            var blockedCount = clips.Count(clip => blacklist.Contains(clip.Id));

            clips = clips
                .Where(clip => !string.IsNullOrWhiteSpace(clip.Id))
                .Where(clip => !blacklist.Contains(clip.Id))
                .ToList();

            if (blockedCount > 0)
            {
                Console.WriteLine(
                    $"⛔ {blockedCount} Clip(s) durch Blacklist ausgeschlossen.");
            }

            if (clips.Count == 0)
            {
                Console.WriteLine(
                    $"❌ {broadcasterName} hat keine verfügbaren Twitch-Clips.");
                return false;
            }

            Console.WriteLine($"✅ {clips.Count} Twitch-Clips gefunden.");

            var maxAttempts = Math.Min(
                Math.Max(1, _config.Twitch.ClipRetryAttempts),
                clips.Count);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var index = Random.Shared.Next(clips.Count);
                var clip = clips[index];
                clips.RemoveAt(index);

                Console.WriteLine();
                Console.WriteLine(
                    $"▶️ Versuch {attempt}/{maxAttempts}: {clip.Title}");

                var success = await TryPlayClipAsync(
                    clip,
                    broadcasterName,
                    cancellationToken);

                if (success)
                {
                    Console.WriteLine("✅ Clip erfolgreich abgespielt.");
                    return true;
                }

                Console.WriteLine(
                    "⚠️ Clip fehlgeschlagen. Versuche den nächsten...");
            }

            Console.WriteLine(
                "❌ Kein Clip konnte erfolgreich abgespielt werden.");
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> TryPlayClipAsync(
        Clip clip,
        string broadcasterName,
        CancellationToken cancellationToken)
    {
        try
        {
            string? mediaUrl = null;

            try
            {
                mediaUrl = await _mediaResolver.ResolveAsync(
                    clip.Id,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "⚠️ Direkte Medienadresse nicht verfügbar: " +
                    exception.Message);
            }

            string clipUrl;

            if (!string.IsNullOrWhiteSpace(mediaUrl))
            {
                clipUrl = _player.GetClipUrl(
                    mediaUrl,
                    _config.Player.VolumePercent);
                Console.WriteLine("🔊 Direkte MP4-Wiedergabe mit Ton.");
            }
            else
            {
                clipUrl = _player.GetClipUrl(
                    clip,
                    _config.Player.VolumePercent);
                Console.WriteLine(
                    "⚠️ Direkte MP4 nicht verfügbar. Nutze Twitch-Fallback.");
            }

            Console.WriteLine($"🌐 Lade Clip: {clip.Title}");
            Console.WriteLine($"🔗 Twitch-Clip: {clip.Url}");

            _obs.SetBrowserUrl(clipUrl);

            var playbackDuration = clip.DurationSeconds > 0
                ? Math.Min(
                    _config.Player.DurationSeconds,
                    Math.Max(1, clip.DurationSeconds))
                : _config.Player.DurationSeconds;

            Console.WriteLine(
                $"⏱️ Wiedergabedauer: {playbackDuration:0.#} Sekunden");

            await Task.Delay(
                TimeSpan.FromSeconds(playbackDuration),
                cancellationToken);

            _history.Add(
                clip.Id,
                clip.Title,
                broadcasterName,
                "Abgespielt");
            return true;
        }
        catch (OperationCanceledException)
        {
            _history.Add(
                clip.Id,
                clip.Title,
                broadcasterName,
                "Abgebrochen");
            Console.WriteLine("⚠️ Wiedergabe wurde abgebrochen.");
            throw;
        }
        catch (Exception exception)
        {
            _history.Add(
                clip.Id,
                clip.Title,
                broadcasterName,
                "Fehler");
            Console.WriteLine($"❌ Fehler beim Clip: {exception.Message}");
            return false;
        }
        finally
        {
            try
            {
                _obs.SetBrowserUrl(_player.IdleUrl);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "⚠️ OBS konnte nicht zurückgesetzt werden: " +
                    exception.Message);
            }
        }
    }

    public Task<bool> PlayRandomClipAsync(
        TwitchUser user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return PlayRandomClipAsync(
            user.Id,
            user.DisplayName,
            cancellationToken);
    }

    public async Task<bool> PlayRandomClipAsync(
        string broadcasterLogin,
        CancellationToken cancellationToken)
    {
        var user = await _twitch.GetUserAsync(
            broadcasterLogin,
            cancellationToken);

        if (user is null)
        {
            Console.WriteLine(
                $"❌ Twitch-Kanal '{broadcasterLogin}' wurde nicht gefunden.");
            return false;
        }

        return await PlayRandomClipAsync(
            user,
            cancellationToken);
    }
}
