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
    private Task? _streamMonitorTask;
    private Task? _cloudSyncTask;
    private readonly CancellationTokenSource _cloudStop=new();
    private void InitializeCloudIntegration()
    {
        Text += " – Cloud Integration Test 2.1.22";
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
                await worker.RunAsync(ct);
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch{CloudLog("Cloud-Sync nicht gestartet: Konfiguration oder DPAPI-Zugang prüfen. Plugin bleibt nutzbar.");}
    }
    private void BeginStreamSession()
    {
        _streamEpoch=_streamAdmission.Begin(()=>_shutdown is {IsCancellationRequested:false} &&
            _minigame is {IsRunning:true} && _chatModeration?.Diagnostics is {WebSocketConnected:true,SubscriptionEnabled:true} &&
            DateTimeOffset.UtcNow-_minigame.LastHeartbeatUtc<=TimeSpan.FromSeconds(20));
    }
    private void StartLiveConfirmation(TwitchService twitch,string broadcaster,CancellationToken ct)
    {
        var epoch=_streamEpoch;
        _streamMonitorTask=Task.Run(async()=>
        {
            try
            {
                while(true)
                {
                    ct.ThrowIfCancellationRequested();
                    var check=_streamAdmission.StartCheck(epoch);
                    try
                    {
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(5));
                        var live=await twitch.GetLiveStreamAsync(broadcaster,deadline.Token);
                        _streamAdmission.Observe(check,live is {IsLive:true} && live.BroadcasterId==broadcaster ? live.Id : null);
                    }
                    catch{_streamAdmission.Observe(check,null);}
                    await Task.Delay(TimeSpan.FromSeconds(10),ct);
                }
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        },ct);
    }
}
