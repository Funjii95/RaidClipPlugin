using System.Text.Json;
using System.Net.Http.Json;
using Backend.Tests;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RemoteExtendedTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-extended",Guid.NewGuid().ToString("N"));
    private readonly StreamAdmission admission=new();
    private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    private ViewerPointStore points=null!;private Guid epoch;private ChatMinigameService service=null!;
    private async Task SeedAsync(){
        points=new(Path.Combine(root,"players"));config.Adventures.Rpg.Enabled=true;config.Adventures.Rpg.Health.Enabled=true;
        config.Adventures.Boss.Enabled=true;config.Adventures.Boss.HitPoints=10000;config.Adventures.Boss.AutoStartEnabled=false;
        await points.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Points=1000;a.Rpg.Inventory.Add(new(){InstanceId="owned-one",ItemId="synthetic",DisplayName="Synthetischer Gegenstand",Rarity=ItemRarity.Common,SalvageValue=25});return new(true);},default);
        await points.SetPointsAsync("1002","TARGET",1000,0,default);
        epoch=admission.Begin(()=>true);points.Admission=admission;
        service=new("42","42",config,new TwitchService("synthetic","synthetic"),points,responseSender:(_,_)=>Task.CompletedTask);
        service.BindStreamSession(admission,epoch);
    }
    private RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"1001","1002"},Path.Combine(root,"receipts"),(_,_)=>true,service);
    private ActionEnvelope Envelope(string kind,object args,string user="1001"){
        var intent=new RpgIntent(Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(args));return new("42",user,Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
    }
    [Fact]public async Task Boss_uses_running_engine_and_shared_chat_cooldown_and_durable_receipt(){
        await SeedAsync();await service.RemoteAdventures.ControlAsync("boss",0,default);
        var e=Envelope("bossAttack",new{});var reply=await Executor().ExecuteAsync(e,default);Assert.True(reply.Success,reply.Message);
        var hp=service.AdventureStatus.BossHp;Assert.InRange(hp,1,9999);
        Assert.Equal(reply,await Executor().ExecuteAsync(e,default));Assert.Equal(hp,service.AdventureStatus.BossHp);
        var blocked=await service.RemoteAdventures.ExecuteBossAttackAsync("1001","SYNTHETIC","chat-after-extension",default);
        Assert.False(blocked.Success);Assert.Equal(hp,service.AdventureStatus.BossHp);
        var status=await Executor().ExecuteAsync(Envelope("bossStatus",new{}),default);Assert.True(status.Success);
        using var data=JsonDocument.Parse(status.DataJson!);Assert.Equal(hp,data.RootElement.GetProperty("status").GetProperty("BossHp").GetInt32());
        admission.End();Assert.False((await Executor().ExecuteAsync(Envelope("bossAttack",new{}),default)).Success);
    }
    [Fact]public async Task Stable_item_protection_preview_and_confirmation_commit_exactly_once(){
        await SeedAsync();var executor=Executor();
        Assert.True((await executor.ExecuteAsync(Envelope("favorite",new{instanceId="owned-one",enabled="true"}),default)).Success);
        Assert.False((await executor.ExecuteAsync(Envelope("salvagePreview",new{instanceId="owned-one"}),default)).Success);
        Assert.False((await executor.ExecuteAsync(Envelope("itemlock",new{instanceId="foreign",enabled="true"}),default)).Success);
        Assert.True((await executor.ExecuteAsync(Envelope("favorite",new{instanceId="owned-one",enabled="false"}),default)).Success);
        var preview=await executor.ExecuteAsync(Envelope("salvagePreview",new{instanceId="owned-one"}),default);Assert.True(preview.Success);
        Assert.Equal(1000,await points.GetPointsAsync("1001",default));
        using var json=JsonDocument.Parse(preview.DataJson!);var token=json.RootElement.GetProperty("quote").GetProperty("token").GetString()!;
        Assert.False((await executor.ExecuteAsync(Envelope("salvageConfirm",new{token},"1002"),default)).Success);
        var confirm=Envelope("salvageConfirm",new{token});Assert.True((await executor.ExecuteAsync(confirm,default)).Success);
        Assert.True((await Executor().ExecuteAsync(confirm,default)).Success);Assert.Equal(1025,await points.GetPointsAsync("1001",default));
        Assert.Empty((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
    }
    [Fact]public async Task Persisted_boss_action_cannot_damage_again_even_after_cooldown_and_without_outer_receipt(){
        await SeedAsync();config.Adventures.Boss.AttackCooldownSeconds=1;
        await service.RemoteAdventures.ControlAsync("boss",0,default);
        var first=await service.RemoteAdventures.ExecuteBossAttackAsync("1001","SYNTHETIC","same-synthetic-request",default);Assert.True(first.Success);
        var hp=service.AdventureStatus.BossHp;await Task.Delay(1100);
        var repeated=await service.RemoteAdventures.ExecuteBossAttackAsync("1001","SYNTHETIC","same-synthetic-request",default);
        Assert.False(repeated.Success);Assert.Equal(hp,service.AdventureStatus.BossHp);Assert.Contains("bereits",repeated.Error);
    }
    [Fact]public async Task Accepted_read_burst_drains_without_starving_following_actions(){
        await SeedAsync();await using var api=new ActionApiFixture();
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport=api.Factory.CreateClient();transport.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Key);
        using var pump=new CloudActionClient(transport,Executor(),()=>admission.CanPlay(epoch),_=>{});var pumping=pump.RunAsync(stop.Token);
        try{
            SnapshotProtocol.RelayAvailability? state;
            do{await Task.Delay(250,stop.Token);state=await api.Viewer.GetFromJsonAsync<SnapshotProtocol.RelayAvailability>("/api/v1/play/state",stop.Token);}while(!state!.Available);
            api.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",state.SessionId!.Value.ToString());
            var requests=Enumerable.Range(0,12).Select(_=>Envelope("inventory",new{}).Intent).ToArray();
            foreach(var request in requests)(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",request,stop.Token)).EnsureSuccessStatusCode();
            SnapshotProtocol.ActionReceipt? last;
            do{await Task.Delay(500,stop.Token);last=await api.Viewer.GetFromJsonAsync<SnapshotProtocol.ActionReceipt>("/api/v1/play/actions/"+requests[^1].ActionId,stop.Token);}while(last!.State is "queued" or "executing");
            Assert.Equal("applied",last.State);Assert.Equal(1000,await points.GetPointsAsync("1001",default));
        }finally{stop.Cancel();await pumping;}
    }
    [Fact]public async Task Changed_protection_invalidates_quote_and_batch_uses_original_exclusions(){
        await SeedAsync();var e=Executor();var preview=await e.ExecuteAsync(Envelope("salvageBatch",new{selection="common"}),default);Assert.True(preview.Success);
        using var json=JsonDocument.Parse(preview.DataJson!);var token=json.RootElement.GetProperty("quote").GetProperty("token").GetString()!;
        Assert.True((await e.ExecuteAsync(Envelope("itemlock",new{instanceId="owned-one",enabled="true"}),default)).Success);
        Assert.False((await e.ExecuteAsync(Envelope("salvageConfirm",new{token}),default)).Success);Assert.Equal(1000,await points.GetPointsAsync("1001",default));
        Assert.Single((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
    }
    [Fact]public async Task Unfinished_duel_reservation_is_refunded_after_crash_once_and_finished_payment_is_not_refunded(){
        await SeedAsync();var id=Guid.NewGuid().ToString("N");
        Assert.True((await points.ReserveDuelStakeAsync("1001","SYNTHETIC",100,0,100,default,id)).Success);
        Assert.True((await points.ReserveDuelStakeAsync("1001","SYNTHETIC",100,0,100,default,id)).Success);Assert.Equal(900,await points.GetPointsAsync("1001",default));
        var restarted=new ViewerPointStore(Path.Combine(root,"players"));await restarted.RecoverDuelReservationsAsync(default);await restarted.RecoverDuelReservationsAsync(default);
        Assert.Equal(1000,await restarted.GetPointsAsync("1001",default));
        var next=Guid.NewGuid().ToString("N");await restarted.ReserveDuelStakeAsync("1001","SYNTHETIC",100,0,100,default,next);
        Assert.True((await restarted.ResolveDuelAsync("1001","SYNTHETIC","1002","TARGET","1002",100,0,long.MaxValue,100,default,next)).Success);
        await restarted.RefundDuelStakeAsync("1001","SYNTHETIC",100,100,default,next);await restarted.RecoverDuelReservationsAsync(default);
        Assert.Equal(900,await restarted.GetPointsAsync("1001",default));Assert.Equal(1100,await restarted.GetPointsAsync("1002",default));
    }
    [Fact]
    public async Task Two_remote_players_share_boss_hp_with_exact_damage_and_fresh_next_round()
    {
        await SeedAsync();config.Adventures.Boss.RoundCooldownSeconds=0;
        await service.RemoteAdventures.ControlAsync("boss",0,default);
        var executor=Executor();
        var first=Envelope("bossAttack",new{},"1001");var second=Envelope("bossAttack",new{},"1002");
        var replies=await Task.WhenAll(executor.ExecuteAsync(first,default),executor.ExecuteAsync(second,default));
        Assert.All(replies,r=>Assert.True(r.Success,r.Message));
        var damage=0;
        for(var i=0;i<replies.Length;i++){
            using var json=JsonDocument.Parse(replies[i].DataJson!);
            var hit=json.RootElement.GetProperty("attack");
            Assert.Equal(i==0?"1001":"1002",hit.GetProperty("PlayerId").GetString());
            damage+=hit.GetProperty("Damage").GetInt32();
        }
        Assert.Equal(10000-damage,service.AdventureStatus.BossHp);
        var oldId=service.AdventureStatus.BossEventId;
        foreach(var user in new[]{"1001","1002"}){
            var response=await executor.ExecuteAsync(Envelope("bossStatus",new{},user),default);
            using var json=JsonDocument.Parse(response.DataJson!);
            Assert.Equal(10000-damage,json.RootElement.GetProperty("status").GetProperty("BossHp").GetInt32());
            Assert.Equal(oldId,json.RootElement.GetProperty("status").GetProperty("BossEventId").GetString());
            Assert.True(json.RootElement.GetProperty("damage").GetInt32()>0);
        }
        Assert.Equal(replies[0],await Executor().ExecuteAsync(first,default));
        Assert.Equal(replies[1],await Executor().ExecuteAsync(second,default));
        Assert.Equal(10000-damage,service.AdventureStatus.BossHp);
        await service.RemoteAdventures.ControlAsync("cancelboss",0,default);
        await Task.Delay(1100); // Existing round pause clamps to at least one second.
        await service.RemoteAdventures.ControlAsync("boss",0,default);
        Assert.NotEqual(oldId,service.AdventureStatus.BossEventId);
        foreach(var user in new[]{"1001","1002"}){
            var response=await Executor().ExecuteAsync(Envelope("bossStatus",new{},user),default);
            using var json=JsonDocument.Parse(response.DataJson!);
            Assert.Equal(10000,json.RootElement.GetProperty("status").GetProperty("BossHp").GetInt32());
            Assert.Equal(0,json.RootElement.GetProperty("damage").GetInt32());
        }
        await Executor().ExecuteAsync(first,default);
        Assert.Equal(10000,service.AdventureStatus.BossHp);
    }
    [Fact]
    public async Task Two_players_equip_and_buy_perks_without_cross_account_or_duplicate_booking()
    {
        await SeedAsync();
        foreach(var user in new[]{"1001","1002"}){
            await points.ApplyRpgAsync(user,user,"seed-ring-"+user,a=>{
                a.Points=2000;a.Rpg.Inventory.Add(new(){InstanceId="ring-"+user,ItemId="test-ring",Slot=EquipmentSlot.Ring1});
                return new(true);
            },default);
        }
        var executor=Executor();
        foreach(var user in new[]{"1001","1002"}){
            Assert.True((await executor.ExecuteAsync(Envelope("equip",new{instanceId="ring-"+user},user),default)).Success);
            Assert.False((await executor.ExecuteAsync(Envelope("equip",new{instanceId="ring-"+(user=="1001"?"1002":"1001")},user),default)).Success);
            Assert.Equal("ring-"+user,(await points.GetProfileAsync(user,default)).Entry.Rpg.Equipment[EquipmentSlot.Ring1]);
        }
        var a=Envelope("buyPerk",new{perkId="damage"},"1001");var b=Envelope("buyPerk",new{perkId="damage"},"1002");
        var results=await Task.WhenAll(executor.ExecuteAsync(a,default),executor.ExecuteAsync(a,default),executor.ExecuteAsync(b,default));
        Assert.All(results,r=>Assert.True(r.Success,r.Message));Assert.Equal(results[0],results[1]);
        var expected=2000-config.Adventures.Rpg.Perks.Single(p=>p.Id=="damage").Cost(0);
        foreach(var user in new[]{"1001","1002"}){
            var account=(await points.GetProfileAsync(user,default)).Entry;
            Assert.Equal(1,account.Rpg.Perks["damage"]);Assert.Equal(expected,account.Points);
            Assert.True((await executor.ExecuteAsync(Envelope("unequip",new{slot="Ring1"},user),default)).Success);
            Assert.False((await points.GetProfileAsync(user,default)).Entry.Rpg.Equipment.ContainsKey(EquipmentSlot.Ring1));
        }
        var restarted=new ViewerPointStore(Path.Combine(root,"players"));
        foreach(var user in new[]{"1001","1002"}){
            Assert.Equal(expected,await restarted.GetPointsAsync(user,default));
            Assert.Equal(1,(await restarted.GetProfileAsync(user,default)).Entry.Rpg.Perks["damage"]);
        }
    }
    public void Dispose(){service?.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
}
