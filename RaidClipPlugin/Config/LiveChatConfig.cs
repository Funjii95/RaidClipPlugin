namespace RaidClipPlugin.Config;

public sealed class LiveChatConfig
{
    public bool Enabled { get; set; } = true;
    public bool ShowTimestamps { get; set; } = true;
    public bool ShowBadges { get; set; } = true;
    public bool ShowUserColors { get; set; } = true;
    public bool HideCommands { get; set; }
    public bool HideBotMessages { get; set; }
    public bool ShowSystemMessages { get; set; } = true;
    public bool AutoScroll { get; set; } = true;
    public int MaxMessages { get; set; } = 1000;
    public bool EnableTwitchEmotes { get; set; } = true;
    public bool EnableBttvEmotes { get; set; }
    public bool EnableSevenTvEmotes { get; set; }
    public bool EnableAnimatedEmotes { get; set; } = true;
    public int EmoteSize { get; set; } = 28;
    public bool CacheEmotes { get; set; } = true;
    public int PopoutWidth { get; set; } = 520;
    public int PopoutHeight { get; set; } = 760;
    public int PopoutLeft { get; set; } = -1;
    public int PopoutTop { get; set; } = -1;
    public bool PopoutTopMost { get; set; }
}
