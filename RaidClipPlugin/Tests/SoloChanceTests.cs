using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class SoloChanceTests
{
    [Theory]
    [InlineData("33", 33)] [InlineData("33/100", 33)] [InlineData(" 3 / 100 ", 3)]
    [InlineData("0/100", 0)] [InlineData("100/100", 100)]
    public void ChanceDisplayAcceptsBothFormats(string input, int expected)
    {
        var property = System.ComponentModel.TypeDescriptor.GetProperties(typeof(SoloAdventureConfig))[nameof(SoloAdventureConfig.LossChance)]!;
        Assert.Equal(expected, property.Converter.ConvertFromString(input));
        Assert.Equal($"{expected}/100", property.Converter.ConvertToString(expected));
    }

    [Theory]
    [InlineData("33/50")] [InlineData("101/100")] [InlineData("-1")]
    [InlineData("33/100/100")] [InlineData("abc")]
    public void ChanceDisplayRejectsInvalidFractions(string input) =>
        Assert.Throws<FormatException>(() => new ChanceOutOf100Converter().ConvertFromString(input));
    [Theory]
    [InlineData(1, 0)] [InlineData(50, 0)] [InlineData(51, 1)] [InlineData(80, 1)]
    [InlineData(81, 2)] [InlineData(97, 2)] [InlineData(98, 3)] [InlineData(100, 3)]
    public void DefaultFishingBoundaryMatchesExistingChances(int roll, int tier) =>
        Assert.Equal(tier, AdventureGameService.SoloOutcome("!fish", roll, new SoloAdventureConfig()).Tier);

    [Fact]
    public void CustomChancesCanDisableTiersAndUseDecimalMultipliers()
    {
        var config = new SoloAdventureConfig { LossChance = 0, SmallChance = 100, LargeChance = 0, RareChance = 0, SmallMultiplier = 1.5m };
        foreach (var roll in Enumerable.Range(1, 100))
        {
            var result = AdventureGameService.SoloOutcome("!fish", roll, config);
            Assert.Equal(1, result.Tier); Assert.Equal(1.5m, result.Multiplier);
        }
    }

    [Fact]
    public void InvalidChanceSumAndNegativeMultipliersAreRejected()
    {
        var config = new SoloAdventureConfig { RareChance = 5 };
        Assert.Throws<InvalidOperationException>(() => config.Validate("Fish"));
        config.RareChance = 3; config.RareMultiplier = -1;
        Assert.Throws<InvalidOperationException>(() => config.Validate("Fish"));
        config.RareMultiplier = 101;
        Assert.Throws<InvalidOperationException>(() => config.Validate("Fish"));
    }

    [Fact]
    public void OldSavedSettingsKeepDifferentFishingAndDiveDefaults()
    {
        var config = JsonSerializer.Deserialize<AdventureGamesConfig>("{\"Fishing\":{\"Enabled\":true},\"Treasure\":{\"Enabled\":true}}")!;
        Assert.Equal(3, config.Fishing.RareChance); Assert.Equal(8m, config.Fishing.RareMultiplier);
        Assert.Equal(5, config.Treasure.RareChance); Assert.Equal(6m, config.Treasure.RareMultiplier);
        config.Treasure.LossChance = 0; config.Treasure.SmallChance = 0; config.Treasure.LargeChance = 0; config.Treasure.RareChance = 100;
        var reload = JsonSerializer.Deserialize<AdventureGamesConfig>(JsonSerializer.Serialize(config))!;
        Assert.Equal(3, AdventureGameService.SoloOutcome("!dive", 1, reload.Treasure).Tier);
    }
}
