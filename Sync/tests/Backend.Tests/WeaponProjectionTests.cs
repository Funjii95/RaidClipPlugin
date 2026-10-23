using System.Text.Json;
namespace Backend.Tests;
public sealed class WeaponProjectionTests
{
 [Fact]public async Task ActualFirestoreSdkPreservesWeaponMagicManaAndShieldWithoutPrivateFields()
 {
  await using var fixture=await TransactionalFirestoreFixture.StartAsync();
  using var doc=JsonDocument.Parse(PublisherTests.Account);
  var account=System.Text.Json.Nodes.JsonNode.Parse(PublisherTests.Account)!;
  account["Rpg"]!["Health"]=JsonSerializer.SerializeToNode(new{CurrentHp=100,CurrentMana=37.125m,LastManaRegen=DateTimeOffset.UtcNow,ManaShieldHp=25,ManaShieldSource="weapon-instance",ManaShieldExpires=DateTimeOffset.UtcNow.AddSeconds(15)});
  account["Rpg"]!["Inventory"]=JsonSerializer.SerializeToNode(new[]{new{InstanceId="weapon-instance",ItemId="weapon_leviathan_call",Rarity=4,Slot=0,Kind=1,Attributes=new{Attack=300,MagicPower=350,ManaRegeneration=40,WaterDamage=45,ElementalDamage=40,AbyssDamage=50,LightDamage=50},Effects=Array.Empty<object>()}});
  await fixture.Sink.ApplyAsync(PublisherTests.Message()with{AccountJson=account.ToJsonString()});var player=(await fixture.Reader.GetAsync("1001",default))!;
  Assert.Equal(37.125m,player.Rpg.Health!.CurrentMana);Assert.Equal(25,player.Rpg.Health.ManaShieldHp);Assert.Equal("weapon-instance",player.Rpg.Health.ManaShieldSource);
  var a=player.Rpg.Inventory[0].GetProperty("Attributes");Assert.Equal(300,a.GetProperty("Attack").GetInt32());Assert.Equal(350,a.GetProperty("MagicPower").GetInt32());Assert.Equal(40,a.GetProperty("ManaRegeneration").GetInt32());Assert.Equal(45,a.GetProperty("WaterDamage").GetInt32());Assert.Equal(50,a.GetProperty("AbyssDamage").GetInt32());
 }
 [Theory][InlineData(-1)][InlineData(101)] public void InvalidManaIsRejectedBeforeProjection(int mana)
 {
  var account=System.Text.Json.Nodes.JsonNode.Parse(PublisherTests.Account)!;account["Rpg"]!["Health"]=JsonSerializer.SerializeToNode(new{CurrentMana=mana});
  Assert.Throws<InvalidDataException>(()=>SnapshotProtocol.SnapshotValidation.Validate(PublisherTests.Message()with{AccountJson=account.ToJsonString()},"42"));
 }
}
