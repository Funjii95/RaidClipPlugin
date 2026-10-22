using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RaidClipPlugin.Services;
public sealed class GroqOptions
{
    public bool Enabled {get;set;}
    public string Model {get;set;}="llama-3.3-70b-versatile";
    public string Personality {get;set;}="Du bist ein freundlicher, frecher Otter im Twitch-Chat von Funjii95. Kurze deutsche Sprüche, spielerisch und abwechslungsreich, keine Beleidigungen.";
    public int CooldownSeconds {get;set;}=10;
    public void Validate(){if(string.IsNullOrWhiteSpace(Model)||Model.Length>100||Model.Any(char.IsWhiteSpace)||Personality.Length>1500||CooldownSeconds is <10 or >600)throw new InvalidOperationException("Bitte Modell, Persönlichkeit und Antwortpause (10–600 Sekunden) prüfen.");}
}
public sealed class GroqSettingsStore(string? directory=null)
{
    private readonly string root=directory??Path.Combine(TestDataPaths.LocalAppData,"RaidClipPlugin");
    public GroqOptions Load()=>File.Exists(Path.Combine(root,"groq.json"))?JsonSerializer.Deserialize<GroqOptions>(File.ReadAllText(Path.Combine(root,"groq.json")))??new():new();
    public void Save(GroqOptions options){options.Validate();Directory.CreateDirectory(root);var file=Path.Combine(root,"groq.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(options,new JsonSerializerOptions{WriteIndented=true}));File.Move(file+".tmp",file,true);}
    public string Key()=>File.Exists(Path.Combine(root,"groq-key.dpapi"))?WindowsProtectedStore.UnprotectJson<string>(File.ReadAllBytes(Path.Combine(root,"groq-key.dpapi")))??"":"";
    public void SaveKey(string key){if(string.IsNullOrWhiteSpace(key)||key.Length>500||key.Any(char.IsWhiteSpace))throw new InvalidOperationException("Ungültiger API-Key.");Directory.CreateDirectory(root);var file=Path.Combine(root,"groq-key.dpapi");File.WriteAllBytes(file+".tmp",WindowsProtectedStore.ProtectJson(key));File.Move(file+".tmp",file,true);}
    public void DeleteKey(){var p=Path.Combine(root,"groq-key.dpapi");if(File.Exists(p))File.Delete(p);}
}
public sealed record GroqTurn(string role,string content);
public sealed class GroqClient:IDisposable
{
    private readonly HttpClient http;
    public GroqClient(HttpMessageHandler? handler=null){http=handler is null?new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}):new(handler);http.Timeout=TimeSpan.FromSeconds(5);}
    public async Task<string> CompleteAsync(GroqOptions options,string key,IReadOnlyList<GroqTurn> messages,CancellationToken ct)
    {
        options.Validate();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.groq.com/openai/v1/chat/completions");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        request.Content=new StringContent(JsonSerializer.Serialize(new{model=options.Model,messages,temperature=0.9,max_completion_tokens=160,stream=false}),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Groq meldet HTTP {(int)response.StatusCode}. API-Key, Modell und Kontingent prüfen.");
        await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var buffer=new MemoryStream();var block=new byte[4096];int n;
        while((n=await stream.ReadAsync(block,deadline.Token))>0){buffer.Write(block,0,n);if(buffer.Length>65536)throw new InvalidOperationException("Groq-Antwort zu groß.");}
        using var json=JsonDocument.Parse(buffer.ToArray());return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"";
    }
    public void Dispose()=>http.Dispose();
}
public sealed class GroqDailyService(GroqClient client,Func<GroqOptions> options,Func<string> key,TimeProvider? time=null)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly TimeProvider clock=time??TimeProvider.System;
    private DateTimeOffset next;
    private string previous="";
    public static string Clean(string raw)
    {
        var text=Regex.Replace(raw??"",@"<think>[\s\S]*?</think>","",RegexOptions.IgnoreCase);
        text=Regex.Replace(text,@"[\r\n\t]+"," ").Trim().Trim('"');text=Regex.Replace(text,@"[\p{Cc}\p{Cf}]","");
        if(string.IsNullOrWhiteSpace(text)||text.StartsWith('/')||Regex.IsMatch(text,@"[0-9%!@]|https?://|www\.|\b(HP|EXP|Schaden|Cooldown|Chance|Talentpunkte|erhält|bekommt|gewinnst|verlierst)\b",RegexOptions.IgnoreCase))return "";
        if(text.Length>180){text=text[..177];if(char.IsHighSurrogate(text[^1]))text=text[..^1];text+="…";}return text;
    }
    public static string Compose(string original,string? flavor)=>string.IsNullOrWhiteSpace(flavor)||original.Length+flavor.Length+1>480?original:original+" "+flavor;
    public async Task<string?> GenerateAsync(CancellationToken ct)
    {
        var config=options();if(!config.Enabled||clock.GetUtcNow()<next||!await gate.WaitAsync(0,ct))return null;
        try
        {
            if(clock.GetUtcNow()<next)return null;var secret=key();if(string.IsNullOrWhiteSpace(secret))return null;
            next=clock.GetUtcNow().AddSeconds(Math.Clamp(config.CooldownSeconds,10,600));
            var turns=new List<GroqTurn>{new("system",config.Personality+" Schreibe einen atmosphärischen Otter-Spruch zur erfolgreichen Abholung der täglichen RPG-Belohnung. Höchstens ein Satz und 180 Zeichen. Keine Zahlen, Beträge, Spielwerte, Belohnungszusagen, Namen, Links, Erwähnungen, Chatbefehle oder Markdown. Die echte Belohnung steht bereits im festen Nachrichtentext.")};
            if(previous.Length>0)turns.Add(new("system","Vermeide die Wiederholung dieses letzten Spruchs: "+previous));
            turns.Add(new("user","Die tägliche Belohnung wurde erfolgreich gespeichert. Ein frischer Otter-Spruch dazu, bitte."));
            var answer=Clean(await client.CompleteAsync(config,secret,turns,ct));if(answer.Length==0||answer==previous||!options().Enabled||ct.IsCancellationRequested)return null;
            previous=answer;return "🦦 KI: "+answer;
        }
        catch(Exception){return null;} // Never change or repeat the committed reward on an AI failure.
        finally{gate.Release();}
    }
}
