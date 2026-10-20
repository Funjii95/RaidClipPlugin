using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using RaidClipPlugin.Services;
using SnapshotProtocol;
namespace RaidClipPlugin;

public partial class MainForm
{
    private Task? _selfDeletionTask;
    private async Task RunSelfDeletionAsync(CancellationToken ct){
        try{
            var path=Path.Combine(TestDataPaths.Root,"action-channel.json");if(!File.Exists(path))return;
            var options=JsonSerializer.Deserialize<ActionChannelOptions>(await File.ReadAllTextAsync(path,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            if(!options.Enabled||!options.SelfDeletionEnabled)return;options.Validate();
            var bytes=ProtectedData.Unprotect(await File.ReadAllBytesAsync(options.ProtectedKeyFile,ct),null,DataProtectionScope.CurrentUser);
            string key;try{if(bytes.Length is <32 or >64)throw new InvalidDataException();key=Convert.ToBase64String(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,CheckCertificateRevocationList=true}){BaseAddress=new Uri(options.BackendUrl),Timeout=TimeSpan.FromSeconds(7)};
            http.DefaultRequestHeaders.Authorization=new("Bearer",key);
            var worker=new SelfDeletionWorker(_viewerPoints,options.AllowedUserIds.ToHashSet(StringComparer.Ordinal),
                async token=>{
                    using var response=await http.GetAsync("/api/v1/plugin/actions/privacy-requests",token);response.EnsureSuccessStatusCode();
                    if(response.Content.Headers.ContentLength>65536)throw new InvalidDataException();
                    var jobs=await response.Content.ReadFromJsonAsync<SelfDeletionJob[]>(cancellationToken:token)??throw new InvalidDataException();
                    if(jobs.Length>100)throw new InvalidDataException();return jobs;
                },async(job,token)=>{
                    using var response=await http.PostAsJsonAsync("/api/v1/plugin/actions/privacy-requests/"+job.UserId+"/complete",new SelfDeletionIntent(job.RequestId,"DELETE_MY_RPG_ACCOUNT"),token);response.EnsureSuccessStatusCode();
                },options.AutomaticPlayerEnrollment?id=>IsAutomaticallyEnrolled(id):null);
            while(!ct.IsCancellationRequested){
                async ValueTask Maintain(CancellationToken token){
                    if(_dataAdministrationBusy)return;
                    _dataAdministrationBusy=true;
                    try{await worker.TickAsync(token,_shutdown is null&&_minigame is null);}
                    finally{_dataAdministrationBusy=false;}
                }
                try{await InvokeAsync(Maintain,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch{CloudLog("Selbstlöschung ausstehend: Wartung, Cloud-Bereinigung oder Zugang noch nicht bestätigt.");}
                await Task.Delay(TimeSpan.FromSeconds(15),ct);
            }
        }catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch{CloudLog("Selbstlöschung nicht gestartet: Freigabe oder geschützte Konfiguration prüfen.");}
    }
}
