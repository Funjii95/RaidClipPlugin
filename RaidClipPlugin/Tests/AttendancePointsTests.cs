using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class AttendancePointsTests
{
    [Theory]
    [InlineData(false, false, 10)]
    [InlineData(false, true, 10)]
    [InlineData(true, false, 5)]
    [InlineData(true, true, 10)]
    public void SelectsConfiguredAttendanceRate(
        bool isLurking,
        bool lurkersReceiveNormalPoints,
        long expected)
    {
        var points = ChatMinigameService.CalculateAttendancePoints(
            isLurking,
            lurkersReceiveNormalPoints,
            normalPoints: 10,
            lurkerPoints: 5);

        Assert.Equal(expected, points);
    }
}
