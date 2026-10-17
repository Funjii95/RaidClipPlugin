using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class CharacterStatService
{
    public static decimal EquipmentMaximumHp(RpgProfile profile,int hpPerStamina=5) => profile.Equipment.Values.Distinct()
        .Select(id=>profile.Inventory.SingleOrDefault(i=>i.InstanceId==id))
        .Where(i=>i?.Slot is not null).Sum(i=>Math.Clamp(i!.Attributes.MaximumHp,0,1000000)+Math.Clamp(i.Attributes.Stamina,0,1000000)*hpPerStamina);
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
    public static RpgAttributes Calculate(RpgProfile profile, RpgConfig config, RpgActivity activity, bool includeEffects = true,
        DateTimeOffset? now = null, long stake = 0)
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
            if (item is not null)
            {
                Add(result, item.Attributes);
                if (includeEffects)
                foreach (var effect in item.Effects.Where(e => !e.RequiresCritical && (!e.Activity.HasValue || e.Activity == activity) && stake >= e.MinimumStake &&
                    (!profile.Cooldowns.TryGetValue("effect:" + item.InstanceId + ":" + e.Id, out var until) || (now ?? DateTimeOffset.UtcNow) >= until)))
                {
                    switch (effect.Trigger)
                    {
                        case RpgEffectTrigger.DoubleAttack when activity == RpgActivity.Boss: result.DoubleAttackChance += effect.Chance; break;
                        case RpgEffectTrigger.TripleAttack when activity == RpgActivity.Boss: result.TripleAttackChance += effect.Chance; break;
                        case RpgEffectTrigger.ExtraLoot when activity == RpgActivity.Dive: result.ExtraLootChance += effect.Chance; break;
                        case RpgEffectTrigger.SushiPreservation: result.SushiPreservationChance += effect.Chance; break;
                    }
                }
            }
        }
        foreach (var buff in profile.Buffs.Where(b => b.Activity == activity && RpgSushiService.Active(b,now??DateTimeOffset.UtcNow))) Add(result, buff.Attributes);
        foreach (var progress in RpgSetService.Progress(profile, config))
        foreach (var bonus in progress.Set.Bonuses.Where(b => progress.Equipped >= b.RequiredPieces)) Add(result, bonus.Attributes);
        result.CritChance = Math.Clamp(result.CritChance, 0, Math.Clamp(config.CritChanceCap, 0, 100));
        result.CritDamage = Math.Clamp(result.CritDamage, 0, Math.Clamp(config.CritDamageCap, 0, 1000));
        result.Luck = Math.Clamp(result.Luck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.DiveLuck = Math.Clamp(result.DiveLuck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.FishingLuck = Math.Clamp(result.FishingLuck, 0, Math.Clamp(config.LuckCap, 0, 1000));
        result.Damage = Math.Clamp(result.Damage + result.Power*config.PowerDamagePerPoint, 0, Math.Clamp(config.DamageBonusCap, 0, 1000));
        result.BossDamage = Math.Clamp(result.BossDamage, 0, Math.Clamp(config.DamageBonusCap, 0, 1000));
        result.PearlBonus = Math.Clamp(result.PearlBonus, 0, Math.Clamp(config.PearlBonusCap, 0, 15));
        result.DoubleAttackChance = Math.Clamp(result.DoubleAttackChance, 0, Math.Clamp(config.DoubleAttackChanceCap, 0, 20));
        result.TripleAttackChance = Math.Clamp(result.TripleAttackChance, 0, Math.Clamp(config.TripleAttackChanceCap, 0, 5));
        result.ExtraLootChance = Math.Clamp(result.ExtraLootChance, 0, Math.Clamp(config.ExtraLootChanceCap, 0, 10));
        result.SushiPreservationChance = Math.Clamp(result.SushiPreservationChance, 0, Math.Clamp(config.SushiPreservationChanceCap, 0, 20));
        result.MaximumHp = Math.Clamp(EquipmentMaximumHp(profile,config.Health.HpPerStamina),0,config.Health.EquipmentHpCap);
        return result;
    }

    private static void Add(RpgAttributes a, RpgAttributes b, int count = 1)
    {
        a.Power+=b.Power*count;a.Stamina+=b.Stamina*count;
        a.Damage += b.Damage * count; a.CritChance += b.CritChance * count;
        a.CritDamage += b.CritDamage * count; a.Luck += b.Luck * count;
        a.BossDamage += b.BossDamage * count; a.PearlBonus += b.PearlBonus * count;
        a.FishingLuck += b.FishingLuck * count; a.DiveLuck += b.DiveLuck * count;
        a.DoubleAttackChance += b.DoubleAttackChance * count; a.TripleAttackChance += b.TripleAttackChance * count;
        a.ExtraLootChance += b.ExtraLootChance * count; a.SushiPreservationChance += b.SushiPreservationChance * count;
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
