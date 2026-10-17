using RaidClipPlugin.Services;

namespace RaidClipPlugin.Models;

// Authoritative immutable snapshot for future adapters. Never accepts client result/payout data.
public sealed record HeistPlayerResult(string UserId,string DisplayName,long Payout,long Lost=0);
public sealed record HeistEventSnapshot(string EventId,string Name,HeistState State,DateTimeOffset StartedAt,
    DateTimeOffset JoinEndsAt,DateTimeOffset? CompletedAt,int MinimumParticipants,int MaximumParticipants,
    string JoinCommand,string Currency,long Jackpot,long EntryStake,IReadOnlyList<HeistPlayerResult> Players,
    long TotalPayout=0,long TotalLost=0);
