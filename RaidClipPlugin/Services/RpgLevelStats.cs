using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public static class RpgLevelStats
{
    public const int PointsPerLevel=5;
    public static int Bonus(RpgProfile profile,int maximumLevel=100)=>checked((Math.Clamp(profile.PlayerLevel,1,Math.Max(1,maximumLevel))-1)*PointsPerLevel);
}
