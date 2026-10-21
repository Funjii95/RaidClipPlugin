using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;

public sealed class RemoteShellTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-remote-shell-"+Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task Shell_action_is_chat_only_and_never_advertised_or_executed_remotely()
    {
        var points=new ViewerPointStore(root);
        var config=new MinigameConfig{Enabled=true,PointsEnabled=true};config.Adventures.Rpg.Enabled=true;
        await points.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Points=100;a.Rpg.Materials.Shells=10;return new(true);},default);
        var admission=new StreamAdmission();var epoch=admission.Begin(()=>true);points.Admission=admission;
        RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"1001"},Path.Combine(root,"receipts"),(_,_)=>true);
        var intent=new RpgIntent(Guid.NewGuid(),"openShells",JsonSerializer.SerializeToElement(new{count="10"}));
        var envelope=new ActionEnvelope("42","1001",Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
        var result=await Executor().ExecuteAsync(envelope,default);
        Assert.False(result.Success);
        Assert.Equal("chat_only",result.Code);
        Assert.Equal(result,await Executor().ExecuteAsync(envelope,default));
        Assert.Equal(10,(await points.GetProfileAsync("1001",default)).Entry.Rpg.Materials.Shells);
        using var catalog=JsonDocument.Parse(Executor().CatalogJson());
        Assert.DoesNotContain(catalog.RootElement.GetProperty("supported").EnumerateArray(),item=>item.GetString()=="openShells");
        var other=intent with{ActionId=Guid.NewGuid()};
        var attempt=envelope with{Intent=other,IntentHash=RpgIntents.Hash(other),UserId="1002"};
        Assert.False((await Executor().ExecuteAsync(attempt,default)).Success);
        admission.End();
        Assert.False((await Executor().ExecuteAsync(attempt with{UserId="1001"},default)).Success);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
