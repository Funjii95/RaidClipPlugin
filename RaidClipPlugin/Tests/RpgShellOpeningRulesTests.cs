using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgShellOpeningRulesTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(100, 100, 0)]
    [InlineData(150, 100, 50)]
    public void Exactly_one_shell_is_required_per_opening(long available, int count, long remaining)
        => Assert.Equal(remaining, RpgShellOpeningRules.RemainingShells(available, count));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Invalid_batch_is_rejected(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RpgShellOpeningRules.RemainingShells(1000, count));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 10)]
    [InlineData(99, 100)]
    public void Insufficient_shells_never_produce_partial_opening(long available, int count)
        => Assert.Throws<InvalidOperationException>(() => RpgShellOpeningRules.RemainingShells(available, count));

    [Fact]
    public void Corrupt_negative_balance_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => RpgShellOpeningRules.RemainingShells(-1, 1));
}