using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RpgTalentTests:IDisposable
{
 readonly string root=Path.Combine(Path.GetTempPath(),"otter-talents-"+Guid.NewGuid().ToString("N"));
 readonly RpgConfig config=new(){Enabled=true};
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 async Task<ViewerPointStore> Seed(int level){var store=new ViewerPointStore(root);await store.ApplyRpgAsync("1001","Otter","seed",a=>{a.Points=1234;a.Rpg.PlayerLevel=level;return new(true);},default);return store;}
 Task<RpgTransactionResult> Learn(ViewerPointStore s,string request,string id,int rank=0)=>RpgTalentService.LearnAsync(s,config,"1001","Otter",request,id,rank,1,default);
 [Theory][InlineData(1,0)][InlineData(30,29)][InlineData(100,99)][InlineData(101,99)][InlineData(int.MinValue,0)]
 public void RetroactiveEntitlement(int level,int expected)=>Assert.Equal(expected,TalentRules.Earned(level));
 [Fact] public void CatalogContainsExactly33PointsPerBranchAnd99Overall(){Assert.Equal(21,TalentRules.Catalog.Count);foreach(var b in new[]{"Boss","Fishing","Dive"})Assert.Equal(33,TalentRules.Catalog.Where(t=>t.Branch==b).Sum(t=>t.Max));var ranks=TalentRules.Catalog.ToDictionary(t=>t.Id,t=>t.Max);TalentRules.Validate(100,ranks);Assert.Equal(0,TalentRules.Available(100,ranks));}
 [Fact] public async Task LegacyProfileBackfillsWithoutSpendingPearlsOrRepeatedCredits(){var s=await Seed(30);var p=(await s.GetProfileAsync("1001",default)).Entry.Rpg;Assert.Equal(29,TalentRules.Available(p.PlayerLevel,p.TalentRanks));Assert.True((await Learn(s,"one","combat-power")).Success);var reload=new ViewerPointStore(root);p=(await reload.GetProfileAsync("1001",default)).Entry.Rpg;Assert.Equal(28,TalentRules.Available(p.PlayerLevel,p.TalentRanks));Assert.Equal(1234,await reload.GetPointsAsync("1001",default));Assert.True(File.Exists(Path.Combine(root,"viewer-points.json.pre-talents-v1.bak")));}
 [Fact] public async Task DuplicateAndConcurrentRequestsCannotSpendTwice(){var s=await Seed(2);var result=await Task.WhenAll(Learn(s,"a","combat-power"),Learn(s,"b","combat-power"));Assert.Single(result.Where(r=>r.Success));var duplicate=await Learn(new ViewerPointStore(root),result[0].Success?"a":"b","combat-power");Assert.True(duplicate.Duplicate);var p=(await s.GetProfileAsync("1001",default)).Entry.Rpg;Assert.Equal(1,p.TalentRanks["combat-power"]);Assert.Equal(0,TalentRules.Available(2,p.TalentRanks));}
 [Fact] public async Task RejectsPrerequisitesUnknownIdsStaleRanksAndCatalog(){var s=await Seed(100);Assert.False((await Learn(s,"prereq","combat-crit")).Success);Assert.False((await Learn(s,"unknown","wrong")).Success);Assert.True((await Learn(s,"ok","combat-power")).Success);Assert.False((await Learn(s,"stale","combat-power")).Success);Assert.False((await RpgTalentService.LearnAsync(s,config,"1001","Otter","catalog","combat-power",1,2,default)).Success);}
 [Fact] public async Task FailedSaveRollsBackAllocation(){var s=await Seed(100);Directory.CreateDirectory(Path.Combine(root,"viewer-points.json.tmp"));await Assert.ThrowsAnyAsync<Exception>(()=>Learn(s,"fail","combat-power"));var p=(await s.GetProfileAsync("1001",default)).Entry.Rpg;Assert.Empty(p.TalentRanks);Assert.DoesNotContain("fail",p.CompletedActions);}
 [Fact] public void LevelUpAddsOnePointWithoutTouchingExistingRanks(){var p=new RpgProfile{PlayerLevel=2,Experience=2999,TalentRanks=new(){{"combat-power",1}}};RpgHealthService.Experience(p,config.Health,1,DateTimeOffset.UtcNow);Assert.Equal(3,p.PlayerLevel);Assert.Equal(1,TalentRules.Available(p.PlayerLevel,p.TalentRanks));}
 [Fact] public void UnderwaterDamageAndDodgeDoNotModifyBossStats(){var p=new RpgProfile{PlayerLevel=100};var before=CharacterStatService.Calculate(p,config,RpgActivity.Boss);p.TalentRanks=new(){{"dive-enemy",5},{"dive-dodge",5}};var after=CharacterStatService.Calculate(p,config,RpgActivity.Boss);Assert.Equal(before.Damage,after.Damage);Assert.Equal(before.BossDodgeChance,after.BossDodgeChance);Assert.Equal(125,RpgTalentService.UnderwaterDamage(100,p));Assert.Equal(20,RpgTalentService.Bonus(p,"underwaterDodge"));}
 sealed class Fixed(int dodge):IDuelRandom {public int NextInclusive(int min,int max)=>max==9999?dodge:min;}
 [Theory][InlineData(1999,0)][InlineData(2000,8)]
 public void UnderwaterDodgeHasExactTwentyPercentBoundary(int roll,int expectedDamage){var p=new RpgProfile{PlayerLevel=100,TalentRanks=new(){{"dive-dodge",5}}};config.Enemies.EncounterChance=100;config.Enemies.MaximumRounds=1;config.Enemies.Tiers[0].HitPoints=1000;var r=new RpgEnemyService(new Fixed(roll)).Encounter(p,config,0,DateTimeOffset.UtcNow)!;Assert.Equal(expectedDamage,r.DamageTaken);}
 [Fact] public void MaxHpTalentDoesNotMultiplyEquipmentHp(){var p=new RpgProfile{PlayerLevel=6,TalentRanks=new(){{"combat-health",5}}};p.Inventory.Add(new(){InstanceId="hp",Slot=EquipmentSlot.Helmet,Attributes=new(){MaximumHp=100}});p.Equipment[EquipmentSlot.Helmet]="hp";Assert.Equal(232,RpgHealthService.Maximum(p,config.Health));}
 [Fact] public void SnapshotRoundtripsTalentRanksWithoutActionTokens(){var account=new ViewerPointEntry{UserId="1001",DisplayName="Otter",UpdatedAt=DateTimeOffset.UtcNow,Rpg=new(){PlayerLevel=30,TalentRanks=new(){{"combat-power",3}}}};var snapshot=SnapshotValidation.Validate(new(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),"42");using var doc=JsonDocument.Parse(snapshot.AccountJson!);Assert.Equal(3,doc.RootElement.GetProperty("Rpg").GetProperty("TalentRanks").GetProperty("combat-power").GetInt32());Assert.DoesNotContain("CompletedActions",snapshot.AccountJson);}
 [Fact] public void RejectsOverspentOrMalformedPersistedRanks(){Assert.Throws<InvalidDataException>(()=>TalentRules.Validate(2,new Dictionary<string,int>{{"combat-power",2}}));Assert.Throws<InvalidDataException>(()=>TalentRules.Validate(100,new Dictionary<string,int>{{"combat-crit",1}}));Assert.Throws<InvalidDataException>(()=>TalentRules.Validate(100,new Dictionary<string,int>{{"combat-power",6}}));}
 [Fact] public void IntentRequiresCurrentRankAndCatalogVersion(){var intent=new RpgIntent(Guid.NewGuid(),"learnTalent",JsonSerializer.SerializeToElement(new{talentId="combat-power",expectedRank="0",catalogVersion="1"}));Assert.NotEmpty(RpgIntents.Hash(intent));Assert.Throws<ArgumentException>(()=>RpgIntents.Hash(intent with {Arguments=JsonSerializer.SerializeToElement(new{talentId="combat-power",expectedRank="-1",catalogVersion="1"})}));}
 [Fact] public void CooldownAndPayoutBonusesAreBounded(){var p=new RpgProfile{TalentRanks=new(){{"combat-speed",5},{"dive-speed",5}}};Assert.Equal(8.5,RpgTalentService.Cooldown(p,10,"attackCooldown"));Assert.Equal(1,RpgTalentService.Cooldown(p,1,"diveCooldown"));}
 [Fact] public async Task FishingTalentRescuesOnlyItsChanceBandAndDoesNotAwardAShell()
 {
  var s=await Seed(30);await s.ApplyRpgAsync("1001","Otter","allocate",a=>{a.Rpg.TalentRanks["fishing-catch"]=5;return new(true);},default);
  var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Fishing.CooldownSeconds=1;
  var game=new RpgAdventureService(s,c,new FirstThenLow(40),()=>DateTimeOffset.UtcNow);
  var result=await game.ExecuteAsync("1001","Otter","fish",RpgActivity.Fishing,0,default);
  Assert.True(result.Success);Assert.NotNull(result.Item);Assert.Equal(0,result.ShellsAwarded);
  Assert.NotEmpty((await s.GetProfileAsync("1001",default)).Entry.Rpg.Fish);
 }
 sealed class FirstThenLow(int first):IDuelRandom{bool used;public int NextInclusive(int min,int max){if(!used){used=true;return first;}return min;}}
 [Fact] public void FishingRarityBonusesRespectStakeGates()
 {
  var p=new RpgProfile{TalentRanks=new(){{"fishing-luck",5},{"fishing-quality",5},{"fishing-master",3}}};
  config.FishSpecies=new(){new(){Id="common",Weight=100,Rarity=ItemRarity.Common},new(){Id="rare",Weight=100,Rarity=ItemRarity.Rare},new(){Id="legend",Weight=100,Rarity=ItemRarity.Legendary,MinimumStake=1000}};
  var weights=RpgFishingService.Weights(config,new(),0,p);
  Assert.Equal(100,weights.Single(x=>x.Fish.Id=="common").Weight);Assert.Equal(125,weights.Single(x=>x.Fish.Id=="rare").Weight);Assert.DoesNotContain(weights,x=>x.Fish.Id=="legend");
 }
 [Fact] public void TalentLootBonusIsCappedWithoutChangingRarityGate()
 {
  config.DiveLoot.MaximumDropChance=100;
  var original=RpgLootService.Chances(0,0,config.DiveLoot);
  var result=new RpgLootService(new FirstThenLow(1)).Roll(0,0,config.DiveLoot,DateTimeOffset.UtcNow,talentDropBonus:20);
  Assert.Equal(Math.Min(100,original.Total+20),result.Chances.Total);Assert.Equal(original.MaximumRarity,result.Chances.MaximumRarity);
 }

}
