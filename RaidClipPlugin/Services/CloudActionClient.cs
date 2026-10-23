using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;

internal sealed class ActionChannelOptions
{
    public bool Enabled {get;set;}
    public bool AutomaticPlayerEnrollment {get;set;}
    public bool SelfDeletionEnabled {get;set;}
    public string BackendUrl {get;set;}="https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app";
    public string ChannelId {get;set;}="42812664";
    public string[] AllowedUserIds {get;set;}=[];
    public string ProtectedKeyFile {get;set;}="";
    public void Validate(){if((BackendUrl!="https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app" && BackendUrl!="https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app") || ChannelId!="42812664" ||
        AllowedUserIds.Length is <1 or >100 || AllowedUserIds.Any(id=>id.Length is <1 or >25 || id.Any(c=>c is <'0' or >'9')) || !Path.IsPathFullyQualified(ProtectedKeyFile))throw new InvalidDataException("Invalid explicit test action scope.");}
}
internal sealed class CloudActionClient : IDisposable
{
    private readonly HttpClient http;
    private readonly RemoteRpgExecutor executor;
    private readonly Func<bool> ready;
    private readonly Action<string> log;
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private Guid session;
    private long sequence;
    internal CloudActionClient(Uri backend,string key,RemoteRpgExecutor executor,Func<bool> ready,Action<string> log)
    {
        http=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,CheckCertificateRevocationList=true}){BaseAddress=backend,Timeout=TimeSpan.FromSeconds(7)};
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key);this.executor=executor;this.ready=ready;this.log=log;
    }
    internal CloudActionClient(HttpClient transport,RemoteRpgExecutor executor,Func<bool> ready,Action<string> log)
    {http=transport;this.executor=executor;this.ready=ready;this.log=log;}
    private async Task<T?> Request<T>(HttpMethod method,string path,object? body,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(7));ct=deadline.Token;
        using var request=new HttpRequestMessage(method,path);
        if(body is not null)request.Content=new StringContent(JsonSerializer.Serialize(body,Json),System.Text.Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        if(response.StatusCode==HttpStatusCode.NoContent)return default;
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var chunk=new byte[4096];
        int n;while((n=await input.ReadAsync(chunk,ct))>0){buffer.Write(chunk,0,n);if(buffer.Length>100000)throw new InvalidDataException("Oversized relay response.");}
        return JsonSerializer.Deserialize<T>(buffer.ToArray(),Json);
    }
    internal async Task RunAsync(CancellationToken ct)
    {
        var nextBeat=DateTimeOffset.MinValue;
        var failures=0;
        ActionEnvelope? pending=null;ActionOutcome? outcome=null;TimeSpan executionWindow=TimeSpan.Zero;
        try {
            while(!ct.IsCancellationRequested){
                try {
                    if(!ready()){
                        if(session!=Guid.Empty)await Close();
                        session=Guid.Empty;pending=null;outcome=null;
                        await Task.Delay(1000,ct);continue;
                    }
                    if(session==Guid.Empty){session=(await Request<RelaySession>(HttpMethod.Post,"/api/v1/plugin/actions/session",null,ct))!.SessionId;sequence=0;nextBeat=DateTimeOffset.MinValue;}
                    if(DateTimeOffset.UtcNow>=nextBeat){
                        var heartbeat=await Request<RelaySession>(HttpMethod.Post,"/api/v1/plugin/actions/heartbeat",new RelayHeartbeat(session,++sequence,DateTimeOffset.UtcNow,true,executor.CatalogJson()),ct);
                        if(heartbeat!.ExpiresAt<=DateTimeOffset.UtcNow)throw new InvalidOperationException("Session expired.");
                        nextBeat=DateTimeOffset.UtcNow.AddSeconds(4);
                    }
                    if(pending is null){
                        var issued=System.Diagnostics.Stopwatch.GetTimestamp();
                        pending=await Request<ActionEnvelope>(HttpMethod.Post,"/api/v1/plugin/actions/claim/"+session,null,ct);
                        if(pending is not null){
                            var serverWindow=pending.ExecuteBefore-pending.ObservedAt;
                            if(serverWindow<=TimeSpan.Zero || serverWindow>TimeSpan.FromSeconds(8))throw new InvalidDataException("Invalid execution lease.");
                            executionWindow=serverWindow-System.Diagnostics.Stopwatch.GetElapsedTime(issued);
                        }
                    }
                    var handled=pending is not null;
                    if(pending is not null){
                        if(pending.SessionId!=session)throw new InvalidDataException("Session mismatch.");
                        outcome??=ready()?await executor.ExecuteAsync(pending,ct,executionWindow):new(false,"not_ready","Cloud-Synchronisierung oder Sitzung nicht bereit.");
                        await Request<ActionReceipt>(HttpMethod.Post,"/api/v1/plugin/actions/complete",new RelayComplete(session,pending.Intent.ActionId,outcome),ct);
                        pending=null;outcome=null;
                    }
                    // Drain accepted work promptly; keep bounded polling when no work exists.
                    failures=0;
                    await Task.Delay(handled?100:500,ct);
                } catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
                catch(HttpRequestException error) when(error.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden){log("Aktionskanal gesperrt: geschützten Zugang prüfen.");return;}
                catch(Exception error) {
                    // A pending outcome is already durable. Never invoke a second action to compensate.
                    log("Aktionskanal unterbrochen ("+(error is HttpRequestException network?"HTTP "+(int?)network.StatusCode:error.GetType().Name)+"); unklare Aktionen werden nicht erneut gebucht.");
                    if(pending is not null && outcome is not null){try{await Request<ActionReceipt>(HttpMethod.Post,"/api/v1/plugin/actions/complete",new RelayComplete(session,pending.Intent.ActionId,outcome),ct);}catch{}}
                    session=Guid.Empty;pending=null;outcome=null;
                    failures=Math.Min(failures+1,5);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(60,3*Math.Pow(2,failures-1))),ct);
                }
            }
        } catch(OperationCanceledException) when(ct.IsCancellationRequested){}
        finally{await Close();}
    }
    private async Task Close(){if(session==Guid.Empty)return;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));try{await Request<bool>(HttpMethod.Delete,"/api/v1/plugin/actions/session/"+session,null,timeout.Token);}catch{}}
    public void Dispose()=>http.Dispose();
}
