using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using System.Text.Json;
namespace RaidClipPlugin.Services;
public static class RpgEternalEightLoot
{
    public static RpgItem? Victory(RpgProfile p,RpgConfig c,string area,int level,string source,bool victory,IDuelRandom rng,DateTimeOffset now,string? personalBossId=null)
    {
        if(!victory||p.Health.CurrentHp is <=0||area!="primordial_throne"||level!=100)return null;
        if(source=="eonath"&&personalBossId!="boss_eonath")return null;
        var chance=c.Zones.EternalEightChances.GetValueOrDefault(source);
        if(chance<=0||rng.NextInclusive(0,999999)>=chance*10000)return null;
        var sets=RpgEternalEightCatalog.Sets();var set=sets[rng.NextInclusive(0,sets.Count-1)];
        var id=set.ItemIds[rng.NextInclusive(0,set.ItemIds.Count-1)];
        var definition=c.DiveLoot.Items.Single(i=>i.ItemId==id);
        var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;
        item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;
        p.Inventory.Add(item);return item;
    }
    private static bool Has(RpgProfile p,RpgConfig c,int n)=>RpgEternalCombat.Has(p,c,"eternal_ten_seas_treasure",n);
    public static int FishRolls(RpgProfile p,RpgConfig c,IDuelRandom rng)=>(Has(p,c,2)&&rng.NextInclusive(0,9999)<5000?1:0)+(Has(p,c,8)&&rng.NextInclusive(0,9999)<3500?1:0);
    public static int DiveRolls(RpgProfile p,RpgConfig c,IDuelRandom rng)=>(Has(p,c,3)&&rng.NextInclusive(0,9999)<4000?1:0)+(Has(p,c,8)&&rng.NextInclusive(0,9999)<3500?1:0);
    public static int ExtraMaterial(RpgProfile p,RpgConfig c,IDuelRandom rng)=>Has(p,c,5)&&rng.NextInclusive(0,9999)<2500?1:0;
}
