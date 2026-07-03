namespace RaidClipPlugin.Services;

public static class RaidDelayService
{
    public const int MaximumDelaySeconds = 600;

    public static TimeSpan GetDelay(int seconds) =>
        TimeSpan.FromSeconds(Math.Clamp(seconds, 0, MaximumDelaySeconds));
}
