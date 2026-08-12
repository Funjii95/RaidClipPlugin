namespace RaidClipPlugin.Config;

public sealed class ChatTimerConfig
{
    public bool Enabled { get; set; } = false;
    public List<ChatTimerEntryConfig> Entries { get; set; } = new();
}

public sealed class ChatTimerEntryConfig
{
    public bool Enabled { get; set; } = true;
    public string Message { get; set; } = "";
    public int IntervalMinutes { get; set; } = 15;
    public int MinimumViewers { get; set; } = 0;
}
