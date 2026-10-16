using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class WheelTests
{
    [Fact]
    public void SpinSnapshotsIndependentFieldsAndRejectsOverlappingSpins()
    {
        var now = DateTimeOffset.UtcNow;
        var wheel = new WheelService(() => now, count => count - 1);
        var config = new WheelConfig { Entries = new() { " A ", "B", "C" }, SpinSeconds = 5 };
        var spin = wheel.Spin(config);
        Assert.Equal(2, spin.WinnerIndex); Assert.Equal("A", spin.Entries[0]);
        config.Entries[2] = "changed";
        Assert.Equal("C", spin.Entries[2]);
        Assert.Throws<InvalidOperationException>(() => wheel.Spin(config));
        Assert.Throws<InvalidOperationException>(() => wheel.Reset());
        now = now.AddSeconds(6);
        Assert.False(wheel.IsSpinning);
        wheel.Reset(); Assert.Empty(wheel.State.Entries);
    }
    [Fact]
    public void InvalidEntriesAreRejectedAndWheelConfigPersistsSeparately()
    {
        Assert.Throws<InvalidOperationException>(() => new WheelConfig { Entries = new() { "one" } }.Validate());
        Assert.Throws<InvalidOperationException>(() => new WheelConfig { Entries = new() { "one", new string('x',81) } }.Validate());
        var config = new GiveawayConfig { Enabled = false, Wheel = new() { Entries = new() { "Dance", "Otter" }, SpinSeconds = 12, DesignImagePath = "custom.png" } };
        var reloaded = JsonSerializer.Deserialize<GiveawayConfig>(JsonSerializer.Serialize(config))!;
        Assert.False(reloaded.Enabled); Assert.Equal(12, reloaded.Wheel.SpinSeconds);
        Assert.Equal("Otter", reloaded.Wheel.Entries[1]); Assert.Equal("custom.png", reloaded.Wheel.DesignImagePath);
    }
    [Fact]
    public async Task BrowserSourceServesLocalDesignAndDoesNotExposeItsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RaidClipWheelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var cts = new CancellationTokenSource();
        Task? run = null;
        try
        {
            var imagePath = Path.Combine(directory, "design.png");
            using (var bitmap = new Bitmap(8,8)) bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            var wheel = new WheelService(pick: _ => 0);
            wheel.Spin(new WheelConfig { Entries = new() { "<script>alert('x')</script>", "B" }, DesignImagePath = imagePath });
            Assert.True(wheel.State.HasDesign);
            var reservation = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            reservation.Start(); var port = ((System.Net.IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            run = new WheelOverlayServer(wheel, port).RunAsync(cts.Token);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://127.0.0.1:{port}/";
            var html = await http.GetStringAsync(url);
            Assert.Contains("drawImage", html); Assert.Contains("textContent", html);
            var state = await http.GetStringAsync(url + "state");
            Assert.DoesNotContain("design.png", state); Assert.DoesNotContain(directory, state);
            Assert.Equal(0, JsonSerializer.Deserialize<WheelState>(state)!.WinnerIndex);
            var bytes = await http.GetByteArrayAsync(url + "design");
            Assert.Equal(File.ReadAllBytes(imagePath), bytes);
            using var post = await http.PostAsync(url + "state", null);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, post.StatusCode);
            using var unknown = await http.GetAsync(url + "other-file");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            cts.Cancel(); if (run is not null) await run.WaitAsync(TimeSpan.FromSeconds(5));
            Directory.Delete(directory, true);
        }
    }
}
