using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using RaidClipPlugin.Models;
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

    [Fact]
    public void AdBreakTemplateUsesDurationAndType()
    {
        var adBreak = new AdBreakEvent(
            90,
            new DateTimeOffset(2026, 8, 12, 12, 30, 0, TimeSpan.Zero),
            true,
            "Streamer");

        var text = MainForm.FormatAdBreakMessage(
            "{duration}s / {minutes} Min. / {type} / {requester}",
            adBreak);

        Assert.Equal("90s / 2 Min. / automatisch / Streamer", text);
    }

    [Fact]
    public void EventTemplateUsesTipAndSubscriptionFields()
    {
        var alert = new ChatAlertEvent(
            ChatAlertKind.Tip,
            "Benjamin",
            12.50m,
            "EUR",
            "Weiter so!",
            Months: 7,
            Quantity: 3,
            IsGift: true,
            Provider: "StreamElements");

        var text = MainForm.FormatChatAlert(
            "{user}: {amount} {currency} via {provider}; {message}; {months}; {quantity}; {gift}",
            alert);

        Assert.Equal(
            "Benjamin: 12.5 EUR via StreamElements; Weiter so!; 7; 3; ja",
            text);
    }

    [Fact]
    public void EnabledTipTriggerRequiresProvider()
    {
        var config = new EventTriggerConfig { Enabled = true };
        config.Tip.Enabled = true;

        Assert.Throws<InvalidOperationException>(() =>
            ConfigurationService.ValidateEventTriggerSettings(config));
    }
}
