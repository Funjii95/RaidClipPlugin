using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgBossHit(int Damage, bool Critical, int ExtraHits){public int DirectDamage{get;init;}}

public static class RpgPassiveService
{
    private sealed record Candidate(string Key, RpgItemEffect Effect);
    // Effect snapshots belong to the instance. Editing a definition never rerolls existing gear.
    public static decimal TryProc(RpgProfile profile, RpgConfig config, RpgActivity activity, RpgEffectTrigger trigger,
        IDuelRandom random, DateTimeOffset now, long stake = 0, bool critical = false)
    {
        if ((trigger is RpgEffectTrigger.DoubleAttack or RpgEffectTrigger.TripleAttack) && activity != RpgActivity.Boss ||
            trigger == RpgEffectTrigger.ExtraLoot && activity != RpgActivity.Dive) return 0;
        var stats = CharacterStatService.Calculate(profile, config, activity, includeEffects: false,now:now);
        var (chance, cap) = trigger switch
        {
            RpgEffectTrigger.DoubleAttack => (stats.DoubleAttackChance, Math.Min(100,(RpgEternalCombat.Enhanced(profile)?100:Math.Clamp(config.DoubleAttackChanceCap, 0, 20))+RpgTalentService.Bonus(profile,"doubleAttack"))),
            RpgEffectTrigger.TripleAttack => (stats.TripleAttackChance, Math.Clamp(config.TripleAttackChanceCap, 0, 5)),
            RpgEffectTrigger.ExtraLoot => (stats.ExtraLootChance, Math.Min(100,Math.Clamp(config.ExtraLootChanceCap, 0, 10)+RpgPerkCatalog.Bonus(profile,"extraLoot"))),
            _ => (stats.SushiPreservationChance, Math.Clamp(config.SushiPreservationChanceCap, 0, 20))
        };
        var candidates = new List<Candidate>();
        if (chance > 0) candidates.Add(new("", new() { Trigger = trigger, Chance = chance, Strength = 0.5m }));
        foreach (var item in profile.Inventory.Where(i => profile.Equipment.Values.Contains(i.InstanceId)).DistinctBy(i => i.InstanceId))
        foreach (var effect in item.Effects.Where(e => e.Trigger == trigger && e.Chance > 0 &&
            (!e.RequiresCritical || critical) && (!e.Activity.HasValue || e.Activity == activity) && stake >= e.MinimumStake))
        {
            var key = "effect:" + item.InstanceId + ":" + effect.Id;
            if (!profile.Cooldowns.TryGetValue(key, out var until) || now >= until) candidates.Add(new(key, effect));
        }
        var total = candidates.Sum(c => Math.Clamp(c.Effect.Chance, 0, 100));
        if (total <= 0 || random.NextInclusive(0, 9999) >= Math.Min(total, cap) * 100) return 0;
        // Select one contributing effect proportionally; never trigger effects recursively.
        var roll = random.NextInclusive(1, Math.Max(1, (int)decimal.Ceiling(total * 100)));
        var selected = candidates[^1];
        foreach (var candidate in candidates)
        { selected = candidate; roll -= (int)decimal.Ceiling(candidate.Effect.Chance * 100); if (roll <= 0) break; }
        if (selected.Key.Length > 0 && selected.Effect.CooldownSeconds > 0)
            profile.Cooldowns[selected.Key] = now.AddSeconds(Math.Clamp(selected.Effect.CooldownSeconds, 0, 86400));
        return Math.Clamp(selected.Effect.Strength, 0.01m, 1);
    }

    public static RpgBossHit BossAttack(int baseDamage, RpgProfile profile, RpgConfig config, IDuelRandom random, DateTimeOffset now)
    {
        var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss,now:now);
        var regular = CharacterStatService.BossDamage(baseDamage, stats, random.NextInclusive(0, 9999) / 100m, out var critical, config.BaseCritMultiplier);
        var strength = TryProc(profile, config, RpgActivity.Boss, RpgEffectTrigger.TripleAttack, random, now, critical: critical);
        var hits = strength > 0 ? 2 : 0;
        if (hits == 0) { strength = TryProc(profile, config, RpgActivity.Boss, RpgEffectTrigger.DoubleAttack, random, now, critical: critical); if (strength > 0) hits = 1; }
        var attack=checked((int)decimal.Floor(baseDamage*(1+stats.AttackPercent/100)*(1+stats.Damage/100)*(1+stats.BossDamage/100)));
        var direct=checked(regular+hits*(int)decimal.Floor(regular*strength));
        return new(checked(direct+RpgSetCombatService.ProcDamage(attack,critical,hits,profile,config,now)+RpgDepthsCombatService.AttackProc(attack,critical,hits,profile,config,random,now)+RpgEternalCombat.Proc(attack,critical,hits,profile,config,now)),critical,hits){DirectDamage=direct};
    }

    public static long RewardPayout(long payout, long stake, decimal bonus)
    {
        var reward = Math.Max(0, payout - stake);
        return checked(payout + (long)decimal.Floor(reward * Math.Clamp(bonus, 0, 15) / 100));
    }

    public static void ConsumeBuffs(RpgProfile profile, RpgActivity activity, RpgConfig config, IDuelRandom random, DateTimeOffset now, long stake = 0)
    {
        foreach (var buff in profile.Buffs.Where(b => !b.EncounterBased && b.ExpiresAt is null && b.Activity == activity && b.RemainingUses > 0).ToArray())
        {
            // At most one consecutive save per buff: finite life even with adversarial RNG.
            var key = "sushi-save:" + buff.RecipeId;
            var previousSaved = profile.Cooldowns.ContainsKey(key);
            if (!previousSaved && TryProc(profile, config, activity, RpgEffectTrigger.SushiPreservation, random, now, stake) > 0)
                profile.Cooldowns[key] = now;
            else { buff.RemainingUses--; profile.Cooldowns.Remove(key); }
        }
        profile.Buffs.RemoveAll(b => b.ExpiresAt is {} end?now>=end:!b.EncounterBased && b.RemainingUses <= 0);
    }

    public static string Describe(RpgItemEffect effect) =>
        $"{effect.Name}: {RpgChatPresentation.Number(effect.Chance)} % Chance, " + (effect.Trigger switch
        {
            RpgEffectTrigger.DoubleAttack => $"bei einem Bossangriff 1 Zusatztreffer mit {RpgChatPresentation.Number(effect.Strength * 100)} % des regulären Treffers auszulösen",
            RpgEffectTrigger.TripleAttack => $"bei einem Bossangriff 2 Zusatztreffer mit je {RpgChatPresentation.Number(effect.Strength * 100)} % Schaden auszulösen (Vorrang vor Doppelangriff)",
            RpgEffectTrigger.ExtraLoot => "beim Tauchen einen zusätzlichen Gegenstand auszuwürfeln (kann leer ausgehen; Einsatzanforderungen gelten weiterhin, keine zusätzlichen Perlen)",
            _ => "beim Verbrauch einer Sushi-Aufladung diese zu behalten (maximal einmal hintereinander je Buff)"
        }) + (effect.RequiresCritical ? "; nur bei kritischem Treffer" : "") + (effect.Activity.HasValue ? "; nur " + RpgChatPresentation.Activity(effect.Activity.Value) : "") +
        (effect.MinimumStake > 0 ? "; Mindesteinsatz " + RpgChatPresentation.Number(effect.MinimumStake) : "") +
        (effect.CooldownSeconds > 0 ? "; Cooldown " + effect.CooldownSeconds + " s" : "");
}
