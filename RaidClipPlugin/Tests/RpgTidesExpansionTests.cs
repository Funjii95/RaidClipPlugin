using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgTidesExpansionTests
{
    [Fact] public async Task PluginTimerHealsWithoutViewerRequestsAndDoesNotAccumulateActionTokens()
    {
        var c=new RpgConfig{Enabled=true};c.Health.PassiveHealing=false;var p=Equipped("coral_oracle",6,c);p.Health.CurrentHp=1;
        var root=Path.Combine(Path.GetTempPath(),"tides-tick-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(root);await store.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Rpg=p;return new(true);},default);
            var gameConfig=new MinigameConfig{Enabled=true,PointsEnabled=true};gameConfig.Adventures.Rpg=c;
            var game=new AdventureGameService(store,gameConfig,(_,_)=>Task.CompletedTask,new Low(),()=>Now.AddSeconds(10),"42");
            await game.TickAsync(default);var healed=(await store.GetProfileAsync("1001",default)).Entry.Rpg;
            Assert.True(healed.Health.CurrentHp>1);Assert.Single(healed.CompletedActions);
            var hp=healed.Health.CurrentHp;await game.TickAsync(default);Assert.Equal(hp,(await store.GetProfileAsync("1001",default)).Entry.Rpg.Health.CurrentHp);
            store=new ViewerPointStore(root);await store.TickSetHealthAsync(c,Now.AddSeconds(20),default);var next=(await store.GetProfileAsync("1001",default)).Entry.Rpg;
            Assert.True(next.Health.CurrentHp>hp);Assert.Single(next.CompletedActions);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact] public async Task All66InstancesRoundtripThroughActualFirestoreSdkAndLocalRestart()
    {
        var c=new RpgConfig();var p=Equipped("coral_oracle",6,c);
        foreach(var item in RpgTidesExpansion.Items().Where(i=>i.SetId!="coral_oracle")){RpgItemGenerationService.Generate(item,c.DiveLoot,new High());p.Inventory.Add(item);}
        RpgSetCombatService.Heal(p,c.Health,1000,Now);
        var root=Path.Combine(Path.GetTempPath(),"tides-persistence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(root);
            await store.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Rpg=p;return new(true);},default);
            var restored=(await new ViewerPointStore(root).GetProfileAsync("1001",default)).Entry.Rpg;
            Assert.Equal(JsonSerializer.Serialize(p.Inventory),JsonSerializer.Serialize(restored.Inventory));
            await using var cloud=await Backend.Tests.TransactionalFirestoreFixture.StartAsync();
            var account=JsonSerializer.Serialize(new{UserId="1001",DisplayName="SYNTHETIC",Points=1234L,Rpg=restored,UpdatedAt=Now});
            await cloud.Sink.ApplyAsync(new SnapshotProtocol.SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),Now,SnapshotProtocol.SnapshotKind.Upsert,account));
            var view=(await cloud.Reader.GetAsync("1001",default))!;
            Assert.Equal(66,view.Rpg.Inventory.Length);Assert.Equal(p.Health.SetShieldHp,view.Rpg.Health!.SetShieldHp);
            for(var n=0;n<66;n++)
            foreach(var field in typeof(RpgAttributes).GetProperties())
                Assert.Equal((decimal)field.GetValue(restored.Inventory[n].Attributes)!,view.Rpg.Inventory[n].GetProperty("Attributes").GetProperty(field.Name).GetDecimal());
            Assert.Equal(p.Cooldowns["set:coral-damage"],view.Rpg.Cooldowns["set:coral-damage"]);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
    private sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
    private static RpgProfile Equipped(string id,int count,RpgConfig c)
    {
        var p=new RpgProfile();
        foreach(var i in RpgTidesExpansion.Items().Where(i=>i.SetId==id).Take(count))
        {
            RpgItemGenerationService.Generate(i,c.DiveLoot,new Low());p.Inventory.Add(i);
            var slot=i.Slot!.Value;if(p.Equipment.ContainsKey(slot))slot=EquipmentSlot.Ring2;p.Equipment[slot]=i.InstanceId;
        }
        RpgHealthService.Refresh(p,c.Health,Now);return p;
    }
    [Fact] public void CatalogContains66UniqueDefinitionsAndExactlySixRealSets()
    {
        var c=new RpgConfig();c.Validate();var items=RpgTidesExpansion.Items();
        Assert.Equal(66,items.Count);Assert.Equal(66,items.Select(i=>i.ItemId).Distinct().Count());
        Assert.Equal(6,RpgTidesExpansion.Sets().Count);
        Assert.All(items,i=>{Assert.Contains(i.ItemId,c.DiveLoot.Items.Select(x=>x.ItemId));Assert.Contains(i.Slot,new EquipmentSlot?[]{EquipmentSlot.Helmet,EquipmentSlot.Chest,EquipmentSlot.Gloves,EquipmentSlot.Boots,EquipmentSlot.Ring1,EquipmentSlot.Amulet});});
        Assert.Equal(24,items.Count(i=>i.SetId.Length>0));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void EveryRolledValueIsBoundedAndSurvivesSerialization(bool high)
    {
        var c=new RpgConfig();IDuelRandom rng=high?new High():new Low();
        foreach(var definition in RpgTidesExpansion.Items())
        {
            var rules=definition.Generation!;var instance=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;
            RpgItemGenerationService.Generate(instance,c.DiveLoot,rng);
            Assert.Null(instance.Generation);Assert.Equal(1,instance.CoreStatsVersion);
            foreach(var range in rules.BaseAttributes)Assert.InRange((decimal)typeof(RpgAttributes).GetProperty(range.Attribute)!.GetValue(instance.Attributes)!,range.Minimum,range.Maximum);
            var secondary=rules.Attributes.Where(a=>(decimal)typeof(RpgAttributes).GetProperty(a.Attribute)!.GetValue(instance.Attributes)!>0).ToList();
            Assert.InRange(secondary.Count,rules.MinimumAttributes!.Value,rules.MaximumAttributes!.Value);
            foreach(var range in secondary)Assert.InRange((decimal)typeof(RpgAttributes).GetProperty(range.Attribute)!.GetValue(instance.Attributes)!,range.Minimum,range.Maximum);
            var json=JsonSerializer.Serialize(instance);var restored=JsonSerializer.Deserialize<RpgItem>(json)!;
            RpgCoreStatService.Apply(restored);Assert.Equal(json,JsonSerializer.Serialize(restored));
        }
    }
    [Theory][InlineData("tide_king_regalia",6)][InlineData("abyss_hunter",6)][InlineData("coral_oracle",6)][InlineData("current_twins",2)][InlineData("dark_pearl_twins",2)][InlineData("moon_tide_jewelry",2)]
    public void AllSetsActivateAndDeactivate(string id,int count)
    {
        var c=new RpgConfig();var p=Equipped(id,count,c);
        Assert.All(c.Sets.Single(s=>s.Id==id).Bonuses,b=>Assert.True(RpgSetCombatService.Active(p,c.Sets,id,b.RequiredPieces)));
        p.Equipment.Clear();RpgSetCombatService.Refresh(p,c.Health,Now);
        Assert.False(RpgSetCombatService.Active(p,c.Sets,id,2));Assert.Equal(0,p.Health.SetShieldHp);
    }
    [Fact] public void IdenticalRingsDoNotCountTwiceAndBothJewelrySlotsWork()
    {
        var c=new RpgConfig();var p=Equipped("current_twins",2,c);Assert.True(p.Equipment.ContainsKey(EquipmentSlot.Ring2));
        p.Inventory[1].ItemId=p.Inventory[0].ItemId;Assert.False(RpgSetCombatService.Active(p,c.Sets,"current_twins",2));
        p=Equipped("moon_tide_jewelry",2,c);Assert.True(p.Equipment.ContainsKey(EquipmentSlot.Amulet));
    }
    [Fact] public void WaveAndFinHaveIndependentCooldownsAndCannotRecurse()
    {
        var c=new RpgConfig();var p=Equipped("tide_king_regalia",4,c);
        Assert.Equal(160,RpgSetCombatService.ProcDamage(100,true,1,p,c,Now));
        Assert.Equal(0,RpgSetCombatService.ProcDamage(100,true,1,p,c,Now.AddSeconds(7)));
        Assert.Equal(160,RpgSetCombatService.ProcDamage(100,true,1,p,c,Now.AddSeconds(8)));
        p=Equipped("abyss_hunter",4,c);Assert.Equal(0,RpgSetCombatService.ProcDamage(100,true,2,p,c,Now));
        Assert.Equal(170,RpgSetCombatService.ProcDamage(100,false,1,p,c,Now));Assert.Equal(0,RpgSetCombatService.ProcDamage(100,false,1,p,c,Now));
    }
    [Fact] public void OverhealShieldAndDamageBuffDoNotStackAndClearOnUnequip()
    {
        var c=new RpgConfig();var p=Equipped("coral_oracle",6,c);var max=RpgHealthService.Maximum(p,c.Health);
        RpgSetCombatService.Heal(p,c.Health,max,Now);Assert.Equal((int)(max*0.18m),p.Health.SetShieldHp);
        var end=p.Cooldowns["set:coral-damage"];RpgSetCombatService.Heal(p,c.Health,max,Now.AddSeconds(1));
        Assert.Equal(end,p.Cooldowns["set:coral-damage"]);
        var json=JsonSerializer.Serialize(p);var restored=JsonSerializer.Deserialize<RpgProfile>(json)!;Assert.Equal(p.Health.SetShieldHp,restored.Health.SetShieldHp);
        restored.Equipment.Clear();RpgSetCombatService.Refresh(restored,c.Health,Now.AddSeconds(2));Assert.Equal(0,restored.Health.SetShieldHp);Assert.DoesNotContain("set:coral-damage",restored.Cooldowns.Keys);
    }
    [Fact] public void MoonShieldTimerSurvivesRestartWithoutDuplicateGrant()
    {
        var c=new RpgConfig();var p=Equipped("moon_tide_jewelry",2,c);Assert.Equal(0,p.Health.SetShieldHp);
        p=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(p))!;
        RpgSetCombatService.Refresh(p,c.Health,Now.AddSeconds(15));var shield=p.Health.SetShieldHp;Assert.True(shield>0);
        p.Health.SetShieldHp=0;RpgSetCombatService.Refresh(p,c.Health,Now.AddSeconds(15));Assert.Equal(0,p.Health.SetShieldHp);
    }
    [Fact] public void HasteAndRegenAreAuthoritativeAndBounded()
    {
        var c=new RpgConfig();var p=Equipped("abyss_hunter",6,c);var before=RpgSetCombatService.AttackSeconds(p,c,10,Now);
        RpgSetCombatService.Defense(0,p,c,Now,true);Assert.True(RpgSetCombatService.AttackSeconds(p,c,10,Now)<before);
        Assert.Equal(before,RpgSetCombatService.AttackSeconds(p,c,10,Now.AddSeconds(8)));
        p=Equipped("tide_king_regalia",6,c);p.Health.CurrentHp=1;RpgSetCombatService.Defense(1,p,c,Now,false);
        RpgSetCombatService.Refresh(p,c.Health,Now.AddSeconds(9));var healed=p.Health.CurrentHp;Assert.True(healed>1);
        RpgSetCombatService.Refresh(p,c.Health,Now.AddSeconds(10));Assert.Equal(healed,p.Health.CurrentHp);
    }
    [Fact] public void ExistingDefinitionsAndInstancesAreNotReplaced()
    {
        var c=new RpgConfig();var old=c.DiveLoot.Items.First();old.DropWeight=23;var count=c.DiveLoot.Items.Count;
        RpgTidesExpansion.AddMissing(c);RpgTidesExpansion.AddMissing(c);Assert.Same(old,c.DiveLoot.Items.First());Assert.Equal(23,old.DropWeight);Assert.Equal(count,c.DiveLoot.Items.Count);
        var p=JsonSerializer.Deserialize<RpgProfile>("{\"Inventory\":[{\"ItemId\":\"old\",\"Attributes\":{\"Damage\":7}}]}")!;
        Assert.Equal(7,p.Inventory[0].Attributes.Damage);Assert.Equal(0,p.Health.SetShieldHp);
    }
}
