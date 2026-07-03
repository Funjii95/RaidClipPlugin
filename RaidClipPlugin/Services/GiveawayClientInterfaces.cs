using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public interface IGiveawayTwitchClient
{
    Task<TwitchLiveStream?> GetLiveStreamAsync(
        string broadcasterId,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetFollowedAtAsync(
        string broadcasterId,
        string userId,
        CancellationToken cancellationToken);

    Task<TwitchUser?> GetUserAsync(
        string login,
        CancellationToken cancellationToken);
}
