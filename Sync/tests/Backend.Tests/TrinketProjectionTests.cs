using System.Text.Json.Nodes;
namespace Backend.Tests;
public sealed class TrinketProjectionTests
{
 [Fact] public async Task FirestoreRoundTripPreservesTwoTrinketSlotsAndCooldowns()
 {
  await using var fixture=await TransactionalFirestoreFixture.StartAsync();
  var account=JsonNode.Parse(PublisherTests.Account)!.AsObject();
  // Account fixtures use the persisted plugin envelope and PascalCase fields.
  var rpg=account["Rpg"]!.AsObject();
  rpg["Inventory"]=JsonNode.Parse("""[{"InstanceId":"one","ItemId":"trinket_shell_chip","Rarity":0,"Slot":9,"Kind":1,"Attributes":{"Attack":3},"Effects":[]},{"InstanceId":"two","ItemId":"trinket_crab_tooth","Rarity":0,"Slot":10,"Kind":1,"Attributes":{"Attack":3},"Effects":[]}]""");
  rpg["Equipment"]=JsonNode.Parse("""{"Trinket1":"one","Trinket2":"two"}""");
  rpg["Cooldowns"]=JsonNode.Parse("""{"trinket:trinket_shell_chip":"2026-10-29T12:00:04Z"}""");
  await fixture.Sink.ApplyAsync(PublisherTests.Message() with{AccountJson=account.ToJsonString()});
  var player=(await fixture.Reader.GetAsync("1001",default))!;
  Assert.Equal(9,player.Rpg.Inventory[0].GetProperty("Slot").GetInt32());
  Assert.Equal(10,player.Rpg.Inventory[1].GetProperty("Slot").GetInt32());
  Assert.Equal("one",player.Rpg.Equipment["Trinket1"]);Assert.Equal("two",player.Rpg.Equipment["Trinket2"]);
  Assert.True(player.Rpg.Cooldowns.ContainsKey("trinket:trinket_shell_chip"));
 }
}
