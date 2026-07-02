using Microsoft.Extensions.Configuration;
using System.Net;
using RaidClipPlugin.Config;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string UserSettingsPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "RaidClipPlugin");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "settings.json");
        }
    }

    public AppConfig Load()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/config.template.json", optional: false)
            .Build();

        var appConfig = new AppConfig();
        configuration.Bind(appConfig);

        var credentials = new TwitchCredentialStore().Load();
        appConfig.Twitch.ClientId = credentials.ClientId;
        appConfig.Twitch.ClientSecret = credentials.ClientSecret;

        if (string.IsNullOrWhiteSpace(appConfig.Twitch.BroadcasterLogin))
        {
            appConfig.Twitch.BroadcasterLogin = "Funjii";
        }

        if (string.IsNullOrWhiteSpace(appConfig.OBS.Host))
        {
            appConfig.OBS.Host = "127.0.0.1";
        }

        ApplySavedGuiSettings(appConfig);
        Normalize(appConfig);
        ValidateTechnicalSettings(appConfig);
        return appConfig;
    }

    public void SaveGuiSettings(AppConfig config)
    {
        Normalize(config);
        ValidateTechnicalSettings(config);
        ValidateGuiSettings(config);

        var settings = new GuiSettings
        {
            TwitchChannel = config.Twitch.BroadcasterLogin,
            ObsHost = config.OBS.Host,
            ObsPort = config.OBS.Port,
            ObsPassword = config.OBS.Password,
            ClipLookbackDays = config.Twitch.ClipLookbackDays,
            RetryAttempts = config.Twitch.ClipRetryAttempts,
            MaxClipDurationSeconds = config.Player.DurationSeconds,
            VolumePercent = config.Player.VolumePercent,
            RaidCooldownMinutes = config.Twitch.RaidCooldownMinutes,
            BlacklistedClipIds = config.Player.BlacklistedClipIds,
            SendRaidMessage = config.Chat.SendRaidMessage,
            SendShoutout = config.Chat.SendShoutout,
            RaidMessageTemplate = config.Chat.RaidMessageTemplate,
            AutoUpdateEnabled = config.Update.Enabled,
            SkippedUpdateVersion = config.Update.SkippedVersion,
            ModerationEnabled = config.Moderation.Enabled,
            ShowChatMessagesInLog = config.Moderation.ShowMessagesInLog,
            AutoFilterEnabled = config.Moderation.AutoFilterEnabled,
            WhitelistModsAndVips = config.Moderation.WhitelistModsAndVips,
            ModerationTimeoutSeconds = config.Moderation.TimeoutSeconds,
            BlockedWords = config.Moderation.BlockedWords,
            MinigameEnabled = config.Minigame.Enabled,
            PointsEnabled = config.Minigame.PointsEnabled,
            PointsPerInterval = config.Minigame.PointsPerInterval,
            PointsIntervalMinutes = config.Minigame.IntervalMinutes,
            MinimumPoints = config.Minigame.MinimumPoints,
            PointsCommandCooldownSeconds =
                config.Minigame.PointsCommandCooldownSeconds,
            GambleEnabled = config.Minigame.GambleEnabled,
            GambleCooldownSeconds = config.Minigame.GambleCooldownSeconds,
            GlobalCommandCooldownSeconds =
                config.Minigame.GlobalCommandCooldownSeconds,
            MinimumBet = config.Minigame.MinimumBet,
            MaximumBet = config.Minigame.MaximumBet,
            GambleRanges = config.Minigame.GambleRanges
                .Select(CloneRange)
                .ToList(),
            Minigame = config.Minigame,
            MusicRequests = config.MusicRequests,
            StreamCheck = config.StreamCheck
        };

        File.WriteAllText(
            UserSettingsPath,
            JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void ApplySavedGuiSettings(AppConfig config)
    {
        if (!File.Exists(UserSettingsPath))
        {
            return;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<GuiSettings>(
                File.ReadAllText(UserSettingsPath),
                JsonOptions);

            if (settings is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(settings.TwitchChannel))
            {
                config.Twitch.BroadcasterLogin = settings.TwitchChannel;
            }

            if (!string.IsNullOrWhiteSpace(settings.ObsHost))
            {
                config.OBS.Host = settings.ObsHost;
            }

            if (settings.ObsPort is not null)
            {
                config.OBS.Port = settings.ObsPort.Value;
            }

            if (settings.ObsPassword is not null)
            {
                config.OBS.Password = settings.ObsPassword;
            }

            if (settings.ClipLookbackDays is not null)
            {
                config.Twitch.ClipLookbackDays = settings.ClipLookbackDays.Value;
            }

            if (settings.RetryAttempts is not null)
            {
                config.Twitch.ClipRetryAttempts = settings.RetryAttempts.Value;
            }

            if (settings.MaxClipDurationSeconds is not null)
            {
                config.Player.DurationSeconds =
                    settings.MaxClipDurationSeconds.Value;
            }

            if (settings.VolumePercent is not null)
            {
                config.Player.VolumePercent = settings.VolumePercent.Value;
            }

            if (settings.RaidCooldownMinutes is not null)
            {
                config.Twitch.RaidCooldownMinutes =
                    settings.RaidCooldownMinutes.Value;
            }

            if (settings.BlacklistedClipIds is not null)
            {
                config.Player.BlacklistedClipIds = settings.BlacklistedClipIds;
            }

            if (settings.SendRaidMessage is not null)
            {
                config.Chat.SendRaidMessage = settings.SendRaidMessage.Value;
            }

            if (settings.SendShoutout is not null)
            {
                config.Chat.SendShoutout = settings.SendShoutout.Value;
            }

            if (!string.IsNullOrWhiteSpace(settings.RaidMessageTemplate))
            {
                config.Chat.RaidMessageTemplate = settings.RaidMessageTemplate;
            }

            if (settings.AutoUpdateEnabled is not null)
            {
                config.Update.Enabled = settings.AutoUpdateEnabled.Value;
            }

            if (settings.SkippedUpdateVersion is not null)
            {
                config.Update.SkippedVersion =
                    settings.SkippedUpdateVersion;
            }

            if (settings.ModerationEnabled is not null)
            {
                config.Moderation.Enabled = settings.ModerationEnabled.Value;
            }

            if (settings.ShowChatMessagesInLog is not null)
            {
                config.Moderation.ShowMessagesInLog =
                    settings.ShowChatMessagesInLog.Value;
            }

            if (settings.AutoFilterEnabled is not null)
            {
                config.Moderation.AutoFilterEnabled =
                    settings.AutoFilterEnabled.Value;
            }

            if (settings.WhitelistModsAndVips is not null)
            {
                config.Moderation.WhitelistModsAndVips =
                    settings.WhitelistModsAndVips.Value;
            }

            if (settings.ModerationTimeoutSeconds is not null)
            {
                config.Moderation.TimeoutSeconds =
                    settings.ModerationTimeoutSeconds.Value;
            }

            if (settings.BlockedWords is not null)
            {
                config.Moderation.BlockedWords = settings.BlockedWords;
            }

            if (settings.MinigameEnabled is not null)
                config.Minigame.Enabled = settings.MinigameEnabled.Value;
            if (settings.PointsEnabled is not null)
                config.Minigame.PointsEnabled = settings.PointsEnabled.Value;
            if (settings.PointsPerInterval is not null)
                config.Minigame.PointsPerInterval = settings.PointsPerInterval.Value;
            if (settings.PointsIntervalMinutes is not null)
                config.Minigame.IntervalMinutes = settings.PointsIntervalMinutes.Value;
            if (settings.MinimumPoints is not null)
                config.Minigame.MinimumPoints = settings.MinimumPoints.Value;
            if (settings.PointsCommandCooldownSeconds is not null)
                config.Minigame.PointsCommandCooldownSeconds =
                    settings.PointsCommandCooldownSeconds.Value;
            if (settings.GambleEnabled is not null)
                config.Minigame.GambleEnabled = settings.GambleEnabled.Value;
            if (settings.GambleCooldownSeconds is not null)
                config.Minigame.GambleCooldownSeconds =
                    settings.GambleCooldownSeconds.Value;
            if (settings.GlobalCommandCooldownSeconds is not null)
                config.Minigame.GlobalCommandCooldownSeconds =
                    settings.GlobalCommandCooldownSeconds.Value;
            if (settings.MinimumBet is not null)
                config.Minigame.MinimumBet = settings.MinimumBet.Value;
            if (settings.MaximumBet is not null)
                config.Minigame.MaximumBet = settings.MaximumBet.Value;
            if (settings.GambleRanges is { Count: > 0 })
                config.Minigame.GambleRanges = settings.GambleRanges
                    .Select(CloneRange)
                    .ToList();
            if (settings.Minigame is not null)
                config.Minigame = settings.Minigame;
            if (settings.MusicRequests is not null)
                config.MusicRequests = settings.MusicRequests;
            if (settings.StreamCheck is not null)
                config.StreamCheck = settings.StreamCheck;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "⚠️ Gespeicherte GUI-Einstellungen konnten nicht geladen werden: " +
                exception.Message);
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.Twitch.BroadcasterLogin =
            (config.Twitch.BroadcasterLogin ?? "").Trim().TrimStart('@');
        config.OBS.Host = (config.OBS.Host ?? "").Trim();
        config.Player.BlacklistedClipIds =
            (config.Player.BlacklistedClipIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.Chat.RaidMessageTemplate =
            (config.Chat.RaidMessageTemplate ?? "").Trim();
        config.Update.SkippedVersion =
            (config.Update.SkippedVersion ?? "").Trim();
        config.StreamCheck ??= new StreamCheckConfig();
        config.StreamCheck.DisabledChecks =
            (config.StreamCheck.DisabledChecks ?? new List<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.StreamCheck.StartScene =
            (config.StreamCheck.StartScene ?? "").Trim();
        config.StreamCheck.MicrophoneSource =
            (config.StreamCheck.MicrophoneSource ?? "").Trim();
        config.StreamCheck.DesktopAudioSource =
            (config.StreamCheck.DesktopAudioSource ?? "").Trim();
        config.StreamCheck.RecordingPath =
            (config.StreamCheck.RecordingPath ?? "").Trim();
        config.StreamCheck.LastSummary =
            (config.StreamCheck.LastSummary ?? "").Trim();
        config.StreamCheck.LastFailedChecks ??= new List<string>();
        config.Moderation.BlockedWords =
            (config.Moderation.BlockedWords ?? new List<string>())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.Minigame.SlotSymbols =
            (config.Minigame.SlotSymbols ?? "").Trim();
        config.Minigame.CurrencySingular =
            (config.Minigame.CurrencySingular ?? "").Trim();
        config.Minigame.CurrencyPlural =
            (config.Minigame.CurrencyPlural ?? "").Trim();
        config.Minigame.CustomPointsCommand =
            (config.Minigame.CustomPointsCommand ?? "").Trim().ToLowerInvariant();
        if (config.Minigame.CustomPointsCommand.Length > 0 &&
            !config.Minigame.CustomPointsCommand.StartsWith('!'))
        {
            config.Minigame.CustomPointsCommand =
                "!" + config.Minigame.CustomPointsCommand;
        }
        config.Minigame.PointsBlacklist =
            (config.Minigame.PointsBlacklist ?? new List<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().TrimStart('@').ToLowerInvariant())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        NormalizeMusicRequests(config.MusicRequests);
        config.Minigame.GambleRanges =
            (config.Minigame.GambleRanges is { Count: > 0 }
                ? config.Minigame.GambleRanges
                : MinigameConfig.CreateDefaultRanges())
            .Select(range =>
            {
                var clone = CloneRange(range);
                clone.ChatText = (clone.ChatText ?? "").Trim();
                return clone;
            })
            .ToList();
    }

    private static void ValidateTechnicalSettings(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Twitch.ClientId))
        {
            throw new InvalidOperationException(
                "Twitch.ClientId fehlt in der technischen Basiskonfiguration.");
        }

        if (string.IsNullOrWhiteSpace(config.Twitch.ClientSecret))
        {
            throw new InvalidOperationException(
                "Twitch.ClientSecret fehlt im verschlüsselten Benutzerspeicher.");
        }

        if (string.IsNullOrWhiteSpace(config.Player.BrowserSource))
        {
            throw new InvalidOperationException(
                "Player.BrowserSource fehlt in der technischen Basiskonfiguration.");
        }

        if (config.Player.Port is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                "Player.Port muss zwischen 1024 und 65535 liegen.");
        }
    }

    private static void ValidateGuiSettings(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Twitch.BroadcasterLogin))
        {
            throw new InvalidOperationException(
                "Bitte einen Twitch-Kanal eingeben.");
        }

        if (string.IsNullOrWhiteSpace(config.OBS.Host))
        {
            throw new InvalidOperationException(
                "Bitte einen OBS-Host eingeben.");
        }

        if (config.OBS.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Der OBS-Port muss zwischen 1 und 65535 liegen.");
        }

        if (config.Twitch.ClipLookbackDays is < 1 or > 3650)
        {
            throw new InvalidOperationException(
                "Der Clip-Lookback muss zwischen 1 und 3650 Tagen liegen.");
        }

        if (config.Twitch.ClipRetryAttempts is < 1 or > 10)
        {
            throw new InvalidOperationException(
                "Die Retry-Anzahl muss zwischen 1 und 10 liegen.");
        }

        if (config.Player.DurationSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException(
                "Die maximale Clipdauer muss zwischen 1 und 600 Sekunden liegen.");
        }

        if (config.Player.VolumePercent is < 0 or > 100)
        {
            throw new InvalidOperationException(
                "Die Lautstärke muss zwischen 0 und 100 Prozent liegen.");
        }

        if (config.Twitch.RaidCooldownMinutes is < 0 or > 1440)
        {
            throw new InvalidOperationException(
                "Der Raid-Cooldown muss zwischen 0 und 1440 Minuten liegen.");
        }

        if (config.Moderation.TimeoutSeconds is < 1 or > 1_209_600)
        {
            throw new InvalidOperationException(
                "Der Moderations-Timeout muss zwischen 1 und 1209600 Sekunden liegen.");
        }

        ValidateMinigameSettings(config.Minigame);
        ValidateMusicRequestSettings(config.MusicRequests);
        var pointCommands = new List<string>();
        if (config.Minigame.PointsCommandPunkteEnabled) pointCommands.Add("!punkte");
        if (config.Minigame.PointsCommandPointsEnabled) pointCommands.Add("!points");
        if (config.Minigame.PointsCommandPerlenEnabled) pointCommands.Add("!perlen");
        if (!string.IsNullOrWhiteSpace(config.Minigame.CustomPointsCommand))
            pointCommands.Add(config.Minigame.CustomPointsCommand);
        var musicCommands = new[]
        {
            (config.MusicRequests.ModeratorCommands.SongEnabled,
                config.MusicRequests.ModeratorCommands.Song),
            (config.MusicRequests.ModeratorCommands.SkipEnabled,
                config.MusicRequests.ModeratorCommands.Skip),
            (config.MusicRequests.ModeratorCommands.QueueEnabled,
                config.MusicRequests.ModeratorCommands.Queue),
            (config.MusicRequests.ModeratorCommands.RemoveEnabled,
                config.MusicRequests.ModeratorCommands.Remove),
            (config.MusicRequests.ModeratorCommands.PauseEnabled,
                config.MusicRequests.ModeratorCommands.Pause),
            (config.MusicRequests.ModeratorCommands.ResumeEnabled,
                config.MusicRequests.ModeratorCommands.Resume)
        }.Where(item => item.Item1).Select(item => item.Item2);
        if (pointCommands.Intersect(musicCommands,
                StringComparer.OrdinalIgnoreCase).Any())
            throw new InvalidOperationException(
                "Ein Musik-Command kollidiert mit einem Punkte-Command.");

        if (config.Chat.SendRaidMessage &&
            string.IsNullOrWhiteSpace(config.Chat.RaidMessageTemplate))
        {
            throw new InvalidOperationException(
                "Bitte eine Raid-Chatnachricht eingeben.");
        }
    }

    public static void ValidateMinigameSettings(MinigameConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CurrencySingular) ||
            string.IsNullOrWhiteSpace(config.CurrencyPlural) ||
            config.CurrencySingular.Length > 30 ||
            config.CurrencyPlural.Length > 30)
            throw new InvalidOperationException(
                "Die Währungsnamen dürfen nicht leer und höchstens 30 Zeichen lang sein.");
        if (!config.PointsCommandPunkteEnabled &&
            !config.PointsCommandPointsEnabled &&
            !config.PointsCommandPerlenEnabled &&
            string.IsNullOrWhiteSpace(config.CustomPointsCommand))
            throw new InvalidOperationException(
                "Bitte mindestens einen Command für die Punkteabfrage aktivieren.");
        if (!string.IsNullOrWhiteSpace(config.CustomPointsCommand))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    config.CustomPointsCommand,
                    "^![a-z0-9_-]{1,29}$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidOperationException(
                    "Der eigene Punkte-Command darf nur Buchstaben, Zahlen, Bindestrich und Unterstrich enthalten.");

            var reservedCommands = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "!punkte", "!points", "!perlen", "!daily", "!top", "!rang",
                "!profil", "!coinflip", "!slots", "!roulette", "!gamble", "!give",
                "!addpoints", "!lurk", "!unlurk"
            };
            if (reservedCommands.Contains(config.CustomPointsCommand))
                throw new InvalidOperationException(
                    $"Der Command {config.CustomPointsCommand} wird bereits verwendet.");
        }
        if (config.PointsPerInterval is < 0 or > 1_000_000)
            throw new InvalidOperationException(
                "Punkte pro Intervall müssen zwischen 0 und 1000000 liegen.");
        if (config.LurkerPointsPerInterval is < 0 or > 1_000_000)
            throw new InvalidOperationException(
                "Lurker-Punkte pro Intervall müssen zwischen 0 und 1000000 liegen.");
        if (config.IntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException(
                "Das Punkteintervall muss zwischen 1 und 1440 Minuten liegen.");
        if (config.MinimumPoints is < 0 or > 1_000_000_000)
            throw new InvalidOperationException(
                "Mindestpunkte müssen zwischen 0 und 1000000000 liegen.");
        if (config.PointsCommandCooldownSeconds is < 0 or > 3600 ||
            config.GambleCooldownSeconds is < 0 or > 3600 ||
            config.GlobalCommandCooldownSeconds is < 0 or > 3600)
            throw new InvalidOperationException(
                "Command-Cooldowns müssen zwischen 0 und 3600 Sekunden liegen.");
        if (config.MinimumBet < 0 || config.MaximumBet < config.MinimumBet ||
            config.MaximumBet > 1_000_000_000)
            throw new InvalidOperationException(
                "Minimale und maximale Einsätze sind ungültig.");
        if (config.ChatMessagePoints is < 0 or > 1_000_000 ||
            config.FollowPoints is < 0 or > 1_000_000 ||
            config.SubPoints is < 0 or > 1_000_000 ||
            config.RaidPoints is < 0 or > 1_000_000 ||
            config.ChannelRewardPoints is < 0 or > 1_000_000 ||
            config.DailyBonusPoints is < 0 or > 1_000_000)
            throw new InvalidOperationException(
                "Passive Punkte und Daily müssen zwischen 0 und 1000000 liegen.");
        if (config.ChatMessagePointsCooldownSeconds is < 0 or > 3600 ||
            config.LeaderboardCooldownSeconds is < 0 or > 3600 ||
            config.ProfileCooldownSeconds is < 0 or > 3600 ||
            config.CoinflipCooldownSeconds is < 0 or > 3600 ||
            config.SlotsCooldownSeconds is < 0 or > 3600 ||
            config.RouletteCooldownSeconds is < 0 or > 3600)
            throw new InvalidOperationException(
                "Minigame-Cooldowns müssen zwischen 0 und 3600 Sekunden liegen.");
        if (config.MaximumTopEntries is < 1 or > 100 ||
            config.HistoryLimit is < 1 or > 10000)
            throw new InvalidOperationException(
                "Top-Anzahl oder Historienlimit ist ungültig.");
        if (config.CoinflipMultiplier is < 0 or > 100 ||
            config.SlotsThreeMultiplier is < 0 or > 100 ||
            config.SlotsTwoMultiplier is < 0 or > 100 ||
            config.SlotsSevenMultiplier is < 0 or > 100 ||
            config.RouletteEvenMoneyMultiplier is < 0 or > 100 ||
            config.RouletteNumberMultiplier is < 0 or > 100)
            throw new InvalidOperationException(
                "Casino-Multiplikatoren müssen zwischen 0 und 100 liegen.");
        if (config.CoinflipMinimumBet < 0 ||
            config.CoinflipMaximumBet < config.CoinflipMinimumBet ||
            config.SlotsMinimumBet < 0 ||
            config.SlotsMaximumBet < config.SlotsMinimumBet ||
            config.RouletteMinimumBet < 0 ||
            config.RouletteMaximumBet < config.RouletteMinimumBet)
            throw new InvalidOperationException(
                "Coinflip-, Slots- oder Roulette-Einsätze sind ungültig.");
        if (config.SlotSymbols.Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).Length < 2)
            throw new InvalidOperationException(
                "Bitte mindestens zwei Slot-Symbole eintragen.");
        if (config.JackpotStartValue < 0 ||
            config.JackpotContributionPercent is < 0 or > 100)
            throw new InvalidOperationException(
                "Jackpot-Einstellungen sind ungültig.");
        if (config.MaximumAccountPoints < config.MinimumPoints ||
            config.DailyGambleLimit < 0 ||
            config.DailyLossLimit < 0 || config.DailyWinLimit < 0)
            throw new InvalidOperationException(
                "Minigame-Limits dürfen nicht negativ sein.");
        if (config.GambleRanges.Count != 4)
            throw new InvalidOperationException(
                "Es müssen genau vier Gamble-Ergebnisbereiche vorhanden sein.");

        var ranges = config.GambleRanges.OrderBy(range => range.From).ToArray();
        var expectedFrom = 1;
        foreach (var range in ranges)
        {
            if (range.From != expectedFrom || range.To < range.From ||
                range.To > 100)
                throw new InvalidOperationException(
                    "Gamble-Bereiche müssen 1 bis 100 lückenlos und ohne Überschneidungen abdecken.");
            if (range.Multiplier is < 0 or > 100)
                throw new InvalidOperationException(
                    "Gamble-Multiplikatoren müssen zwischen 0 und 100 liegen.");
            if (string.IsNullOrWhiteSpace(range.ChatText))
                throw new InvalidOperationException(
                    "Jeder Gamble-Bereich benötigt einen Chattext.");
            expectedFrom = range.To + 1;
        }

        if (expectedFrom != 101)
            throw new InvalidOperationException(
                "Gamble-Bereiche müssen bei 100 enden.");
    }

    private static void NormalizeMusicRequests(MusicRequestConfig config)
    {
        config.ChatMessages ??= new MusicRequestChatMessages();
        config.ModeratorCommands ??= new MusicModeratorCommands();
        config.SpotifyClientId = (config.SpotifyClientId ?? "").Trim();
        config.RedirectUri = (config.RedirectUri ?? "").Trim();
        config.SelectedRewardId = (config.SelectedRewardId ?? "").Trim();
        config.SelectedRewardName = (config.SelectedRewardName ?? "").Trim();
        config.SelectedDeviceId = (config.SelectedDeviceId ?? "").Trim();
        config.UserBlacklist = NormalizeList(config.UserBlacklist, true);
        config.ArtistBlacklist = NormalizeList(config.ArtistBlacklist);
        config.TrackBlacklist = NormalizeList(config.TrackBlacklist);
        config.SongTitleBlacklist = NormalizeList(config.SongTitleBlacklist);
        config.BlockedTitleTerms = NormalizeList(config.BlockedTitleTerms);
        config.ModeratorCommands.Song = NormalizeCommand(config.ModeratorCommands.Song);
        config.ModeratorCommands.Skip = NormalizeCommand(config.ModeratorCommands.Skip);
        config.ModeratorCommands.Queue = NormalizeCommand(config.ModeratorCommands.Queue);
        config.ModeratorCommands.Remove = NormalizeCommand(config.ModeratorCommands.Remove);
        config.ModeratorCommands.Pause = NormalizeCommand(config.ModeratorCommands.Pause);
        config.ModeratorCommands.Resume = NormalizeCommand(config.ModeratorCommands.Resume);
    }

    private static List<string> NormalizeList(
        List<string>? values, bool twitchNames = false) =>
        (values ?? new List<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => twitchNames
            ? value.Trim().TrimStart('@').ToLowerInvariant()
            : value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string NormalizeCommand(string? command)
    {
        var normalized = (command ?? "").Trim().ToLowerInvariant();
        return normalized.Length > 0 && !normalized.StartsWith('!')
            ? "!" + normalized
            : normalized;
    }

    public static void ValidateMusicRequestSettings(MusicRequestConfig config)
    {
        if (config.MaximumTrackDurationMinutes is < 1 or > 180)
            throw new InvalidOperationException(
                "Die maximale Songdauer muss zwischen 1 und 180 Minuten liegen.");
        if (config.MaximumQueueLength is < 1 or > 500)
            throw new InvalidOperationException(
                "Die Musikwunsch-Warteschlange muss zwischen 1 und 500 Einträge erlauben.");
        if (config.UserCooldownMinutes is < 0 or > 1440 ||
            config.MaximumRequestsPerUser is < 1 or > 100)
            throw new InvalidOperationException(
                "Cooldown oder Nutzerlimit für Musikwünsche ist ungültig.");
        if (config.Enabled && string.IsNullOrWhiteSpace(config.SpotifyClientId))
            throw new InvalidOperationException(
                "Bitte eine Spotify Client-ID eingeben.");
        if (config.Enabled && string.IsNullOrWhiteSpace(config.SelectedRewardId))
            throw new InvalidOperationException(
                "Bitte eine Twitch-Musikwunsch-Belohnung auswählen oder ihre ID eintragen.");
        if (!Uri.TryCreate(config.RedirectUri, UriKind.Absolute, out var redirect) ||
            redirect.Scheme != Uri.UriSchemeHttp ||
            !(redirect.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              IPAddress.TryParse(redirect.Host, out var redirectAddress) &&
              IPAddress.IsLoopback(redirectAddress)))
            throw new InvalidOperationException(
                "Der Spotify-Redirect muss eine lokale HTTP-Adresse sein.");

        var messages = new[]
        {
            config.ChatMessages.Queued, config.ChatMessages.Playing,
            config.ChatMessages.NotFound, config.ChatMessages.NoDevice,
            config.ChatMessages.TooLong, config.ChatMessages.ExplicitBlocked,
            config.ChatMessages.Cooldown, config.ChatMessages.QueueFull,
            config.ChatMessages.Blacklisted, config.ChatMessages.InvalidInput
        };
        if (messages.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Musikwunsch-Chattexte dürfen nicht leer sein.");
        var enabledCommands = new[]
        {
            (config.ModeratorCommands.SongEnabled, config.ModeratorCommands.Song),
            (config.ModeratorCommands.SkipEnabled, config.ModeratorCommands.Skip),
            (config.ModeratorCommands.QueueEnabled, config.ModeratorCommands.Queue),
            (config.ModeratorCommands.RemoveEnabled, config.ModeratorCommands.Remove),
            (config.ModeratorCommands.PauseEnabled, config.ModeratorCommands.Pause),
            (config.ModeratorCommands.ResumeEnabled, config.ModeratorCommands.Resume)
        };
        if (enabledCommands.Any(item => item.Item1 &&
                string.IsNullOrWhiteSpace(item.Item2)))
            throw new InvalidOperationException(
                "Aktivierte Musik-Commands dürfen nicht leer sein.");

        var commands = enabledCommands.Where(item => item.Item1)
            .Select(item => item.Item2).ToArray();
        if (commands.Any(command =>
                !System.Text.RegularExpressions.Regex.IsMatch(
                    command, "^![a-z0-9_-]{1,29}$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
            commands.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            commands.Length)
            throw new InvalidOperationException(
                "Musik-Commands sind ungültig oder doppelt vergeben.");

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "!punkte", "!points", "!perlen", "!daily", "!top", "!rang",
            "!profil", "!coinflip", "!slots", "!roulette", "!gamble", "!give",
            "!addpoints", "!lurk", "!unlurk"
        };
        if (commands.Any(reserved.Contains))
            throw new InvalidOperationException(
                "Ein Musik-Command kollidiert mit einem bestehenden Command.");
    }

    private static GambleRangeConfig CloneRange(GambleRangeConfig range) =>
        new()
        {
            From = range.From,
            To = range.To,
            Multiplier = range.Multiplier,
            ChatText = range.ChatText
        };

    private sealed class GuiSettings
    {
        public string? TwitchChannel { get; set; }
        public string? ObsHost { get; set; }
        public int? ObsPort { get; set; }
        public string? ObsPassword { get; set; }
        public int? ClipLookbackDays { get; set; }
        public int? RetryAttempts { get; set; }
        public int? MaxClipDurationSeconds { get; set; }
        public int? VolumePercent { get; set; }
        public int? RaidCooldownMinutes { get; set; }
        public List<string>? BlacklistedClipIds { get; set; }
        public bool? SendRaidMessage { get; set; }
        public bool? SendShoutout { get; set; }
        public string? RaidMessageTemplate { get; set; }
        public bool? AutoUpdateEnabled { get; set; }
        public string? SkippedUpdateVersion { get; set; }
        public bool? ModerationEnabled { get; set; }
        public bool? ShowChatMessagesInLog { get; set; }
        public bool? AutoFilterEnabled { get; set; }
        public bool? WhitelistModsAndVips { get; set; }
        public int? ModerationTimeoutSeconds { get; set; }
        public List<string>? BlockedWords { get; set; }
        public bool? MinigameEnabled { get; set; }
        public bool? PointsEnabled { get; set; }
        public int? PointsPerInterval { get; set; }
        public int? PointsIntervalMinutes { get; set; }
        public int? MinimumPoints { get; set; }
        public int? PointsCommandCooldownSeconds { get; set; }
        public bool? GambleEnabled { get; set; }
        public int? GambleCooldownSeconds { get; set; }
        public int? GlobalCommandCooldownSeconds { get; set; }
        public int? MinimumBet { get; set; }
        public int? MaximumBet { get; set; }
        public List<GambleRangeConfig>? GambleRanges { get; set; }
        public MinigameConfig? Minigame { get; set; }
        public MusicRequestConfig? MusicRequests { get; set; }
        public StreamCheckConfig? StreamCheck { get; set; }
    }
}
