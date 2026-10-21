using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgEnemyResult(string Name,bool Victory,int Rounds,int DamageTaken,int CurrentHp,int MaximumHp,int Experience,RpgItem? Loot) { public RpgItem? ExtraLoot {get;init;} }

/// <summary>Runs only on the detached draft of the existing dive transaction.</summary>
public sealed class RpgEnemyService(IDuelRandom random)
{
    public RpgEnemyResult? Encounter(RpgProfile profile,RpgConfig config,long paidStake,DateTimeOffset now,decimal hotTimePercent=0)
    {
        if(!config.Enemies.Enabled || !config.Health.Enabled) return null;
        config.Enemies.Validate();
        if(paidStake<0) throw new ArgumentOutOfRangeException(nameof(paidStake));
        RpgHealthService.Refresh(profile,config.Health,now);
        if(profile.Health.CurrentHp<=0) throw new InvalidOperationException("Vor dem Tauchen erst heilen.");
        if(random.NextInclusive(1,10000)>config.Enemies.EncounterChance*100) return null;
        var enemy=config.Enemies.Tiers.Where(t=>t.MinimumStake<=paidStake).MaxBy(t=>t.MinimumStake)!;
        var hp=enemy.HitPoints;var taken=0;var rounds=0;
        var initialHp=profile.Health.CurrentHp!.Value;var remainingHp=initialHp;
        // Dive buffs apply, boss-only sushi is neither used nor consumed here.
        var stats=CharacterStatService.Calculate(profile,config,RpgActivity.Dive,now:now,stake:paidStake);
        while(hp>0 && remainingHp>0 && rounds<config.Enemies.MaximumRounds)
        {
            rounds++;
            var hit=CharacterStatService.BossDamage(random.NextInclusive(10,30),stats,random.NextInclusive(0,9999)/100m,out _,config.BaseCritMultiplier);
            hit=RpgTalentService.UnderwaterDamage(hit,profile);
            var actual=Math.Min(hp,Math.Max(1,hit));
            hp=Math.Max(0,hp-actual);
            // Healing is based on real damage and never revives a defeated character.
            remainingHp+=(int)Math.Min(RpgHealthService.Maximum(profile,config.Health)-remainingHp,decimal.Floor(actual*RpgTalentService.Bonus(profile,"lifeleech")/100));
            if(hp>0 && !RpgTalentService.Proc(profile,"underwaterDodge",random)){var received=Math.Min(remainingHp,random.NextInclusive(enemy.MinimumDamage,enemy.MaximumDamage));taken=checked(taken+received);remainingHp-=received;}
        }
        profile.Health.CurrentHp=Math.Max(initialHp,remainingHp);
        var victory=hp==0;
        RpgHealthService.ResolveEnemy(profile,config.Health,Math.Max(0,initialHp-remainingHp),victory,enemy.Experience,now);
        RpgItem? loot=null,extraLoot=null;
        if(victory)
        {
            var sources=CharacterStatService.DiveLuckSources(profile,config);
            loot=new RpgLootService(random).RollEnemy(paidStake,sources.Perks,config.DiveLoot,now,sources.Sushi,sources.Equipment,hotTimePercent,RpgCollectorService.Calculate(profile,config.DiveLoot),stats.RareEquipmentLuck,RpgTalentService.Bonus(profile,"loot")).Item;
            if(RpgTalentService.Proc(profile,"enemyLoot",random))extraLoot=new RpgLootService(random).RollEnemy(paidStake,sources.Perks,config.DiveLoot,now,sources.Sushi,sources.Equipment,hotTimePercent,RpgCollectorService.Calculate(profile,config.DiveLoot),stats.RareEquipmentLuck,RpgTalentService.Bonus(profile,"loot")).Item;
        }
        return new(enemy.Name,victory,rounds,taken,profile.Health.CurrentHp!.Value,RpgHealthService.Maximum(profile,config.Health),victory?enemy.Experience:0,loot){ExtraLoot=extraLoot};
    }
}
