using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed class DiscordCredentialStore
{
    private readonly string _path;

    public DiscordCredentialStore(string? storagePath = null)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin");
        Directory.CreateDirectory(directory);
        _path = storagePath ?? Path.Combine(directory, "discord-credentials.dat");
    }

    public DiscordCredentials Load()
    {
        if (!File.Exists(_path)) return new DiscordCredentials();
        try
        {
            var credentials = WindowsProtectedStore.UnprotectJson<DiscordCredentials>(
                File.ReadAllBytes(_path)) ?? new DiscordCredentials();
            credentials.BotToken ??= "";
            credentials.WebhookUrls ??= new Dictionary<string, string>(
                StringComparer.Ordinal);
            return credentials;
        }
        catch
        {
            return new DiscordCredentials();
        }
    }

    public async Task SaveAsync(
        DiscordCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var protectedData = WindowsProtectedStore.ProtectJson(credentials);
        var temporary = _path + ".tmp";
        await File.WriteAllBytesAsync(temporary, protectedData, cancellationToken);
        File.Move(temporary, _path, overwrite: true);
    }
}

public sealed class DiscordCredentials
{
    public string BotToken { get; set; } = "";
    public Dictionary<string, string> WebhookUrls { get; set; } =
        new(StringComparer.Ordinal);
}
