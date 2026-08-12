using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class ChatTimerTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(10, 9, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 25, true)]
    public void ViewerThresholdControlsPosting(
        int minimumViewers,
        int currentViewers,
        bool expected)
    {
        Assert.Equal(expected,
            ChatTimerService.ShouldPost(minimumViewers, currentViewers));
    }

    [Fact]
    public void EnabledTimerRequiresAnEnabledEntry()
    {
        var config = new ChatTimerConfig { Enabled = true };

        Assert.Throws<InvalidOperationException>(() =>
            ConfigurationService.ValidateTimerSettings(config));
    }

    [Fact]
    public void ValidTimerConfigurationIsAccepted()
    {
        var config = new ChatTimerConfig
        {
            Enabled = true,
            Entries = new List<ChatTimerEntryConfig>
            {
                new()
                {
                    Enabled = true,
                    Message = "Folgt dem Kanal!",
                    IntervalMinutes = 15,
                    MinimumViewers = 20
                }
            }
        };

        ConfigurationService.ValidateTimerSettings(config);
    }
}
