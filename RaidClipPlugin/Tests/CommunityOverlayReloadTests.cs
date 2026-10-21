using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CommunityOverlayReloadTests
{
    [Theory]
    [InlineData(CommunityEventType.Boss)]
    [InlineData(CommunityEventType.Race)]
    [InlineData(CommunityEventType.Heist)]
    public async Task EachNewEventReloadsOnceNotEveryParticipantOrCountdownUpdate(CommunityEventType type)
    {
        var calls=0;var logs=new List<string>();var service=new CommunityOverlayReloadService(()=>{Interlocked.Increment(ref calls);return 1;},logs.Add);
        await Task.WhenAll(Enumerable.Range(0,50).Select(_=>service.RequestAsync(type,"first")));
        Assert.Equal(1,calls);Assert.Single(logs);await service.RequestAsync(type,"second");Assert.Equal(2,calls);
    }
    [Fact]
    public async Task MissingObsDoesNotFaultAndAnotherEventCanTryAgain()
    {
        var calls=0;var logs=new List<string>();var service=new CommunityOverlayReloadService(()=>{calls++;throw new IOException("OBS getrennt");},logs.Add);
        await service.RequestAsync(CommunityEventType.Boss,"1");await service.RequestAsync(CommunityEventType.Race,"2");
        Assert.Equal(2,calls);Assert.All(logs,l=>Assert.Contains("Event läuft weiter",l));
    }
    [Fact]
    public async Task ReloadRunsAwayFromGameThreadAndLoggingFailureIsIsolated()
    {
        using var blocked=new ManualResetEventSlim();
        var service=new CommunityOverlayReloadService(()=>{blocked.Wait(TimeSpan.FromSeconds(5));return 1;},_=>throw new IOException("log unavailable"));
        var pending=service.RequestAsync(CommunityEventType.Heist,"1");
        try{Assert.False(pending.IsCompleted);}finally{blocked.Set();}
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Theory]
    [InlineData("http://127.0.0.1:18769/",true)]
    [InlineData("http://localhost:18769/?x=1",true)]
    [InlineData("http://127.0.0.1:18769/preview",false)]
    [InlineData("http://127.0.0.1:17891/",false)]
    [InlineData("http://example.com:18769/",false)]
    [InlineData("not a URL",false)]
    public void AutomaticSourceDiscoveryOnlyMatchesTheEventOverlay(string url,bool expected)=>Assert.Equal(expected,ObsService.IsCommunityOverlayUrl(url));
    [Fact]
    public async Task RealBossAndRaceOnlyRaiseStartWhenStateIsAlreadyAvailable()
    {
        var dir=Path.Combine(Path.GetTempPath(),"RaidClipReload-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=new MinigameConfig{Enabled=true,PointsEnabled=true};var points=new ViewerPointStore(dir);
            var service=new AdventureGameService(points,c,(_,_)=>Task.CompletedTask);
            var events=new List<CommunityEventType>();
            service.OverlayEventStarted+=(_,_)=>throw new IOException("OBS not connected");
            service.OverlayEventStarted+=(type,id)=>
            {
                Assert.NotEmpty(id);Assert.True(type==CommunityEventType.Boss?service.Status.BossActive:service.Status.RaceActive);events.Add(type);
            };
            await service.ControlAsync("boss",0,default);await service.TickAsync(default);await service.ControlAsync("boss",0,default);
            Assert.Single(events);Assert.True(service.Status.BossActive);
            await points.SetPointsAsync("u","U",1000,0,default);await points.SetPointsAsync("v","V",1000,0,default);
            await service.HandleAsync(new(){UserId="u",UserName="U"},"!startrace",new[]{"!startrace","100"},default);
            await service.HandleAsync(new(){UserId="v",UserName="V"},"!joinrace",new[]{"!joinrace"},default);
            await service.TickAsync(default);Assert.Equal(new[]{CommunityEventType.Boss,CommunityEventType.Race},events);
        }
        finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
