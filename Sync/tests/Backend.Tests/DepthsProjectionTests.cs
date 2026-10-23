using System.Text.Json;
namespace Backend.Tests;
public sealed class DepthsProjectionTests
{
    [Fact] public async Task ActualFirestoreSdkAndBackendPreserveRefinementCursesAndExpandedMaterials()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var account=PublisherTests.Account.Replace("\"Inventory\":[]", """
            "Materials":{"Shells":1,"WhitePearls":2,"BluePearls":3,"RedPearls":4,"StoneChips":5,"PearlDust":6,"LuminousScale":7,"RuneShell":8,"EternalDepthCore":9,"TrueCoralCore":10,"CursedSilk":11,"AbyssShard":12,"ReinforcementCore":13},
            "Inventory":[{"InstanceId":"cursed","ItemId":"cursed_greed_crown","Rarity":6,"Slot":1,"Kind":1,"UpgradeLevel":9,"IsRefined":true,"Attributes":{"CritChance":28,"HealingPower":-12,"MovementSpeed":-10,"BossLifeleech":8},"Effects":[]}]
            """);
        await fixture.Sink.ApplyAsync(PublisherTests.Message() with{AccountJson=account});var player=(await fixture.Reader.GetAsync("1001",default))!;
        Assert.Equal(13,player.Rpg.Materials!.ReinforcementCore);Assert.Equal(12,player.Rpg.Materials.AbyssShard);Assert.Equal(6,player.Rpg.Materials.PearlDust);
        var item=Assert.Single(player.Rpg.Inventory);Assert.True(item.GetProperty("IsRefined").GetBoolean());Assert.Equal(9,item.GetProperty("UpgradeLevel").GetInt32());Assert.Equal(-12,item.GetProperty("Attributes").GetProperty("HealingPower").GetDecimal());
    }
}
