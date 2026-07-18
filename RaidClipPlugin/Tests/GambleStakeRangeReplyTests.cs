using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class GambleStakeRangeReplyTests
{
    [Fact]
    public void StakeBelowMinimumShowsMinimumInsteadOfUsage()
    {
        var reply = ChatMinigameService.BuildGambleStakeRangeReply(
            "chrissy0711", 20, 50, 1000, false, "Punkt", "Punkte");

        Assert.Contains("@chrissy0711", reply);
        Assert.Contains("Mindesteinsatz", reply);
        Assert.Contains("50 Punkte", reply);
        Assert.DoesNotContain("<einsatz|all>", reply);
    }

    [Fact]
    public void StakeAboveMaximumShowsMaximumInsteadOfUsage()
    {
        var reply = ChatMinigameService.BuildGambleStakeRangeReply(
            "Funjii", 2000, 10, 1000, false, "Punkt", "Punkte");

        var normalized = reply.Replace(".", "").Replace(",", "");
        Assert.Contains("Maximaleinsatz", reply);
        Assert.Contains("1000 Punkte", normalized);
        Assert.DoesNotContain("<einsatz|all>", reply);
    }

    [Fact]
    public void AllInBelowMinimumShowsAllInHint()
    {
        var reply = ChatMinigameService.BuildGambleStakeRangeReply(
            "Funjii", 0, 10, 1000, true, "Punkt", "Punkte");

        Assert.Contains("All-in", reply);
        Assert.Contains("Mindesteinsatz", reply);
        Assert.DoesNotContain("<einsatz|all>", reply);
    }
}
