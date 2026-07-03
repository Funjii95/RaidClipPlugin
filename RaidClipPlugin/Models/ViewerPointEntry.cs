namespace RaidClipPlugin.Models;

public sealed class ViewerPointEntry
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Points { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public int WatchMinutes { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int BiggestWin { get; set; }
    public DateTimeOffset? LastDailyUtc { get; set; }
    public DateOnly DailyLimitDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int DailyGambles { get; set; }
    public int DailyLoss { get; set; }
    public int DailyWin { get; set; }
}

public sealed record GambleBalanceResult(bool Success, int Balance);

public sealed record PointTransferResult(
    bool Success,
    string Error,
    int SenderBalance,
    int RecipientBalance);

public sealed class MinigameHistoryEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Game { get; set; } = "";
    public string Action { get; set; } = "";
    public int Change { get; set; }
    public int Balance { get; set; }
}

public sealed record ViewerProfileResult(
    ViewerPointEntry Entry,
    int Rank);

public sealed record DailyClaimResult(
    bool Success,
    int Balance,
    TimeSpan Remaining);

public sealed record CasinoApplyResult(
    bool Success,
    string Error,
    int Balance,
    int JackpotWon);
public sealed record HeistParticipantPayout(
    string UserId,
    string DisplayName,
    int Payout,
    int NewBalance);

public sealed record HeistPayoutResult(
    int JackpotBefore,
    int JackpotAfter,
    IReadOnlyList<HeistParticipantPayout> Payouts);
