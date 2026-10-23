using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class RemoteDepthsTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-remote-depths-"+Guid.NewGuid().ToString("N"));
    private readonly StreamAdmission admission=new();private readonly Guid epoch;
    private readonly ViewerPointStore points;private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    public RemoteDepthsTests(){points=new(root);config.Adventures.Rpg.Enabled=true;epoch=admission.Begin(()=>true);}
    private RemoteRpgExecutor Executor(Action<RpgUpgradeConfig>? save=null)=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"42","1001"},Path.Combine(root,"receipts"),(_,_)=>true,saveUpgrades:save);
    private ActionEnvelope Envelope(string kind,object args,string user="42")
    {var intent=new RpgIntent(Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(args));return new("42",user,Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    [Fact] public async Task MaterialGrantsAreOwnerOnlyDurableAndReplaySafe()
    {
        await points.SetPointsAsync("1001","Synthetic",500,0,default);var args=new{targetUserId="1001",materialId="reinforcement_core",count="12"};
        Assert.False((await Executor().ExecuteAsync(Envelope("adminMaterialGrant",args,"1001"),default)).Success);
        var e=Envelope("adminMaterialGrant",args);var first=await Executor().ExecuteAsync(e,default);Assert.True(first.Success);Assert.NotNull(first.DataJson);
        Assert.Equal(first,await Executor().ExecuteAsync(e,default));var restored=(await new ViewerPointStore(root).GetProfileAsync("1001",default)).Entry;
        Assert.Equal(12,restored.Rpg.Materials.ReinforcementCore);Assert.Equal(500,restored.Points);
    }
    [Fact] public async Task BalancingRequiresOwnerAndValidatesBeforeDurableSave()
    {
        int saves=0;RpgUpgradeConfig? saved=null;var executor=Executor(c=>{saves++;saved=c;});
        Assert.False((await executor.ExecuteAsync(Envelope("adminBalance",new{path="drop:dive:pearl_dust:Chance",value="50"},"1001"),default)).Success);Assert.Equal(0,saves);
        Assert.False((await executor.ExecuteAsync(Envelope("adminBalance",new{path="drop:dive:pearl_dust:Chance",value="101"}),default)).Success);Assert.Equal(0,saves);
        var e=Envelope("adminBalance",new{path="drop:dive:pearl_dust:Chance",value="50"});Assert.True((await executor.ExecuteAsync(e,default)).Success);Assert.Equal(50,saved!.Drops["dive"]["pearl_dust"].Chance);
        await executor.ExecuteAsync(e,default);Assert.Equal(1,saves);
        Assert.True((await executor.ExecuteAsync(Envelope("adminBalance",new{path="rarity:Abyss:9",value="7"}),default)).Success);
        Assert.Equal(7,RpgEquipmentUpgradeRules.Cost(8,RaidClipPlugin.Models.ItemRarity.Abyss,config.Adventures.Rpg.Upgrades).Materials["abyss_shard"]);
    }
    [Fact] public async Task UpgradeReturnsTheCommittedOwnersProjectionAndReplaysWithoutDebit()
    {
        var item=new RpgItem{ItemId="synthetic",Slot=EquipmentSlot.Helmet,Attributes=new(){MaximumHp=10},CoreStatsVersion=1};
        await points.ApplyRpgAsync("1001","Synthetic","seed",a=>{a.Points=500;a.Rpg.Inventory.Add(item);return new(true);},default);
        var e=Envelope("upgradeItem",new{instanceId=item.InstanceId,expectedLevel="0"},"1001");var first=await Executor().ExecuteAsync(e,default);Assert.True(first.Success);
        using var data=JsonDocument.Parse(first.DataJson!);Assert.Equal(1,data.RootElement.GetProperty("profile").GetProperty("Rpg").GetProperty("Inventory")[0].GetProperty("UpgradeLevel").GetInt32());
        Assert.Equal(first,await Executor().ExecuteAsync(e,default));Assert.Equal(400,(await points.GetProfileAsync("1001",default)).Entry.Points);
        Assert.False((await Executor().ExecuteAsync(Envelope("upgradeItem",new{instanceId=item.InstanceId,expectedLevel="1"}),default)).Success);
    }
    [Fact] public void FullExpandedHeartbeatStaysWithinTheActualWireBudget()
    {
        var heartbeat=new RelayHeartbeat(Guid.NewGuid(),1,DateTimeOffset.UtcNow,true,Executor().CatalogJson());var bytes=JsonSerializer.SerializeToUtf8Bytes(heartbeat).Length;
        Assert.True(bytes<=64*1024,$"Heartbeat uses {bytes} bytes.");
    }
    [Fact] public async Task HealAllIsOwnerOnlyPersistentAndCannotReplayOrEraseBossDefeat()
    {
        foreach(var user in new[]{"42","1001"})await points.ApplyRpgAsync(user,"Synthetic","seed",a=>{a.Points=1234;a.Rpg.PlayerLevel=user=="42"?1:8;a.Rpg.Health.CurrentHp=user=="42"?5:0;a.Rpg.Health.BossId="active-boss";a.Rpg.Health.Defeated=user=="1001";return new(true);},default);
        Assert.False((await Executor().ExecuteAsync(Envelope("adminHealAll",new{},"1001"),default)).Success);
        Assert.Equal(5,(await points.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentHp);
        var e=Envelope("adminHealAll",new{});var first=await Executor().ExecuteAsync(e,default);Assert.True(first.Success);
        foreach(var user in new[]{"42","1001"}){var a=(await new ViewerPointStore(root).GetProfileAsync(user,default)).Entry;Assert.Equal(RpgHealthService.Maximum(a.Rpg,config.Adventures.Rpg.Health),a.Rpg.Health.CurrentHp);Assert.Equal(1234,a.Points);Assert.Equal(user=="1001",a.Rpg.Health.Defeated);}
        await points.ApplyRpgAsync("1001","Synthetic","damage",a=>{a.Rpg.Health.CurrentHp=1;return new(true);},default);
        Assert.Equal(first,await Executor().ExecuteAsync(e,default));Assert.Equal(1,(await points.GetProfileAsync("1001",default)).Entry.Rpg.Health.CurrentHp);
    }
    [Fact] public async Task FailedHealAllWriteRollsBackEveryPlayerAndAllowsRetry()
    {
        foreach(var user in new[]{"42","1001"})await points.ApplyRpgAsync(user,"Synthetic","seed",a=>{a.Rpg.Health.CurrentHp=1;return new(true);},default);
        var blocked=Path.Combine(root,"viewer-points.json.tmp");Directory.CreateDirectory(blocked);
        await Assert.ThrowsAnyAsync<Exception>(()=>points.HealAllAsync("42","synthetic-heal",config.Adventures.Rpg.Health,DateTimeOffset.UtcNow,default));
        foreach(var user in new[]{"42","1001"})Assert.Equal(1,(await points.GetProfileAsync(user,default)).Entry.Rpg.Health.CurrentHp);
        Directory.Delete(blocked);Assert.True((await points.HealAllAsync("42","synthetic-heal",config.Adventures.Rpg.Health,DateTimeOffset.UtcNow,default)).Success);
        Assert.True((await points.HealAllAsync("42","synthetic-heal",config.Adventures.Rpg.Health,DateTimeOffset.UtcNow,default)).Duplicate);
    }
}
