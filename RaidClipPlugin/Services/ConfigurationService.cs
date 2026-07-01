using Microsoft.Extensions.Configuration;
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
            BlockedWords = config.Moderation.BlockedWords
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
        config.Moderation.BlockedWords =
            (config.Moderation.BlockedWords ?? new List<string>())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
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

        if (config.Chat.SendRaidMessage &&
            string.IsNullOrWhiteSpace(config.Chat.RaidMessageTemplate))
        {
            throw new InvalidOperationException(
                "Bitte eine Raid-Chatnachricht eingeben.");
        }
    }

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
    }
}
