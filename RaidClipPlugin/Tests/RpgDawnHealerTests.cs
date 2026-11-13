using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;

public sealed class RpgDawnHealerTests
{
 readonly RpgConfig config=new();
 readonly DateTimeOffset now=DateTimeOffset.Parse("2026-11-06T12:00:00Z");
 sealed class High:IDuelRandom {public int NextInclusive(int min,int max)=>max;}
 static RpgProfile Wear(int count=11){var p=new RpgProfile{PlayerLevel=1};foreach(var item in RpgDawnHealerCatalog.Items().Take(count)){p.Inventory.Add(item);p.Equipment[item.Slot!.Value]=item.InstanceId;}p.Health.CurrentHp=100;return p;}
 [Fact] public async Task EverySlotCanBeEquippedAndPersistedAtLevelOne()
 {
  var directory=Path.Combine(Path.GetTempPath(),"DawnEquip-"+Guid.NewGuid().ToString("N"));
  try{
   var store=new ViewerPointStore(directory);config.Enabled=true;var items=RpgDawnHealerCatalog.Items();
   await store.ApplyRpgAsync("test","Test","seed",a=>{a.Rpg.PlayerLevel=1;a.Rpg.Inventory.AddRange(items);return new(true);},default);
   var service=new RpgProgressionService(store,config);
   foreach(var item in items)Assert.True((await service.EquipAsync("test","Test","equip:"+item.ItemId,item.InstanceId,item.Slot!.Value,default)).Success);
   var saved=(await new ViewerPointStore(directory).GetProfileAsync("test",default)).Entry.Rpg;
   Assert.Equal(1,saved.PlayerLevel);Assert.Equal(11,saved.Equipment.Count);Assert.True(RpgSetCombatService.Active(saved,config.Sets,RpgDawnHealerCatalog.SetId,11));
  }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
 [Fact] public void CatalogSurvivesReloadAndRetainsLevelOneRequirement()
 {
  config.Validate();var restored=JsonSerializer.Deserialize<RpgConfig>(JsonSerializer.Serialize(config))!;restored.Validate();
  var items=restored.DiveLoot.Items.Where(i=>i.SetId==RpgDawnHealerCatalog.SetId).ToArray();
  Assert.Equal(11,items.Length);Assert.Equal(11,items.Select(i=>i.Slot).Distinct().Count());
  Assert.All(items,i=>{Assert.Equal(1,i.MinimumLevel);Assert.Equal(ItemRarity.Mythic,i.Rarity);Assert.Equal(i.ItemId,i.IconId);Assert.True(i.Attributes.HealingPower>0);});
  Assert.Single(restored.Sets,s=>s.Id==RpgDawnHealerCatalog.SetId);
  Assert.True(RpgSetCombatService.Active(Wear(),restored.Sets,RpgDawnHealerCatalog.SetId,11));
  Assert.False(RpgSetCombatService.Active(Wear(10),restored.Sets,RpgDawnHealerCatalog.SetId,11));
  Assert.All(RpgLootLocations.Build(restored).Where(i=>i.SetId==RpgDawnHealerCatalog.SetId),i=>{Assert.NotEmpty(i.Ranges);Assert.All(i.Sources,s=>Assert.Equal("dungeon",s.Kind));});
 }
 [Fact] public void HealingUnlockAndShieldRespectPieceThresholdsAndCap()
 {
  Assert.False(RpgGroupHealing.CanHeal(Wear(1),config));Assert.True(RpgGroupHealing.CanHeal(Wear(2),config));
  var target=new RpgProfile();target.Health.CurrentHp=RpgHealthService.Maximum(target,config.Health);
  var entries=new Dictionary<string,ViewerPointEntry>{{"a",new(){Rpg=Wear(8)}},{"b",new(){Rpg=target}}};var instance=new PartyInstance();
  RpgGroupHealing.Heal("a","b",10000,instance,id=>entries[id],config,new High(),now);Assert.Empty(instance.Shields);
  entries["a"].Rpg=Wear(9);RpgGroupHealing.Heal("a","b",10000,instance,id=>entries[id],config,new High(),now);
  Assert.Equal((int)(RpgHealthService.Maximum(target,config.Health)*config.Multiplayer.ShieldMaxHpPercent/100),instance.Shields["b"]);
 }
 [Fact] public void FullSetWaveHealsOtherLivingMembersOnceAndPersistsCooldown()
 {
  var entries=new Dictionary<string,ViewerPointEntry>{{"a",new(){Rpg=Wear()}},{"b",new(){Rpg=new()}},{"c",new(){Rpg=new()}},{"dead",new(){Rpg=new()}}};
  entries["b"].Rpg.Health.CurrentHp=1;entries["c"].Rpg.Health.CurrentHp=1;entries["dead"].Rpg.Health.CurrentHp=0;
  var party=new PartyState{Members=["a","b","c","dead"],Instance=new()};
  RpgGroupHealing.DawnWave("a",10,party,id=>entries[id],config,new High(),now);
  var healed=entries["b"].Rpg.Health.CurrentHp;Assert.True(healed>1);Assert.Equal(healed,entries["c"].Rpg.Health.CurrentHp);Assert.Equal(0,entries["dead"].Rpg.Health.CurrentHp);Assert.Equal(100,entries["a"].Rpg.Health.CurrentHp);
  entries["a"].Rpg=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(entries["a"].Rpg))!;
  RpgGroupHealing.DawnWave("a",10,party,id=>entries[id],config,new High(),now.AddSeconds(7));Assert.Equal(healed,entries["b"].Rpg.Health.CurrentHp);
  RpgGroupHealing.DawnWave("a",10,party,id=>entries[id],config,new High(),now.AddSeconds(8));Assert.True(entries["b"].Rpg.Health.CurrentHp>healed);
  entries["a"].Rpg.Equipment.Remove(EquipmentSlot.Belt);healed=entries["b"].Rpg.Health.CurrentHp;
  RpgGroupHealing.DawnWave("a",10,party,id=>entries[id],config,new High(),now.AddSeconds(16));Assert.Equal(healed,entries["b"].Rpg.Health.CurrentHp);
 }
}
