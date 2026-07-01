namespace RaidClipPlugin.Models;

public sealed class ChatMessage
{
    public string Id { get; init; } = "";
    public string UserId { get; init; } = "";
    public string UserLogin { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
    public bool IsModerator { get; init; }
    public bool IsVip { get; init; }
    public bool IsBroadcaster { get; init; }

    public bool IsWhitelisted => IsModerator || IsVip || IsBroadcaster;
}
