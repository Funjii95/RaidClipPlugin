using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

public sealed record EternalEightProc(int Damage, int SoulDamage);

public static class RpgEternalEightCombat
{
    private const string Cosmic="eternal_cosmic_forge", Soul="eternal_soul_devourer";
    private const string Prefix="eternal8:";
    private static bool Has(RpgProfile p,RpgConfig c,string id,int n)=>RpgSetCombatService.Active(p,c.Sets,id,n);
    private static bool Ready(RpgProfile p,string key,DateTimeOffset now)=>!p.Cooldowns.TryGetValue(Prefix+key,out var end)||end<=now;
    private static void Timer(RpgProfile p,string key,DateTimeOffset now,int seconds)=>p.Cooldowns[Prefix+key]=now.AddSeconds(seconds);
    private static int Stacks(RpgProfile p,string id,DateTimeOffset now,int cap)=>Enumerable.Range(1,cap).Count(n=>!Ready(p,id+":stack:"+n,now));
    private static void ClearStacks(RpgProfile p,string id,int cap){for(var n=1;n<=cap;n++)p.Cooldowns.Remove(Prefix+id+":stack:"+n);}
    private static void Stack(RpgProfile p,string id,DateTimeOffset now,int cap,int seconds)
    {
        var count=Math.Min(cap,Stacks(p,id,now,cap)+1);
        for(var n=1;n<=count;n++)Timer(p,id+":stack:"+n,now,seconds);
    }
    public static void Refresh(RpgProfile p,RpgHealthConfig c)
    {
        foreach(var key in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix)).ToArray()){
            if(key.Contains(":rescue:"))continue;
            var id=key[Prefix.Length..].Split(':')[0];
            var n=key.Contains(":stack:")?5:key.EndsWith(":spark")?3:key.EndsWith(":nova")?7:key.EndsWith(":meteor")?8:key.EndsWith(":rush")?4:key.EndsWith(":fifth")?3:7;
            if(!RpgSetCombatService.Active(p,c.EquipmentSets,id,n))p.Cooldowns.Remove(key);
        }
    }
    public static void Dynamic(RpgProfile p,RpgConfig c,RpgAttributes stats,DateTimeOffset now)
    {
        if(Has(p,c,Cosmic,5))stats.Damage+=4*Stacks(p,Cosmic,now,10);
        if(Has(p,c,Soul,4)&&!Ready(p,Soul+":rush",now))stats.Damage+=30;
        if(Has(p,c,Soul,5))stats.Damage+=6*Stacks(p,Soul,now,8);
    }
    public static void Healed(RpgProfile p,RpgHealthConfig c,int actual,DateTimeOffset now)
    {
        if(actual>0&&RpgSetCombatService.Active(p,c.EquipmentSets,Soul,4))Timer(p,Soul+":rush",now,8);
    }
    // Only direct authoritative attack entry points call this. Returned secondary damage
    // is added once and never enters the proc system again.
    public static EternalEightProc Proc(int attack,bool critical,int extraHits,RpgProfile p,RpgConfig c,DateTimeOffset now)
    {
        decimal factor=0,soul=0;
        if(critical&&Has(p,c,Cosmic,3)&&Ready(p,Cosmic+":spark",now)){factor+=1.8m;Timer(p,Cosmic+":spark",now,5);}
        if(extraHits>0&&Has(p,c,Cosmic,6))factor+=1.5m;
        if(Has(p,c,Cosmic,7)&&Stacks(p,Cosmic,now,10)==10&&Ready(p,Cosmic+":nova",now)){
            factor+=5;ClearStacks(p,Cosmic,10);Timer(p,Cosmic+":nova",now,10);
        }
        if(Has(p,c,Cosmic,8)&&Ready(p,Cosmic+":meteor",now)){factor+=8;Timer(p,Cosmic+":meteor",now,8);}
        if(Has(p,c,Soul,7)&&Ready(p,Soul+":strike",now)){soul=4;Timer(p,Soul+":strike",now,7);}
        return new((int)Math.Min(int.MaxValue,decimal.Floor(attack*(factor+soul))),
            (int)Math.Min(int.MaxValue,decimal.Floor(attack*soul)));
    }
    public static void AfterDirect(RpgProfile p,RpgConfig c,int actualDirect,int actualSoul,bool critical,DateTimeOffset now)
    {
        if(p.Health.CurrentHp is null or <=0)return;
        if(actualDirect>0){
            var lifeleech=Has(p,c,Soul,1)?20m:0;
            if(Has(p,c,Soul,8)&&Stacks(p,Soul,now,8)==8)lifeleech+=20;
            if(lifeleech>0)RpgSetCombatService.Heal(p,c.Health,(int)(actualDirect*lifeleech/100),now);
            if(Has(p,c,Cosmic,5))Stack(p,Cosmic,now,10,8);
            if(critical&&Has(p,c,Soul,5))Stack(p,Soul,now,8,10);
            if(Has(p,c,Soul,3)){
                // A persisted bounded counter; no dependency on wall-clock elapsed time.
                var key=Prefix+Soul+":fifth";
                var count=p.Cooldowns.TryGetValue(key,out var stored)?(int)(stored-DateTimeOffset.UnixEpoch).TotalSeconds:0;
                count=(count+1)%5;p.Cooldowns[key]=DateTimeOffset.UnixEpoch.AddSeconds(count);
                if(count==0)RpgSetCombatService.Heal(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.15m),now);
            }
        }
        if(actualSoul>0)RpgSetCombatService.Heal(p,c.Health,(int)(actualSoul*.15m),now);
    }
    public static int Receive(int damage,RpgProfile p,RpgConfig c,string combatId,bool boss,DateTimeOffset now)
    {
        var hp=p.Health.CurrentHp??0;var max=RpgHealthService.Maximum(p,c.Health);
        if(!boss||hp<=0||damage<=0||hp-damage>=max*.3m||!Has(p,c,Soul,6))return damage;
        var key=Prefix+Soul+":rescue:"+combatId;
        if(p.Cooldowns.ContainsKey(key))return damage;
        foreach(var old in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix+Soul+":rescue:")).ToArray())p.Cooldowns.Remove(old);
        p.Cooldowns[key]=now;
        // Crossing 30% triggers before the remainder of a potentially lethal blow.
        var threshold=Math.Max(1,(int)decimal.Ceiling(max*.3m)-1);
        var before=Math.Max(0,hp-threshold);p.Health.CurrentHp=hp-before;
        RpgSetCombatService.Heal(p,c.Health,(int)(max*.5m),now);
        return Math.Max(0,damage-before);
    }
}
