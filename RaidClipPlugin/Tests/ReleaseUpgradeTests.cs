using System.Reflection;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class ReleaseUpgradeTests
{
    [Fact] public void Existing_updater_preserves_saved_data_and_settings()
    {
        var root = Path.Combine(Path.GetTempPath(), "otter-upgrade-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "payload"); var target = Path.Combine(root, "installed");
        Directory.CreateDirectory(payload); Directory.CreateDirectory(Path.Combine(target, "minigame"));
        var save = Path.Combine(target, "minigame", "viewer-points.json");
        var settings = Path.Combine(target, "settings.json");
        const string data = "{\"1001\":{\"Points\":1234,\"Rpg\":{}}}";
        File.WriteAllText(save, data); File.WriteAllText(settings, "synthetic-settings");
        File.WriteAllText(Path.Combine(target, "RaidClipPlugin.exe"), "synthetic-2.1.22");
        File.WriteAllText(Path.Combine(payload, "RaidClipPlugin.exe"), "synthetic-2.1.23");
        typeof(UpdateApplier).GetMethod("CopyPayload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { payload, target });
        Assert.Equal("synthetic-2.1.23", File.ReadAllText(Path.Combine(target, "RaidClipPlugin.exe")));
        Assert.Equal(data, File.ReadAllText(save)); Assert.Equal("synthetic-settings", File.ReadAllText(settings));
    }
    [Fact] public void Regular_release_retains_updater_executable_contract()
    {
        Assert.Equal("RaidClipPlugin", typeof(UpdateService).Assembly.GetName().Name);
        Assert.Equal(new Version(2,5,1,0), typeof(UpdateService).Assembly.GetName().Version);
        Assert.False(UpdateApplier.TryRun(Array.Empty<string>()));
    }
}
