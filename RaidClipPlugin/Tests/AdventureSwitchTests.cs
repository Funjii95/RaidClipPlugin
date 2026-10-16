using System.ComponentModel;
using RaidClipPlugin.Config;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class AdventureSwitchTests
{
    [Fact]
    public void CheckboxesLoadAndSaveAllGameBooleansWithoutTrueFalseRows()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var grid = new PropertyGrid();
                using var switches = MainForm.BuildAdventureSwitches(grid);
                foreach (var config in new object[] { new SoloAdventureConfig(), new DiveAdventureConfig(), new RaceAdventureConfig(), new BossAdventureConfig() })
                {
                    grid.SelectedObject = config;
                    var properties = TypeDescriptor.GetProperties(config).Cast<PropertyDescriptor>().Where(p => p.PropertyType == typeof(bool)).ToArray();
                    Assert.Equal(properties.Length, switches.Controls.Count);
                    foreach (var property in properties)
                    {
                        Assert.False(property.IsBrowsable);
                        var check = Assert.IsType<CheckBox>(switches.Controls[property.Name]);
                        Assert.Equal(property.GetValue(config), check.Checked);
                        check.Checked = !check.Checked;
                        Assert.Equal(check.Checked, property.GetValue(config));
                    }
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(error);
    }
}
