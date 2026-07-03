using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ClipPermissionService
{
    private readonly ITwitchClipClient _twitch;

    public ClipPermissionService(ITwitchClipClient twitch)
    {
        _twitch = twitch;
    }

    public async Task<ClipPermissionDecision> CheckAsync(
        ChatMessage message,
        string broadcasterId,
        ClipCommandConfig config,
        CancellationToken cancellationToken)
    {
        var login = Normalize(message.UserLogin.Length > 0
            ? message.UserLogin
            : message.UserName);
        if (config.BlockedUsers.Any(user => Normalize(user) == login))
            return new ClipPermissionDecision(false, "blacklist");

        if (config.AllowedUsers.Any(user => Normalize(user) == login))
            return new ClipPermissionDecision(true, "user-whitelist");

        var roles = config.AllowedRoles;
        if (roles.Everyone ||
            roles.Broadcaster && message.IsBroadcaster ||
            roles.Moderators && message.IsModerator ||
            roles.Vips && message.IsVip ||
            roles.Subscribers && message.IsSubscriber)
            return new ClipPermissionDecision(true, "role");

        if (roles.Followers && !string.IsNullOrWhiteSpace(message.UserId))
        {
            try
            {
                if (await _twitch.IsFollowerAsync(
                        broadcasterId, message.UserId, cancellationToken))
                    return new ClipPermissionDecision(true, "follower");
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Follower-Berechtigung konnte nicht geprüft werden: " +
                    exception.Message);
            }
        }

        return new ClipPermissionDecision(false, "role");
    }

    private static string Normalize(string value) =>
        (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
}

public sealed record ClipPermissionDecision(bool Allowed, string Reason);
