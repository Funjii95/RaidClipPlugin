using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly DataGridView _rpgFishEditor=new(){Dock=DockStyle.Fill,AllowUserToDeleteRows=false,RowHeadersVisible=false};
    private readonly DataGridView _rpgRecipeEditor=new(){Dock=DockStyle.Fill,AllowUserToDeleteRows=false,RowHeadersVisible=false};
    private readonly NumericUpDown _freeFishReward=new(){Minimum=0,Maximum=1000000,Width=120};
    private Control BuildRpgSushiPanel()
    {
        void Columns(DataGridView grid,params string[] names)
        {foreach(var name in names)grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=name,Width=130,SortMode=DataGridViewColumnSortMode.NotSortable});}
        Columns(_rpgFishEditor,"ID","Name","Seltenheit","Gewicht","Perlen (leer = bisherige Auszahlung)","Mindesteinsatz","Fanggebiet");
        Columns(_rpgRecipeEditor,"ID","Name","Kategorie","Haupt-Fisch-ID","Menge","Zutaten (ID:Menge;ID:Menge)","Perlenkosten","HP/Minute zusätzlich","Chat-HP zusätzlich","Boss-Reduktion %","Boss-Teilnahmen");
        var tabs=new TabControl{Dock=DockStyle.Fill};AddMinigameTab(tabs,"Fangpool",_rpgFishEditor);AddMinigameTab(tabs,"Rezepte & Buffs",_rpgRecipeEditor);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new(10)};
        root.RowStyles.Add(new(SizeType.Absolute,70));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,55));
        root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="!fish = kostenloser Fangversuch, !fish <Einsatz> bleibt möglich. Gewichte sind relativ; Gewicht 0 deaktiviert eine Art. Gebiet ist ein Label, Mindesteinsatz optional. Rezepte: Offensive/Defensive; genau ein aktiver Buff je Slot. IDs bestehender Einträge bleiben stabil. Bei Mehrfachzutaten: shrimp:2;giant_shrimp:1."},0,0);
        root.Controls.Add(tabs,0,1);var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};var save=NewActionButton("Fische & Sushi speichern");save.Click+=(_,_)=>SaveMinigameSettingsFromControls();
        actions.Controls.AddRange(new Control[]{new Label{Text="Basisbelohnung für alte Arten bei kostenlosem Fang:",AutoSize=true},_freeFishReward,save});root.Controls.Add(actions,0,2);return root;
    }
    private void LoadRpgSushi(RpgConfig c)
    {
        _freeFishReward.Value=Math.Clamp(c.FreeFishingReward,0,1000000);_rpgFishEditor.Rows.Clear();_rpgRecipeEditor.Rows.Clear();
        foreach(var f in c.FishSpecies)
        {var row=_rpgFishEditor.Rows[_rpgFishEditor.Rows.Add(f.Id,f.Name,f.Rarity.ToString(),f.Weight,f.PearlReward?.ToString()??"",f.MinimumStake,f.Zone)];row.Cells[0].ReadOnly=true;}
        foreach(var r in c.Recipes)
        {var row=_rpgRecipeEditor.Rows[_rpgRecipeEditor.Rows.Add(r.Id,r.DisplayName,r.Category.ToString(),r.FishId,r.FishQuantity,string.Join(";",r.Ingredients.Select(i=>$"{i.Key}:{i.Value}")),r.PearlCost,r.PassiveHealing,r.ChatHealing,r.BossDamageReduction,RpgSushiService.Uses(r,c))];row.Cells[0].ReadOnly=true;}
    }
    private void ReadRpgSushi(RpgConfig c)
    {
        _rpgFishEditor.EndEdit();_rpgRecipeEditor.EndEdit();c.FreeFishingReward=(long)_freeFishReward.Value;
        string Text(DataGridViewRow row,int i)=>Convert.ToString(row.Cells[i].Value)?.Trim()??"";
        var fishIds=new HashSet<string>(StringComparer.Ordinal);var recipeIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(DataGridViewRow row in _rpgFishEditor.Rows)
        {
            if(row.IsNewRow)continue;var id=Text(row,0);if(id.Length==0)throw new InvalidOperationException("Fisch-ID fehlt.");
            if(!fishIds.Add(id))throw new InvalidOperationException("Doppelte Fisch-ID: "+id);
            var f=c.FishSpecies.FirstOrDefault(f=>f.Id==id);if(f is null){f=new(){Id=id};c.FishSpecies.Add(f);}
            f.Name=Text(row,1);f.Rarity=Enum.Parse<ItemRarity>(Text(row,2),true);f.Weight=int.Parse(Text(row,3));f.PearlReward=Text(row,4).Length==0?null:long.Parse(Text(row,4));f.MinimumStake=long.Parse(Text(row,5));f.Zone=Text(row,6);
        }
        foreach(DataGridViewRow row in _rpgRecipeEditor.Rows)
        {
            if(row.IsNewRow)continue;var id=Text(row,0);if(id.Length==0)throw new InvalidOperationException("Rezept-ID fehlt.");
            if(!recipeIds.Add(id))throw new InvalidOperationException("Doppelte Rezept-ID: "+id);
            var r=c.Recipes.FirstOrDefault(r=>r.Id==id);if(r is null){r=new(){Id=id};c.Recipes.Add(r);}
            r.DisplayName=Text(row,1);r.Category=Enum.Parse<SushiCategory>(Text(row,2),true);r.FishId=Text(row,3);r.FishQuantity=int.Parse(Text(row,4));
            r.Ingredients=Text(row,5).Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Split(':')).ToDictionary(p=>p[0].Trim(),p=>int.Parse(p[1]));
            r.PearlCost=long.Parse(Text(row,6));r.PassiveHealing=decimal.Parse(Text(row,7));r.ChatHealing=int.Parse(Text(row,8));r.BossDamageReduction=decimal.Parse(Text(row,9));r.BossUses=int.Parse(Text(row,10));
        }
    }
}
