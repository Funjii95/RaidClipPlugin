using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private bool _dataAdministrationBusy;
    private bool _administrationTestOnly;
    internal void EnableAdministrationTestOnly(){_administrationTestOnly=true;_startButton.Enabled=false;Text+=" – Verwaltungstest ohne Netzwerk-Spielbetrieb";}
    private Control BuildDataAdministrationPanel()
    {
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(16)};
        var info=new Label{AutoSize=true,MaximumSize=new Size(800,0),Text="Nur bei vollständig gestopptem Plugin. Zurücksetzungen erhalten Level, Perks, Cooldowns und Spielhistorie. Items umfasst Inventar, Ausrüstung, Fische, Sushi und aktive Item-Buffs. Der gemeinsame Jackpot bleibt erhalten."};
        var players=new ComboBox{Width=650,DropDownStyle=ComboBoxStyle.DropDownList};
        var load=NewActionButton("Spieler und Löschstatus laden");
        var pearls=NewActionButton("Alle Perlen löschen");var items=NewActionButton("Alle Items löschen");var both=NewActionButton("Perlen und Items löschen");
        var identity=new CheckBox{AutoSize=true,Text="Für Datenschutz-Löschung: Kontoinhaber identifiziert und Löschanfrage geprüft"};
        var privacy=NewActionButton("Ausgewählten Spieler zur Datenlöschung sperren und bereinigen");
        var status=new Label{AutoSize=true,MaximumSize=new Size(800,0),Text="Noch keine Änderung ausgeführt. Sammelaktionen erzeugen eine Sicherung. Datenschutz-Löschungen erzeugen keine zusätzliche Spielstandsicherung."};
        panel.Controls.AddRange([info,players,load,pearls,items,both,identity,privacy,status]);
        var busy=false;
        async Task Safe(Func<Task> operation){
            if(busy || _dataAdministrationBusy)return;
            if(_shutdown is not null || _minigame is {IsRunning:true}){status.Text="Plugin zuerst mit Stop vollständig anhalten. Danach erneut ausführen.";return;}
            busy=true;_dataAdministrationBusy=true;var startEnabled=_startButton.Enabled;_startButton.Enabled=false;foreach(var button in new[]{load,pearls,items,both,privacy})button.Enabled=false;
            try{await operation();}catch(Exception error){status.Text="Nicht abgeschlossen: "+error.Message;}
            finally{busy=false;_dataAdministrationBusy=false;if(!panel.IsDisposed){_startButton.Enabled=startEnabled&&!_administrationTestOnly;foreach(var button in new[]{load,pearls,items,both,privacy})button.Enabled=true;}}
        }
        async Task Load(){
            players.Items.Clear();players.Items.Add(new RpgChoice("ALL","ALLE SPIELER"));
            foreach(var entry in await _viewerPoints.GetTopAsync(int.MaxValue,default))players.Items.Add(new RpgChoice(entry.UserId,$"{entry.DisplayName} · Twitch-ID {entry.UserId}"));
            foreach(var request in await _viewerPoints.PrivacyRequestsAsync())players.Items.Add(new RpgChoice(request.UserId,$"Gesperrte Twitch-ID {request.UserId} · lokal {(request.LocalErased?"bereinigt":"offen")} · Cloud {(request.CloudErased?"bereinigt":"offen")}"));
            players.SelectedIndex=0;status.Text="Auswahl geladen. Cloud-offene Löschungen bleiben gespeichert und werden nach Freigabe des Cloud-Löschwegs erneut versucht. Externe Exporte und Betriebsprotokolle separat prüfen.";
        }
        load.Click+=async(_,_)=>await Safe(Load);
        async Task Reset(RpgResetScope scope){
            if(players.SelectedItem is not RpgChoice selected)return;var id=selected.Id=="ALL"?null:selected.Id;
            var preview=await _viewerPoints.PreviewResetAsync(id,default);
            var mode=scope==RpgResetScope.Pearls?"PERLEN":scope==RpgResetScope.Items?"ITEMS":"PERLEN UND ITEMS";
            var phrase=$"{mode} LÖSCHEN";
            if(!ConfirmDataAdministration($"{mode} zurücksetzen",$"Ziel: {selected.Name}\nBetroffene Spieler: {preview.Players}\nInventar-Items: {preview.Items}, Fische: {preview.Fish}, Sushi: {preview.Sushi}\n\nPerlen werden bei entsprechender Auswahl exakt auf 0 gesetzt. Items werden bei entsprechender Auswahl entfernt, auch gesperrte/favorisierte und ausgerüstete Items. Eine lokale Sicherung wird erstellt. Cloud-Abgleich nur für bereits freigegebene Konten.\n\nZur Bestätigung eingeben:",phrase))return;
            var result=await Task.Run(()=>_viewerPoints.ResetAsync(id,scope,Guid.NewGuid(),preview.Fingerprint,default));status.Text=result.Message;
        }
        pearls.Click+=async(_,_)=>await Safe(()=>Reset(RpgResetScope.Pearls));
        items.Click+=async(_,_)=>await Safe(()=>Reset(RpgResetScope.Items));
        both.Click+=async(_,_)=>await Safe(()=>Reset(RpgResetScope.PearlsAndItems));
        privacy.Click+=async(_,_)=>await Safe(async()=>{
            if(players.SelectedItem is not RpgChoice selected || selected.Id=="ALL"){status.Text="Datenschutz-Löschung benötigt genau einen Spieler.";return;}
            if(!identity.Checked){status.Text="Zuerst Kontoinhaber und Löschanfrage prüfen und bestätigen.";return;}
            if(!ConfirmDataAdministration("Spielerdaten löschen",$"Ziel: {selected.Name}\n\nDas Konto wird gegen automatische Neuanlage gesperrt. Lokaler Spielstand, zuordenbare Spielhistorie und verwaltete Spielstandsicherungen werden ohne neue Sicherung bereinigt. Eine minimale Twitch-ID-Sperre bleibt gegen erneute Übertragung erhalten.\nCloud-Profile und Aktionsbelege gelten erst nach bestätigtem Cloud-Abgleich als bereinigt. Weitere Exporte und Protokolle müssen separat geprüft werden.\n\nZur Bestätigung eingeben:",$"SPIELER {selected.Id} LÖSCHEN"))return;
            var result=await Task.Run(()=>_viewerPoints.RequestPrivacyDeletionAsync(selected.Id,Guid.NewGuid(),default));
            status.Text=$"Spieler gesperrt. Lokale Bereinigung: {(result.LocalErased?"erledigt":"offen")}. Cloud-Bereinigung: {(result.CloudErased?"bestätigt":"ausstehend; Löschauftrag bleibt gespeichert")}. Externe Kopien und Protokolle separat prüfen.";identity.Checked=false;
        });
        return panel;
    }
    private bool ConfirmDataAdministration(string title,string message,string phrase)
    {
        using var dialog=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,Width=740,Height=510,MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=4,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
        var text=new Label{Text=message,Dock=DockStyle.Fill};var expected=new Label{Text=phrase,Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold)};var input=new TextBox{Dock=DockStyle.Fill};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};var confirm=new Button{Text="Endgültig ausführen",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};var cancel=new Button{Text="Abbrechen",AutoSize=true,DialogResult=DialogResult.Cancel};
        input.TextChanged+=(_,_)=>confirm.Enabled=input.Text==phrase;buttons.Controls.AddRange([confirm,cancel]);layout.Controls.Add(text,0,0);layout.Controls.Add(expected,0,1);layout.Controls.Add(input,0,2);layout.Controls.Add(buttons,0,3);dialog.Controls.Add(layout);dialog.CancelButton=cancel;
        return dialog.ShowDialog(this)==DialogResult.OK && input.Text==phrase;
    }
}
