namespace RaidClipPlugin.Models;

public enum CommandAuthorization { Default, Allowed, Denied }

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
    public bool IsSubscriber { get; init; }
    public bool IsBroadcaster { get; init; }
    public CommandAuthorization CommandAuthorization { get; set; }
    public string UserColor { get; init; } = "";
    public IReadOnlyList<string> Badges { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ChatEmoteFragment> Emotes { get; init; } = Array.Empty<ChatEmoteFragment>();
    public bool IsBot { get; init; }

    public bool IsWhitelisted => IsModerator || IsVip || IsBroadcaster;
}