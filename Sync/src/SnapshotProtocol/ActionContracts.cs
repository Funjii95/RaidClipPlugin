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
public sealed record RelayAvailability(bool Available, string Reason, string? CatalogJson, Guid? SessionId=null,bool SelfDeletionEnabled=false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelfDeletionIntent(Guid RequestId,string Confirmation);
public sealed record SelfDeletionJob(string UserId,Guid RequestId,DateTimeOffset RequestedAt,bool Completed=false);

public static class RpgIntents
{
    public static readonly string[] Supported = ["status","inventory","sets","perks","equip","unequip","buyPerk","dive","fish","craft","eat","preview","minigames","casino","bossStatus","bossAttack","itemlock","favorite","salvagePreview","salvageBatch","salvageConfirm","salvageCancel","community","openShells"];
    public static string Canonical(RpgIntent intent)
    {
        if(intent.ActionId==Guid.Empty || !Supported.Contains(intent.Kind) || intent.Arguments.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("Unsupported RPG intent.");
        string[] required=intent.Kind switch {"openShells"=>["count"],"equip"=>["instanceId"],"unequip"=>["slot"],"buyPerk"=>["perkId"],"dive" or "fish"=>["stake"],"craft" or "eat"=>["recipeId"],"preview"=>["stake","fishingStake"],"casino"=>["game","stake","choice"],"itemlock" or "favorite"=>["instanceId","enabled"],"salvagePreview"=>["instanceId"],"salvageBatch"=>["selection"],"salvageConfirm" or "salvageCancel"=>["token"],"community"=>["game","operation","eventId","stake","target"],_=>[]};
        var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var field in intent.Arguments.EnumerateObject())
        {
            if((!required.Contains(field.Name) && !(intent.Kind=="equip" && field.Name=="slot")) ||
                field.Value.ValueKind!=JsonValueKind.String || !values.TryAdd(field.Name,field.Value.GetString()!) ||
                string.IsNullOrWhiteSpace(values[field.Name]) || values[field.Name].Length>150 || values[field.Name].Any(char.IsControl))
                throw new ArgumentException("Invalid action arguments.");
        }
        if(required.Any(p=>!values.ContainsKey(p)))throw new ArgumentException("Missing action arguments.");
        if(intent.Kind=="casino" && (values["game"] is not ("gamble" or "coinflip" or "slots" or "roulette") ||
            (values["game"]=="coinflip" && values["choice"] is not ("kopf" or "zahl"))))throw new ArgumentException("Invalid casino selection.");
        if(intent.Kind is "itemlock" or "favorite" && values["enabled"] is not ("true" or "false"))throw new ArgumentException("Invalid desired protection state.");
        if(intent.Kind=="salvageBatch" && values["selection"] is not ("common" or "duplicates"))throw new ArgumentException("Invalid batch selection.");
        if(intent.Kind=="community" && !((values["game"],values["operation"]) is ("heist","join") or ("race","start") or ("race","join") or ("duel","challenge") or ("duel","accept") or ("duel","deny")))throw new ArgumentException("Unsupported community operation.");
        if(intent.Kind=="community" && values["game"]=="duel" && values["operation"]=="challenge" && (values["target"].Length>25 || values["target"].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_')))throw new ArgumentException("Invalid opponent login.");
        if(values.TryGetValue("stake",out var stake) && (!long.TryParse(stake,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var amount) || amount<0 || amount.ToString(System.Globalization.CultureInfo.InvariantCulture)!=stake))
            throw new ArgumentException("Stake must be a canonical non-negative Int64 string.");
        if(values.TryGetValue("fishingStake",out var fish) && (!long.TryParse(fish,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var f) || f<0 || f.ToString(System.Globalization.CultureInfo.InvariantCulture)!=fish))throw new ArgumentException("Invalid fishing stake.");
        if(intent.Kind=="openShells" && (!int.TryParse(values["count"],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var count)||count is <1 or >100||count.ToString(System.Globalization.CultureInfo.InvariantCulture)!=values["count"]))throw new ArgumentException("Invalid shell count.");
        return JsonSerializer.Serialize(new {intent.Kind,Arguments=values});
    }
    public static string Hash(RpgIntent intent)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(intent))));
}
