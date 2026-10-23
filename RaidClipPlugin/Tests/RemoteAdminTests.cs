using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RemoteAdminTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-admin-"+Guid.NewGuid().ToString("N"));
    private readonly StreamAdmission admission=new();private readonly Guid epoch;
    private readonly ViewerPointStore points;private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    public RemoteAdminTests(){points=new(root);config.Adventures.Rpg.Enabled=true;epoch=admission.Begin(()=>true);}
    private RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"42","1001"},Path.Combine(root,"receipts"),(_,_)=>true);
    private ActionEnvelope Envelope(string kind,object args,string user="42",Guid? id=null)
    {var intent=new RpgIntent(id??Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(args));return new("42",user,Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    [Theory][InlineData("boss")][InlineData("mimic")][InlineData("race")][InlineData("hotTime")]
    public async Task OwnerEventStartsUseTheActualConnectedGameServiceAndCannotReplay(string kind)
    {
        await points.SetPointsAsync("42","SYNTHETIC OWNER",10000,0,default);
        config.Adventures.Boss.Enabled=true;config.Adventures.Race.Enabled=true;config.Adventures.Rpg.Mimic.Enabled=true;
        using var stop=new CancellationTokenSource();
        var service=new ChatMinigameService("42","42",config,new TwitchService("synthetic","synthetic"),points,responseSender:(_,_)=>Task.CompletedTask);
        service.BindStreamSession(admission,epoch);var running=service.RunAsync(stop.Token);
        try
        {
            var executor=new RemoteRpgExecutor(points,()=>config,admission,epoch,"42",new HashSet<string>{"42"},Path.Combine(root,"receipts"),(_,_)=>true,service);
            var e=Envelope("adminStart",new{@event=kind,stake=kind=="race"?"100":"0"});
            var first=await executor.ExecuteAsync(e,default);Assert.True(first.Success,first.Message);
            Assert.Equal(first,await executor.ExecuteAsync(e,default));
            if(kind is "boss" or "mimic")Assert.True(service.RemoteAdventures.Status.BossActive);
            if(kind=="mimic")Assert.True(service.RemoteAdventures.Status.BossIsMimic);
            if(kind=="race")Assert.True(service.RemoteAdventures.Status.RaceActive);
            if(kind=="hotTime")Assert.True(points.HotTime.Status.Active);
        }
        finally{stop.Cancel();try{await running;}catch(OperationCanceledException){}}
    }
    [Theory][InlineData("random")][InlineData("minimum")][InlineData("maximum")][InlineData("custom")]
    public async Task GrantsPersistExactlyOnceWithSeparateInstancesAndNoPearlMutation(string roll)
    {
        await points.SetPointsAsync("1001","SYNTHETIC",1234,0,default);
        var e=Envelope("adminGrant",new{itemId="tide_king_helmet",targetUserId="1001",count="2",roll,stats=roll=="custom"?"{\"MaximumHp\":321,\"CritChance\":42}":"{}"});
        var first=await Executor().ExecuteAsync(e,default);Assert.True(first.Success,first.Message);
        Assert.Equal(first,await Executor().ExecuteAsync(e,default));
        var restored=(await new ViewerPointStore(root).GetProfileAsync("1001",default)).Entry;
        Assert.Equal(1234,restored.Points);Assert.Equal(2,restored.Rpg.Inventory.Count);Assert.Equal(2,restored.Rpg.Inventory.Select(i=>i.InstanceId).Distinct().Count());
        Assert.All(restored.Rpg.Inventory,i=>{Assert.Null(i.Generation);Assert.Equal("tide_king_regalia",i.SetId);if(roll=="custom"){Assert.Equal(321,i.Attributes.MaximumHp);Assert.Equal(42,i.Attributes.CritChance);Assert.Equal(0,i.Attributes.Armor);}});
    }
    [Fact] public async Task ViewerCannotReadAdminCatalogOrGrantEvenWithOwnerDisplayName()
    {
        await points.SetPointsAsync("1001","Funjii",1234,0,default);
        Assert.Equal("admin_required",(await Executor().ExecuteAsync(Envelope("adminCatalog",new{},"1001"),default)).Code);
        var e=Envelope("adminGrant",new{itemId="tide_king_helmet",targetUserId="1001",count="1",roll="maximum",stats="{}"},"1001");
        Assert.False((await Executor().ExecuteAsync(e,default)).Success);Assert.Empty((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
    }
    [Theory][InlineData("{\"NotAStat\":3}")][InlineData("{\"CritChance\":101}")][InlineData("{\"Damage\":3,\"Damage\":4}")]
    public async Task InvalidCustomStatsDoNotCreateAnything(string stats)
    {
        await points.SetPointsAsync("1001","SYNTHETIC",1234,0,default);
        var e=Envelope("adminGrant",new{itemId="tide_king_helmet",targetUserId="1001",count="1",roll="custom",stats});
        Assert.False((await Executor().ExecuteAsync(e,default)).Success);Assert.Empty((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
    }
    [Fact] public async Task NonexistentTargetDoesNotCreateAPlayer()
    {
        var e=Envelope("adminGrant",new{itemId="tide_king_helmet",targetUserId="9999",count="1",roll="maximum",stats="{}"});
        Assert.False((await Executor().ExecuteAsync(e,default)).Success);Assert.Null(await points.ReadCommittedFileAsync(default));
    }
    [Fact] public async Task OversizedGrantRollsBackInsteadOfBreakingCloudSync()
    {
        await points.SetPointsAsync("1001","SYNTHETIC",1234,0,default);
        await points.ApplyRpgAsync("1001","SYNTHETIC","seed-large",a=>{a.Rpg.Inventory.Add(new(){ItemId="synthetic-large",Description=new string('x',250000)});return new(true);},default);
        var e=Envelope("adminGrant",new{itemId="tide_king_helmet",targetUserId="1001",count="20",roll="maximum",stats="{}"});
        var result=await Executor().ExecuteAsync(e,default);Assert.False(result.Success);Assert.Contains("Größenbegrenzung",result.Message);
        Assert.Single((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
    }
    [Fact] public async Task OwnerCatalogContainsAll66NewItemsAndOnlyEquipment()
    {
        var outcome=await Executor().ExecuteAsync(Envelope("adminCatalog",new{}),default);Assert.True(outcome.Success);
        using var data=JsonDocument.Parse(outcome.DataJson!);var items=data.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.All(RpgTidesExpansion.Items(),i=>Assert.Contains(items,x=>x.GetProperty("id").GetString()==i.ItemId));
        Assert.All(RpgDepthsExpansion.Items(),i=>Assert.Contains(items,x=>x.GetProperty("id").GetString()==i.ItemId));
        Assert.DoesNotContain(items,x=>x.GetProperty("id").GetString()=="shell");
    }
    [Fact] public void AdministrativeHotTimePersistsWithoutPretendingABossVictory()
    {
        var hot=new RpgHotTimeService(root);var cfg=new RpgHotTimeConfig{AutoAfterBoss=false};
        Assert.True(hot.StartAdministrative("admin:synthetic",cfg));Assert.False(hot.StartAdministrative("admin:synthetic",cfg));
        var restored=new RpgHotTimeService(root);Assert.True(restored.Status.Active);Assert.Equal("admin:synthetic",restored.Status.Event!.BossId);
    }
}
