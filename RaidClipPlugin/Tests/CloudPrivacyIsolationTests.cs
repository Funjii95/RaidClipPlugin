using System.Net;
using System.Text.Json;
using RaidClipPlugin.Services;
using SnapshotDelivery;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;

public sealed class CloudPrivacyIsolationTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-privacy-isolation-"+Guid.NewGuid().ToString("N"));
    private DateTimeOffset clock=DateTimeOffset.UtcNow;
    private ViewerPointStore Source()=>new(Path.Combine(root,"players"));
    private LocalOutboxStore Outbox()=>new(Path.Combine(root,"outbox"),"42");
    private LocalSnapshotStore Cloud()=>new(Path.Combine(root,"cloud"),"42");
    private async Task<StoredSnapshot?> Read(string id,CancellationToken ct){var path=Path.Combine(root,"cloud","42-"+id+".json");return File.Exists(path)?JsonSerializer.Deserialize<StoredSnapshot>(await File.ReadAllTextAsync(path,ct)):null;}
    private async Task<DeliveryResult> Send(SnapshotMessage message,CancellationToken ct)=>new(DeliveryStatus.Delivered,await Cloud().ApplyAsync(message,ct));
    private IntegratedSnapshotWorker Worker(ViewerPointStore source,Func<string,CancellationToken,Task>? purge)=>new(source,Outbox(),"42",["1001"],_=>Task.CompletedTask,Read,Send,purge,true,now:()=>clock);
    private async Task<ViewerPointStore> Seed()
    {
        var source=Source();
        foreach(var id in new[]{"1001","1002"})await source.ApplyRpgAsync(id,"SYNTHETIC","seed",a=>{a.Points=100;return new(true);},default);
        await new IntegratedSnapshotWorker(source,Outbox(),"42",["1001","1002"],_=>Task.CompletedTask,Read,Send,automaticPlayerEnrollment:true).SynchronizeOnceAsync();
        await source.RequestPrivacyDeletionAsync("1001",Guid.NewGuid(),default);
        await source.ApplyRpgAsync("1002","SYNTHETIC","earn",a=>{a.Points+=10;return new(true);},default);
        return source;
    }
    [Theory][InlineData(401)][InlineData(403)][InlineData(503)]
    public async Task Failed_deletion_does_not_block_other_profiles_and_retries_with_backoff(int status)
    {
        var source=await Seed();var attempts=0;var denied=true;
        var worker=Worker(source,(_,_)=>{attempts++;return denied?Task.FromException(new HttpRequestException("secret must never be logged",null,(HttpStatusCode)status)):Task.CompletedTask;});
        await worker.SynchronizeOnceAsync();Assert.Equal(1,worker.PendingPrivacyCount);
        Assert.Contains("HTTP "+status,worker.PrivacyStatus);Assert.DoesNotContain("secret",worker.PrivacyStatus);
        Assert.Contains("110",(await Read("1002",default))!.AccountJson!);
        Assert.False((await source.ReadPrivacyRequestAsync("1001"))!.CloudErased);
        await Assert.ThrowsAnyAsync<Exception>(()=>source.ApplyRpgAsync("1001","SYNTHETIC","forbidden",a=>{a.Points++;return new(true);},default));
        await worker.SynchronizeOnceAsync();Assert.Equal(1,attempts);
        clock=clock.AddSeconds(6);denied=false;await worker.SynchronizeOnceAsync();
        Assert.Equal(2,attempts);Assert.Equal(0,worker.PendingPrivacyCount);
        Assert.True((await source.ReadPrivacyRequestAsync("1001"))!.CloudErased);
        Assert.True((await Read("1001",default))!.Deleted);
        Assert.Equal(110,await source.GetPointsAsync("1002",default));
    }
    [Fact]
    public async Task Ready_heartbeat_remains_enabled_with_pending_deletion_and_restart_recovers_request()
    {
        var source=await Seed();var worker=Worker(source,(_,_)=>Task.FromException(new HttpRequestException("disabled",null,HttpStatusCode.ServiceUnavailable)));
        using var stop=new CancellationTokenSource();var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wasReady=false;worker.AvailabilityChanged+=value=>wasReady|=value;
        worker.StatusChanged+=status=>{if(status.Contains("Löschauftrag"))ready.TrySetResult();};
        var running=worker.RunAsync(stop.Token);await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(wasReady);Assert.Contains("Löschauftrag",worker.Status);stop.Cancel();await running;
        var restarted=Source();await Worker(restarted,(_,_)=>Task.CompletedTask).SynchronizeOnceAsync();
        Assert.True((await restarted.ReadPrivacyRequestAsync("1001"))!.CloudErased);
        Assert.True((await Read("1001",default))!.Deleted);
        Assert.Equal(110,await restarted.GetPointsAsync("1002",default));
    }
    [Fact]
    public async Task Missing_purge_channel_is_explicit_and_never_marks_deletion_complete()
    {
        var source=await Seed();var worker=Worker(source,null);await worker.SynchronizeOnceAsync();
        Assert.Contains("nicht eingerichtet",worker.PrivacyStatus);
        Assert.False((await source.ReadPrivacyRequestAsync("1001"))!.CloudErased);
        Assert.False((await Read("1001",default))!.Deleted);
        Assert.Equal(110,await source.GetPointsAsync("1002",default));
    }
    [Fact]
    public async Task Global_capability_failure_still_blocks_all_actions()
    {
        var source=await Seed();var worker=new IntegratedSnapshotWorker(source,Outbox(),"42",["1002"],_=>Task.FromException(new SnapshotHeadReadException(false)),Read,Send);
        var available=false;worker.AvailabilityChanged+=v=>available|=v;
        await worker.RunAsync(default);
        Assert.False(available);Assert.Contains("Vertrag oder Zugang",worker.Status);
    }
    [Fact]
    public async Task Transient_local_file_failure_retries_without_discarding_work()
    {
        var source=await Seed();var attempts=0;var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker=new IntegratedSnapshotWorker(source,Outbox(),"42",["1002"],_=>++attempts==1?Task.FromException(new IOException("synthetic file busy")):Task.CompletedTask,Read,Send);
        var messages=new List<string>();worker.StatusChanged+=status=>{messages.Add(status);if(status.StartsWith("Cloud synchronisiert"))ready.TrySetResult();};
        using var stop=new CancellationTokenSource();var running=worker.RunAsync(stop.Token);
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));Assert.True(attempts>=2);Assert.Contains(messages,m=>m.Contains("Dateizugriff"));}
        finally{stop.Cancel();await running;}
        Assert.Equal(110,await source.GetPointsAsync("1002",default));
        Assert.False((await source.ReadPrivacyRequestAsync("1001"))!.CloudErased);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
