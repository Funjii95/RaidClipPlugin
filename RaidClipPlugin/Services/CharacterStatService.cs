using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class CharacterStatService
{
    public static (decimal Perks, decimal Equipment, decimal Sushi) DiveLuckSources(RpgProfile profile, RpgConfig config)
    {
        static decimal Luck(RpgAttributes stats) => stats.Luck + stats.DiveLuck;
        var perks = Luck(Calculate(new() { Perks = profile.Perks }, config, RpgActivity.Dive));
        var equipment = Luck(Calculate(new() { Inventory = profile.Inventory, Equipment = profile.Equipment }, config, RpgActivity.Dive));
        var sushi = Luck(Calculate(new() { Buffs = profile.Buffs }, config, RpgActivity.Dive));
        var total = Luck(Calculate(profile, config, RpgActivity.Dive));
        var sum = perks + equipment + sushi;
        var factor = sum > 0 ? Math.Min(1, total / sum) : 0;
        return (perks * factor, equipment * factor, sushi * factor);
    }
    public static RpgAttributes Calculate(RpgProfile profile, RpgConfig config, RpgActivity activity)
    {
        var result = new RpgAttributes();
        foreach (var pair in profile.Perks)
        {
            var definition = config.Perks.SingleOrDefault(p => p.Id == pair.Key);
            if (definition is not null) Add(result, definition.BonusPerLevel, Math.Clamp(pair.Value, 0, definition.MaximumLevel));
        }
        foreach (var id in profile.Equipment.Values.Distinct())
        {
            var item = profile.Inventory.SingleOrDefault(i => i.InstanceId == id);
            if (item is not null) Add(result, item.Attributes);
        }
        foreach (var buff in profile.Buffs.Where(b => b.Activity == activity && b.RemainingUses > 0)) Add(result, buff.Attributes);
        result.CritChance = Math.Clamp(result.CritChance, 0, Math.Clamp(config.CritChanceCap, 0, 100));
        result.CritDamage = Math.Clamp(result.CritDamage, 0, Math.Clamp(config.CritDamageCap, 0, 1000));
        result.Luck = Math.Clamp(result.Luck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.DiveLuck = Math.Clamp(result.DiveLuck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.FishingLuck = Math.Clamp(result.FishingLuck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.Damage = Math.Clamp(result.Damage, 0, Math.Clamp(config.DamageBonusCap, 0, 1000));
        result.BossDamage = Math.Clamp(result.BossDamage, 0, Math.Clamp(config.DamageBonusCap, 0, 1000));
        result.PearlBonus = Math.Clamp(result.PearlBonus, 0, Math.Clamp(config.DamageBonusCap, 0, 1000));
        return result;
    }

    private static void Add(RpgAttributes a, RpgAttributes b, int count = 1)
    {
        a.Damage += b.Damage * count; a.CritChance += b.CritChance * count;
        a.CritDamage += b.CritDamage * count; a.Luck += b.Luck * count;
        a.BossDamage += b.BossDamage * count; a.PearlBonus += b.PearlBonus * count;
        a.FishingLuck += b.FishingLuck * count; a.DiveLuck += b.DiveLuck * count;
    }

    public static int BossDamage(int baseDamage, RpgAttributes stats, decimal rollPercent, out bool critical, decimal baseCritMultiplier = 1.5m)
    {
        if (baseDamage < 0 || rollPercent is < 0 or >= 100) throw new ArgumentOutOfRangeException();
        critical = rollPercent < stats.CritChance;
        var damage = baseDamage * (1 + stats.Damage / 100) * (1 + stats.BossDamage / 100);
        if (critical) damage *= Math.Clamp(baseCritMultiplier, 1, 10) + stats.CritDamage / 100;
        return checked((int)decimal.Floor(damage));
    }
}
