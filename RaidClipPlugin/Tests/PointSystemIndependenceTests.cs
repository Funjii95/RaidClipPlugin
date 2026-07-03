using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class PointSystemIndependenceTests
{
    [Fact]
    public void PointSystem_StartsWithoutGames()
    {
        var config = new MinigameConfig
        {
            Enabled = false,
            PointsEnabled = true
        };

        Assert.True(ChatMinigameService.ShouldRun(config));
        Assert.True(ChatMinigameService.IsCommandModuleEnabled(config, "!punkte"));
        Assert.True(ChatMinigameService.IsCommandModuleEnabled(config, "!daily"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!gamble"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!roulette"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!jackpot"));
    }

    [Fact]
    public void Games_CanRunWithoutAwardingNewPoints()
    {
        var config = new MinigameConfig
        {
            Enabled = true,
            PointsEnabled = false
        };

        Assert.True(ChatMinigameService.ShouldRun(config));
        Assert.True(ChatMinigameService.IsCommandModuleEnabled(config, "!gamble"));
        Assert.True(ChatMinigameService.IsCommandModuleEnabled(config, "!jackpot"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!punkte"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!give"));
    }

    [Fact]
    public void BothDisabled_DoesNotStartService()
    {
        var config = new MinigameConfig
        {
            Enabled = false,
            PointsEnabled = false
        };

        Assert.False(ChatMinigameService.ShouldRun(config));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!punkte"));
        Assert.False(ChatMinigameService.IsCommandModuleEnabled(config, "!slots"));
    }
}
