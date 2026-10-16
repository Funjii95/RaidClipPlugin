using System.Reflection;
using RaidClipPlugin.Controls;
using Xunit;

namespace RaidClipPlugin.Tests;

[Collection("WinForms")]
public sealed class WheelLayoutTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Console.SetOut(originalOut); Console.SetError(originalError); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null) throw new Exception("Glücksrad-UI-Test fehlgeschlagen", failure);
    }

    [Fact]
    public void WheelGetsWholePageAndGiveawayLayoutIsRestoredOnTabChange() => Sta(() =>
    {
        using var form = new MainForm();
        var tabs = Assert.IsType<TabControl>(Assert.Single(form.Controls.Find("GiveawaySettingsTabs", true)));
        var layout = Assert.IsType<TableLayoutPanel>(tabs.Parent);
        void Select(string text)
        {
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(p => p.Text == text);
            // Without showing a window, explicitly exercise the same selection event.
            typeof(TabControl).GetMethod("OnSelectedIndexChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tabs, new object[] { EventArgs.Empty });
        }
        Select("Glücksrad");
        Assert.Equal(SizeType.Percent, layout.RowStyles[1].SizeType);
        Assert.Equal(0, layout.RowStyles[2].Height);
        Assert.Equal(0, layout.RowStyles[3].Height);
        Assert.False(Assert.Single(form.Controls.Find("GiveawayActions", true)).Visible);
        Assert.False(Assert.Single(form.Controls.Find("GiveawayParticipants", true)).Visible);
        var wheel = Assert.Single(form.Controls.Find("WheelLayout", true));
        Assert.Single(wheel.Controls.Find("WheelPreview", true));
        Assert.Equal("Bild auswählen …", Assert.Single(wheel.Controls.Find("WheelChooseImage", true)).Text);
        Select("Allgemein");
        Assert.Equal(SizeType.Absolute, layout.RowStyles[1].SizeType);
        Assert.Equal(330, layout.RowStyles[1].Height);
        Assert.Equal(118, layout.RowStyles[2].Height);
        Assert.Equal(SizeType.Percent, layout.RowStyles[3].SizeType);
    });

    [Fact]
    public void PreviewDrawsDraftWheelAndCustomDesignWithoutLockingImage() => Sta(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "wheel-preview-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var source = new Bitmap(20, 20))
            { using var g = Graphics.FromImage(source); g.Clear(Color.Magenta); source.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            using var preview = new WheelPreviewControl { Size = new Size(500, 500) };
            preview.SetDraft(new[] { "Überraschung", "Otter-Jubel", "Freie Auswahl", "Nochmal drehen" });
            using var normal = new Bitmap(500, 500); preview.DrawToBitmap(normal, preview.ClientRectangle);
            Assert.NotEqual(preview.BackColor.ToArgb(), normal.GetPixel(330, 300).ToArgb());
            preview.SetDesign(path);
            using (var unlocked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) Assert.True(unlocked.Length > 0);
            using var custom = new Bitmap(500, 500); preview.DrawToBitmap(custom, preview.ClientRectangle);
            Assert.NotEqual(normal.GetPixel(330, 300).ToArgb(), custom.GetPixel(330, 300).ToArgb());
            preview.SetDesign("");
            using var restored = new Bitmap(500, 500); preview.DrawToBitmap(restored, preview.ClientRectangle);
            Assert.Equal(normal.GetPixel(330, 300).ToArgb(), restored.GetPixel(330, 300).ToArgb());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    });
}
