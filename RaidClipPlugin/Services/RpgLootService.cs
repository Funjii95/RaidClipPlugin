using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record LootChanceBreakdown(decimal Base, decimal Stake, decimal Luck, decimal Event, decimal Total, ItemRarity MaximumRarity,
    decimal Sushi = 0, decimal Equipment = 0);
public sealed record RpgLootResult(RpgItem? Item, LootChanceBreakdown Chances);

/// <summary>Pure trusted loot calculation. The caller must supply the stake actually
/// debited by its transaction, not an unverified amount from chat or a frontend.</summary>
public sealed class RpgLootService(IDuelRandom? random = null)
{
    private readonly IDuelRandom _random = random ?? new CryptoDuelRandom();

    public static LootChanceBreakdown Chances(long paidStake, decimal effectiveLuck, RpgLootConfig config, decimal sushiLuck = 0, decimal equipmentLuck = 0, decimal hotTimePercent = 0)
    {
        config.Validate();
        if (paidStake < 0) throw new ArgumentOutOfRangeException(nameof(paidStake));
        var tier = config.RiskTiers.Where(t => t.MinimumStake <= paidStake).MaxBy(t => t.MinimumStake)!;
        var maximum = tier.MaximumRarity;
        if (paidStake < config.LegendaryMinimumStake && maximum == ItemRarity.Legendary) maximum = ItemRarity.Epic;
        var perkPoints = Math.Clamp(effectiveLuck, 0, 1000);
        var sushiPoints = Math.Clamp(sushiLuck, 0, 1000);
        var equipmentPoints = Math.Clamp(equipmentLuck, 0, 1000);
        var combined = perkPoints + sushiPoints + equipmentPoints;
        var bonus = Math.Min(combined * config.LuckChancePerPoint, config.MaximumLuckBonus);
        var factor = combined > 0 ? bonus / combined : 0;
        var luck = perkPoints * factor;
        var eventBonus = Math.Clamp(config.EventBonus, 0, config.MaximumEventBonus);
        var hotBonus=config.BaseDropChance*Math.Clamp(hotTimePercent,0,200)/100;
        return new(config.BaseDropChance, tier.DropBonus, luck, eventBonus+hotBonus,
            Math.Clamp(config.BaseDropChance + hotBonus + tier.DropBonus + bonus + eventBonus, 0, Math.Min(100,config.MaximumDropChance)), maximum,
            sushiPoints * factor, equipmentPoints * factor);
    }

    public RpgLootResult Roll(long paidStake, decimal effectiveLuck, RpgLootConfig config, DateTimeOffset now, decimal sushiLuck = 0, decimal equipmentLuck = 0, decimal hotTimePercent = 0)
    {
        var chances = Chances(paidStake, effectiveLuck, config, sushiLuck, equipmentLuck,hotTimePercent);
        return Select(config, now, chances, RpgActivity.Dive);
    }
    public RpgLootResult RollReward(RpgLootConfig config, DateTimeOffset now, RpgActivity activity, decimal chance, ItemRarity maximum, decimal hotTimePercent=0)
    {
        config.Validate();
        if (activity == RpgActivity.Dive || chance is < 0 or > 100 || !Enum.IsDefined(maximum)) throw new ArgumentException("Ungültige Belohnungs-Lootquelle.");
        var total=Math.Min(Math.Min(100,config.MaximumDropChance),chance*(1+Math.Clamp(hotTimePercent,0,200)/100));
        return Select(config, now, new(chance, 0, 0, total-chance, total, maximum), activity);
    }
    public RpgLootResult RollEnemy(long paidStake,decimal luck,RpgLootConfig config,DateTimeOffset now,decimal sushi=0,decimal equipment=0,decimal hotTimePercent=0)
    {
        // Best of two, but only one reward. Same stake gates, pool and item generation.
        var first=Roll(paidStake,luck,config,now,sushi,equipment,hotTimePercent);
        var second=Roll(paidStake,luck,config,now,sushi,equipment,hotTimePercent);
        return first.Item is null || second.Item is not null && second.Item.Rarity>first.Item.Rarity ? second : first;
    }
    private RpgLootResult Select(RpgLootConfig config, DateTimeOffset now, LootChanceBreakdown chances, RpgActivity activity)
    {
        var pool = config.Items.Where(i => i.DropActivities.Contains(activity)).ToArray();
        // Gate the complete candidate table BEFORE either rarity or item selection.
        var eligible = config.RarityWeights.Where(p => p.Key <= chances.MaximumRarity && p.Value > 0 && pool.Any(i => i.Rarity == p.Key)).OrderBy(p => p.Key).ToArray();
        if (eligible.Length == 0 || _random.NextInclusive(1, 10000) > chances.Total * 100) return new(null, chances);
        var roll = _random.NextInclusive(1, eligible.Sum(p => p.Value));
        var selected = eligible[0].Key;
        foreach (var pair in eligible) { selected = pair.Key; roll -= pair.Value; if (roll <= 0) break; }
        var candidates = pool.Where(i => i.Rarity == selected).ToArray();
        var item = JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(candidates[_random.NextInclusive(0, candidates.Length - 1)]))!;
        item.InstanceId = Guid.NewGuid().ToString("N"); item.FoundAt = now;
        RpgItemGenerationService.Generate(item, config, _random);
        return new(item, chances);
    }
}
