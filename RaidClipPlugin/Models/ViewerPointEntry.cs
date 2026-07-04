namespace RaidClipPlugin.Models;

public sealed class ViewerPointEntry
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Points { get; set; }
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

public sealed record GambleBalanceResult(bool Success, long Balance);

public sealed record PointTransferResult(
    bool Success,
    string Error,
    long SenderBalance,
    long RecipientBalance);

public sealed class MinigameHistoryEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Game { get; set; } = "";
    public string Action { get; set; } = "";
    public long Change { get; set; }
    public long Balance { get; set; }
}

public sealed record ViewerProfileResult(
    ViewerPointEntry Entry,
    int Rank);

public sealed record DailyClaimResult(
    bool Success,
    long Balance,
    TimeSpan Remaining);

public sealed record CasinoApplyResult(
    bool Success,
    string Error,
    long Balance,
    int JackpotWon);
public sealed record HeistParticipantPayout(
    string UserId,
    string DisplayName,
    int Payout,
    long NewBalance);

public sealed record HeistPayoutResult(
    int JackpotBefore,
    int JackpotAfter,
    IReadOnlyList<HeistParticipantPayout> Payouts);

public sealed record DuelReserveResult(bool Success, string Error, long Balance);

public sealed record DuelResolutionResult(
    bool Success,
    string Error,
    long ChallengerBalance,
    long TargetBalance,
    int Pot);
