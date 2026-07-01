namespace RaidClipPlugin.Services;

public sealed class TwitchCredentialStore
{
    private readonly string _credentialPath;

    public TwitchCredentialStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin");
        Directory.CreateDirectory(directory);
        _credentialPath = Path.Combine(
            directory,
            "twitch-credentials.dat");
    }

    public bool HasCredentials
    {
        get
        {
            try
            {
                var credentials = Load();
                return !string.IsNullOrWhiteSpace(credentials.ClientId) &&
                       !string.IsNullOrWhiteSpace(credentials.ClientSecret);
            }
            catch
            {
                return false;
            }
        }
    }

    public TwitchCredentials Load()
    {
        if (!File.Exists(_credentialPath))
        {
            throw new InvalidOperationException(
                "Die Twitch-Anwendung wurde noch nicht eingerichtet.");
        }

        try
        {
            var protectedData = File.ReadAllBytes(_credentialPath);
            var credentials =
                WindowsProtectedStore.UnprotectJson<TwitchCredentials>(
                    protectedData);

            if (credentials is null ||
                string.IsNullOrWhiteSpace(credentials.ClientId) ||
                string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Die gespeicherten Twitch-Zugangsdaten sind unvollständig.");
            }

            return new TwitchCredentials(
                credentials.ClientId.Trim(),
                credentials.ClientSecret.Trim());
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Die Twitch-Zugangsdaten konnten für diesen Windows-Benutzer " +
                "nicht entschlüsselt werden.",
                exception);
        }
    }

    public void Save(string clientId, string clientSecret)
    {
        clientId = (clientId ?? "").Trim();
        clientSecret = (clientSecret ?? "").Trim();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "Bitte die Twitch Client ID eingeben.");
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Bitte den Twitch Client Secret eingeben.");
        }

        var protectedData = WindowsProtectedStore.ProtectJson(
            new TwitchCredentials(clientId, clientSecret));
        var temporaryPath = _credentialPath + ".tmp";

        File.WriteAllBytes(temporaryPath, protectedData);
        File.Move(temporaryPath, _credentialPath, overwrite: true);
    }
}

public sealed record TwitchCredentials(
    string ClientId,
    string ClientSecret);
