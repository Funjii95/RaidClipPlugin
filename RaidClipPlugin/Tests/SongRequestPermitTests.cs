using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class SongRequestPermitTests
{
    [Fact]
    public void PermitExpiresAfterExactlyFiveMinutesAndIsUserSpecific()
    {
        var permits = new SongRequestPermits();
        var now = DateTimeOffset.UtcNow;
        permits.Grant("one", now);
        Assert.True(permits.IsAllowed("one", now.AddMinutes(5).AddTicks(-1)));
        Assert.False(permits.IsAllowed("two", now));
        Assert.False(permits.IsAllowed("one", now.AddMinutes(5)));
    }

    [Fact]
    public void RenewingPermitStartsNewFiveMinuteWindow()
    {
        var permits = new SongRequestPermits();
        var now = DateTimeOffset.UtcNow;
        permits.Grant("one", now);
        permits.Grant("one", now.AddMinutes(4));
        Assert.True(permits.IsAllowed("one", now.AddMinutes(8)));
        Assert.False(permits.IsAllowed("one", now.AddMinutes(9)));
    }
}
