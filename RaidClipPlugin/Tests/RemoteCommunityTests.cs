using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RemoteCommunityTests : IAsyncLifetime
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-community",Guid.NewGuid().ToString("N"));
    private ViewerPointStore points=null!;private DuelService duel=null!;private HeistService heist=null!;
    private readonly FakeTwitch twitch=new();private readonly HashSet<string> users=["1001","1002"];
    public async Task InitializeAsync(){
        points=new(root);await points.SetPointsAsync("1001","SYNTHETIC",1000,0,default);await points.SetPointsAsync("1002","TARGET",1000,0,default);
        var minigame=new MinigameConfig{Enabled=true,PointsEnabled=true,JackpotStartValue=100};
        duel=new("42","42",new DuelConfig{Enabled=true,UserCooldownSeconds=0,GlobalCooldownSeconds=0},minigame,twitch,points,new FixedRandom());
        heist=new("42","42",new HeistConfig{Enabled=true,MinimumParticipants=2,SuccessChancePercent=100,JoinDurationSeconds=60},minigame,twitch,points,new FixedRandom());
    }
    private static ChatMessage Actor(string user)=>new(){UserId=user,UserName=user=="1001"?"SYNTHETIC":"TARGET",UserLogin=user=="1001"?"synthetic":"target",Id=Guid.NewGuid().ToString("N")};
    private async Task<string> EventAsync(){using var json=JsonDocument.Parse(JsonSerializer.Serialize(await duel.ReadRemoteAsync("1002",1,default)));return json.RootElement.GetProperty("eventId").GetString()!;}
    [Fact]public async Task Duel_resolves_target_on_server_rejects_foreign_target_and_stale_invitation_then_pays_once(){
        Assert.False(await duel.ExecuteRemoteAsync(Actor("1001"),"challenge","-","100","outsider",users,default));Assert.Equal(1000,await points.GetPointsAsync("1001",default));
        Assert.True(await duel.ExecuteRemoteAsync(Actor("1001"),"challenge","-","100","target",users,default));Assert.Equal(900,await points.GetPointsAsync("1001",default));
        var eventId=await EventAsync();
        Assert.False(await duel.ExecuteRemoteAsync(Actor("1001"),"accept",eventId,"0","-",users,default));
        Assert.False(await duel.ExecuteRemoteAsync(Actor("1002"),"accept","stale","0","-",users,default));
        Assert.True(await duel.ExecuteRemoteAsync(Actor("1002"),"accept",eventId,"0","-",users,default));
        Assert.False(await duel.ExecuteRemoteAsync(Actor("1002"),"accept",eventId,"0","-",users,default));
        Assert.Equal(2000,await points.GetPointsAsync("1001",default)+await points.GetPointsAsync("1002",default));
        await points.RecoverDuelReservationsAsync(default);Assert.Equal(1100,await points.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Duel_denial_refunds_once_and_cannot_affect_later_invitation(){
        Assert.True(await duel.ExecuteRemoteAsync(Actor("1001"),"challenge","-","100","target",users,default));var old=await EventAsync();
        Assert.True(await duel.ExecuteRemoteAsync(Actor("1002"),"deny",old,"0","-",users,default));Assert.Equal(1000,await points.GetPointsAsync("1001",default));
        Assert.True(await duel.ExecuteRemoteAsync(Actor("1001"),"challenge","-","100","target",users,default));
        Assert.False(await duel.ExecuteRemoteAsync(Actor("1002"),"deny",old,"0","-",users,default));Assert.Equal(900,await points.GetPointsAsync("1001",default));
    }
    [Fact]public async Task Heist_join_uses_existing_round_excludes_stale_requests_and_does_not_charge_entry(){
        await heist.StartAsync(new(){UserId="42",UserName="HOST",IsBroadcaster=true},default);var id=heist.Snapshot!.EventId;
        Assert.False(await heist.JoinRemoteAsync(Actor("1001"),"old-event",default));
        Assert.True(await heist.JoinRemoteAsync(Actor("1001"),id,default));Assert.False(await heist.JoinRemoteAsync(Actor("1001"),id,default));
        Assert.Equal(1000,await points.GetPointsAsync("1001",default));await heist.FinishJoiningForTestAsync();
        Assert.Equal(HeistState.Successful,heist.State);Assert.Equal(1050,await points.GetPointsAsync("1001",default));
        Assert.False(await heist.JoinRemoteAsync(Actor("1002"),id,default));
    }
    [Fact]public async Task Heist_round_cannot_be_paid_twice_and_interrupted_jackpot_reset_recovers(){
        await points.ResetJackpotAsync(100,default);var id=Guid.NewGuid().ToString("N");
        await points.PayoutHeistJackpotAsync([("1001","SYNTHETIC"),("1002","TARGET")],[],0,false,100,default,id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>points.PayoutHeistJackpotAsync([("1001","SYNTHETIC"),("1002","TARGET")],[],0,false,100,default,id));
        // Simulate crash after atomic player rename, before the separate jackpot state rename.
        await File.WriteAllTextAsync(Path.Combine(root,"state.json"),"100");
        await File.WriteAllTextAsync(Path.Combine(root,"heist-commit.json"),JsonSerializer.Serialize(new{Marker="heist-paid:"+id,Users=new[]{"1001","1002"},JackpotAfter=0}));
        var restarted=new ViewerPointStore(root);Assert.Equal(0,await restarted.GetJackpotAsync(0,default));Assert.Equal(1050,await restarted.GetPointsAsync("1001",default));Assert.False(File.Exists(Path.Combine(root,"heist-commit.json")));
    }
    public async Task DisposeAsync(){await duel.DisposeAsync();await heist.DisposeAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class FixedRandom:IDuelRandom,IHeistRandom{public int NextInclusive(int minimum,int maximum)=>minimum;}
    private sealed class FakeTwitch:IDuelTwitchClient,IHeistTwitchClient{
        public Task<TwitchUser?> GetUserAsync(string login,CancellationToken ct)=>Task.FromResult<TwitchUser?>(login=="target"?new("1002","target","TARGET"):new("9999","outsider","OUTSIDER"));
        public Task SendChatMessageAsync(string broadcasterId,string senderId,string text,CancellationToken ct)=>Task.CompletedTask;
        public Task<bool> IsFollowerAsync(string broadcasterId,string userId,CancellationToken ct)=>Task.FromResult(false);
    }
}
