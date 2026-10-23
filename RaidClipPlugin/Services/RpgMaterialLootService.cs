using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
/// <summary>Only call inside an existing idempotent reward transaction.</summary>
public static class RpgMaterialLootService
{
    public static IReadOnlyDictionary<string,int> Award(RpgProfile profile,RpgUpgradeConfig config,string source,IDuelRandom random,decimal hotTime=0)
    {
        config.Validate();var awards=new Dictionary<string,int>();
        if(source=="mimic"){
            if(random.NextInclusive(0,9999)>=5000)return awards;
            var pick=random.NextInclusive(1,config.MimicWeights.Values.Sum());
            foreach(var pair in config.MimicWeights){pick-=pair.Value;if(pick>0)continue;awards[pair.Key]=1;break;}}
        else foreach(var pair in config.Drops[source]){
            var chance=Math.Clamp(pair.Value.Chance*(1+Math.Clamp(hotTime,0,200)/100),0,100);
            if(chance>0&&random.NextInclusive(0,999999)<chance*10000)awards[pair.Key]=random.NextInclusive(pair.Value.Minimum,pair.Value.Maximum);}
        foreach(var award in awards)RpgUpgradeCatalog.Add(profile.Materials,award.Key,award.Value);
        return awards;
    }
}
