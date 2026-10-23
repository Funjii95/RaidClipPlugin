using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapshotProtocol;

public static class ActionResultLimits
{
    // Keep the complete Firestore action document comfortably below 1 MiB.
    // Measure serialized UTF-8: DataJson is escaped again inside the receipt.
    public const int MaximumOutcomeBytes = 384 * 1024;
    public const int MaximumCompletionBodyBytes = MaximumOutcomeBytes + 8192;
}

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
    public static bool IsAdmin(string kind)=>kind is "adminCatalog" or "adminStart" or "adminGrant";
    public static readonly string[] Supported = ["upgradeItem","status","inventory","sets","perks","equip","unequip","buyPerk","dive","fish","craft","eat","preview","minigames","casino","bossStatus","bossAttack","itemlock","favorite","salvagePreview","salvageBatch","salvageConfirm","salvageCancel","community","openShells","learnTalent"];
    public static string Canonical(RpgIntent intent)
    {
        if(intent.ActionId==Guid.Empty || !(Supported.Contains(intent.Kind)||IsAdmin(intent.Kind)) || intent.Arguments.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("Unsupported RPG intent.");
        string[] required=intent.Kind switch {"upgradeItem"=>["instanceId","expectedLevel"],"learnTalent"=>["talentId","expectedRank","catalogVersion"],"openShells"=>["count"],"equip"=>["instanceId"],"unequip"=>["slot"],"buyPerk"=>["perkId"],"dive" or "fish"=>["stake"],"craft" or "eat"=>["recipeId"],"preview"=>["stake","fishingStake"],"casino"=>["game","stake","choice"],"itemlock" or "favorite"=>["instanceId","enabled"],"salvagePreview"=>["instanceId"],"salvageBatch"=>["selection"],"salvageConfirm" or "salvageCancel"=>["token"],"community"=>["game","operation","eventId","stake","target"],_=>[]};
        var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
        if(intent.Kind=="adminStart")required=["event","stake"];
        if(intent.Kind=="adminGrant")required=["itemId","targetUserId","count","roll","stats"];
        foreach(var field in intent.Arguments.EnumerateObject())
        {
            if((!required.Contains(field.Name) && !(intent.Kind=="equip" && field.Name=="slot") && !(intent.Kind=="buyPerk" && field.Name=="mode")) ||
                field.Value.ValueKind!=JsonValueKind.String || !values.TryAdd(field.Name,field.Value.GetString()!) ||
                string.IsNullOrWhiteSpace(values[field.Name]) || values[field.Name].Length>(intent.Kind=="adminGrant"&&field.Name=="stats"?1500:150) || values[field.Name].Any(char.IsControl))
                throw new ArgumentException("Invalid action arguments.");
        }
        if(required.Any(p=>!values.ContainsKey(p)))throw new ArgumentException("Missing action arguments.");
        if(intent.Kind=="adminStart" && values["event"] is not ("boss" or "mimic" or "race" or "heist" or "hotTime"))throw new ArgumentException("Unsupported admin event.");
        if(intent.Kind=="adminGrant")
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(values["targetUserId"],"^[0-9]{1,25}$") || values["roll"] is not ("random" or "minimum" or "maximum" or "custom") ||
                !int.TryParse(values["count"],out var quantity)||quantity is <1 or >20||quantity.ToString()!=values["count"])throw new ArgumentException("Invalid admin grant.");
            using var stats=JsonDocument.Parse(values["stats"]);
            if(stats.RootElement.ValueKind!=JsonValueKind.Object || stats.RootElement.EnumerateObject().Count()>24 || stats.RootElement.EnumerateObject().Any(p=>p.Value.ValueKind!=JsonValueKind.Number || !p.Value.TryGetDecimal(out var value)||value<0||value>1000000))throw new ArgumentException("Invalid admin stats.");
        }
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
        if(intent.Kind=="learnTalent" && (!int.TryParse(values["expectedRank"],out var rank) || rank is <0 or >5 || rank.ToString()!=values["expectedRank"] || values["catalogVersion"]!="1"))throw new ArgumentException("Invalid talent allocation.");
        if(intent.Kind=="buyPerk" && values.TryGetValue("mode",out var mode) && mode!="max")throw new ArgumentException("Invalid perk purchase mode.");
        if(intent.Kind=="upgradeItem" && (!int.TryParse(values["expectedLevel"],out var level) || level is <0 or >8 || level.ToString()!=values["expectedLevel"]))throw new ArgumentException("Invalid upgrade level.");
        return JsonSerializer.Serialize(new {intent.Kind,Arguments=values});
    }
    public static string Hash(RpgIntent intent)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(intent))));
}
