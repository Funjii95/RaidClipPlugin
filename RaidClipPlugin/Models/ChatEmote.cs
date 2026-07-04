namespace RaidClipPlugin.Models;

public enum ChatEmoteProvider
{
    Twitch,
    BetterTTV,
    SevenTV,
    FrankerFaceZ
}

public sealed class ChatEmote
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public int StartIndex { get; init; }
    public int EndIndex { get; init; }
    public ChatEmoteProvider Provider { get; init; }
}
