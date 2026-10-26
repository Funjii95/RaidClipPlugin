using System.Text.Json;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using SnapshotDelivery;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CloudIntegrationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-integration",Guid.NewGuid().ToString("N"));
    private string Players=>Path.Combine(root,"players");
    private string FilePath=>Path.Combine(Players,"viewer-points.json");
    private LocalOutboxStore Outbox()=>new(Path.Combine(root,"outbox"),"42");
    private readonly FakeCloud cloud=new();
    private async Task<ViewerPointStore> SeedAsync()
    {
        var store=new ViewerPointStore(Players);
        await store.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>
        {
            a.Points=100;
            a.Rpg.Health.SushiRegenRemainder=0.25m;
            a.Rpg.Inventory.Add(new(){InstanceId="item-1",ItemId="synthetic-item",IsFavorite=true,IsLocked=true,SetId="set-1",
                SalvageValue=17,CoreStatsVersion=2,FoundAt=DateTimeOffset.UtcNow,
                Collector=new(){FishingPearls=1.5m},Effects=new(){new(){Id="extra",Name="Extra",Chance=5,Strength=0.5m}}});
            a.Rpg.Buffs.Add(new(){RecipeId="fighter",EncounterBased=true,LastBossId="boss-1",ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(3),
                ActivatedAt=DateTimeOffset.UtcNow,PassiveHealing=1.5m,BossLifeleech=2.25m});
            a.Rpg.Cooldowns["dive"]=DateTimeOffset.UtcNow.AddSeconds(20);
            a.Rpg.Fish["sardine"]=new(){FishId="sardine",Quantity=4};
            a.Rpg.Sushi["fighter"]=2;a.Rpg.Perks["power"]=1;
            return new(true);
        },default);
        using var document=JsonDocument.Parse(await File.ReadAllBytesAsync(FilePath));
        var initial=new SnapshotMessage(1,"42","1001",7,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,
            document.RootElement.GetProperty("1001").GetRawText());
        cloud.Apply(initial);
        return store;
    }
    private IntegratedSnapshotWorker Worker(ViewerPointStore store,Func<CancellationToken,Task>? capabilities=null)=>
        new(store,Outbox(),"42",["1001"],capabilities??(_=>Task.CompletedTask),cloud.Read,cloud.Send);
    private static Task<RpgTransactionResult> Change(ViewerPointStore store,string action="change")=>
        store.ApplyRpgAsync("1001","SYNTHETIC",action,a=>{a.Points+=10;a.Rpg.Perks["power"]=2;return new(true);},default);

    [Fact]
    public async Task Large_private_journal_is_projected_before_transport_limit_without_losing_progress()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        await store.ApplyRpgAsync("1001","SYNTHETIC","journal",a=>{for(var n=0;n<12000;n++)a.Rpg.CompletedActions.Add("automatic:"+Guid.NewGuid().ToString("N")+":"+n);a.Points+=123;a.Rpg.Experience+=10000;return new(true);},default);
        var account=(await store.GetProfileAsync("1001",default)).Entry;var raw=new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account));
        Assert.Throws<InvalidDataException>(()=>SnapshotValidation.Validate(raw,"42"));
        var clean=SnapshotValidation.ValidateLocalSource(raw,"42");Assert.DoesNotContain("CompletedActions",clean.AccountJson);Assert.Equal(clean.ContentHash,SnapshotValidation.Validate(raw with{AccountJson=clean.AccountJson},"42").ContentHash);
        var saved=await File.ReadAllBytesAsync(FilePath);await Worker(store).SynchronizeOnceAsync();Assert.Equal(saved,await File.ReadAllBytesAsync(FilePath));Assert.Equal(223,JsonDocument.Parse(cloud.Head!.AccountJson!).RootElement.GetProperty("Points").GetInt64());
        Assert.True((await store.GetProfileAsync("1001",default)).Entry.Rpg.CompletedActions.Count>=12000);Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Real_mutation_generates_complete_snapshot_without_changing_the_legacy_file()
    {
        var store=await SeedAsync();var worker=Worker(store);await worker.SynchronizeOnceAsync();
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
        await Change(store);
        var bytes=await File.ReadAllBytesAsync(FilePath);
        await worker.SynchronizeOnceAsync();
        Assert.Equal(bytes,await File.ReadAllBytesAsync(FilePath));
        using var profile=JsonDocument.Parse(cloud.Head!.AccountJson!);
        var rpg=profile.RootElement.GetProperty("Rpg");
        Assert.Equal(110,profile.RootElement.GetProperty("Points").GetInt64());
        Assert.Equal(0.25m,rpg.GetProperty("Health").GetProperty("SushiRegenRemainder").GetDecimal());
        foreach(var property in typeof(RpgProfile).GetProperties().Where(p=>p.Name!="CompletedActions"&&p.Name!="DiveSeries"))Assert.True(rpg.TryGetProperty(property.Name,out _),property.Name);
        foreach(var property in typeof(RpgItem).GetProperties())Assert.True(rpg.GetProperty("Inventory")[0].TryGetProperty(property.Name,out _),property.Name);
        foreach(var property in typeof(RpgBuff).GetProperties())Assert.True(rpg.GetProperty("Buffs")[0].TryGetProperty(property.Name,out _),property.Name);
        Assert.DoesNotContain("CompletedActions",cloud.Head.AccountJson);
        Assert.Equal(8,cloud.Head.Revision);
        Assert.Equal(2,cloud.Applied); // Baseline fixture and one new snapshot, no gameplay replay.
    }

    [Fact]
    public async Task LargeIndentedSaveUsesCompactTransportWithoutDroppingItemsOrChangingTheSave()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        await store.ApplyRpgAsync("1001","SYNTHETIC","large-indented",a=>{
            while(JsonSerializer.SerializeToUtf8Bytes(new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(a,new JsonSerializerOptions{WriteIndented=true}))).Length<=SnapshotValidation.MaxMessageBytes)
                a.Rpg.Inventory.Add(new(){ItemId="synthetic-gear",DisplayName="Synthetische Rüstung",Slot=EquipmentSlot.Chest,Attributes=new(){Armor=42,Power=12,Stamina=16}});
            return new(true);
        },default);
        var saved=await File.ReadAllBytesAsync(FilePath);
        using var before=JsonDocument.Parse(saved);var count=before.RootElement.GetProperty("1001").GetProperty("Rpg").GetProperty("Inventory").GetArrayLength();
        await Worker(store).SynchronizeOnceAsync();
        Assert.Equal(saved,await File.ReadAllBytesAsync(FilePath));
        using var sent=JsonDocument.Parse(cloud.Head!.AccountJson!);
        Assert.Equal(count,sent.RootElement.GetProperty("Rpg").GetProperty("Inventory").GetArrayLength());
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
    }
    [Fact]
    public async Task Lost_ack_and_restart_resend_identical_message_without_double_booking()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        await Change(store);cloud.LoseAcknowledgement=true;
        await Assert.ThrowsAnyAsync<Exception>(()=>Worker(store).SynchronizeOnceAsync());
        var queued=Assert.Single((await Outbox().ReadAsync("1001"))!.Pending);
        var restarted=new ViewerPointStore(Players);
        await Worker(restarted).SynchronizeOnceAsync();
        Assert.Equal(queued.SnapshotId,cloud.LastSent);
        Assert.Equal(2,cloud.Applied);
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
        var duplicate=await Change(restarted);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(110,await restarted.GetPointsAsync("1001",default));
    }

    [Fact]
    public async Task Offline_save_before_worker_capture_is_recovered_from_authoritative_file()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        cloud.Offline=true;await Change(store);
        await Assert.ThrowsAsync<SnapshotHeadReadException>(()=>Worker(store).SynchronizeOnceAsync());
        Assert.Equal(110,await new ViewerPointStore(Players).GetPointsAsync("1001",default));
        cloud.Offline=false;await Worker(new ViewerPointStore(Players)).SynchronizeOnceAsync();
        Assert.Equal(8,cloud.Head!.Revision);
        Assert.Equal(2,cloud.Applied);
    }

    [Fact]
    public async Task Snapshot_is_durable_when_network_fails_during_post()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        await Change(store);cloud.FailPost=true;
        await Assert.ThrowsAnyAsync<Exception>(()=>Worker(store).SynchronizeOnceAsync());
        var first=Assert.Single((await Outbox().ReadAsync("1001"))!.Pending);
        cloud.FailPost=false;await Worker(new ViewerPointStore(Players)).SynchronizeOnceAsync();
        Assert.Equal(first.SnapshotId,cloud.LastSent);
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Newer_cloud_state_is_never_rebased_or_overwritten()
    {
        var store=await SeedAsync();await Worker(store).SynchronizeOnceAsync();
        await Change(store);cloud.Head=cloud.Head! with {Revision=100};
        await Assert.ThrowsAsync<InvalidDataException>(()=>Worker(store).SynchronizeOnceAsync());
        Assert.Equal(100,cloud.Head.Revision);Assert.Equal(1,cloud.Applied);
    }

    [Fact]
    public async Task Incompatible_server_receives_nothing_and_local_save_remains_available()
    {
        var store=await SeedAsync();await Change(store);
        var worker=Worker(store,_=>Task.FromException(new SnapshotHeadReadException(false)));
        await Assert.ThrowsAsync<SnapshotHeadReadException>(()=>worker.SynchronizeOnceAsync());
        Assert.Equal(110,await store.GetPointsAsync("1001",default));Assert.Equal(1,cloud.Applied);
    }

    [Fact]
    public async Task Store_save_wakes_background_worker_without_waiting_for_network()
    {
        var store=await SeedAsync();var worker=Worker(store);await worker.SynchronizeOnceAsync();
        using var stop=new CancellationTokenSource();
        var success=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.StatusChanged+=_=>{if(cloud.Applied==2)success.TrySetResult();};
        var running=Task.Run(()=>worker.RunAsync(stop.Token));
        await Change(store);
        await success.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();await running;
        Assert.Equal(110,await store.GetPointsAsync("1001",default));
    }

    [Fact]
    public async Task Marker_recovers_both_crash_windows_without_changing_format()
    {
        Directory.CreateDirectory(Players);await File.WriteAllTextAsync(FilePath,"{}");
        var marker=new SaveCommitMarker(FilePath);await marker.RecoverAsync(default);
        var temp=FilePath+".prepared";await File.WriteAllTextAsync(temp,"{\"1001\":{\"Points\":5}}");
        await marker.PrepareAsync(temp,default);
        await new SaveCommitMarker(FilePath).RecoverAsync(default); // Crash before rename.
        Assert.Equal("{}",await File.ReadAllTextAsync(FilePath));
        marker=new SaveCommitMarker(FilePath);await marker.RecoverAsync(default);await marker.PrepareAsync(temp,default);
        File.Move(temp,FilePath,true);
        await new SaveCommitMarker(FilePath).RecoverAsync(default); // Crash after rename, before marker completion.
        Assert.Equal("{\"1001\":{\"Points\":5}}",await File.ReadAllTextAsync(FilePath));
        await File.WriteAllTextAsync(FilePath,"{}");
        await Assert.ThrowsAsync<InvalidDataException>(()=>new SaveCommitMarker(FilePath).RecoverAsync(default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}

    [Fact]
    public async Task Independent_accounts_sync_two_at_a_time_without_changing_local_saves()
    {
        var store=new ViewerPointStore(Players);
        var ids=Enumerable.Range(1001,8).Select(i=>i.ToString()).ToArray();
        foreach(var id in ids)await store.ApplyRpgAsync(id,"SYNTHETIC","seed",a=>{a.Points=100;return new(true);},default);
        var before=await File.ReadAllBytesAsync(FilePath);
        using var accounts=JsonDocument.Parse(before);
        var sink=new LocalSnapshotStore(Path.Combine(root,"parallel-cloud"),"42");
        foreach(var id in ids)await sink.ApplyAsync(new(1,"42",id,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,accounts.RootElement.GetProperty(id).GetRawText()),default);
        int active=0,peak=0,reads=0;
        async Task<StoredSnapshot?> Read(string id,CancellationToken ct){
            var concurrency=Interlocked.Increment(ref active);Interlocked.Increment(ref reads);
            int previous;do{previous=Volatile.Read(ref peak);if(previous>=concurrency)break;}while(Interlocked.CompareExchange(ref peak,concurrency,previous)!=previous);
            try{Assert.InRange(concurrency,1,2);await Task.Delay(100,ct);return JsonSerializer.Deserialize<StoredSnapshot>(await File.ReadAllTextAsync(Path.Combine(root,"parallel-cloud","42-"+id+".json"),ct));}
            finally{Interlocked.Decrement(ref active);}
        }
        var worker=new IntegratedSnapshotWorker(store,Outbox(),"42",ids,_=>Task.CompletedTask,Read,
            (_,_)=>throw new InvalidOperationException("Unchanged profiles must not be sent."));
        var watch=System.Diagnostics.Stopwatch.StartNew();await worker.SynchronizeOnceAsync();watch.Stop();
        Assert.Equal(2,peak);Assert.Equal(8,reads);
        Assert.Equal(before,await File.ReadAllBytesAsync(FilePath));
        foreach(var id in ids){var state=await Outbox().ReadAsync(id);Assert.NotNull(state);Assert.Empty(state.Pending);}
        Console.WriteLine($"Eight synthetic 100ms cloud heads: parallel pass {watch.ElapsedMilliseconds}ms; serialized network waits alone: 800ms.");
    }

    private IntegratedSnapshotWorker AutomaticWorker(ViewerPointStore store,Func<string,string,bool>? eligible=null)=>
        new(store,Outbox(),"42",[],_=>Task.CompletedTask,cloud.Read,cloud.Send,
            automaticPlayerEnrollment:true,eligible:eligible);

    [Fact]
    public async Task Automatic_enrollment_creates_profile_without_touching_local_save_and_keeps_zero_balance_after_restart()
    {
        var store=await SeedAsync();cloud.Head=null;cloud.Applied=0;
        var before=await File.ReadAllBytesAsync(FilePath);
        await AutomaticWorker(store).SynchronizeOnceAsync();
        Assert.Equal(before,await File.ReadAllBytesAsync(FilePath));
        Assert.Equal(1,cloud.Head!.Revision);Assert.Equal(1,cloud.Applied);
        await store.ApplyRpgAsync("1001","SYNTHETIC","zero",a=>{a.Points=0;return new(true);},default);
        await AutomaticWorker(new ViewerPointStore(Players)).SynchronizeOnceAsync();
        Assert.Equal(2,cloud.Head!.Revision);
        using var account=JsonDocument.Parse(cloud.Head.AccountJson!);
        Assert.Equal(0,account.RootElement.GetProperty("Points").GetInt64());
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Initial_upload_with_lost_acknowledgement_is_recovered_once_after_restart()
    {
        var store=await SeedAsync();cloud.Head=null;cloud.Applied=0;cloud.LoseAcknowledgement=true;
        await Assert.ThrowsAnyAsync<Exception>(()=>AutomaticWorker(store).SynchronizeOnceAsync());
        Assert.Single((await Outbox().ReadAsync("1001"))!.Pending);
        await AutomaticWorker(new ViewerPointStore(Players)).SynchronizeOnceAsync();
        Assert.Equal(1,cloud.Applied);Assert.Equal(1,cloud.Head!.Revision);
        Assert.Empty((await Outbox().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Unenrolled_zero_balance_and_excluded_bot_are_not_uploaded()
    {
        var store=await SeedAsync();cloud.Head=null;cloud.Applied=0;
        await AutomaticWorker(store,(_,_)=>false).SynchronizeOnceAsync();
        Assert.Null(cloud.Head);Assert.Null(await Outbox().ReadAsync("1001"));
        await store.ApplyRpgAsync("1001","SYNTHETIC","zero",a=>{a.Points=0;return new(true);},default);
        await AutomaticWorker(store).SynchronizeOnceAsync();
        Assert.Null(cloud.Head);Assert.Equal(0,cloud.Applied);
    }

    [Fact]
    public async Task Automatic_enrollment_never_restores_a_cloud_tombstone()
    {
        var store=await SeedAsync();
        cloud.Head=cloud.Head! with{Deleted=true,AccountJson=null};
        var head=cloud.Head;
        await AutomaticWorker(store).SynchronizeOnceAsync();
        Assert.Equal(head,cloud.Head);Assert.Null(await Outbox().ReadAsync("1001"));
    }

    private sealed class FakeCloud
    {
        internal StoredSnapshot? Head;
        internal bool Offline,FailPost,LoseAcknowledgement;
        internal int Applied;
        internal Guid LastSent;
        internal SnapshotDecision Apply(SnapshotMessage message)
        {
            var valid=SnapshotValidation.Validate(message,"42");
            var decision=SnapshotRules.Decide(Head,valid);
            if(decision==SnapshotDecision.Applied){Applied++;Head=new("42",message.UserId,message.Revision,message.SnapshotId,message.CapturedAt,false,valid.AccountJson,valid.ContentHash);}
            return decision;
        }
        internal Task<StoredSnapshot?> Read(string user,CancellationToken ct)=>Offline
            ? Task.FromException<StoredSnapshot?>(new SnapshotHeadReadException(true)) : Task.FromResult(Head);
        internal Task<DeliveryResult> Send(SnapshotMessage message,CancellationToken ct)
        {
            LastSent=message.SnapshotId;
            if(FailPost)return Task.FromResult(new DeliveryResult(DeliveryStatus.RetryLater));
            var decision=Apply(message);
            if(LoseAcknowledgement){LoseAcknowledgement=false;return Task.FromResult(new DeliveryResult(DeliveryStatus.RetryLater));}
            return Task.FromResult(new DeliveryResult(DeliveryStatus.Delivered,decision));
        }
    }
}
