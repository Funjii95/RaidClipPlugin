using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;

public static class RpgTalentService
{
    public static decimal Bonus(RpgProfile p,string effect)=>TalentRules.Bonus(p.TalentRanks,effect);
    public static object View(RpgProfile p)=>new {catalogVersion=TalentRules.CatalogVersion,ranks=p.TalentRanks,
        earned=TalentRules.Earned(p.PlayerLevel),spent=TalentRules.Spent(p.TalentRanks),available=TalentRules.Available(p.PlayerLevel,p.TalentRanks)};
    public static Task<RpgTransactionResult> LearnAsync(ViewerPointStore points,RpgConfig config,string userId,string name,string requestId,
        string talentId,int expectedRank,int catalogVersion,CancellationToken ct)=>points.ApplyRpgAsync(userId,name,requestId,account=>
    {
        if(!config.Enabled)return new(false,"RPG ist deaktiviert.");
        var p=account.Rpg;
        var rejection=TalentRules.Rejection(talentId,p.PlayerLevel,p.TalentRanks,expectedRank,catalogVersion);
        if(rejection is not null)return new(false,rejection);
        p.TalentRanks[talentId]=expectedRank+1;
        if(config.Health.Enabled)RpgHealthService.Refresh(p,config.Health,DateTimeOffset.UtcNow);
        return new(true);
    },ct);
    public static double Cooldown(RpgProfile p,int seconds,string effect)=>Math.Max(1,Math.Clamp(seconds,1,86400)*(1-(double)Bonus(p,effect)/100));
    public static bool Proc(RpgProfile p,string effect,IDuelRandom random)
    {
        var chance=Bonus(p,effect);
        return chance>0 && random.NextInclusive(0,9999)<Math.Clamp(chance,0,100)*100;
    }
    // This helper is called only from RpgEnemyService, never the boss resolver.
    public static int UnderwaterDamage(int damage,RpgProfile p)=>checked((int)decimal.Floor(damage*(1+Bonus(p,"underwaterDamage")/100)));
    public static int Heal(RpgProfile p,RpgHealthConfig config,int actualDamage)
    {
        if(p.Health.CurrentHp is null or <=0 || actualDamage<=0)return 0;
        var amount=(int)Math.Min(Math.Max(0,RpgHealthService.Maximum(p,config)-p.Health.CurrentHp.Value),decimal.Floor(actualDamage*Bonus(p,"lifeleech")/100));
        p.Health.CurrentHp+=amount;return amount;
    }
}
