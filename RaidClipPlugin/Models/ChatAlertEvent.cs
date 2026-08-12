namespace RaidClipPlugin.Models;

public enum ChatAlertKind
{
    Follow,
    Tip,
    Subscription,
    Cheer,
    AdBreak
}

public sealed record ChatAlertEvent(
    ChatAlertKind Kind,
    string UserName,
    decimal Amount = 0,
    string Currency = "",
    string Message = "",
    int Months = 0,
    int Quantity = 1,
    bool IsGift = false,
    string Provider = "Twitch",
    int DurationSeconds = 0,
    bool IsAutomatic = false);
