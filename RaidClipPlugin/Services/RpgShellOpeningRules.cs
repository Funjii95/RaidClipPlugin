namespace RaidClipPlugin.Services;

// Validation only until the operator defines and approves shell contents.
// The future mutation must debit shells and award contents in one existing RPG transaction.
internal static class RpgShellOpeningRules
{
    internal const int MaximumBatchSize = 100;

    internal static long RemainingShells(long availableShells, int count)
    {
        if (availableShells < 0)
            throw new ArgumentOutOfRangeException(nameof(availableShells));
        if (count < 1 || count > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (availableShells < count)
            throw new InvalidOperationException("Nicht genügend Muscheln.");
        return availableShells - count;
    }
}