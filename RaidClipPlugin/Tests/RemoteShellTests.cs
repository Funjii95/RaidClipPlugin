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
    public async Task Shell_action_uses_owners_profile_and_durable_receipt_and_blocks_disconnected_plugin()
    {
        var points=new ViewerPointStore(root);
        var config=new MinigameConfig{Enabled=true,PointsEnabled=true};config.Adventures.Rpg.Enabled=true;
        await points.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Points=100;a.Rpg.Materials.Shells=10;return new(true);},default);
        var admission=new StreamAdmission();var epoch=admission.Begin(()=>true);points.Admission=admission;
        RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"1001"},Path.Combine(root,"receipts"),(_,_)=>true);
        var intent=new RpgIntent(Guid.NewGuid(),"openShells",JsonSerializer.SerializeToElement(new{count="10"}));
        var envelope=new ActionEnvelope("42","1001",Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
        var result=await Executor().ExecuteAsync(envelope,default);
        Assert.True(result.Success);
        Assert.Equal(result,await Executor().ExecuteAsync(envelope,default));
        var account=(await points.GetProfileAsync("1001",default)).Entry;
        Assert.Equal(0,account.Rpg.Materials.Shells);
        var stock=account.Rpg.Materials;
        var pearls=(account.Points-100);
        Assert.InRange(pearls,0,500);
        using var body=JsonDocument.Parse(result.DataJson!);
        Assert.Equal(0,body.RootElement.GetProperty("profile").GetProperty("Rpg").GetProperty("Materials").GetProperty("Shells").GetInt32());
        var other=intent with{ActionId=Guid.NewGuid()};
        var attempt=envelope with{Intent=other,IntentHash=RpgIntents.Hash(other),UserId="1002"};
        Assert.False((await Executor().ExecuteAsync(attempt,default)).Success);
        admission.End();
        Assert.False((await Executor().ExecuteAsync(attempt with{UserId="1001"},default)).Success);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
