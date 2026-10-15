namespace RaidClipPlugin.Services;

public interface IChatAnnouncementClient
{
    Task SendChatAnnouncementAsync(string broadcasterId, string moderatorId,
        string message, string color, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Dieser Chat-Client unterstützt keine Ankündigungen.");
}

public static class ChatAnnouncement
{
    public static readonly string[] Labels =
        { "Normaler Chat", "Blau", "Grün", "Orange", "Lila", "Kanalfarbe" };
    private static readonly string[] Colors = { "", "blue", "green", "orange", "purple", "primary" };
    public static string ColorFromLabel(string? label) => Colors[Math.Max(0, Array.IndexOf(Labels, label))];
    public static string LabelFromColor(string? color) => Labels[Math.Max(0, Array.IndexOf(Colors, color))];
    public static string Validate(string? color) => Colors.Contains(color ?? "")
        ? color ?? "" : throw new InvalidOperationException("Ungültige Ankündigungsfarbe.");
}
