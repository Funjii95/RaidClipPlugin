namespace RaidClipPlugin.Models;

public class Clip
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string EmbedUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public double DurationSeconds { get; set; }
    public string ThumbnailUrl { get; set; } = "";
    public string CreatorName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;
    public int ViewCount { get; set; }

    public string VideoUrl => GetVideoUrlFromThumbnail(ThumbnailUrl);

    private static string GetVideoUrlFromThumbnail(string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return "";
        }

        var index = thumbnailUrl.IndexOf(
            "-preview-",
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return "";
        }

        return thumbnailUrl[..index] + ".mp4";
    }
}
