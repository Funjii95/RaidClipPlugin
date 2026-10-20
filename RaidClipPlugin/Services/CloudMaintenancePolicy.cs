namespace RaidClipPlugin.Services;

internal static class CloudMaintenancePolicy
{
    internal static void RequireOwner(IEnumerable<string> channels, string authenticatedUserId)
    {
        if (channels.Any(channel => string.IsNullOrWhiteSpace(channel) || channel != authenticatedUserId))
            throw new InvalidOperationException("RPG-Löschen gesperrt: Das angemeldete Twitch-Konto ist nicht der Inhaber " +
                "der eingerichteten Cloud-Kanäle. Getrennte Cloud-Zugänge verwenden; kein fremdes Cloud-Profil zurücksetzen.");
    }
}
