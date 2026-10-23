using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgBossDefenseService
{
    public static int Counter(int raw,RpgProfile profile,RpgConfig config,IDuelRandom random,DateTimeOffset now,decimal armorIgnore=0,decimal reductionIgnore=0)
    {
        if(raw<=0)return 0;
        var stats=CharacterStatService.Calculate(profile,config,RpgActivity.Boss,now:now);
        RpgSetCombatService.Refresh(profile,config.Health,now);
        var dodged=stats.BossDodgeChance>0 && random.NextInclusive(0,9999)<stats.BossDodgeChance*100;
        var armor=Math.Clamp(stats.Armor*(1+stats.ArmorPercent/100),0,10000)*(1-Math.Clamp(armorIgnore,0,100)/100);
        if(!dodged)RpgDepthsCombatService.BeforeCounter(profile,config,now);
        var reduced=dodged?0:(int)decimal.Ceiling(raw*(1-stats.BossDamageReduction*(1-Math.Clamp(reductionIgnore,0,100)/100)/100)*100/(100+armor));
        return RpgSetCombatService.Defense(reduced,profile,config,now,dodged);
    }
}
