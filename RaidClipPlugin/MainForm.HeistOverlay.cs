using System.Diagnostics;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private HeistOverlayConfig _heistOverlayEditor=new();
    private readonly Dictionary<string,Control> _heistOverlayControls=new();
    private readonly CheckBox _heistStartMessagesCheck=new(){Text="Heist-Startmeldung senden",AutoSize=true,Checked=true};
    private Control BuildHeistOverlayPanel()
    {
        var root=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(12)};
        root.Controls.Add(new Label{AutoSize=true,MaximumSize=new(850,0),Text="Gemeinsame OBS-Browserquelle: "+AdventureOverlayServer.Url+"\nEmpfohlen: 900 × 600, transparenter Hintergrund. Boss → Rennen → Heist → Ergebnisse. Der Countdown kommt direkt aus dem Heist; keine zweite Spielzeit.\nOBS darf getrennt sein: Teilnahme, Jackpot und Auszahlung funktionieren weiterhin. Ohne Events ist die Quelle transparent."});
        var names=new Dictionary<string,string>{
            ["Enabled"]="Heist-Overlay aktivieren",["Title"]="Heist-Titel",["Scene"]="OBS-Szene (leer = aktuelle)",["BrowserSource"]="OBS-Browserquelle",
            ["ResultSeconds"]="Ergebnis anzeigen (Sekunden)",["AnimationSeconds"]="Überfallanimation (Sekunden)",["ShowParticipants"]="Teilnehmernamen anzeigen",
            ["MaximumVisibleParticipants"]="Maximal sichtbare Namen",["SuccessAnimation"]="Erfolgseffekte",["FailureAnimation"]="Niederlageeffekte",
            ["ShowHotTime"]="Hot-Time-Banner (nicht über Boss)",["BossPriority"]="Boss-Priorität",["RacePriority"]="Renn-Priorität",
            ["HeistPriority"]="Heist-Priorität",["ResultPriority"]="Ergebnis-Priorität",["MaximumQueuedSeconds"]="Maximales Ergebnisalter in Warteschlange (Sek.)"};
        foreach(var property in typeof(HeistOverlayConfig).GetProperties())
        {
            Control control=property.PropertyType==typeof(bool)?new CheckBox{Text=names[property.Name],AutoSize=true}:
                property.PropertyType==typeof(int)?new NumericUpDown{Minimum=0,Maximum=1000,Width=130}:new ComboBox{Width=390,DropDownStyle=ComboBoxStyle.DropDown};
            _heistOverlayControls[property.Name]=control;
            root.Controls.Add(control is CheckBox?control:CreateSettingEditor(names[property.Name],control));
        }
        root.Controls.Add(_heistStartMessagesCheck);
        var refresh=NewActionButton("OBS-Szenen & Browserquellen laden");
        refresh.Click+=async(_,_)=>
        {
            refresh.Enabled=false;
            try
            {
                if(_obs?.IsConnected!=true)throw new InvalidOperationException("Bitte OBS verbinden oder Quellnamen manuell auswählen/eintragen.");
                var sources=await Task.Run(_obs.GetBrowserSourceNames);var scenes=await Task.Run(_obs.GetSceneNames);
                Fill("BrowserSource",sources);Fill("Scene",scenes);
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"OBS-Auswahl");}finally{refresh.Enabled=true;}
        };
        void Fill(string key,IEnumerable<string> values){var combo=(ComboBox)_heistOverlayControls[key];var old=combo.Text;combo.Items.Clear();combo.Items.AddRange(values.Cast<object>().ToArray());combo.Text=old;}
        var setup=NewActionButton("Browserquelle in OBS einrichten");setup.Click+=async(_,_)=>
        {
            setup.Enabled=false;
            try
            {
                var h=new HeistConfig();ReadHeistOverlaySettings(h);h.Overlay.Validate();
                if(_obs?.IsConnected!=true)throw new InvalidOperationException("Bitte zuerst OBS verbinden.");
                await Task.Run(()=>_obs.EnsureCommunityOverlay(h.Overlay));
                MessageBox.Show(this,"Browserquelle eingerichtet. Ohne Event bleibt sie transparent. Bitte Einstellungen speichern.","Community-Overlay");
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"OBS-Overlay");}finally{setup.Enabled=true;}
        };
        var copy=NewActionButton("Browserquellen-URL kopieren");copy.Click+=(_,_)=>Clipboard.SetText(AdventureOverlayServer.Url);
        var preview=NewActionButton("Sichere lokale Vorschau öffnen");preview.Click+=(_,_)=>OpenHeistPreview();
        var save=NewActionButton("Overlay-Einstellungen speichern");save.Click+=(_,_)=>SaveHeistSettingsFromControls();
        foreach(var button in new[]{refresh,setup,copy,preview,save})root.Controls.Add(button);
        LoadHeistOverlaySettings(new());return root;
    }
    private void LoadHeistOverlaySettings(HeistConfig h)
    {
        _heistOverlayEditor=JsonSerializer.Deserialize<HeistOverlayConfig>(JsonSerializer.Serialize(h.Overlay??new()))!;
        _heistStartMessagesCheck.Checked=h.SendStartMessages;
        foreach(var property in typeof(HeistOverlayConfig).GetProperties())if(_heistOverlayControls.TryGetValue(property.Name,out var control))
        {
            var value=property.GetValue(_heistOverlayEditor);
            if(control is CheckBox check)check.Checked=(bool)value!;
            else if(control is NumericUpDown number)number.Value=Math.Clamp((int)value!,0,1000);
            else control.Text=(string?)value??"";
        }
    }
    private void ReadHeistOverlaySettings(HeistConfig h)
    {
        var copy=JsonSerializer.Deserialize<HeistOverlayConfig>(JsonSerializer.Serialize(_heistOverlayEditor))!;
        foreach(var property in typeof(HeistOverlayConfig).GetProperties())if(_heistOverlayControls.TryGetValue(property.Name,out var control))
            property.SetValue(copy,control is CheckBox check?(object)check.Checked:control is NumericUpDown number?(int)number.Value:control.Text.Trim());
        copy.Validate();h.Overlay=copy;h.SendStartMessages=_heistStartMessagesCheck.Checked;
    }
    private void OpenHeistPreview()
    {
        try
        {
            var directory=Path.Combine(Path.GetTempPath(),"RaidClipPlugin-OverlayPreview");Directory.CreateDirectory(directory);
            var file=Path.Combine(directory,"heist-preview.html");
            File.WriteAllText(file,HeistOverlayPage.Html.Replace("location.pathname==='/preview'","true"));
            Process.Start(new ProcessStartInfo(file){UseShellExecute=true});
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Heist-Vorschau");}
    }
}
