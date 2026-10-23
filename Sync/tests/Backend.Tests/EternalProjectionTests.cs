using System.Text.Json;
namespace Backend.Tests;
public sealed class EternalProjectionTests
{
 [Fact]public async Task ActualFirestoreSdkPreservesBothZonesEternalDivineAndTheirNewAttributes()
 {
  await using var fixture=await TransactionalFirestoreFixture.StartAsync();
  var account=PublisherTests.Account.Replace("\"Inventory\":[]","""
   "SelectedDivingZone":"primordial_throne","SelectedFishingZone":"crystal_grotto",
   "Inventory":[{"InstanceId":"eternal","ItemId":"eternal_star_hunter_helmet","Rarity":8,"Slot":1,"Kind":1,"UpgradeLevel":9,"IsRefined":true,"Attributes":{"MaximumHp":230,"MaterialFind":25,"AttackPercent":50,"ExperienceBonus":50},"Effects":[]},{"InstanceId":"divine","ItemId":"divine_infinity_gloves","Rarity":9,"Slot":3,"Kind":1,"UpgradeLevel":0,"IsRefined":false,"Attributes":{"Power":10000,"DivineDamage":10000,"EnemyEncounterChance":1000},"Effects":[]}]
   """);
  await fixture.Sink.ApplyAsync(PublisherTests.Message()with{AccountJson=account});var player=(await fixture.Reader.GetAsync("1001",default))!;
  Assert.Equal("primordial_throne",player.Rpg.SelectedDivingZone);Assert.Equal("crystal_grotto",player.Rpg.SelectedFishingZone);Assert.Equal(2,player.Rpg.Inventory.Length);
  Assert.Equal(8,player.Rpg.Inventory[0].GetProperty("Rarity").GetInt32());Assert.True(player.Rpg.Inventory[0].GetProperty("IsRefined").GetBoolean());Assert.Equal(1000,player.Rpg.Inventory[1].GetProperty("Attributes").GetProperty("EnemyEncounterChance").GetDecimal());
 }
}
