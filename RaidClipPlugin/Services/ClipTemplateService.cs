using System.Text;
using System.Text.RegularExpressions;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ClipTemplateService
{
    private static readonly Regex WhiteSpace = new(
        "\\s+", RegexOptions.Compiled);
    private static readonly Regex DiscordRoleMention = new(
        "<@&\\d+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string CreateTitle(
        string rawTitle,
        string defaultTitle,
        int maximumLength,
        ChatMessage message,
        TwitchLiveStream stream,
        DateTimeOffset now)
    {
        var title = SanitizeTitle(rawTitle, maximumLength);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ApplyCommonTemplate(
                defaultTitle, message.UserName, stream, now);
            title = SanitizeTitle(title, maximumLength);
        }
        return title;
    }

    public string ApplyCommonTemplate(
        string template,
        string username,
        TwitchLiveStream stream,
        DateTimeOffset now) =>
        (template ?? "")
            .Replace("{username}", username, StringComparison.OrdinalIgnoreCase)
            .Replace("{channel}", stream.BroadcasterName,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{game}", stream.GameName,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToLocalTime().ToString("dd.MM.yyyy"),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToLocalTime().ToString("HH:mm:ss"),
                StringComparison.OrdinalIgnoreCase);

    public string ApplyClipTemplate(
        string template,
        ClipDiscordContext context) =>
        (template ?? "")
            .Replace("{clipTitle}",
                SanitizeDiscordUserContent(context.RequestedTitle),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{clipUrl}", context.Clip.Url,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{clipId}", context.Clip.Id,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{username}",
                SanitizeDiscordUserContent(context.Username),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{channel}",
                SanitizeDiscordUserContent(context.Channel),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{game}",
                SanitizeDiscordUserContent(context.Game),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{timestamp}",
                context.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
                StringComparison.OrdinalIgnoreCase);

    public static string SanitizeTitle(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsControl(character) || character is '\t' or '\r' or '\n')
                builder.Append(character);
        }
        var normalized = WhiteSpace.Replace(builder.ToString(), " ").Trim();
        var limit = Math.Clamp(maximumLength, 1, 140);
        return normalized.Length <= limit ? normalized : normalized[..limit].TrimEnd();
    }

    public static string SanitizeDiscordUserContent(string? value)
    {
        var text = value ?? "";
        text = text.Replace("@everyone", "@\u200Beveryone",
            StringComparison.OrdinalIgnoreCase);
        text = text.Replace("@here", "@\u200Bhere",
            StringComparison.OrdinalIgnoreCase);
        return DiscordRoleMention.Replace(text,
            match => match.Value.Replace("<@&", "<@\u200B&",
                StringComparison.Ordinal));
    }
}
