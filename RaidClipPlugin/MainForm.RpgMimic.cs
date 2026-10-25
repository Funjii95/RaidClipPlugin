using RaidClipPlugin.Config;
namespace RaidClipPlugin;
public sealed partial class MainForm
{
    private readonly PropertyGrid _mimicSettings=NewAdventureGrid();
    private Control BuildRpgMimicPanel()
    {
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,80));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
        panel.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Zehn regionale Mimics: Fundchance aus dem jeweiligen Tauchgebiet. Gemeinsame HP wie beim Boss, 2× Gebiets-Boss-HP und mindestens 1,5× Basisangriff.\nKein neuer Boss während einer aktiven Runde oder Rundenpause. Nach Sieg: exakt 50 % Chance auf Ausrüstung des Fundgebiets je berechtigtem Überlebenden. Regionale Kampfwerte ersetzen die alten einzelnen HP-/Namenswerte.\nOBS-Browserquelle nur für Mimics: "+Services.AdventureOverlayServer.MimicUrl+" (900 × 600)."},0,0);
        panel.Controls.Add(_mimicSettings,0,1);var save=NewActionButton("Mimic-Einstellungen speichern");save.Click+=(_,_)=>SaveMinigameSettingsFromControls();panel.Controls.Add(save,0,2);return panel;
    }
}
