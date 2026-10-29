using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgEarlyLegendaryTests
{
 private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 private static RpgProfile Equipped(string id){var p=new RpgProfile{PlayerLevel=30};var item=RpgEarlyLegendaryCatalog.Items().Single(i=>i.ItemId=="starter_"+id);p.Inventory.Add(item);p.Equipment[item.Slot!.Value]=item.InstanceId;return p;}
 [Fact] public void Twenty_unique_nonset_items_cover_existing_slots_and_have_real_sources()
 {
  var c=new RpgConfig{Enabled=true};c.Validate();var items=RpgEarlyLegendaryCatalog.Items();Assert.Equal(20,items.Count);Assert.Equal(20,items.Select(i=>i.ItemId).Distinct().Count());Assert.All(items,i=>{Assert.Equal(ItemRarity.Legendary,i.Rarity);Assert.Empty(i.SetId);Assert.InRange(i.MinimumLevel,1,30);Assert.False(string.IsNullOrWhiteSpace(i.Description));});Assert.Equal(Enum.GetValues<EquipmentSlot>().Where(s=>s!=EquipmentSlot.Belt).Order(),items.Select(i=>i.Slot!.Value).Distinct().Order());
  foreach(var item in items)Assert.Equal(3,RpgLootLocations.Build(c).Single(i=>i.Id==item.ItemId).Sources.Count);
 }
 [Fact] public void Extra_hit_and_healing_are_real_nonrecursive_and_respect_damage_confirmation()
 {
  var c=new RpgConfig();var now=DateTimeOffset.UtcNow;var p=Equipped("crab_breaker");var enhanced=RpgPassiveService.BossAttack(100,p,c,new Low(),now);p.Inventory[0].ItemId="ordinary-weapon";var ordinary=RpgPassiveService.BossAttack(100,p,c,new Low(),now);Assert.Equal(ordinary.DirectDamage+(int)(ordinary.DirectDamage*.5m),enhanced.DirectDamage);
  p=Equipped("healing_ring");RpgHealthService.Refresh(p,c.Health,now);p.Health.CurrentHp=1;RpgEarlyLegendaryEffects.AfterHit(0,p,c,new Low(),now);Assert.Equal(1,p.Health.CurrentHp);RpgEarlyLegendaryEffects.AfterHit(100,p,c,new Low(),now);Assert.Equal(1+(int)(RpgHealthService.Maximum(p,c.Health)*.05m),p.Health.CurrentHp);
  p.Health.CurrentHp=0;RpgEarlyLegendaryEffects.AfterHit(100,p,c,new Low(),now);Assert.Equal(0,p.Health.CurrentHp);
 }
 [Fact] public void Fatal_rescue_is_once_per_combat_persistent_and_does_not_revive_dead_players()
 {
  var c=new RpgConfig();var now=DateTimeOffset.UtcNow;var p=Equipped("forgotten_plate");RpgHealthService.Refresh(p,c.Health,now);p.Health.CurrentHp=40;
  var received=RpgEarlyLegendaryEffects.Receive(99999,p,c,"boss-one",now);p.Health.CurrentHp-=received;Assert.Equal((int)(RpgHealthService.Maximum(p,c.Health)*.2m),p.Health.CurrentHp);
  p=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(p))!;Assert.Equal(99999,RpgEarlyLegendaryEffects.Receive(99999,p,c,"boss-one",now.AddSeconds(1)));
  p.Health.CurrentHp=0;Assert.Equal(99999,RpgEarlyLegendaryEffects.Receive(99999,p,c,"boss-two",now.AddSeconds(1)));Assert.Equal(0,p.Health.CurrentHp);
 }
 [Fact] public async Task Minimum_level_is_server_validated_and_early_drops_never_leak_to_high_level_regions()
 {
  var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
  try{var c=new RpgConfig{Enabled=true};var p=new RpgProfile();var now=DateTimeOffset.UtcNow;var drop=RpgEarlyLegendaryCatalog.Roll(p,c,c.Zones.Get("sunny_lagoon"),new Low(),now);Assert.NotNull(drop);Assert.Equal(1,drop.MinimumLevel);Assert.Null(RpgEarlyLegendaryCatalog.Roll(p,c,c.Zones.Get("primordial_throne"),new Low(),now));
   var store=new ViewerPointStore(root);var weapon=RpgEarlyLegendaryCatalog.Items().Single(i=>i.ItemId=="starter_crab_breaker");await store.ApplyRpgAsync("42","Otter","seed",a=>{a.Rpg.Inventory.Add(weapon);return new(true);},default);
   var result=await new RpgProgressionService(store,c).EquipSelectionAsync("42","Otter","equip-too-early",weapon.InstanceId,EquipmentSlot.Weapon,default);Assert.False(result.Success);Assert.Empty((await store.GetProfileAsync("42",default)).Entry.Rpg.Equipment);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
