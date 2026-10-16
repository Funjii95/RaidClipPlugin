using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class EventStartCommand
{
    public static bool IsAllowed(ChatMessage user, string broadcasterId, string[]? parts = null) =>
        user.CommandAuthorization != CommandAuthorization.Denied &&
        ((parts is { Length: 3 } && parts[1].Equals("race", StringComparison.OrdinalIgnoreCase)) ||
         user.IsModerator || user.IsBroadcaster || (!string.IsNullOrEmpty(broadcasterId) && user.UserId == broadcasterId));

    public static string[]? Translate(string[] parts)
    {
        if (parts.Length < 2) return null;
        return parts[1].ToLowerInvariant() switch
        {
            "boss" when parts.Length == 2 => new[] { "!boss" },
            "race" when parts.Length == 3 => new[] { "!startrace", parts[2] },
            "heist" when parts.Length == 2 => new[] { "!heist" },
            _ => null
        };
    }
}
