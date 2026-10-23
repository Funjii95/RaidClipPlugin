using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgSetTests
{
    private sealed class Low : IDuelRandom { public int NextInclusive(int min, int max) => min; }
    private static RpgProfile Wearing(string set, int count)
    {
        var p = new RpgProfile();
        foreach (var item in RpgEquipmentCatalog.Items().Where(i => i.SetId == set).Take(count))
        { item.Attributes = new(); item.Generation = null; p.Inventory.Add(item); p.Equipment[item.Slot!.Value] = item.InstanceId; }
        return p;
    }
    [Theory]
    [InlineData(2,5,0,0)] [InlineData(3,5,3,0)] [InlineData(4,5,3,5)]
    public void GuardianBonusesAreCumulativeAndRemovalUpdatesImmediately(int count, decimal boss, decimal crit, decimal doubleChance)
    {
        var p = Wearing("deepsea-guardian",count); var c = new RpgConfig();
        var s = CharacterStatService.Calculate(p,c,RpgActivity.Boss);
        Assert.Equal(boss,s.BossDamage); Assert.Equal(crit,s.CritChance); Assert.Equal(doubleChance,s.DoubleAttackChance);
        p.Equipment.Clear(); Assert.Equal(0,CharacterStatService.Calculate(p,c,RpgActivity.Boss).BossDamage);
    }
    [Fact]
    public void DuplicateDefinitionsDoNotCountTwiceAndSetsCanMix()
    {
        var p = Wearing("deepsea-guardian",2); var c = new RpgConfig();
        var duplicate = JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(p.Inventory[0]))!;
        duplicate.InstanceId = "duplicate"; p.Inventory.Add(duplicate); p.Equipment[EquipmentSlot.Ring1] = duplicate.InstanceId;
        Assert.Equal(2,RpgSetService.Progress(p,c).First().Equipped);
        var ocean = Wearing("ocean-treasure-hunter",4);
        foreach (var item in ocean.Inventory.Skip(2)) { p.Inventory.Add(item); p.Equipment[item.Slot!.Value] = item.InstanceId; }
        var s = CharacterStatService.Calculate(p,c,RpgActivity.Dive);
        Assert.Equal(5,s.BossDamage); Assert.Equal(5,s.DiveLuck);
        Assert.Equal(new[] {2,2,0,0},RpgSetService.Progress(p,c).Take(4).Select(x=>x.Equipped));
        Assert.All(RpgSetService.Progress(p,c).Skip(4),x=>Assert.Equal(0,x.Equipped));
    }
    [Fact]
    public void TreasurePearlsAndDoubleAttacksShareExistingCaps()
    {
        var c = new RpgConfig(); var p = Wearing("ocean-treasure-hunter",4);
        p.Inventory[0].Attributes.PearlBonus = 14;
        Assert.Equal(15,CharacterStatService.Calculate(p,c,RpgActivity.Dive).PearlBonus);
        Assert.Equal(100,RpgPassiveService.RewardPayout(100,100,15));
        p = Wearing("deepsea-guardian",4); p.Inventory[0].Attributes.DoubleAttackChance = 19;
        Assert.Equal(20,CharacterStatService.Calculate(p,c,RpgActivity.Boss).DoubleAttackChance);
    }
    [Fact]
    public void LegendaryEffectsHaveExactStrengthAndCriticalCondition()
    {
        var c = new RpgConfig(); var p = new RpgProfile();
        var ring = RpgEquipmentCatalog.Items().Single(i=>i.ItemId=="tidal-seal");
        p.Inventory.Add(ring); p.Equipment[EquipmentSlot.Ring2] = ring.InstanceId;
        Assert.Equal(0,RpgPassiveService.TryProc(p,c,RpgActivity.Boss,RpgEffectTrigger.DoubleAttack,new Low(),DateTimeOffset.UtcNow));
        Assert.Equal(0.5m,RpgPassiveService.TryProc(p,c,RpgActivity.Boss,RpgEffectTrigger.DoubleAttack,new Low(),DateTimeOffset.UtcNow,critical:true));
        var trident = RpgEquipmentCatalog.Items().Single(i=>i.ItemId=="leviathan-trident");
        p.Inventory.Add(trident); p.Equipment[EquipmentSlot.Weapon] = trident.InstanceId;
        var hit = RpgPassiveService.BossAttack(100,p,c,new Low(),DateTimeOffset.UtcNow);
        Assert.Equal(2,hit.ExtraHits);
        var regular = CharacterStatService.BossDamage(100,CharacterStatService.Calculate(p,c,RpgActivity.Boss),0,out _);
        Assert.Equal(regular+2*(int)decimal.Floor(regular*0.35m),hit.Damage);
    }
    [Fact]
    public void BossLegendaryGateIsIndependentAndRequiresExplicitOptIn()
    {
        var c = new RpgConfig(); Assert.Equal(ItemRarity.Epic,RpgBossLootService.MaximumRarity(c,10000,20));
        c.BossLegendaryEnabled = true;
        Assert.Equal(ItemRarity.Epic,RpgBossLootService.MaximumRarity(c,999,3));
        Assert.Equal(ItemRarity.Epic,RpgBossLootService.MaximumRarity(c,1000,2));
        Assert.Equal(ItemRarity.Legendary,RpgBossLootService.MaximumRarity(c,1000,3));
        c.Validate();
    }
    [Fact]
    public async Task SetPersistenceChatAndOldItemsRemainCompatible()
    {
        var dir = Path.Combine(Path.GetTempPath(),"RaidClipSets-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ViewerPointStore(dir); var c = new RpgConfig { Enabled = true };
            await store.ApplyRpgAsync("42","Otter","seed",a=> { a.Rpg=Wearing("deepsea-guardian",3); a.Rpg.Inventory.Add(new() { InstanceId="legacy" }); return new(true); },default);
            store = new(dir);
            var p = (await store.GetProfileAsync("42",default)).Entry.Rpg;
            Assert.Equal(3,RpgSetService.Progress(p,c).First().Equipped); Assert.Equal("",p.Inventory.Last().SetId);
            var chat = new RpgChatCommand(store,c);
            var pages = new List<string>(); for(var i=1;i<6;i++) pages.Add(await chat.ExecuteAsync(new() { UserId="42",UserName="Otter" },"!sets",new[] {"!sets",i.ToString()},default));
            Assert.Contains("Aktiv",string.Join(" ",pages)); Assert.Contains("Noch nicht freigeschaltet",string.Join(" ",pages));
            await new RpgProgressionService(store,c).UnequipAsync("42","Otter","remove",EquipmentSlot.Helmet,default);
            Assert.Equal(0,CharacterStatService.Calculate((await store.GetProfileAsync("42",default)).Entry.Rpg,c,RpgActivity.Boss).CritChance);
        }
        finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }
}
