using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class EventStartCommand
{
    public static bool IsAllowed(ChatMessage user, string broadcasterId) =>
        user.CommandAuthorization != CommandAuthorization.Denied &&
        (user.IsModerator || user.IsBroadcaster || (!string.IsNullOrEmpty(broadcasterId) && user.UserId == broadcasterId));

    public static string[]? Translate(string[] parts)
    {
        if (parts.Length < 2) return null;
        return parts[1].ToLowerInvariant() switch
        {
            "boss" when parts.Length == 2 => new[] { "!boss" },
            "rennen" or "race" when parts.Length == 3 => new[] { "!rennen", parts[2] },
            "heist" when parts.Length == 2 => new[] { "!heist" },
            _ => null
        };
    }
}
