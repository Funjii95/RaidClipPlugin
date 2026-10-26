using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgCombatPowerTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 [Fact] public void Gear_buffs_and_roles_affect_assignment_and_extreme_single_stats_are_bounded()
 {
  var c=new RpgConfig{DamageBonusCap=1000};var now=DateTimeOffset.UtcNow;var weak=new RpgProfile{PlayerLevel=20};var strong=new RpgProfile{PlayerLevel=20};var item=new RpgItem{ItemId="strong",Slot=EquipmentSlot.Weapon,Attributes=new(){Attack=1000,Armor=400,MaximumHp=1000,CritChance=30,CritDamage=100}};strong.Inventory.Add(item);strong.Equipment[EquipmentSlot.Weapon]=item.InstanceId;
  var a=RpgCombatPowerService.Choose(weak,c,c.AreaBosses.Bosses,now);var b=RpgCombatPowerService.Choose(strong,c,c.AreaBosses.Bosses,now);Assert.True(b.Level>a.Level);
  var before=RpgCombatPowerService.Evaluate(strong,c,now).Rating;strong.Buffs.Add(new(){AllActivities=true,Category=SushiCategory.Offensive,RemainingUses=1,ActivatedAt=now,Attributes=new(){Damage=30}});Assert.True(RpgCombatPowerService.Evaluate(strong,c,now).Rating>before);
  strong.Role=RpgRole.Tank;var tank=RpgCombatPowerService.Evaluate(strong,c,now);strong.Role=RpgRole.Damage;Assert.NotEqual(tank.Rating,RpgCombatPowerService.Evaluate(strong,c,now).Rating);
  item.Attributes=new(){Armor=1000000,MaximumHp=1000000};var extreme=RpgCombatPowerService.Choose(strong,c,c.AreaBosses.Bosses,now);Assert.True(extreme.Level<100);
 }
 [Fact] public async Task Global_completion_does_not_kill_an_unbeaten_personal_boss_or_grant_its_items()
 {
  var now=DateTimeOffset.UtcNow;var points=new ViewerPointStore(root);var config=new MinigameConfig{Enabled=true,PointsEnabled=true};var c=config.Adventures.Rpg;c.Enabled=true;c.AreaBosses.Enabled=true;c.Health.Counterattacks=false;config.Adventures.Boss.AttackCooldownSeconds=1;c.AreaBosses.Bosses.ForEach(b=>b.ItemChance=100);
  await points.AddPointsAsync("1","Weak",100,0,default);await points.ApplyRpgAsync("2","Strong","seed",a=>{var item=new RpgItem{ItemId="strong",CoreStatsVersion=3,Slot=EquipmentSlot.Weapon,Attributes=new(){Attack=1000000}};a.Rpg.Inventory.Add(item);a.Rpg.Equipment[EquipmentSlot.Weapon]=item.InstanceId;return new(true);},default);
  var game=new AdventureGameService(points,config,(_,_)=>Task.CompletedTask,new Low(),()=>now,"42");await game.HandleAsync(new(){UserId="42",UserName="Owner",IsBroadcaster=true},"!start boss",["!start boss"],default);
  Assert.True((await game.ExecuteBossAttackAsync("1","Weak","first",default)).Success);Assert.True((await game.ExecuteBossAttackAsync("2","Strong","last",default)).Success);
  var state=(await points.ReadGlobalBossAsync(default))!;Assert.True(state.RewardsSettled);Assert.True(state.Participants["1"].PersonalRemainingHp>0);Assert.False(state.Participants["1"].PersonalVictory);Assert.True(state.Participants["2"].PersonalVictory);
  var weak=(await points.GetProfileAsync("1",default)).Entry;Assert.Empty(weak.Rpg.Inventory);Assert.Equal(0,weak.Rpg.Statistics.Counters.GetValueOrDefault("bossKills"));Assert.Equal("participation",Assert.Single(weak.Rpg.CombatRewards).Kind);var winner=(await points.GetProfileAsync("2",default)).Entry;Assert.Equal("boss",Assert.Single(winner.Rpg.CombatRewards).Kind);Assert.NotEmpty(winner.Rpg.CombatRewards[0].Items);
 }
}

