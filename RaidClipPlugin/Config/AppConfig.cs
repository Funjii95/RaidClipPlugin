namespace RaidClipPlugin.Config;

public class AppConfig
{
    public TwitchConfig Twitch { get; set; } = new();
    public OBSConfig OBS { get; set; } = new();
    public PlayerConfig Player { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public UpdateConfig Update { get; set; } = new();
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
        "Danke für den Raid, @{name}! Schaut bei https://twitch.tv/{login} vorbei!";
}

public class UpdateConfig
{
    public string ManifestUrl { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string SkippedVersion { get; set; } = "";
}
