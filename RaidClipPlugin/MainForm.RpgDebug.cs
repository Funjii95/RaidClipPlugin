using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly PropertyGrid _hotTimeSettings=NewAdventureGrid();
    private Control BuildHotTimePanel()
    {
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new(12)};
        layout.RowStyles.Add(new(SizeType.Absolute,55));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,90));
        var status=new Label {Dock=DockStyle.Fill,Text="Hot Time: inaktiv"};layout.Controls.Add(status,0,0);
        var editor=new TableLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,RowCount=7};editor.ColumnStyles.Add(new(SizeType.Absolute,245));editor.ColumnStyles.Add(new(SizeType.Percent,100));
        var enabled=new CheckBox {Text="Hot Time aktivieren",AutoSize=true};var automatic=new CheckBox {Text="Automatisch nach echtem Boss-Sieg",AutoSize=true};
        var minutes=new NumericUpDown {Minimum=1,Maximum=60,Value=5,Width=140};var drop=new NumericUpDown {Minimum=0,Maximum=200,Value=50,DecimalPlaces=1,Width=140};var pearls=new NumericUpDown {Minimum=0,Maximum=200,Value=100,DecimalPlaces=1,Width=140};
        var startText=new TextBox {Dock=DockStyle.Fill,MaxLength=450};var endText=new TextBox {Dock=DockStyle.Fill,MaxLength=450};
        var fields=new (string Name,Control Input)[]{("Aktivierung",enabled),("Auslöser",automatic),("Dauer (Minuten)",minutes),("Relativer Basis-Dropbonus (%)",drop),("Zusätzliche Perlenbelohnungen (%)",pearls),("Starttext ({minutes}, {drop}, {reward})",startText),("Endtext",endText)};
        for(var i=0;i<fields.Length;i++){editor.RowStyles.Add(new(SizeType.Absolute,42));editor.Controls.Add(new Label {Text=fields[i].Name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i);editor.Controls.Add(fields[i].Input,1,i);}layout.Controls.Add(editor,0,1);
        var loading=false;
        void Capture(){if(loading||_hotTimeSettings.SelectedObject is not RpgHotTimeConfig hot)return;hot.Enabled=enabled.Checked;hot.AutoAfterBoss=automatic.Checked;hot.DurationMinutes=(int)minutes.Value;hot.DropBonusPercent=drop.Value;hot.PearlBonusPercent=pearls.Value;hot.StartMessage=startText.Text;hot.EndMessage=endText.Text;}
        _hotTimeSettings.SelectedObjectsChanged+=(_,_)=>{if(_hotTimeSettings.SelectedObject is not RpgHotTimeConfig hot)return;loading=true;enabled.Checked=hot.Enabled;automatic.Checked=hot.AutoAfterBoss;minutes.Value=Math.Clamp(hot.DurationMinutes,1,60);drop.Value=Math.Clamp(hot.DropBonusPercent,0,200);pearls.Value=Math.Clamp(hot.PearlBonusPercent,0,200);startText.Text=hot.StartMessage;endText.Text=hot.EndMessage;loading=false;};
        enabled.CheckedChanged+=(_,_)=>Capture();automatic.CheckedChanged+=(_,_)=>Capture();foreach(var n in new[]{minutes,drop,pearls})n.ValueChanged+=(_,_)=>Capture();startText.TextChanged+=(_,_)=>Capture();endText.TextChanged+=(_,_)=>Capture();
        var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true};var save=NewActionButton("Hot-Time-Einstellungen speichern");var stop=NewActionButton("Hot Time sofort beenden");var restore=NewActionButton("Letzte gültige Konfiguration wiederherstellen");
        save.Click+=(_,_)=>SaveMinigameSettingsFromControls();
        stop.Click+=(_,_)=>{try{_viewerPoints.HotTime.End();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Hot Time");}};
        restore.Click+=(_,_)=>
        {
            if(MessageBox.Show(this,"Letzte gültige gesamte Konfiguration wiederherstellen? Der aktuelle Stand wird separat gesichert.","Konfiguration wiederherstellen",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            try{_configurationService.RestoreLastValidSettings();var config=_configurationService.LoadForEditing();LoadSettingsIntoControls();ApplyRuntimeSettings(config);MessageBox.Show(this,"Konfiguration wiederhergestellt. Die Einstellungsansichten wurden aktualisiert.");}
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Wiederherstellung fehlgeschlagen");}
        };
        actions.Controls.AddRange(new Control[]{save,stop,restore});layout.Controls.Add(actions,0,2);
        var timer=new System.Windows.Forms.Timer {Interval=1000};timer.Tick+=(_,_)=>{var s=_viewerPoints.HotTime.Status;status.Text=s.Error.Length>0?s.Error:s.Active?$"🔥 Hot Time aktiv: {TimeSpan.FromSeconds(s.RemainingSeconds):mm\\:ss} | Boss: {s.Event?.BossId}":"Hot Time: inaktiv. Manuelle Tests ausschließlich im isolierten RPG Debug Center.";};timer.Start();layout.Disposed+=(_,_)=>timer.Dispose();return layout;
    }
    private Control BuildRpgDebugPanel()
    {
        var session=new RpgDebugSession();var root=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Padding=new(10)};
        root.RowStyles.Add(new(SizeType.Absolute,50));root.RowStyles.Add(new(SizeType.Absolute,50));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,220));
        var enabled=new CheckBox {Text="TESTMODUS aktivieren – nur lokaler Administrator",AutoSize=true};
        var warning=new Label {Text="DEAKTIVIERT – keine Verbindung zu echten Zuschauerdaten",AutoSize=true,ForeColor=Color.OrangeRed};
        var top=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true};top.Controls.AddRange(new Control[]{enabled,warning});root.Controls.Add(top,0,0);
        var commands=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true};var save=NewActionButton("Testkonfiguration speichern");var restore=NewActionButton("Testkonfiguration wiederherstellen");var refresh=NewActionButton("Testkonto / Werte anzeigen");commands.Controls.AddRange(new Control[]{save,restore,refresh});root.Controls.Add(commands,0,1);
        var tabs=new TabControl {Dock=DockStyle.Fill,Enabled=false};root.Controls.Add(tabs,0,2);
        var output=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false};root.Controls.Add(output,0,3);
        var options=NewAdventureGrid();var testConfig=NewAdventureGrid();var busy=false;var cancel=new CancellationTokenSource();
        void Show(object? value)=>output.Text=JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true});
        async Task Run(Func<Task<object?>> action)
        {
            if(busy)return;busy=true;tabs.Enabled=false;commands.Enabled=false;enabled.Enabled=false;
            try{Show(await Task.Run(action));}catch(Exception ex){output.Text="TEST-FEHLER: "+ex.Message;}
            finally{busy=false;if(!root.IsDisposed){tabs.Enabled=session.Active;commands.Enabled=true;enabled.Enabled=true;}}
        }
        FlowLayoutPanel Row(params Control[] controls){var row=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoScroll=true,WrapContents=true};row.Controls.AddRange(controls);return row;}
        Panel Page(string name){var panel=new Panel {Dock=DockStyle.Fill,AutoScroll=true};AddMinigameTab(tabs,name,panel);return panel;}
        var heistPreview=Page("Heist-Overlay");var previewButton=NewActionButton("Isolierte Heist-Vorschau öffnen");
        previewButton.Click+=(_,_)=>OpenHeistPreview();
        heistPreview.Controls.Add(Row(previewButton,new Label{AutoSize=true,Text="Nur Mockdaten: Teilnehmer, Countdown, Erfolg/Niederlage und Boss-/Rennkonflikt testen. Keine echten Perlen oder Twitch-Nachrichten."}));
        var loot=Page("Drop-Editor");var lootActions=Row();var roll=NewActionButton("Lootwurf testen");var legendary=NewActionButton("Legendary-Drop erzwingen");lootActions.Controls.AddRange(new Control[]{roll,legendary});loot.Controls.Add(options);loot.Controls.Add(lootActions);
        roll.Click+=async(_,_)=>await Run(async()=>await session.RollLootAsync(cancel.Token));
        legendary.Click+=async(_,_)=>await Run(async()=>{session.Options.OnlyRarity=ItemRarity.Legendary;session.Options.LootMode=RpgDebugLootMode.Garantiert;session.Options.Stake=Math.Max(10000,session.Options.LegendaryMinimumStake);session.Config.Adventures.Rpg.DiveLoot.RiskTiers=new(){new(){MinimumStake=0,MaximumRarity=ItemRarity.Legendary}};var result=await session.RollLootAsync(cancel.Token);return result;});
        var cfg=Page("Test-Balancing");cfg.Controls.Add(testConfig);
        var generator=Page("Item-Generator");var filter=new TextBox {Width=220,PlaceholderText="Name oder Item-ID filtern"};var rarity=new ComboBox{Width=130,DropDownStyle=ComboBoxStyle.DropDownList};rarity.Items.Add("Alle Seltenheiten");foreach(var r in Enum.GetValues<ItemRarity>())rarity.Items.Add(r);rarity.SelectedIndex=0;
        var slot=new ComboBox{Width=130,DropDownStyle=ComboBoxStyle.DropDownList};slot.Items.Add("Alle Slots");foreach(var s in Enum.GetValues<EquipmentSlot>())slot.Items.Add(s);slot.SelectedIndex=0;
        var set=new ComboBox{Width=210,DropDownStyle=ComboBoxStyle.DropDownList};var effect=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList};
        var items=new ComboBox {Width=550,DropDownStyle=ComboBoxStyle.DropDownList};var mode=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var m in Enum.GetValues<RpgDebugRollMode>())mode.Items.Add(m);mode.SelectedIndex=0;
        var values=new DataGridView {Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false};
        foreach(var name in new[]{"Attribut","Minimum","Maximum","Testwert"})values.Columns.Add(new DataGridViewTextBoxColumn {Name=name,HeaderText=name,ReadOnly=name!="Testwert",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
        var create=NewActionButton("Testitem erzeugen");var gLayout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};gLayout.RowStyles.Add(new(SizeType.Absolute,100));gLayout.RowStyles.Add(new(SizeType.Absolute,50));gLayout.RowStyles.Add(new(SizeType.Percent,100));gLayout.Controls.Add(Row(filter,rarity,slot,set,effect),0,0);gLayout.Controls.Add(Row(items,mode,create),0,1);gLayout.Controls.Add(values,0,2);generator.Controls.Add(gLayout);
        void FilterItems()
        {
            if(!session.Active)return;items.Items.Clear();
            foreach(var item in session.Config.Adventures.Rpg.DiveLoot.Items.Where(i=>(filter.Text.Length==0||i.DisplayName.Contains(filter.Text,StringComparison.OrdinalIgnoreCase)||i.ItemId.Contains(filter.Text,StringComparison.OrdinalIgnoreCase))&&
                (rarity.SelectedItem is not ItemRarity r||i.Rarity==r)&&(slot.SelectedItem is not EquipmentSlot s||i.Slot==s)&&
                (set.SelectedItem is not RpgChoice sc||i.SetId==sc.Id)&&(effect.SelectedItem is not RpgChoice ec||i.Effects.Any(e=>e.Id==ec.Id)||i.Rarity>=ItemRarity.Rare&&i.Generation?.GenerateEffect==true&&i.Generation.EffectIds.Contains(ec.Id))))
                items.Items.Add(new RpgChoice(item.ItemId,$"{item.DisplayName} [{item.Rarity}] – {item.ItemId}"));
            if(items.Items.Count>0)items.SelectedIndex=0;
        }
        foreach(var combo in new[]{rarity,slot,set,effect})combo.SelectedIndexChanged+=(_,_)=>FilterItems();filter.TextChanged+=(_,_)=>FilterItems();
        items.SelectedIndexChanged+=(_,_)=>
        {
            values.Rows.Clear();if(items.SelectedItem is not RpgChoice item)return;
            var definition=session.Config.Adventures.Rpg.DiveLoot.Items.Single(i=>i.ItemId==item.Id);
            if(definition.Generation is {} generation)foreach(var range in generation.Attributes)values.Rows.Add(range.Attribute,range.Minimum,range.Maximum,range.Minimum);
        };
        create.Click+=async(_,_)=>
        {
            if(items.SelectedItem is not RpgChoice item||mode.SelectedItem is not RpgDebugRollMode selected)return;
            var custom=new Dictionary<string,int>();try{values.EndEdit();foreach(DataGridViewRow row in values.Rows)custom[Convert.ToString(row.Cells[0].Value)??throw new InvalidOperationException("Attribut fehlt.")]=Convert.ToInt32(row.Cells[3].Value);}catch(Exception ex){output.Text=ex.Message;return;}
            var effectId=(effect.SelectedItem as RpgChoice)?.Id;
            await Run(async()=>await session.GenerateAsync(item.Id,selected,custom,cancel.Token,effectId));
        };
        var combat=Page("Kampf & Fortschritt");var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};combat.Controls.Add(actions);
        var hit=NewActionButton("Bossangriff simulieren");var win=NewActionButton("Boss-Sieg simulieren (ohne Hot Time)");var resetBoss=NewActionButton("Boss-HP aus Testwert übernehmen");
        hit.Click+=async(_,_)=>await Run(async()=>await session.CombatAsync(false,cancel.Token));win.Click+=async(_,_)=>await Run(async()=>await session.CombatAsync(true,cancel.Token));resetBoss.Click+=(_,_)=>{try{session.ResetBoss();Show(new {session.RemainingBossHp});}catch(Exception ex){output.Text=ex.Message;}};
        actions.Controls.Add(Row(hit,win,resetBoss));actions.Controls.Add(new Label {AutoSize=true,Text="Erzwungene Krit-/Double-/Triple-Ergebnisse: Haken im Drop-Editor. Ausschließlich Testkonto."});
        var selection=new ComboBox {Width=350,DropDownStyle=ComboBoxStyle.DropDownList};var amount=new NumericUpDown {Minimum=-1000000000,Maximum=1000000000,Value=1000,Width=150};
        var action=new ComboBox {Width=230,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var a in new[]{"Perlen ändern","Aktivitätsbelohnung","Perk setzen","Fische erzeugen","Sushi herstellen","Sushi aktivieren","Sushi-Verbrauch testen","Inventar zurücksetzen","Item entfernen","Alles ablegen","Cooldowns zurücksetzen","Ausrüstung anlegen","Set testen","Hot Time testen","Hot Time beenden"})action.Items.Add(a);action.SelectedIndex=0;
        void UpdateSelection()
        {
            selection.Items.Clear();if(!session.Active)return;var rpg=session.Config.Adventures.Rpg;
            IEnumerable<RpgChoice> choices=(string?)action.SelectedItem switch{"Perk setzen"=>rpg.Perks.Select(p=>new RpgChoice(p.Id,p.DisplayName.Length>0?p.DisplayName:p.Id)),"Fische erzeugen"=>rpg.FishSpecies.Select(f=>new RpgChoice(f.Id,f.Name)),"Sushi herstellen" or "Sushi aktivieren"=>rpg.Recipes.Select(r=>new RpgChoice(r.Id,RpgChatPresentation.RecipeName(r))),"Sushi-Verbrauch testen"=>Enum.GetValues<RpgActivity>().Select(a=>new RpgChoice(a.ToString(),RpgChatPresentation.Activity(a))),"Set testen"=>rpg.Sets.Select(s=>new RpgChoice(s.Id,s.Name)),_=>Array.Empty<RpgChoice>()};
            foreach(var choice in choices)selection.Items.Add(choice);if(selection.Items.Count>0)selection.SelectedIndex=0;
        }
        action.SelectedIndexChanged+=(_,_)=>UpdateSelection();var execute=NewActionButton("Testaktion ausführen");actions.Controls.Add(Row(action,selection,amount,execute));
        execute.Click+=async(_,_)=>{var name=(string)action.SelectedItem!;var selected=(selection.SelectedItem as RpgChoice)?.Id??"";var value=(long)amount.Value;await Run(async()=>await session.ProgressAsync(name,selected,value,cancel.Token));};
        var sim=Page("Simulation");var runs=new ComboBox{Width=130,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var n in new[]{100,1000,10000,100000})runs.Items.Add(n);runs.SelectedIndex=0;var simulate=NewActionButton("Loot & Kampf simulieren");sim.Controls.Add(Row(runs,simulate,new Label{Text="Reproduzierbarer Seed aus Drop-Editor. Kein Schreiben von Spielerständen durch Simulationen.",AutoSize=true}));
        var health=Page("HP & Überleben");var hpAction=new ComboBox {Width=220,DropDownStyle=ComboBoxStyle.DropDownList};
        hpAction.Items.AddRange(new object[]{"Status","HP setzen","Gegenangriff","Besiegt markieren","Regeneration","Chat-Heilung","Boss-Teilnahme","Boss-Lifeleech"});hpAction.SelectedIndex=0;
        var hpValue=new NumericUpDown {Minimum=0,Maximum=1000000,Value=50,Width=130};var hpRun=NewActionButton("HP-Test ausführen");
        health.Controls.Add(Row(hpAction,hpValue,hpRun,new Label {AutoSize=true,Text="Wert: HP / Boss-Ausgangs-HP / Minuten / Schaden bei Boss-Lifeleech. Nur isoliertes Testkonto."}));
        hpRun.Click+=async(_,_)=>{var a=(string)hpAction.SelectedItem!;var v=(int)hpValue.Value;await Run(async()=>await session.HealthAsync(a,v,cancel.Token));};
        simulate.Click+=async(_,_)=>{var count=(int)runs.SelectedItem!;await Run(async()=>await session.SimulateAsync(count,cancel.Token));};
        void Bind()
        {
            options.SelectedObject=session.Options;testConfig.SelectedObject=session.Config.Adventures.Rpg;
            set.Items.Clear();set.Items.Add("Alle Sets");foreach(var s in session.Config.Adventures.Rpg.Sets)set.Items.Add(new RpgChoice(s.Id,s.Name));set.SelectedIndex=0;
            effect.Items.Clear();effect.Items.Add("Alle Effekte");foreach(var e in session.Config.Adventures.Rpg.DiveLoot.Effects.Concat(session.Config.Adventures.Rpg.DiveLoot.Items.SelectMany(i=>i.Effects)).DistinctBy(e=>e.Id))effect.Items.Add(new RpgChoice(e.Id,e.Name));effect.SelectedIndex=0;FilterItems();UpdateSelection();
        }
        enabled.CheckedChanged+=(_,_)=>
        {
            try{if(enabled.Checked){session.Activate(_configurationService.LoadForEditing().Minigame);Bind();warning.Text="TESTMODUS – isolierte Daten: "+session.DirectoryPath;}else{session.Deactivate();warning.Text="DEAKTIVIERT – Produktivregeln unverändert";}tabs.Enabled=session.Active;}
            catch(Exception ex){session.Deactivate();enabled.Checked=false;output.Text=ex.Message;}
        };
        save.Click+=(_,_)=>{try{session.SaveSettings();Show("Testkonfiguration gespeichert.");}catch(Exception ex){output.Text=ex.Message;}};
        restore.Click+=(_,_)=>{try{session.RestoreSettings();if(session.Active)Bind();Show("Testkonfiguration wiederhergestellt.");}catch(Exception ex){output.Text=ex.Message;}};
        refresh.Click+=async(_,_)=>await Run(async()=>{var profile=await session.ProfileAsync(cancel.Token);return new {profile.Points,profile.Rpg,Stats=CharacterStatService.Calculate(profile.Rpg,session.Config.Adventures.Rpg,RpgActivity.Boss),Sammlerboni=RpgCollectorService.Calculate(profile.Rpg,session.Config.Adventures.Rpg.DiveLoot),Sets=RpgSetService.Progress(profile.Rpg,session.Config.Adventures.Rpg)};});
        root.Disposed+=(_,_)=>{cancel.Cancel();session.Deactivate();};return root;
    }
}
