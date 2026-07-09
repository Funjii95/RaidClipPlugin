namespace RaidClipPlugin.Config;

public class AppConfig
{
    public string UiTheme { get; set; } = "RaidRed";
    public TwitchConfig Twitch { get; set; } = new();
    public OBSConfig OBS { get; set; } = new();
    public PlayerConfig Player { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public ModerationConfig Moderation { get; set; } = new();
    public LiveChatConfig LiveChat { get; set; } = new();
    public MinigameConfig Minigame { get; set; } = new();
    public HeistConfig Heist { get; set; } = new();
    public DuelConfig Duel { get; set; } = new();
    public CommandsConfig Commands { get; set; } = new();
    public MusicRequestConfig MusicRequests { get; set; } = new();
    public StreamCheckConfig StreamCheck { get; set; } = new();
    public ClipCommandConfig ClipCommand { get; set; } = new();
    public DiscordClipsConfig DiscordClips { get; set; } = new();
    public AutoDiscordClipPosterConfig AutoDiscordClipPoster { get; set; } = new();
    public GiveawayConfig Giveaways { get; set; } = new();
    public UpdateConfig Update { get; set; } = new();
    public ModuleHealthConfig ModuleHealth { get; set; } = new();
}

public class ModuleHealthConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
    public bool AutoRestartEnabled { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartCooldownSeconds { get; set; } = 30;
    public int RestartWindowMinutes { get; set; } = 5;
    public bool GambleHealthcheckEnabled { get; set; } = true;
}

public class TwitchConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string BroadcasterLogin { get; set; } = "";
    public int MinimumRaidViewers { get; set; } = 1;
    public int ClipLookbackDays { get; set; } = 365;
    public int ClipRetryAttempts { get; set; } = 3;
    public int RaidCooldownMinutes { get; set; } = 5;
    public int RaidDelaySeconds { get; set; } = 0;
}

public class OBSConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4455;
    public string Password { get; set; } = "";
}

public class PlayerConfig
{
    public string Scene { get; set; } = "Alerts";
    public string BrowserSource { get; set; } = "RaidClip";
    public int Port { get; set; } = 17891;
    public int DurationSeconds { get; set; } = 30;
    public int VolumePercent { get; set; } = 100;
    public List<string> BlacklistedClipIds { get; set; } = new();
}

public class ChatConfig
{
    public bool SendRaidMessage { get; set; } = true;
    public bool SendShoutout { get; set; } = true;
    public string RaidMessageTemplate { get; set; } =
        "Danke fÃ¼r den Raid, @{name}! Schaut bei https://twitch.tv/{login} vorbei!";
}

public class ModerationConfig
{
    public bool Enabled { get; set; } = false;
    public bool ShowMessagesInLog { get; set; } = false;
    public bool AutoFilterEnabled { get; set; } = false;
    public bool WhitelistModsAndVips { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 600;
    public List<string> BlockedWords { get; set; } = new();
}

public class MinigameConfig
{
    public bool Enabled { get; set; } = false;
    public bool PointsEnabled { get; set; } = true;
    public long PointsPerInterval { get; set; } = 10;
    public long LurkerPointsPerInterval { get; set; } = 5;
    public string CurrencySingular { get; set; } = "Punkt";
    public string CurrencyPlural { get; set; } = "Punkte";
    public bool PointsCommandPunkteEnabled { get; set; } = true;
    public bool PointsCommandPointsEnabled { get; set; } = false;
    public bool PointsCommandPerlenEnabled { get; set; } = false;
    public string CustomPointsCommand { get; set; } = "";
    public List<string> PointsBlacklist { get; set; } = new()
    {
        "nightbot",
        "streamelements",
        "streamlabs",
        "moobot"
    };
    public int IntervalMinutes { get; set; } = 5;
    public long MinimumPoints { get; set; } = 0;
    public int PointsCommandCooldownSeconds { get; set; } = 30;
    public bool GambleEnabled { get; set; } = true;
    public int GambleCooldownSeconds { get; set; } = 20;
    public int GlobalCommandCooldownSeconds { get; set; } = 2;
    public long MinimumBet { get; set; } = 10;
    public long MaximumBet { get; set; } = 1000;
    public bool ChatPointsEnabled { get; set; } = true;
    public long ChatMessagePoints { get; set; } = 1;
    public int ChatMessagePointsCooldownSeconds { get; set; } = 60;
    public bool FollowPointsEnabled { get; set; } = true;
    public long FollowPoints { get; set; } = 50;
    public bool SubPointsEnabled { get; set; } = true;
    public long SubPoints { get; set; } = 250;
    public bool RaidPointsEnabled { get; set; } = true;
    public long RaidPoints { get; set; } = 100;
    public bool ChannelRewardPointsEnabled { get; set; } = true;
    public long ChannelRewardPoints { get; set; } = 25;
    public bool DailyEnabled { get; set; } = true;
    public long DailyBonusPoints { get; set; } = 100;
    public bool LeaderboardEnabled { get; set; } = true;
    public int MaximumTopEntries { get; set; } = 10;
    public int LeaderboardCooldownSeconds { get; set; } = 30;
    public bool ProfileEnabled { get; set; } = true;
    public int ProfileCooldownSeconds { get; set; } = 30;
    public bool HistoryEnabled { get; set; } = true;
    public int HistoryLimit { get; set; } = 500;
    public bool CoinflipEnabled { get; set; } = false;
    public decimal CoinflipMultiplier { get; set; } = 2.0m;
    public long CoinflipMinimumBet { get; set; } = 10;
    public long CoinflipMaximumBet { get; set; } = 1000;
    public int CoinflipCooldownSeconds { get; set; } = 20;
    public bool SlotsEnabled { get; set; } = false;
    public string SlotSymbols { get; set; } = "ðŸ’,ðŸ‹,ðŸ””,â­,ðŸ’Ž,7ï¸âƒ£";
    public decimal SlotsThreeMultiplier { get; set; } = 5.0m;
    public decimal SlotsTwoMultiplier { get; set; } = 1.5m;
    public decimal SlotsSevenMultiplier { get; set; } = 10.0m;
    public long SlotsMinimumBet { get; set; } = 10;
    public long SlotsMaximumBet { get; set; } = 1000;
    public int SlotsCooldownSeconds { get; set; } = 20;
    public bool RouletteEnabled { get; set; } = false;
    public decimal RouletteEvenMoneyMultiplier { get; set; } = 2.0m;
    public decimal RouletteNumberMultiplier { get; set; } = 36.0m;
    public long RouletteMinimumBet { get; set; } = 10;
    public long RouletteMaximumBet { get; set; } = 1000;
    public int RouletteCooldownSeconds { get; set; } = 20;
    public bool JackpotEnabled { get; set; } = false;
    public long JackpotStartValue { get; set; } = 1000;
    public decimal JackpotContributionPercent { get; set; } = 10m;
    public bool MaximumAccountEnabled { get; set; } = false;
    public long MaximumAccountPoints { get; set; } = 1_000_000;
    public bool DailyGambleLimitEnabled { get; set; } = false;
    public int DailyGambleLimit { get; set; } = 100;
    public bool DailyLossLimitEnabled { get; set; } = false;
    public long DailyLossLimit { get; set; } = 10_000;
    public bool DailyWinLimitEnabled { get; set; } = false;
    public long DailyWinLimit { get; set; } = 10_000;
    public List<GambleRangeConfig> GambleRanges { get; set; } =
        CreateDefaultRanges();

