using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class MaintenanceTests
{
    [Theory]
    [InlineData(null, 8, "Willkommen!")]
    [InlineData("Dennis", 8, "Guten Morgen, Dennis")]
    [InlineData("Dennis", 14, "Guten Tag, Dennis")]
    [InlineData("Dennis", 22, "Guten Abend, Dennis")]
    public void Greeting_uses_account_and_local_hour(string? name, int hour, string expected) =>
        Assert.Equal(expected, MainForm.AccountGreeting(name, hour));

    [Fact]
    public void Dennis_cannot_administer_another_cloud_channel()
    {
        CloudMaintenancePolicy.RequireOwner(["1001"], "1001");
        Assert.Throws<InvalidOperationException>(() => CloudMaintenancePolicy.RequireOwner(["1001"], "1002"));
        Assert.Throws<InvalidOperationException>(() => CloudMaintenancePolicy.RequireOwner(["1001", "1002"], "1001"));
    }

    [Fact]
    public void Credential_replacement_preserves_data_encrypts_backup_and_invalidates_both_tokens()
    {
        var root = TestDataPaths.Root;
        Directory.CreateDirectory(root);
        new TwitchCredentialStore().Save("old-client", "old-secret");
        File.WriteAllText(Path.Combine(root, "twitch-token.json"), "synthetic-legacy-token");
        File.WriteAllText(Path.Combine(root, "twitch-token.dat"), "synthetic-encrypted-token");
        var sentinel = Path.Combine(root, "cloud-sync-preservation-test.json");
        File.WriteAllText(sentinel, "unchanged");
        var backup = new PluginMaintenanceService().ReplaceTwitchCredentials("new-client", "new-secret");
        Assert.Equal("new-client", new TwitchCredentialStore().Load().ClientId);
        Assert.False(File.Exists(Path.Combine(root, "twitch-token.json")));
        Assert.False(File.Exists(Path.Combine(root, "twitch-token.dat")));
        Assert.Equal("unchanged", File.ReadAllText(sentinel));
        var previous = WindowsProtectedStore.UnprotectJson<Dictionary<string, byte[]?>>(File.ReadAllBytes(backup))!;
        Assert.Equal("old-client", WindowsProtectedStore.UnprotectJson<TwitchCredentials>(previous["twitch-credentials.dat"]!)!.ClientId);
        Assert.Equal("synthetic-legacy-token", System.Text.Encoding.UTF8.GetString(previous["twitch-token.json"]!));
        Assert.DoesNotContain("synthetic-legacy-token", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(backup)));

        // Fail after token invalidation: previous credentials and authorization must survive.
        File.WriteAllText(Path.Combine(root, "twitch-token.json"), "preserve-on-error");
        using (var locked = new FileStream(Path.Combine(root, "twitch-credentials.dat"), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.True(Record.Exception(() => new PluginMaintenanceService().ReplaceTwitchCredentials("failed", "failed")) is IOException or UnauthorizedAccessException);
        Assert.Equal("new-client", new TwitchCredentialStore().Load().ClientId);
        Assert.Equal("preserve-on-error", File.ReadAllText(Path.Combine(root, "twitch-token.json")));
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("minigame/viewer-points.json")]
    [InlineData("cloud-sync.json")]
    [InlineData("twitch-credentials.dat")]
    [InlineData("../RaidClipPlugin.exe")]
    public void Repair_rejects_data_and_traversal_before_any_write(string unexpected)
    {
        Assert.False(ApplicationFileRepair.IsApplicationFile(unexpected));
        var root = Path.Combine(Path.GetTempPath(), "repair-test-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "stage", "payload");
        var target = Path.Combine(root, "app");
        Directory.CreateDirectory(payload); Directory.CreateDirectory(target);
        try
        {
            File.WriteAllText(Path.Combine(payload, "RaidClipPlugin.exe"), "new");
            File.WriteAllText(Path.Combine(payload, "settings.json"), "must not overwrite");
            File.WriteAllText(Path.Combine(target, "RaidClipPlugin.exe"), "old");
            Assert.Throws<InvalidDataException>(() => ApplicationFileRepair.Apply(payload, target, (_, _) => Assert.Fail("No writes allowed")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(target, "RaidClipPlugin.exe")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Repair_accepts_the_documentation_shipped_by_the_windows_publish()
    {
        var root = Path.Combine(Path.GetTempPath(), "repair-publish-test-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "stage", "payload"); var target = Path.Combine(root, "app");
        Directory.CreateDirectory(payload); Directory.CreateDirectory(target);
        try
        {
            var published = new[] { "RaidClipPlugin.exe", "Microsoft.Web.WebView2.Core.xml", "Microsoft.Web.WebView2.WinForms.xml", "Microsoft.Web.WebView2.Wpf.xml" };
            foreach (var name in published) File.WriteAllText(Path.Combine(payload, name), "published content");
            ApplicationFileRepair.Apply(payload, target, (source, destination) => File.Copy(source, destination, true));
            foreach (var name in published) Assert.Equal("published content", File.ReadAllText(Path.Combine(target, name)));
            Assert.False(ApplicationFileRepair.IsApplicationFile("settings.xml"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Repair_restores_partial_copy_failure_and_removes_new_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "repair-test-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "stage", "payload"); var target = Path.Combine(root, "app");
        Directory.CreateDirectory(payload); Directory.CreateDirectory(target);
        try
        {
            File.WriteAllText(Path.Combine(payload, "RaidClipPlugin.exe"), "new exe");
            File.WriteAllText(Path.Combine(payload, "new.dll"), "new library");
            File.WriteAllText(Path.Combine(target, "RaidClipPlugin.exe"), "old exe");
            File.WriteAllText(Path.Combine(target, "settings.json"), "user settings");
            var copies = 0;
            Assert.Throws<IOException>(() => ApplicationFileRepair.Apply(payload, target, (source, destination) =>
            {
                File.Copy(source, destination, true);
                if (++copies == 2) throw new IOException("Synthetic interrupted copy");
            }));
            Assert.Equal("old exe", File.ReadAllText(Path.Combine(target, "RaidClipPlugin.exe")));
            Assert.False(File.Exists(Path.Combine(target, "new.dll")));
            Assert.Equal("user settings", File.ReadAllText(Path.Combine(target, "settings.json")));
            ApplicationFileRepair.Apply(payload, target, (source, destination) => File.Copy(source, destination, true));
            Assert.Equal("new exe", File.ReadAllText(Path.Combine(target, "RaidClipPlugin.exe")));
            Assert.Equal("user settings", File.ReadAllText(Path.Combine(target, "settings.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
