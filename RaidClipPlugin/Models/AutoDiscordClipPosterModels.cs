namespace RaidClipPlugin.Models;

public sealed record AutoDiscordClipPosterEntry(
    string ClipId,
    string BroadcasterId,
    string BroadcasterName,
    string ClipUrl,
    string ClipTitle,
    string CreatorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset PostedAt,
    string DiscordWebhookUrl,
    string DiscordChannelId,
    string DiscordMessageId,
    string Status);

public sealed record AutoDiscordClipPosterResult(
    int Found,
    int AlreadyPosted,
    int Posted,
    int Skipped,
    int Failed);

public sealed record AutoDiscordClipPosterPeriod(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Description);
