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
        Shown+=(_,_)=>{_cloudSyncTask=Task.Run(()=>RunCloudSyncAsync(_cloudStop.Token));};
        FormClosed+=async(_,_)=>
        {
            _streamAdmission.End();_cloudStop.Cancel();
            if(_cloudSyncTask is not null)try{await _cloudSyncTask.WaitAsync(TimeSpan.FromSeconds(5));}catch{}
        };
    }
    private void CloudLog(string status)
    {
        if(IsDisposed)return;
        try{BeginInvoke(()=>AppendLog(status));}catch(InvalidOperationException){}
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
                var worker=new IntegratedSnapshotWorker(_viewerPoints,outbox,options.ChannelId,options.AllowedUserIds,
                    token=>reader.RequireFullProjectionAsync(options.AllowedUserIds,token),reader.ReadAsync,sender.SendAsync);
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
                if(!sync.Enabled || sync.ChannelId!=options.ChannelId || options.AllowedUserIds.Any(id=>!sync.AllowedUserIds.Contains(id)))throw new InvalidDataException("Action users require the enabled snapshot pipeline.");
                var secret=ProtectedData.Unprotect(await File.ReadAllBytesAsync(options.ProtectedKeyFile,ct),null,DataProtectionScope.CurrentUser);
                string key;try{if(secret.Length is <32 or >64)throw new InvalidDataException();key=Convert.ToBase64String(secret);}finally{CryptographicOperations.ZeroMemory(secret);}
                var executor=new RemoteRpgExecutor(_viewerPoints,()=>minigame.RemoteConfiguration,_streamAdmission,epoch,options.ChannelId,
                    options.AllowedUserIds.ToHashSet(StringComparer.Ordinal),Path.Combine(TestDataPaths.Root,"action-receipts"),
                    (command,name)=>_commandRegistry.Commands.Any(c=>c.CommandText==command && c.Enabled && c.RequiredRole==RaidClipPlugin.Models.CommandRole.Viewer));
                using var client=new CloudActionClient(new Uri(options.BackendUrl),key,executor,()=>_streamAdmission.CanPlay(epoch)&&_cloudSyncReady,CloudLog);
                await client.RunAsync(ct);
            }catch(OperationCanceledException)when(ct.IsCancellationRequested){}catch{CloudLog("Aktionskanal nicht gestartet: getrennte Konfiguration/Zugang prüfen.");}
        },ct);
    }
    private void BeginStreamSession()
    {
        _streamEpoch=_streamAdmission.Begin(()=>_shutdown is {IsCancellationRequested:false} &&
            _minigame is {IsRunning:true} && _chatModeration?.Diagnostics is {WebSocketConnected:true,SubscriptionEnabled:true} &&
            DateTimeOffset.UtcNow-_minigame.LastHeartbeatUtc<=TimeSpan.FromSeconds(20) &&
            _minigame.RemoteConfiguration is {Enabled:true,PointsEnabled:true,Adventures.Rpg.Enabled:true});
    }
}
