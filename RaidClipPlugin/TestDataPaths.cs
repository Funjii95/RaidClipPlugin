namespace RaidClipPlugin;

// Keep the historic helper name to avoid unrelated persistence changes.
internal static class TestDataPaths
{
    public static bool IsTestBuild => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT"));
    public static string LocalAppData { get; } = ResolveLocalAppData(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT"));
    public static string Root => Path.Combine(LocalAppData, "RaidClipPlugin");

    internal static string ResolveLocalAppData(string normalPath, string? isolatedPath)
    {
        var normal = Path.GetFullPath(normalPath).TrimEnd(Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(isolatedPath)) return normal;
        var full = Path.GetFullPath(isolatedPath).TrimEnd(Path.DirectorySeparatorChar);
        var original = Path.Combine(normal, "RaidClipPlugin");
        if (full.Equals(normal, StringComparison.OrdinalIgnoreCase) ||
            full.Equals(original, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(original + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An isolated test directory is required.");
        return full;
    }
}
