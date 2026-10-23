using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;
public sealed class RpgCollectionInventoryTests
{
    private static ViewerPointEntry Account(int count) => new()
    {
        UserId="collection-test",Points=100,
        Rpg=new(){Inventory=Enumerable.Range(0,count).Select(n=>new RpgItem
        {
            ItemId="shell-relic",InstanceId="relic-"+n,Kind=RpgItemKind.Treasure,
            Rarity=ItemRarity.Rare,Collector=new(){BossPearls=2},SalvageValue=250
        }).ToList()}
    };
    [Fact] public void SixthCopyIsConvertedOnceAndBonusesRemain()
    {
        var a=Account(6);var c=new RpgSalvageConfig();
        Assert.Equal(1,RpgCollectionInventory.SalvageOverflow(a,c));
        Assert.Equal(5,a.Rpg.Inventory.Count);Assert.Equal(350,a.Points);
        Assert.Equal(0,RpgCollectionInventory.SalvageOverflow(a,c));
        Assert.Equal(350,a.Points);
        Assert.Equal(2,RpgCollectorService.Calculate(a.Rpg,new RpgLootConfig()).BossPearls);
    }
    [Fact] public void DifferentCollectionsEachKeepFiveAndEquipmentIsUntouched()
    {
        var a=Account(7);var equipment=new RpgItem{ItemId="weapon",Kind=RpgItemKind.Equipment,Slot=EquipmentSlot.Weapon};
        a.Rpg.Inventory.Add(equipment);a.Rpg.Inventory.Add(new(){ItemId="other",Kind=RpgItemKind.Treasure,Collector=new()});
        Assert.Equal(2,RpgCollectionInventory.SalvageOverflow(a,new()));
        Assert.Contains(equipment,a.Rpg.Inventory);Assert.Equal(7,a.Rpg.Inventory.Count);
    }
    [Fact] public void LockedCollectionsAndBalanceLimitPreventDestructiveConversion()
    {
        var a=Account(6);foreach(var item in a.Rpg.Inventory)item.IsLocked=true;
        Assert.Equal(0,RpgCollectionInventory.SalvageOverflow(a,new()));
        foreach(var item in a.Rpg.Inventory)item.IsLocked=false;
        Assert.Equal(0,RpgCollectionInventory.SalvageOverflow(a,new(),200));
        Assert.Equal(6,a.Rpg.Inventory.Count);Assert.Equal(100,a.Points);
    }
    [Fact] public async Task DuplicateTransactionsNeverCreditOverflowTwice()
    {
        var dir=Path.Combine(Path.GetTempPath(),"otter-collection-"+Guid.NewGuid().ToString("N"));
        try {
            var store=new ViewerPointStore(dir);
            var first=await store.ApplyRpgAsync("123","Test","collect",a=>{
                a.Points=100;a.Rpg.Inventory=Account(6).Rpg.Inventory;return new(true);
            },default);
            Assert.True(first.Success);Assert.Equal(350,first.Balance);
            var replay=await store.ApplyRpgAsync("123","Test","collect",a=>throw new Exception("Replay executed"),default);
            Assert.True(replay.Duplicate);Assert.Equal(350,replay.Balance);
            var restored=await new ViewerPointStore(dir).GetProfileAsync("123",default);
            Assert.Equal(5,restored.Entry.Rpg.Inventory.Count);Assert.Equal(350,restored.Entry.Points);
        } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
