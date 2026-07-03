namespace RaidClipPlugin.Models;

public sealed record TwitchClipRequest(
    string BroadcasterId,
    string Title,
    int DurationSeconds);

public sealed record TwitchCreatedClip(
    string Id,
    string EditUrl);

public sealed record TwitchLiveStream(
    string Id,
    string BroadcasterId,
    string BroadcasterLogin,
    string BroadcasterName,
    string GameId,
    string GameName,
    DateTimeOffset StartedAt,
    bool IsLive);

public sealed record ClipCommandRequest(
    ChatMessage Message,
    string Title,
    TwitchLiveStream Stream,
    DateTimeOffset RequestedAt);

public sealed record PublishedClip(
    string Id,
    string Url,
    string Title,
    string ThumbnailUrl,
    double DurationSeconds);

public sealed record ClipDiscordContext(
    PublishedClip Clip,
    string RequestedTitle,
    string Username,
    string Channel,
    string Game,
    DateTimeOffset Timestamp,
    string ThumbnailUrl = "");

public sealed record DiscordChannelValidation(
    string ChannelId,
    string Name,
    bool IsValid,
    bool CanView,
    bool CanSend,
    bool CanEmbed,
    string Error = "");

public sealed record DiscordClipDelivery(
    string ChannelId,
    bool Success,
    string Error = "");

public sealed record DiscordClipPostResult(
    IReadOnlyList<DiscordClipDelivery> Deliveries)
{
    public int SuccessfulChannels => Deliveries.Count(item => item.Success);
    public int FailedChannels => Deliveries.Count(item => !item.Success);
    public bool AnySuccess => SuccessfulChannels > 0;
    public bool AllSucceeded => Deliveries.Count > 0 && FailedChannels == 0;
}
