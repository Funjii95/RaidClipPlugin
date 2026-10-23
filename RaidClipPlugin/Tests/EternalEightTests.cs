using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
using System.Text.Json;
using SnapshotProtocol;
namespace RaidClipPlugin.Tests;
public sealed class EternalEightTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    private sealed class Boundary(int roll):IDuelRandom {public int NextInclusive(int min,int max)=>max==999999?roll:min;}
    private static RpgConfig Config(){var c=new RpgConfig{Enabled=true};RpgEternalCatalog.AddMissing(c);return c;}
    private static RpgProfile Equipped(string id,int pieces=8)
    {
        var p=new RpgProfile();
        foreach(var i in RpgEternalEightCatalog.Items().Where(i=>i.SetId==id).Take(pieces)){
            p.Inventory.Add(i);var slot=i.Slot!.Value;
            if(slot==EquipmentSlot.Ring1&&p.Equipment.ContainsKey(slot))slot=EquipmentSlot.Ring2;
            p.Equipment[slot]=i.InstanceId;
        }
        return p;
    }
    [Fact] public void ExactlyThreeAdditionalSetsWithTwentyFourFixedItems()
    {
        var c=Config();var sets=RpgEternalEightCatalog.Sets();var items=RpgEternalEightCatalog.Items();
        Assert.Equal(3,sets.Count);Assert.Equal(24,items.Count);Assert.Equal(24,items.Select(i=>i.ItemId).Distinct().Count());
        foreach(var s in sets){
            Assert.Equal(8,s.ItemIds.Count);Assert.Equal(Enumerable.Range(1,8),s.Bonuses.Select(b=>b.RequiredPieces));
            Assert.Equal(2,items.Count(i=>i.SetId==s.Id&&i.Slot==EquipmentSlot.Ring1));
            Assert.Single(items,i=>i.SetId==s.Id&&i.Slot==EquipmentSlot.Weapon);
        }
        foreach(var i in items){Assert.Equal(ItemRarity.Eternal,i.Rarity);Assert.Equal(1,i.CoreStatsVersion);Assert.Empty(i.DropActivities);Assert.Single(c.DiveLoot.Items,j=>j.ItemId==i.ItemId);}
        Assert.Equal(1650,items.Single(i=>i.ItemId=="eternal_cosmic_forge_weapon").Attributes.Attack);
        Assert.Equal(1550,items.Single(i=>i.ItemId=="eternal_soul_devourer_weapon").Attributes.Attack);
        Assert.Equal(1450,items.Single(i=>i.ItemId=="eternal_ten_seas_treasure_weapon").Attributes.Attack);
        Assert.Equal(3,c.Sets.Count(s=>s.Id.StartsWith("eternal_")&&s.ItemIds.Count==6));
        Assert.Equal(8,c.Sets.Single(s=>s.Id=="divine_infinity").ItemIds.Count);
    }
    [Theory][InlineData("eternal_cosmic_forge")][InlineData("eternal_soul_devourer")][InlineData("eternal_ten_seas_treasure")]
    public void EveryThresholdIsCumulativeAndDuplicateRingsNeverIncreaseProgress(string id)
    {
        var c=Config();for(var n=1;n<=8;n++){
            var p=Equipped(id,n);
            Assert.True(RpgSetCombatService.Active(p,c.Sets,id,n));
            Assert.False(RpgSetCombatService.Active(p,c.Sets,id,n+1));
        }
        var full=Equipped(id);var first=full.Inventory.First(i=>i.Slot==EquipmentSlot.Ring1);
        var second=full.Inventory.Last(i=>i.Slot==EquipmentSlot.Ring1);
        second.ItemId=first.ItemId;
        Assert.False(RpgSetCombatService.Active(full,c.Sets,id,8));
        full.Equipment.Remove(EquipmentSlot.Weapon);
        Assert.False(RpgSetCombatService.Active(full,c.Sets,id,7));
    }
    [Fact] public void CosmicResonanceExpiresAndSupernovaConsumesStacksWithoutRecursion()
    {
        var c=Config();var p=Equipped("eternal_cosmic_forge");RpgHealthService.Refresh(p,c.Health,Now);
        for(var i=0;i<10;i++)RpgEternalEightCombat.AfterDirect(p,c,1,0,false,Now);
        var a=new RpgAttributes();RpgEternalEightCombat.Dynamic(p,c,a,Now);Assert.Equal(40,a.Damage);
        var hit=RpgEternalEightCombat.Proc(100,true,1,p,c,Now);Assert.Equal(1630,hit.Damage);
        Assert.Equal(150,RpgEternalEightCombat.Proc(100,true,1,p,c,Now).Damage);
        a=new();RpgEternalEightCombat.Dynamic(p,c,a,Now);Assert.Equal(0,a.Damage);
        RpgEternalEightCombat.AfterDirect(p,c,1,0,false,Now);
        a=new();RpgEternalEightCombat.Dynamic(p,c,a,Now.AddSeconds(9));Assert.Equal(0,a.Damage);
        p.Equipment.Remove(EquipmentSlot.Weapon);p.Equipment.Remove(EquipmentSlot.Amulet);p.Equipment.Remove(EquipmentSlot.Boots);p.Equipment.Remove(EquipmentSlot.Gloves);
        a=new();RpgEternalEightCombat.Dynamic(p,c,a,Now);Assert.Equal(0,a.Damage);
    }
    [Fact] public void SoulHealingUsesConfirmedDamageNeverAbsorbedHitsAndDoesNotRevive()
    {
        var c=Config();var p=Equipped("eternal_soul_devourer");RpgHealthService.Refresh(p,c.Health,Now);p.Health.CurrentHp=100;
        RpgEternalEightCombat.AfterDirect(p,c,0,0,true,Now);Assert.Equal(100,p.Health.CurrentHp);
        RpgEternalEightCombat.AfterDirect(p,c,100,100,false,Now);
        Assert.True(p.Health.CurrentHp>100);Assert.True(p.Health.CurrentHp<=RpgHealthService.Maximum(p,c.Health));
        var a=new RpgAttributes();RpgEternalEightCombat.Dynamic(p,c,a,Now);Assert.Equal(30,a.Damage);
        a=new();RpgEternalEightCombat.Dynamic(p,c,a,Now.AddSeconds(9));Assert.Equal(0,a.Damage);
        p.Health.CurrentHp=0;RpgEternalEightCombat.AfterDirect(p,c,1000,1000,true,Now);Assert.Equal(0,p.Health.CurrentHp);
    }
    [Fact] public void SoulRescueIsOncePerBossAndNeverTriggersInOrdinaryZoneCombat()
    {
        var c=Config();var p=Equipped("eternal_soul_devourer");RpgHealthService.Refresh(p,c.Health,Now);p.Health.CurrentHp=100;
        Assert.Equal(99,RpgEternalEightCombat.Receive(99,p,c,"zone",false,Now));Assert.Equal(100,p.Health.CurrentHp);
        RpgEternalEightCombat.Receive(99,p,c,"boss-a",true,Now);Assert.True(p.Health.CurrentHp>100);
        p.Health.CurrentHp=100;Assert.Equal(99,RpgEternalEightCombat.Receive(99,p,c,"boss-a",true,Now));Assert.Equal(100,p.Health.CurrentHp);
    }
    [Theory][InlineData("eternal_cosmic_forge")][InlineData("eternal_soul_devourer")][InlineData("eternal_ten_seas_treasure")]
    public void NewItemsUpgradeWithoutRerollingAndTheirCombatStatePassesCloudValidation(string id)
    {
        var c=Config();var p=Equipped(id);RpgHealthService.Refresh(p,c.Health,Now);
        var originals=p.Inventory.ToDictionary(i=>i.ItemId,i=>JsonSerializer.Serialize(i.Attributes));
        foreach(var item in p.Inventory){
            item.UpgradeLevel=9;item.IsRefined=true;
            Assert.Equal(originals[item.ItemId],JsonSerializer.Serialize(item.Attributes));
            Assert.Equal(decimal.Round(item.Attributes.Attack*1.5m,4),RpgEquipmentUpgradeRules.Effective(item).Attack);
        }
        RpgEternalEightCombat.AfterDirect(p,c,10,0,true,Now);
        var account=new ViewerPointEntry{UserId="1001",DisplayName="Synthetic",Rpg=p};
        var snapshot=new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),Now,SnapshotKind.Upsert,JsonSerializer.Serialize(account));
        SnapshotValidation.Validate(snapshot,"42");
        var restored=JsonSerializer.Deserialize<ViewerPointEntry>(snapshot.AccountJson!)!;
        Assert.Equal(8,restored.Rpg.Inventory.Count);Assert.Equal(8,restored.Rpg.Equipment.Count);
        Assert.Equal(p.Cooldowns,restored.Rpg.Cooldowns);
    }
    [Theory][InlineData("normal",100)][InlineData("elite",400)][InlineData("mimic",800)][InlineData("eonath",1000)]
    public void RareRollBoundaryIsForWholeGroupAndRequiresAuthorizedVictory(string source,int boundary)
    {
        var c=Config();var p=new RpgProfile();p.Health.CurrentHp=100;
        Assert.NotNull(RpgEternalEightLoot.Victory(p,c,"primordial_throne",100,source,true,new Boundary(boundary-1),Now,"boss_eonath"));
        Assert.Null(RpgEternalEightLoot.Victory(p,c,"primordial_throne",100,source,true,new Boundary(boundary),Now,"boss_eonath"));
        Assert.Null(RpgEternalEightLoot.Victory(p,c,"sunny_lagoon",100,source,true,new Low(),Now,"boss_eonath"));
        Assert.Null(RpgEternalEightLoot.Victory(p,c,"primordial_throne",99,source,true,new Low(),Now,"boss_eonath"));
        Assert.Null(RpgEternalEightLoot.Victory(p,c,"primordial_throne",100,source,false,new Low(),Now,"boss_eonath"));
        if(source=="eonath")Assert.Null(RpgEternalEightLoot.Victory(p,c,"primordial_throne",100,source,true,new Low(),Now,"boss_korallus"));
        p.Health.CurrentHp=0;Assert.Null(RpgEternalEightLoot.Victory(p,c,"primordial_throne",100,source,true,new Low(),Now,"boss_eonath"));
    }
}
