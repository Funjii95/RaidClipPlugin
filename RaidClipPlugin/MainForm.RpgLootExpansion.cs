using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private Control BuildRpgLootExpansionPanel()
    {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new(12)};
        root.RowStyles.Add(new(SizeType.Absolute,65));root.RowStyles.Add(new(SizeType.Absolute,45));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,55));
        root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Loot Expansion: Items, Dropgewichte, Slots, Sets und reine Sammlerboni.\nSammlerboni gelten einmal je Definition, nicht je Exemplar. Keine Kampfboni durch Besitz.\nÄnderungen betreffen künftige Funde; vorhandene Iteminstanzen bleiben unverändert."},0,0);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};var reload=NewActionButton("Aus Editor laden");var category=new ComboBox{Width=190,DropDownStyle=ComboBoxStyle.DropDownList};
        category.Items.AddRange(new object[]{"Ausrüstung","Sammlerschätze","Weitere Items","Sets","Pools & Grenzen"});category.SelectedIndex=0;
        var choice=new ComboBox{Width=400,DropDownStyle=ComboBoxStyle.DropDownList};actions.Controls.AddRange(new Control[]{reload,category,choice});root.Controls.Add(actions,0,1);
        var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font("Consolas",10)};root.Controls.Add(text,0,2);
        RpgConfig? snapshot=null;var json=new JsonSerializerOptions{WriteIndented=true};var loading=false;
        void Select()
        {
            if(loading || snapshot is null)return;
            var key=(string)category.SelectedItem!;
            text.Text=key=="Pools & Grenzen"?JsonSerializer.Serialize(new LootPoolEditor(snapshot.DiveLoot),json):
                key=="Sets" && choice.SelectedIndex>=0?JsonSerializer.Serialize(snapshot.Sets[choice.SelectedIndex],json):
                choice.SelectedItem is LootItemChoice item?JsonSerializer.Serialize(snapshot.DiveLoot.Items.Single(i=>i.ItemId==item.Id),json):"";
        }
        void List()
        {
            loading=true;choice.Items.Clear();
            if(snapshot is not null)
            {
                var key=(string)category.SelectedItem!;
                if(key=="Sets")foreach(var set in snapshot.Sets)choice.Items.Add(set.Name);
                else if(key!="Pools & Grenzen")foreach(var item in snapshot.DiveLoot.Items.Where(i=>key=="Ausrüstung"?i.Slot.HasValue:key=="Sammlerschätze"?i.Collector is not null:!i.Slot.HasValue && i.Collector is null))
                    choice.Items.Add(new LootItemChoice(item.ItemId,$"{item.DisplayName} [{item.Rarity}] · {(item.Slot.HasValue?RpgChatPresentation.SlotName(item.Slot.Value):RpgChatPresentation.KindName(item))} · Gewicht {item.DropWeight}"));
                if(choice.Items.Count>0)choice.SelectedIndex=0;
            }
            loading=false;Select();
        }
        reload.Click+=(_,_)=>{try{snapshot=JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text)??throw new InvalidOperationException("Zuerst RPG-Einstellungen laden.");List();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Loot-Editor");}};
        category.SelectedIndexChanged+=(_,_)=>List();choice.SelectedIndexChanged+=(_,_)=>Select();
        var save=NewActionButton("Auswahl übernehmen & speichern");save.Click+=(_,_)=>
        {
            try
            {
                if(snapshot is null)throw new InvalidOperationException("Zuerst aus Editor laden.");
                // Re-read the main editor, so unrelated edits are not overwritten.
                var current=JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text)!;
                if((string)category.SelectedItem! == "Pools & Grenzen") (JsonSerializer.Deserialize<LootPoolEditor>(text.Text)??throw new InvalidOperationException("Pool fehlt.")).Apply(current.DiveLoot);
                else if((string)category.SelectedItem! == "Sets")
                {
                    var set=JsonSerializer.Deserialize<RpgItemSet>(text.Text)!;var original=snapshot.Sets[choice.SelectedIndex];
                    if(set.Id!=original.Id)throw new InvalidOperationException("Bestehende Set-ID nicht ändern.");
                    current.Sets[current.Sets.FindIndex(s=>s.Id==set.Id)]=set;
                }
                else if(choice.SelectedItem is LootItemChoice selected)
                {
                    var item=JsonSerializer.Deserialize<RpgItem>(text.Text)!;
                    if(item.ItemId!=selected.Id)throw new InvalidOperationException("Bestehende Item-ID nicht ändern.");
                    current.DiveLoot.Items[current.DiveLoot.Items.FindIndex(i=>i.ItemId==selected.Id)]=item;
                }
                current.Validate();_rpgConfigJson.Text=JsonSerializer.Serialize(current,json);snapshot=current;SaveMinigameSettingsFromControls();List();
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Loot-Einstellungen");}
        };
        root.Controls.Add(save,0,3);return root;
    }
    private sealed record LootItemChoice(string Id,string Text){public override string ToString()=>Text;}
    public sealed class LootPoolEditor
    {
        public LootPoolEditor(){}
        public LootPoolEditor(RpgLootConfig c){CollectorPoolChance=c.CollectorPoolChance;CollectorPearlCap=c.CollectorPearlCap;CollectorChanceCap=c.CollectorChanceCap;CollectorExperienceCap=c.CollectorExperienceCap;RarityWeights=c.RarityWeights;RiskTiers=c.RiskTiers;}
        public decimal CollectorPoolChance{get;set;}=15;
        public decimal CollectorPearlCap{get;set;}=15;
        public decimal CollectorChanceCap{get;set;}=10;
        public decimal CollectorExperienceCap{get;set;}=10;
        public Dictionary<ItemRarity,int> RarityWeights{get;set;}=new();
        public List<RpgRiskTier> RiskTiers{get;set;}=new();
        public void Apply(RpgLootConfig c){c.CollectorPoolChance=CollectorPoolChance;c.CollectorPearlCap=CollectorPearlCap;c.CollectorChanceCap=CollectorChanceCap;c.CollectorExperienceCap=CollectorExperienceCap;c.RarityWeights=RarityWeights;c.RiskTiers=RiskTiers;}
    }
}
