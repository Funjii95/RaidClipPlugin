using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class ReleaseDataPathsTests
{
    [Fact] public void Normal_release_uses_original_location_without_migration()
    {
        var normal = Path.Combine(Path.GetTempPath(), "release-path-original");
        Assert.Equal(Path.GetFullPath(normal), TestDataPaths.ResolveLocalAppData(normal, null));
        Assert.Equal(Path.GetFullPath(normal), TestDataPaths.ResolveLocalAppData(normal, " "));
    }
    [Theory] [InlineData("")] [InlineData("RaidClipPlugin")] [InlineData("RaidClipPlugin/minigame")]
    public void Test_override_cannot_target_original_data(string suffix)
    {
        var normal = Path.Combine(Path.GetTempPath(), "release-path-original");
        Assert.Throws<InvalidOperationException>(() => TestDataPaths.ResolveLocalAppData(normal, Path.Combine(normal, suffix)));
    }
    [Fact] public void Separate_test_root_is_retained()
    {
        var normal = Path.Combine(Path.GetTempPath(), "release-path-original");
        var isolated = Path.Combine(Path.GetTempPath(), "release-path-isolated");
        Assert.Equal(Path.GetFullPath(isolated), TestDataPaths.ResolveLocalAppData(normal, isolated));
    }
}
