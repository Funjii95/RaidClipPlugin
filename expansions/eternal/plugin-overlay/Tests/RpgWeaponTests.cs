using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RpgWeaponTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),"otter-weapons-"+Guid.NewGuid().ToString("N"));
 private static readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
 private sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
 private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 private static RpgConfig Config(){var c=new RpgConfig{Enabled=true};c.Health.Enabled=true;return c;}
 private static RpgProfile Armed(string id){var p=new RpgProfile();var item=RpgWeaponCatalog.Items().Single(i=>i.ItemId==id);p.Inventory.Add(item);p.Equipment[EquipmentSlot.Weapon]=item.InstanceId;return p;}
 public static IEnumerable<object[]> Weapons()=>RpgWeaponCatalog.Definitions.Select(w=>new object[]{w.Id,w.ManaCost});
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 [Theory][MemberData(nameof(Weapons))]public void EveryWeaponCastsWithRealManaAndRespectsItsPersistedCooldown(string id,int cost)
 {
  var c=Config();var p=Armed(id);RpgHealthService.Refresh(p,c.Health,Now);
  var hit=RpgPassiveService.BossAttack(10,p,c,new High(),Now);Assert.NotNull(hit.Spell);Assert.True(hit.Damage>hit.DirectDamage);Assert.Equal(100-cost,p.Health.CurrentMana);
  var restored=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(p))!;
  Assert.Null(RpgPassiveService.BossAttack(10,restored,c,new High(),Now).Spell);Assert.Equal(p.Health.CurrentMana,restored.Health.CurrentMana);
  Assert.True(hit.Spell.CounterReduction is >=0 and <=100);
 }
 [Fact]public void EmptyManaStillPermitsNormalAttacksAndRegenerationUsesTheEquippedBonus()
 {
  var c=Config();var p=Armed("weapon_leviathan_call");RpgHealthService.Refresh(p,c.Health,Now);p.Health.CurrentMana=0;
  var hit=RpgPassiveService.BossAttack(10,p,c,new High(),Now);Assert.Null(hit.Spell);Assert.Equal(310,hit.DirectDamage);Assert.True(hit.Damage>0);
  RpgWeaponService.RefreshMana(p,Now.AddSeconds(1));Assert.Equal(7,p.Health.CurrentMana);
  RpgWeaponService.RefreshMana(p,Now.AddSeconds(1));Assert.Equal(7,p.Health.CurrentMana);
  RpgWeaponService.RefreshMana(p,Now);Assert.Equal(7,p.Health.CurrentMana);
  RpgWeaponService.RefreshMana(p,Now.AddHours(1));Assert.Equal(100,p.Health.CurrentMana);
 }
 [Fact]public void ManaAndSpellsDoNotResetBySwitchingWeapons()
 {
  var c=Config();var p=Armed("weapon_leviathan_call");RpgHealthService.Refresh(p,c.Health,Now);
  Assert.NotNull(RpgPassiveService.BossAttack(10,p,c,new High(),Now).Spell);Assert.Equal(40,p.Health.CurrentMana);
  var other=RpgWeaponCatalog.Items().Single(i=>i.ItemId=="weapon_despair_staff");p.Inventory.Add(other);p.Equipment[EquipmentSlot.Weapon]=other.InstanceId;
  Assert.Null(RpgPassiveService.BossAttack(10,p,c,new High(),Now).Spell);Assert.Equal(40,p.Health.CurrentMana);
 }
 [Fact]public void ElementBonusesOnlyApplyToTheMatchingDamageSchoolAndElementalBonusAppliesToAllSchools()
 {
  var p=Armed("weapon_tide_crown");Assert.Equal(165,RpgWeaponService.ElementDamage(100,p,new(){WaterDamage=45,ElementalDamage=20,LightDamage=100,AbyssDamage=100}));
  p=Armed("weapon_star_tide");Assert.Equal(170,RpgWeaponService.ElementDamage(100,p,new(){LightDamage=50,ElementalDamage=20,WaterDamage=100}));
  p=Armed("weapon_void_devourer");Assert.Equal(145,RpgWeaponService.ElementDamage(100,p,new(){AbyssDamage=45,WaterDamage=100}));
 }
 [Fact]public void WeaponShieldAbsorbsActualDamageExpiresAndCannotBeKeptAfterUnequipping()
 {
  var c=Config();var p=Armed("weapon_reef_wardens");RpgHealthService.Refresh(p,c.Health,Now);var hit=RpgPassiveService.BossAttack(10,p,c,new High(),Now);
  RpgWeaponService.AfterHit(p,c,hit.Spell,20,Now);Assert.Equal(27,p.Health.ManaShieldHp);
  Assert.Equal(23,RpgWeaponService.Counter(100,p,hit.Spell,Now));Assert.Equal(0,p.Health.ManaShieldHp);
  RpgWeaponService.AfterHit(p,c,hit.Spell,20,Now);RpgWeaponService.RefreshMana(p,Now.AddSeconds(15));Assert.Equal(0,p.Health.ManaShieldHp);
  RpgWeaponService.AfterHit(p,c,hit.Spell,20,Now);p.Equipment.Clear();RpgWeaponService.RefreshMana(p,Now);Assert.Equal(0,p.Health.ManaShieldHp);
 }
 [Fact]public void OriginStaffHealsOnlyAfterAnActualHitAndNeverRevivesADeadPlayer()
 {
  var c=Config();var p=Armed("weapon_origin_current");RpgHealthService.Refresh(p,c.Health,Now);p.Health.CurrentHp=1;
  var hit=RpgPassiveService.BossAttack(10,p,c,new High(),Now);Assert.Equal(320,hit.Spell!.Healing);
  RpgWeaponService.AfterHit(p,c,hit.Spell,0,Now);Assert.Equal(1,p.Health.CurrentHp);
  RpgWeaponService.AfterHit(p,c,hit.Spell,10,Now);Assert.Equal(100,p.Health.CurrentHp);
  p.Health.CurrentHp=0;RpgWeaponService.AfterHit(p,c,hit.Spell,10,Now);Assert.Equal(0,p.Health.CurrentHp);
 }
 [Fact]public void NewWeaponsStayFixedAndEternalWeaponsUseOnlyTheThroneVictoryPool()
 {
  var c=Config();c.Zones.EternalChances["normal"]=100;
  Assert.Equal(12,RpgWeaponCatalog.Items().Count);Assert.Equal(12,RpgWeaponCatalog.Items().Select(w=>w.ItemId).Distinct().Count());
  Assert.All(RpgWeaponCatalog.Items(),w=>{Assert.Equal(EquipmentSlot.Weapon,w.Slot);Assert.Null(w.Generation);Assert.Empty(w.SetId);});
  var pool=RpgItemGenerationService.EnableForFutureDrops(c).DiveLoot.Items.Where(w=>RpgWeaponCatalog.Find(w.ItemId)!=null);Assert.Equal(12,pool.Count());Assert.All(pool,w=>Assert.Null(w.Generation));
  var p=new RpgProfile();var drop=RpgEternalLoot.Victory(p,c,"primordial_throne",100,"normal",true,new HighRoll(),Now);Assert.NotNull(drop);Assert.Equal("weapon_worldbreaker",drop.ItemId);Assert.Single(p.Inventory);
  Assert.Null(RpgEternalLoot.Victory(new(),c,"abyss",100,"normal",true,new Low(),Now));Assert.Null(RpgEternalLoot.Victory(new(),c,"primordial_throne",100,"fish",true,new Low(),Now));
 }
 private sealed class HighRoll:IDuelRandom{private int n;public int NextInclusive(int min,int max)=>n++==0?min:max;}
 [Fact]public async Task ActualBossActionPersistsManaAndCannotSpendItAgainOnReplay()
 {
  var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();c.Adventures.Boss.HitPoints=100000;c.Adventures.Boss.RoundCooldownSeconds=0;
  var store=new ViewerPointStore(root);var p=Armed("weapon_leviathan_call");await store.ApplyRpgAsync("42","Synthetic","seed",a=>{a.Rpg=p;return new(true);},default);
  var game=new AdventureGameService(store,c,(_,_)=>Task.CompletedTask,new High(),()=>Now,"weapon-test");await game.ControlAsync("boss",0,default);
  var hit=await game.ExecuteBossAttackAsync("42","Synthetic","attack-once",default);Assert.True(hit.Success);
  Assert.Equal(40,(await new ViewerPointStore(root).GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentMana);
  Assert.False((await game.ExecuteBossAttackAsync("42","Synthetic","attack-once",default)).Success);Assert.Equal(40,(await store.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentMana);
 }
 [Fact]public void ActualZoneFightUsesTheWeaponAndSpendsMana()
 {
  var c=Config();var p=Armed("weapon_leviathan_call");var result=new RpgEnemyService(new High()).Fight(p,c,c.Zones.Get("sunny_lagoon"),"normal",0,Now);
  Assert.True(result.Victory);Assert.Equal(1,result.Rounds);Assert.Equal(40,p.Health.CurrentMana);
 }
}
