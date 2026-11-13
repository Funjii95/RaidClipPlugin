using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RpgTankTests
{
 readonly RpgConfig c=new();
 readonly DateTimeOffset now=DateTimeOffset.Parse("2026-10-29T12:00:00Z");
 sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
 sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 RpgProfile Wear(string set,int count=8){var p=new RpgProfile{PlayerLevel=99};foreach(var i in RpgTankCatalog.Items().Where(x=>x.SetId=="tank_"+set).Take(count)){i.InstanceId=Guid.NewGuid().ToString("N");p.Inventory.Add(i);p.Equipment[i.Slot!.Value]=i.InstanceId;}RpgHealthService.Refresh(p,c.Health,now);return p;}
 [Fact] public void CatalogAndLegacyEnumsAreStable(){c.Validate();Assert.Equal(27,RpgTankCatalog.Items().Count);Assert.Equal(27,RpgTankCatalog.Items().Select(x=>x.ItemId).Distinct().Count());Assert.Equal(9,(int)ItemRarity.Divine);Assert.Equal(10,(int)ItemRarity.Mythic);Assert.Equal(7,(int)EquipmentSlot.Amulet);Assert.Equal(8,(int)EquipmentSlot.Belt);Assert.All(RpgTankCatalog.Sets(),s=>Assert.Equal(8,s.ItemIds.Distinct().Count()));}
 [Fact] public void LegacySalvageConfigAcquiresMythic(){c.Salvage.Values.Remove(ItemRarity.Mythic);c.Salvage.Validate();Assert.True(c.Salvage.Values.ContainsKey(ItemRarity.Mythic));}
 [Theory][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)] public void SetThresholdsRequireDistinctEquippedPieces(int n){var p=Wear("coral",n);Assert.True(RpgTankCombat.Has(p,c.Health,"coral",n));Assert.False(RpgTankCombat.Has(p,c.Health,"coral",n+1));p.Inventory.Add(p.Inventory[0]);Assert.False(RpgTankCombat.Has(p,c.Health,"coral",n+1));}
 [Fact] public void BlockIsCapped(){var p=Wear("coral");foreach(var i in p.Inventory)i.Attributes.BlockChance=100;Assert.Equal(75,CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now).BlockChance);}
 [Fact] public void SmallerShieldDoesNotEraseRescueBarrier(){var p=Wear("sentinel");RpgTankCombat.Shield(p,c.Health,200,.4m);var original=p.Health.TankBarrierHp;RpgTankCombat.Shield(p,c.Health,10,.15m,true);Assert.True(p.Health.TankBarrierHp>=original);}
 [Fact] public void LethalRescueHasPersistentCooldown(){var p=Wear("sentinel");p.Health.CurrentHp=40;Assert.Equal(39,RpgTankCombat.Lethal(99999,p,c,now));Assert.True(p.Health.TankBarrierHp>0);var restored=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(p))!;Assert.Equal(99999,RpgTankCombat.Lethal(99999,restored,c,now.AddSeconds(1)));}
 [Fact] public void AvatarExpiresWithoutKillingPlayerAndCannotBeResetByUnequip(){var p=Wear("leviathan");var max=RpgHealthService.Maximum(p,c.Health);p.Health.CurrentHp=max/10;RpgTankCombat.Before(p,c,10,true,now);Assert.True(RpgTankCombat.Active(p,"avatar",now));Assert.Equal(max+max/2,RpgHealthService.Maximum(p,c.Health));p.Health.CurrentHp=20;RpgTankCombat.Refresh(p,c.Health,now.AddSeconds(11));Assert.Equal(20,p.Health.CurrentHp);Assert.Equal(max,RpgHealthService.Maximum(p,c.Health));p.Equipment.Remove(EquipmentSlot.Belt);RpgTankCombat.Refresh(p,c.Health,now.AddSeconds(12));Assert.True(RpgTankCombat.Active(p,"avatar-cd",now.AddSeconds(12)));}
 [Fact] public void HeavyHitCapDoesNotApplyToOrdinaryHits(){var p=Wear("leviathan",5);var max=RpgHealthService.Maximum(p,c.Health);Assert.Equal((int)Math.Ceiling(max*.35m),RpgTankCombat.Defend(max*2,p,c,new High(),now,true));Assert.Equal(max*2,RpgTankCombat.Defend(max*2,p,c,new High(),now,false));}
 [Fact] public void EpicChestReducesOnlyEveryFifthHit(){var p=new RpgProfile();var item=RpgTankCatalog.Items().Single(i=>i.ItemId=="tank_depth_chest");p.Inventory.Add(item);p.Equipment[item.Slot!.Value]=item.InstanceId;RpgHealthService.Refresh(p,c.Health,now);for(int i=0;i<4;i++)Assert.Equal(100,RpgTankCombat.Defend(100,p,c,new High(),now,false));Assert.Equal(75,RpgTankCombat.Defend(100,p,c,new High(),now,false));}
 [Fact] public void EndgameTanksDoNotLeakIntoAreaBossPools(){foreach(var boss in c.AreaBosses.Bosses)Assert.DoesNotContain(RpgLootLocations.BossPool(c,boss),i=>i.SetId is "tank_sentinel" or "tank_leviathan");}

 [Fact] public void MultipleGuardiansRedirectOnlyOnce(){var target=Wear("coral",0);target.Health.CurrentHp=10;var a=Wear("leviathan",7);var b=Wear("leviathan",7);var hp=a.Health.CurrentHp;var other=b.Health.CurrentHp;var accounts=new Dictionary<string,ViewerPointEntry>{{"1",new(){Rpg=target}},{"2",new(){Rpg=a}},{"3",new(){Rpg=b}}};var party=new SnapshotProtocol.PartyState{Members=["1","2","3"],Instance=new()};Assert.Equal(80,RpgTankCombat.Protect("1",100,party,id=>accounts[id],c,now));Assert.Equal(hp-10,a.Health.CurrentHp);Assert.Equal(other,b.Health.CurrentHp);}
 [Fact] public void GroupReductionUsesStrongestEffect(){var target=Wear("coral",0);var a=Wear("sentinel");var b=Wear("sentinel");a.Cooldowns["tank:sentinel"]=now.AddSeconds(6);b.Cooldowns["tank:sentinel"]=now.AddSeconds(6);var accounts=new Dictionary<string,ViewerPointEntry>{{"1",new(){Rpg=target}},{"2",new(){Rpg=a}},{"3",new(){Rpg=b}}};Assert.Equal(80,RpgTankCombat.Protect("1",100,new(){Members=["1","2","3"],Instance=new()},id=>accounts[id],c,now));}
 [Fact] public void UnequipRemovesStatsButPreservesCooldowns(){var p=Wear("coral");var before=RpgRoleService.Threat(p,c,100,0);p.Equipment.Clear();RpgTankCombat.Refresh(p,c.Health,now);Assert.False(RpgTankCombat.Has(p,c.Health,"coral",2));Assert.True(RpgRoleService.Threat(p,c,100,0)<before);Assert.Equal(0,p.Health.TankBarrierHp);}
}
