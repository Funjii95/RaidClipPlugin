using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class AreaBossTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),"otter-area-boss-"+Guid.NewGuid().ToString("N"));
 private readonly ViewerPointStore points;private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
 private DateTimeOffset now=DateTimeOffset.UtcNow;
 private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 public AreaBossTests(){points=new(root);var c=config.Adventures.Rpg;c.Enabled=true;c.Health.Enabled=true;c.Health.Counterattacks=false;c.AreaBosses.Enabled=true;config.Adventures.Boss.AttackCooldownSeconds=1;}
 private AdventureGameService Engine()=>new(points,config,(_,_)=>Task.CompletedTask,new Low(),()=>now,"42");
 private async Task Start(AdventureGameService engine)=>await engine.HandleAsync(new(){UserId="42",UserName="Owner",IsBroadcaster=true},"!start boss",["!start boss"],default);
 private async Task Seed(string user,string boss,int attack=0)=>await points.ApplyRpgAsync(user,user,"seed",a=>{
  a.Rpg.SelectedBossId=boss;a.Rpg.Inventory.Add(new(){ItemId="test-weapon",Kind=RpgItemKind.Equipment,Slot=EquipmentSlot.Weapon,Attributes=new(){Attack=attack}});a.Rpg.Equipment[EquipmentSlot.Weapon]=a.Rpg.Inventory.Last().InstanceId;return new(true);
 },default);
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 [Fact]public void TenFixedDefinitionsAreUniqueAndUseExactAreaLevelHpAndAttack()
 {
  var bosses=RpgAreaBossConfig.Defaults();Assert.Equal(10,bosses.Count);Assert.Equal(10,bosses.Select(b=>b.Id).Distinct().Count());
  Assert.Equal(new[]{15000,35000,70000,120000,200000,320000,500000,850000,1500000,3000000},bosses.Select(b=>b.BaseHp));
  Assert.Equal(new[]{25,50,90,145,220,320,450,650,950,1400},bosses.Select(b=>b.Attack));
  Assert.Equal(RpgZoneConfig.Defaults().Select(z=>z.Id),bosses.Select(b=>b.AreaId));Assert.Equal(Enumerable.Range(1,10).Select(n=>n*10),bosses.Select(b=>b.Level));
  Assert.Equal("Thal'Ruun, der Tempelhüter",bosses[1].Name);Assert.Equal("Nhar'Zul, Verschlinger der Leere",bosses[8].Name);
  Assert.Equal(10,bosses.Select(b=>b.Ability).Distinct().Count());new RpgAreaBossConfig().Validate();
 }
 [Fact]public async Task DifferentBossLevelsShareOneExactProgressAndAttacksBindTheirBoss()
 {
  await Seed("1001","boss_korallus",990);await Seed("1002","boss_eonath",59990);var engine=Engine();await Start(engine);
  var first=await engine.ExecuteBossAttackAsync("1001","A","hit-a",default);var second=await engine.ExecuteBossAttackAsync("1002","B","hit-b",default);
  Assert.True(first.Success,first.Error);Assert.True(second.Success,second.Error);
  var state=await points.ReadGlobalBossAsync(default);Assert.NotNull(state);
  // The existing critical mechanics remain authoritative; use their confirmed damage.
  Assert.Equal(1m-(decimal)first.Damage/15000-(decimal)second.Damage/3000000,state.Remaining);
  Assert.Equal("boss_korallus",state.Participants["1001"].BossId);Assert.Equal("boss_eonath",state.Participants["1002"].BossId);
  Assert.False((await engine.SelectAreaBossAsync("1001","A","switch","boss_eonath",default)).Success);
  var raw=await points.ReadCommittedFileAsync(default);Assert.DoesNotContain("$globalBoss",System.Text.Encoding.UTF8.GetString(raw!));
  var persisted=await new ViewerPointStore(root).ReadGlobalBossAsync(default);Assert.Equal(state.Remaining,persisted!.Remaining);
 }
 [Fact]public async Task DuplicateAndConcurrentAttacksNeverDoubleCountAndReconnectKeepsBinding()
 {
  await Seed("1001","boss_korallus",100);await Seed("1002","boss_eonath",100);var engine=Engine();await Start(engine);
  var hits=await Task.WhenAll(engine.ExecuteBossAttackAsync("1001","A","same",default),engine.ExecuteBossAttackAsync("1001","A","same",default),engine.ExecuteBossAttackAsync("1002","B","other",default));
  Assert.Equal(2,hits.Count(h=>h.Success));var state=await points.ReadGlobalBossAsync(default);Assert.Equal(2,state!.Participants.Count);
  var restored=new AdventureGameService(new ViewerPointStore(root),config,(_,_)=>Task.CompletedTask,new Low(),()=>now,"42");
  await restored.TickAsync(default);Assert.Equal(engine.Status.BossHp,restored.Status.BossHp);
  Assert.False((await restored.SelectAreaBossAsync("1001","A","switch","boss_nharzul",default)).Success);
  Assert.Equal(1,(await new ViewerPointStore(root).ReadGlobalBossAsync(default))!.Participants["1001"].Cycles);
 }
 [Fact]public async Task FailedAtomicSaveCannotAdvancePlayerOrGlobalProgress()
 {
  await Seed("1001","boss_korallus",100);var engine=Engine();await Start(engine);var before=await points.ReadGlobalBossAsync(default);
  Directory.CreateDirectory(Path.Combine(root,"viewer-points.json.tmp"));
  await Assert.ThrowsAnyAsync<Exception>(()=>engine.ExecuteBossAttackAsync("1001","A","hit",default));
  Assert.Equal(before!.Remaining,(await new ViewerPointStore(root).ReadGlobalBossAsync(default))!.Remaining);
  Assert.Empty((await new ViewerPointStore(root).ReadGlobalBossAsync(default))!.Participants);
  Directory.Delete(Path.Combine(root,"viewer-points.json.tmp"));
  Assert.True((await engine.ExecuteBossAttackAsync("1001","A","hit",default)).Success);
 }
 [Fact]public async Task EonathVictoryGrantsPersonalLootExactlyOnceAndKorallusNeverGetsEternal()
 {
  var c=config.Adventures.Rpg;RpgEternalCatalog.AddMissing(c);c.Health.Counterattacks=false;
  c.AreaBosses.Bosses.ForEach(b=>b.ItemChance=100);c.Zones.EternalEightChances["eonath"]=100;
  await Seed("1001","boss_korallus",1);await Seed("1002","boss_eonath",1000000);
  var engine=Engine();await Start(engine);Assert.True((await engine.ExecuteBossAttackAsync("1001","A","a",default)).Success);
  for(var n=0;n<10&&engine.Status.BossActive;n++){now=now.AddSeconds(2);await engine.ExecuteBossAttackAsync("1002","B","b"+n,default);}
  Assert.False(engine.Status.BossActive);var state=await points.ReadGlobalBossAsync(default);Assert.True(state!.Victory);Assert.True(state.RewardsSettled);Assert.Equal(0,state.Remaining);
  var low=(await points.GetProfileAsync("1001",default)).Entry;var high=(await points.GetProfileAsync("1002",default)).Entry;
  Assert.DoesNotContain(low.Rpg.Inventory,i=>i.Rarity>=ItemRarity.Eternal);Assert.Contains(high.Rpg.Inventory,i=>i.Rarity==ItemRarity.Eternal&&i.SetId.StartsWith("eternal_cosmic_forge"));
  Assert.Equal(500,low.Rpg.Experience);Assert.Equal(65000,high.Rpg.Experience);
  var before=JsonSerializer.Serialize(high);await engine.TickAsync(default);Assert.Equal(before,JsonSerializer.Serialize((await points.GetProfileAsync("1002",default)).Entry));
 }
 [Theory][InlineData("boss_korallus",25)][InlineData("boss_thalruun",63)][InlineData("boss_moderklaue",90)][InlineData("boss_luminara",196)][InlineData("boss_vortexion",363)][InlineData("boss_krysalith",512)][InlineData("boss_maledictus",630)][InlineData("boss_aethernos",1203)][InlineData("boss_nharzul",2299)][InlineData("boss_eonath",4620)]
 public async Task EachBossUsesItsOwnFixedCounterAtPlayerLevelOne(string boss,int expected)
 {
  config.Adventures.Rpg.Health.Counterattacks=true;await Seed("1001",boss);
  await points.ApplyRpgAsync("1001","A","hp",a=>{a.Rpg.Inventory[0].Rarity=ItemRarity.Eternal;a.Rpg.Inventory[0].Attributes.MaximumHp=100000;return new(true);},default);
  var engine=Engine();await Start(engine);var before=RpgHealthService.Maximum((await points.GetProfileAsync("1001",default)).Entry.Rpg,config.Adventures.Rpg.Health);
  Assert.True((await engine.ExecuteBossAttackAsync("1001","A","hit",default)).Success);
  var saved=(await points.GetProfileAsync("1001",default)).Entry.Rpg;
  Assert.Equal(1,saved.PlayerLevel);Assert.Equal(expected,before-saved.Health.CurrentHp);
 }
 [Fact]public async Task DeletedParticipantCannotRewindTheSingleGlobalProgress()
 {
  await Seed("1001","boss_korallus",100);var engine=Engine();await Start(engine);await engine.ExecuteBossAttackAsync("1001","A","hit",default);
  var before=(await points.ReadGlobalBossAsync(default))!.Remaining;
  await points.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default);
  var after=await new ViewerPointStore(root).ReadGlobalBossAsync(default);Assert.Equal(before,after!.Remaining);
 }
 [Fact]public async Task DefeatedParticipantRemainsIneligibleEvenAfterExternalHealing()
 {
  config.Adventures.Rpg.Health.Counterattacks=true;await Seed("1001","boss_eonath");var engine=Engine();await Start(engine);
  Assert.True((await engine.ExecuteBossAttackAsync("1001","A","dead",default)).Success);
  Assert.True((await points.ReadGlobalBossAsync(default))!.Participants["1001"].Defeated);
  await points.ApplyRpgAsync("1001","A","heal",a=>{a.Rpg.Health.CurrentHp=100;a.Rpg.Health.Defeated=false;return new(true);},default);
  now=now.AddSeconds(10);Assert.False((await engine.ExecuteBossAttackAsync("1001","A","again",default)).Success);
 }
}
