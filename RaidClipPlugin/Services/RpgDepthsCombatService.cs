using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;

public static class RpgDepthsCombatService
{
    private const string Prefix="depths:";
    public static int MitigateBoss(int damage,decimal reduction,bool critical,RpgProfile p,RpgConfig c)
    {
        var effective=Math.Clamp(reduction,0,100);
        if(critical&&Has(p,c,"abyss_devourer",6))effective*=.85m;
        return checked((int)decimal.Floor(damage*(1-effective/100)));
    }
    public static bool Has(RpgProfile p,RpgConfig c,string id,int count)=>RpgSetCombatService.Active(p,c.Sets,id,count);
    private static bool Ready(RpgProfile p,string key,DateTimeOffset now)=>!p.Cooldowns.TryGetValue(Prefix+key,out var end)||now>=end;
    private static void Cooldown(RpgProfile p,string key,DateTimeOffset now,int seconds)=>p.Cooldowns[Prefix+key]=now.AddSeconds(seconds);
    public static void Refresh(RpgProfile p,RpgHealthConfig health,DateTimeOffset now)
    {
        foreach(var key in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix)).ToArray())
        {
            if(key.Contains(":used:"))continue;
            var id=key[Prefix.Length..].Split(':')[0];
            if(!RpgSetCombatService.Active(p,health.EquipmentSets,id,id=="abyss_blood_dynasty"?6:4))p.Cooldowns.Remove(key);
        }
        if(p.Health.SetShieldSource=="true_deep_guardian"&&!RpgSetCombatService.Active(p,health.EquipmentSets,"true_deep_guardian",4)){p.Health.SetShieldHp=0;p.Health.SetShieldSource="";}
    }
    public static decimal DamageBonus(RpgProfile p,RpgConfig c,DateTimeOffset now)
    {
        var bonus=Has(p,c,"abyss_blood_dynasty",6)&&!Ready(p,"abyss_blood_dynasty:rage",now)?35m:0;
        if(Has(p,c,"abyss_sunken_world",6))for(int i=1;i<=5;i++)if(!Ready(p,"abyss_sunken_world:stack"+i,now))bonus+=5;
        return bonus;
    }
    public static int AttackProc(int attack,bool critical,int extraHits,RpgProfile p,RpgConfig c,IDuelRandom rng,DateTimeOffset now)
    {
        decimal multiplier=0;
        if(Has(p,c,"true_ancient_current",4)&&critical&&Ready(p,"true_ancient_current:heal",now)){
            RpgSetCombatService.Heal(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.05m),now);Cooldown(p,"true_ancient_current:heal",now,5);}
        if(Has(p,c,"true_ancient_current",6)&&Ready(p,"true_ancient_current:wave",now)){multiplier+=2;Cooldown(p,"true_ancient_current:wave",now,20);}
        if(critical&&Has(p,c,"abyss_devourer",4)&&Ready(p,"abyss_devourer:explosion",now)&&rng.NextInclusive(0,9999)<1200){multiplier+=2.5m;Cooldown(p,"abyss_devourer:explosion",now,8);}
        if(critical&&Has(p,c,"abyss_sunken_world",4)&&Ready(p,"abyss_sunken_world:shadow",now)&&rng.NextInclusive(0,9999)<1500){multiplier+=1.2m;Cooldown(p,"abyss_sunken_world:shadow",now,6);}
        if(extraHits==1&&Has(p,c,"abyss_sunken_world",6)){
            var key=Enumerable.Range(1,5).FirstOrDefault(i=>Ready(p,"abyss_sunken_world:stack"+i,now));
            if(key==0)key=Enumerable.Range(1,5).MinBy(i=>p.Cooldowns[Prefix+"abyss_sunken_world:stack"+i]);
            Cooldown(p,"abyss_sunken_world:stack"+key,now,8);}
        return checked((int)decimal.Floor(attack*multiplier));
    }
    public static void BeforeCounter(RpgProfile p,RpgConfig c,DateTimeOffset now)
    {
        if(!Has(p,c,"true_deep_guardian",4)||!Ready(p,"true_deep_guardian:shield",now))return;
        var amount=(int)(RpgHealthService.Maximum(p,c.Health)*.2m);
        if(amount>p.Health.SetShieldHp){p.Health.SetShieldHp=amount;p.Health.SetShieldSource="true_deep_guardian";}
        Cooldown(p,"true_deep_guardian:shield",now,20);
    }
    public static void AfterCounter(RpgProfile p,RpgConfig c,string bossId,DateTimeOffset now)
    {
        if(p.Health.CurrentHp is null or <=0||p.Health.CurrentHp>=RpgHealthService.Maximum(p,c.Health)*.3m||!Has(p,c,"abyss_blood_dynasty",6))return;
        var key=Prefix+"abyss_blood_dynasty:used:"+bossId;if(p.Cooldowns.ContainsKey(key))return;
        foreach(var stale in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix+"abyss_blood_dynasty:used:")).ToArray())p.Cooldowns.Remove(stale);
        p.Cooldowns[key]=now;Cooldown(p,"abyss_blood_dynasty:rage",now,10);
        RpgSetCombatService.Heal(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.45m),now);
    }
}
