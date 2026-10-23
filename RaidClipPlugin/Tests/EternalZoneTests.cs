using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class EternalZoneTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),"otter-zones-"+Guid.NewGuid().ToString("N"));
 private static readonly DateTimeOffset Now=new(2026,10,23,12,0,0,TimeSpan.Zero);
 private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 private sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
 private sealed class First(int first):IDuelRandom{private bool used;public int NextInclusive(int min,int max){if(!used){used=true;return Math.Clamp(first,min,max);}return min;}}
 private static RpgConfig Config(){var c=new RpgConfig{Enabled=true};c.Health.Enabled=true;return c;}
 private static RpgProfile Equipped(string set,int count=6){var p=new RpgProfile();foreach(var i in RpgEternalCatalog.Items().Where(i=>i.SetId==set).Take(count)){p.Inventory.Add(i);p.Equipment[i.Slot!.Value]=i.InstanceId;}return p;}
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 public static IEnumerable<object[]> Areas()=>RpgZoneConfig.Defaults().Select(z=>new object[]{z.Id,z.Name,z.Level,z.Materials,z.MaximumRarity});
 [Theory][MemberData(nameof(Areas))]
 public async Task EveryAreaHasFixedRealPoolsFreePersistentSelectionAndASeparateFishingChoice(string id,string name,int level,string[] materials,ItemRarity rarity)
 {
  var c=Config();c.Validate();var z=c.Zones.Get(id);Assert.Equal(name,z.Name);Assert.Equal(level,z.Level);Assert.Equal(3,z.Enemies.Length);Assert.Equal(materials,z.Materials);Assert.Equal(rarity,z.MaximumRarity);Assert.True(z.Fish.Distinct().Count()>=3);
  var store=new ViewerPointStore(root);await store.SetPointsAsync("42","Synthetic",0,0,default);
  Assert.True((await RpgZoneService.SelectAsync(store,c,"42","Synthetic","select", "dive",id,default)).Success);
  var saved=(await new ViewerPointStore(root).GetProfileAsync("42",default)).Entry;Assert.Equal(1,saved.Rpg.PlayerLevel);Assert.Equal(0,saved.Points);Assert.Equal(id,saved.Rpg.SelectedDivingZone);Assert.Equal("sunny_lagoon",saved.Rpg.SelectedFishingZone);
  var fish=RpgFishingService.Weights(c,new(),0,saved.Rpg).Where(f=>c.Zones.Get(saved.Rpg.SelectedFishingZone).Fish.Contains(f.Fish.Id));Assert.All(fish,f=>Assert.Contains(f.Fish.Id,c.Zones.Get("sunny_lagoon").Fish));
  var victory=Equipped("divine_infinity");victory.SelectedDivingZone=id;var combat=new RpgEnemyService(new Low()).Fight(victory,c,z,"normal",1,Now);
  Assert.True(combat.Victory);Assert.Equal(level,combat.Level);Assert.Equal(id,combat.ZoneId);Assert.Equal(z.Enemies[1],combat.Name);Assert.True(victory.Experience>0);Assert.DoesNotContain(victory.Inventory,i=>i.Rarity==ItemRarity.Eternal&&id!="primordial_throne");
 }
 [Fact]public void CatalogHas24FixedUniqueItemsAndSixCumulativeBonusesPerSet()
 {
  var items=RpgEternalCatalog.Items();Assert.Equal(24,items.Count);Assert.Equal(18,items.Count(i=>i.Rarity==ItemRarity.Eternal));Assert.Equal(6,items.Count(i=>i.Rarity==ItemRarity.Divine));Assert.Equal(24,items.Select(i=>i.ItemId).Distinct().Count());
  Assert.All(items,i=>{Assert.NotNull(i.Slot);Assert.NotEqual(EquipmentSlot.Weapon,i.Slot);Assert.Null(i.Generation);Assert.Empty(i.DropActivities);});
  Assert.All(RpgEternalCatalog.Sets(),s=>{Assert.Equal(6,s.ItemIds.Count);Assert.Equal(new[]{1,2,3,4,5,6},s.Bonuses.Select(b=>b.RequiredPieces));});
  var c=Config();var original=JsonSerializer.Serialize(items.Select(i=>i.Attributes));c=RpgItemGenerationService.EnableForFutureDrops(c);Assert.All(c.DiveLoot.Items.Where(i=>items.Any(d=>d.ItemId==i.ItemId)),i=>Assert.Null(i.Generation));Assert.Equal(original,JsonSerializer.Serialize(RpgEternalCatalog.Items().Select(i=>i.Attributes)));
 }
 [Theory][InlineData("ocean",100,"normal",true)][InlineData("abyss",100,"normal",true)][InlineData("primordial_throne",99,"normal",true)][InlineData("primordial_throne",100,"fish",true)][InlineData("primordial_throne",100,"chest",true)][InlineData("primordial_throne",100,"normal",false)]
 public void EternalRejectsEveryIneligibleSource(string area,int level,string source,bool won)=>Assert.Null(RpgEternalLoot.Victory(new(),Config(),area,level,source,won,new Low(),Now));
 [Theory][InlineData("normal",300)][InlineData("elite",1200)][InlineData("mimic",2000)][InlineData("endgame",3500)]
 public void EternalRatesHaveExactIndependentBoundaries(string source,int threshold)
 {
  var c=Config();Assert.NotNull(RpgEternalLoot.Victory(new(),c,"primordial_throne",100,source,true,new First(threshold-1),Now));Assert.Null(RpgEternalLoot.Victory(new(),c,"primordial_throne",100,source,true,new First(threshold),Now));
 }
 [Fact]public void WeakLevelOneLosesInAbyssWithoutAnyEnemyLootOrExperience()
 {
  var c=Config();var p=new RpgProfile();var r=new RpgEnemyService(new Low()).Fight(p,c,c.Zones.Get("abyss"),"normal",0,Now);Assert.Equal(90,r.Level);Assert.False(r.Victory);Assert.Empty(p.Inventory);Assert.Empty(r.Materials);Assert.Equal(0,p.Experience);
 }
 [Fact]public void DuplicateRingDoesNotActivateSecondSetBonusAndUnequippingClearsTimedStacks()
 {
  var c=Config();var p=Equipped("eternal_primordial_wrath",0);var ring=RpgEternalCatalog.Items().Single(i=>i.ItemId=="eternal_primordial_wrath_ring");p.Inventory.Add(ring);p.Equipment[EquipmentSlot.Ring1]=ring.InstanceId;
  var duplicate=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(ring))!;duplicate.InstanceId="second";p.Inventory.Add(duplicate);p.Equipment[EquipmentSlot.Ring2]=duplicate.InstanceId;Assert.False(RpgEternalCombat.Has(p,c,ring.SetId,2));
  p=Equipped("eternal_primordial_wrath");RpgEternalCombat.Proc(100,true,1,p,c,Now);Assert.Contains(p.Cooldowns.Keys,k=>k.Contains(":stack"));p.Equipment.Clear();RpgEternalCombat.Refresh(p,c.Health,Now);Assert.DoesNotContain(p.Cooldowns.Keys,k=>k.Contains(":stack"));
 }
 [Fact]public void DivineImmunityAndJudgmentUseRealAuthoritativeVictoryAndKeepRewards()
 {
  var c=Config();c.BossLootChance=100;c.DiveLoot.MaximumDropChance=100;c.Zones.EternalChances["normal"]=100;var p=Equipped("divine_infinity");
  Assert.Equal(0,RpgEternalCombat.Receive(1000000,p,c,"fight",Now));
  var r=new RpgEnemyService(new Low()).Fight(p,c,c.Zones.Get("primordial_throne"),"normal",0,Now);Assert.True(r.Victory);Assert.Equal(1,r.Rounds);Assert.Equal(0,r.DamageTaken);Assert.True(r.Experience>0);Assert.NotEmpty(r.Materials);Assert.Contains(p.Inventory,i=>i.Rarity==ItemRarity.Eternal);Assert.True(p.Inventory.Count>7);
 }
 [Fact]public async Task DivineUpgradeRejectsWithoutSpendingAndEternalAllNineLevelsPersist()
 {
  var c=Config();var store=new ViewerPointStore(root);await store.SetPointsAsync("42","Synthetic",1000000,0,default);
  await store.ApplyRpgAsync("42","Synthetic","seed",a=>{a.Rpg.Inventory.Add(RpgEternalCatalog.Items()[0]);a.Rpg.Inventory.Add(RpgEternalCatalog.Items().First(i=>i.Rarity==ItemRarity.Divine));foreach(var mat in RpgUpgradeCatalog.Materials)RpgUpgradeCatalog.Add(a.Rpg.Materials,mat.Id,1000);return new(true);},default);
  var account=(await store.GetProfileAsync("42",default)).Entry;var eternal=account.Rpg.Inventory[0];var divine=account.Rpg.Inventory[1];var before=JsonSerializer.Serialize(account);
  Assert.False((await new RpgUpgradeService(store,c).ApplyAsync("42","Synthetic","divine",divine.InstanceId,0,false,default)).Success);Assert.Equal(before,JsonSerializer.Serialize((await store.GetProfileAsync("42",default)).Entry));
  var baseStats=JsonSerializer.Serialize(eternal.Attributes);for(int n=0;n<9;n++)Assert.True((await new RpgUpgradeService(store,c).ApplyAsync("42","Synthetic","up"+n,eternal.InstanceId,n,false,default)).Success);
  Assert.True((await new RpgUpgradeService(store,c).ApplyAsync("42","Synthetic","refine",eternal.InstanceId,9,true,default)).Success);
  var restored=(await new ViewerPointStore(root).GetProfileAsync("42",default)).Entry.Rpg.Inventory[0];Assert.Equal(9,restored.UpgradeLevel);Assert.True(restored.IsRefined);Assert.Equal(baseStats,JsonSerializer.Serialize(restored.Attributes));
  var cost=RpgEquipmentUpgradeRules.Cost(8,ItemRarity.Eternal,c.Upgrades);Assert.Equal(5,cost.Materials["abyss_shard"]);Assert.Equal(3,cost.Materials["eternal_depth_core"]);
 }
 [Fact]public async Task PassiveChestAndFishingNeverGrantGearAndActionsCannotDuplicate()
 {
  var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();var z=c.Adventures.Rpg.Zones.Get("sunny_lagoon");z.MimicChance=z.EnemyChance=0;z.ChestChance=100;
  var store=new ViewerPointStore(root);await store.SetPointsAsync("42","Synthetic",0,0,default);var service=new RpgAdventureService(store,c,new Low(),()=>Now);
  var result=await service.ExecuteAsync("42","Synthetic","chest",RpgActivity.Dive,100000,default);Assert.True(result.Success);Assert.Equal(0,result.Stake);Assert.Equal("chest",result.EventType);Assert.Null(result.Enemy);
  var before=JsonSerializer.Serialize((await store.GetProfileAsync("42",default)).Entry);await service.ExecuteAsync("42","Synthetic","chest",RpgActivity.Dive,0,default);Assert.Equal(before,JsonSerializer.Serialize((await store.GetProfileAsync("42",default)).Entry));
  await service.ExecuteAsync("42","Synthetic","fish",RpgActivity.Fishing,100000,default);Assert.DoesNotContain((await store.GetProfileAsync("42",default)).Entry.Rpg.Inventory,i=>i.Slot.HasValue);
 }
 [Fact]public void OldProfilesDefaultBothZonesAndUnknownIdsAreRejected()
 {
  var p=JsonSerializer.Deserialize<RpgProfile>("{\"SchemaVersion\":1}")!;Assert.Equal("sunny_lagoon",p.SelectedDivingZone);Assert.Equal("sunny_lagoon",p.SelectedFishingZone);Assert.Throws<InvalidOperationException>(()=>Config().Zones.Get("invented"));
 }
 [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
 public void DivineEnemyChanceIsExactlyOneThousandPercentRelativeAndDoesNotStackPerPiece(int pieces)
 {
  var c=Config();var p=Equipped("divine_infinity",pieces);var a=CharacterStatService.Calculate(p,c,RpgActivity.Dive);Assert.Equal(1000,a.EnemyEncounterChance);
  var zone=c.Zones.Get("sunny_lagoon");Assert.Equal(99,RpgAdventureService.EnemyChance(zone,a));zone.EnemyChance=2;Assert.Equal(22,RpgAdventureService.EnemyChance(zone,a));zone.EnemyChance=0;Assert.Equal(0,RpgAdventureService.EnemyChance(zone,a));
  p.Equipment.Clear();Assert.Equal(0,CharacterStatService.Calculate(p,c,RpgActivity.Dive).EnemyEncounterChance);Assert.Equal(.03m,c.Zones.EternalChances["normal"]);
 }
 [Fact]public void WrathProcsDoNotRecurseStacksStopAtTenAndExpire()
 {
  var c=Config();var p=Equipped("eternal_primordial_wrath");Assert.Equal(600,RpgEternalCombat.Proc(100,true,1,p,c,Now));Assert.Equal(0,RpgEternalCombat.Proc(100,true,0,p,c,Now));
  for(int n=0;n<30;n++)RpgEternalCombat.Proc(100,true,0,p,c,Now);var stats=new RpgAttributes();RpgEternalCombat.Dynamic(p,c,stats,Now);Assert.Equal(80,stats.Damage);Assert.Equal(30,stats.AttackSpeed);
  stats=new();RpgEternalCombat.Dynamic(p,c,stats,Now.AddSeconds(11));Assert.Equal(0,stats.Damage);Assert.Equal(0,stats.AttackSpeed);
 }
 [Fact]public void TideHealingStacksRequireActualHealingAndRescueIsOncePerFight()
 {
  var c=Config();var p=Equipped("eternal_tide_blessing");RpgHealthService.Refresh(p,c.Health,Now);var max=RpgHealthService.Maximum(p,c.Health);
  Assert.Equal(0,RpgSetCombatService.Heal(p,c.Health,100,Now));Assert.DoesNotContain(p.Cooldowns.Keys,k=>k.Contains(":stack"));p.Health.CurrentHp=1;
  Assert.True(RpgSetCombatService.Heal(p,c.Health,1,Now)>0);Assert.Contains(p.Cooldowns.Keys,k=>k.Contains(":stack"));
  p.Health.CurrentHp=1;Assert.Equal(0,RpgEternalCombat.Receive(max,p,c,"combat",Now));Assert.Equal(max/2,p.Health.CurrentHp);Assert.Equal(max,RpgEternalCombat.Receive(max,p,c,"combat",Now));
  Assert.Equal(0,RpgEternalCombat.Receive(max,p,c,"new-combat",Now));
 }
}

