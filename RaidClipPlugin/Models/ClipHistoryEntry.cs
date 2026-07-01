namespace RaidClipPlugin.Models;

public sealed class ClipHistoryEntry
{
    public string ClipId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Channel { get; set; } = "";
    public DateTimeOffset PlayedAt { get; set; }
    public string Status { get; set; } = "";
}
