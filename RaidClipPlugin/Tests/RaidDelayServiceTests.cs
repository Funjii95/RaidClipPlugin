using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RaidDelayServiceTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(15, 15)]
    [InlineData(600, 600)]
    [InlineData(999, 600)]
    public void DelayIsClampedSafely(int configured, int expected)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expected),
            RaidDelayService.GetDelay(configured));
    }
}
