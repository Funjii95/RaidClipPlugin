namespace RaidClipPlugin;

internal static class TestDataPaths
{
    public static bool IsTestBuild => true;
    public static string LocalAppData { get; } = Resolve();
    public static string Root => Path.Combine(LocalAppData, "RaidClipPlugin");
    private static string Resolve()
    {
        var normal = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var value = Environment.GetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT") ??
            Path.Combine(normal, "FunjiiOtterRpgIntegratedTest", "isolated-appdata");
        var full = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);
        if (full.Equals(normal, StringComparison.OrdinalIgnoreCase) ||
            full.Equals(Path.Combine(normal, "RaidClipPlugin"), StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(Path.Combine(normal, "RaidClipPlugin") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An isolated test directory is required.");
        return full;
    }
}
