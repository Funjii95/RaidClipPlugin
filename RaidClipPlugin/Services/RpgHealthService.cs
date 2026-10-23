using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Shared HP rules. Mutating helpers must run on the store's detached transaction draft.</summary>
public static class RpgHealthService
{
    private static int BaseMaximum(RpgProfile p,RpgHealthConfig c) => checked(c.BaseHp + (Math.Clamp(p.PlayerLevel,1,c.MaximumLevel)-1)*c.HpPerLevel);
    public static int Maximum(RpgProfile p, RpgHealthConfig c) => checked((int)decimal.Floor((BaseMaximum(p,c)*(1+RpgPerkCatalog.Combined(p,"health")/100)+Math.Clamp(CharacterStatService.EquipmentMaximumHp(p,c.HpPerStamina,c.EquipmentSets),0,c.EquipmentHpCap))*(1+Math.Clamp(RpgSetCombatService.Gear(p,"MaximumHpPercent")+RpgSetCombatService.Bonus(p,c.EquipmentSets,"MaximumHpPercent"),0,200)/100)));
    public static void Refresh(RpgProfile p, RpgHealthConfig c, DateTimeOffset now)
    {
        c.Validate(); p.Health ??= new();
        var h=p.Health; var max=Maximum(p,c);
        if(h.CurrentHp is null) { h.CurrentHp=max; h.AppliedMaximumHp=max; h.LastPassiveRegen=now; }
        var baseMax=BaseMaximum(p,c);
        var previousBase=h.AppliedBaseMaximumHp??(h.AppliedMaximumHp>0?h.AppliedMaximumHp:baseMax);
        if(c.HealLevelGain && baseMax>previousBase)
            h.CurrentHp=(int)Math.Min(max,(long)h.CurrentHp.Value+baseMax-previousBase);
        h.AppliedBaseMaximumHp=baseMax;
        h.AppliedMaximumHp=max;
        h.CurrentHp=Math.Clamp(h.CurrentHp.Value,0,max);
        h.LastPassiveRegen ??= now;
        if(now<h.LastPassiveRegen) h.LastPassiveRegen=now;
        var intervals=(long)((now-h.LastPassiveRegen.Value).TotalSeconds/c.PassiveSeconds);
        if(intervals>0)
        {
            var credited=c.CatchUpMinutes==0 ? intervals : Math.Min(intervals,(long)c.CatchUpMinutes*60/c.PassiveSeconds);
            var intervalEnd=h.LastPassiveRegen.Value.AddSeconds(intervals*c.PassiveSeconds);
            var creditedStart=intervalEnd.AddSeconds(-Math.Min(credited,max)*c.PassiveSeconds);
            var extra=p.Buffs.Where(b=>b.Category==SushiCategory.Defensive && b.PassiveHealing>0).Sum(b=>
            {
                var start=b.ActivatedAt>creditedStart?b.ActivatedAt:creditedStart;
                var end=b.ExpiresAt is {} expires && expires<intervalEnd?expires:intervalEnd;
                if(b.ExpiresAt is null && b.RemainingUses<=0)return 0m;
                return Math.Max(0,(decimal)(end-start).TotalSeconds)*Math.Clamp(b.PassiveHealing,0,1000)/60;
            });
            if(c.PassiveHealing)
            {
                var accrued=extra+Math.Clamp(h.SushiRegenRemainder,0,0.9999999999999999999999999999m);
                RpgSetCombatService.Heal(p,c,(int)Math.Min(int.MaxValue,Math.Min(credited,max)*(c.PassiveHp+RpgPerkCatalog.Bonus(p,"regen"))+decimal.Floor(accrued)),now);
                h.SushiRegenRemainder=h.CurrentHp>=max?0:accrued-decimal.Floor(accrued);
            }
            h.LastPassiveRegen=h.LastPassiveRegen.Value.AddSeconds(intervals*c.PassiveSeconds);
        }
        RpgSetCombatService.Refresh(p,c,now);
    }
    public static void Experience(RpgProfile p, RpgHealthConfig c, int amount, DateTimeOffset now)
    {
        Refresh(p,c,now);
        p.Experience=checked(p.Experience+Math.Max(0,amount));
        while(p.PlayerLevel<c.MaximumLevel && p.Experience >= (long)c.ExperiencePerLevel*p.PlayerLevel*(p.PlayerLevel+1)/2)
            p.PlayerLevel++;
        Refresh(p,c,now);
    }
    public static bool Eligible(RpgProfile p,string bossId) => p.Health.CurrentHp>0 && p.Health.BossId==bossId && !p.Health.Defeated;
    // Integration point for a future authoritative underwater combat resolver. No currency or item mutation.
    internal static void ResolveEnemy(RpgProfile p,RpgHealthConfig c,int damage,bool victory,int experience,DateTimeOffset now)
    {
        Refresh(p,c,now);
        if(p.Health.CurrentHp<=0) throw new InvalidOperationException("Vor einem neuen Gegnerkampf erst heilen.");
        if(damage<0 || experience<0) throw new ArgumentOutOfRangeException();
        p.Health.CurrentHp=Math.Max(0,p.Health.CurrentHp!.Value-damage);
        if(p.Health.CurrentHp==0 && !string.IsNullOrEmpty(p.Health.BossId))p.Health.Defeated=true;
        if(victory && p.Health.CurrentHp>0) Experience(p,c,experience,now);
    }
    public static int CounterDamage(int initialBossHp,RpgHealthConfig c,IDuelRandom random)
    {
        var range=initialBossHp>=c.LegendaryThreshold ? (c.LegendaryMinimum,c.LegendaryMaximum) :
            initialBossHp>=c.EliteThreshold ? (c.EliteMinimum,c.EliteMaximum) :
            initialBossHp>=c.StrongThreshold ? (c.StrongMinimum,c.StrongMaximum) : (c.CommonMinimum,c.CommonMaximum);
        return c.Counterattacks ? random.NextInclusive(range.Item1,range.Item2) : 0;
    }
    public static string Status(RpgProfile p,RpgHealthConfig c) =>
        $"❤️ HP: {p.Health.CurrentHp}/{Maximum(p,c)} | Level {p.PlayerLevel} ({p.Experience} EXP) | " +
        $"Passiv: +{(c.PassiveHealing?c.PassiveHp+RpgPerkCatalog.Bonus(p,"regen")+RpgSushiService.PassiveBonus(p,DateTimeOffset.UtcNow)*c.PassiveSeconds/60:0)} HP/{c.PassiveSeconds}s | Chat: +{(c.ChatHealing?c.ChatHp+RpgSushiService.ChatBonus(p,DateTimeOffset.UtcNow):0)} HP/{c.ChatCooldownSeconds}s." +
        (p.Health.Defeated ? " Für den zuletzt angegriffenen Boss besiegt; Heilung hebt diese Sperre nicht auf." : "");
    public static async Task HealChatAsync(ViewerPointStore points,RpgConfig config,ChatMessage message,DateTimeOffset now,CancellationToken token)
    {
        if(!config.Enabled || !config.Health.Enabled || message.IsBot || string.IsNullOrWhiteSpace(message.UserId) ||
            string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.Text) || message.Text.TrimStart().StartsWith('!')) return;
        await points.ApplyRpgAsync(message.UserId,message.UserName,"hpchat:"+message.Id,a=>
        {
            Refresh(a.Rpg,config.Health,now);
            var h=a.Rpg.Health;
            if(config.Health.ChatHealing && (h.LastChatHeal is null || (now-h.LastChatHeal.Value).TotalSeconds>=config.Health.ChatCooldownSeconds))
            {
                RpgSetCombatService.Heal(a.Rpg,config.Health,config.Health.ChatHp+RpgSushiService.ChatBonus(a.Rpg,now),now);
                h.LastChatHeal=now;
            }
            return new(true);
        },token);
    }
    public static async Task<RpgProfile> ReadAsync(ViewerPointStore points,string id,string name,RpgHealthConfig config,DateTimeOffset now,CancellationToken token)
    {
        await points.ApplyRpgAsync(id,name,"hpview:"+Guid.NewGuid().ToString("N"),a=>{Refresh(a.Rpg,config,now);return new(true);},token);
        return (await points.GetProfileAsync(id,token)).Entry.Rpg;
    }
}
