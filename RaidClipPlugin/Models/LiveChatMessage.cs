namespace RaidClipPlugin.Models;

public enum LiveChatMessageType { Normal, Command, System, Deleted, Timeout, Ban }

public sealed record ChatEmoteFragment(string Id, string Code, string Provider = "Twitch");

public sealed record LiveChatMessage(
    string Id,
    DateTimeOffset Timestamp,
    string Username,
    string DisplayName,
    string Message,
    string RawMessage,
    string UserColor,
    bool IsBroadcaster,
    bool IsModerator,
    bool IsVip,
    bool IsSubscriber,
    bool IsBot,
    bool IsCommand,
    IReadOnlyList<string> Badges,
    IReadOnlyList<ChatEmoteFragment> Emotes,
    LiveChatMessageType MessageType);
