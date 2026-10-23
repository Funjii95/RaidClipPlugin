using System.Text.RegularExpressions;
namespace Backend;
public sealed class BackendSettings
{
    public DesktopSettings Desktop { get; set; } = new();
    public ActionSettings Actions {get;set;}=new();
    public TwitchSettings Twitch { get; set; } = new();
    public PluginSettings Plugin { get; set; } = new();
    public DataSettings Data { get; set; } = new();
    public void Validate()
    {
        if(Desktop.Enabled && !Regex.IsMatch(Desktop.ClientId,"^[a-zA-Z0-9]{10,100}$"))
            throw new InvalidOperationException("Desktop login requires its registered public Twitch ClientId.");
        if(Actions.Enabled && (Actions.Mode is not ("Firestore" or "Synthetic") || Actions.AllowedUserIds.Length is <1 or >100 || Actions.AllowedUserIds.Any(id=>!Ids.IsNumeric(id)) ||
            Actions.PluginKeyHash.Length!=64 || Actions.PluginKeyHash.Any(c=>!Uri.IsHexDigit(c)) ||
            !Regex.IsMatch(Actions.Collection,"^[a-zA-Z0-9_-]{1,64}$") || Actions.Collection==Data.Collection ||
            Actions.Mode=="Firestore" && (string.IsNullOrWhiteSpace(Data.ProjectId))))
            throw new InvalidOperationException("Explicit action test scope, credential hash and durable store required.");
        if(Actions.AutomaticPlayerEnrollment && (!Actions.Enabled || Actions.Mode!="Firestore" || Data.Mode!="FirestoreReadOnly"))throw new InvalidOperationException("Automatic admission requires authoritative published profiles.");
        byte[] secret;
        try { secret = Convert.FromBase64String(Twitch.SecretBase64); }
        catch (FormatException) { throw new InvalidOperationException("Twitch secret must be Base64."); }
        if (secret.Length < 32 || !Ids.IsNumeric(Twitch.ChannelId))
            throw new InvalidOperationException("Configure Twitch secret (32+ decoded bytes) and numeric ChannelId.");
        if(Actions.Enabled && Actions.PluginKeyHash.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(secret)),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Action credential must differ from Twitch signing secret.");
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
public sealed class DesktopSettings
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
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

public sealed class ActionSettings
{
    public bool AutomaticPlayerEnrollment {get;set;}
    public bool SelfDeletionEnabled {get;set;}
    public bool PrivacyDeletionEnabled {get;set;}
    public bool Enabled {get;set;}
    public string Mode {get;set;}="Firestore";
    public string Collection {get;set;}="rpgActionChannelsTest";
    public string[] AllowedUserIds {get;set;}=[];
    public string PluginKeyHash {get;set;}="";
    public string HelixClientId {get;set;}="";
    public string HelixAccessToken {get;set;}="";
}
