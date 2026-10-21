using RaidClipPlugin.Config;
namespace RaidClipPlugin;
public sealed partial class MainForm
{
    private readonly PropertyGrid _mimicSettings=NewAdventureGrid();
    private Control BuildRpgMimicPanel()
    {
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,80));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
        panel.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Gemeinsamer Tauchboss: 0,5 % Grundchance + 0,1 Prozentpunkte je Abgrundsinn-Rang (1 % bei 5/5).\nKein neuer Boss während einer aktiven Runde oder Rundenpause. Legendary-Würfe gelten je berechtigtem Überlebenden nach einem Sieg. Geänderte Kampfwerte gelten ab der nächsten Mimic."},0,0);
        panel.Controls.Add(_mimicSettings,0,1);var save=NewActionButton("Mimic-Einstellungen speichern");save.Click+=(_,_)=>SaveMinigameSettingsFromControls();panel.Controls.Add(save,0,2);return panel;
    }
}