    public static List<GambleRangeConfig> CreateDefaultRanges() =>
        new()
        {
            new GambleRangeConfig
            {
                From = 1,
                To = 31,
                Multiplier = 0.0m,
                ChatText = "@{name} wÃ¼rfelt eine {roll} und verliert {stake} Punkte! Neuer Stand: {balance}."
            },
            new GambleRangeConfig
            {
                From = 32,
                To = 50,
                Multiplier = 0.5m,
                ChatText = "@{name} wÃ¼rfelt eine {roll} und erhÃ¤lt {payout} Punkte zurÃ¼ck! Neuer Stand: {balance}."
            },
            new GambleRangeConfig
            {
                From = 51,
                To = 70,
                Multiplier = 1.0m,
                ChatText = "@{name} wÃ¼rfelt eine {roll} und erhÃ¤lt den Einsatz zurÃ¼ck! Neuer Stand: {balance}."
            },
            new GambleRangeConfig
            {
                From = 71,
                To = 100,
                Multiplier = 2.0m,
                ChatText = "@{name} wÃ¼rfelt eine {roll} und gewinnt {payout} Punkte! Neuer Stand: {balance}."
            }
        };
}

public class GambleRangeConfig
{
    public int From { get; set; }
    public int To { get; set; }
    public decimal Multiplier { get; set; }
    public string ChatText { get; set; } = "";
}


public class StreamCheckConfig
{
    public List<string> DisabledChecks { get; set; } = new();
    public string StartScene { get; set; } = "";
    public string MicrophoneSource { get; set; } = "Mic/Aux";
    public string DesktopAudioSource { get; set; } = "Desktop Audio";
    public string RecordingPath { get; set; } = "";
    public int MinimumFreeSpaceGb { get; set; } = 10;
    public bool SelectStartScene { get; set; } = true;
    public bool StartObsStreaming { get; set; } = true;
    public bool StartPluginServices { get; set; } = true;
    public DateTimeOffset? LastCheckUtc { get; set; }
    public long LastDurationMilliseconds { get; set; }
    public string LastSummary { get; set; } = "";
    public List<string> LastFailedChecks { get; set; } = new();
}

public class UpdateConfig
{
    public string ManifestUrl { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string SkippedVersion { get; set; } = "";
}

public enum ClipPosterTimeRange
{
    Last1Hour,
    Last6Hours,
    Last12Hours,
    Last24Hours,
    Last3Days,
    Last7Days,
    Last14Days,
    Last30Days,
    Custom
}

public class AutoDiscordClipPosterConfig
{
    public bool Enabled { get; set; } = false;
    public string BroadcasterLogin { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public string DiscordChannelId { get; set; } = "";
    public int IntervalMinutes { get; set; } = 5;
    public ClipPosterTimeRange TimeRange { get; set; } =
        ClipPosterTimeRange.Last24Hours;
    public DateTimeOffset CustomStart { get; set; } =
        DateTimeOffset.UtcNow.AddDays(-1);
    public DateTimeOffset CustomEnd { get; set; } = DateTimeOffset.UtcNow;
    public int MaxClipsPerCheck { get; set; } = 25;
    public double MinimumDurationSeconds { get; set; } = 0;
    public int MinimumViewCount { get; set; } = 0;
    public bool IgnoreBotCreatedClips { get; set; } = false;
    public string BotName { get; set; } = "raidclipplugin";
    public string IgnoredCreators { get; set; } = "";
    public bool CheckOnPluginStart { get; set; } = true;
    public bool UseEmbed { get; set; } = true;
    public bool UseThumbnail { get; set; } = true;
}

