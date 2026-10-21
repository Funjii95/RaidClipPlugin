using System.Text.Json;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using SnapshotDelivery;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class DataAdministrationTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"OtterAdministration-"+Guid.NewGuid().ToString("N"));
    private string DirectoryPath=>Path.Combine(root,"minigame");
    private async Task<ViewerPointStore> Seed(){
        var store=new ViewerPointStore(DirectoryPath);
        foreach(var id in new[]{"1001","1002"}){
            await store.SetPointsAsync(id,"SYNTHETIC-"+id,10000,0,default);
            await store.ApplyRpgAsync(id,"SYNTHETIC-"+id,"seed",p=>{
                p.Rpg.PlayerLevel=7;p.Rpg.Experience=222;p.Rpg.Perks["crit"]=3;p.Rpg.Cooldowns["!dive"]=DateTimeOffset.UtcNow.AddHours(1);
                p.Rpg.Inventory.Add(new(){InstanceId="item-"+id,Slot=EquipmentSlot.Weapon,IsLocked=true,IsFavorite=true});p.Rpg.Equipment[EquipmentSlot.Weapon]="item-"+id;
                p.Rpg.Fish["perch"]=new(){FishId="perch",Quantity=4};p.Rpg.Sushi["fighter"]=2;p.Rpg.Buffs.Add(new(){RecipeId="fighter"});p.FishCollection["Hecht"]=99;return new(true);
            },default);
        }
        return store;
    }
    [Theory][InlineData(RpgResetScope.Pearls)][InlineData(RpgResetScope.Items)][InlineData(RpgResetScope.PearlsAndItems)]
    public async Task Selected_reset_changes_only_requested_values_and_replays_safely(RpgResetScope scope){
        var store=await Seed();var other=JsonSerializer.Serialize((await store.GetProfileAsync("1002",default)).Entry);
        var preview=await store.PreviewResetAsync("1001",default);var op=Guid.NewGuid();
        Assert.True((await store.ResetAsync("1001",scope,op,preview.Fingerprint,default)).Success);
        var after=(await new ViewerPointStore(DirectoryPath).GetProfileAsync("1001",default)).Entry;
        Assert.Equal(scope==RpgResetScope.Items?10000:0,after.Points);
        if(scope!=RpgResetScope.Pearls){Assert.Empty(after.Rpg.Inventory);Assert.Empty(after.Rpg.Equipment);Assert.Empty(after.Rpg.Fish);Assert.Empty(after.Rpg.Sushi);Assert.Empty(after.Rpg.Buffs);Assert.True(after.Rpg.LegacyFishImported);}else Assert.Single(after.Rpg.Inventory);
        Assert.Equal(7,after.Rpg.PlayerLevel);Assert.Equal(222,after.Rpg.Experience);Assert.Equal(3,after.Rpg.Perks["crit"]);Assert.Single(after.Rpg.Cooldowns);Assert.Equal(99,after.FishCollection["Hecht"]);
        Assert.Equal(other,JsonSerializer.Serialize((await store.GetProfileAsync("1002",default)).Entry));
        await store.AddPointsAsync("1001","SYNTHETIC-1001",25,0,default);
        Assert.True((await new ViewerPointStore(DirectoryPath).ResetAsync("1001",scope,op,preview.Fingerprint,default)).Duplicate);
        Assert.Equal(after.Points+25,await store.GetPointsAsync("1001",default));Assert.Single(System.IO.Directory.GetFiles(DirectoryPath,"*.admin-reset-*.bak"));
    }
    [Fact]public async Task All_reset_is_one_commit_and_preserves_save_format(){
        var store=await Seed();var preview=await store.PreviewResetAsync(null,default);Assert.Equal(2,preview.Players);
        var result=await store.ResetAsync(null,RpgResetScope.PearlsAndItems,Guid.NewGuid(),preview.Fingerprint,default);Assert.Equal(2,result.Players);
        using var saved=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DirectoryPath,"viewer-points.json")));Assert.Equal(2,saved.RootElement.EnumerateObject().Count());
        foreach(var id in new[]{"1001","1002"}){var p=(await store.GetProfileAsync(id,default)).Entry;Assert.Equal(0,p.Points);Assert.Empty(p.Rpg.Inventory);}
    }
    [Fact]public async Task Stale_preview_and_running_plugin_prevent_reset(){
        var store=await Seed();var preview=await store.PreviewResetAsync(null,default);await store.AddPointsAsync("1001","SYNTHETIC-1001",1,0,default);
        Assert.False((await store.ResetAsync(null,RpgResetScope.Pearls,Guid.NewGuid(),preview.Fingerprint,default)).Success);
        store.Admission=new();store.Admission.Begin(()=>true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>store.ResetAsync(null,RpgResetScope.Pearls,Guid.NewGuid(),preview.Fingerprint,default));
        store.Admission.End();var fresh=await store.PreviewResetAsync(null,default);Assert.True((await store.ResetAsync(null,RpgResetScope.Pearls,Guid.NewGuid(),fresh.Fingerprint,default)).Success);
    }
    [Fact]public async Task Failed_reset_restores_committed_state_and_does_not_retry_uncertain_operation(){
        var store=await Seed();var preview=await store.PreviewResetAsync(null,default);var op=Guid.NewGuid();var file=Path.Combine(DirectoryPath,"viewer-points.json");var before=await File.ReadAllBytesAsync(file);
        System.IO.Directory.CreateDirectory(file+".tmp");await Assert.ThrowsAnyAsync<Exception>(()=>store.ResetAsync(null,RpgResetScope.Pearls,op,preview.Fingerprint,default));
        Assert.Equal(before,await File.ReadAllBytesAsync(file));System.IO.Directory.Delete(file+".tmp");Assert.False((await store.ResetAsync(null,RpgResetScope.Pearls,op,preview.Fingerprint,default)).Success);Assert.Equal(10000,await store.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Privacy_erases_local_profile_history_backups_and_receipts_but_blocks_recreation_after_restart(){
        var store=await Seed();var file=Path.Combine(DirectoryPath,"viewer-points.json");File.Copy(file,file+".admin-reset-synthetic.bak");
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath,"history.json"),"[{\"UserId\":\"1001\",\"DisplayName\":\"PRIVATE\"},{\"UserId\":\"1002\",\"DisplayName\":\"KEEP\"}]");
        var receipts=Path.Combine(root,"action-receipts");System.IO.Directory.CreateDirectory(receipts);await File.WriteAllTextAsync(Path.Combine(receipts,"one.json"),"{\"UserId\":\"1001\",\"Outcome\":{\"DataJson\":\"PRIVATE\"}}");
        // Reload so the test uses the same persisted history path as the normal plugin.
        store=new(DirectoryPath);var op=Guid.NewGuid();var result=await store.RequestPrivacyDeletionAsync("1001",op,default);Assert.True(result.LocalErased);Assert.False(result.CloudErased);
        Assert.False(File.Exists(Path.Combine(receipts,"one.json")));Assert.DoesNotContain("1001",await File.ReadAllTextAsync(file));Assert.DoesNotContain("1001",await File.ReadAllTextAsync(file+".admin-reset-synthetic.bak"));
        Assert.DoesNotContain("PRIVATE",await File.ReadAllTextAsync(Path.Combine(DirectoryPath,"history.json")));Assert.Contains("KEEP",await File.ReadAllTextAsync(Path.Combine(DirectoryPath,"history.json")));
        var restarted=new ViewerPointStore(DirectoryPath);await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.AddPointsAsync("1001","PRIVATE",100,0,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.ApplyRpgAsync("1001","PRIVATE","retry",_=>new(true),default));
        Assert.Equal(op,(await restarted.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default)).OperationId);Assert.Equal(10000,await restarted.GetPointsAsync("1002",default));
    }
    [Fact]public async Task Privacy_snapshot_pipeline_cancels_old_payloads_retries_lost_ack_and_never_resurrects(){
        var store=await Seed();var bytes=await store.ReadCommittedFileAsync(default);using var document=JsonDocument.Parse(bytes!);var json=document.RootElement.GetProperty("1001").GetRawText();
        var baseline=new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,json);var sink=new LocalSnapshotStore(Path.Combine(root,"cloud"),"42");Assert.Equal(SnapshotDecision.Applied,await sink.ApplyAsync(baseline));
        var outbox=new LocalOutboxStore(Path.Combine(root,"outbox"),"42");await outbox.InitializeBaselineAsync(baseline,SnapshotValidation.Validate(baseline,"42").ContentHash);
        await outbox.CommitAsync("1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,json);
        var cloudFile=Path.Combine(root,"cloud","42-1001.json");Task<StoredSnapshot?> Read(string _,CancellationToken ct)=>ReadFile(ct);async Task<StoredSnapshot?> ReadFile(CancellationToken ct)=>JsonSerializer.Deserialize<StoredSnapshot>(await File.ReadAllTextAsync(cloudFile,ct));
        var lost=true;var purges=0;var clock=DateTimeOffset.UtcNow;
        var worker=new IntegratedSnapshotWorker(store,outbox,"42",["1001"],_=>Task.CompletedTask,Read,async(message,ct)=>{
            Assert.Equal(SnapshotKind.Delete,message.Kind);Assert.Null(message.AccountJson);var decision=await sink.ApplyAsync(message,ct);
            if(lost){lost=false;throw new IOException("synthetic lost acknowledgement");}return new DeliveryResult(DeliveryStatus.Delivered,decision);
        },(_,_)=>{purges++;return Task.CompletedTask;},now:()=>clock);
        await store.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default);
        await worker.SynchronizeOnceAsync();Assert.Equal(1,worker.PendingPrivacyCount);Assert.False((await store.PrivacyRequestsAsync())[0].CloudErased);
        clock=clock.AddSeconds(6);await worker.SynchronizeOnceAsync();Assert.True((await store.PrivacyRequestsAsync())[0].CloudErased);Assert.True((await ReadFile(default))!.Deleted);Assert.Null(await outbox.ReadAsync("1001"));
        await worker.SynchronizeOnceAsync();Assert.Equal(2,purges);Assert.Equal(SnapshotDecision.RestoreRequired,await sink.ApplyAsync(baseline with{Revision=10,SnapshotId=Guid.NewGuid()}));
    }
    [Fact]public async Task Interrupted_privacy_cleanup_stays_blocked_and_can_resume_after_restart(){
        var store=await Seed();var backup=Path.Combine(DirectoryPath,"viewer-points.json.admin-reset-damaged.bak");await File.WriteAllTextAsync(backup,"invalid synthetic JSON");
        await Assert.ThrowsAnyAsync<Exception>(()=>store.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default));
        var restarted=new ViewerPointStore(DirectoryPath);Assert.True(restarted.IsPrivacyBlocked("1001"));Assert.False((await restarted.PrivacyRequestsAsync())[0].LocalErased);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.SetPointsAsync("1001","PRIVATE",10,0,default));
        await File.WriteAllTextAsync(backup,"{}");Assert.True((await restarted.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default)).LocalErased);
        Assert.Equal(10000,await restarted.GetPointsAsync("1002",default));
    }
    [Fact]public async Task A_late_gameplay_commit_cannot_be_authorized_by_administrator_maintenance(){
        var admission=new StreamAdmission();var epoch=admission.Begin(()=>true);admission.End();
        Task? late=null;
        using(admission.Enter(epoch))late=Task.Run(async()=>{await Task.Delay(50);Assert.Throws<GameplayUnavailableException>(()=>admission.Commit(()=>throw new Exception("must not run")));});
        using(admission.EnterMaintenance()){
            Assert.Throws<InvalidOperationException>(()=>admission.Begin(()=>true));await late;admission.Commit(()=>{});
        }
    }
    public void Dispose(){if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);}
}
