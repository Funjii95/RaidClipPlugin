using System.Net;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class GroqIntegrationTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-groq-integration",Guid.NewGuid().ToString("N"));
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;public void Next()=>Now=Now.AddSeconds(11);}
    private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
    private sealed class Handler:HttpMessageHandler
    {
        public int Calls;public List<string> Bodies=[];
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Block;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;Bodies.Add(await request.Content!.ReadAsStringAsync(ct));Started.TrySetResult();if(Block is not null)await Block.Task.WaitAsync(ct);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content="Die Otterpfoten tanzen "+(char)('a'+Calls)+"."},finish_reason="stop"}}}))};
        }
    }
    private static MinigameConfig Config(){var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Rpg.Mimic.Enabled=false;return c;}
    [Theory]
    [InlineData(GroqScene.Dive,"Dive")]
    [InlineData(GroqScene.Fishing,"Fishing")]
    [InlineData(GroqScene.Mimic,"Mimic")]
    [InlineData(GroqScene.Boss,"Boss")]
    [InlineData(GroqScene.LevelUp,"LevelUp")]
    public async Task Each_narration_area_can_be_disabled_and_uses_no_player_identity(GroqScene scene,string property)
    {
        var options=new GroqOptions{Enabled=true};typeof(GroqOptions).GetProperty(property)!.SetValue(options,false);
        var handler=new Handler();using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"synthetic");
        var story=new GroqStoryEvent(scene,"private-action","Kampf gewonnen","PrivateViewer",42);
        Assert.Null(await service.StoryAsync(story,default));Assert.Equal(0,handler.Calls);
        typeof(GroqOptions).GetProperty(property)!.SetValue(options,true);Assert.NotNull(await service.StoryAsync(story,default));
        Assert.DoesNotContain("PrivateViewer",handler.Bodies.Last());Assert.DoesNotContain("private-action",handler.Bodies.Last());Assert.DoesNotContain("42",handler.Bodies.Last());
    }
    [Fact]public async Task Global_pause_hour_limit_and_duplicate_events_apply_across_areas()
    {
        var options=new GroqOptions{Enabled=true,HourlyLimit=2};var clock=new Clock();var handler=new Handler();using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"synthetic",clock);
        var first=new GroqStoryEvent(GroqScene.Dive,"one","Schatzfund");Assert.NotNull(await service.StoryAsync(first,default));
        Assert.Null(await service.StoryAsync(new(GroqScene.Boss,"two","Kampf gestartet"),default));Assert.Equal(1,handler.Calls);
        clock.Next();Assert.Null(await service.StoryAsync(first,default));Assert.Equal(1,handler.Calls);
        Assert.NotNull(await service.StoryAsync(new(GroqScene.Boss,"two","Kampf gestartet"),default));clock.Next();Assert.Null(await service.GenerateAsync(default));Assert.Equal(2,handler.Calls);
        clock.Now=clock.Now.AddHours(1);Assert.NotNull(await service.GenerateAsync(default));Assert.Equal(3,handler.Calls);
    }
    [Fact]public async Task Conversations_need_direct_mention_ignore_bots_and_isolate_viewer_history()
    {
        var options=new GroqOptions{Enabled=true};var clock=new Clock();var handler=new Handler();using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"synthetic",clock);
        ChatMessage Message(string id,string user,string text,bool bot=false)=>new(){Id=id,UserId=user,UserName=user,Text=text,IsBot=bot};
        Assert.Null(await service.ReplyAsync(Message("a","viewer","Hallo zusammen"),"bot","otterbot",default));
        Assert.Null(await service.ReplyAsync(Message("b","viewer","@otterbot_extra Hallo"),"bot","otterbot",default));
        Assert.Null(await service.ReplyAsync(Message("c","bot","@otterbot Hallo",true),"bot","otterbot",default));
        Assert.Null(await service.ReplyAsync(Message("d","viewer","!give @otterbot"),"bot","otterbot",default));Assert.Equal(0,handler.Calls);
        Assert.NotNull(await service.ReplyAsync(Message("e","viewerA","@OtterBot MeinMarkerAlpha"),"bot","otterbot",default));clock.Next();
        Assert.NotNull(await service.ReplyAsync(Message("f","viewerB","@otterbot AndereFrage"),"bot","otterbot",default));Assert.DoesNotContain("MeinMarkerAlpha",handler.Bodies.Last());Assert.DoesNotContain("viewerA",handler.Bodies.Last());clock.Next();
        Assert.NotNull(await service.ReplyAsync(Message("g","viewerA","@otterbot WeiterBitte"),"bot","otterbot",default));Assert.Contains("MeinMarkerAlpha",handler.Bodies.Last());Assert.DoesNotContain("AndereFrage",handler.Bodies.Last());clock.Next();
        await service.StoryAsync(new(GroqScene.Fishing,"story","Fisch gefangen"),default);Assert.DoesNotContain("MeinMarkerAlpha",handler.Bodies.Last());
        options.Conversations=false;clock.Next();Assert.Null(await service.ReplyAsync(Message("h","viewerA","@otterbot Hallo"),"bot","otterbot",default));Assert.Equal(4,handler.Calls);
    }
    [Fact]public async Task Level_event_is_after_commit_not_duplicate_failed_or_rolled_back()
    {
        var store=new ViewerPointStore(root);var events=new List<GroqStoryEvent>();store.StoryCommitted+=events.Add;
        await store.ApplyRpgAsync("1001","Otter","one",a=>{a.Rpg.PlayerLevel=2;return new(true);},default);
        Assert.Equal(2,Assert.Single(events).Level);Assert.Equal(2,(await store.GetProfileAsync("1001",default)).Entry.Rpg.PlayerLevel);
        await store.ApplyRpgAsync("1001","Otter","one",a=>{a.Rpg.PlayerLevel=3;return new(true);},default);
        await store.ApplyRpgAsync("1001","Otter","failed",a=>{a.Rpg.PlayerLevel=3;return new(false);},default);Assert.Single(events);
        Directory.CreateDirectory(Path.Combine(root,"viewer-points.json.tmp"));
        await Assert.ThrowsAnyAsync<Exception>(()=>store.ApplyRpgAsync("1001","Otter","rollback",a=>{a.Rpg.PlayerLevel=3;return new(true);},default));Assert.Single(events);
    }
    [Theory]
    [InlineData(RpgActivity.Dive,GroqScene.Dive)]
    [InlineData(RpgActivity.Fishing,GroqScene.Fishing)]
    public async Task Adventures_publish_only_committed_actions_for_chat_and_extension_paths(RpgActivity activity,GroqScene scene)
    {
        var store=new ViewerPointStore(root);var events=new List<GroqStoryEvent>();store.StoryCommitted+=events.Add;var config=Config();
        var adventure=new RpgAdventureService(store,config,new Low());
        Assert.True((await adventure.ExecuteAsync("1001","Otter","one",activity,0,default)).Success);Assert.Equal(scene,Assert.Single(events).Scene);
        Assert.False((await adventure.ExecuteAsync("1001","Otter","one",activity,0,default)).Success);Assert.Single(events);
        Assert.False((await adventure.ExecuteAsync("1001","Otter","cooldown",activity,0,default)).Success);Assert.Single(events);
    }
    [Theory]
    [InlineData(false,GroqScene.Boss)]
    [InlineData(true,GroqScene.Mimic)]
    public async Task Boss_start_and_victory_emit_once_and_not_per_attack(bool mimic,GroqScene scene)
    {
        var store=new ViewerPointStore(root);var events=new List<GroqStoryEvent>();store.StoryCommitted+=events.Add;var config=Config();config.Adventures.Rpg.Mimic.Enabled=true;config.Adventures.Rpg.Mimic.HitPoints=10;config.Adventures.Boss.HpMode=BossHpMode.Fixed;config.Adventures.Boss.HitPoints=10;config.Adventures.Rpg.Health.VictoryExperience=0;config.Adventures.Rpg.Health.TopExperience=0;
        var game=new AdventureGameService(store,config,(_,_)=>Task.CompletedTask,new Low());
        if(mimic){await store.ApplyRpgAsync("1001","Otter","mimic-test-gear",a=>{a.Rpg.PlayerLevel=99;var item=new RpgItem{Slot=EquipmentSlot.Weapon,Attributes=new(){Attack=100000}};a.Rpg.Inventory.Add(item);a.Rpg.Equipment[EquipmentSlot.Weapon]=item.InstanceId;return new(true);},default);events.Clear();}
        if(mimic)Assert.True(await game.StartDiscoveredBossAsync("Otter",default));else await game.ControlAsync("boss",0,default);
        Assert.Equal(scene,Assert.Single(events).Scene);var hit=await game.ExecuteBossAttackAsync("1001","Otter","attack",default);Assert.True(hit.Defeated);
        Assert.Equal(2,events.Count);Assert.All(events,e=>Assert.Equal(scene,e.Scene));Assert.Equal("Kampf gewonnen",events.Last().Outcome);
        await game.ExecuteBossAttackAsync("1001","Otter","attack",default);Assert.Equal(2,events.Count);
    }
    [Fact]public async Task Slow_or_cancelled_Groq_never_blocks_game_or_sends_after_stop()
    {
        var store=new ViewerPointStore(root);var options=new GroqOptions{Enabled=true};var handler=new Handler{Block=new(TaskCreationOptions.RunContinuationsAsynchronously)};using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"synthetic");var messages=new List<string>();using var stop=new CancellationTokenSource();
        var runtime=new GroqRuntime(store,service,"bot","otterbot",()=>true,(text,_)=>{messages.Add(text);return Task.CompletedTask;},stop.Token);
        var result=await new RpgAdventureService(store,Config(),new Low()).ExecuteAsync("1001","Otter","one",RpgActivity.Fishing,0,default).WaitAsync(TimeSpan.FromSeconds(2));Assert.True(result.Success);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));runtime.Stop();await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));Assert.Empty(messages);Assert.Equal(1,handler.Calls);
        store.PublishStory(new(GroqScene.Boss,"after-stop","Kampf gewonnen"));Assert.Equal(1,handler.Calls);
    }
    [Fact]public void Existing_daily_settings_gain_requested_switches_without_losing_model_or_enable()
    {
        var options=JsonSerializer.Deserialize<GroqOptions>("{\"Enabled\":true,\"Model\":\"openai/gpt-oss-20b\"}")!;
        Assert.All(Enum.GetValues<GroqScene>(),s=>Assert.True(options.Allows(s)));Assert.Equal(120,options.HourlyLimit);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
