using System.Runtime.CompilerServices;
namespace RaidClipPlugin.Tests;
internal static class ReleaseTestIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT")))
            Environment.SetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT",
                Path.Combine(Path.GetTempPath(), "otter-tests", Guid.NewGuid().ToString("N"), "appdata"));
    }
}
