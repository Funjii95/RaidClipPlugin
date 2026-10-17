using RaidClipPlugin.Config;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEnemyValidationTests
{
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
            case "chance":c.EncounterChance=0;break;
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
