using System.Text.RegularExpressions;
namespace Backend;
public sealed class BackendSettings
{
    public TwitchSettings Twitch { get; set; } = new();
    public PluginSettings Plugin { get; set; } = new();
    public DataSettings Data { get; set; } = new();
    public void Validate()
    {
        byte[] secret;
        try { secret = Convert.FromBase64String(Twitch.SecretBase64); }
        catch (FormatException) { throw new InvalidOperationException("Twitch secret must be Base64."); }
        if (secret.Length < 32 || !Ids.IsNumeric(Twitch.ChannelId))
            throw new InvalidOperationException("Configure Twitch secret (32+ decoded bytes) and numeric ChannelId.");
        if (Plugin.Enabled && (System.Text.Encoding.UTF8.GetByteCount(Plugin.ApiKey) < 32 || Plugin.ApiKey == Twitch.SecretBase64))
            throw new InvalidOperationException("Plugin requires a separate random API key of 32+ bytes.");
        if (Data.Mode is not ("Mock" or "FirestoreReadOnly")) throw new InvalidOperationException("Invalid Data mode.");
        if (Data.Mode == "FirestoreReadOnly" && (string.IsNullOrWhiteSpace(Data.ProjectId) ||
            !Regex.IsMatch(Data.Collection, "^[a-zA-Z0-9_-]{1,100}$") || string.IsNullOrWhiteSpace(Data.DatabaseId)))
            throw new InvalidOperationException("Configure Firestore project, database and single collection name.");
        if (Twitch.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.GetLeftPart(UriPartial.Authority) != origin))
            throw new InvalidOperationException("CORS origins must be exact HTTPS origins without trailing slash.");
    }
}
public sealed class TwitchSettings
{
    public string SecretBase64 { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string[] AllowedOrigins { get; set; } = [];
}
public sealed class PluginSettings
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
}
public sealed class DataSettings
{
    public string Mode { get; set; } = "Mock";
    public string ProjectId { get; set; } = "";
    public string DatabaseId { get; set; } = "(default)";
    public string Collection { get; set; } = "rpgReadSnapshots";
}
public static partial class Ids
{
    public static bool IsNumeric(string? value) => value is not null && Numeric().IsMatch(value);
    [GeneratedRegex("^[0-9]{1,25}$", RegexOptions.CultureInvariant)]
    private static partial Regex Numeric();
}
