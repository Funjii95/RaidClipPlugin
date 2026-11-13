using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgMythicBalanceTests
{
 [Fact] public void Existing_items_keep_identity_upgrade_and_healing_and_migrate_once(){
  var item=RpgDawnHealerCatalog.Items()[4];item.UpgradeLevel=9;item.IsRefined=true;item.IsLocked=true;
  var id=item.InstanceId;var heal=item.Attributes.HealingPower;
  Assert.True(RpgCoreStatService.Apply(item));Assert.Equal(40,item.Attributes.BossDamage);Assert.Equal(2400,item.Attributes.Attack);
  var json=JsonSerializer.Serialize(item);Assert.False(RpgCoreStatService.Apply(item));Assert.Equal(json,JsonSerializer.Serialize(item));
  Assert.Equal(id,item.InstanceId);Assert.Equal(9,item.UpgradeLevel);Assert.True(item.IsRefined);Assert.True(item.IsLocked);Assert.Equal(heal,item.Attributes.HealingPower);
  item.Attributes.BossDamage=99;RpgCoreStatService.Apply(item);Assert.Equal(99,item.Attributes.BossDamage);
 }
 [Fact] public void All_mythic_equipment_including_trinkets_gains_boss_damage_without_affecting_eternal(){
  var c=new RpgConfig();c.Validate();var mythic=c.DiveLoot.Items.Where(i=>i.Rarity==ItemRarity.Mythic && i.Kind==RpgItemKind.Equipment).ToArray();
  Assert.NotEmpty(mythic);Assert.All(mythic,i=>Assert.True(i.Attributes.BossDamage>=40));
  var eternal=RpgEternalEightCatalog.Items()[0];var json=JsonSerializer.Serialize(eternal);Assert.False(RpgMythicBalance.Apply(eternal));Assert.Equal(json,JsonSerializer.Serialize(eternal));
 }
 [Fact] public async Task Saved_old_items_receive_bonus_on_reload_without_losing_upgrades(){
  var root=Path.Combine(Path.GetTempPath(),"mythic-migration-"+Guid.NewGuid().ToString("N"));
  try{
   var store=new ViewerPointStore(root);var item=RpgDawnHealerCatalog.Items()[4];item.UpgradeLevel=9;item.IsFavorite=true;
   await store.ApplyRpgAsync("1001","Test","seed",a=>{a.Rpg.Inventory.Add(item);a.Rpg.Equipment[EquipmentSlot.Weapon]=item.InstanceId;return new(true);},default);
   var profile=(await new ViewerPointStore(root).GetProfileAsync("1001",default)).Entry.Rpg;
   var saved=Assert.Single(profile.Inventory);Assert.Equal(item.InstanceId,saved.InstanceId);Assert.Equal(9,saved.UpgradeLevel);Assert.True(saved.IsFavorite);
   Assert.Equal(40,saved.Attributes.BossDamage);Assert.Equal(2400,saved.Attributes.Attack);Assert.Equal(item.InstanceId,profile.Equipment[EquipmentSlot.Weapon]);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 [Fact] public async Task Startup_persists_already_v3_items_to_cloud_source_exactly_once(){
  var root=Path.Combine(Path.GetTempPath(),"mythic-startup-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);var health=new RpgHealthConfig{PassiveHealing=false};
   var entry=new ViewerPointEntry{UserId="1001",DisplayName="Test",Points=777};entry.Rpg.PlayerLevel=99;
   entry.Rpg.Inventory=RpgDawnHealerCatalog.Items();entry.Rpg.Health.CurrentHp=10;
   RpgHealthService.ApplyStatMigration(entry.Rpg,health);
   var file=Path.Combine(root,"viewer-points.json");
   await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new Dictionary<string,ViewerPointEntry>{{"1001",entry}}));
   var store=new ViewerPointStore(root);var events=0;store.Persisted+=()=>events++;
   Assert.Equal(1,await store.UpgradeEquipmentCoreStatsAsync(health,default));
   using(var doc=JsonDocument.Parse(await File.ReadAllBytesAsync(file))){var account=doc.RootElement.GetProperty("1001");Assert.Equal(777,account.GetProperty("Points").GetInt64());
    foreach(var item in account.GetProperty("Rpg").GetProperty("Inventory").EnumerateArray()){Assert.Equal(40,item.GetProperty("Attributes").GetProperty("BossDamage").GetDecimal());Assert.True(item.GetProperty("Attributes").GetProperty("Attack").GetDecimal()>=140);}}
   Assert.Equal(1,events);var saved=await File.ReadAllBytesAsync(file);
   Assert.Equal(0,await new ViewerPointStore(root).UpgradeEquipmentCoreStatsAsync(health,default));Assert.Equal(saved,await File.ReadAllBytesAsync(file));
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 static RpgProfile Wear(IEnumerable<RpgItem> items,int upgrade){var p=new RpgProfile{PlayerLevel=99};foreach(var i in items){RpgCoreStatService.Apply(i);i.UpgradeLevel=upgrade;p.Inventory.Add(i);var slot=i.Slot!.Value;if(slot==EquipmentSlot.Ring1 && p.Equipment.ContainsKey(slot))slot=EquipmentSlot.Ring2;p.Equipment[slot]=i.InstanceId;}p.Health.CurrentHp=100;return p;}
 [Theory][InlineData(0)][InlineData(9)] public void Full_mythic_damage_set_beats_eternal_damage_sets_at_equal_upgrade(int upgrade){
  var c=new RpgConfig();c.Validate();var now=DateTimeOffset.UtcNow;
  var mythic=Wear(c.DiveLoot.Items.Where(i=>i.SetId==RpgMythicRoleCatalog.DamageSet),upgrade);
  var m=RpgCombatPowerService.Evaluate(mythic,c,now).ExpectedHit;
  foreach(var group in RpgEternalEightCatalog.Items().Concat(RpgEternalCatalog.Items()).Where(i=>i.Rarity==ItemRarity.Eternal).GroupBy(i=>i.SetId)){
   var eternal=Wear(group,upgrade);
   foreach(var slot in new[]{EquipmentSlot.Belt,EquipmentSlot.Trinket1,EquipmentSlot.Trinket2})if(!eternal.Equipment.ContainsKey(slot)){var extra=c.DiveLoot.Items.Where(i=>i.Rarity==ItemRarity.Eternal && i.Slot==slot).OrderByDescending(i=>i.Attributes.Attack+i.Attributes.BossDamage).FirstOrDefault();if(extra is not null){extra.UpgradeLevel=upgrade;eternal.Inventory.Add(extra);eternal.Equipment[slot]=extra.InstanceId;}}var e=RpgCombatPowerService.Evaluate(eternal,c,now).ExpectedHit;
   Console.WriteLine($"+{upgrade} {group.Key}: Mythic {m:N0}, Eternal {e:N0}, ratio {m/e:N2}");
   Assert.True(m>e*1.15m,$"{group.Key}: Mythic {m:N0}, Eternal {e:N0}");
  }
 }
}
