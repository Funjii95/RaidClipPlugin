using RaidClipPlugin.Services;
namespace RaidClipPlugin;
public sealed partial class MainForm
{
    private readonly GroqSettingsStore _groqStore=new();
    private readonly GroqClient _groqClient=new();
    private GroqOptions _groqOptions=new();
    private GroqDailyService _groqDaily=null!;
    private TabPage BuildGroqDailyTab()
    {
        var page=new TabPage("Groq · Daily"){BackColor=SurfaceColor,ForeColor=TextColor,Padding=new Padding(12)};
        var root=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var enabled=NewCheck("Groq für erfolgreiche !daily-Antworten aktivieren",false);
        var model=new ComboBox{Width=440,DropDownStyle=ComboBoxStyle.DropDown};model.Items.AddRange(new object[]{"llama-3.3-70b-versatile","llama-3.1-8b-instant","openai/gpt-oss-20b"});
        var personality=new TextBox{Width=600,Height=95,Multiline=true,MaxLength=1500};
        var key=new TextBox{Width=440,UseSystemPasswordChar=true,MaxLength=500,PlaceholderText="Neuer API-Key; leer lässt den gespeicherten Schlüssel unverändert"};
        var cooldown=NewNumber(10,10,600);
        var status=new Label{AutoSize=true,MaximumSize=new Size(650,0),ForeColor=MutedTextColor};
        try{_groqOptions=_groqStore.Load();_groqOptions.Validate();status.Text=string.IsNullOrWhiteSpace(_groqStore.Key())?"Noch kein API-Key gespeichert.":"API-Key geschützt gespeichert.";}
        catch{_groqOptions=new();status.Text="Groq-Konfiguration nicht lesbar. Bitte neu speichern.";}
        _groqDaily=new(_groqClient,()=>_groqOptions,()=>_groqStore.Key());
        enabled.Checked=_groqOptions.Enabled;model.Text=_groqOptions.Model;personality.Text=_groqOptions.Personality;cooldown.Value=_groqOptions.CooldownSeconds;
        GroqOptions Read()=>new(){Enabled=enabled.Checked,Model=model.Text.Trim(),Personality=personality.Text.Trim(),CooldownSeconds=(int)cooldown.Value};
        root.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(650,0),Text="Abwechslung für !daily: Die echte Belohnung und der Kontostand bleiben im festen Plugin-Text. Groq ergänzt einen kurzen Otter-Spruch. Andere Befehle und Gespräche verwenden keine KI."});
        root.Controls.Add(enabled);root.Controls.Add(CreateSettingEditor("Groq-Modell",model));root.Controls.Add(CreateSettingEditor("Otter-Persönlichkeit",personality));root.Controls.Add(CreateSettingEditor("API-Key (Windows-geschützt)",key));root.Controls.Add(CreateSettingEditor("Pause zwischen KI-Anfragen (Sekunden)",cooldown));
        root.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(650,0),Text="Groq erhält nur einen allgemeinen Daily-Anlass und den letzten KI-Spruch, keine Chatnachrichten, Spielernamen oder Kontostände. API-Nutzung verbraucht dein Groq-Kontingent. Bei Fehlern, leerem Key oder während der Pause bleibt der normale Daily-Text erhalten."});
        var buttons=new FlowLayoutPanel{AutoSize=true};var save=NewHeistActionButton("Groq speichern",150);var test=NewHeistActionButton("Verbindung testen",170);var remove=NewHeistActionButton("API-Key entfernen",170);
        save.Click+=(_,_)=>{
            try{var options=Read();options.Validate();var newKey=key.Text.Trim();if(options.Enabled&&newKey.Length==0&&string.IsNullOrWhiteSpace(_groqStore.Key()))throw new InvalidOperationException("Bitte zuerst einen Groq-API-Key eingeben.");if(newKey.Length>0)_groqStore.SaveKey(newKey);_groqStore.Save(options);_groqOptions=options;key.Clear();status.Text=options.Enabled?"Groq ist für !daily aktiv. Einstellungen gelten sofort.":"Groq deaktiviert. Normale Daily-Antworten aktiv.";}
            catch(InvalidOperationException error){status.Text=error.Message;}
            catch{status.Text="Einstellungen konnten nicht gespeichert werden.";}
        };
        test.Click+=async(_,_)=>{
            test.Enabled=false;status.Text="Groq-Verbindung wird geprüft; keine Chatnachricht wird gesendet …";
            try{var options=Read();options.Validate();var apiKey=string.IsNullOrWhiteSpace(key.Text)?_groqStore.Key():key.Text.Trim();if(string.IsNullOrWhiteSpace(apiKey))throw new InvalidOperationException("Bitte einen Groq-API-Key eingeben.");var text=await _groqClient.CompleteAsync(options,apiKey,[new("system","Antworte mit einem kurzen freundlichen Otter-Spruch auf Deutsch, ohne Zahlen, Links oder Namen."),new("user","Ein Daily wurde abgeholt.")],CancellationToken.None);var cleaned=GroqDailyService.Clean(text);status.Text=cleaned.Length>0?"Verbindung erfolgreich · Vorschau: "+cleaned:"Verbindung erfolgreich; Antwort enthielt keinen passenden kurzen Spruch.";}
            catch(InvalidOperationException error){status.Text=error.Message;}
            catch{status.Text="Groq nicht erreichbar. Verbindung, API-Key und Modell prüfen.";}
            finally{if(!test.IsDisposed)test.Enabled=true;}
        };
        remove.Click+=(_,_)=>{try{_groqStore.DeleteKey();enabled.Checked=false;var options=Read();_groqStore.Save(options);_groqOptions=options;key.Clear();status.Text="API-Key entfernt. Groq deaktiviert.";}catch{status.Text="API-Key konnte nicht entfernt werden.";}};
        buttons.Controls.AddRange(new Control[]{save,test,remove});root.Controls.Add(buttons);root.Controls.Add(status);page.Controls.Add(root);
        FormClosed+=(_,_)=>_groqClient.Dispose();return page;
    }
}
