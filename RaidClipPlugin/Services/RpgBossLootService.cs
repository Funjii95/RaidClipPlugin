using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class RpgBossLootService(ViewerPointStore points, RpgConfig config, IDuelRandom random)
{
    public static RpgItem? RollLegendary(RpgConfig config,IDuelRandom random,DateTimeOffset now,bool mimic=false)
    {
        var pool=config.DiveLoot.Items.Where(i=>i.Rarity==ItemRarity.Legendary && i.DropActivities.Contains(RpgActivity.Boss) && i.Kind!=RpgItemKind.Consumable && i.DropWeight>0).ToArray();
        if(pool.Length==0 || random.NextInclusive(0,9999)>=(mimic?config.Mimic.LegendaryChance:config.Mimic.NormalBossLegendaryChance)*100)return null;
        var roll=random.NextInclusive(0,pool.Sum(i=>i.DropWeight)-1);var chosen=pool[^1];
        foreach(var item in pool){chosen=item;roll-=item.DropWeight;if(roll<0)break;}
        var found=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(chosen))!;
        found.InstanceId=Guid.NewGuid().ToString("N");found.FoundAt=now;
        RpgItemGenerationService.Generate(found,config.DiveLoot,random);
        return found;
    }
    public static ItemRarity MaximumRarity(RpgConfig config, int initialHp, int participants) =>
        config.BossLegendaryEnabled && initialHp >= config.BossLegendaryMinimumHp && participants >= config.BossLegendaryMinimumPlayers
            ? ItemRarity.Legendary : ItemRarity.Epic;

    public async Task<RpgItem?> AwardAsync(string userId, string name, string encounterId, int initialHp, int participants, DateTimeOffset now, CancellationToken token)
    {
        if (!config.Enabled) return null;
        config.Validate(); RpgItem? item = null;
        var result = await points.ApplyRpgAsync(userId, name, "bossloot:" + encounterId, account =>
        {
            if(config.Health.Enabled && account.Rpg.Health.BossId==encounterId && account.Rpg.Health.Defeated)
                return new(false,"Besiegte Teilnehmer erhalten keine Bossbeute.");
            item = new RpgLootService(random).RollReward(config.DiveLoot, now, RpgActivity.Boss, config.BossLootChance,
                ItemRarity.Epic,points.HotTime.DropBonus(config.HotTime)).Item;
            if (item is not null) account.Rpg.Inventory.Add(item);
            RpgMaterialLootService.Award(account.Rpg,config.Upgrades,"boss",random,points.HotTime.DropBonus(config.HotTime));
            RpgDepthsLootService.Award(account.Rpg,config,"boss",random,now,points.HotTime.DropBonus(config.HotTime),initialHp>=config.Health.LegendaryThreshold);
            var legendary=RollLegendary(config,random,now);
            if(legendary is not null){account.Rpg.Inventory.Add(legendary);item=legendary;}
            return new(true);
        }, token);
        return result.Success ? item : null;
    }
}
