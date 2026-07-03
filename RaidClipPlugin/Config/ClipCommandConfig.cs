namespace RaidClipPlugin.Config;

public sealed class ClipCommandConfig
{
    public bool Enabled { get; set; } = false;
    public string Command { get; set; } = "!clip";
    public List<string> Aliases { get; set; } = new() { "!createclip" };
    public string DefaultTitle { get; set; } = "Clip von {username}";
    public int DurationSeconds { get; set; } = 30;
    public int MaximumTitleLength { get; set; } = 100;
    public ClipAllowedRolesConfig AllowedRoles { get; set; } = new();
    public List<string> AllowedUsers { get; set; } = new();
    public List<string> BlockedUsers { get; set; } = new();
    public int GlobalCooldownSeconds { get; set; } = 30;
    public int UserCooldownSeconds { get; set; } = 120;
    public int MaximumClipsPerStream { get; set; } = 50;
    public int MaximumClipsPerUserPerStream { get; set; } = 5;
    public bool QueueEnabled { get; set; } = false;
    public int MaximumQueueSize { get; set; } = 5;
    public int DuplicateWindowSeconds { get; set; } = 5;
    public bool ChatResponsesEnabled { get; set; } = true;
    public ClipChatMessages ChatMessages { get; set; } = new();
}

public sealed class ClipAllowedRolesConfig
{
    public bool Broadcaster { get; set; } = true;
    public bool Moderators { get; set; } = true;
    public bool Vips { get; set; } = true;
    public bool Subscribers { get; set; } = false;
    public bool Followers { get; set; } = false;
    public bool Everyone { get; set; } = false;
}

public sealed class ClipChatMessages
{
    public ClipChatMessage Starting { get; set; } = new(true,
        "@{username}, dein Clip wird erstellt …");
    public ClipChatMessage Success { get; set; } = new(true,
        "@{username}, der Clip wurde erstellt: {clipUrl}");
    public ClipChatMessage SuccessDiscord { get; set; } = new(true,
        "@{username}, der Clip wurde erstellt und auf Discord gepostet: {clipUrl}");
    public ClipChatMessage Cooldown { get; set; } = new(true,
        "@{username}, du kannst in {remainingSeconds} Sekunden wieder einen Clip erstellen.");
    public ClipChatMessage Offline { get; set; } = new(true,
        "Clips können nur erstellt werden, während der Kanal live ist.");
    public ClipChatMessage Forbidden { get; set; } = new(true,
        "@{username}, du darfst diesen Command nicht verwenden.");
    public ClipChatMessage TwitchError { get; set; } = new(true,
        "Der Clip konnte momentan nicht erstellt werden.");
    public ClipChatMessage PartialDiscord { get; set; } = new(true,
        "Der Clip wurde erstellt, konnte aber nicht in alle Discord-Channels gepostet werden: {clipUrl}");
    public ClipChatMessage QueueFull { get; set; } = new(true,
        "@{username}, die Clip-Warteschlange ist voll. Bitte versuche es später erneut.");
    public ClipChatMessage Busy { get; set; } = new(true,
        "@{username}, es wird bereits ein Clip erstellt. Bitte versuche es gleich erneut.");
    public ClipChatMessage LimitReached { get; set; } = new(true,
        "@{username}, das Clip-Limit für diesen Stream ist erreicht.");
    public ClipChatMessage MissingScope { get; set; } = new(true,
        "Der Twitch-Zugriff benötigt clips:edit. Bitte Twitch erneut verbinden.");
}

public sealed class ClipChatMessage
{
    public ClipChatMessage() { }
    public ClipChatMessage(bool enabled, string text)
    {
        Enabled = enabled;
        Text = text;
    }

    public bool Enabled { get; set; }
    public string Text { get; set; } = "";
}

public sealed class DiscordClipsConfig
{
    public bool Enabled { get; set; } = false;
    public string GuildId { get; set; } = "";
    public List<DiscordClipChannelConfig> Channels { get; set; } = new();
    public string MessageTemplate { get; set; } =
        "🎬 **{clipTitle}**\nErstellt von {username}\n{clipUrl}";
    public bool UseEmbed { get; set; } = true;
    public string EmbedColor { get; set; } = "#9146FF";
    public string FooterText { get; set; } = "RaidClipPlugin";
    public string? MentionRoleId { get; set; }
    public bool UseThumbnail { get; set; } = true;
}

public sealed class DiscordClipChannelConfig
{
    public string ChannelId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool UseWebhook { get; set; } = false;
    public string MessageTemplate { get; set; } = "";
}
