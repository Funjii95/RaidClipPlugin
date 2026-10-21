using RaidClipPlugin.Config;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEnemyValidationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(50)] [InlineData(100)]
    public void FullPercentageRangeIsAcceptedAndPreserved(int chance)
    {
        var config=new RpgEnemyConfig {EncounterChance=chance};
        config.Validate();
        var restored=System.Text.Json.JsonSerializer.Deserialize<RpgEnemyConfig>(System.Text.Json.JsonSerializer.Serialize(config))!;
        restored.Validate();
        Assert.Equal(chance,restored.EncounterChance);
    }
    [Theory]
    [InlineData(-1)] [InlineData(101)]
    public void ValuesOutsidePercentageRangeAreRejected(int chance)
    {
        var config=new RpgEnemyConfig {EncounterChance=chance};
        Assert.Throws<InvalidOperationException>(config.Validate);
    }
    [Theory]
    [InlineData("chance","Begegnungschance")]
    [InlineData("rounds","Kampfrunden")]
    [InlineData("duplicate","mehrfach")]
    [InlineData("missingzero","Basisstufe")]
    [InlineData("hp","Lebenspunkte")]
    [InlineData("damage","Schadensbereich")]
    [InlineData("exp","EXP")]
    public void InvalidEnemyEditsIdentifyExactFieldWithoutSilentlyChangingBalance(string field,string expected)
    {
        var c=new RpgEnemyConfig();
        switch(field)
        {
            case "chance":c.EncounterChance=-1;break;
            case "rounds":c.MaximumRounds=0;break;
            case "duplicate":c.Tiers.Add(new(){MinimumStake=0});break;
            case "missingzero":c.Tiers[0].MinimumStake=1;break;
            case "hp":c.Tiers[0].HitPoints=0;break;
            case "damage":c.Tiers[0].MinimumDamage=10;c.Tiers[0].MaximumDamage=5;break;
            case "exp":c.Tiers[0].Experience=-1;break;
        }
        var before=System.Text.Json.JsonSerializer.Serialize(c);
        var error=Assert.Throws<InvalidOperationException>(c.Validate);
        Assert.Contains(expected,error.Message);Assert.Contains("wiederherstellen",error.Message);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(c));
        new RpgEnemyConfig().Validate();
    }
}
