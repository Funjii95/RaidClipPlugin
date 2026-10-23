using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEquipmentUpgradeRulesTests
{
    [Theory]
    [InlineData(0, 100, null)]
    [InlineData(1, 250, null)]
    [InlineData(2, 500, null)]
    [InlineData(3, 1_000, null)]
    [InlineData(4, 2_000, null)]
    [InlineData(5, 3_500, null)]
    [InlineData(6, 5_000, "WhitePearl")]
    [InlineData(7, 8_000, "BluePearl")]
    [InlineData(8, 12_000, "RedPearl")]
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
    [InlineData(1, 103)]
    [InlineData(2, 106)]
    [InlineData(9, 145)]
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
        Assert.Equal(0.725m, RpgEquipmentUpgradeRules.AttributeValue(0.5m, 9));
        Assert.Equal(0m, RpgEquipmentUpgradeRules.AttributeValue(0m, 9));
        Assert.Equal(145m, RpgEquipmentUpgradeRules.AttributeValue(100m, 9));
    }

    [Fact]
    public void Overflow_is_rejected_instead_of_producing_a_wrapped_value()
        => Assert.Throws<OverflowException>(() => RpgEquipmentUpgradeRules.AttributeValue(decimal.MaxValue, 9));
}
