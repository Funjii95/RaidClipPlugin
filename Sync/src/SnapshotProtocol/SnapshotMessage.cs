using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SnapshotProtocol;

public enum SnapshotKind { Upsert, Delete, Restore }
public sealed record SnapshotMessage(int ContractVersion, string ChannelId, string UserId, long Revision,
    Guid SnapshotId, DateTimeOffset CapturedAt, SnapshotKind Kind, string? AccountJson,
    long? ExpectedDeletedRevision = null);

public sealed record ValidatedSnapshot(string ChannelId, string UserId, long Revision, Guid SnapshotId,
    DateTimeOffset CapturedAt, SnapshotKind Kind, string? AccountJson, long? ExpectedDeletedRevision,
    string ContentHash);

public static class SnapshotValidation
{
    public const int RpgProjectionVersion = 7;
    public const int MaxMessageBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    // Bound the complete UTF-8 envelope before JSON parsing, including escaped accountJson.
    public static async Task<ValidatedSnapshot> ReadAsync(Stream input, string trustedChannelId,
        CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length,
                MaxMessageBytes + 1 - (int)buffer.Length)), ct);
            if (count == 0) break;
            buffer.Write(chunk, 0, count);
            if (buffer.Length > MaxMessageBytes) throw new InvalidDataException("Snapshot envelope too large.");
        }
        using var document = JsonDocument.Parse(buffer.ToArray());
        RejectDuplicateProperties(document.RootElement);
        var message = document.RootElement.Deserialize<SnapshotMessage>(Options)
            ?? throw new InvalidDataException("Missing snapshot message.");
        return Validate(message, trustedChannelId);
    }

    public static ValidatedSnapshot Validate(SnapshotMessage message, string trustedChannelId)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(message).Length > MaxMessageBytes)
            throw new InvalidDataException("Snapshot envelope too large.");
        if (message.ContractVersion != 1 || !Numeric(message.ChannelId) || message.ChannelId != trustedChannelId ||
            !Numeric(message.UserId) || message.Revision <= 0 || message.SnapshotId == Guid.Empty ||
            message.CapturedAt == default || message.CapturedAt.Offset != TimeSpan.Zero || !Enum.IsDefined(message.Kind))
            throw new InvalidDataException("Invalid snapshot identity, version, revision or UTC timestamp.");
        if (message.Kind == SnapshotKind.Restore
            ? message.ExpectedDeletedRevision is null or <= 0 || message.ExpectedDeletedRevision >= message.Revision
            : message.ExpectedDeletedRevision is not null)
            throw new InvalidDataException("Invalid restore precondition.");
        string? account = null;
        if (message.Kind == SnapshotKind.Delete)
        {
            if (message.AccountJson is not null) throw new InvalidDataException("Delete must not contain an account.");
        }
        else
        {
            account = Project(message.AccountJson ?? throw new InvalidDataException("Missing account."), message.UserId);
        }
        var content = $"{(int)message.Kind}\n{message.ExpectedDeletedRevision}\n{message.CapturedAt:O}\n{account}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return new(message.ChannelId, message.UserId, message.Revision, message.SnapshotId,
            message.CapturedAt, message.Kind, account, message.ExpectedDeletedRevision, hash);
    }

    private static string Project(string json, string userId)
    {
        using var document = JsonDocument.Parse(json);
        RejectDuplicateProperties(document.RootElement);
        var root = Clean(document.RootElement, ["UserId", "DisplayName", "Points", "UpdatedAt", "Rpg"]);
        if (root["UserId"]?.GetValue<string>() != userId ||
            root["DisplayName"]?.GetValue<string>() is not { Length: > 0 and <= 256 } ||
            root["Points"] is not JsonValue points || !points.TryGetValue<long>(out var balance) || balance < 0 ||
            root["UpdatedAt"] is not JsonValue date || !date.TryGetValue<DateTimeOffset>(out var updatedAt) ||
            updatedAt == default || root["Rpg"] is not JsonObject)
            throw new InvalidDataException("Invalid account identity, balance, date or RPG object.");
        var rpg = Clean(JsonSerializer.SerializeToElement(root["Rpg"]),
            ["SchemaVersion", "LegacyFishImported", "Inventory", "Equipment", "Perks", "Fish", "Sushi", "Buffs", "Cooldowns",
                "PlayerLevel", "Experience", "Health", "SushiVersion", "Materials", "TalentRanks", "SelectedDivingZone", "SelectedFishingZone"]);
        foreach(var selected in new[]{"SelectedDivingZone","SelectedFishingZone"})if(rpg.ContainsKey(selected)&&rpg[selected]?.GetValue<string>() is not ("sunny_lagoon" or "sunken_temples" or "shipwreck_graveyard" or "glowing_coral_forest" or "tidal_maelstrom" or "crystal_grotto" or "cursed_depths" or "true_abyss" or "abyss" or "primordial_throne"))throw new InvalidDataException("Invalid selected zone.");
        if (rpg["SchemaVersion"]?.GetValue<int>() != 1) throw new InvalidDataException("Unsupported RPG schema.");
        foreach (var key in new[] { "PlayerLevel", "Experience", "SushiVersion" })
            if (rpg.ContainsKey(key) && (rpg[key] is not JsonValue value ||
                !value.TryGetValue<long>(out var count) || count < (key == "PlayerLevel" ? 1 : 0) ||
                (key != "Experience" && count > int.MaxValue)))
                throw new InvalidDataException("Invalid RPG progression value.");
        if(rpg.ContainsKey("TalentRanks"))
        {
            var ranks=rpg["TalentRanks"]?.Deserialize<Dictionary<string,int>>() ?? throw new InvalidDataException("Invalid talents.");
            TalentRules.Validate(rpg["PlayerLevel"]?.GetValue<int>()??1,ranks);
        }
        if (rpg.ContainsKey("Materials"))
        {
            var materials = Clean(JsonSerializer.SerializeToElement(rpg["Materials"]),
                ["Shells", "WhitePearls", "BluePearls", "RedPearls", "StoneChips", "PearlDust", "LuminousScale", "RuneShell", "EternalDepthCore", "TrueCoralCore", "CursedSilk", "AbyssShard", "ReinforcementCore"]);
            foreach (var key in new[] {"Shells","WhitePearls","BluePearls","RedPearls","StoneChips"})
                if (materials[key] is not JsonValue value || !value.TryGetValue<int>(out var count) || count < 0)
                    throw new InvalidDataException("Invalid RPG material balance.");
            foreach(var field in materials) if(field.Value is not JsonValue v||!v.TryGetValue<int>(out var n)||n<0)throw new InvalidDataException("Invalid material balance.");
            rpg["Materials"] = materials;
        }
        if (rpg.ContainsKey("Health"))
        {
            var health = Clean(JsonSerializer.SerializeToElement(rpg["Health"]),
                ["CurrentHp", "AppliedMaximumHp", "AppliedBaseMaximumHp", "LastPassiveRegen", "SushiRegenRemainder",
                    "LastChatHeal", "BossId", "Defeated", "BossDamage", "BossRemainingHp", "SetShieldHp", "SetShieldSource"]);
            foreach (var field in health)
            {
                if (field.Value is null) continue;
                if (field.Value is not JsonValue value || (field.Key switch
                {
                    "LastPassiveRegen" or "LastChatHeal" => !value.TryGetValue<DateTimeOffset>(out _),
                    "BossId" or "SetShieldSource" => !value.TryGetValue<string>(out _),
                    "Defeated" => !value.TryGetValue<bool>(out _),
                    "SushiRegenRemainder" => !value.TryGetValue<decimal>(out var remainder) || remainder < 0,
                    _ => !value.TryGetValue<long>(out var number) || number < 0
                })) throw new InvalidDataException("Invalid RPG health value.");
            }
            rpg["Health"] = health;
        }
        if (rpg.ContainsKey("LegacyFishImported") &&
            (rpg["LegacyFishImported"] is not JsonValue legacy || !legacy.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Invalid legacy import flag.");
        foreach (var key in new[] { "Inventory", "Buffs" })
        {
            if (rpg[key] is null) { rpg[key] = new JsonArray(); continue; }
            if (rpg[key] is not JsonArray array) throw new InvalidDataException("Expected RPG array.");
            for (var i = 0; i < array.Count; i++)
            {
                var item = Clean(JsonSerializer.SerializeToElement(array[i]), key == "Inventory"
                    ? ["UpgradeLevel", "IsRefined", "InstanceId", "ItemId", "DisplayName", "Description", "IconId", "Rarity", "Kind", "ConsumableRecipeId", "Slot", "Attributes", "FoundAt", "DropWeight", "Collector", "CoreStatsVersion", "SalvageValue", "IsLocked", "IsFavorite", "SetId", "DropActivities", "Effects", "Generation"]
                    : ["RecipeId", "AllActivities", "Activity", "RemainingUses", "Attributes", "Category", "EncounterBased", "ActivatedAt", "LastBossId", "ExpiresAt", "PassiveHealing", "ChatHealing", "BossDamageReduction", "BossLifeleech"]);
                if (item["Attributes"] is not null)
                {
                    item["Attributes"] = Clean(JsonSerializer.SerializeToElement(item["Attributes"]),
                        ["Damage", "CritChance", "CritDamage", "Luck", "BossDamage", "PearlBonus", "FishingLuck", "DiveLuck",
                            "Power", "Stamina", "MaximumHp", "DoubleAttackChance", "TripleAttackChance", "ExtraLootChance",
                            "SushiPreservationChance", "BossDamageReduction", "BossDodgeChance", "CriticalHealing", "RareEquipmentLuck",
                            "EnemyEncounterChance", "MaterialFind", "AttackPercent", "ExperienceBonus", "DivineDamage", "MovementSpeed", "BossLifeleech", "Armor", "ArmorPercent", "MaximumHpPercent", "AttackSpeed", "HealingPower"]);
                    foreach (var attribute in (JsonObject)item["Attributes"]!)
                        if (attribute.Value is not JsonValue number || !number.TryGetValue<decimal>(out _))
                            throw new InvalidDataException("Invalid decimal RPG attribute.");
                }
                foreach (var field in item.ToArray())
                {
                    if(field.Key=="UpgradeLevel") {if(field.Value is not JsonValue v||!v.TryGetValue<int>(out var level)||level is <0 or >9)throw new InvalidDataException("Invalid upgrade level.");continue;}
                    if(field.Key=="IsRefined") {if(field.Value is not JsonValue v||!v.TryGetValue<bool>(out var refined)||refined&&(item["UpgradeLevel"]?.GetValue<int>()??0)!=9)throw new InvalidDataException("Invalid refinement.");continue;}
                    if (field.Key == "Attributes") continue;
                    if (FullRpgFields.Validate(item, field.Key, field.Value)) continue;
                    if (field.Key == "Slot" && field.Value is null) continue;
                    if (field.Key is "Rarity" or "Kind" or "Slot" or "Activity" or "RemainingUses")
                    {
                        var maximum = field.Key switch { "Rarity" => 9, "Kind" or "Activity" => 2, "Slot" => 7, _ => int.MaxValue };
                        if (field.Value is not JsonValue number || !number.TryGetValue<int>(out var value) || value < 0 || value > maximum)
                            throw new InvalidDataException("Invalid enum or remaining uses.");
                    }
                    else if (field.Value is not JsonValue text || !text.TryGetValue<string>(out _))
                        throw new InvalidDataException("Invalid item text or timestamp.");
                    if (field.Key == "FoundAt" && !DateTimeOffset.TryParse(field.Value!.GetValue<string>(), out _))
                        throw new InvalidDataException("Invalid item timestamp.");
                }
                array[i] = item;
            }
        }
        foreach (var key in new[] { "Equipment", "Perks", "Fish", "Sushi", "Cooldowns" })
        {
            if (rpg[key] is null) rpg[key] = new JsonObject();
            if (rpg[key] is not JsonObject map) throw new InvalidDataException("Expected RPG dictionary.");
            foreach (var entry in map.ToArray())
            {
                if (key == "Fish")
                {
                    var fish = Clean(JsonSerializer.SerializeToElement(entry.Value), ["FishId", "Quantity", "FirstCaughtAt", "LastCaughtAt"]);
                    foreach (var field in fish)
                    {
                        if (field.Value is not JsonValue value || (field.Key == "Quantity"
                            ? !value.TryGetValue<int>(out var count) || count < 0
                            : field.Key == "FishId" ? !value.TryGetValue<string>(out _)
                            : !value.TryGetValue<DateTimeOffset>(out _)))
                            throw new InvalidDataException("Invalid fish stack.");
                    }
                    map[entry.Key] = fish;
                }
                else if (entry.Value is not JsonValue value ||
                    (key is "Perks" or "Sushi" ? !value.TryGetValue<int>(out var count) || count < 0 :
                        key == "Cooldowns" ? !value.TryGetValue<DateTimeOffset>(out _) : !value.TryGetValue<string>(out _)))
                    throw new InvalidDataException("Invalid RPG dictionary value.");
            }
        }
        root["Rpg"] = rpg;
        return Canonical(root).ToJsonString();
    }

    private static JsonObject Clean(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected JSON object.");
        var result = new JsonObject();
        foreach (var property in value.EnumerateObject())
        {
            var name = names.FirstOrDefault(n => n.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (name is not null) result[name] = JsonNode.Parse(property.Value.GetRawText());
        }
        return result;
    }
    private static JsonNode Canonical(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : Canonical(p.Value)))),
        JsonArray array => new JsonArray(array.Select(v => v is null ? null : Canonical(v)).ToArray()),
        _ => node.DeepClone()
    };
    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
    private static bool Numeric(string? value) => value is { Length: > 0 and <= 25 } && value.All(c => c is >= '0' and <= '9');
}

