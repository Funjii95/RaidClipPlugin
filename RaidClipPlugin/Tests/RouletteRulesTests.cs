using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RouletteRulesTests
{
    [Theory]
    [InlineData("rot", RouletteBetKind.Red)]
    [InlineData("schwarz", RouletteBetKind.Black)]
    [InlineData("gerade", RouletteBetKind.Even)]
    [InlineData("ungerade", RouletteBetKind.Odd)]
    [InlineData("1-18", RouletteBetKind.Low)]
    [InlineData("19-36", RouletteBetKind.High)]
    [InlineData("0", RouletteBetKind.Number)]
    [InlineData("36", RouletteBetKind.Number)]
    public void SupportedBetsAreParsed(string input, RouletteBetKind kind)
    {
        Assert.True(RouletteRules.TryParseBet(input, out var bet));
        Assert.Equal(kind, bet.Kind);
    }

    [Theory]
    [InlineData("37")]
    [InlineData("-1")]
    [InlineData("blau")]
    [InlineData("")]
    public void InvalidBetsAreRejected(string input) =>
        Assert.False(RouletteRules.TryParseBet(input, out _));

    [Fact]
    public void ZeroOnlyWinsAsExactNumber()
    {
        Assert.True(RouletteRules.TryParseBet("0", out var zero));
        Assert.True(RouletteRules.IsWinner(zero, 0));
        foreach (var input in new[]
                 { "rot", "schwarz", "gerade", "ungerade", "niedrig", "hoch" })
        {
            Assert.True(RouletteRules.TryParseBet(input, out var bet));
            Assert.False(RouletteRules.IsWinner(bet, 0));
        }
    }

    [Theory]
    [InlineData(1, "rot")]
    [InlineData(2, "schwarz")]
    [InlineData(18, "gerade")]
    [InlineData(19, "ungerade")]
    [InlineData(12, "niedrig")]
    [InlineData(32, "hoch")]
    [InlineData(17, "17")]
    public void WinningBetsMatchEuropeanWheel(int number, string input)
    {
        Assert.True(RouletteRules.TryParseBet(input, out var bet));
        Assert.True(RouletteRules.IsWinner(bet, number));
    }
}
