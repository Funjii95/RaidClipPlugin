using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RpgDiveSeriesTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));private DateTimeOffset now=DateTimeOffset.UtcNow;
 private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
 private sealed class FixedRandom(int value):IDuelRandom{public int NextInclusive(int min,int max)=>Math.Clamp(value,min,max);}
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 private async Task<ViewerPointStore> Seed(){config.Adventures.Rpg.Enabled=true;config.Adventures.Treasure.Enabled=true;config.Adventures.Rpg.Health.BaseHp=1000;var points=new ViewerPointStore(root);await points.ApplyRpgAsync("1","Otter","seed",a=>{a.Points=0;RpgHealthService.Refresh(a.Rpg,config.Adventures.Rpg.Health,now);return new(true);},default);return points;}
 [Theory][InlineData(10)][InlineData(20)][InlineData(100)]
 public async Task Series_honors_cooldown_survives_restart_and_awards_each_dive_once(int count){
  var points=await Seed();var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);Assert.True((await service.StartAsync("1","Otter","start",count,default)).Success);
  Assert.Equal(1,await service.TickAsync(default));var a=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal(1,a.Rpg.DiveSeries!.Completed);Assert.Equal(0,await service.TickAsync(default));
  Assert.False((await new RpgAdventureService(points,config,new FixedRandom(999999),()=>now).ExecuteAsync("1","Otter","manual",RpgActivity.Dive,0,default)).Success);
  points=new(root);service=new(points,config,new FixedRandom(999999),()=>now);
  for(var n=1;n<count;n++){now=now.AddHours(1);Assert.Equal(1,await service.TickAsync(default));}
  a=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal("completed",a.Rpg.DiveSeries!.Status);Assert.Equal(count,a.Rpg.DiveSeries.Completed);Assert.Equal(count*5,a.Points);Assert.Equal(a.Points,a.Rpg.DiveSeries.Pearls);Assert.Equal(count,a.Rpg.Statistics.Counters["dives"]);Assert.Equal(0,await service.TickAsync(default));
  var projected=SnapshotValidation.ValidateLocalSource(new(1,"1","1",1,Guid.NewGuid(),now,SnapshotKind.Upsert,JsonSerializer.Serialize(a)),"1");Assert.Contains("DiveSeries",projected.AccountJson);
 }
 [Fact] public async Task Fatal_dive_stops_at_zero_without_revival_and_keeps_earned_rewards(){
  var points=await Seed();var zone=config.Adventures.Rpg.Zones.Get("sunny_lagoon");zone.MimicChance=0;zone.EnemyChance=100;zone.ChestChance=0;zone.Hp=100000;zone.Attack=100000;
  var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);zone.EnemyChance=0;zone.ChestChance=0;Assert.True((await service.StartAsync("1","Otter","start",100,default)).Success);await service.TickAsync(default);
  var before=(await points.GetProfileAsync("1",default)).Entry;zone.EnemyChance=100;now=now.AddHours(1);await service.TickAsync(default);var dead=(await points.GetProfileAsync("1",default)).Entry;
  Assert.Equal(0,dead.Rpg.Health.CurrentHp);Assert.Equal("defeated",dead.Rpg.DiveSeries!.Status);Assert.Equal(2,dead.Rpg.DiveSeries.Completed);Assert.True(dead.Rpg.DiveSeries.Damage>0);Assert.Equal(before.Points,dead.Points);Assert.Equal(dead.Points,dead.Rpg.DiveSeries.Pearls);
  points=new(root);now=now.AddDays(1);Assert.Equal(0,await new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now).TickAsync(default));Assert.Equal(0,(await points.GetProfileAsync("1",default)).Entry.Rpg.Health.CurrentHp);
 }
 [Fact] public async Task Stop_and_zone_change_are_safe_and_counts_are_allowlisted(){
  var points=await Seed();var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);await Assert.ThrowsAsync<ArgumentException>(()=>service.StartAsync("1","Otter","bad",1000,default));
  await service.StartAsync("1","Otter","start",10,default);Assert.False((await RpgZoneService.SelectAsync(points,config.Adventures.Rpg,"1","Otter","zone","dive","abyss",default)).Success);await service.TickAsync(default);await service.StopAsync("1","Otter","stop",default);now=now.AddHours(1);Assert.Equal(0,await service.TickAsync(default));var a=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal("stopped",a.Rpg.DiveSeries!.Status);Assert.Equal(5,a.Points);
 }
 [Fact] public async Task Successful_series_reports_real_loot_materials_and_experience_without_double_awards(){
  var points=await Seed();var c=config.Adventures.Rpg;var zone=c.Zones.Get("sunny_lagoon");zone.EnemyChance=100;zone.MimicChance=zone.ChestChance=zone.EliteChance=0;zone.Hp=1;c.EarlyLegendaries.Enabled=false;c.BossLootChance=100;c.DiveLoot.MaximumDropChance=100;
  var service=new RpgDiveSeriesService(points,config,new FixedRandom(0),()=>now);await service.StartAsync("1","Otter","start",10,default);
  for(var n=0;n<10;n++){now=now.AddHours(1);Assert.Equal(1,await service.TickAsync(default));}
  var a=(await points.GetProfileAsync("1",default)).Entry;var s=a.Rpg.DiveSeries!;Assert.Equal(3000,s.Experience);Assert.Equal(a.Rpg.Experience,s.Experience);Assert.Equal(200,s.Pearls);Assert.Equal(a.Rpg.Inventory.Count,s.Items.Sum(i=>i.Quantity));Assert.NotEmpty(s.Items);Assert.NotEmpty(s.Materials);
  foreach(var material in s.Materials)Assert.Equal(material.Value,RpgUpgradeCatalog.Count(a.Rpg.Materials,material.Key));
  var before=JsonSerializer.Serialize(a.Rpg.DiveSeries);await new RpgAdventureService(points,config,new FixedRandom(0),()=>now).ExecuteAsync("1","Otter","series:"+s.Id+":0",RpgActivity.Dive,0,default,s.Id,0);Assert.Equal(before,JsonSerializer.Serialize((await points.GetProfileAsync("1",default)).Entry.Rpg.DiveSeries));
 }
 [Fact] public void Contract_rejects_forged_count_and_unknown_operations(){
  Assert.NotEmpty(RpgIntents.Canonical(new(Guid.NewGuid(),"diveSeries",JsonSerializer.SerializeToElement(new{operation="start",count="100"}))));
  foreach(var args in new[]{new{operation="start",count="101"},new{operation="attack",count="100"}})Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(new(Guid.NewGuid(),"diveSeries",JsonSerializer.SerializeToElement(args))));
 }
 [Fact] public void Lagoon_has_more_experience_and_materials_without_changing_other_biomes(){
  var c=config.Adventures.Rpg;var p=new RpgProfile();var z=c.Zones.Get("sunny_lagoon");var other=c.Zones.Get("sunken_temples");var baseline=RpgZoneService.MaterialChance(p,c,other,"blue_pearl","enemy");Assert.True(RpgZoneService.MaterialChance(p,c,z,"white_pearl","enemy")>=10);Assert.Equal(baseline,RpgZoneService.MaterialChance(p,c,other,"blue_pearl","enemy"));
 }
}
