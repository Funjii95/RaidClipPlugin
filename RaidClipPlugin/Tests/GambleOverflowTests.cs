using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class GambleOverflowTests
{
    [Fact]
    public async Task BalancesCanGrowBeyondTheOldInt32Limit()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new ViewerPointStore(directory);
            await store.AddPointsAsync(
                "viewer", "Viewer", int.MaxValue, 0,
                CancellationToken.None, long.MaxValue);
            var balance = await store.AddPointsAsync(
                "viewer", "Viewer", int.MaxValue, 0,
                CancellationToken.None, long.MaxValue);

            Assert.Equal(4_294_967_294L, balance);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(10_000_000, 10_000_000)]
    [InlineData(50_000_000, 10_000_000)]
    public void GambleAllNeverExceedsTenMillion(long available, int expected)
    {
        Assert.Equal(expected,
            ChatMinigameService.CalculateAllInStake(available));
    }

    [Fact]
    public async Task AddPointsAllUpdatesEveryStoredViewer()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new ViewerPointStore(directory);
            await store.SetPointsAsync("one", "One", 10, 0, CancellationToken.None, long.MaxValue);
            await store.SetPointsAsync("two", "Two", 20, 0, CancellationToken.None, long.MaxValue);
            var recipients = await store.AddPointsToAllAsync(50, 0, long.MaxValue, CancellationToken.None);
            Assert.Equal(2, recipients);
            Assert.Equal(60, await store.GetPointsAsync("one", CancellationToken.None));
            Assert.Equal(70, await store.GetPointsAsync("two", CancellationToken.None));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task AddJackpotSaturatesInsteadOfOverflowing()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "state.json"), (int.MaxValue - 5).ToString());
            var store = new ViewerPointStore(directory);
            var result = await store.AddJackpotAsync(100, 0, CancellationToken.None);
            Assert.Equal(int.MaxValue - 5, result.Previous);
            Assert.Equal(int.MaxValue, result.Current);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task AllInLossSaturatesJackpotInsteadOfOverflowing()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "state.json"),
                (int.MaxValue - 5).ToString());
            var store = new ViewerPointStore(directory);
            await store.SetPointsAsync(
                "viewer", "Viewer", int.MaxValue, 0,
                CancellationToken.None, int.MaxValue);

            var result = await store.ApplyCasinoAsync(
                "viewer", "Viewer", "Gamble",
                1_000, 0, 0, int.MaxValue,
                0, 0, 0, 1_000,
                false, 0, 100, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(
                int.MaxValue,
                await store.GetJackpotAsync(0, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task RepeatedLargeAllInLossesSaturateStatistics()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new ViewerPointStore(directory);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await store.SetPointsAsync(
                    "viewer", "Viewer", int.MaxValue, 0,
                    CancellationToken.None, int.MaxValue);
                var result = await store.ApplyCasinoAsync(
                    "viewer", "Viewer", "Gamble",
                    1_500_000_000, 0, 0, int.MaxValue,
                    0, 0, 0, 0,
                    false, 0, 100, CancellationToken.None);
                Assert.True(result.Success);
            }

            var profile = await store.GetProfileAsync(
                "viewer", CancellationToken.None);
            Assert.Equal(int.MaxValue, profile.Entry.DailyLoss);
            Assert.Equal(2, profile.Entry.GamesPlayed);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DumpJackpotReturnsRemovedAmountAndConfiguredStartValue()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "state.json"), "5000");
            var store = new ViewerPointStore(directory);

            var dumped = await store.ResetJackpotAsync(
                100, CancellationToken.None);

            Assert.Equal(5000, dumped.Previous);
            Assert.Equal(100, dumped.Current);
            Assert.Equal(
                100,
                await store.GetJackpotAsync(100, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "RaidClipPlugin.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
