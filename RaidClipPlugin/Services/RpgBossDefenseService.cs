using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgBossDefenseService
{
    public static int Counter(int raw,RpgProfile profile,RpgConfig config,IDuelRandom random,DateTimeOffset now)
    {
        if(raw<=0)return 0;
        var stats=CharacterStatService.Calculate(profile,config,RpgActivity.Boss,now:now);
        if(stats.BossDodgeChance>0 && random.NextInclusive(0,9999)<stats.BossDodgeChance*100)return 0;
        return (int)decimal.Ceiling(raw*(1-stats.BossDamageReduction/100));
    }
}
