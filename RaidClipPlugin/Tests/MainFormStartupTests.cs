using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class MainFormStartupTests
{
    [Fact]
    public void MainWindowCanBeConstructedOnStaThread()
    {
        Exception? startupFailure = null;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                Assert.NotNull(form);
            }
            catch (Exception exception)
            {
                startupFailure = exception;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(20)),
            "Das Hauptfenster konnte nicht innerhalb von 20 Sekunden aufgebaut werden.");
        Assert.Null(startupFailure);
    }

    [Fact]
    public void InteractiveSettingsAndLiveChatToolbarsRemainReachable()
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                var primary = Assert.Single(form.Controls.Find("LiveChatPrimaryToolbar", true));
                var filters = Assert.Single(form.Controls.Find("LiveChatFilterToolbar", true));
                Assert.Single(form.Controls.Find("LiveChatComposer", true));
                Assert.Single(form.Controls.Find("LiveChatPage", true));
                Assert.Single(form.Controls.Find("OfficialLiveChatToolbar", true));
                Assert.Single(form.Controls.Find("OfficialTwitchChatWebView", true));
                Assert.Single(form.Controls.Find("OfficialLiveChatComposer", true));
                Assert.Single(form.Controls.Find("CustomCommandPasteInput", true));
                var importPreview = Assert.IsType<DataGridView>(Assert.Single(
                    form.Controls.Find("CustomCommandImportPreview", true)));
                Assert.Equal(DataGridViewAutoSizeRowsMode.None, importPreview.AutoSizeRowsMode);
                Assert.Equal(DataGridViewTriState.False, importPreview.DefaultCellStyle.WrapMode);
                Assert.False(importPreview.AllowUserToResizeRows);
                Assert.Equal(28, importPreview.RowTemplate.Height);
                Assert.True(importPreview.MinimumSize.Height >= 180);
                Assert.Single(form.Controls.Find("CustomCommandImportDetails", true));
                Assert.Contains(Descendants(form).OfType<CheckBox>(),
                    check => check.Text.Contains("7TV im Popout"));
                Assert.Contains(Descendants(form).OfType<CheckBox>(),
                    check => check.Text.Contains("BTTV im Popout"));
                Assert.True(((FlowLayoutPanel)primary).AutoScroll);
                Assert.True(((FlowLayoutPanel)filters).AutoScroll);
                var interactiveFlows = Descendants(form)
                    .OfType<FlowLayoutPanel>()
                    .Where(ContainsInteractiveSettings)
                    .ToArray();
                Assert.NotEmpty(interactiveFlows);
                Assert.All(interactiveFlows, flow => Assert.True(flow.AutoScroll,
                    $"Die Funktionsleiste '{flow.Name}' ist bei kleiner Fenstergröße nicht scrollbar."));
            }
            catch (Exception exception) { failure = exception; }
            finally { completed.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static bool ContainsInteractiveSettings(Control root) =>
        Descendants(root).Any(child => child is Button or CheckBox or TextBox or
            ComboBox or NumericUpDown or ListBox or ListView or DataGridView);
}
