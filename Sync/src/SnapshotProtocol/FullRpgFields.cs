using System.Text.Json.Nodes;

namespace SnapshotProtocol;

internal static class FullRpgFields
{
    // Return true only for newly supported fields; existing v1 checks remain unchanged.
    internal static bool Validate(JsonObject parent, string name, JsonNode? node)
    {
        switch(name)
        {
            case "IsLocked": case "IsFavorite": case "EncounterBased":
                Require(node is JsonValue flag && flag.TryGetValue<bool>(out _)); return true;
            case "CoreStatsVersion": case "DropWeight": case "SalvageValue":
                Require(name=="SalvageValue" && node is null || node is JsonValue integer && integer.TryGetValue<long>(out var n) && n>=0); return true;
            case "Category":
                Require(node is JsonValue category && category.TryGetValue<int>(out var c) && c is >=0 and <=1); return true;
            case "PassiveHealing": case "ChatHealing": case "BossDamageReduction": case "BossLifeleech":
                Require(node is JsonValue number && number.TryGetValue<decimal>(out var d) && d>=0); return true;
            case "ActivatedAt": case "ExpiresAt":
                Require(name=="ExpiresAt" && node is null || node is JsonValue date && date.TryGetValue<DateTimeOffset>(out _)); return true;
            case "DropActivities":
                Require(node is JsonArray); foreach(var item in (JsonArray)node!)
                    Require(item is JsonValue activity && activity.TryGetValue<int>(out var a) && a is >=0 and <=2);
                return true;
            case "Collector":
                if(node is null)return true;
                parent[name]=Clean(node,["FishingPearls","DivePearls","BossPearls","BossExperience","TreasureChance","RareTreasureChance"]);
                foreach(var field in (JsonObject)parent[name]!) Require(field.Value is JsonValue value && value.TryGetValue<decimal>(out _));
                return true;
            case "Effects":
                Require(node is JsonArray);
                for(var i=0;i<((JsonArray)node!).Count;i++)
                {
                    var effect=Clean(((JsonArray)node)[i],["RequiresCritical","Id","Name","Trigger","Chance","Strength","MinimumStake","CooldownSeconds","Activity"]);
                    foreach(var field in effect)
                    {
                        if(field.Key=="Activity" && field.Value is null)continue;
                        Require(field.Value is JsonValue);
                        var v=(JsonValue)field.Value!;
                        Require(field.Key switch {"Id" or "Name"=>v.TryGetValue<string>(out _), "RequiresCritical"=>v.TryGetValue<bool>(out _), _=>v.TryGetValue<decimal>(out var effectNumber)&&effectNumber>=0});
                    }
                    ((JsonArray)node)[i]=effect;
                }
                return true;
            case "Generation":
                if(node is null)return true;
                var generation=Clean(node,["AttributeCount","GenerateEffect","DoubleAttackMinimumChance","DoubleAttackMaximumChance","Attributes","EffectIds"]);
                if(generation["Attributes"] is JsonArray ranges)
                    for(var i=0;i<ranges.Count;i++)ranges[i]=Clean(ranges[i],["Attribute","Minimum","Maximum"]);
                parent[name]=generation; return true;
            default:return false;
        }
    }
    private static JsonObject Clean(JsonNode? node, string[] allowed)
    {
        if(node is not JsonObject obj)throw new InvalidDataException("Expected RPG object.");
        var result=new JsonObject();
        foreach(var field in obj)
        {
            var key=allowed.FirstOrDefault(a=>a.Equals(field.Key,StringComparison.OrdinalIgnoreCase));
            if(key is not null)result[key]=field.Value?.DeepClone();
        }
        return result;
    }
    private static void Require(bool valid) {if(!valid)throw new InvalidDataException("Invalid complete RPG projection field.");}
}
