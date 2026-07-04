using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class HeistCooldownTests
{
    [Theory]
    [InlineData(0, "0 Sek.")]
    [InlineData(12, "12 Sek.")]
    [InlineData(78, "1 Min. 18 Sek.")]
    public void RemainingCooldownIsFormattedForChat(int seconds, string expected)
    {
        Assert.Equal(expected,
            HeistService.FormatCooldown(TimeSpan.FromSeconds(seconds)));
    }
}
