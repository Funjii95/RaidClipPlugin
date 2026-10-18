using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapshotProtocol;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RpgIntent(Guid ActionId, string Kind, JsonElement Arguments);
public sealed record ActionOutcome(bool? Success, string Code, string Message, string? DataJson = null);
public sealed record ActionEnvelope(string ChannelId, string UserId, Guid SessionId, string StreamId,
    DateTimeOffset ExecuteBefore, RpgIntent Intent, string IntentHash, DateTimeOffset ObservedAt=default);
public sealed record ActionReceipt(Guid ActionId, string State, ActionOutcome? Outcome);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RelayHeartbeat(Guid SessionId, long Sequence, DateTimeOffset SentAt, bool Ready, string CatalogJson);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RelayComplete(Guid SessionId, Guid ActionId, ActionOutcome Outcome);
public sealed record RelaySession(Guid SessionId, DateTimeOffset ExpiresAt);
public sealed record RelayAvailability(bool Available, string Reason, string? CatalogJson, Guid? SessionId=null);

public static class RpgIntents
{
    public static readonly string[] Supported = ["status","inventory","sets","perks","equip","unequip","buyPerk","dive","fish","craft","eat"];
    public static string Canonical(RpgIntent intent)
    {
        if(intent.ActionId==Guid.Empty || !Supported.Contains(intent.Kind) || intent.Arguments.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("Unsupported RPG intent.");
        string[] required=intent.Kind switch {"equip"=>["instanceId"],"unequip"=>["slot"],"buyPerk"=>["perkId"],"dive" or "fish"=>["stake"],"craft" or "eat"=>["recipeId"],_=>[]};
        var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var field in intent.Arguments.EnumerateObject())
        {
            if((!required.Contains(field.Name) && !(intent.Kind=="equip" && field.Name=="slot")) ||
                field.Value.ValueKind!=JsonValueKind.String || !values.TryAdd(field.Name,field.Value.GetString()!) ||
                string.IsNullOrWhiteSpace(values[field.Name]) || values[field.Name].Length>150 || values[field.Name].Any(char.IsControl))
                throw new ArgumentException("Invalid action arguments.");
        }
        if(required.Any(p=>!values.ContainsKey(p)))throw new ArgumentException("Missing action arguments.");
        if(values.TryGetValue("stake",out var stake) && (!long.TryParse(stake,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var amount) || amount<0 || amount.ToString(System.Globalization.CultureInfo.InvariantCulture)!=stake))
            throw new ArgumentException("Stake must be a canonical non-negative Int64 string.");
        return JsonSerializer.Serialize(new {intent.Kind,Arguments=values});
    }
    public static string Hash(RpgIntent intent)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(intent))));
}
