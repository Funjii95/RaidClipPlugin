using Microsoft.Extensions.Configuration;
using System.Net;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
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
            UiTheme = config.UiTheme,
            TwitchChannel = config.Twitch.BroadcasterLogin,
            ObsHost = config.OBS.Host,
            ObsPort = config.OBS.Port,
            ObsPassword = config.OBS.Password,
            ClipLookbackDays = config.Twitch.ClipLookbackDays,
            RetryAttempts = config.Twitch.ClipRetryAttempts,
            MaxClipDurationSeconds = config.Player.DurationSeconds,
            VolumePercent = config.Player.VolumePercent,
            RaidCooldownMinutes = config.Twitch.RaidCooldownMinutes,
            RaidDelaySeconds = config.Twitch.RaidDelaySeconds,
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
            LiveChat = config.LiveChat,
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
            Heist = config.Heist,
            Duel = config.Duel,
            Commands = config.Commands,
            MusicRequests = config.MusicRequests,
            StreamCheck = config.StreamCheck,
            ClipCommand = config.ClipCommand,
            DiscordClips = config.DiscordClips,
            Giveaways = config.Giveaways,
            ModuleHealth = config.ModuleHealth
        };




        File.WriteAllText(
            UserSettingsPath,
            JsonSerializer.Serialize(settings, JsonOptions));
    }




    public void SaveSpotifyConnectionSettings(MusicRequestConfig musicRequests)
    {
        ArgumentNullException.ThrowIfNull(musicRequests);




        NormalizeMusicRequests(musicRequests);
        if (string.IsNullOrWhiteSpace(musicRequests.SpotifyClientId))
        {
            throw new InvalidOperationException(
                "Bitte eine Spotify Client-ID eingeben.");
        }




        if (!Uri.TryCreate(
                musicRequests.RedirectUri,
                UriKind.Absolute,
                out var redirect) ||
            redirect.Scheme != Uri.UriSchemeHttp ||
            !(redirect.Host.Equals(
                  "localhost",
                  StringComparison.OrdinalIgnoreCase) ||
              IPAddress.TryParse(
                  redirect.Host,
                  out var redirectAddress) &&
              IPAddress.IsLoopback(redirectAddress)))
        {
            throw new InvalidOperationException(
                "Der Spotify-Redirect muss eine lokale HTTP-Adresse sein.");
        }




        GuiSettings settings;
        try
        {
            settings = File.Exists(UserSettingsPath)
                ? JsonSerializer.Deserialize<GuiSettings>(
                      File.ReadAllText(UserSettingsPath),
                      JsonOptions) ??
                  new GuiSettings()
                : new GuiSettings();
        }
        catch (JsonException)
        {
            settings = new GuiSettings();
        }




        settings.MusicRequests = musicRequests;




        File.WriteAllText(
            UserSettingsPath,
            JsonSerializer.Serialize(settings, JsonOptions));
    }




    public void SaveMusicRequestSettings(MusicRequestConfig musicRequests)
    {
        ArgumentNullException.ThrowIfNull(musicRequests);


        NormalizeMusicRequests(musicRequests);
        ValidateMusicRequestSettings(musicRequests);


        GuiSettings settings;
        try
        {
            settings = File.Exists(UserSettingsPath)
                ? JsonSerializer.Deserialize<GuiSettings>(
                      File.ReadAllText(UserSettingsPath),
                      JsonOptions) ??
                  new GuiSettings()
                : new GuiSettings();
        }
        catch (JsonException)
        {
            settings = new GuiSettings();
        }


        settings.MusicRequests = musicRequests;


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




            if (!string.IsNullOrWhiteSpace(settings.UiTheme))
            {
                config.UiTheme = settings.UiTheme;
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




            if (settings.RaidDelaySeconds is not null)
            {
                config.Twitch.RaidDelaySeconds =
                    settings.RaidDelaySeconds.Value;
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




            if (settings.Minigame is not null)
                config.Minigame = settings.Minigame;


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
            if (settings.LiveChat is not null)
                config.LiveChat = settings.LiveChat;
            if (settings.Heist is not null)
                config.Heist = settings.Heist;
            if (settings.Duel is not null)
                config.Duel = settings.Duel;
            if (settings.Commands is not null)
                config.Commands = settings.Commands;
            if (settings.MusicRequests is not null)
                config.MusicRequests = settings.MusicRequests;
            if (settings.StreamCheck is not null)
                config.StreamCheck = settings.StreamCheck;
            if (settings.ClipCommand is not null)
                config.ClipCommand = settings.ClipCommand;
            if (settings.DiscordClips is not null)
                config.DiscordClips = settings.DiscordClips;
            if (settings.Giveaways is not null)
                config.Giveaways = settings.Giveaways;
            if (settings.ModuleHealth is not null)
                config.ModuleHealth = settings.ModuleHealth;
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
        config.UiTheme = (config.UiTheme ?? "RaidRed").Trim();
        if (config.UiTheme is not ("RaidRed" or "DarkPurple" or "DarkBlue" or "LightModern" or "NeonGreen" or "TwitchPurple"))
            config.UiTheme = "RaidRed";
        config.Twitch.BroadcasterLogin =
            (config.Twitch.BroadcasterLogin ?? "").Trim().TrimStart('@');
        config.OBS.Host = (config.OBS.Host ?? "").Trim();
        config.Player.BlacklistedClipIds =
            (config.Player.BlacklistedClipIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.Chat ??= new ChatConfig();
        var defaultChat = new ChatConfig();
        config.Chat.RaidMessageTemplate = NormalizeOptionalText(
            config.Chat.RaidMessageTemplate,
            defaultChat.RaidMessageTemplate);
        config.Update.SkippedVersion =
            (config.Update.SkippedVersion ?? "").Trim();
        config.StreamCheck ??= new StreamCheckConfig();
        config.ClipCommand ??= new ClipCommandConfig();
        config.DiscordClips ??= new DiscordClipsConfig();
        config.Giveaways ??= new GiveawayConfig();
        config.ModuleHealth ??= new ModuleHealthConfig();
        config.ModuleHealth.IntervalSeconds = Math.Clamp(
            config.ModuleHealth.IntervalSeconds, 5, 600);
        config.ModuleHealth.MaxRestartAttempts = Math.Clamp(
            config.ModuleHealth.MaxRestartAttempts, 1, 20);
        config.ModuleHealth.RestartCooldownSeconds = Math.Clamp(
            config.ModuleHealth.RestartCooldownSeconds, 5, 3600);
        config.ModuleHealth.RestartWindowMinutes = Math.Clamp(
            config.ModuleHealth.RestartWindowMinutes, 1, 60);
        config.LiveChat = LiveChatService.NormalizeConfig(config.LiveChat);
        config.Duel ??= new DuelConfig();
        NormalizeClipSettings(config.ClipCommand, config.DiscordClips);
        NormalizeGiveawaySettings(config.Giveaways);
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
        NormalizeHeistSettings(config.Heist);
        NormalizeDuelSettings(config.Duel);
        config.Commands.Command = NormalizeCommand(config.Commands.Command);
        config.Commands.ExportDirectory = (config.Commands.ExportDirectory ?? "exports").Trim();
        config.Commands.CommandRoleOverrides = (config.Commands.CommandRoleOverrides ??
            new Dictionary<string, string>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key.Trim(), item =>
                CommandRegistry.ParseRole(item.Value).ToString(), StringComparer.OrdinalIgnoreCase);
        config.Commands.CommandEnabledOverrides = (config.Commands.CommandEnabledOverrides ??
            new Dictionary<string, bool>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key.Trim(), item => item.Value,
                StringComparer.OrdinalIgnoreCase);
        config.Commands.CustomCommands ??= new List<CustomChatCommandConfig>();
        foreach (var custom in config.Commands.CustomCommands)
        {
            custom.Id = string.IsNullOrWhiteSpace(custom.Id)
                ? Guid.NewGuid().ToString("N") : custom.Id.Trim();
            custom.Command = NormalizeCommand(custom.Command);
            custom.Aliases = (custom.Aliases ?? new List<string>())
                .Select(NormalizeCommand).Where(alias => alias.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            custom.Response = (custom.Response ?? "").Trim();
            custom.RequiredRole = CommandRegistry.ParseRole(custom.RequiredRole).ToString();
        }
        NormalizeMusicRequests(config.MusicRequests);
        NormalizeGambleRanges(config.Minigame);
    }




    private static void NormalizeGambleRanges(MinigameConfig minigame)
    {
        var defaults = MinigameConfig.CreateDefaultRanges();
        var ranges = minigame.GambleRanges is { Count: 4 }
            ? minigame.GambleRanges
            : defaults;


        minigame.GambleRanges = ranges
            .Select((range, index) =>
            {
                var clone = CloneRange(range);
                var fallback = index < defaults.Count
                    ? defaults[index].ChatText
                    : defaults[^1].ChatText;
                clone.ChatText = NormalizeOptionalText(clone.ChatText, fallback);
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




        if (config.Twitch.RaidDelaySeconds is < 0 or >
            RaidDelayService.MaximumDelaySeconds)
        {
            throw new InvalidOperationException(
                $"Die Raid-Verzögerung muss zwischen 0 und " +
                $"{RaidDelayService.MaximumDelaySeconds} Sekunden liegen.");
        }




        if (config.Moderation.TimeoutSeconds is < 1 or > 1_209_600)
        {
            throw new InvalidOperationException(
                "Der Moderations-Timeout muss zwischen 1 und 1209600 Sekunden liegen.");
        }




        ValidateMinigameSettings(config.Minigame);
        ValidateMusicRequestSettings(config.MusicRequests);
        ValidateClipSettings(config.ClipCommand, config.DiscordClips);
        ValidateGiveawaySettings(config.Giveaways);
        ValidateDuelSettings(config.Duel);
        ValidateHeistAndCommands(config);
        var pointCommands = new List<string>();
        if (config.Minigame.PointsCommandPunkteEnabled) pointCommands.Add("!punkte");
        if (config.Minigame.PointsCommandPointsEnabled) pointCommands.Add("!points");
        if (config.Minigame.PointsCommandPerlenEnabled) pointCommands.Add("!perlen");
        if (!string.IsNullOrWhiteSpace(config.Minigame.CustomPointsCommand))
            pointCommands.Add(config.Minigame.CustomPointsCommand);
        IEnumerable<string> musicCommands = Enumerable.Empty<string>();
        if (config.MusicRequests.Enabled)
        {
            musicCommands = new[]
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
        }
        if (pointCommands.Intersect(musicCommands,
                StringComparer.OrdinalIgnoreCase).Any())
            throw new InvalidOperationException(
                "Ein Musik-Command kollidiert mit einem Punkte-Command.");
        var clipCommands = new[] { config.ClipCommand.Command }
            .Concat(config.ClipCommand.Aliases);
        if (clipCommands.Intersect(pointCommands.Concat(musicCommands),
                StringComparer.OrdinalIgnoreCase).Any())
            throw new InvalidOperationException(
                "Ein Clip-Command kollidiert mit einem bestehenden Chat-Command.");
        var giveawayCommands = GetGiveawayCommands(config.Giveaways);
        if (giveawayCommands.Intersect(
                pointCommands.Concat(musicCommands).Concat(clipCommands),
                StringComparer.OrdinalIgnoreCase).Any())
            throw new InvalidOperationException(
                "Ein Giveaway-Command kollidiert mit einem bestehenden Chat-Command.");




        if (config.Chat.SendRaidMessage &&
            string.IsNullOrWhiteSpace(config.Chat.RaidMessageTemplate))
        {
            throw new InvalidOperationException(
                "Bitte eine Raid-Chatnachricht eingeben.");
        }
    }




    public static void ValidateDuelSettings(DuelConfig duel)
    {
        NormalizeDuelSettings(duel);
        if (!duel.Enabled)
        {
            return;
        }


        var commands = new[] { duel.DuelCommand, duel.AcceptCommand, duel.DenyCommand };
        if (commands.Any(string.IsNullOrWhiteSpace) ||
            commands.Distinct(StringComparer.OrdinalIgnoreCase).Count() != commands.Length)
            throw new InvalidOperationException("Duel-, Accept- und Deny-Command müssen gültig und unterschiedlich sein.");
        if (duel.MinimumBet <= 0)
            throw new InvalidOperationException("Der Duel-Mindesteinsatz muss größer als 0 sein.");
        if (duel.MaximumBet < duel.MinimumBet || duel.MaximumBet > 9_000_000_000L)
            throw new InvalidOperationException("Der Duel-Maximaleinsatz muss mindestens dem Mindesteinsatz entsprechen.");
        if (duel.RequestTimeoutSeconds is < 10 or > 300)
            throw new InvalidOperationException("Der Duel-Timeout muss zwischen 10 und 300 Sekunden liegen.");
        if (duel.UserCooldownSeconds < 0 || duel.GlobalCooldownSeconds < 0)
            throw new InvalidOperationException("Duel-Cooldowns dürfen nicht negativ sein.");
        if (duel.ChallengerWinChancePercent is < 1 or > 99)
            throw new InvalidOperationException("Die Duel-Gewinnchance muss zwischen 1 und 99 Prozent liegen.");
        if (!(duel.AllowEveryone || duel.AllowFollowers || duel.AllowSubscribers || duel.AllowVips || duel.AllowModerators))
            throw new InvalidOperationException("Bitte mindestens eine Duel-Berechtigung aktivieren.");
        var messages = new[] { duel.DuelRequestMessage, duel.DuelAcceptedMessage, duel.DuelWinMessage,
            duel.DuelDeniedMessage, duel.DuelTimeoutMessage, duel.NotEnoughPointsChallengerMessage,
            duel.NotEnoughPointsTargetMessage, duel.SelfDuelMessage, duel.NoPendingDuelMessage,
            duel.WrongTargetMessage, duel.AlreadyPendingDuelMessage, duel.InvalidBetMessage };
        if (messages.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Alle Duel-Chatnachrichten müssen ausgefüllt sein.");
    }




    public static void ValidateHeistAndCommands(AppConfig config)
    {
        var heist = config.Heist;
        NormalizeHeistSettings(heist);
        if (heist.Enabled)
        {
            if (heist.MinimumParticipants < 3)
                throw new InvalidOperationException("Der Heist benötigt mindestens 3 Teilnehmer.");
            if (heist.MaximumParticipants < heist.MinimumParticipants || heist.MaximumParticipants > 500)
                throw new InvalidOperationException("Die maximale Heist-Teilnehmerzahl muss zwischen Mindestteilnehmern und 500 liegen.");
            if (heist.JoinDurationSeconds is < 10 or > 300)
                throw new InvalidOperationException("Die Heist-Beitrittszeit muss zwischen 10 und 300 Sekunden liegen.");
            if (heist.SuccessChancePercent is < 0 or > 100)
                throw new InvalidOperationException("Die Heist-Erfolgschance muss zwischen 0 und 100 Prozent liegen.");
            if (heist.UserCooldownMinutes < 0 || heist.GlobalCooldownMinutes < 0)
                throw new InvalidOperationException("Heist-Cooldowns dürfen nicht negativ sein.");
            if (string.IsNullOrWhiteSpace(heist.StartCommand) || string.IsNullOrWhiteSpace(heist.JoinCommand) ||
                heist.StartCommand.Equals(heist.JoinCommand, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Heist-Start- und Beitritts-Command müssen unterschiedlich und gültig sein.");
            if (!(heist.AllowEveryone || heist.AllowFollowers || heist.AllowSubscribers || heist.AllowVips || heist.AllowModerators))
                throw new InvalidOperationException("Bitte mindestens eine Heist-Berechtigung aktivieren.");
            var messages = new[] { heist.StartMessage, heist.JoinMessage, heist.AlreadyJoinedMessage,
                heist.NoActiveHeistMessage, heist.MaximumParticipantsMessage, heist.NotEnoughParticipantsMessage,
                heist.EvaluationMessage, heist.SuccessMessage, heist.FailureMessage };
            if (messages.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Alle Heist-Chatnachrichten müssen ausgefüllt sein.");
        }




        var commands = config.Commands;
        if (string.IsNullOrWhiteSpace(commands.Command))
            throw new InvalidOperationException("Der Commands-Command darf nicht leer sein.");
        if (commands.UserCooldownSeconds < 0 || commands.GlobalCooldownSeconds < 0)
            throw new InvalidOperationException("Commands-Cooldowns dürfen nicht negativ sein.");
        if (commands.CommandsPerPage < 1)
            throw new InvalidOperationException("Commands pro Seite muss größer als 0 sein.");
        if (commands.MaximumMessagesPerRequest is < 1 or > 5)
            throw new InvalidOperationException("Maximale Commands-Nachrichten müssen zwischen 1 und 5 liegen.");
        if (commands.CustomCommands.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
            throw new InvalidOperationException("Custom Commands benötigen eindeutige IDs.");
        foreach (var custom in commands.CustomCommands)
        {
            if (string.IsNullOrWhiteSpace(custom.Command) || custom.Command == "!")
                throw new InvalidOperationException("Ein Custom Command darf nicht leer sein.");
            if (custom.Enabled && string.IsNullOrWhiteSpace(custom.Response))
                throw new InvalidOperationException($"Für {custom.Command} fehlt die Chatantwort.");
            if (custom.Response.Length > 480)
                throw new InvalidOperationException($"Die Chatantwort für {custom.Command} darf höchstens 480 Zeichen lang sein.");
            if (custom.UserCooldownSeconds < 0 || custom.GlobalCooldownSeconds < 0)
                throw new InvalidOperationException($"Die Cooldowns für {custom.Command} dürfen nicht negativ sein.");
            if (!Enum.TryParse<CommandRole>(custom.RequiredRole, true, out _))
                throw new InvalidOperationException($"Die Berechtigung für {custom.Command} ist ungültig.");
        }
        if (commands.CommandRoleOverrides.Any(item =>
            !Enum.TryParse<CommandRole>(item.Value, true, out _)))
            throw new InvalidOperationException("Mindestens eine Command-Berechtigung ist ungültig.");




        var registry = new CommandRegistry();
        registry.Update(config);
        var collision = registry.FindCollisions(includeDisabled: false).FirstOrDefault();
        if (collision is not null)
        {
            Console.WriteLine("Command-Kollision: " + collision.Message);
            throw new InvalidOperationException(collision.Message);
        }
    }




    public static void ValidateMinigameSettings(MinigameConfig config)
    {
        if (!config.Enabled && !config.PointsEnabled)
        {
            return;
        }




        if (string.IsNullOrWhiteSpace(config.CurrencySingular) ||
            string.IsNullOrWhiteSpace(config.CurrencyPlural) ||
            config.CurrencySingular.Length > 30 ||
            config.CurrencyPlural.Length > 30)
            throw new InvalidOperationException(
                "Die Währungsnamen dürfen nicht leer und höchstens 30 Zeichen lang sein.");
        if (config.MinimumPoints is < 0 or > 9_000_000_000L ||
            config.MaximumAccountPoints < config.MinimumPoints)
            throw new InvalidOperationException(
                "Mindestpunkte oder maximales Kontolimit sind ungültig.");
        if (config.GlobalCommandCooldownSeconds is < 0 or > 3600)
            throw new InvalidOperationException(
                "Der globale Command-Cooldown muss zwischen 0 und 3600 Sekunden liegen.");
        if (config.HistoryLimit is < 1 or > 10000)
            throw new InvalidOperationException(
                "Das Historienlimit muss zwischen 1 und 10000 liegen.");




        if (config.PointsEnabled)
        {
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
                    "!profil", "!coinflip", "!slots", "!roulette", "!gamble", "!jackpot", "!give",
                    "!addpoints", "!removepoints", "!lurk", "!unlurk"
                };
                if (reservedCommands.Contains(config.CustomPointsCommand))
                    throw new InvalidOperationException(
                        $"Der Command {config.CustomPointsCommand} wird bereits verwendet.");
            }
            if (config.PointsPerInterval is < 0 or > 9_000_000_000L ||
                config.LurkerPointsPerInterval is < 0 or > 9_000_000_000L)
                throw new InvalidOperationException(
                    "Punkte pro Intervall müssen zwischen 0 und 9 Milliarden liegen.");
            if (config.IntervalMinutes is < 1 or > 1440)
                throw new InvalidOperationException(
                    "Das Punkteintervall muss zwischen 1 und 1440 Minuten liegen.");
            if (config.PointsCommandCooldownSeconds is < 0 or > 3600 ||
                config.ChatMessagePointsCooldownSeconds is < 0 or > 3600 ||
                config.LeaderboardCooldownSeconds is < 0 or > 3600 ||
                config.ProfileCooldownSeconds is < 0 or > 3600)
                throw new InvalidOperationException(
                    "Punkte-Cooldowns müssen zwischen 0 und 3600 Sekunden liegen.");
            if (config.ChatMessagePoints is < 0 or > 9_000_000_000L ||
                config.FollowPoints is < 0 or > 9_000_000_000L ||
                config.SubPoints is < 0 or > 9_000_000_000L ||
                config.RaidPoints is < 0 or > 9_000_000_000L ||
                config.ChannelRewardPoints is < 0 or > 9_000_000_000L ||
                config.DailyBonusPoints is < 0 or > 9_000_000_000L)
                throw new InvalidOperationException(
                    "Passive Punkte und Daily müssen zwischen 0 und 9 Milliarden liegen.");
            if (config.MaximumTopEntries is < 1 or > 100)
                throw new InvalidOperationException(
                    "Die Anzahl der Top-Einträge muss zwischen 1 und 100 liegen.");
        }




        if (!config.Enabled)
        {
            return;
        }




        if (config.GambleCooldownSeconds is < 0 or > 3600 ||
            config.CoinflipCooldownSeconds is < 0 or > 3600 ||
            config.SlotsCooldownSeconds is < 0 or > 3600 ||
            config.RouletteCooldownSeconds is < 0 or > 3600)
            throw new InvalidOperationException(
                "Minigame-Cooldowns müssen zwischen 0 und 3600 Sekunden liegen.");
        if (config.MinimumBet < 0 || config.MaximumBet < config.MinimumBet ||
            config.MaximumBet > 9_000_000_000L)
            throw new InvalidOperationException(
                "Minimale und maximale Einsätze sind ungültig.");
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
        if (config.DailyGambleLimit < 0 ||
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
