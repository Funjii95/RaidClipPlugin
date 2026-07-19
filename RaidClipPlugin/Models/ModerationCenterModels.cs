using RaidClipPlugin.Config;

namespace RaidClipPlugin.Models;

public sealed record LinkMatch(string OriginalText, string NormalizedText, string Domain, bool IsObfuscated);

public sealed record LinkDetectionResult(IReadOnlyList<LinkMatch> Links)
{
    public bool HasLinks => Links.Count > 0;
}

public sealed class PermitEntry
{
    public string TwitchUserId { get; set; } = "";
    public string UserLogin { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string GrantedById { get; set; } = "";
    public string GrantedByName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(1);
    public PermitMode Mode { get; set; } = PermitMode.SingleMessage;
    public bool Used { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public string UsedMessageId { get; set; } = "";
    public string Source { get; set; } = "Chat-Command";
    public string Note { get; set; } = "";
}

public sealed class ModerationActionEntry
{
    public string ChannelId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public string ModeratorId { get; set; } = "";
    public string ModeratorName { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string MessageId { get; set; } = "";
    public string MessagePreview { get; set; } = "";
    public string Result { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string Rule { get; set; } = "";
}
