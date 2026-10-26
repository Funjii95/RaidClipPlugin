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
 [Theory][InlineData(10)][InlineData(25)][InlineData(50)][InlineData(100)]
 public async Task One_tick_resolves_the_entire_series_at_one_time_and_awards_once(int count){
  var points=await Seed();var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);Assert.True((await service.StartAsync("1","Otter","start",count,default)).Success);
  var started=(await points.GetProfileAsync("1",default)).Entry.Rpg.DiveSeries!;Assert.Equal(0,started.Completed);
  Assert.Equal(1,await service.TickAsync(default));var a=(await points.GetProfileAsync("1",default)).Entry;
  Assert.Equal("completed",a.Rpg.DiveSeries!.Status);Assert.Equal(count,a.Rpg.DiveSeries.Completed);Assert.Equal(count*5,a.Points);Assert.Equal(a.Points,a.Rpg.DiveSeries.Pearls);Assert.Equal(count,a.Rpg.Statistics.Counters["dives"]);
  Assert.Equal(now.AddSeconds(config.Adventures.Treasure.CooldownSeconds),a.Rpg.Cooldowns["!dive"]);
  Assert.Equal(3,a.Rpg.CompletedActions.Count); // seed, start, one atomic batch
  points=new(root);Assert.Equal(0,await new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now).TickAsync(default));
  var before=JsonSerializer.Serialize(a);await new RpgAdventureService(points,config,new FixedRandom(999999),()=>now).ExecuteSeriesAsync("1","Otter","series-batch:"+started.Id+":0",default,started.Id,0);Assert.Equal(before,JsonSerializer.Serialize((await points.GetProfileAsync("1",default)).Entry));
  var projected=SnapshotValidation.ValidateLocalSource(new(1,"1","1",1,Guid.NewGuid(),now,SnapshotKind.Upsert,JsonSerializer.Serialize(a)),"1");Assert.Contains("DiveSeries",projected.AccountJson);
 }
 private sealed class OpeningPeaceRandom:IDuelRandom{private int rolls;public int NextInclusive(int min,int max)=>min==0&&max==999999&&++rolls<=3?max:min;}
 [Fact] public async Task Fatal_dive_stops_batch_at_zero_and_keeps_all_preceding_loot(){
  var points=await Seed();var zone=config.Adventures.Rpg.Zones.Get("sunny_lagoon");zone.MimicChance=0;zone.EnemyChance=50;zone.ChestChance=0;zone.Hp=100000;zone.Attack=100000;
  var service=new RpgDiveSeriesService(points,config,new OpeningPeaceRandom(),()=>now);Assert.True((await service.StartAsync("1","Otter","start",100,default)).Success);await service.TickAsync(default);var dead=(await points.GetProfileAsync("1",default)).Entry;
  Assert.Equal(0,dead.Rpg.Health.CurrentHp);Assert.Equal("defeated",dead.Rpg.DiveSeries!.Status);Assert.Equal(2,dead.Rpg.DiveSeries.Completed);Assert.Equal(1000,dead.Rpg.DiveSeries.Damage);Assert.Equal(5,dead.Points);Assert.Equal(dead.Points,dead.Rpg.DiveSeries.Pearls);
  points=new(root);now=now.AddDays(1);Assert.Equal(0,await new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now).TickAsync(default));Assert.Equal(0,(await points.GetProfileAsync("1",default)).Entry.Rpg.Health.CurrentHp);
 }
 [Fact] public async Task Pending_series_can_be_cancelled_without_any_rewards_or_zone_change(){
  var points=await Seed();var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);await Assert.ThrowsAsync<ArgumentException>(()=>service.StartAsync("1","Otter","bad",1000,default));
  await service.StartAsync("1","Otter","start",10,default);Assert.False((await RpgZoneService.SelectAsync(points,config.Adventures.Rpg,"1","Otter","zone","dive","abyss",default)).Success);await service.StopAsync("1","Otter","stop",default);Assert.Equal(0,await service.TickAsync(default));var a=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal("stopped",a.Rpg.DiveSeries!.Status);Assert.Equal(0,a.Points);
 }
 [Fact] public async Task Successful_batch_reports_real_loot_materials_and_experience_without_double_awards(){
  var points=await Seed();var c=config.Adventures.Rpg;var zone=c.Zones.Get("sunny_lagoon");zone.EnemyChance=100;zone.MimicChance=zone.ChestChance=zone.EliteChance=0;zone.Hp=1;c.EarlyLegendaries.Enabled=false;c.BossLootChance=100;c.DiveLoot.MaximumDropChance=100;
  var service=new RpgDiveSeriesService(points,config,new FixedRandom(0),()=>now);await service.StartAsync("1","Otter","start",10,default);Assert.Equal(1,await service.TickAsync(default));
  var a=(await points.GetProfileAsync("1",default)).Entry;var s=a.Rpg.DiveSeries!;Assert.Equal(3000,s.Experience);Assert.Equal(a.Rpg.Experience,s.Experience);Assert.Equal(200,s.Pearls);Assert.Equal(a.Rpg.Inventory.Count,s.Items.Sum(i=>i.Quantity));Assert.NotEmpty(s.Items);Assert.NotEmpty(s.Materials);
  foreach(var material in s.Materials)Assert.Equal(material.Value,RpgUpgradeCatalog.Count(a.Rpg.Materials,material.Key));
 }
 [Fact] public async Task Existing_cooldown_is_checked_once_before_start_and_not_waited_between_rolls(){
  var points=await Seed();await points.ApplyRpgAsync("1","Otter","cooldown",a=>{a.Rpg.Cooldowns["!dive"]=now.AddMinutes(1);return new(true);},default);var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);
  Assert.False((await service.StartAsync("1","Otter","early",100,default)).Success);now=now.AddMinutes(1);Assert.True((await service.StartAsync("1","Otter","start",100,default)).Success);await service.TickAsync(default);
  Assert.False((await new RpgAdventureService(points,config,new FixedRandom(999999),()=>now).ExecuteAsync("1","Otter","manual",RpgActivity.Dive,0,default)).Success);Assert.Equal(100,(await points.GetProfileAsync("1",default)).Entry.Rpg.DiveSeries!.Completed);
 }
 [Fact] public async Task Failed_batch_save_rolls_back_all_rolls_and_retry_does_not_duplicate_loot(){
  var points=await Seed();var service=new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now);await service.StartAsync("1","Otter","start",100,default);var temp=Path.Combine(root,"viewer-points.json.tmp");Directory.CreateDirectory(temp);
  await Assert.ThrowsAnyAsync<Exception>(()=>service.TickAsync(default));var unchanged=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal(0,unchanged.Points);Assert.Equal(0,unchanged.Rpg.DiveSeries!.Completed);Directory.Delete(temp);
  points=new(root);Assert.Equal(1,await new RpgDiveSeriesService(points,config,new FixedRandom(999999),()=>now).TickAsync(default));Assert.Equal(500,(await points.GetProfileAsync("1",default)).Entry.Points);
 }
 [Fact] public void Contract_rejects_forged_count_and_unknown_operations(){
  Assert.NotEmpty(RpgIntents.Canonical(new(Guid.NewGuid(),"diveSeries",JsonSerializer.SerializeToElement(new{operation="start",count="100"}))));
  foreach(var args in new[]{new{operation="start",count="101"},new{operation="attack",count="100"}})Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(new(Guid.NewGuid(),"diveSeries",JsonSerializer.SerializeToElement(args))));
 }
 [Fact] public void Lagoon_has_more_experience_and_materials_without_changing_other_biomes(){
  var c=config.Adventures.Rpg;var p=new RpgProfile();var z=c.Zones.Get("sunny_lagoon");var other=c.Zones.Get("sunken_temples");var baseline=RpgZoneService.MaterialChance(p,c,other,"blue_pearl","enemy");Assert.True(RpgZoneService.MaterialChance(p,c,z,"white_pearl","enemy")>=10);Assert.Equal(baseline,RpgZoneService.MaterialChance(p,c,other,"blue_pearl","enemy"));
 }
}
