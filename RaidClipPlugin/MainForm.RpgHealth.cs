using RaidClipPlugin.Config;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly PropertyGrid _healthSettings=NewAdventureGrid();
    private readonly PropertyGrid _enemySettings=NewAdventureGrid();
    private readonly DataGridView _enemyTierEditor=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false};
    private readonly CheckBox _enemyEnabled=new(){Text="Unterwassergegner aktiv",AutoSize=true};
    private readonly NumericUpDown _enemyChance=new(){Minimum=10,Maximum=20,DecimalPlaces=2,Value=15,Width=110};
    private readonly NumericUpDown _enemyRounds=new(){Minimum=1,Maximum=20,Value=6,Width=110};
    private RpgEnemyConfig _lastLoadedEnemies=new();
    private Control BuildRpgEnemyPanel()
    {
        var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new(12)};
        panel.RowStyles.Add(new(SizeType.Absolute,65));panel.RowStyles.Add(new(SizeType.Absolute,65));panel.RowStyles.Add(new(SizeType.Percent,100));panel.RowStyles.Add(new(SizeType.Absolute,95));
        panel.Controls.Add(new Label {Dock=DockStyle.Fill,Text="Unterwassergegner: 10–20 % pro gültigem Tauchgang. Jede Stufe braucht einen eindeutigen Mindesteinsatz; eine Stufe muss bei 0 beginnen. Sieg: EXP und der bessere von zwei normalen Lootwürfen. Keine zusätzlichen Perlenverluste; aktives HP-System erforderlich."},0,0);
        var fields=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};fields.Controls.AddRange(new Control[]{_enemyEnabled,new Label{Text="Chance (%)",AutoSize=true},_enemyChance,new Label{Text="Kampfrunden",AutoSize=true},_enemyRounds});panel.Controls.Add(fields,0,1);
        foreach(var title in new[]{"Mindesteinsatz","Name","Lebenspunkte","Min. Schaden","Max. Schaden","EXP"})_enemyTierEditor.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,Width=135,SortMode=DataGridViewColumnSortMode.NotSortable});
        _enemySettings.SelectedObjectsChanged+=(_,_)=>{if(_enemySettings.SelectedObject is RpgEnemyConfig c)LoadEnemyEditor(c);};
        panel.Controls.Add(_enemyTierEditor,0,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};var add=NewActionButton("Stufe hinzufügen");var remove=NewActionButton("Ausgewählte Stufe entfernen");var restore=NewActionButton("Zuletzt geladenen Stand wiederherstellen");var save=NewActionButton("Gegner-Einstellungen speichern");
        add.Click+=(_,_)=>{try{var c=ReadEnemyEditor();if(c.Tiers.Count>=30)throw new InvalidOperationException("Maximal 30 Stufen.");var stake=checked(c.Tiers.Max(t=>t.MinimumStake)+1);_enemyTierEditor.Rows.Add(stake,"Unterwassergegner",25,3,6,30);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Unterwassergegner");}};
        remove.Click+=(_,_)=>{if(_enemyTierEditor.CurrentRow is {} row && _enemyTierEditor.Rows.Count>1)_enemyTierEditor.Rows.Remove(row);};
        restore.Click+=(_,_)=>LoadEnemyEditor(_lastLoadedEnemies);
        save.Click+=(_,_)=>SaveMinigameSettingsFromControls();actions.Controls.AddRange(new Control[]{add,remove,restore,save});panel.Controls.Add(actions,0,3);return panel;
    }
    private void LoadEnemyEditor(RpgEnemyConfig c)
    {
        _lastLoadedEnemies=System.Text.Json.JsonSerializer.Deserialize<RpgEnemyConfig>(System.Text.Json.JsonSerializer.Serialize(c))!;
        _enemyEnabled.Checked=c.Enabled;_enemyChance.Value=Math.Clamp(c.EncounterChance,10,20);_enemyRounds.Value=Math.Clamp(c.MaximumRounds,1,20);
        _enemyTierEditor.Rows.Clear();foreach(var t in c.Tiers)_enemyTierEditor.Rows.Add(t.MinimumStake,t.Name,t.HitPoints,t.MinimumDamage,t.MaximumDamage,t.Experience);
    }
    private RpgEnemyConfig ReadEnemyEditor()
    {
        _enemyTierEditor.EndEdit();
        var c=new RpgEnemyConfig{Enabled=_enemyEnabled.Checked,EncounterChance=_enemyChance.Value,MaximumRounds=(int)_enemyRounds.Value,Tiers=new()};
        foreach(DataGridViewRow row in _enemyTierEditor.Rows)
        {
            long Number(int col){if(long.TryParse(Convert.ToString(row.Cells[col].Value),out var n))return n;throw new InvalidOperationException($"Unterwassergegner: Zeile {row.Index+1}, Feld {_enemyTierEditor.Columns[col].HeaderText} benötigt eine ganze Zahl.");}
            c.Tiers.Add(new(){MinimumStake=Number(0),Name=Convert.ToString(row.Cells[1].Value)??"",HitPoints=checked((int)Number(2)),MinimumDamage=checked((int)Number(3)),MaximumDamage=checked((int)Number(4)),Experience=checked((int)Number(5))});
        }
        c.Validate();return c;
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
