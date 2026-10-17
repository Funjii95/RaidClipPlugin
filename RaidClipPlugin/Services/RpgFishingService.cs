using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgFishingService
{
    public static IReadOnlyList<(RpgFishDefinition Fish,int Weight)> Weights(RpgConfig config,RpgAttributes stats,long stake) => config.FishSpecies
        .Where(f=>f.MinimumStake<=stake && f.Weight>0 && f.Weight<=1000000)
        .Select(f=>(f,checked((int)Math.Min(1000000,f.Weight*(f.Rarity>=ItemRarity.Rare?1+Math.Min(100,stats.Luck+stats.FishingLuck)/100:1))))).ToArray();
    public static RpgFishDefinition Select(RpgConfig config,RpgAttributes stats,long stake,IDuelRandom random)
    {
        var pool=Weights(config,stats,stake);
        if(pool.Count==0)throw new InvalidOperationException("Kein fangbarer Fisch in dieser Einsatzstufe konfiguriert.");
        var roll=random.NextInclusive(1,pool.Sum(p=>p.Weight));
        foreach(var p in pool){roll-=p.Weight;if(roll<=0)return p.Fish;}
        return pool[^1].Fish;
    }
}
