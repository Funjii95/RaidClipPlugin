namespace RaidClipPlugin.Services;

// Local 0.0.3 preparation only. No persisted item or gameplay mutation.
// Always pass the original attribute, never the result of a previous upgrade.
internal static class RpgEquipmentUpgradeRules
{
    internal const int MaximumLevel = 9;
    internal const decimal BonusPerLevel = 0.025m;

    internal static decimal Multiplier(int level)
    {
        if (level < 0 || level > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return 1m + level * BonusPerLevel;
    }

    internal static decimal AttributeValue(decimal originalValue, int level) =>
        checked(originalValue * Multiplier(level));
}