namespace RaidClipPlugin.Services;

/// <summary>Changes only Twitch application credentials and its cached authorization.</summary>
public sealed class PluginMaintenanceService
{
    private static readonly string[] TwitchFiles =
        ["twitch-credentials.dat", "twitch-token.dat", "twitch-token.json"];

    public string ReplaceTwitchCredentials(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Client ID und Client Secret sind erforderlich.");

        var root = TestDataPaths.Root;
        Directory.CreateDirectory(root);
        var previous = TwitchFiles.ToDictionary(name => name,
            name => File.Exists(Path.Combine(root, name)) ? File.ReadAllBytes(Path.Combine(root, name)) : null);
        // Even a legacy plaintext token is encrypted in the recovery copy.
        var backupDirectory = Path.Combine(root, "maintenance-backups");
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"twitch-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(backup, WindowsProtectedStore.ProtectJson(previous));
        try
        {
            // Invalidate first: an interrupted operation can require login, but cannot reuse
            // the previous account's token with newly supplied application credentials.
            foreach (var name in TwitchFiles.Skip(1)) File.Delete(Path.Combine(root, name));
            new TwitchCredentialStore().Save(clientId, clientSecret);
            return backup;
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>();
            foreach (var (name, bytes) in previous)
            {
                try
                {
                    var file = Path.Combine(root, name);
                    if (bytes is null) File.Delete(file);
                    else if (!File.Exists(file) || !File.ReadAllBytes(file).SequenceEqual(bytes))
                    {
                        var temp = file + ".restore-" + Guid.NewGuid().ToString("N");
                        File.WriteAllBytes(temp, bytes);
                        File.Move(temp, file, overwrite: true);
                    }
                }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 0)
                throw new AggregateException("Twitch-Wiederherstellung unvollständig. Verschlüsselte Sicherung: " + backup, [failure, .. errors]);
            throw;
        }
    }
}
