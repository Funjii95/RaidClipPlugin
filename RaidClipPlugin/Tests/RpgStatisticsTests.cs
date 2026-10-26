using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgStatisticsTests
{
 [Fact] public async Task Counters_commit_once_survive_restart_and_reach_cloud_projection()
 {
  var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
  try{var store=new ViewerPointStore(root);
   Task<RpgTransactionResult> Record(string id)=>store.ApplyRpgAsync("42","Otter",id,a=>{RpgStatisticsService.Add(a.Rpg,"dives");RpgStatisticsService.Zone(a.Rpg,"sunny_lagoon","dives");RpgStatisticsService.Fish(a.Rpg,"sunfin",3);return new(true);},default);
   await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Record("same-event")));
   var account=(await new ViewerPointStore(root).GetProfileAsync("42",default)).Entry;
   Assert.Equal(1,account.Rpg.Statistics.Counters["dives"]);Assert.Equal(3,account.Rpg.Statistics.Fish["sunfin"]);
   var projected=SnapshotValidation.Validate(new(1,"42","42",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),"42");
   using var doc=JsonDocument.Parse(projected.AccountJson!);Assert.Equal(1,doc.RootElement.GetProperty("Rpg").GetProperty("Statistics").GetProperty("Counters").GetProperty("dives").GetInt32());
   var failed=await store.ApplyRpgAsync("42","Otter","rejected",a=>{RpgStatisticsService.Add(a.Rpg,"dives");return new(false);},default);Assert.False(failed.Success);Assert.Equal(1,(await store.GetProfileAsync("42",default)).Entry.Rpg.Statistics.Counters["dives"]);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 [Fact] public void Old_profiles_start_empty_and_invalid_or_unbounded_maps_are_rejected()
 {
  var profile=JsonSerializer.Deserialize<RpgProfile>("{\"SchemaVersion\":1}")!;Assert.Empty(profile.Statistics.Counters);RpgStatistics.Validate(profile.Statistics);
  profile.Statistics.Counters["dives"]=-1;Assert.Throws<InvalidDataException>(()=>RpgStatistics.Validate(profile.Statistics));
  profile.Statistics.Counters.Clear();profile.Statistics.Zones["invented_biome"]=new();Assert.Throws<InvalidDataException>(()=>RpgStatistics.Validate(profile.Statistics));
 }
 [Fact] public void Material_navigation_uses_the_same_live_formula_and_zone_tables_as_rewards()
 {
  var c=new RpgConfig{Enabled=true};var p=new RpgProfile();var z=c.Zones.Get("sunny_lagoon");
  c.Upgrades.Drops["enemy"]["white_pearl"]=new(){Chance=17,Minimum=1,Maximum=1};
  Assert.Equal(32,RpgZoneService.MaterialChance(p,c,z,"white_pearl","enemy"));Assert.Equal(0,RpgZoneService.MaterialChance(p,c,z,"abyss_shard","enemy"));
  using var doc=JsonDocument.Parse(JsonSerializer.Serialize(RpgZoneService.MaterialLocations(p,c)));var found=doc.RootElement.EnumerateArray().Single(x=>x.GetProperty("MaterialId").GetString()=="white_pearl"&&x.GetProperty("ZoneId").GetString()==z.Id&&x.GetProperty("Source").GetString()=="enemy");Assert.Equal(32,found.GetProperty("Chance").GetDecimal());
  Assert.Equal(10,c.Zones.Areas.Count);Assert.Equal(c.Zones.Areas.Select(x=>x.Id).Order(),RpgStatistics.ZoneIds.Order());
 }
}
