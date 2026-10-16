using System.Text.Json;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

/// <summary>Explicit opt-in only: never invoked while loading saved settings.</summary>
public static class RpgEconomyPreset
{
    public static RpgConfig Apply12(RpgConfig current)
    {
        var copy = JsonSerializer.Deserialize<RpgConfig>(JsonSerializer.Serialize(current))!;
        var defaults = new RpgConfig();
        var loot = copy.DiveLoot;
        var target = defaults.DiveLoot;
        loot.BaseDropChance = target.BaseDropChance;
        loot.MaximumDropChance = target.MaximumDropChance;
        loot.LuckChancePerPoint = target.LuckChancePerPoint;
        loot.MaximumLuckBonus = target.MaximumLuckBonus;
        loot.EventBonus = target.EventBonus;
        loot.MaximumEventBonus = target.MaximumEventBonus;
        loot.LegendaryMinimumStake = target.LegendaryMinimumStake;
        loot.RiskTiers = target.RiskTiers;
        loot.RarityWeights = target.RarityWeights;
        foreach (var perk in copy.Perks.Where(p => defaults.Perks.Any(d => d.Id == p.Id)))
        { perk.MaximumLevel = 5; perk.BaseCost = 500; perk.CostGrowth = 2; }
        foreach (var recipe in copy.Recipes)
        {
            var standard = defaults.Recipes.SingleOrDefault(r => r.Id == recipe.Id);
            if (standard is not null) recipe.PearlCost = standard.PearlCost;
        }
        copy.Validate();
        return copy;
    }
}
