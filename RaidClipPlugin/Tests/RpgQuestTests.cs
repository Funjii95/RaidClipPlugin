using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RpgQuestTests:IDisposable
{
 readonly string dir=Path.Combine(Path.GetTempPath(),"Quest-"+Guid.NewGuid().ToString("N"));
 public void Dispose(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
 static MinigameConfig Config(){var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Rpg.Validate();return c;}
 static void Complete(RpgProfile p,QuestDefinition q){foreach(var r in q.Chapters.SelectMany(c=>c.Requirements))p.Quests.Counters[q.Id+":"+r.Id]=r.Target;}
 [Fact] public void CatalogHasElevenQuestsAndTenDistinctEightPieceLegendarySets(){
  var c=Config().Adventures.Rpg;Assert.Equal(new[]{1,10,20,30,40,50,60,70,80,90,99},RpgQuestCatalog.All.Select(q=>q.Level));
  foreach(var q in RpgQuestCatalog.Chains){var items=c.DiveLoot.Items.Where(i=>i.SetId==q.SetId).ToArray();Assert.Equal(8,items.Length);Assert.Equal(8,items.Select(i=>i.ItemId).Distinct().Count());Assert.Equal(2,items.Count(i=>i.Slot==EquipmentSlot.Ring1));Assert.All(items,i=>{Assert.Equal(ItemRarity.Legendary,i.Rarity);Assert.Equal(0,i.DropWeight);Assert.Empty(i.DropActivities);});
   var zone=c.Zones.Get(q.ZoneId);foreach(var r in q.Chapters.SelectMany(ch=>ch.Requirements)){if(r.FishId.Length>0)Assert.Contains(r.FishId,zone.Fish);if(r.RecipeFishId.Length>0)Assert.Contains(c.Recipes,recipe=>RpgSushiService.Ingredients(recipe).ContainsKey(r.RecipeFishId));}
  }
 }
 [Fact] public void CountersRespectLevelRegionSequentialChaptersAndMatchingFish(){
  var p=new RpgProfile{PlayerLevel=19};RpgQuestService.Record(p,"dive","sunken_temples",10);Assert.Empty(p.Quests.Counters);
  p.PlayerLevel=20;RpgQuestService.Record(p,"fish","sunken_temples",1,"rune_ray");Assert.Empty(p.Quests.Counters);
  RpgQuestService.Record(p,"dive","sunny_lagoon",3);Assert.False(p.Quests.Counters.ContainsKey("temple:dive"));
  RpgQuestService.Record(p,"dive","sunken_temples",3);Assert.Equal(1,RpgQuestService.Chapter(p,RpgQuestCatalog.Chains[1]));
  RpgQuestService.Record(p,"fish","sunken_temples",1,"eel");Assert.False(p.Quests.Counters.ContainsKey("temple:fish"));
  RpgQuestService.Record(p,"fish","sunken_temples",100,"rune_ray");Assert.Equal(1,p.Quests.Counters["temple:fish"]);
 }
 [Fact] public async Task TutorialAidAndRewardAreDurableAndOneTime(){
  var c=Config();var store=new ViewerPointStore(dir);var service=new RpgQuestService(store,c);
  var start=await service.StartAsync("42","Tester","start","tutorial",default);Assert.True(start.Success,start.Error);
  var first=(await store.GetProfileAsync("42",default)).Entry;Assert.NotEmpty(first.Rpg.Inventory);var pearls=first.Points;
  Assert.False((await service.StartAsync("42","Tester","repeat","tutorial",default)).Success);
  await store.ApplyRpgAsync("42","Tester","finish",a=>{Complete(a.Rpg,RpgQuestCatalog.Tutorial);return new(true);},default);
  Assert.True((await service.ClaimAsync("42","Tester","claim","tutorial",default)).Success);
  Assert.False((await service.ClaimAsync("42","Tester","claim-again","tutorial",default)).Success);
  var saved=(await new ViewerPointStore(dir).GetProfileAsync("42",default)).Entry;Assert.Equal(pearls+100000,saved.Points);Assert.Contains("tutorial",saved.Rpg.Quests.Claimed);
 }
 [Fact] public async Task FullSetClaimGivesEightLockedItemsAndCannotRepeat(){
  var c=Config();var store=new ViewerPointStore(dir);var q=RpgQuestCatalog.Chains[9];await store.ApplyRpgAsync("42","Tester","seed",a=>{a.Rpg.PlayerLevel=99;Complete(a.Rpg,q);return new(true);},default);
  var service=new RpgQuestService(store,c);Assert.True((await service.ClaimAsync("42","Tester","claim",q.Id,default)).Success);Assert.False((await service.ClaimAsync("42","Tester","claim2",q.Id,default)).Success);
  var p=(await store.GetProfileAsync("42",default)).Entry.Rpg;Assert.Equal(8,p.Inventory.Count);Assert.All(p.Inventory,i=>Assert.True(i.IsLocked));
 }
 [Fact] public async Task IncompleteConfigurationRollsBackAndLeavesRewardAvailable(){
  var c=Config();var q=RpgQuestCatalog.Chains[0];c.Adventures.Rpg.DiveLoot.Items.RemoveAll(i=>i.ItemId==q.SetId+"_7");var store=new ViewerPointStore(dir);await store.ApplyRpgAsync("42","Tester","seed",a=>{a.Rpg.PlayerLevel=10;Complete(a.Rpg,q);return new(true);},default);
  Assert.False((await new RpgQuestService(store,c).ClaimAsync("42","Tester","claim",q.Id,default)).Success);var p=(await store.GetProfileAsync("42",default)).Entry.Rpg;Assert.Empty(p.Inventory);Assert.DoesNotContain(q.Id,p.Quests.Claimed);
 }
 [Fact] public void ProtocolRejectsUnknownQuestStateAndViewFitsActionBudget(){
  Assert.Throws<InvalidDataException>(()=>QuestProgressRules.Validate(new(){Counters=new(){{"forged:dive",1}}}));Assert.Throws<InvalidDataException>(()=>QuestProgressRules.Validate(new(){Claimed=["forged"]}));
  var c=Config().Adventures.Rpg;var data=JsonSerializer.Serialize(RpgQuestService.View(new(),c));Assert.True(JsonSerializer.SerializeToUtf8Bytes(new ActionOutcome(true,"read","Quests",data)).Length<ActionResultLimits.MaximumOutcomeBytes);
 }
}

