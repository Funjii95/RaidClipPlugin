using System.Net.Http.Json;
using System.Text.Json;
using Backend.Tests;
using RaidClipPlugin.Services;
using SnapshotDelivery;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class SelfDeletionEndToEndTests
{
    [Fact]public async Task Viewer_request_to_plugin_and_snapshot_tombstone_recovers_a_lost_cloud_ack(){
        var root=Path.Combine(Path.GetTempPath(),"otter-self-delete-e2e-"+Guid.NewGuid());
        try{
            await using var firestore=await TransactionalFirestoreFixture.StartAsync();
            var relayStore=new Backend.FirestoreRelayStore(firestore.Database,"42","syntheticDeletion");
            await using var api=new ActionApiFixture(store:relayStore,privacy:true,selfDeletion:true);
            var directory=Path.Combine(root,"minigame");var store=new ViewerPointStore(directory);
            await store.SetPointsAsync("1001","SYNTHETIC",500,0,default);await store.SetPointsAsync("1002","OTHER",600,0,default);
            using var document=JsonDocument.Parse((await store.ReadCommittedFileAsync(default))!);
            var baseline=new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,document.RootElement.GetProperty("1001").GetRawText());
            var sink=new LocalSnapshotStore(Path.Combine(root,"cloud"),"42");await sink.ApplyAsync(baseline);
            var outbox=new LocalOutboxStore(Path.Combine(root,"outbox"),"42");await outbox.InitializeBaselineAsync(baseline,SnapshotValidation.Validate(baseline,"42").ContentHash);
            var intent=new SelfDeletionIntent(Guid.NewGuid(),"DELETE_MY_RPG_ACCOUNT");
            await api.ReadyAsync();(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).EnsureSuccessStatusCode();
            (await api.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent)).EnsureSuccessStatusCode();
            SelfDeletionWorker Privacy(ViewerPointStore s)=>new(s,new HashSet<string>{"1001"},async ct=>
                (await api.Plugin.GetFromJsonAsync<SelfDeletionJob[]>("/api/v1/plugin/actions/privacy-requests",ct))!,async(job,ct)=>{
                    (await api.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/privacy-requests/"+job.UserId+"/complete",new SelfDeletionIntent(job.RequestId,"DELETE_MY_RPG_ACCOUNT"),ct)).EnsureSuccessStatusCode();
                });
            var admission=new StreamAdmission();store.Admission=admission;admission.Begin(()=>true);
            await Privacy(store).TickAsync(default);Assert.Null(await store.ReadPrivacyRequestAsync("1001"));admission.End();
            await Privacy(store).TickAsync(default);Assert.True((await store.ReadPrivacyRequestAsync("1001"))!.LocalErased);
            var lost=true;
            Task<StoredSnapshot?> Read(string _,CancellationToken ct)=>ReadHead(ct);
            async Task<StoredSnapshot?> ReadHead(CancellationToken ct)=>JsonSerializer.Deserialize<StoredSnapshot>(await File.ReadAllTextAsync(Path.Combine(root,"cloud","42-1001.json"),ct));
            IntegratedSnapshotWorker Sync(ViewerPointStore s)=>new(s,outbox,"42",["1001"],_=>Task.CompletedTask,Read,async(message,ct)=>{
                Assert.Equal(SnapshotKind.Delete,message.Kind);Assert.Null(message.AccountJson);var decision=await sink.ApplyAsync(message,ct);
                if(lost){lost=false;throw new IOException("synthetic lost ACK");}return new DeliveryResult(DeliveryStatus.Delivered,decision);
            },async(id,ct)=>(await api.Plugin.PostAsync("/api/v1/plugin/actions/privacy/"+id,null,ct)).EnsureSuccessStatusCode());
            var interrupted=Sync(store);await interrupted.SynchronizeOnceAsync();Assert.Equal(1,interrupted.PendingPrivacyCount);
            var pending=await api.Viewer.GetStringAsync("/api/v1/play/privacy/deletion");Assert.Contains("\"completed\":false",pending);
            store=new ViewerPointStore(directory);await Sync(store).SynchronizeOnceAsync();await Privacy(store).TickAsync(default);
            Assert.True((await ReadHead(default))!.Deleted);Assert.Null(await outbox.ReadAsync("1001"));
            Assert.Contains("\"completed\":true",await api.Viewer.GetStringAsync("/api/v1/play/privacy/deletion"));
            Assert.Equal(600,await store.GetPointsAsync("1002",default));
            Assert.Equal(SnapshotDecision.RestoreRequired,await sink.ApplyAsync(baseline with{Revision=100,SnapshotId=Guid.NewGuid()}));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
