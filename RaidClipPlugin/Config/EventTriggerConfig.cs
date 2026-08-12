namespace RaidClipPlugin.Config;

public sealed class EventTriggerConfig
{
    public bool Enabled { get; set; } = false;
    public ChatAlertRuleConfig Follow { get; set; } = new()
    {
        Message = "Danke für den Follow, @{user}!"
    };
    public ChatAlertRuleConfig Tip { get; set; } = new()
    {
        Message = "Vielen Dank an {user} für {amount} {currency}!"
    };
    public ChatAlertRuleConfig Subscription { get; set; } = new()
    {
        Message = "Danke für dein Abo, @{user}!"
    };
    public ChatAlertRuleConfig Cheer { get; set; } = new()
    {
        Message = "Danke @{user} für {amount} Bits!"
    };
    public ChatAlertRuleConfig AdBreak { get; set; } = new()
    {
        Message = "Werbepause für {duration} Sekunden – gleich geht es weiter!"
    };
    public TipProviderConfig TipProviders { get; set; } = new();
}

public sealed class ChatAlertRuleConfig
{
    public bool Enabled { get; set; } = false;
    public string Message { get; set; } = "";
    public decimal MinimumAmount { get; set; } = 0;
}

public sealed class TipProviderConfig
{
    public StreamElementsTipConfig StreamElements { get; set; } = new();
    public StreamlabsTipConfig Streamlabs { get; set; } = new();
    public WebhookTipConfig KoFi { get; set; } = new() { Path = "kofi" };
    public WebhookTipConfig TipeeeStream { get; set; } = new() { Path = "tipeeestream" };
}

public sealed class StreamElementsTipConfig
{
    public bool Enabled { get; set; } = false;
    public string ChannelId { get; set; } = "";
    public string Token { get; set; } = "";
    public string TokenType { get; set; } = "jwt";
}

public sealed class StreamlabsTipConfig
{
    public bool Enabled { get; set; } = false;
    public string AccessToken { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 15;
}

public sealed class WebhookTipConfig
{
    public bool Enabled { get; set; } = false;
    public string Path { get; set; } = "";
    public string VerificationToken { get; set; } = "";
}
