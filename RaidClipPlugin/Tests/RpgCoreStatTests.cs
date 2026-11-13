using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgCoreStatTests
{
    [Theory]
    [InlineData(ItemRarity.Common)] [InlineData(ItemRarity.Legendary)] [InlineData(ItemRarity.Eternal)]
    public void WeaponsAlwaysHaveDamageIncludingPreviouslyMigratedInventory(ItemRarity rarity)
    {
        var item=new RpgItem{Slot=EquipmentSlot.Weapon,Rarity=rarity,CoreStatsVersion=3,UpgradeLevel=7,Attributes=new(){Power=42}};
        var id=item.InstanceId;Assert.True(RpgCoreStatService.Apply(item));Assert.True(item.Attributes.Attack>0);
        Assert.Equal(42,item.Attributes.Power);Assert.Equal(7,item.UpgradeLevel);Assert.Equal(id,item.InstanceId);
        var attack=item.Attributes.Attack;Assert.False(RpgCoreStatService.Apply(item));Assert.Equal(attack,item.Attributes.Attack);
        item.Attributes.Attack=320;Assert.False(RpgCoreStatService.Apply(item));Assert.Equal(320,item.Attributes.Attack);
    }
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
            Assert.Equal(power*1.5m,item.Attributes.Power);Assert.Equal(stamina*1.5m,item.Attributes.Stamina);
            var p=new RpgProfile();p.Inventory.Add(item);p.Equipment[slot]=item.InstanceId;
            Assert.Equal((int)(100+stamina*1.5m*5),RpgHealthService.Maximum(p,new()));
            Assert.True(CharacterStatService.Calculate(p,new(),RpgActivity.Boss).Damage>=power);
        }
    }
    [Fact]
    public void UpgradePreservesRollsEffectsAndCustomCoreValuesAndIsIdempotent()
    {
        var item=new RpgItem{Slot=EquipmentSlot.Gloves,Rarity=ItemRarity.Epic,Attributes=new(){Damage=12,Power=17},Effects=new(){new(){Id="test"}}};
        var id=item.InstanceId;Assert.True(RpgCoreStatService.Apply(item));Assert.False(RpgCoreStatService.Apply(item));
        Assert.Equal(id,item.InstanceId);Assert.Equal(12,item.Attributes.Damage);Assert.Equal(25.5m,item.Attributes.Power);
        Assert.Equal(10.5m,item.Attributes.Stamina);Assert.Equal("test",Assert.Single(item.Effects).Id);
        var treasure=new RpgItem();Assert.False(RpgCoreStatService.Apply(treasure));Assert.Equal(0,treasure.Attributes.Power);
    }
    [Theory]
    [InlineData(EquipmentSlot.Helmet)] [InlineData(EquipmentSlot.Chest)]
    [InlineData(EquipmentSlot.Gloves)] [InlineData(EquipmentSlot.Boots)]
    public void CorrectsPreviouslyAddedPercentageWithoutRerollingOrCompounding(EquipmentSlot slot)
    {
        var item=new RpgItem{Slot=slot,CoreStatsVersion=2,UpgradeLevel=9,Attributes=new(){Armor=42,ArmorPercent=62,Power=17,Stamina=16,CritChance=7}};
        var id=item.InstanceId;RpgCoreStatService.Apply(item);
        Assert.Equal(63,item.Attributes.Armor);Assert.Equal(12,item.Attributes.ArmorPercent);
        Assert.Equal(25.5m,item.Attributes.Power);Assert.Equal(7,item.Attributes.CritChance);Assert.Equal(id,item.InstanceId);Assert.Equal(9,item.UpgradeLevel);
        Assert.Equal(91.35m,RpgEquipmentUpgradeRules.Effective(item).Armor);
        Assert.False(RpgCoreStatService.Apply(item));Assert.Equal(63,item.Attributes.Armor);
    }
    [Theory] [InlineData(1,0)] [InlineData(2,5)] [InlineData(37,180)] [InlineData(100,495)]
    public void LevelStatsAreRetroactiveAndNeverAccumulateAcrossReads(int level,int bonus)
    {
        var profile=new RpgProfile{PlayerLevel=level};var config=new RpgConfig();
        for(var n=0;n<3;n++){var stats=CharacterStatService.Calculate(profile,config,RpgActivity.Boss);Assert.Equal(bonus,stats.Power);Assert.Equal(bonus,stats.Stamina);Assert.Equal(100+(level-1)*27,RpgHealthService.Maximum(profile,config.Health));}
        Assert.Empty(profile.Inventory);
    }
    [Fact] public void ArmorOnAccessoriesIsAlsoIncreasedAndLegitimatePercentageIsPreserved()
    {
        var item=new RpgItem{Slot=EquipmentSlot.Ring1,CoreStatsVersion=1,Attributes=new(){Armor=10,ArmorPercent=9}};
        RpgCoreStatService.Apply(item);Assert.Equal(15,item.Attributes.Armor);Assert.Equal(9,item.Attributes.ArmorPercent);
    }
    [Fact]
    public void CoreStatsAreDisplayedAndDoNotDuplicateAcrossTwoSlots()
    {
        var ring=new RpgItem{Slot=EquipmentSlot.Ring1,Rarity=ItemRarity.Rare};RpgCoreStatService.Apply(ring);
        var p=new RpgProfile();p.Inventory.Add(ring);p.Equipment[EquipmentSlot.Ring1]=p.Equipment[EquipmentSlot.Ring2]=ring.InstanceId;
        var cfg=new RpgConfig();cfg.PowerDamagePerPoint=2;
        var stats=CharacterStatService.Calculate(p,cfg,RpgActivity.Boss);Assert.Equal(9,stats.Damage);Assert.Equal(130,RpgHealthService.Maximum(p,cfg.Health));
        var text=RpgChatPresentation.Bonuses(RpgChatPresentation.Snapshot(ring.Attributes));Assert.Contains("4,5 Kraft",text);Assert.Contains("6 Ausdauer",text);
    }
    [Fact] public async Task StartupMigrationPersistsAllGearOnceAndPreservesWoundsAndCurrency()
    {
        var dir=Path.Combine(Path.GetTempPath(),"otter-stat-migration-"+Guid.NewGuid().ToString("N"));
        try{
            var store=new ViewerPointStore(dir);var health=new RpgHealthConfig{PassiveHealing=false};var now=DateTimeOffset.UtcNow;
            await store.ApplyRpgAsync("42","Owner","seed",a=>{a.Points=123;a.Rpg.PlayerLevel=10;a.Rpg.Health.CurrentHp=50;a.Rpg.Health.AppliedBaseMaximumHp=118;a.Rpg.Inventory.Add(new(){Slot=EquipmentSlot.Chest,CoreStatsVersion=2,Attributes=new(){Armor=42,ArmorPercent=62,Power=17,Stamina=16}});return new(true);},default);
            Assert.Equal(1,await store.UpgradeEquipmentCoreStatsAsync(health,default));
            Assert.Equal(0,await store.UpgradeEquipmentCoreStatsAsync(health,default));
            var profile=(await new ViewerPointStore(dir).GetProfileAsync("42",default)).Entry;
            Assert.Equal(123,profile.Points);Assert.Equal(3,profile.Rpg.Inventory[0].CoreStatsVersion);Assert.Equal(63,profile.Rpg.Inventory[0].Attributes.Armor);
            RpgHealthService.Refresh(profile.Rpg,health,now);Assert.Equal(50,profile.Rpg.Health.CurrentHp);
            Assert.True(File.Exists(Path.Combine(dir,"viewer-points.json.pre-armor-correction.bak")));
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
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
            Assert.Equal(4.5m,profile.Inventory[0].Attributes.Power);Assert.Equal(7,profile.Inventory[0].Attributes.BossDamage);
            await store.ApplyRpgAsync("u","U","save",a=>{RpgHealthService.Refresh(a.Rpg,new(),DateTimeOffset.UtcNow);return new(true);},default);
            profile=(await new ViewerPointStore(dir).GetProfileAsync("u",default)).Entry.Rpg;
            Assert.Equal(50,profile.Health.CurrentHp);Assert.Equal(130,RpgHealthService.Maximum(profile,new()));
            Assert.Equal(3,profile.Inventory[0].CoreStatsVersion);Assert.True(File.Exists(Path.Combine(dir,"viewer-points.json.pre-core-stats.bak")));
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
