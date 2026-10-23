using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
using System.Text.Json;
namespace RaidClipPlugin.Services;
public static class RpgEternalLoot
{
    public const string Throne="primordial_throne";
    public static RpgItem? Victory(RpgProfile p,RpgConfig c,string area,int level,string source,bool victory,IDuelRandom random,DateTimeOffset now)
    {
        if(!victory||area!=Throne||level!=100)return null;
        var chance=c.Zones.EternalChances.GetValueOrDefault(source)*10000;
        if(chance==0||random.NextInclusive(0,999999)>=chance)return null;
        // One successful Eternal roll selects one of 18 armor pieces or 3 weapons uniformly.
        var pool=RpgEternalCatalog.Items().Where(i=>i.Rarity==ItemRarity.Eternal).Concat(RpgWeaponCatalog.Items().Where(i=>i.Rarity==ItemRarity.Eternal)).ToArray();
        var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(pool[random.NextInclusive(0,pool.Length-1)]))!;
        item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;p.Inventory.Add(item);return item;
    }
    public static int FishRolls(RpgProfile p,RpgConfig c,IDuelRandom random)
    {
        var count=Has(p,c,2)&&random.NextInclusive(0,9999)<3000?1:0;
        if(Has(p,c,5)&&random.NextInclusive(0,9999)<2500)count++;
        if(Has(p,c,6)&&random.NextInclusive(0,9999)<3000)count++;
        return count;
    }
    public static int DiveRolls(RpgProfile p,RpgConfig c,IDuelRandom random)=>(Has(p,c,2)&&random.NextInclusive(0,9999)<3500?1:0)+(Has(p,c,5)&&random.NextInclusive(0,9999)<2500?1:0);
    private static bool Has(RpgProfile p,RpgConfig c,int n)=>RpgEternalCombat.Has(p,c,"eternal_star_hunter",n);
}
