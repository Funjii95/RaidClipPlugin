namespace RaidClipPlugin.Services;

internal enum EquipmentUpgradeMaterial { WhitePearl, BluePearl, RedPearl }
internal sealed record EquipmentUpgradeRequirement(int TargetLevel, long PearlCost,
    EquipmentUpgradeMaterial? Material, int MaterialCount);

// Local 0.0.3 preparation only. No persisted item or gameplay mutation.
// Always pass the original attribute, never the result of a previous upgrade.
internal static class RpgEquipmentUpgradeRules
{
    internal const int MaximumLevel = 9;
    internal const decimal BonusPerLevel = 0.025m;

    internal static EquipmentUpgradeRequirement NextRequirement(int currentLevel)
    {
        if (currentLevel < 0 || currentLevel > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(currentLevel));
        if (currentLevel == MaximumLevel)
            throw new InvalidOperationException("Maximale Upgrade-Stufe +9 erreicht.");
        var target = currentLevel + 1;
        var cost = target switch
        {
            1 => 10_000L, 2 => 25_000L, 3 => 50_000L,
            4 => 100_000L, 5 => 200_000L, 6 => 350_000L,
            7 => 500_000L, 8 => 750_000L, 9 => 1_000_000L,
            _ => throw new InvalidOperationException()
        };
        EquipmentUpgradeMaterial? material = target switch
        {
            7 => EquipmentUpgradeMaterial.WhitePearl,
            8 => EquipmentUpgradeMaterial.BluePearl,
            9 => EquipmentUpgradeMaterial.RedPearl,
            _ => null
        };
        return new(target, cost, material, material.HasValue ? 1 : 0);
    }

    internal static decimal Multiplier(int level)
    {
        if (level < 0 || level > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return 1m + level * BonusPerLevel;
    }

    internal static decimal AttributeValue(decimal originalValue, int level) =>
        checked(originalValue * Multiplier(level));
}
