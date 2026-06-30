namespace RaidClipPlugin.Config;

public class AppConfig
{
    public TwitchConfig Twitch { get; set; } = new();
    public OBSConfig OBS { get; set; } = new();
    public PlayerConfig Player { get; set; } = new();
}

public class TwitchConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
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
}