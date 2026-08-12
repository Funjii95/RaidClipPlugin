namespace RaidClipPlugin.Models;

public sealed record AdBreakEvent(
    int DurationSeconds,
    DateTimeOffset StartedAt,
    bool IsAutomatic,
    string RequesterName);
