using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using System.Drawing;

namespace RaidClipPlugin.Services;

public sealed class AutoDiscordClipPosterService : IDisposable
{
    private readonly TwitchService _twitch;
    private readonly AutoDiscordClipPosterStore _store;
    private readonly DiscordWebhookService _discord;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private AutoDiscordClipPosterConfig _config;
    private TwitchUser _broadcaster;
    private bool _disposed;

    public event Action<string, Color>? StatusChanged;

    public AutoDiscordClipPosterService(
        AutoDiscordClipPosterConfig config,
        TwitchUser broadcaster,
        TwitchService twitch,
        Action<string> log)
    {
        _config = config;
        _broadcaster = broadcaster;
        _twitch = twitch;
        _log = log;
        _store = new AutoDiscordClipPosterStore();
        _discord = new DiscordWebhookService();
    }

    public void UpdateConfig(
        AutoDiscordClipPosterConfig config,
        TwitchUser? broadcaster = null)
    {
        _config = config;
        if (broadcaster is not null)
            _broadcaster = broadcaster;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
            return;

        SetStatus("Auto-Poster läuft", Color.LimeGreen);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(Math.Max(1, _config.IntervalMinutes)),
                    cancellationToken);
                await CheckNowAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _log("Auto Discord Clip Poster Fehler: " + exception.Message);
                SetStatus("Auto-Poster Fehler", Color.OrangeRed);
            }
        }
    }

    public async Task<AutoDiscordClipPosterResult> CheckNowAsync(
        CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            SetStatus("Auto-Poster deaktiviert", Color.Gray);
            return new AutoDiscordClipPosterResult(0, 0, 0, 0, 0);
        }

        if (string.IsNullOrWhiteSpace(_config.WebhookUrl))
            throw new InvalidOperationException("Discord Webhook URL fehlt.");

        if (!await _checkLock.WaitAsync(0, cancellationToken))
        {
            _log("Auto Discord Clip Poster: Check läuft bereits, überspringe parallelen Lauf.");
            return new AutoDiscordClipPosterResult(0, 0, 0, 0, 0);
        }

        try
        {
            var broadcaster = await ResolveBroadcasterAsync(cancellationToken);
            var period = ResolvePeriod(_config, DateTimeOffset.UtcNow);
            var maxClips = Math.Clamp(_config.MaxClipsPerCheck, 1, 500);
            var destination = AutoDiscordClipPosterStore.DestinationKey(
                _config.WebhookUrl, _config.DiscordChannelId);

            _log($"Auto Discord Clip Poster prüft Zeitraum: {period.Description}.");
            SetStatus("Prüfe Twitch-Clips …", Color.DarkOrange);

            var clips = await _twitch.GetClipsForBroadcasterAsync(
                broadcaster.Id,
                period.StartedAt.UtcDateTime,
                period.EndedAt.UtcDateTime,
                cancellationToken);

            clips = clips
                .Where(clip => clip.CreatedAt >= period.StartedAt &&
                               clip.CreatedAt <= period.EndedAt)
                .OrderBy(clip => clip.CreatedAt)
                .Take(maxClips)
                .ToList();

            var already = 0;
            var posted = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var clip in clips)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await _store.WasPostedAsync(clip.Id, destination, cancellationToken))
                {
                    already++;
                    continue;
                }

                if (ShouldSkip(clip))
                {
                    skipped++;
                    await _store.SaveAsync(CreateEntry(
                        clip, broadcaster, "", "Skipped"), cancellationToken);
                    continue;
                }

                try
                {
                    var messageId = await _discord.SendClipAsync(
                        _config.WebhookUrl,
                        BuildDiscordPayload(clip, broadcaster),
                        cancellationToken);
                    await _store.SaveAsync(CreateEntry(
                        clip, broadcaster, messageId, "Posted"), cancellationToken);
                    posted++;
                    _log($"Auto Discord Clip Poster: Clip gepostet: {clip.Title} ({clip.Id}).");
                }
                catch (Exception exception)
                {
                    failed++;
                    await _store.SaveAsync(CreateEntry(
                        clip, broadcaster, "", "Failed"), cancellationToken);
                    _log("Auto Discord Clip Poster: Discord-Post fehlgeschlagen: " +
                         SafeError(exception));
                }
            }

            _log(
                $"Auto Discord Clip Poster: {clips.Count} Clips gefunden, " +
                $"{already} bereits gepostet, {posted} neu gepostet, " +
                $"{skipped} übersprungen, {failed} Fehler.");
            SetStatus(posted > 0
                    ? $"Auto-Poster: {posted} neue Clips"
                    : "Auto-Poster: keine neuen Clips",
                failed > 0 ? Color.DarkOrange : Color.LimeGreen);

            return new AutoDiscordClipPosterResult(
                clips.Count, already, posted, skipped, failed);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public async Task SendTestMessageAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.WebhookUrl))
            throw new InvalidOperationException("Discord Webhook URL fehlt.");

        await _discord.SendClipAsync(
            _config.WebhookUrl,
            new
            {
                content =
                    "🎬 RaidClip Auto Discord Clip Poster Testnachricht",
                allowed_mentions = new { parse = Array.Empty<string>() }
            },
            cancellationToken);
        _log("Auto Discord Clip Poster: Testnachricht an Discord gesendet.");
    }

    private async Task<TwitchUser> ResolveBroadcasterAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.BroadcasterLogin))
            return _broadcaster;

        var user = await _twitch.GetUserAsync(
            _config.BroadcasterLogin.Trim(), cancellationToken);
        return user ?? throw new InvalidOperationException(
            "Twitch-Kanal für Auto Discord Clip Poster wurde nicht gefunden.");
    }

    private bool ShouldSkip(Clip clip)
    {
        if (_config.MinimumDurationSeconds > 0 &&
            clip.DurationSeconds < _config.MinimumDurationSeconds)
            return true;

        if (_config.MinimumViewCount > 0 &&
            clip.ViewCount < _config.MinimumViewCount)
            return true;

        if (_config.IgnoreBotCreatedClips &&
            !string.IsNullOrWhiteSpace(clip.CreatorName) &&
            clip.CreatorName.Equals(_config.BotName, StringComparison.OrdinalIgnoreCase))
            return true;

        var ignored = Split(_config.IgnoredCreators);
        return !string.IsNullOrWhiteSpace(clip.CreatorName) &&
               ignored.Contains(clip.CreatorName, StringComparer.OrdinalIgnoreCase);
    }

    private AutoDiscordClipPosterEntry CreateEntry(
        Clip clip,
        TwitchUser broadcaster,
        string messageId,
        string status)
    {
        return new AutoDiscordClipPosterEntry(
            clip.Id,
            broadcaster.Id,
            broadcaster.DisplayName,
            clip.Url,
            clip.Title,
            clip.CreatorName,
            clip.CreatedAt,
            DateTimeOffset.UtcNow,
            _config.WebhookUrl,
            _config.DiscordChannelId,
            messageId,
            status);
    }

    private object BuildDiscordPayload(Clip clip, TwitchUser broadcaster)
    {
        var content =
            $"🎬 Neuer Clip von {Sanitize(broadcaster.DisplayName)}\n" +
            $"**{Sanitize(clip.Title)}**\n" +
            $"Erstellt von: {Sanitize(clip.CreatorName)}\n" +
            $"{clip.Url}";

        var embed = new Dictionary<string, object>
        {
            ["title"] = "🎬 Neuer Twitch-Clip",
            ["description"] =
                $"**{Sanitize(clip.Title)}**\n" +
                $"Erstellt von: {Sanitize(clip.CreatorName)}",
            ["url"] = clip.Url,
            ["color"] = 0x9146FF,
            ["fields"] = new object[]
            {
                new { name = "Twitch-Kanal", value = Sanitize(broadcaster.DisplayName), inline = true },
                new { name = "Erstellt am", value = clip.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), inline = true }
            }
        };

        if (_config.UseThumbnail && !string.IsNullOrWhiteSpace(clip.ThumbnailUrl))
            embed["image"] = new { url = clip.ThumbnailUrl };

        return new
        {
            content = content.Length <= 1900 ? content : content[..1900],
            embeds = _config.UseEmbed ? new object[] { embed } : Array.Empty<object>(),
            allowed_mentions = new { parse = Array.Empty<string>() }
        };
    }

    public static AutoDiscordClipPosterPeriod ResolvePeriod(
        AutoDiscordClipPosterConfig config,
        DateTimeOffset now)
    {
        if (config.TimeRange == ClipPosterTimeRange.Custom)
        {
            var start = config.CustomStart == default
                ? now.AddHours(-24)
                : config.CustomStart.ToUniversalTime();
            var end = config.CustomEnd == default
                ? now
                : config.CustomEnd.ToUniversalTime();
            if (end <= start)
                end = start.AddHours(1);
            return new AutoDiscordClipPosterPeriod(
                start, end,
                $"{start:dd.MM.yyyy HH:mm} bis {end:dd.MM.yyyy HH:mm}");
        }

        var span = config.TimeRange switch
        {
            ClipPosterTimeRange.Last1Hour => TimeSpan.FromHours(1),
            ClipPosterTimeRange.Last6Hours => TimeSpan.FromHours(6),
            ClipPosterTimeRange.Last12Hours => TimeSpan.FromHours(12),
            ClipPosterTimeRange.Last24Hours => TimeSpan.FromHours(24),
            ClipPosterTimeRange.Last3Days => TimeSpan.FromDays(3),
            ClipPosterTimeRange.Last7Days => TimeSpan.FromDays(7),
            ClipPosterTimeRange.Last14Days => TimeSpan.FromDays(14),
            ClipPosterTimeRange.Last30Days => TimeSpan.FromDays(30),
            _ => TimeSpan.FromHours(24)
        };

        return new AutoDiscordClipPosterPeriod(
            now.Subtract(span), now, "letzte " + Describe(span));
    }

    private static string Describe(TimeSpan span)
    {
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours} Stunde(n)";
        return $"{(int)span.TotalDays} Tag(e)";
    }

    private static IReadOnlyList<string> Split(string value) =>
        (value ?? "")
        .Split(new[] { ',', ';', '\n', '\r' },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string Sanitize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Unbekannt"
            : value.Replace("@", "@\u200b").Trim();

    private static string SafeError(Exception exception) =>
        exception is HttpRequestException or InvalidOperationException or TaskCanceledException
            ? exception.Message.Replace("discord.com/api/webhooks", "discord.com/api/webhooks/***")
            : "Discord-Post fehlgeschlagen.";

    private void SetStatus(string text, Color color) =>
        StatusChanged?.Invoke(text, color);

    public void Dispose()
    {
        if (_disposed)
            return;
        _discord.Dispose();
        _checkLock.Dispose();
        _disposed = true;
    }
}
