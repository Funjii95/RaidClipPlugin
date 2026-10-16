using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class AdventureGridThemeTests
{
    [Fact]
    public void TableHelpAndSelectionFollowThemeChanges()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var grid = new PropertyGrid();
                foreach (var background in new[] { Color.FromArgb(13, 13, 16), Color.FromArgb(250, 252, 253) })
                {
                    var text = background.R < 100 ? Color.WhiteSmoke : Color.FromArgb(28, 32, 38);
                    MainForm.StyleAdventureGrid(grid, background, background, text, Color.Gray, Color.DarkGray, Color.Maroon);
                    Assert.Equal(background, grid.ViewBackColor);
                    Assert.Equal(background, grid.HelpBackColor);
                    Assert.Equal(text, grid.ViewForeColor);
                    Assert.Equal(text, grid.HelpForeColor);
                    Assert.Equal(Color.Maroon, grid.SelectedItemWithFocusBackColor);
                    Assert.Equal(Color.DarkGray, grid.ViewBorderColor);
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(error);
    }
}
