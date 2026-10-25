using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RegionalMimicTests:IDisposable
{
 readonly string root=Path.Combine(Path.GetTempPath(),"RegionalMimic-"+Guid.NewGuid().ToString("N"));
 readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
 DateTimeOffset now=DateTimeOffset.UtcNow;
 sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 sealed class Boundary(int value):IDuelRandom{public int NextInclusive(int min,int max)=>Math.Clamp(value,min,max);}
 public RegionalMimicTests(){var c=config.Adventures.Rpg;c.Enabled=true;c.Health.Enabled=true;c.Health.Counterattacks=false;c.AreaBosses.Enabled=true;config.Adventures.Boss.AttackCooldownSeconds=1;}
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 AdventureGameService Engine(ViewerPointStore store)=>new(store,config,(_,_)=>Task.CompletedTask,new Low(),()=>now,"42");
 async Task Seed(ViewerPointStore store,string id,int attack=100)=>await store.ApplyRpgAsync(id,id,"seed",a=>{a.Rpg.PlayerLevel=50;a.Rpg.SelectedBossId="boss_eonath";a.Rpg.Inventory.Add(new(){ItemId="test-weapon",Kind=RpgItemKind.Equipment,Slot=EquipmentSlot.Weapon,Attributes=new(){Attack=attack}});a.Rpg.Equipment[EquipmentSlot.Weapon]=a.Rpg.Inventory.Last().InstanceId;return new(true);},default);
 [Fact] public void TenRegionalVariantsHaveHeavyHpDamageAndRegionalLootOnly(){
  var c=config.Adventures.Rpg;c.Validate();var mimics=RpgRegionalMimics.Catalog(c);Assert.Equal(10,mimics.Count);Assert.Equal(10,mimics.Select(m=>m.Name).Distinct().Count());
  foreach(var mimic in mimics){var original=RpgAreaBossConfig.Defaults().Single(b=>b.AreaId==mimic.AreaId);Assert.Equal(original.BaseHp*2,mimic.BaseHp);Assert.True(mimic.Attack>original.Attack);Assert.Equal(50,mimic.ItemChance);Assert.Equal(c.Zones.Get(mimic.AreaId).Materials,mimic.Materials);
   var pool=RpgRegionalMimics.Pool(c,mimic);Assert.NotEmpty(pool);Assert.All(pool,item=>{Assert.Equal(mimic.Rarity,item.Rarity.ToString());Assert.NotEqual(ItemRarity.Divine,item.Rarity);Assert.False(item.ItemId.StartsWith("quest_"));});
  }
 }
 [Theory][InlineData(0,true)][InlineData(4999,true)][InlineData(5000,false)][InlineData(9999,false)]
 public void EquipmentChanceIsExactlyFiftyPercentWithoutAdditiveCombatBonus(int value,bool expected){
  var c=config.Adventures.Rpg;c.Validate();var mimic=RpgRegionalMimics.ForZone(c,"tidal_maelstrom");var item=RpgRegionalMimics.Roll(c,mimic,new Boundary(value),now);Assert.Equal(expected,item is not null);if(item is not null)Assert.Contains(RpgRegionalMimics.Pool(c,mimic),i=>i.ItemId==item.ItemId);
 }
 [Fact] public async Task TwoPlayersAttackOnePersistedRegionalHpPoolAndReplayDoesNotCount(){
  var store=new ViewerPointStore(root);await Seed(store,"1001");await Seed(store,"1002");var engine=Engine(store);Assert.True(await engine.StartDiscoveredBossAsync("Finder",default,"crystal_grotto"));
  var before=await store.ReadGlobalBossAsync(default);Assert.True(before!.EventConfig.IsMimic);var face=Assert.Single(before.Bosses);Assert.Equal("crystal_grotto",face.AreaId);
  var hits=await Task.WhenAll(engine.ExecuteBossAttackAsync("1001","A","hit-a",default),engine.ExecuteBossAttackAsync("1002","B","hit-b",default),engine.ExecuteBossAttackAsync("1001","A","hit-a",default));Assert.Equal(2,hits.Count(h=>h.Success));
  var saved=await new ViewerPointStore(root).ReadGlobalBossAsync(default);Assert.Equal(1m-hits.Where(h=>h.Success).Sum(h=>(decimal)h.Damage)/face.BaseHp,saved!.Remaining);Assert.Equal(2,saved.Participants.Count);Assert.All(saved.Participants.Values,p=>Assert.Equal(face.Id,p.BossId));
  var restored=Engine(new ViewerPointStore(root));await restored.TickAsync(default);Assert.True(restored.Status.BossIsMimic);Assert.Equal(engine.Status.BossHp,restored.Status.BossHp);Assert.False(await restored.StartDiscoveredBossAsync("Other",default,"abyss"));
 }
 [Fact] public async Task VictoryPaysEachSurvivorOnceAndPreservesBossSelection(){
  var store=new ViewerPointStore(root);await Seed(store,"1001",100);await Seed(store,"1002",100000);var engine=Engine(store);await engine.StartDiscoveredBossAsync("Finder",default,"sunken_temples");
  await engine.ExecuteBossAttackAsync("1001","A","a",default);await engine.ExecuteBossAttackAsync("1002","B","b",default);var a=(await store.GetProfileAsync("1001",default)).Entry;var b=(await store.GetProfileAsync("1002",default)).Entry;Assert.True((await store.ReadGlobalBossAsync(default))!.RewardsSettled);
  Assert.Equal("boss_eonath",a.Rpg.SelectedBossId);Assert.Equal(400,a.Points);Assert.Equal(400,b.Points);Assert.Equal(2,a.Rpg.Inventory.Count);Assert.Equal(2,b.Rpg.Inventory.Count);Assert.All(a.Rpg.Inventory.Where(i=>i.ItemId!="test-weapon"),i=>Assert.Equal(ItemRarity.Rare,i.Rarity));
  await engine.TickAsync(default);Assert.False((await engine.ExecuteBossAttackAsync("1002","B","repeat",default)).Success);Assert.Equal(a.Points,(await new ViewerPointStore(root).GetProfileAsync("1001",default)).Entry.Points);
 }
 [Fact] public async Task DiscoveryDoesNotResolveSoloCombatOrGrantImmediateLoot(){
  var store=new ViewerPointStore(root);await Seed(store,"1001");var c=config.Adventures.Rpg;c.Zones.Areas[0].MimicChance=100;c.Zones.Areas[0].EnemyChance=0;c.Zones.Areas[0].ChestChance=0;
  var result=await new RpgAdventureService(store,config,new Low(),()=>now).ExecuteAsync("1001","A","dive",RpgActivity.Dive,0,default);Assert.True(result.Success,result.Error);Assert.True(result.BossDiscovered);Assert.Null(result.Enemy);Assert.Null(result.Item);Assert.Empty(result.MaterialRewards);Assert.Empty(result.AdditionalItems);
  var duplicate=await new RpgAdventureService(store,config,new Low(),()=>now.AddHours(1)).ExecuteAsync("1001","A","dive",RpgActivity.Dive,0,default);Assert.False(duplicate.BossDiscovered);
 }
 [Fact] public async Task FailedSaveCannotAdvanceSharedMimicHp(){
  var store=new ViewerPointStore(root);await Seed(store,"1001");var engine=Engine(store);await engine.StartDiscoveredBossAsync("Finder",default);var before=await store.ReadGlobalBossAsync(default);Directory.CreateDirectory(Path.Combine(root,"viewer-points.json.tmp"));await Assert.ThrowsAnyAsync<Exception>(()=>engine.ExecuteBossAttackAsync("1001","A","hit",default));Assert.Equal(before!.Remaining,(await new ViewerPointStore(root).ReadGlobalBossAsync(default))!.Remaining);
 }
 [Fact] public async Task StrongMimicsCounterattackAndDefeatedPlayersCannotAttackAgain(){
  config.Adventures.Rpg.Health.Counterattacks=true;var store=new ViewerPointStore(root);await Seed(store,"1001",0);var engine=Engine(store);await engine.StartDiscoveredBossAsync("Finder",default,"primordial_throne");var hit=await engine.ExecuteBossAttackAsync("1001","A","hit",default);Assert.True(hit.Success);var p=(await store.GetProfileAsync("1001",default)).Entry.Rpg;Assert.Equal(0,p.Health.CurrentHp);now=now.AddSeconds(11);Assert.False((await engine.ExecuteBossAttackAsync("1001","A","hit2",default)).Success);
 }
}
