using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgHealthEquipmentTests
{
    [Fact]
    public void EquippedItemsIncreaseMaximumWithoutHealingAndLevelGainStillHealsTwo()
    {
        var p=new RpgProfile();var c=new RpgHealthConfig();var now=DateTimeOffset.UtcNow;
        RpgHealthService.Refresh(p,c,now);p.Health.CurrentHp=70;
        var item=RpgHealthEquipmentCatalog.Items().Last();p.Inventory.Add(item);p.Equipment[EquipmentSlot.Amulet]=item.InstanceId;
        RpgHealthService.Refresh(p,c,now);Assert.Equal(150,RpgHealthService.Maximum(p,c));Assert.Equal(70,p.Health.CurrentHp);
        RpgHealthService.Experience(p,c,1000,now);Assert.Equal(177,RpgHealthService.Maximum(p,c));Assert.Equal(97,p.Health.CurrentHp);
        p.Equipment.Clear();RpgHealthService.Refresh(p,c,now);Assert.Equal(127,RpgHealthService.Maximum(p,c));Assert.Equal(97,p.Health.CurrentHp);
    }
    [Fact]
    public void OnlyEquippedDistinctInstancesCountAndBonusIsCapped()
    {
        var p=new RpgProfile();var c=new RpgConfig();var ring=new RpgItem{Slot=EquipmentSlot.Ring1,Attributes=new(){MaximumHp=20}};
        p.Inventory.Add(ring);Assert.Equal(100,RpgHealthService.Maximum(p,c.Health));
        p.Equipment[EquipmentSlot.Ring1]=ring.InstanceId;p.Equipment[EquipmentSlot.Ring2]=ring.InstanceId;
        Assert.Equal(120,RpgHealthService.Maximum(p,c.Health));c.Health.EquipmentHpCap=15;
        Assert.Equal(115,RpgHealthService.Maximum(p,c.Health));Assert.Equal(15,CharacterStatService.Calculate(p,c,RpgActivity.Boss).MaximumHp);
        Assert.Contains("20 maximale Lebenspunkte",RpgChatPresentation.Bonuses(ring.Attributes));
        Assert.Equal(20,RpgChatPresentation.Snapshot(ring.Attributes).MaximumHp);
    }
    [Fact]
    public void RemovingHpGearClampsAndReequipCannotRestoreLostHpOrClearDeath()
    {
        var p=new RpgProfile();var c=new RpgHealthConfig();var now=DateTimeOffset.UtcNow;
        var item=RpgHealthEquipmentCatalog.Items().Last();p.Inventory.Add(item);p.Equipment[EquipmentSlot.Amulet]=item.InstanceId;
        RpgHealthService.Refresh(p,c,now);Assert.Equal(150,p.Health.CurrentHp);
        p.Equipment.Clear();RpgHealthService.Refresh(p,c,now);Assert.Equal(100,p.Health.CurrentHp);
        p.Equipment[EquipmentSlot.Amulet]=item.InstanceId;RpgHealthService.Refresh(p,c,now);Assert.Equal(100,p.Health.CurrentHp);
        p.Health.CurrentHp=0;p.Health.Defeated=true;RpgHealthService.Refresh(p,c,now);Assert.Equal(0,p.Health.CurrentHp);Assert.True(p.Health.Defeated);
    }
    [Fact]
    public void DefaultsAreValidMigrationIsIdempotentAndCustomDefinitionsRemain()
    {
        var c=new RpgLootConfig();c.Validate();Assert.Equal(5,c.Items.Count(i=>i.ItemId.StartsWith("hp-")));
        var first=c.Items.First(i=>i.ItemId.StartsWith("hp-"));first.Attributes.MaximumHp=42;
        RpgHealthEquipmentCatalog.AddMissing(c);Assert.Equal(42,first.Attributes.MaximumHp);
        Assert.Equal(5,c.Items.Count(i=>i.ItemId.StartsWith("hp-")));
        var p=new RpgProfile();p.Inventory.Add(first);p.Equipment[first.Slot!.Value]=first.InstanceId;
        var saved=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(p))!;Assert.Equal(142,RpgHealthService.Maximum(saved,new()));
    }
    [Fact]
    public async Task EquipmentTransactionsUpdateHpImmediatelyAndPersistAcrossRestart()
    {
        var dir=Path.Combine(Path.GetTempPath(),"RaidClipHpGear-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(dir);var c=new RpgConfig{Enabled=true};c.Health.PassiveHealing=false;
            var item=RpgHealthEquipmentCatalog.Items().Last();
            await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Inventory.Add(item);RpgHealthService.Refresh(a.Rpg,c.Health,DateTimeOffset.UtcNow);a.Rpg.Health.CurrentHp=80;return new(true);},default);
            var service=new RpgProgressionService(store,c);
            Assert.True((await service.EquipAsync("u","U","equip",item.InstanceId,EquipmentSlot.Amulet,default)).Success);
            var p=(await new ViewerPointStore(dir).GetProfileAsync("u",default)).Entry.Rpg;
            Assert.Equal(225,RpgHealthService.Maximum(p,c.Health));Assert.Equal(80,p.Health.CurrentHp);
            Assert.True((await service.UnequipAsync("u","U","off",EquipmentSlot.Amulet,default)).Success);
            p=(await store.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(100,RpgHealthService.Maximum(p,c.Health));Assert.Equal(80,p.Health.CurrentHp);
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
