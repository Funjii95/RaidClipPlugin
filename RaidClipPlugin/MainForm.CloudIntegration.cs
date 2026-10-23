using System.Security.Cryptography;
using System.Text.Json;
using RaidClipPlugin.Services;
using SnapshotDelivery;
using SnapshotProtocol;

namespace RaidClipPlugin;

public partial class MainForm
{
    internal void CloseSmoke(){_explicitExitRequested=true;Close();}
    private readonly StreamAdmission _streamAdmission=new();
    private Guid _streamEpoch;
    private Task? _cloudSyncTask;
    private Task? _actionChannelTask;
    private volatile bool _cloudSyncReady;
    private readonly CancellationTokenSource _cloudStop=new();
    private void InitializeCloudIntegration()
    {
        if (TestDataPaths.IsTestBuild) Text += " – Isolierter Integrationstest";
        _viewerPoints.Admission=_streamAdmission;
        Shown+=(_,_)=>{if(!_administrationTestOnly){_cloudSyncTask=Task.Run(()=>RunCloudSyncAsync(_cloudStop.Token));_selfDeletionTask=Task.Run(()=>RunSelfDeletionAsync(_cloudStop.Token));}};
        FormClosed+=async(_,_)=>
        {
            _streamAdmission.End();_cloudStop.Cancel();_cloudStatusTip.Dispose();
            if(_cloudSyncTask is not null)try{await _cloudSyncTask.WaitAsync(TimeSpan.FromSeconds(5));}catch{}
            if(_selfDeletionTask is not null)try{await _selfDeletionTask.WaitAsync(TimeSpan.FromSeconds(5));}catch{}
        };
    }
    private readonly Label _cloudIntegrationStatusLabel=new() {
        Text="RPG-Cloud: Verbindung wird geprüft …",Dock=DockStyle.Fill,AutoSize=false,AutoEllipsis=true,
        ForeColor=Color.DimGray,Font=new Font("Segoe UI",9F),TextAlign=ContentAlignment.TopLeft
    };
    private readonly ToolTip _cloudStatusTip=new();
    private void CloudLog(string status)
    {
        if(IsDisposed)return;
        try{BeginInvoke(()=>{_cloudIntegrationStatusLabel.Text="RPG-Cloud: "+status;_cloudStatusTip.SetToolTip(_cloudIntegrationStatusLabel,status);AppendLog(status);});}catch(InvalidOperationException){}
    }
    private async Task RunCloudSyncAsync(CancellationToken ct)
    {
        try
        {
            var file=Path.Combine(TestDataPaths.Root,"cloud-sync.json");
            if(!File.Exists(file)){CloudLog("Cloud-Sync deaktiviert: getrennte Konfiguration cloud-sync.json fehlt.");return;}
            var options=JsonSerializer.Deserialize<CloudSyncOptions>(await File.ReadAllTextAsync(file,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            if(!options.Enabled){CloudLog("Cloud-Sync deaktiviert.");return;}
            options.Validate();
            var secret=ProtectedData.Unprotect(await File.ReadAllBytesAsync(options.ProtectedKeyFile,ct),null,DataProtectionScope.CurrentUser);
            HttpSnapshotSender sender;HttpSnapshotHeadReader reader;
            try
            {
                var endpoint=new Uri(options.Endpoint);var key=Convert.ToBase64String(secret);
                sender=new(endpoint,key);reader=new(endpoint,key,options.ChannelId);
            }
            finally{CryptographicOperations.ZeroMemory(secret);}
            using(sender)using(reader)
            {
                var outbox=new LocalOutboxStore(Path.Combine(TestDataPaths.Root,"cloud-sync-outbox"),options.ChannelId);
                var excludedNames=_configurationService.LoadForEditing().Minigame.PointsBlacklist.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var worker=new IntegratedSnapshotWorker(_viewerPoints,outbox,options.ChannelId,options.AllowedUserIds,
                    token=>reader.RequireFullProjectionAsync(options.AllowedUserIds,token,options.AutomaticPlayerEnrollment),reader.ReadAsync,sender.SendAsync,PurgeCloudPlayerActionsAsync,options.AutomaticPlayerEnrollment,
                    (id,name)=>id!="216527497" && !name.Equals("SoundAlerts",StringComparison.OrdinalIgnoreCase) && !excludedNames.Contains(name) && !(_minigame?.RemoteConfiguration.PointsBlacklist.Contains(name,StringComparer.OrdinalIgnoreCase)??false));
                worker.StatusChanged+=CloudLog;
                worker.AvailabilityChanged+=ready=>_cloudSyncReady=ready;
                await worker.RunAsync(ct);
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch{CloudLog("Cloud-Sync nicht gestartet: Konfiguration oder DPAPI-Zugang prüfen. Plugin bleibt nutzbar.");}
    }
    private void StartActionChannel(CancellationToken ct)
    {
        var epoch=_streamEpoch;var minigame=_minigame!;
        _actionChannelTask=Task.Run(async()=>{
            try {
                var file=Path.Combine(TestDataPaths.Root,"action-channel.json");
                if(!File.Exists(file))return;
                var options=JsonSerializer.Deserialize<ActionChannelOptions>(await File.ReadAllTextAsync(file,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
                if(!options.Enabled)return;options.Validate();
                var sync=JsonSerializer.Deserialize<CloudSyncOptions>(await File.ReadAllTextAsync(Path.Combine(TestDataPaths.Root,"cloud-sync.json"),ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
                sync.Validate();
                if(!sync.Enabled || sync.ChannelId!=options.ChannelId || options.AutomaticPlayerEnrollment!=sync.AutomaticPlayerEnrollment || options.AllowedUserIds.Any(id=>!sync.AllowedUserIds.Contains(id)))throw new InvalidDataException("Action users require the enabled snapshot pipeline.");
                var secret=ProtectedData.Unprotect(await File.ReadAllBytesAsync(options.ProtectedKeyFile,ct),null,DataProtectionScope.CurrentUser);
                string key;try{if(secret.Length is <32 or >64)throw new InvalidDataException();key=Convert.ToBase64String(secret);}finally{CryptographicOperations.ZeroMemory(secret);}
                var executor=new RemoteRpgExecutor(_viewerPoints,()=>minigame.RemoteConfiguration,_streamAdmission,epoch,options.ChannelId,
                    options.AllowedUserIds.ToHashSet(StringComparer.Ordinal),Path.Combine(TestDataPaths.Root,"action-receipts"),
                    (command,name)=>_commandRegistry.Commands.Any(c=>c.CommandText==command && c.Enabled && c.RequiredRole==RaidClipPlugin.Models.CommandRole.Viewer),minigames:minigame,enrolled:options.AutomaticPlayerEnrollment?id=>IsAutomaticallyEnrolled(id):null,saveUpgrades:upgrades=>{var saved=_configurationService.LoadForEditing();saved.Minigame.Adventures.Rpg.Upgrades=upgrades;_configurationService.SaveGuiSettings(saved);},saveZones:zones=>{var saved=_configurationService.LoadForEditing();saved.Minigame.Adventures.Rpg.Zones=zones;_configurationService.SaveGuiSettings(saved);},readAdminConfiguration:()=>_configurationService.LoadForEditing(),saveAdminConfiguration:saved=>{_configurationService.SaveGuiSettings(saved);minigame.UpdateConfiguration(saved);});
                using var client=new CloudActionClient(new Uri(options.BackendUrl),key,executor,()=>_streamAdmission.CanPlay(epoch)&&_cloudSyncReady,CloudLog);
                await client.RunAsync(ct);
            }catch(OperationCanceledException)when(ct.IsCancellationRequested){}catch{CloudLog("Aktionskanal nicht gestartet: getrennte Konfiguration/Zugang prüfen.");}
        },ct);
    }
    private async Task PurgeCloudPlayerActionsAsync(string id,CancellationToken ct)
    {
        var path=Path.Combine(TestDataPaths.Root,"action-channel.json");
        var options=JsonSerializer.Deserialize<ActionChannelOptions>(await File.ReadAllTextAsync(path,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        options.Validate();if(!options.Enabled || !(options.AllowedUserIds.Contains(id)||options.AutomaticPlayerEnrollment))throw new InvalidOperationException("Cloud-Löschfreigabe für diesen Spieler fehlt.");
        var protectedKey=ProtectedData.Unprotect(await File.ReadAllBytesAsync(options.ProtectedKeyFile,ct),null,DataProtectionScope.CurrentUser);
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,CheckCertificateRevocationList=true}){BaseAddress=new Uri(options.BackendUrl),Timeout=TimeSpan.FromSeconds(8)};
        try{http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",Convert.ToBase64String(protectedKey));}
        finally{CryptographicOperations.ZeroMemory(protectedKey);}
        using var response=await http.PostAsync("/api/v1/plugin/actions/privacy/"+id,null,ct);response.EnsureSuccessStatusCode();
        var content=await response.Content.ReadAsStringAsync(ct);if(content.Length>1024)throw new InvalidDataException("Ungültige Löschbestätigung.");
        using var json=JsonDocument.Parse(content);if(!json.RootElement.TryGetProperty("erased",out var erased)||erased.ValueKind!=JsonValueKind.True)throw new InvalidDataException("Löschung nicht bestätigt.");
    }
    private bool IsAutomaticallyEnrolled(string id)
    {
        if(id.Length is <1 or >25 || id.Any(c=>c is <'0' or >'9') || id=="216527497")return false;
        var path=Path.Combine(TestDataPaths.Root,"cloud-sync-outbox","42812664-"+id+".outbox.json");
        try{
            var state=JsonSerializer.Deserialize<OutboxState>(File.ReadAllText(path));
            return state is not null && state.UserId==id && state.ChannelId=="42812664" && !state.Deleted && (state.Pending.Length==0 || state.Revision>1);
        }catch{return false;}
    }
    private void BeginStreamSession()
    {
        _streamEpoch=_streamAdmission.Begin(()=>_shutdown is {IsCancellationRequested:false} &&
            _minigame is {IsRunning:true} && _chatModeration?.Diagnostics is {WebSocketConnected:true,SubscriptionEnabled:true} &&
            DateTimeOffset.UtcNow-_minigame.LastHeartbeatUtc<=TimeSpan.FromSeconds(20) &&
            _minigame.RemoteConfiguration is {Enabled:true,PointsEnabled:true,Adventures.Rpg.Enabled:true});
    }
}

