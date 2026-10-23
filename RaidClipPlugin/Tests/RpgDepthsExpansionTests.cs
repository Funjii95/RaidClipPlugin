using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;

public sealed class RpgDepthsExpansionTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-depths-"+Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
    private sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
    private sealed class Gate(int first,int second=1):IDuelRandom{int calls;public int NextInclusive(int min,int max)=>++calls==1?first:second;}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    private static RpgProfile Wear(string id,int pieces,RpgConfig c)
    {
        var p=new RpgProfile();foreach(var i in RpgDepthsExpansion.Items().Where(i=>i.SetId==id).Take(pieces)){i.Generation=null;p.Inventory.Add(i);p.Equipment[i.Slot!.Value]=i.InstanceId;}return p;
    }
    [Fact] public void CatalogHas54UniqueGearItemsElevenStackableMaterialsSixRealSetsAndNoNewSlot()
    {
        var c=new RpgConfig();c.Validate();var items=RpgDepthsExpansion.Items();Assert.Equal(54,items.Count);Assert.Equal(54,items.Select(i=>i.ItemId).Distinct().Count());
        Assert.Equal(6,RpgDepthsExpansion.Sets().Count);Assert.Equal(11,RpgUpgradeCatalog.Materials.Length);Assert.Equal(8,Enum.GetValues<EquipmentSlot>().Length);
        Assert.All(RpgUpgradeCatalog.Materials,m=>Assert.True(RpgUpgradeCatalog.Properties.ContainsKey(m.Id)));
        var n=c.DiveLoot.Items.Count;RpgDepthsExpansion.AddMissing(c);Assert.Equal(n,c.DiveLoot.Items.Count);
        Assert.All(items.Where(i=>i.Rarity==ItemRarity.Abyss),i=>Assert.Equal(new[]{RpgActivity.Boss},i.DropActivities));
    }
    [Fact] public void ExistingGeneratorEnableActionPreservesEveryFixedCurseAndJewelryDefinition()
    {
        var before=new RpgConfig();var enabled=RpgItemGenerationService.EnableForFutureDrops(before);
        foreach(var original in RpgDepthsExpansion.Items().Where(i=>i.Generation is null)){
            var current=enabled.DiveLoot.Items.Single(i=>i.ItemId==original.ItemId);Assert.Null(current.Generation);
            Assert.Equal(JsonSerializer.Serialize(original.Attributes),JsonSerializer.Serialize(current.Attributes));}
    }
    [Theory][InlineData("dive")][InlineData("fish")][InlineData("boss")][InlineData("enemy")][InlineData("chest")][InlineData("rareChest")][InlineData("legendaryChest")]
    public void EveryMaterialSourceUsesItsExactPoolAndConfiguredQuantities(string source)
    {
        var c=new RpgUpgradeConfig();var p=new RpgProfile();var awards=RpgMaterialLootService.Award(p,c,source,new Low());
        Assert.Equal(c.Drops[source].Count(x=>x.Value.Chance>0),awards.Count);
        foreach(var a in awards){Assert.Equal(c.Drops[source][a.Key].Minimum,a.Value);Assert.Equal(a.Value,RpgUpgradeCatalog.Count(p.Materials,a.Key));}
        Assert.Empty(RpgMaterialLootService.Award(new(),c,source,new High()));
    }
    [Theory][InlineData(4999,true)][InlineData(5000,false)][InlineData(9999,false)]
    public void MimicGateIsExactlyFiftyPercentAndIgnoresHotTime(int gate,bool granted)
    {
        var c=new RpgUpgradeConfig();var a=RpgMaterialLootService.Award(new(),c,"mimic",new Gate(gate),200);Assert.Equal(granted,a.Count==1);
    }
    [Theory][InlineData(1,"red_pearl")][InlineData(30,"red_pearl")][InlineData(31,"rune_shell")][InlineData(55,"rune_shell")][InlineData(56,"cursed_silk")][InlineData(73,"cursed_silk")][InlineData(74,"true_coral_core")][InlineData(88,"true_coral_core")][InlineData(89,"abyss_shard")][InlineData(98,"abyss_shard")][InlineData(99,"eternal_depth_core")][InlineData(100,"eternal_depth_core")]
    public void MimicPoolBoundariesMatchAllSixWeights(int roll,string id)=>Assert.Equal(id,Assert.Single(RpgMaterialLootService.Award(new(),new(),"mimic",new Gate(0,roll))).Key);
    [Theory][InlineData(ItemRarity.Common,"pearl_dust",5)][InlineData(ItemRarity.Uncommon,"pearl_dust",5)][InlineData(ItemRarity.Rare,"pearl_dust",5)][InlineData(ItemRarity.Epic,"luminous_scale",3)][InlineData(ItemRarity.Legendary,"rune_shell",3)][InlineData(ItemRarity.True,"true_coral_core",3)][InlineData(ItemRarity.Cursed,"cursed_silk",3)][InlineData(ItemRarity.Abyss,"abyss_shard",3)]
    public void PlusNineRequiresMatchingRarityMaterial(ItemRarity rarity,string id,int count)
    {
        var cost=RpgEquipmentUpgradeRules.Cost(8,rarity,new());Assert.Equal(12000,cost.Pearls);Assert.Equal(5,cost.Materials["reinforcement_core"]);Assert.Equal(1,cost.Materials["red_pearl"]);Assert.Equal(count,cost.Materials[id]);
    }
    [Fact] public async Task NineGuaranteedStepsAndRefinementPersistWithoutRerollAndReplayCannotDebitTwice()
    {
        var store=new ViewerPointStore(root);var c=new RpgConfig{Enabled=true};var item=RpgDepthsExpansion.Items().Single(i=>i.ItemId=="cursed_greed_crown");var original=JsonSerializer.Serialize(item.Attributes);
        await store.ApplyRpgAsync("42","Synthetic","seed",a=>{a.Points=100000;foreach(var m in RpgUpgradeCatalog.Materials)RpgUpgradeCatalog.Add(a.Rpg.Materials,m.Id,100);a.Rpg.Inventory.Add(item);return new(true);},default);
        var service=new RpgUpgradeService(store,c);
        for(int level=0;level<9;level++){var request="upgrade:"+level;Assert.True((await service.ApplyAsync("42","Synthetic",request,item.InstanceId,level,false,default)).Success);var replay=await service.ApplyAsync("42","Synthetic",request,item.InstanceId,level,false,default);Assert.True(replay.Duplicate);}
        Assert.True((await service.ApplyAsync("42","Synthetic","refine",item.InstanceId,9,true,default)).Success);
        Assert.False((await service.ApplyAsync("42","Synthetic","refine-again",item.InstanceId,9,true,default)).Success);
        var account=(await new ViewerPointStore(root).GetProfileAsync("42",default)).Entry;var restored=Assert.Single(account.Rpg.Inventory);
        Assert.Equal(9,restored.UpgradeLevel);Assert.True(restored.IsRefined);Assert.Equal(original,JsonSerializer.Serialize(restored.Attributes));
        Assert.Equal(42,RpgEquipmentUpgradeRules.Effective(restored).CritChance);Assert.Equal(-12,RpgEquipmentUpgradeRules.Effective(restored).HealingPower);
        Assert.Equal(100000-c.Upgrades.PearlCosts.Sum()-15000,account.Points);
        var envelope=new SnapshotMessage(1,"42","42",1,Guid.NewGuid(),now,SnapshotKind.Upsert,JsonSerializer.Serialize(account));var projection=SnapshotValidation.Validate(envelope,"42");
        using var json=JsonDocument.Parse(projection.AccountJson!);Assert.True(json.RootElement.GetProperty("Rpg").GetProperty("Inventory")[0].GetProperty("IsRefined").GetBoolean());Assert.Equal(100,json.RootElement.GetProperty("Rpg").GetProperty("Materials").GetProperty("AbyssShard").GetInt32());
    }
    [Fact] public async Task MissingMaterialsWrongOwnerAndStaleLevelsLeaveAllBalancesAndOriginalStatsUntouched()
    {
        var store=new ViewerPointStore(root);var c=new RpgConfig{Enabled=true};var item=new RpgItem{ItemId="synthetic",Slot=EquipmentSlot.Chest,UpgradeLevel=4,Attributes=new(){MaximumHp=10},CoreStatsVersion=1};
        await store.ApplyRpgAsync("42","Synthetic","seed",a=>{a.Points=10000;a.Rpg.Inventory.Add(item);return new(true);},default);var service=new RpgUpgradeService(store,c);
        Assert.False((await service.ApplyAsync("42","Synthetic","missing",item.InstanceId,4,false,default)).Success);
        Assert.False((await service.ApplyAsync("43","Other","owner",item.InstanceId,4,false,default)).Success);
        Assert.False((await service.ApplyAsync("42","Synthetic","stale",item.InstanceId,2,false,default)).Success);
        var a=(await store.GetProfileAsync("42",default)).Entry;Assert.Equal(10000,a.Points);Assert.Equal(4,Assert.Single(a.Rpg.Inventory).UpgradeLevel);
    }
    [Theory][InlineData("true_eternal_reef")][InlineData("true_deep_guardian")][InlineData("true_ancient_current")][InlineData("abyss_devourer")][InlineData("abyss_blood_dynasty")][InlineData("abyss_sunken_world")]
    public void SixPiecesRequireDistinctEquippedDefinitionsAndUnequippingDisablesBonus(string id)
    {
        var c=new RpgConfig();var p=Wear(id,6,c);Assert.True(RpgDepthsCombatService.Has(p,c,id,6));p.Equipment.Remove(EquipmentSlot.Amulet);Assert.False(RpgDepthsCombatService.Has(p,c,id,6));
    }
    [Fact] public void GuardianShieldAndBloodRescueUseAuthoritativeHpAndCooldowns()
    {
        var c=new RpgConfig();var p=Wear("true_deep_guardian",6,c);RpgHealthService.Refresh(p,c.Health,now);var max=RpgHealthService.Maximum(p,c.Health);
        RpgDepthsCombatService.BeforeCounter(p,c,now);Assert.Equal((int)(max*.2m),p.Health.SetShieldHp);p.Health.SetShieldHp=0;RpgDepthsCombatService.BeforeCounter(p,c,now.AddSeconds(1));Assert.Equal(0,p.Health.SetShieldHp);
        var blood=Wear("abyss_blood_dynasty",6,c);RpgHealthService.Refresh(blood,c.Health,now);blood.Health.CurrentHp=1;
        RpgDepthsCombatService.AfterCounter(blood,c,"boss1",now);Assert.True(blood.Health.CurrentHp>1);blood.Health.CurrentHp=1;RpgDepthsCombatService.AfterCounter(blood,c,"boss1",now.AddSeconds(20));Assert.Equal(1,blood.Health.CurrentHp);
    }
    [Fact] public void ShadowStacksExpireAndCriticalProcsCannotRecurse()
    {
        var c=new RpgConfig();var p=Wear("abyss_sunken_world",6,c);
        for(int i=0;i<8;i++)RpgDepthsCombatService.AttackProc(100,true,1,p,c,new Low(),now);
        Assert.Equal(25,RpgDepthsCombatService.DamageBonus(p,c,now));Assert.Equal(0,RpgDepthsCombatService.DamageBonus(p,c,now.AddSeconds(9)));
    }
    [Fact] public void AbyssEquipmentOnlyDropsFromQualifiedEndgameSources()
    {
        var c=new RpgConfig();c.Upgrades.EquipmentDropChances["Abyss"]=100;c.Upgrades.EquipmentDropChances["True"]=c.Upgrades.EquipmentDropChances["Cursed"]=0;
        var p=new RpgProfile();RpgDepthsLootService.Award(p,c,"dive",new Low(),now,0,true);RpgDepthsLootService.Award(p,c,"boss",new Low(),now);Assert.Empty(p.Inventory);
        RpgDepthsLootService.Award(p,c,"boss",new Low(),now,0,true);var item=Assert.Single(p.Inventory);Assert.Equal(ItemRarity.Abyss,item.Rarity);Assert.Null(item.Generation);
    }
    [Fact] public void DevourerCriticalsIgnoreFifteenPercentOfExistingBossReduction()
    {
        var c=new RpgConfig();var p=Wear("abyss_devourer",6,c);Assert.Equal(80,RpgDepthsCombatService.MitigateBoss(100,20,false,p,c));Assert.Equal(83,RpgDepthsCombatService.MitigateBoss(100,20,true,p,c));
    }
    [Fact] public async Task FailedStorageRollsBackUpgradeCostsAndAllowsTheSameUncommittedRequest()
    {
        var store=new ViewerPointStore(root);var c=new RpgConfig{Enabled=true};var item=new RpgItem{Slot=EquipmentSlot.Helmet,CoreStatsVersion=1};
        await store.ApplyRpgAsync("42","Synthetic","seed",a=>{a.Points=1000;a.Rpg.Inventory.Add(item);return new(true);},default);
        var blocked=Path.Combine(root,"viewer-points.json.tmp");Directory.CreateDirectory(blocked);var service=new RpgUpgradeService(store,c);
        await Assert.ThrowsAnyAsync<Exception>(()=>service.ApplyAsync("42","Synthetic","upgrade",item.InstanceId,0,false,default));Assert.Equal(1000,(await store.GetProfileAsync("42",default)).Entry.Points);Directory.Delete(blocked);
        Assert.True((await service.ApplyAsync("42","Synthetic","upgrade",item.InstanceId,0,false,default)).Success);Assert.Equal(900,(await store.GetProfileAsync("42",default)).Entry.Points);
    }
}
