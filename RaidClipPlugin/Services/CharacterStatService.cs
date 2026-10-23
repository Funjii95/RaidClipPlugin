using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class CharacterStatService
{
    public static decimal EquipmentMaximumHp(RpgProfile profile,int hpPerStamina=5, IReadOnlyList<RpgItemSet>? sets=null) => profile.Equipment.Values.Distinct()
        .Select(id=>profile.Inventory.SingleOrDefault(i=>i.InstanceId==id))
        .Where(i=>i?.Slot is not null).Sum(i=>Math.Clamp(RpgEquipmentUpgradeRules.Effective(i!).MaximumHp,0,1000000)+Math.Clamp(RpgEquipmentUpgradeRules.Effective(i!).Stamina,0,1000000)*hpPerStamina)
        + (sets??Array.Empty<RpgItemSet>()).Sum(s=>s.Bonuses.Where(b=>profile.Inventory.Where(i=>i.SetId==s.Id && s.ItemIds.Contains(i.ItemId) && profile.Equipment.Values.Contains(i.InstanceId)).Select(i=>i.ItemId).Distinct().Count()>=b.RequiredPieces)
            .Sum(b=>Math.Max(0,b.Attributes.MaximumHp)+Math.Max(0,b.Attributes.Stamina)*hpPerStamina));
    public static (decimal Perks, decimal Equipment, decimal Sushi) DiveLuckSources(RpgProfile profile, RpgConfig config)
    {
        static decimal Luck(RpgAttributes stats) => stats.Luck + stats.DiveLuck;
        var perks = Luck(Calculate(new() { Perks = profile.Perks, TalentRanks=profile.TalentRanks }, config, RpgActivity.Dive));
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
        var enhanced=RpgEternalCombat.Enhanced(profile);
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
                Add(result, RpgEquipmentUpgradeRules.Effective(item));
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
        foreach (var buff in profile.Buffs.Where(b => (b.Activity == activity || b.AllActivities) && RpgSushiService.Active(b,now??DateTimeOffset.UtcNow))) Add(result, buff.Attributes);
        foreach (var progress in RpgSetService.Progress(profile, config))
        foreach (var bonus in progress.Set.Bonuses.Where(b => progress.Equipped >= b.RequiredPieces)) Add(result, bonus.Attributes);
        result.CritChance = Math.Clamp(result.CritChance, 0, (enhanced?100:Math.Clamp(config.CritChanceCap, 0, 100)));
        result.CritDamage = Math.Clamp(result.CritDamage, 0, (enhanced?10000:Math.Clamp(config.CritDamageCap, 0, 1000)));
        result.Luck = Math.Clamp(result.Luck, 0, (enhanced?10000:Math.Clamp(config.LuckCap, 0, 1000)));
        result.DiveLuck = Math.Clamp(result.DiveLuck, 0, (enhanced?10000:Math.Clamp(config.LuckCap, 0, 1000)));
        result.FishingLuck = Math.Clamp(result.FishingLuck, 0, (enhanced?10000:Math.Clamp(config.LuckCap, 0, 1000)));
        result.Damage = Math.Clamp(result.Damage + result.Power*config.PowerDamagePerPoint, 0, (enhanced?10000:Math.Clamp(config.DamageBonusCap, 0, 1000)));
        result.BossDamage = Math.Clamp(result.BossDamage, 0, (enhanced?10000:Math.Clamp(config.DamageBonusCap, 0, 1000)));
        result.PearlBonus = Math.Clamp(result.PearlBonus, 0, (enhanced?10000:Math.Clamp(config.PearlBonusCap, 0, 15)));
        result.DoubleAttackChance = Math.Clamp(result.DoubleAttackChance, 0, (enhanced?100:Math.Clamp(config.DoubleAttackChanceCap, 0, 20)));
        result.TripleAttackChance = Math.Clamp(result.TripleAttackChance, 0, Math.Clamp(config.TripleAttackChanceCap, 0, 5));
        result.ExtraLootChance = Math.Clamp(result.ExtraLootChance+RpgPerkCatalog.Bonus(profile,"extraLoot"), 0, Math.Min(100,Math.Clamp(config.ExtraLootChanceCap, 0, 10)+RpgPerkCatalog.Bonus(profile,"extraLoot")));
        result.SushiPreservationChance = Math.Clamp(result.SushiPreservationChance, 0, Math.Clamp(config.SushiPreservationChanceCap, 0, 20));
        if(activity==RpgActivity.Boss)
            result.BossDamageReduction+=RpgSushiService.Defensive(profile,now??DateTimeOffset.UtcNow).Select(b=>b.BossDamageReduction).DefaultIfEmpty().Max();
        result.BossDamageReduction=Math.Clamp(result.BossDamageReduction+RpgPerkCatalog.Bonus(profile,"defense"),-80,80);
        result.BossDodgeChance=Math.Clamp(result.BossDodgeChance,0,100);
        result.CriticalHealing=Math.Clamp(result.CriticalHealing,0,100);
        result.RareEquipmentLuck=Math.Clamp(result.RareEquipmentLuck,0,100);
        result.MaximumHp = Math.Clamp(EquipmentMaximumHp(profile,config.Health.HpPerStamina,config.Sets),0,enhanced?1000000:config.Health.EquipmentHpCap);
        // Permanent talents are applied after equipment caps; probabilities remain bounded.
        result.Damage+=RpgTalentService.Bonus(profile,"damage")+RpgDepthsCombatService.DamageBonus(profile,config,now??DateTimeOffset.UtcNow);
        if(profile.Cooldowns.TryGetValue("perk:otter-rage",out var rageUntil) && (now??DateTimeOffset.UtcNow)<rageUntil)result.Damage+=RpgPerkCatalog.Bonus(profile,"victoryDamage");
        if(activity==RpgActivity.Boss)result.DoubleAttackChance=Math.Min(100,result.DoubleAttackChance+RpgTalentService.Bonus(profile,"doubleAttack"));
        result.CritChance=Math.Min(100,result.CritChance+RpgTalentService.Bonus(profile,"crit"));
        if(activity==RpgActivity.Boss)result.BossDamage+=RpgTalentService.Bonus(profile,"boss");
        if(activity==RpgActivity.Boss && RpgSetCombatService.Active(profile,config.Sets,"abyss_hunter",6) && RpgSetCombatService.Until(profile,"hunter-haste",now??DateTimeOffset.UtcNow)) {result.BossDamage+=18;result.AttackSpeed+=28;}
        if(activity==RpgActivity.Boss && RpgSetCombatService.Active(profile,config.Sets,"coral_oracle",6) && RpgSetCombatService.Until(profile,"coral-damage",now??DateTimeOffset.UtcNow))result.BossDamage+=20;
        result.Damage+=RpgWeaponService.DamageBonus(profile,now??DateTimeOffset.UtcNow);
        RpgEternalCombat.Dynamic(profile,config,result,now??DateTimeOffset.UtcNow);
        return result;
    }

    private static void Add(RpgAttributes a, RpgAttributes b, int count = 1)
    {
        a.Attack+=b.Attack*count;a.MagicPower+=b.MagicPower*count;a.ManaRegeneration+=b.ManaRegeneration*count;a.WaterDamage+=b.WaterDamage*count;a.ElementalDamage+=b.ElementalDamage*count;a.AbyssDamage+=b.AbyssDamage*count;a.LightDamage+=b.LightDamage*count;
        a.EnemyEncounterChance+=b.EnemyEncounterChance*count;a.MaterialFind+=b.MaterialFind*count;a.AttackPercent+=b.AttackPercent*count;a.ExperienceBonus+=b.ExperienceBonus*count;a.DivineDamage+=b.DivineDamage*count;
        a.MovementSpeed+=b.MovementSpeed*count;a.BossLifeleech+=b.BossLifeleech*count;a.Armor+=b.Armor*count;a.ArmorPercent+=b.ArmorPercent*count;a.MaximumHpPercent+=b.MaximumHpPercent*count;
        a.AttackSpeed+=b.AttackSpeed*count;a.HealingPower+=b.HealingPower*count;
        a.BossDamageReduction+=b.BossDamageReduction*count; a.BossDodgeChance+=b.BossDodgeChance*count;
        a.CriticalHealing+=b.CriticalHealing*count; a.RareEquipmentLuck+=b.RareEquipmentLuck*count;
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
        var damage = (baseDamage+Math.Clamp(stats.Attack,0,1000000)) * (1+stats.AttackPercent/100) * (1 + stats.Damage / 100) * (1 + stats.BossDamage / 100);
        if (critical) damage *= Math.Clamp(baseCritMultiplier, 1, 10) + stats.CritDamage / 100;
        return (int)Math.Min(int.MaxValue,decimal.Floor(damage));
    }
}
