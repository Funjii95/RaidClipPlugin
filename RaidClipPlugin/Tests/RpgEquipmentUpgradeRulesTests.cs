using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEquipmentUpgradeRulesTests
{
    [Theory]
    [InlineData(0, 10_000, null)]
    [InlineData(1, 25_000, null)]
    [InlineData(2, 50_000, null)]
    [InlineData(3, 100_000, null)]
    [InlineData(4, 200_000, null)]
    [InlineData(5, 350_000, null)]
    [InlineData(6, 500_000, "WhitePearl")]
    [InlineData(7, 750_000, "BluePearl")]
    [InlineData(8, 1_000_000, "RedPearl")]
    public void Next_step_requires_the_approved_cost_and_one_matching_material(int current, long cost, string? material)
    {
        var requirement = RpgEquipmentUpgradeRules.NextRequirement(current);
        Assert.Equal(current + 1, requirement.TargetLevel);
        Assert.Equal(cost, requirement.PearlCost);
        Assert.Equal(material, requirement.Material?.ToString());
        Assert.Equal(material is null ? 0 : 1, requirement.MaterialCount);
    }

    [Fact]
    public void Maximum_level_has_no_further_purchase()
        => Assert.Throws<InvalidOperationException>(() => RpgEquipmentUpgradeRules.NextRequirement(9));

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Invalid_current_level_cannot_produce_a_purchase(int level)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RpgEquipmentUpgradeRules.NextRequirement(level));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 102.5)]
    [InlineData(2, 105)]
    [InlineData(9, 122.5)]
    public void Bonus_is_additive_to_the_original_value(int level, double expected)
        => Assert.Equal((decimal)expected, RpgEquipmentUpgradeRules.AttributeValue(100m, level));

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Invalid_upgrade_level_is_rejected(int level)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RpgEquipmentUpgradeRules.AttributeValue(100m, level));

    [Fact]
    public void Fractional_values_are_preserved_without_rounding_or_compounding()
    {
        Assert.Equal(0.6125m, RpgEquipmentUpgradeRules.AttributeValue(0.5m, 9));
        Assert.Equal(0m, RpgEquipmentUpgradeRules.AttributeValue(0m, 9));
        Assert.Equal(122.5m, RpgEquipmentUpgradeRules.AttributeValue(100m, 9));
    }

    [Fact]
    public void Overflow_is_rejected_instead_of_producing_a_wrapped_value()
        => Assert.Throws<OverflowException>(() => RpgEquipmentUpgradeRules.AttributeValue(decimal.MaxValue, 9));
}
