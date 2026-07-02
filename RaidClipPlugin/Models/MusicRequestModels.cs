namespace RaidClipPlugin.Models;

public enum MusicPlaybackMode
{
    AddToQueue,
    PlayImmediately
}

public enum MusicRequestStatus
{
    Checking,
    Accepted,
    Queued,
    Playing,
    Rejected,
    Failed,
    Skipped,
    Completed
}

public sealed record SpotifyTrack(
    string Id,
    string Uri,
    string Name,
    string Artist,
    int DurationMs,
    bool Explicit,
    bool IsPlayable,
    string ExternalUrl,
    bool IsLocal = false,
    string Type = "track");

public sealed record SpotifyDevice(
    string Id,
    string Name,
    string Type,
    bool IsActive,
    bool IsRestricted)
{
    public override string ToString() =>
        $"{Name} ({Type}){(IsActive ? " · aktiv" : "")}";
}

public sealed record TwitchCustomReward(
    string Id,
    string Title,
    bool RequiresInput,
    bool IsEnabled)
{
    public override string ToString() =>
        $"{Title}{(RequiresInput ? "" : " · keine Texteingabe")}";
}

public sealed record MusicRequestRedemption(
    string RedemptionId,
    string RewardId,
    string RewardName,
    string UserId,
    string UserLogin,
    string DisplayName,
    string UserInput,
    DateTimeOffset RedeemedAt,
    string Status);

public sealed class MusicRequestEntry
{
    public string RedemptionId { get; set; } = "";
    public string RewardId { get; set; } = "";
    public string RewardName { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserLogin { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string UserInput { get; set; } = "";
    public DateTimeOffset RedeemedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public MusicRequestStatus Status { get; set; } = MusicRequestStatus.Checking;
    public MusicPlaybackMode PlaybackMode { get; set; }
    public SpotifyTrack? Track { get; set; }
    public string FailureReason { get; set; } = "";
}

public sealed record MusicRequestResult(
    bool Success,
    string FailureReason,
    string UserMessage,
    SpotifyTrack? Track,
    string RedemptionId,
    bool IsTemporaryFailure = false);
