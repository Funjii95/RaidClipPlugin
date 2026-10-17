using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class HeistOverlayTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipHeistOverlay-"+Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now=DateTimeOffset.UtcNow;
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    private HeistEventSnapshot Event(string id="heist",HeistState state=HeistState.Joining)=>new(id,"Perlenschatzkammer",state,_now,_now.AddSeconds(60),
        state is HeistState.Successful or HeistState.Failed or HeistState.Cancelled?_now:null,3,50,"!joinheist","Perlen",1000,0,
        new[]{new HeistPlayerResult("private-id","Otter",state==HeistState.Successful?1000:0)},state==HeistState.Successful?1000:0);
    [Fact]
    public void PriorityConflictsNeverRewindGameCountdownOrDropQueuedResult()
    {
        var manager=new CommunityOverlayEventManager();manager.Update(Event());
        var both=new AdventureStatus(BossActive:true,BossEndsAt:_now.AddSeconds(80),RaceActive:true,RaceEndsAt:_now.AddSeconds(70));
        var first=manager.Read(both,_now);Assert.Equal(CommunityEventType.Boss,first.Current!.Type);Assert.Equal(2,first.QueuedCount);Assert.Null(first.Heist);
        Assert.Equal(CommunityEventType.Race,manager.Read(both with{BossActive=false},_now).Current!.Type);
        Assert.Equal(_now.AddSeconds(60),manager.Read(new(),_now.AddSeconds(30)).Heist!.JoinEndsAt);
        manager.Update(Event(state:HeistState.Successful) with{CompletedAt=_now.AddSeconds(60)});
        Assert.Equal(CommunityEventType.Boss,manager.Read(both,_now.AddSeconds(65)).Current!.Type);
        var result=manager.Read(new(),_now.AddSeconds(85));Assert.Equal("Raiding",result.Heist!.Phase);Assert.Equal(_now.AddSeconds(85),result.Heist.PresentationStartedAt);
        Assert.Equal("Successful",manager.Read(new(),_now.AddSeconds(92)).Heist!.Phase);
        Assert.Equal(1000,result.Heist.TotalPayout);
    }
    [Fact]
    public void SameEventUpdatesPlayersAndReloadKeepsOnePresentationTimeline()
    {
        var manager=new CommunityOverlayEventManager();manager.Update(Event());
        manager.Update(Event() with{Players=new[]{new HeistPlayerResult("a","A",0),new HeistPlayerResult("b","B",0)}});
        Assert.Equal(2,manager.Read(new(),_now).Heist!.ParticipantCount);
        manager.Update(Event(state:HeistState.Successful));
        var first=manager.Read(new(),_now).Heist!;var reload=manager.Read(new(),_now.AddSeconds(3)).Heist!;
        Assert.Equal(first.PresentationStartedAt,reload.PresentationStartedAt);
        manager.Update(Event());Assert.Equal("Raiding",manager.Read(new(),_now.AddSeconds(3)).Heist!.Phase);
        Assert.Equal("Hiding",manager.Read(new(),_now.AddSeconds(16)).Heist!.Phase);
        Assert.Null(manager.Read(new(),_now.AddSeconds(18)).Current);
        manager.Update(Event(state:HeistState.Successful));Assert.Null(manager.Read(new(),_now.AddSeconds(19)).Current);
    }
    [Fact]
    public void QueueIsBoundedExpiresStaleEventsAndHonorsExplicitHide()
    {
        var manager=new CommunityOverlayEventManager();
        for(var i=0;i<30;i++)manager.Update(Event("h"+i));
        Assert.Equal(15,manager.Read(new(),_now).QueuedCount);
        Assert.Null(manager.Read(new(),_now.AddMinutes(3)).Current);
        manager.Update(Event("result",HeistState.Failed));Assert.Null(manager.Read(new(),_now.AddMinutes(3)).Current);
        manager.Update(Event("hide"));manager.Hide("hide",_now);manager.Update(Event("hide"));Assert.Null(manager.Read(new(),_now).Current);
    }
    [Fact]
    public void DisabledOverlayParticipantPrivacyAndVisibleLimitsDoNotChangeSnapshot()
    {
        var config=new HeistOverlayConfig{Enabled=false};var manager=new CommunityOverlayEventManager(config);var e=Event();manager.Update(e);
        Assert.Null(manager.Read(new(),_now).Heist);config=new(){MaximumVisibleParticipants=1};manager.Configure(config);
        manager.Update(e with{Players=new[]{new HeistPlayerResult("a","Alpha",2),new HeistPlayerResult("b","Beta",3)}});
        var s=manager.Read(new(),_now);Assert.Equal(2,s.Heist!.ParticipantCount);Assert.Single(s.Heist.Players);
        Assert.DoesNotContain("private-id",JsonSerializer.Serialize(s));Assert.DoesNotContain("UserId",JsonSerializer.Serialize(s));
        manager.Configure(new(){ShowParticipants=false});Assert.Empty(manager.Read(new(),_now).Heist!.Players);
        manager.Configure(new(){Enabled=false});Assert.Null(manager.Read(new(),_now).Heist);
    }
    [Fact]
    public void HotTimeUsesExistingExpiryAndDoesNotCoverBossOrRestartOnReconnect()
    {
        var manager=new CommunityOverlayEventManager();var end=_now.AddMinutes(5);var hot=new RpgHotTimeStatus(new("hot","boss",_now,end,true),300,true);
        var config=new RpgHotTimeConfig{DropBonusPercent=25,PearlBonusPercent=75};
        var s=manager.Read(new(),_now,hot,config);Assert.True(s.HotTimeActive);Assert.Equal(end,s.HotTimeEndsAt);Assert.Equal(25,s.HotTimeDrop);Assert.Equal(75,s.HotTimePearls);
        Assert.False(manager.Read(new(BossActive:true,BossEndsAt:end),_now,hot,config).HotTimeActive);
        Assert.Equal(end,manager.Read(new(),_now.AddSeconds(100),hot,config).HotTimeEndsAt);
    }
    private sealed class Chat:IHeistTwitchClient
    {
        public List<string> Messages=new();
        public Task SendChatMessageAsync(string broadcasterId,string senderId,string message,CancellationToken token){Messages.Add(message);return Task.CompletedTask;}
        public Task<bool> IsFollowerAsync(string broadcasterId,string userId,CancellationToken token)=>Task.FromResult(true);
    }
    private sealed class Roll(bool success):IHeistRandom{public int NextInclusive(int min,int max)=>success?min:max;}
    private static ChatMessage User(string id,bool mod=false)=>new(){Id=Guid.NewGuid().ToString("N"),UserId=id,UserName=id,UserLogin=id,IsModerator=mod};
    [Theory]
    [InlineData(true,true)] [InlineData(true,false)] [InlineData(false,true)]
    public async Task RealHeistSnapshotMatchesAtomicPointPayoutWithoutObsAndCannotPayTwice(bool success,bool overlayEnabled)
    {
        var chat=new Chat();var points=new ViewerPointStore(_dir);var config=new HeistConfig{Enabled=true,JoinDurationSeconds=60,MinimumParticipants=3,GlobalCooldownMinutes=0,SendParticipantJoinMessages=false,Overlay=new(){Enabled=overlayEnabled}};
        var minigame=new MinigameConfig{Enabled=true,PointsEnabled=true,JackpotStartValue=1000};
        await using var service=new HeistService("b","bot",config,minigame,chat,points,new Roll(success));
        var manager=new CommunityOverlayEventManager(config.Overlay);var phases=new List<HeistState>();
        service.EventChanged+=e=>throw new IOException("OBS getrennt / fehlerhafter Subscriber");
        service.EventChanged+=e=>{manager.Update(e);phases.Add(e.State);};
        await service.StartAsync(User("mod",true),default);var id=service.Snapshot!.EventId;
        await service.JoinAsync(User("a"),default);await service.JoinAsync(User("b"),default);
        Assert.Equal(3,service.Snapshot!.Players.Count);Assert.Equal(0,service.Snapshot.EntryStake);Assert.Equal(60,(service.Snapshot.JoinEndsAt-service.Snapshot.StartedAt).TotalSeconds);
        await service.FinishJoiningForTestAsync();var final=service.Snapshot!;
        Assert.Equal(id,final.EventId);Assert.Equal(success?HeistState.Successful:HeistState.Failed,final.State);Assert.Equal(success?1000:0,final.TotalPayout);Assert.Equal(0,final.TotalLost);
        Assert.Contains(HeistState.Evaluating,phases);Assert.Equal(3,final.Players.Count);Assert.Equal(final.TotalPayout,final.Players.Sum(p=>p.Payout));
        foreach(var player in final.Players)Assert.Equal(player.Payout,(await points.GetProfileAsync(player.UserId,default)).Entry.Points);
        await service.FinishJoiningForTestAsync();Assert.Equal(final,service.Snapshot);
        Assert.Single(chat.Messages,m=>m.Contains("plant einen Heist"));Assert.Single(chat.Messages,m=>m.Contains(success?"war erfolgreich":"ist gescheitert"));
        Assert.DoesNotContain(chat.Messages,m=>m.Contains("nimmt am Heist teil"));
        Assert.Equal(overlayEnabled,manager.Read(new(),DateTimeOffset.UtcNow).Heist is not null);
    }
    [Fact]
    public async Task CancelAndDisabledChatFlagsDoNotPayOrInventLosses()
    {
        var chat=new Chat();var points=new ViewerPointStore(_dir);var c=new HeistConfig{Enabled=true,SendStartMessages=false,SendResultMessages=false};
        await using var service=new HeistService("b","bot",c,new(){JackpotStartValue=1000},chat,points,new Roll(true));
        await service.StartAsync(User("mod",true),default);await service.CancelAsync(false);
        Assert.Empty(chat.Messages);Assert.Equal(HeistState.Cancelled,service.Snapshot!.State);Assert.Equal(0,service.Snapshot.TotalPayout);
        Assert.Equal(1000,await points.GetJackpotAsync(1000,default));
    }
    [Fact]
    public async Task LoopbackOverlayReloadIsReadOnlyAndDoesNotExposePrivateIds()
    {
        using var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        using var stop=new CancellationTokenSource();var manager=new CommunityOverlayEventManager();manager.Update(Event(state:HeistState.Successful));
        var server=new AdventureOverlayServer(()=>new(),port,()=>manager.Read(new(),DateTimeOffset.UtcNow));var task=server.RunAsync(stop.Token);
        try
        {
            using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}/"),Timeout=TimeSpan.FromSeconds(5)};
            var a=JsonSerializer.Deserialize<CommunityOverlayState>(await http.GetStringAsync("community"))!;
            var b=JsonSerializer.Deserialize<CommunityOverlayState>(await http.GetStringAsync("community"))!;
            Assert.Equal(a.Heist!.PresentationStartedAt,b.Heist!.PresentationStartedAt);Assert.DoesNotContain("private-id",JsonSerializer.Serialize(a));
            var html=await http.GetStringAsync("preview");Assert.Contains("LOKALE VORSCHAU",html);Assert.Contains("otter-pirate",html);Assert.Contains("background:transparent",html);
            using var post=await http.PostAsync("community",null);Assert.Equal(HttpStatusCode.NotFound,post.StatusCode);
            Assert.Contains("!attack",await http.GetStringAsync(""));
        }
        finally{stop.Cancel();await task.WaitAsync(TimeSpan.FromSeconds(5));}
    }
    [Fact]
    public void ConfigPersistenceAndOldSettingsUseSafeDefaults()
    {
        var old=JsonSerializer.Deserialize<HeistConfig>("{\"Enabled\":true}")!;old.Overlay.Validate();Assert.True(old.SendStartMessages);
        old.Overlay.ResultSeconds=15;old.Overlay.BrowserSource="Eigene Quelle";
        var restored=JsonSerializer.Deserialize<HeistConfig>(JsonSerializer.Serialize(old))!;Assert.Equal(15,restored.Overlay.ResultSeconds);Assert.Equal("Eigene Quelle",restored.Overlay.BrowserSource);
        restored.Overlay.AnimationSeconds=0;Assert.Throws<InvalidOperationException>(restored.Overlay.Validate);
    }
}
