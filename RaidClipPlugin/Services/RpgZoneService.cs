using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static class RpgZoneService
{
    public const string Default="sunny_lagoon";
    public static void AddMissing(RpgConfig c)
    {
        // All zone fish refer to the existing Sushi/Fish registry; no migration rerolls fish.

    }
    public static Task<RpgTransactionResult> SelectAsync(ViewerPointStore points,RpgConfig c,string user,string name,string request,string activity,string zone,CancellationToken ct)
    {
        if(activity is not ("dive" or "fish"))throw new ArgumentException("UngÃ¼ltige AktivitÃ¤t.");
        c.Zones.Get(zone);
        return points.ApplyRpgAsync(user,name,request,a=>{if(!c.Enabled)return new(false,"RPG deaktiviert.");if(activity=="fish")a.Rpg.SelectedFishingZone=zone;else a.Rpg.SelectedDivingZone=zone;return new(true,"Gebiet gespeichert: "+c.Zones.Get(zone).Name);},ct);
    }
    public static IReadOnlyDictionary<string,int> Materials(RpgProfile p,RpgConfig c,RpgZoneDefinition z,string source,IDuelRandom rng)
    {
        if(source=="mimic")return RpgMaterialLootService.Award(p,c.Upgrades,"mimic",rng);
        var stats=CharacterStatService.Calculate(p,c,RpgActivity.Dive);
        var guaranteed=RpgEternalCombat.Has(p,c,"divine_infinity",3);
        var awards=new Dictionary<string,int>();
        foreach(var id in z.Materials) {
            var chance=c.Upgrades.Drops[source].GetValueOrDefault(id)?.Chance??(source=="enemy"?8m:15m);
            if(guaranteed||rng.NextInclusive(0,999999)<Math.Clamp(chance*(1+(stats.MaterialFind+(source=="fish"?0:RpgPerkCatalog.Combined(p,"diveMaterials")))/100),0,100)*10000)awards[id]=(rng.NextInclusive(1,3)+RpgEternalEightLoot.ExtraMaterial(p,c,rng))*RpgEternalCombat.NormalLootMultiplier(p,c);
        }
        foreach(var pair in awards)RpgUpgradeCatalog.Add(p.Materials,pair.Key,pair.Value);
        return awards;
    }
    public static RpgItem? Equipment(RpgProfile p,RpgConfig c,RpgZoneDefinition z,IDuelRandom rng,DateTimeOffset now)
    {
        // Explicit area access order: True is not unlocked by the numeric Cursed enum value.
        var allowed=new HashSet<ItemRarity>{ItemRarity.Common,ItemRarity.Uncommon};
        if(z.Level>=20)allowed.Add(ItemRarity.Rare);
        if(z.Level>=30)allowed.Add(ItemRarity.Epic);
        if(z.Level>=50)allowed.Add(ItemRarity.Legendary);
        if(z.Level>=70)allowed.Add(ItemRarity.Cursed);
        if(z.Level>=80)allowed.Add(ItemRarity.True);
        if(z.Level>=90)allowed.Add(ItemRarity.Abyss);
        foreach(var rarity in new[]{ItemRarity.Abyss,ItemRarity.True,ItemRarity.Cursed}) {
            if(!allowed.Contains(rarity)||rng.NextInclusive(0,999999)>=c.Upgrades.EquipmentDropChances[rarity.ToString()]*10000)continue;
            var pool=c.DiveLoot.Items.Where(i=>i.Rarity==rarity&&i.Slot.HasValue).ToArray();
            if(pool.Length>0)return Instance(pool[rng.NextInclusive(0,pool.Length-1)],c,rng,now);
        }
        var combatPool=System.Text.Json.JsonSerializer.Deserialize<RpgLootConfig>(System.Text.Json.JsonSerializer.Serialize(c.DiveLoot))!;
        combatPool.Items=combatPool.Items.Where(i=>i.Slot.HasValue&&i.Rarity<=ItemRarity.Legendary).ToList();
        var normal=new RpgLootService(rng).RollReward(combatPool,now,RpgActivity.Boss,c.BossLootChance,(ItemRarity)Math.Min((int)ItemRarity.Legendary,(int)z.MaximumRarity)).Item;
        return normal?.Slot is not null?normal:null;
    }
    private static RpgItem Instance(RpgItem definition,RpgConfig c,IDuelRandom rng,DateTimeOffset now)
    {
        var item=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(definition))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;RpgItemGenerationService.Generate(item,c.DiveLoot,rng);return item;
    }
}
