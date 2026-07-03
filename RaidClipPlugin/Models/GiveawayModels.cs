namespace RaidClipPlugin.Models;

public enum GiveawayStatus
{
    NotStarted,
    Active,
    Paused,
    Ended,
    Cancelled
}

public sealed class GiveawayRuntimeState
{
    public string Id { get; set; } = "";
    public GiveawayStatus Status { get; set; } = GiveawayStatus.NotStarted;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Prize { get; set; } = "";
    public string Command { get; set; } = "!giveaway";
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public int PausedRemainingSeconds { get; set; }
    public DateTimeOffset? LastParticipantAnnouncementUtc { get; set; }
    public List<GiveawayParticipant> Participants { get; set; } = new();
    public List<GiveawayWinner> Winners { get; set; } = new();

    public bool IsRunning => Status is GiveawayStatus.Active or GiveawayStatus.Paused;
}

public sealed class GiveawayParticipant
{
    public string UserId { get; set; } = "";
    public string UserLogin { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset JoinedAtUtc { get; set; }
    public string Role { get; set; } = "Zuschauer";
    public bool IsSubscriber { get; set; }
    public bool IsVip { get; set; }
    public bool IsBroadcaster { get; set; }
    public int PointsUsed { get; set; }
    public int ExtraTickets { get; set; }
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = "";
}

public sealed class GiveawayWinner
{
    public string UserId { get; set; } = "";
    public string UserLogin { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset DrawnAtUtc { get; set; }
    public int DrawNumber { get; set; }
}

public sealed record GiveawayActionResult(
    bool Success,
    string Error = "",
    IReadOnlyList<GiveawayWinner>? Winners = null);

public sealed record GiveawayEligibilityResult(
    bool Allowed,
    string Reason,
    DateTimeOffset? FollowedAtUtc = null);
