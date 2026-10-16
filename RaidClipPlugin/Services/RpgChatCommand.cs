using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class RpgChatCommand(ViewerPointStore points, RpgConfig config)
{
    public static bool Recognizes(string command) => command is "!inventory" or "!inventar" or "!equipment" or "!ausrüstung" or "!equip" or "!perks" or "!perk" or "!sushi" or "!craft" or "!stats";
    public async Task<string> ExecuteAsync(ChatMessage user, string command, string[] parts, CancellationToken token)
    {
        if (!config.Enabled) return "RPG ist aktuell deaktiviert.";
        var progression = new RpgProgressionService(points, config);
        var id = string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id;
        RpgTransactionResult? action = null;
        if (command == "!perk" && parts.Length == 2) action = await progression.BuyPerkAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!craft" && parts.Length == 2) action = await progression.CraftAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!sushi" && parts.Length == 2) action = await progression.ActivateSushiAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!equip" && parts.Length == 3 && Enum.TryParse<EquipmentSlot>(parts[2], true, out var slot))
            action = await progression.EquipAsync(user.UserId, user.UserName, id, parts[1], slot, token);
        if (action is not null) return $"@{user.UserName}, " + (action.Success ? $"erledigt! Perlen: {action.Balance}." : action.Error);
        var account = (await points.GetProfileAsync(user.UserId, token)).Entry;
        var profile = account.Rpg;
        var page = parts.Length == 2 && int.TryParse(parts[1], out var requested) ? Math.Max(1, requested) : 1;
        if (command is "!inventory" or "!inventar")
        {
            var entries = profile.Inventory.Skip((int)Math.Min(int.MaxValue, (long)(page - 1) * 3)).Take(3)
                .Select(i => $"{i.DisplayName} [{i.Rarity}] ID:{i.InstanceId}");
            return $"@{user.UserName}, Inventar {page}/{Math.Max(1, (profile.Inventory.Count + 2) / 3)}: " + string.Join(" | ", entries);
        }
        if (command is "!equipment" or "!ausrüstung") return $"@{user.UserName}, Ausrüstung: " + string.Join(" | ", profile.Equipment.Select(p => p.Key + ": " + profile.Inventory.FirstOrDefault(i => i.InstanceId == p.Value)?.DisplayName));
        if (command == "!perks") return $"@{user.UserName}, Perks: " + string.Join(" | ", config.Perks.Select(p =>
            p.Id + " " + profile.Perks.GetValueOrDefault(p.Id) + "/" + p.MaximumLevel +
            (profile.Perks.GetValueOrDefault(p.Id) >= p.MaximumLevel ? " MAX" : " Kosten:" + p.Cost(profile.Perks.GetValueOrDefault(p.Id)))));
        if (command == "!sushi") return $"@{user.UserName}, Sushi: " + string.Join(", ", profile.Sushi.Select(p => p.Key + ":" + p.Value)) +
            " | Buffs: " + string.Join(", ", profile.Buffs.Select(b => b.RecipeId + ":" + b.RemainingUses)) + " | Aktivieren: !sushi <Rezept>";
        if (command == "!stats")
        {
            var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss);
            return $"@{user.UserName}, Boss-Werte: Schaden +{stats.Damage}%, Crit {stats.CritChance}%, Crit-Schaden +{stats.CritDamage}%, Boss +{stats.BossDamage}%, Glück {stats.Luck}.";
        }
        return "Nutzung: !perk <ID> | !equip <Item-ID> <Weapon|Helmet|Chest|Gloves|Boots|Ring1|Ring2|Amulet> | !craft <Rezept> | !sushi <Rezept>. Rezepte: " + string.Join(", ", config.Recipes.Select(r => $"{r.Id} ({r.FishQuantity}x {r.FishId}, {r.PearlCost} Perlen)"));
    }
}
