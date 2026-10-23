using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static class RpgEternalCombat
{
    private const string Prefix="eternal:";
    public static bool Has(RpgProfile p,IReadOnlyList<RpgItemSet> sets,string id,int n)=>RpgSetCombatService.Active(p,sets,id,n);
    public static bool Has(RpgProfile p,RpgConfig c,string id,int n)=>Has(p,c.Sets,id,n);
    public static bool Enhanced(RpgProfile p)=>p.Inventory.Any(i=>p.Equipment.Values.Contains(i.InstanceId)&&i.Rarity>=ItemRarity.Eternal);
    public static bool Judgment(RpgProfile p,RpgConfig c)=>Has(p,c,"divine_infinity",6);
    private static bool Ready(RpgProfile p,string key,DateTimeOffset now)=>!p.Cooldowns.TryGetValue(Prefix+key,out var end)||end<=now;
    private static void Set(RpgProfile p,string key,DateTimeOffset now,int seconds)=>p.Cooldowns[Prefix+key]=now.AddSeconds(seconds);
    private static int Stacks(RpgProfile p,string id,DateTimeOffset now,int maximum)=>Enumerable.Range(1,maximum).Count(i=>!Ready(p,id+":stack"+i,now));
    private static void Stack(RpgProfile p,string id,DateTimeOffset now,int maximum,int seconds,bool renewAll)
    {
        int next=Math.Min(maximum,Stacks(p,id,now,maximum)+1);
        if(renewAll)for(int i=1;i<=next;i++)Set(p,id+":stack"+i,now,seconds);
        else {var slot=Enumerable.Range(1,maximum).FirstOrDefault(i=>Ready(p,id+":stack"+i,now));if(slot==0)slot=Enumerable.Range(1,maximum).MinBy(i=>p.Cooldowns[Prefix+id+":stack"+i]);Set(p,id+":stack"+slot,now,seconds);}
    }
    public static void Healed(RpgProfile p,RpgHealthConfig c,int actual,DateTimeOffset now)
    {if(actual>0&&Has(p,c.EquipmentSets,"eternal_tide_blessing",5))Stack(p,"eternal_tide_blessing",now,5,10,true);}
    public static void Refresh(RpgProfile p,RpgHealthConfig c,DateTimeOffset now)
    {
        foreach(var key in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix)&&!k.Contains(":used:")).ToArray()) {
            var id=key[Prefix.Length..].Split(':')[0];
            var required=key.Contains(":stack")?(id=="eternal_tide_blessing"?5:5):key.EndsWith(":regen5")?1:key.EndsWith(":crit")?3:key.EndsWith(":low")?4:6;
            if(!Has(p,c.EquipmentSets,id,required))p.Cooldowns.Remove(key);
        }
        if(p.Health.CurrentHp is null or <=0)return;
        foreach(var (n,seconds,percent) in new[]{(1,5,.05m),(6,8,.20m)}) {
            if(!Has(p,c.EquipmentSets,"eternal_tide_blessing",n))continue;
            var key="eternal_tide_blessing:regen"+seconds;
            if(!p.Cooldowns.ContainsKey(Prefix+key)){Set(p,key,now,seconds);continue;}
            if(Ready(p,key,now)){RpgSetCombatService.Heal(p,c,(int)(RpgHealthService.Maximum(p,c)*percent),now);Set(p,key,now,seconds);}
        }
    }
    public static void Dynamic(RpgProfile p,RpgConfig c,RpgAttributes a,DateTimeOffset now)
    {
        if(Has(p,c,"eternal_tide_blessing",5))a.Damage+=5*Stacks(p,"eternal_tide_blessing",now,5);
        if(Has(p,c,"eternal_tide_blessing",6)&&!Ready(p,"eternal_tide_blessing:rescue",now))a.BossDamage+=50;
        if(Has(p,c,"eternal_primordial_wrath",5)){var stacks=Stacks(p,"eternal_primordial_wrath",now,10);a.Damage+=8*stacks;if(stacks==10)a.AttackSpeed+=30;}
    }
    public static int Proc(int attack,bool critical,int extraHits,RpgProfile p,RpgConfig c,DateTimeOffset now)
    {
        decimal factor=0;
        if(critical&&Has(p,c,"eternal_tide_blessing",3)&&Ready(p,"eternal_tide_blessing:crit",now)){RpgSetCombatService.Heal(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.08m),now);Set(p,"eternal_tide_blessing:crit",now,3);}
        if(extraHits>0&&Has(p,c,"eternal_primordial_wrath",4))factor+=1.5m;
        if(critical&&Has(p,c,"eternal_primordial_wrath",5))Stack(p,"eternal_primordial_wrath",now,10,10,false);
        if(Has(p,c,"eternal_primordial_wrath",6)&&Ready(p,"eternal_primordial_wrath:tear",now)){factor+=4.5m;Set(p,"eternal_primordial_wrath:tear",now,6);}
        return (int)Math.Min(int.MaxValue,decimal.Floor(attack*factor));
    }
    public static void AfterHit(RpgProfile p,RpgConfig c,int actualDirect,DateTimeOffset now)
    {
        if(actualDirect<=0)return;
        if(Has(p,c,"divine_infinity",5)) {var delta=RpgHealthService.Maximum(p,c.Health)-(p.Health.CurrentHp??0);p.Health.CurrentHp=RpgHealthService.Maximum(p,c.Health);Healed(p,c.Health,delta,now);}
    }
    public static int Receive(int damage,RpgProfile p,RpgConfig c,string combatId,DateTimeOffset now)
    {
        if(Has(p,c,"divine_infinity",4))return 0;
        var hp=p.Health.CurrentHp??0;var max=RpgHealthService.Maximum(p,c.Health);
        if(damage>=hp&&Has(p,c,"eternal_tide_blessing",6)&&!p.Cooldowns.ContainsKey(Prefix+"eternal_tide_blessing:used:"+combatId)) {
            foreach(var old in p.Cooldowns.Keys.Where(k=>k.StartsWith(Prefix+"eternal_tide_blessing:used:")).ToArray())p.Cooldowns.Remove(old);
            p.Cooldowns[Prefix+"eternal_tide_blessing:used:"+combatId]=now;
            p.Health.CurrentHp=Math.Max(1,max/2);Healed(p,c.Health,Math.Max(0,max/2-hp),now);Set(p,"eternal_tide_blessing:rescue",now,10);return 0;
        }
        if(hp-damage>0&&hp-damage<max*.4m&&Has(p,c,"eternal_tide_blessing",4)&&Ready(p,"eternal_tide_blessing:low",now)) {
            p.Health.CurrentHp=hp-damage;RpgSetCombatService.Heal(p,c.Health,(int)(max*.35m),now);Set(p,"eternal_tide_blessing:low",now,20);return 0;
        }
        return damage;
    }
}
