namespace RaidClipPlugin.Services;

internal enum ShellRewardKind { WhitePearl, BluePearl, RedPearl, Pearls, StoneChips }

// The plugin evaluates each opening independently using these approved integer ranges.
internal static class RpgShellOpeningRules
{
    internal const int MaximumBatchSize = 100;
    internal const int MinimumPearlReward = 100;
    internal const int MaximumPearlReward = 10000;

    internal static ShellRewardKind Reward(int roll) => roll switch
    {
        < 1 or > 100 => throw new ArgumentOutOfRangeException(nameof(roll)),
        <= 3 => ShellRewardKind.WhitePearl,
        <= 5 => ShellRewardKind.BluePearl,
        <= 6 => ShellRewardKind.RedPearl,
        <= 53 => ShellRewardKind.Pearls,
        _ => ShellRewardKind.StoneChips
    };

    internal static long RemainingShells(long availableShells, int count)
    {
        if (availableShells < 0)
            throw new ArgumentOutOfRangeException(nameof(availableShells));
        if (count < 1 || count > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (availableShells < count)
            throw new InvalidOperationException("Nicht genÃ¼gend Muscheln.");
        return availableShells - count;
    }
}