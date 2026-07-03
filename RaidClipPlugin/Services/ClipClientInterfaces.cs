using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public interface ITwitchClipClient
{
    Task<TwitchCreatedClip> CreateClipAsync(
        TwitchClipRequest request,
        CancellationToken cancellationToken);

    Task<PublishedClip?> GetClipByIdAsync(
        string clipId,
        CancellationToken cancellationToken);

    Task<TwitchLiveStream?> GetLiveStreamAsync(
        string broadcasterId,
        CancellationToken cancellationToken);

    Task<bool> IsFollowerAsync(
        string broadcasterId,
        string userId,
        CancellationToken cancellationToken);
}

public interface IDiscordClipClient
{
    Task<DiscordChannelValidation> ValidateChannelAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken);

    Task<DiscordChannelValidation> ValidateWebhookAsync(
        string webhookUrl,
        string guildId,
        string channelId,
        CancellationToken cancellationToken);

    Task SendMessageAsync(
        string channelId,
        object payload,
        CancellationToken cancellationToken);

    Task SendWebhookAsync(
        string webhookUrl,
        object payload,
        CancellationToken cancellationToken);
}

public interface IClipChatClient
{
    Task SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string message,
        CancellationToken cancellationToken);
}
