using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgLootTests
{
    private sealed class Lowest : IDuelRandom { public int NextInclusive(int min, int max) => min; }
    [Theory]
    [InlineData(0, ItemRarity.Uncommon)]
    [InlineData(99, ItemRarity.Uncommon)]
    [InlineData(100, ItemRarity.Rare)]
    [InlineData(499, ItemRarity.Rare)]
    [InlineData(500, ItemRarity.Epic)]
    [InlineData(4999, ItemRarity.Epic)]
    [InlineData(5000, ItemRarity.Legendary)]
    public void EnormousLuckCannotBypassStakeGate(long stake, ItemRarity expected)
    {
        var config = new RpgLootConfig();
        Assert.Equal(expected, RpgLootService.Chances(stake, decimal.MaxValue, config).MaximumRarity);
        config.BaseDropChance = config.MaximumDropChance = 100;
        config.RarityWeights = new() { [ItemRarity.Legendary] = 1 };
        var item = new RpgLootService(new Lowest()).Roll(stake, decimal.MaxValue, config, DateTimeOffset.UtcNow).Item;
        if (stake < 5000) Assert.Null(item);
        else Assert.Equal(ItemRarity.Legendary, item!.Rarity);
    }

    [Fact]
    public void ChanceSourcesAreSeparateAndShareTheSameLuckCap()
    {
        var config = new RpgLootConfig();
        var chance = RpgLootService.Chances(5000, 100, config, 100, 100);
        Assert.Equal(10, chance.Luck + chance.Sushi + chance.Equipment, 10);
        Assert.Equal(38, chance.Total);
        Assert.True(chance.Sushi > 0);
        Assert.True(chance.Equipment > 0);
    }

    [Fact]
    public void LegendaryMinimumAppliesEvenWithMisconfiguredLowTierAndNoSharedItemState()
    {
        var config = new RpgLootConfig { BaseDropChance = 100, MaximumDropChance = 100 };
        config.RiskTiers[0].MaximumRarity = ItemRarity.Legendary;
        Assert.Equal(ItemRarity.Epic, RpgLootService.Chances(0, 999, config).MaximumRarity);
        var service = new RpgLootService(new Lowest());
        var first = service.Roll(5000, 0, config, DateTimeOffset.UtcNow).Item!;
        var second = service.Roll(5000, 0, config, DateTimeOffset.UtcNow).Item!;
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        first.Attributes.Luck = 900;
        Assert.Equal(0, second.Attributes.Luck);
    }
}
