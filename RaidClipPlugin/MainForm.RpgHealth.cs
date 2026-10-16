using RaidClipPlugin.Config;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly PropertyGrid _healthSettings=NewAdventureGrid();
    private readonly PropertyGrid _enemySettings=NewAdventureGrid();
    private Control BuildRpgEnemyPanel()
    {
        var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new(12)};
        panel.RowStyles.Add(new(SizeType.Absolute,70));panel.RowStyles.Add(new(SizeType.Percent,100));panel.RowStyles.Add(new(SizeType.Absolute,50));
        panel.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Unterwassergegner: 10–20 % pro gültigem Tauchgang. Stufen nach Einsatz; Tiers über (…) bearbeiten. Sieg: EXP und der bessere von zwei normalen Lootwürfen als Bonus. Legendary-Einsatzgrenzen bleiben erhalten. Keine zusätzlichen Perlenverluste. Benötigt aktives HP-System."},0,0);
        panel.Controls.Add(_enemySettings,0,1);var save=NewActionButton("Gegner-Einstellungen speichern");save.Click+=(_,_)=>SaveMinigameSettingsFromControls();panel.Controls.Add(save,0,2);return panel;
    }
    private Control BuildRpgHealthPanel()
    {
        _healthSettings.PropertySort=PropertySort.Categorized;
        var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new(12)};
        panel.RowStyles.Add(new(SizeType.Absolute,65));panel.RowStyles.Add(new(SizeType.Percent,100));panel.RowStyles.Add(new(SizeType.Absolute,50));
        panel.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Persistente HP: 0 bedeutet besiegt. Chat heilt nur normale Nachrichten, niemals Befehle oder Bots. Tod sperrt denselben Boss auch nach Heilung. Minimale HP sind stets 0; Offline-Heilung ist auf MaxHP begrenzt."},0,0);
        panel.Controls.Add(_healthSettings,0,1);
        var save=NewActionButton("HP-Einstellungen speichern");save.Click+=(_,_)=>SaveMinigameSettingsFromControls();panel.Controls.Add(save,0,2);
        return panel;
    }
}
