using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Local authoritative operations; adapters supply the verified Twitch user ID, never a target username.
public sealed class RpgProgressionService(ViewerPointStore points, RpgConfig config)
{
    public Task<RpgTransactionResult> BuyPerkAsync(string userId, string name, string requestId, string perkId, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var perk = config.Perks.SingleOrDefault(p => p.Id == perkId);
            if (perk is null) return new(false, "Unbekannter Perk.");
            var level = account.Rpg.Perks.GetValueOrDefault(perkId);
            if (level >= perk.MaximumLevel) return new(false, "Maximallevel erreicht.");
            var cost = perk.Cost(level);
            if (account.Points < cost) return new(false, "Nicht genügend Perlen.");
            account.Points -= cost;
            account.Rpg.Perks[perkId] = level + 1;
            return new(true);
        }, token);

    public Task<RpgTransactionResult> EquipAsync(string userId, string name, string requestId, string instanceId, EquipmentSlot slot, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var item = account.Rpg.Inventory.SingleOrDefault(i => i.InstanceId == instanceId);
            if (item?.Slot is null || !Enum.IsDefined(slot)) return new(false, "Gegenstand nicht ausrüstbar.");
            var ring = item.Slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2;
            if (item.Slot != slot && !(ring && slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2)) return new(false, "Unpassender Ausrüstungsplatz.");
            foreach (var old in account.Rpg.Equipment.Where(p => p.Value == instanceId).Select(p => p.Key).ToArray()) account.Rpg.Equipment.Remove(old);
            account.Rpg.Equipment[slot] = instanceId;
            return new(true);
        }, token);

    public Task<RpgTransactionResult> CraftAsync(string userId, string name, string requestId, string recipeId, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var recipe = config.Recipes.SingleOrDefault(r => r.Id == recipeId);
            if (recipe is null) return new(false, "Unbekanntes Rezept.");
            if (recipe.PearlCost < 0 || recipe.FishQuantity < 1) throw new InvalidOperationException("Ungültiges Rezept.");
            if (!account.Rpg.Fish.TryGetValue(recipe.FishId, out var fish) || fish.Quantity < recipe.FishQuantity || account.Points < recipe.PearlCost)
                return new(false, "Nicht genügend Fische oder Perlen.");
            account.Points -= recipe.PearlCost;
            fish.Quantity -= recipe.FishQuantity;
            account.Rpg.Sushi[recipeId] = checked(account.Rpg.Sushi.GetValueOrDefault(recipeId) + 1);
            return new(true);
        }, token);

    public Task<RpgTransactionResult> ActivateSushiAsync(string userId, string name, string requestId, string recipeId, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var recipe = config.Recipes.SingleOrDefault(r => r.Id == recipeId);
            if (recipe is null || account.Rpg.Sushi.GetValueOrDefault(recipeId) < 1) return new(false, "Kein passendes Sushi vorhanden.");
            if (account.Rpg.Buffs.Any(b => b.RecipeId == recipeId && b.RemainingUses > 0)) return new(false, "Dieser Buff ist bereits aktiv.");
            account.Rpg.Buffs.RemoveAll(b => b.RemainingUses <= 0);
            if (account.Rpg.Buffs.Count >= Math.Clamp(config.MaximumDifferentBuffs, 1, 10)) return new(false, "Zu viele aktive Buffs.");
            account.Rpg.Sushi[recipeId]--;
            account.Rpg.Buffs.Add(new() { RecipeId = recipeId, Activity = recipe.Activity,
                RemainingUses = Math.Clamp(config.BuffUses, 1, 100), Attributes = recipe.Attributes });
            return new(true);
        }, token);

    internal static void ConsumeBuffs(RpgProfile profile, RpgActivity activity)
    {
        foreach (var buff in profile.Buffs.Where(b => b.Activity == activity && b.RemainingUses > 0)) buff.RemainingUses--;
        profile.Buffs.RemoveAll(b => b.RemainingUses <= 0);
    }
}
