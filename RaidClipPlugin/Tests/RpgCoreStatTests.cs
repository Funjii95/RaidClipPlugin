using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgCoreStatTests
{
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    [Theory]
    [InlineData(ItemRarity.Common,1,1)] [InlineData(ItemRarity.Uncommon,2,2)] [InlineData(ItemRarity.Rare,3,4)]
    [InlineData(ItemRarity.Epic,5,7)] [InlineData(ItemRarity.Legendary,8,10)]
    public void EveryEquipmentSlotHasTwoAdditionalGuaranteedStats(ItemRarity rarity,int power,int stamina)
    {
        foreach(var slot in Enum.GetValues<EquipmentSlot>())
        {
            var item=new RpgItem{Slot=slot,Rarity=rarity,Generation=RpgItemGenerationService.DefaultRules(slot,rarity)};
            RpgItemGenerationService.Generate(item,new(),new Low());
            Assert.Equal(power,item.Attributes.Power);Assert.Equal(stamina,item.Attributes.Stamina);
            var p=new RpgProfile();p.Inventory.Add(item);p.Equipment[slot]=item.InstanceId;
            Assert.Equal(100+stamina*5,RpgHealthService.Maximum(p,new()));
            Assert.True(CharacterStatService.Calculate(p,new(),RpgActivity.Boss).Damage>=power);
        }
    }
    [Fact]
    public void UpgradePreservesRollsEffectsAndCustomCoreValuesAndIsIdempotent()
    {
        var item=new RpgItem{Slot=EquipmentSlot.Gloves,Rarity=ItemRarity.Epic,Attributes=new(){Damage=12,Power=17},Effects=new(){new(){Id="test"}}};
        var id=item.InstanceId;Assert.True(RpgCoreStatService.Apply(item));Assert.False(RpgCoreStatService.Apply(item));
        Assert.Equal(id,item.InstanceId);Assert.Equal(12,item.Attributes.Damage);Assert.Equal(17,item.Attributes.Power);
        Assert.Equal(7,item.Attributes.Stamina);Assert.Equal("test",Assert.Single(item.Effects).Id);
        var treasure=new RpgItem();Assert.False(RpgCoreStatService.Apply(treasure));Assert.Equal(0,treasure.Attributes.Power);
    }
    [Fact]
    public void CoreStatsAreDisplayedAndDoNotDuplicateAcrossTwoSlots()
    {
        var ring=new RpgItem{Slot=EquipmentSlot.Ring1,Rarity=ItemRarity.Rare};RpgCoreStatService.Apply(ring);
        var p=new RpgProfile();p.Inventory.Add(ring);p.Equipment[EquipmentSlot.Ring1]=p.Equipment[EquipmentSlot.Ring2]=ring.InstanceId;
        var cfg=new RpgConfig();cfg.PowerDamagePerPoint=2;
        var stats=CharacterStatService.Calculate(p,cfg,RpgActivity.Boss);Assert.Equal(6,stats.Damage);Assert.Equal(120,RpgHealthService.Maximum(p,cfg.Health));
        var text=RpgChatPresentation.Bonuses(RpgChatPresentation.Snapshot(ring.Attributes));Assert.Contains("3 Kraft",text);Assert.Contains("4 Ausdauer",text);
    }
    [Fact]
    public async Task LegacyReadAndCommitUpgradePersistWithoutHealingOrChangingSecondaryRolls()
    {
        var dir=Path.Combine(Path.GetTempPath(),"RaidClipCore-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(dir);var item=new RpgItem{Slot=EquipmentSlot.Chest,Rarity=ItemRarity.Rare,Attributes=new(){BossDamage=7}};
            await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Inventory.Add(item);a.Rpg.Equipment[EquipmentSlot.Chest]=item.InstanceId;RpgHealthService.Refresh(a.Rpg,new(),DateTimeOffset.UtcNow);a.Rpg.Health.CurrentHp=50;return new(true);},default);
            var profile=(await store.GetProfileAsync("u",default)).Entry.Rpg;
            Assert.Equal(3,profile.Inventory[0].Attributes.Power);Assert.Equal(7,profile.Inventory[0].Attributes.BossDamage);
            await store.ApplyRpgAsync("u","U","save",a=>{RpgHealthService.Refresh(a.Rpg,new(),DateTimeOffset.UtcNow);return new(true);},default);
            profile=(await new ViewerPointStore(dir).GetProfileAsync("u",default)).Entry.Rpg;
            Assert.Equal(50,profile.Health.CurrentHp);Assert.Equal(120,RpgHealthService.Maximum(profile,new()));
            Assert.Equal(2,profile.Inventory[0].CoreStatsVersion);Assert.True(File.Exists(Path.Combine(dir,"viewer-points.json.pre-core-stats.bak")));
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
