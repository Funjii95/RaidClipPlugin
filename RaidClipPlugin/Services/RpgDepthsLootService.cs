using System.Text.Json;
using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static class RpgDepthsLootService
{
    // An additional independent pool preserves the existing fish, treasure and equipment rolls.
    public static void Award(RpgProfile p,RpgConfig c,string source,IDuelRandom rng,DateTimeOffset now,decimal hotTime=0,bool endgame=false)
    {
        foreach(var rarity in new[]{ItemRarity.True,ItemRarity.Cursed,ItemRarity.Abyss})
        {
            if(rarity==ItemRarity.Abyss&&(!endgame||source is not ("boss" or "mimic")))continue;
            var key=rarity.ToString();var baseChance=c.Upgrades.EquipmentDropChances[key];
            var chance=Math.Clamp(baseChance*(1+Math.Clamp(hotTime,0,200)/100),0,100);
            if(chance<=0||rng.NextInclusive(0,999999)>=chance*10000)continue;
            var activity=source=="fish"?RpgActivity.Fishing:source is "boss" or "mimic"?RpgActivity.Boss:RpgActivity.Dive;
            var pool=c.DiveLoot.Items.Where(i=>i.Rarity==rarity&&i.Slot.HasValue&&i.DropActivities.Contains(activity)&&i.DropWeight>0).ToArray();
            if(pool.Length==0)continue;
            var roll=rng.NextInclusive(1,pool.Sum(i=>i.DropWeight));var chosen=pool[^1];foreach(var item in pool){chosen=item;roll-=item.DropWeight;if(roll<=0)break;}
            var found=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(chosen))!;found.InstanceId=Guid.NewGuid().ToString("N");found.FoundAt=now;
            RpgItemGenerationService.Generate(found,c.DiveLoot,rng);p.Inventory.Add(found);
        }
    }
}
