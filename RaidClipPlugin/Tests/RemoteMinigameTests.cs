using System.Text.Json;
using System.Net.Http.Json;
using Backend;
using Backend.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotDelivery;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;

namespace RaidClipPlugin.Tests;
public sealed class RemoteMinigameTests : IAsyncLifetime
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-remote-casino",Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource stop=new();
    private readonly StreamAdmission admission=new();
    private Guid epoch;
    private ViewerPointStore points=null!;private ChatMinigameService service=null!;private Task running=null!;
    private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true,ChatPointsEnabled=false,GambleEnabled=true,CoinflipEnabled=true,SlotsEnabled=true,RouletteEnabled=true,JackpotEnabled=false};
    private readonly List<string> chat=[];
    public async Task InitializeAsync(){
        config.Adventures.Rpg.Enabled=true;config.Adventures.Boss.Enabled=false;config.Adventures.Race.Enabled=false;
        points=new(Path.Combine(root,"players"));await points.SetPointsAsync("1001","SYNTHETIC",10000,0,default);
        epoch=admission.Begin(()=>true);points.Admission=admission;
        var registry=new CommandRegistry();registry.Update(new AppConfig{Minigame=config});
        service=new("42","42",config,new TwitchService("synthetic","synthetic"),points,commandRegistry:registry,
            responseSender:(message,_)=>{chat.Add(message);return Task.CompletedTask;});
        service.BindStreamSession(admission,epoch);running=service.RunAsync(stop.Token);
    }
    private RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"1001"},Path.Combine(root,"receipts"),(_,_)=>true,service);
    private ActionEnvelope Envelope(string kind,object args,Guid? id=null){var intent=new RpgIntent(id??Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(args));return new("42","1001",Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));}
    [Theory][InlineData("gamble","-")][InlineData("coinflip","kopf")][InlineData("slots","-")][InlineData("roulette","rot")]
    public async Task Existing_handler_commits_once_without_chat_reward_or_external_message(string game,string choice){
        var e=Envelope("casino",new{game,stake="100",choice});var executor=Executor();
        var reply=await executor.ExecuteAsync(e,default);Assert.True(reply.Success,reply.Message);
        var balance=await points.GetPointsAsync("1001",default);
        var repeated=await Executor().ExecuteAsync(e,default);Assert.Equal(reply,repeated);Assert.Equal(balance,await points.GetPointsAsync("1001",default));
        Assert.Empty(chat);var account=(await points.GetProfileAsync("1001",default)).Entry;
        Assert.Contains("casino:extension:"+e.Intent.ActionId.ToString("N"),account.Rpg.CompletedActions);
        Assert.True(account.Rpg.Cooldowns.ContainsKey("!"+game));
        using var json=JsonDocument.Parse(reply.DataJson!);Assert.Equal(balance,json.RootElement.GetProperty("frame").GetProperty("balance").GetInt64());
    }
    [Fact]public async Task Chat_and_extension_share_cooldown_and_persist_it(){
        await service.ProcessMessageAsync(new(){Id="synthetic-chat",UserId="1001",UserName="SYNTHETIC",Text="!coinflip kopf 100"},default);
        var before=await points.GetPointsAsync("1001",default);
        var reply=await Executor().ExecuteAsync(Envelope("casino",new{game="coinflip",stake="100",choice="zahl"}),default);
        Assert.False(reply.Success);Assert.Equal(before,await points.GetPointsAsync("1001",default));
        using var restarted=new ViewerPointStoreForTest(Path.Combine(root,"players"));
        Assert.True((await restarted.Store.GetProfileAsync("1001",default)).Entry.Rpg.Cooldowns["!coinflip"]>DateTimeOffset.UtcNow);
    }
    [Fact]public async Task Insufficient_funds_and_disabled_game_do_not_book(){
        var before=await points.GetPointsAsync("1001",default);
        var a=await Executor().ExecuteAsync(Envelope("casino",new{game="slots",stake="999999999",choice="-"}),default);Assert.False(a.Success);
        config.RouletteEnabled=false;
        var b=await Executor().ExecuteAsync(Envelope("casino",new{game="roulette",stake="100",choice="rot"}),default);Assert.False(b.Success);
        Assert.Equal(before,await points.GetPointsAsync("1001",default));Assert.Empty(chat);
    }
    [Fact]public async Task Preview_and_perk_catalog_use_original_rules_without_saving(){
        var file=Path.Combine(root,"players","viewer-points.json");var before=await File.ReadAllBytesAsync(file);
        var reply=await Executor().ExecuteAsync(Envelope("preview",new{stake="1000",fishingStake="0"}),default);Assert.True(reply.Success);
        using var json=JsonDocument.Parse(reply.DataJson!);
        var account=(await points.GetProfileAsync("1001",default)).Entry;var sources=CharacterStatService.DiveLuckSources(account.Rpg,config.Adventures.Rpg);
        Assert.Equal(RpgLootService.Chances(1000,sources.Perks,config.Adventures.Rpg.DiveLoot,sources.Sushi,sources.Equipment).Total,json.RootElement.GetProperty("dive").GetProperty("chance").GetDecimal());
        using var catalog=JsonDocument.Parse(Executor().CatalogJson());var perk=catalog.RootElement.GetProperty("perks")[0];
        Assert.True(perk.TryGetProperty("BonusPerLevel",out _));Assert.Equal(config.Adventures.Rpg.Perks[0].Cost(0).ToString(),perk.GetProperty("Costs")[0].GetString());
        Assert.Equal(before,await File.ReadAllBytesAsync(file));
    }
    [Fact]public async Task Race_start_and_join_share_chat_round_and_reject_old_event(){
        config.Adventures.Race.Enabled=true;var executor=Executor();
        var start=Envelope("community",new{game="race",operation="start",eventId="-",stake="100",target="-"});
        Assert.True((await executor.ExecuteAsync(start,default)).Success);Assert.Equal(1,service.AdventureStatus.RacePlayers);
        Assert.True((await Executor().ExecuteAsync(start,default)).Success);Assert.Equal(1,service.AdventureStatus.RacePlayers);Assert.Equal(10000,await points.GetPointsAsync("1001",default));
        var stale=Envelope("community",new{game="race",operation="join",eventId="old-round",stake="100",target="-"});
        Assert.False((await executor.ExecuteAsync(stale,default)).Success);
    }
    [Theory][InlineData("casino")][InlineData("bossAttack")]
    public async Task Jwt_HTTP_outgoing_plugin_original_engine_automatic_sync_and_Firestore_reader_roundtrip(string kind){
        if(kind=="bossAttack"){
            config.Adventures.Boss.Enabled=true;config.Adventures.Boss.AutoStartEnabled=false;config.Adventures.Boss.HitPoints=10000;config.Adventures.Rpg.Health.Enabled=true;
            await service.RemoteAdventures.ControlAsync("boss",0,default);
        }
        await using var database=await TransactionalFirestoreFixture.StartAsync();
        using var baseline=JsonDocument.Parse((await points.ReadCommittedFileAsync(default))!);
        await database.Sink.ApplyAsync(new SnapshotMessage(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,baseline.RootElement.GetProperty("1001").GetRawText()));
        await using var api=new ActionApiFixture(new FirestoreRelayStore(database.Database,"42","rpgActionChannelsTest"),database.Reader);
        await using var publisherBase=new PublisherFactory(Path.Combine(root,"publisher"),ActionApiFixture.Key);
        await using var publisher=publisherBase.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Publisher:AllowedUserIds:0"]="1001"}))
            .ConfigureServices(s=>{s.RemoveAll<ISnapshotSink>();s.AddSingleton<ISnapshotSink>(database.Sink);}));
        using var sender=new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"),ActionApiFixture.Key,publisher.Server.CreateHandler(),true);
        var worker=new IntegratedSnapshotWorker(points,new LocalOutboxStore(Path.Combine(root,"outbox"),"42"),"42",["1001"],_=>Task.CompletedTask,(id,ct)=>database.Sink.ReadCurrentAsync(id,ct),sender.SendAsync);
        await worker.SynchronizeOnceAsync();
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport=api.Factory.CreateClient();transport.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Key);
        using var pump=new CloudActionClient(transport,Executor(),()=>admission.CanPlay(epoch),_=>{});
        var pumping=pump.RunAsync(limit.Token);var syncing=worker.RunAsync(limit.Token);
        try{
            RelayAvailability? state;
            do{await Task.Delay(20,limit.Token);state=await api.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state",limit.Token);}while(!state!.Available);
            api.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",state.SessionId!.Value.ToString());
            var e=kind=="casino"?Envelope("casino",new{game="coinflip",stake="100",choice="kopf"}):Envelope("bossAttack",new{});
            (await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",e.Intent,limit.Token)).EnsureSuccessStatusCode();
            ActionReceipt? receipt;
            do{await Task.Delay(30,limit.Token);receipt=await api.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+e.Intent.ActionId,limit.Token);}while(receipt!.State is "queued" or "executing");
            Assert.Equal("applied",receipt.State);Assert.True(receipt.Outcome!.Success);
            while((await database.Sink.ReadCurrentAsync("1001"))!.Revision<2)await Task.Delay(30,limit.Token);
            var actual=(await points.GetProfileAsync("1001",default)).Entry;
            var visible=await api.Viewer.GetFromJsonAsync<PlayerView>("/api/v1/me",limit.Token);Assert.Equal(actual.Points,visible!.Points);
            var json=await (await api.Viewer.GetAsync("/api/v1/me",limit.Token)).Content.ReadAsStringAsync(limit.Token);
            Assert.DoesNotContain("CompletedActions",json);Assert.Contains(kind=="casino"?"!coinflip":"!attack",json);
            (await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",e.Intent,limit.Token)).EnsureSuccessStatusCode();
            Assert.Equal(actual.Points,await points.GetPointsAsync("1001",default));
        }finally{limit.Cancel();await Task.WhenAll(pumping,syncing);}
    }
    [Theory]
    [InlineData("gamble","-")][InlineData("coinflip","kopf")][InlineData("slots","-")][InlineData("roulette","rot")]
    public async Task Two_players_parallel_casino_receipts_preserve_individual_balances_on_retry(string game,string choice)
    {
        await points.SetPointsAsync("1002","SYNTHETIC-B",10000,0,default);
        var executor=new RemoteRpgExecutor(points,()=>config,admission,epoch,"42",new HashSet<string>{"1001","1002"},Path.Combine(root,"receipts"),(_,_)=>true,service);
        var a=Envelope("casino",new{game,stake="100",choice});
        var b=Envelope("casino",new{game,stake="100",choice}) with {UserId="1002"};
        var outcomes=await Task.WhenAll(executor.ExecuteAsync(a,default),executor.ExecuteAsync(b,default));
        Assert.True(outcomes[0].Success,outcomes[0].Message);
        Assert.False(outcomes[1].Success);Assert.Contains("Cooldown",outcomes[1].Message);
        var balances=new[]{await points.GetPointsAsync("1001",default),await points.GetPointsAsync("1002",default)};
        for(var i=0;i<2;i++){
            using var json=JsonDocument.Parse(outcomes[i].DataJson!);
            Assert.Equal(balances[i],json.RootElement.GetProperty("frame").GetProperty("balance").GetInt64());
        }
        var retries=await Task.WhenAll(executor.ExecuteAsync(a,default),executor.ExecuteAsync(b,default));
        Assert.Equal(outcomes[0],retries[0]);Assert.Equal(outcomes[1],retries[1]);
        Assert.Equal(balances[0],await points.GetPointsAsync("1001",default));
        Assert.Equal(balances[1],await points.GetPointsAsync("1002",default));
        Assert.False((await executor.ExecuteAsync(Envelope("casino",new{game,stake="100",choice}),default)).Success);
        Assert.Equal(balances[0],await points.GetPointsAsync("1001",default));
        Assert.Equal(balances[1],await points.GetPointsAsync("1002",default));
    }
    public async Task DisposeAsync(){stop.Cancel();await running;service.Dispose();stop.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class ViewerPointStoreForTest(string path):IDisposable{public ViewerPointStore Store {get;}=new(path);public void Dispose(){}}
}
