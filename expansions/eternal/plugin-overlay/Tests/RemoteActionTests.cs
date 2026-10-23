using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Backend;
using Backend.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotDelivery;
using SnapshotProtocol;
using Xunit;
using Xunit.Abstractions;
namespace RaidClipPlugin.Tests;

[Collection("Cloud pump integration")]
public sealed class RemoteActionTests(ITestOutputHelper output) : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-actions-",Guid.NewGuid().ToString("N"));
    private readonly StreamAdmission gate=new();private Guid epoch;
    private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    private ViewerPointStore? points;
    private async Task<RemoteRpgExecutor> SeedAsync(long pearls=2000,int level=1)
    {
        points=new(Path.Combine(root,"players"));config.Adventures.Rpg.Enabled=true;
        await points.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{
            a.Points=pearls;a.Rpg.PlayerLevel=level;a.Rpg.Inventory.Add(new(){InstanceId="ring-owned",ItemId="test-ring",Slot=EquipmentSlot.Ring1});
            a.Rpg.Fish["sardine"]=new(){FishId="sardine",Quantity=2};return new(true);
        },default);
        epoch=gate.Begin(()=>true);points.Admission=gate;
        return Executor();
    }
    private RemoteRpgExecutor Executor()=>new(points!,()=>config,gate,epoch,"42",new HashSet<string>{"1001"},Path.Combine(root,"receipts"),(_,_)=>true);
    private ActionEnvelope Envelope(RpgIntent intent)=>new("42","1001",Guid.NewGuid(),"synthetic-stream",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
    [Fact]public void Tides_requires_publisher_support_for_extended_stats_and_shields()
    {
        Assert.Equal(9,SnapshotValidation.RpgProjectionVersion);
    }
    [Fact]public async Task Large_inventory_status_keeps_authoritative_stats_and_profile()
    {
        var executor=await SeedAsync();
        await points!.ApplyRpgAsync("1001","SYNTHETIC","large-inventory",account=>{
            for(var i=0;i<80;i++)account.Rpg.Inventory.Add(new(){InstanceId="owned-large-"+i,ItemId="test-large",DisplayName="Synthetischer Gegenstand "+i,Description=new string('x',100),Kind=RpgItemKind.Equipment,Slot=EquipmentSlot.Weapon,Attributes=new(){Damage=12}});
            return new(true);
        },default);
        var result=await executor.ExecuteAsync(Envelope(ActionApiFixture.Intent("status","{}")),default);
        Assert.True(result.Success);Assert.NotNull(result.DataJson);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(result).Length;
        Assert.True(bytes>48000);Assert.True(bytes<=ActionResultLimits.MaximumOutcomeBytes);
        using var json=JsonDocument.Parse(result.DataJson!);
        Assert.Equal(81,json.RootElement.GetProperty("profile").GetProperty("Rpg").GetProperty("Inventory").GetArrayLength());
        Assert.True(json.RootElement.TryGetProperty("stats",out _));
    }
    [Fact]public async Task Talent_intents_are_authoritative_idempotent_and_return_committed_ranks()
    {
        var executor=await SeedAsync(2000,30);
        var catalogJson=executor.CatalogJson();Assert.InRange(catalogJson.Length,1,48000);
        using var catalog=JsonDocument.Parse(catalogJson);
        Assert.Equal(21,catalog.RootElement.GetProperty("talents").GetArrayLength());
        var intent=new RpgIntent(Guid.NewGuid(),"learnTalent",JsonSerializer.SerializeToElement(new{talentId="combat-power",expectedRank="0",catalogVersion="1"}));
        var envelope=Envelope(intent);var first=await executor.ExecuteAsync(envelope,default);
        Assert.True(first.Success);Assert.Equal(first,await Executor().ExecuteAsync(envelope,default));
        using var receipt=JsonDocument.Parse(first.DataJson!);
        Assert.Equal(1,receipt.RootElement.GetProperty("profile").GetProperty("Rpg").GetProperty("TalentRanks").GetProperty("combat-power").GetInt32());
        var stale=await executor.ExecuteAsync(Envelope(intent with{ActionId=Guid.NewGuid()}),default);Assert.False(stale.Success);
        var saved=(await new ViewerPointStore(Path.Combine(root,"players")).GetProfileAsync("1001",default)).Entry;
        Assert.Equal(1,saved.Rpg.TalentRanks["combat-power"]);Assert.Equal(28,TalentRules.Available(saved.Rpg.PlayerLevel,saved.Rpg.TalentRanks));Assert.Equal(2000,saved.Points);
    }
    [Fact]public async Task Offline_Twitch_does_not_block_actual_perk_and_duplicates_after_restart()
    {
        var executor=await SeedAsync();var e=Envelope(ActionApiFixture.Intent()) with {StreamId=""};
        var first=await executor.ExecuteAsync(e,default);var second=await Executor().ExecuteAsync(e,default);
        Assert.True(first.Success);Assert.Equal(first,second);Assert.Equal(1500,await points!.GetPointsAsync("1001",default));
        using var response=JsonDocument.Parse(first.DataJson!);
        var committed=response.RootElement.GetProperty("profile");
        Assert.Equal("1001",committed.GetProperty("UserId").GetString());
        Assert.Equal(1500,committed.GetProperty("Points").GetInt64());
        Assert.Equal(1,committed.GetProperty("Rpg").GetProperty("Perks").GetProperty("damage").GetInt32());
        Assert.True(response.RootElement.TryGetProperty("stats",out _));
        Assert.False(committed.GetProperty("Rpg").TryGetProperty("CompletedActions",out _));
        Assert.Equal(1,(await points.GetProfileAsync("1001",default)).Entry.Rpg.Perks["damage"]);
    }
    [Theory][InlineData("buyPerk","{\"perkId\":\"damage\"}",0)][InlineData("equip","{\"instanceId\":\"foreign-item\"}",2000)]
    public async Task Authoritative_rules_reject_insufficient_pearls_and_foreign_item(string kind,string args,long pearls)
    {
        var executor=await SeedAsync(pearls);var before=await File.ReadAllBytesAsync(Path.Combine(root,"players","viewer-points.json"));
        var result=await executor.ExecuteAsync(Envelope(ActionApiFixture.Intent(kind,args)),default);
        Assert.False(result.Success);Assert.Equal("rule_rejected",result.Code);Assert.Equal(before,await File.ReadAllBytesAsync(Path.Combine(root,"players","viewer-points.json")));
    }
    [Fact]public async Task Dive_uses_original_rules_and_persisted_cooldown()
    {
        var executor=await SeedAsync();var first=await executor.ExecuteAsync(Envelope(ActionApiFixture.Intent("dive","{\"stake\":\"100\"}")),default);
        Assert.True(first.Success);
        var balance=await points!.GetPointsAsync("1001",default);
        var next=await Executor().ExecuteAsync(Envelope(ActionApiFixture.Intent("dive","{\"stake\":\"100\"}")),default);
        Assert.False(next.Success);Assert.Contains("Cooldown",next.Message);Assert.Equal(balance,await points.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Equipment_crafting_eating_and_views_use_existing_services()
    {
        var executor=await SeedAsync();
        foreach(var (kind,args) in new[]{("equip","{\"instanceId\":\"ring-owned\"}"),("unequip","{\"slot\":\"Ring1\"}"),("craft","{\"recipeId\":\"fishing\"}"),("eat","{\"recipeId\":\"fishing\"}"),("inventory","{}"),("status","{}"),("sets","{}"),("perks","{}")})
            Assert.True((await executor.ExecuteAsync(Envelope(ActionApiFixture.Intent(kind,args)),default)).Success,kind);
        var profile=(await points!.GetProfileAsync("1001",default)).Entry;
        Assert.Empty(profile.Rpg.Equipment);Assert.Equal(0,profile.Rpg.Sushi["fishing"]);Assert.Single(profile.Rpg.Buffs);
        Assert.Equal(1750,profile.Points);
    }
    [Fact]public async Task Disconnected_plugin_expired_request_and_foreign_user_cannot_mutate()
    {
        var executor=await SeedAsync();var e=Envelope(ActionApiFixture.Intent());
        Assert.False((await executor.ExecuteAsync(e with{UserId="1002"},default)).Success);
        Assert.False((await executor.ExecuteAsync(e with{ExecuteBefore=DateTimeOffset.UtcNow.AddSeconds(-1)},default)).Success);
        gate.End();Assert.False((await executor.ExecuteAsync(e,default)).Success);
        Assert.Equal(2000,await points!.GetPointsAsync("1001",default));
    }
    [Theory][InlineData("rpg")][InlineData("points")][InlineData("module")]
    public async Task Disabled_plugin_configuration_cannot_mutate(string disabled)
    {
        var executor=await SeedAsync();if(disabled=="rpg")config.Adventures.Rpg.Enabled=false;
        if(disabled=="points")config.PointsEnabled=false;if(disabled=="module")config.Enabled=false;
        Assert.False((await executor.ExecuteAsync(Envelope(ActionApiFixture.Intent()),default)).Success);
        Assert.Equal(2000,await points!.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Crash_after_intent_fsync_is_unknown_and_never_replayed()
    {
        var executor=await SeedAsync();var e=Envelope(ActionApiFixture.Intent());Directory.CreateDirectory(Path.Combine(root,"receipts"));
        await File.WriteAllTextAsync(Path.Combine(root,"receipts",e.Intent.ActionId.ToString("N")+".json"),JsonSerializer.Serialize(new LocalActionRecord(e.IntentHash,e.UserId,null)));
        Assert.Null((await executor.ExecuteAsync(e,default)).Success);Assert.Equal(2000,await points!.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Full_HTTP_queue_plugin_sync_firestore_and_viewer_snapshot_roundtrip()
    {
        var executor=await SeedAsync();await using var database=await TransactionalFirestoreFixture.StartAsync();
        var bytes=await points!.ReadCommittedFileAsync(default);using var source=JsonDocument.Parse(bytes!);
        var baseline=new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,source.RootElement.GetProperty("1001").GetRawText());
        Assert.Equal(SnapshotDecision.Applied,await database.Sink.ApplyAsync(baseline));
        await using var api=new ActionApiFixture(new FirestoreRelayStore(database.Database,"42","rpgActionChannelsTest"),database.Reader);
        await using var publisherBase=new PublisherFactory(Path.Combine(root,"publisher"),ActionApiFixture.Key);
        await using var publisher=publisherBase.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Publisher:AllowedUserIds:0"]="1001"}))
            .ConfigureServices(s=>{s.RemoveAll<ISnapshotSink>();s.AddSingleton<ISnapshotSink>(database.Sink);}));
        using var sender=new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"),ActionApiFixture.Key,publisher.Server.CreateHandler(),true);
        var worker=new IntegratedSnapshotWorker(points,new LocalOutboxStore(Path.Combine(root,"outbox"),"42"),"42",["1001"],_=>Task.CompletedTask,
            (id,ct)=>database.Sink.ReadCurrentAsync(id,ct),sender.SendAsync);
        await worker.SynchronizeOnceAsync();
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport=api.Factory.CreateClient();transport.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Key);
        using var pump=new CloudActionClient(transport,executor,()=>gate.CanPlay(epoch),_=>{});
        var syncing=worker.RunAsync(stop.Token);var pumping=pump.RunAsync(stop.Token);
        try {
            RelayAvailability? available;
            do {await Task.Delay(20,stop.Token);available=await api.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state",stop.Token);}while(!available!.Available);
            api.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",available.SessionId!.Value.ToString());
            var intent=ActionApiFixture.Intent();
            output.WriteLine("01 JWT verified against synthetic HMAC secret; user=1001, channel=42, initial pearls=2000, snapshot revision=1.");
            (await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent,stop.Token)).EnsureSuccessStatusCode();
            output.WriteLine("02 HTTP action queued in Firestore SDK transaction: "+intent.ActionId+"; kind=buyPerk, perkId=damage.");
            ActionReceipt? receipt;
            do {await Task.Delay(30,stop.Token);receipt=await api.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+intent.ActionId,stop.Token);}while(receipt!.State is "queued" or "executing");
            Assert.Equal("applied",receipt.State);var outcome=receipt.Outcome!;Assert.True(outcome.Success);
            output.WriteLine("03 Outgoing plugin pump claimed, called existing RpgProgressionService/ViewerPointStore and acknowledged applied.");
            PlayerView? visible;
            do {await Task.Delay(30,stop.Token);visible=await api.Viewer.GetFromJsonAsync<PlayerView>("/api/v1/me",stop.Token);}while(visible!.Points!=1500);
            Assert.Equal(1,visible.Rpg.Perks["damage"]);Assert.Equal(2,(await database.Sink.ReadCurrentAsync("1001"))!.Revision);
            Assert.DoesNotContain("CompletedActions",await (await api.Viewer.GetAsync("/api/v1/me",stop.Token)).Content.ReadAsStringAsync(stop.Token));
            output.WriteLine("04 Automatic SnapshotDelivery -> HTTP SnapshotPublisher -> Firestore SDK -> JWT GET /me: pearls=1500, damage perk=1, revision=2. No private action ledger exposed.");
            var envelope=new ActionEnvelope("42","1001",available.SessionId.Value,"synthetic-stream",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
        Assert.Equal(outcome,await Executor().ExecuteAsync(envelope,default));
        Assert.Equal(1500,await points.GetPointsAsync("1001",default));
            output.WriteLine("05 Same ActionId after executor restart returned identical receipt; pearls remain 1500. No double booking.");
        } finally {stop.Cancel();await Task.WhenAll(syncing,pumping);}
    }
    [Fact]public async Task Outgoing_plugin_pump_executes_and_acknowledges_without_inbound_ports()
    {
        var executor=await SeedAsync();await using var api=new ActionApiFixture();
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport=api.Factory.CreateClient();transport.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Key);
        using var pump=new CloudActionClient(transport,executor,()=>gate.CanPlay(epoch),_=>{});var running=pump.RunAsync(cancel.Token);
        // Respect the production read limit (60/minute), including on slower CI workers.
        RelayAvailability? availability;
        do {await Task.Delay(1100,cancel.Token);availability=await api.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state",cancel.Token);}while(!availability!.Available);
        api.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",availability.SessionId!.Value.ToString());
        var intent=ActionApiFixture.Intent();(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent,cancel.Token)).EnsureSuccessStatusCode();
        ActionReceipt? receipt=null;
        do {await Task.Delay(50,cancel.Token);receipt=await api.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+intent.ActionId,cancel.Token);}while(receipt!.State is "queued" or "executing");
        Assert.Equal("applied",receipt.State);cancel.Cancel();await running;
        Assert.Equal(1500,await points!.GetPointsAsync("1001",default));
        Assert.False((await api.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state"))!.Available);
    }

    [Fact]public async Task Action_expiring_while_waiting_for_store_cannot_commit_later()
    {
        var executor=await SeedAsync();var e=Envelope(ActionApiFixture.Intent()) with{ExecuteBefore=DateTimeOffset.UtcNow.AddMilliseconds(150)};
        var mutex=(SemaphoreSlim)typeof(ViewerPointStore).GetField("_lock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(points)!;
        await mutex.WaitAsync();var operation=executor.ExecuteAsync(e,default);await Task.Delay(250);mutex.Release();
        Assert.False((await operation).Success);Assert.Equal(2000,await points!.GetPointsAsync("1001",default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
