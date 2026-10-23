using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Called only on authoritative transaction drafts. Timers survive restart; procs never recurse.</summary>
public static class RpgSetCombatService
{
    public static bool Active(RpgProfile p,IReadOnlyList<RpgItemSet> sets,string id,int pieces) => sets.Any(s=>s.Id==id &&
        p.Inventory.Where(i=>i.SetId==id && s.ItemIds.Contains(i.ItemId) && p.Equipment.Values.Contains(i.InstanceId)).Select(i=>i.ItemId).Distinct().Count()>=pieces);
    public static decimal Bonus(RpgProfile p,IReadOnlyList<RpgItemSet> sets,string attribute) => sets.Sum(s=>s.Bonuses.Where(b=>Active(p,sets,s.Id,b.RequiredPieces)).Sum(b=>(decimal)typeof(RpgAttributes).GetProperty(attribute)!.GetValue(b.Attributes)!));
    public static decimal Gear(RpgProfile p,string attribute) => p.Inventory.Where(i=>p.Equipment.Values.Contains(i.InstanceId)).DistinctBy(i=>i.InstanceId).Sum(i=>(decimal)typeof(RpgAttributes).GetProperty(attribute)!.GetValue(RpgEquipmentUpgradeRules.Effective(i))!);
    public static bool Until(RpgProfile p,string key,DateTimeOffset now)=>p.Cooldowns.TryGetValue("set:"+key,out var end)&&now<end;
    private static bool Ready(RpgProfile p,string key,int seconds,DateTimeOffset now)
    {
        if(Until(p,key,now))return false;
        p.Cooldowns["set:"+key]=now.AddSeconds(seconds);return true;
    }
    public static void Refresh(RpgProfile p,RpgHealthConfig c,DateTimeOffset now)
    {
        RpgDepthsCombatService.Refresh(p,c,now);
        RpgEternalCombat.Refresh(p,c,now);
        var sets=c.EquipmentSets;
        bool Has(string id,int n)=>Active(p,sets,id,n);
        foreach(var key in p.Cooldowns.Keys.Where(k=>k.StartsWith("set:")).ToArray())
        {
            var valid=key.Contains("tide")?Has("tide_king_regalia",key.Contains("wave")?4:6):
                key.Contains("hunter")?Has("abyss_hunter",key.Contains("fin")?4:6):
                key.Contains("moon")?Has("moon_tide_jewelry",2):Has("coral_oracle",key.Contains("heal")?4:6);
            if(!valid)p.Cooldowns.Remove(key);
        }
        if(p.Health.SetShieldSource=="coral"&&!Has("coral_oracle",6)||p.Health.SetShieldSource=="moon"&&!Has("moon_tide_jewelry",2))
        {p.Health.SetShieldHp=0;p.Health.SetShieldSource="";}
        if(p.Health.CurrentHp is null or <=0)return;
        var max=RpgHealthService.Maximum(p,c);
        p.Health.SetShieldHp=Math.Clamp(p.Health.SetShieldHp,0,(int)(max*(p.Health.SetShieldSource=="true_deep_guardian"?.2m:.18m)));
        if(Has("tide_king_regalia",6)&&p.Cooldowns.ContainsKey("set:tide-regen"))
        {
            var end=p.Cooldowns["set:tide-regen"];
            var last=p.Cooldowns.GetValueOrDefault("set:tide-tick",end.AddSeconds(-8));
            var credited=now<end?now:end;
            var ticks=(int)Math.Clamp((credited-last).TotalSeconds,0,8);
            if(ticks>0){Heal(p,c,(int)(max*0.03m*ticks),now);p.Cooldowns["set:tide-tick"]=last.AddSeconds(ticks);}
        }
        if(Has("coral_oracle",4))
        {
            if(!p.Cooldowns.ContainsKey("set:coral-heal"))p.Cooldowns["set:coral-heal"]=now.AddSeconds(10);
            else if(p.Health.CurrentHp<max&&Ready(p,"coral-heal",10,now))Heal(p,c,(int)(max*0.22m),now);
        }
        if(Has("moon_tide_jewelry",2))
        {
            if(!p.Cooldowns.ContainsKey("set:moon-shield"))p.Cooldowns["set:moon-shield"]=now.AddSeconds(15);
            else if(Ready(p,"moon-shield",15,now))Shield(p,(int)(max*0.12m),"moon",now);
        }
    }
    public static int Heal(RpgProfile p,RpgHealthConfig c,int raw,DateTimeOffset now)
    {
        if(p.Health.CurrentHp is null or <0||raw<=0)return 0;
        var max=RpgHealthService.Maximum(p,c);
        var power=Math.Clamp(Gear(p,"HealingPower")+Bonus(p,c.EquipmentSets,"HealingPower"),-90,200);
        var amount=(int)Math.Min(int.MaxValue,decimal.Floor(raw*(1+power/100)));
        var missing=Math.Max(0,max-p.Health.CurrentHp.Value);var granted=Math.Min(missing,amount);
        p.Health.CurrentHp+=granted;
        RpgEternalCombat.Healed(p,c,granted,now);
        if(amount>missing&&Active(p,c.EquipmentSets,"coral_oracle",6))Shield(p,Math.Min(amount-missing,(int)(max*0.18m)),"coral",now);
        return granted;
    }
    private static void Shield(RpgProfile p,int amount,string source,DateTimeOffset now)
    {
        if(amount<=0)return;
        var created=p.Health.SetShieldHp==0;
        if(amount>=p.Health.SetShieldHp){p.Health.SetShieldHp=amount;p.Health.SetShieldSource=source;}
        if(created&&source=="coral")p.Cooldowns["set:coral-damage"]=now.AddSeconds(8);
    }
    public static int Defense(int damage,RpgProfile p,RpgConfig c,DateTimeOffset now,bool dodged)
    {
        if(dodged&&Active(p,c.Sets,"abyss_hunter",6))p.Cooldowns["set:hunter-haste"]=now.AddSeconds(8);
        var absorbed=Math.Min(p.Health.SetShieldHp,damage);p.Health.SetShieldHp-=absorbed;
        if(damage>0&&Active(p,c.Sets,"tide_king_regalia",6))
        {p.Cooldowns["set:tide-regen"]=now.AddSeconds(8);p.Cooldowns["set:tide-tick"]=now;}
        return damage-absorbed;
    }
    public static int ProcDamage(int regular,bool critical,int extraHits,RpgProfile p,RpgConfig c,DateTimeOffset now)
    {
        var bonus=0m;
        if(critical&&Active(p,c.Sets,"tide_king_regalia",4)&&Ready(p,"tide-wave",8,now))bonus+=1.6m;
        if(extraHits==1&&Active(p,c.Sets,"abyss_hunter",4)&&Ready(p,"hunter-fin",6,now))bonus+=1.7m;
        return checked((int)decimal.Floor(regular*bonus));
    }
    public static double AttackSeconds(RpgProfile p,RpgConfig c,double seconds,DateTimeOffset now)
    {
        var haste=Gear(p,"AttackSpeed")+Bonus(p,c.Sets,"AttackSpeed");
        if(Active(p,c.Sets,"abyss_hunter",6)&&Until(p,"hunter-haste",now))haste+=28;
        var dynamic=new RpgAttributes();RpgEternalCombat.Dynamic(p,c,dynamic,now);haste+=dynamic.AttackSpeed;
        return Math.Max(1,seconds/(1+(double)Math.Clamp(haste,-90,200)/100));
    }
}
