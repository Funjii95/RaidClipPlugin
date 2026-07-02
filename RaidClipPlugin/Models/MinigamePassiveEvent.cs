namespace RaidClipPlugin.Models;

public enum MinigamePassiveEventKind
{
    Follow,
    Subscription,
    Raid,
    ChannelReward
}

public sealed record MinigamePassiveEvent(
    MinigamePassiveEventKind Kind,
    string UserId,
    string DisplayName);
