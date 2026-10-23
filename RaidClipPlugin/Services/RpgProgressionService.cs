using RaidClipPlugin.Config;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Local authoritative operations; adapters supply the verified Twitch user ID, never a target username.
public sealed class RpgProgressionService(ViewerPointStore points, RpgConfig config,Func<DateTimeOffset>? clock=null)
{
    public Task<RpgTransactionResult> UnequipAsync(string userId, string name, string requestId, EquipmentSlot slot, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            if (!Enum.IsDefined(slot) || !account.Rpg.Equipment.ContainsKey(slot)) return new(false, "Dieser Ausrüstungsplatz ist leer oder ungültig.");
            if(config.Health.Enabled) RpgHealthService.Refresh(account.Rpg,config.Health,DateTimeOffset.UtcNow);
            account.Rpg.Equipment.Remove(slot);
            RpgSetCombatService.Refresh(account.Rpg,config.Health,clock?.Invoke()??DateTimeOffset.UtcNow);
            if(config.Health.Enabled) RpgHealthService.Refresh(account.Rpg,config.Health,DateTimeOffset.UtcNow);
            return new(true);
        }, token);
    public Task<RpgTransactionResult> BuyPerkAsync(string userId, string name, string requestId, string perkId, CancellationToken token, bool maximum = false) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var perk = config.Perks.SingleOrDefault(p => p.Id == perkId);
            if (perk is null) return new(false, "Unbekannter Perk. Verfügbare IDs: " + string.Join(", ", config.Perks.Select(p => p.Id)) + ". Kauf: !perk <ID> (Alias !buyperk).");
            var level = account.Rpg.Perks.GetValueOrDefault(perkId);
            if (level >= perk.MaximumLevel) return new(false, "Maximallevel erreicht.");
            var nextLevel=level; long cost=0;
            do { var nextCost=perk.Cost(nextLevel); if(nextCost>account.Points-cost)break; cost=checked(cost+nextCost); nextLevel++; } while(maximum && nextLevel<perk.MaximumLevel);
            if(nextLevel==level) return new(false, $"Nicht genügend Perlen. Kosten: {RpgChatPresentation.Number(perk.Cost(level))}, vorhanden: {RpgChatPresentation.Number(account.Points)} Perlen.");
            if (account.Points < cost) return new(false, $"Nicht genügend Perlen. Kosten: {RpgChatPresentation.Number(cost)}, vorhanden: {RpgChatPresentation.Number(account.Points)} Perlen.");
            account.Points -= cost;
            account.Rpg.Perks[perkId] = nextLevel;
            if(config.Health.Enabled)RpgHealthService.Refresh(account.Rpg,config.Health,clock?.Invoke()??DateTimeOffset.UtcNow);
            return new(true, Details: new(RpgActionKind.BuyPerk, RpgChatPresentation.PerkName(perk), Cost: cost,
                Level: nextLevel, Bonuses: RpgChatPresentation.Snapshot(perk.BonusPerLevel, nextLevel),
                NextCost: nextLevel < perk.MaximumLevel ? perk.Cost(nextLevel) : null, MaximumLevel: perk.MaximumLevel,EffectDescription:RpgPerkCatalog.Find(perkId) is {Value:>0} entry?$"{entry.Value*nextLevel} {entry.Label}":""));
        }, token);

    public Task<RpgTransactionResult> EquipAsync(string userId, string name, string requestId, string instanceId, EquipmentSlot slot, CancellationToken token) =>
        EquipSelectionAsync(userId, name, requestId, instanceId, slot, token);

    public Task<RpgTransactionResult> EquipSelectionAsync(string userId, string name, string requestId, string selection, EquipmentSlot? requestedSlot, CancellationToken token)
    {
        var numbered = int.TryParse(selection, out var number);
        var view = numbered ? RpgInventoryNumbers.For(points).Read(userId) : null;
        return
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            if (numbered && view is null) return new(false, "Bitte zuerst !inv anzeigen, dann !equip <Nummer> verwenden.");
            if (numbered && !RpgInventoryNumbers.Matches(account.Rpg, view!)) return new(false, "Dein Inventar hat sich geändert. Bitte !inv erneut anzeigen; nichts wurde ausgerüstet.");
            if (numbered && (number < 1 || number > view!.Length)) return new(false, $"Gegenstand {number} existiert nicht. Übersicht: !inv.");
            var instanceId = numbered ? view![number - 1] : selection;
            var item = account.Rpg.Inventory.SingleOrDefault(i => i.InstanceId == instanceId);
            if (item is null) return new(false, "Dieser Gegenstand ist nicht in deinem Inventar.");
            // Persisted slot remains authoritative for legacy items without Kind metadata.
            if (item.Slot is null)
                return new(false, $"{RpgChatPresentation.ItemName(item)} ist " + (item.Kind == RpgItemKind.Consumable ? "ein Verbrauchsgegenstand" : "ein Schatz") + " und kann nicht ausgerüstet werden.");
            var ring = item.Slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2;
            var slot = requestedSlot ?? item.Slot.Value;
            if (ring && requestedSlot is null)
            {
                var existing = account.Rpg.Equipment.Where(p => p.Value == instanceId).Select(p => (EquipmentSlot?)p.Key).FirstOrDefault();
                if (existing.HasValue) slot = existing.Value;
                else if (!account.Rpg.Equipment.ContainsKey(EquipmentSlot.Ring1)) slot = EquipmentSlot.Ring1;
                else if (!account.Rpg.Equipment.ContainsKey(EquipmentSlot.Ring2)) slot = EquipmentSlot.Ring2;
                else return new(false, $"Beide Ringplätze sind belegt. Wähle !equip {selection} ring1 oder !equip {selection} ring2 zum Ersetzen.");
            }
            if (!Enum.IsDefined(slot)) return new(false, "Ungültiger Ausrüstungsplatz.");
            if (item.Slot != slot && !(ring && slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2)) return new(false, "Unpassender Ausrüstungsplatz.");
            if(config.Health.Enabled) RpgHealthService.Refresh(account.Rpg,config.Health,DateTimeOffset.UtcNow);
            foreach (var old in account.Rpg.Equipment.Where(p => p.Value == instanceId).Select(p => p.Key).ToArray()) account.Rpg.Equipment.Remove(old);
            account.Rpg.Equipment[slot] = instanceId;
            RpgSetCombatService.Refresh(account.Rpg,config.Health,clock?.Invoke()??DateTimeOffset.UtcNow);
            if(config.Health.Enabled) RpgHealthService.Refresh(account.Rpg,config.Health,DateTimeOffset.UtcNow);
            return new(true, Details: new(RpgActionKind.Equip, RpgChatPresentation.ItemName(item),
                Bonuses: RpgChatPresentation.Snapshot(RpgEquipmentUpgradeRules.Effective(item)), Slot: slot,
                Effects: JsonSerializer.Deserialize<List<RpgItemEffect>>(JsonSerializer.Serialize(item.Effects)),
                SetName: config.Sets.FirstOrDefault(s => s.Id == item.SetId)?.Name ?? ""));
        }, token);
    }

    public Task<RpgTransactionResult> CraftAsync(string userId, string name, string requestId, string recipeId, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var recipe = config.Recipes.SingleOrDefault(r => r.Id == recipeId);
            if (recipe is null) return new(false, "Unbekanntes Rezept.");
            if (recipe.PearlCost < 0 || recipe.FishQuantity < 1) throw new InvalidOperationException("Ungültiges Rezept.");
            var ingredients=RpgSushiService.Ingredients(recipe);
            if (ingredients.Any(i=>i.Value<1 || account.Rpg.Fish.GetValueOrDefault(i.Key)?.Quantity<i.Value || !account.Rpg.Fish.ContainsKey(i.Key)) || account.Points < recipe.PearlCost)
                return new(false, "Nicht genügend Fische oder Perlen.");
            account.Points -= recipe.PearlCost;
            foreach(var ingredient in ingredients)account.Rpg.Fish[ingredient.Key].Quantity-=ingredient.Value;
            account.Rpg.Sushi[recipeId] = checked(account.Rpg.Sushi.GetValueOrDefault(recipeId) + 1);
            return new(true, Details: new(RpgActionKind.Craft, RpgChatPresentation.RecipeName(recipe), Cost: recipe.PearlCost,
                FishName: config.FishSpecies.SingleOrDefault(f => f.Id == recipe.FishId)?.Name is { Length: > 0 } fishName && !string.IsNullOrWhiteSpace(fishName) ? fishName : recipe.FishId,
                FishQuantity: recipe.FishQuantity, Quantity: 1, Bonuses: RpgChatPresentation.Snapshot(recipe.Attributes),
                Activity: recipe.Activity, RemainingUses: RpgSushiService.Uses(recipe,config),IngredientsDescription:RpgSushiService.IngredientsText(recipe,config),EffectDescription:RpgSushiService.Effects(recipe),EncounterBased:false,DurationSeconds:RpgSushiService.DurationSeconds));
        }, token);

    public Task<RpgTransactionResult> ActivateSushiAsync(string userId, string name, string requestId, string recipeId, CancellationToken token) =>
        points.ApplyRpgAsync(userId, name, requestId, account =>
        {
            if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
            var recipe = config.Recipes.SingleOrDefault(r => r.Id == recipeId);
            if (recipe is null || account.Rpg.Sushi.GetValueOrDefault(recipeId) < 1) return new(false, "Kein passendes Sushi vorhanden.");
            var now=clock?.Invoke()??DateTimeOffset.UtcNow;
            RpgHealthService.Refresh(account.Rpg,config.Health,now);
            if (account.Rpg.Buffs.Any(b => b.Category==recipe.Category && RpgSushiService.Active(b,now))) return new(false, "Dieser Sushi-Slot ist bereits aktiv/belegt. Erst den laufenden Buff beenden lassen.");
            account.Rpg.Buffs.RemoveAll(b => !RpgSushiService.Active(b,now));
            account.Rpg.Sushi[recipeId]--;
            account.Rpg.Buffs.Add(new() { RecipeId = recipeId, Activity = recipe.Activity,
                RemainingUses = RpgSushiService.Uses(recipe,config), Attributes = JsonSerializer.Deserialize<RpgAttributes>(JsonSerializer.Serialize(recipe.Attributes))!,
                Category=recipe.Category,AllActivities=recipe.AllActivities,EncounterBased=false,ActivatedAt=now,ExpiresAt=now.AddHours(1),PassiveHealing=recipe.PassiveHealing,ChatHealing=recipe.ChatHealing,BossDamageReduction=recipe.BossDamageReduction,BossLifeleech=recipe.BossLifeleech });
            var buff = account.Rpg.Buffs[^1];
            var factor=1+RpgPerkCatalog.Bonus(account.Rpg,"sushiPower")/100;
            buff.Attributes=RpgChatPresentation.Snapshot(buff.Attributes,factor);
            buff.PassiveHealing*=factor;buff.ChatHealing=(int)decimal.Floor(buff.ChatHealing*factor);
            buff.BossDamageReduction=Math.Min(80,buff.BossDamageReduction*factor);buff.BossLifeleech=Math.Min(100,buff.BossLifeleech*factor);
            return new(true, Details: new(RpgActionKind.ActivateSushi, RpgChatPresentation.RecipeName(recipe),
                Bonuses: RpgChatPresentation.Snapshot(buff.Attributes), Activity: buff.Activity, RemainingUses: buff.RemainingUses,EffectDescription:RpgSushiService.Effects(recipe),EncounterBased:false,DurationSeconds:RpgSushiService.DurationSeconds));
        }, token);

    internal static void ConsumeBuffs(RpgProfile profile, RpgActivity activity)
    {
        foreach (var buff in profile.Buffs.Where(b => b.ExpiresAt is null && b.Activity == activity && b.RemainingUses > 0)) buff.RemainingUses--;
        profile.Buffs.RemoveAll(b => b.ExpiresAt is null && b.RemainingUses <= 0);
    }
}

