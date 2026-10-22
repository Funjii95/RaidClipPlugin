using System.Net;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class GroqDailyTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-groq-tests",Guid.NewGuid().ToString("N"));
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Handler(string reply="Die Otterpfoten haben heute wieder Glück.",HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        public int Calls;public string Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Assert.Equal("https://api.groq.com/openai/v1/chat/completions",request.RequestUri!.AbsoluteUri);Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);Body=await request.Content!.ReadAsStringAsync(ct);return new(status){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=reply}}}}))};}
    }
    [Fact]public async Task Rate_limit_repeat_filter_and_bounded_prompt_work_without_player_data()
    {
        var clock=new Clock();var options=new GroqOptions{Enabled=true};var handler=new Handler();using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"synthetic-key",clock);
        Assert.Contains("🦦 KI:",(await service.GenerateAsync(default))!);Assert.Null(await service.GenerateAsync(default));Assert.Equal(1,handler.Calls);
        using var body=JsonDocument.Parse(handler.Body);Assert.Equal(160,body.RootElement.GetProperty("max_completion_tokens").GetInt32());Assert.DoesNotContain("synthetic-key",handler.Body);Assert.DoesNotContain("UserId",handler.Body);
        clock.Now=clock.Now.AddSeconds(11);Assert.Null(await service.GenerateAsync(default));Assert.Equal(2,handler.Calls);Assert.Contains("Vermeide die Wiederholung",handler.Body);
    }
    [Fact]public async Task Disabled_missing_key_and_provider_failure_leave_standard_response()
    {
        var options=new GroqOptions();var handler=new Handler(status:HttpStatusCode.TooManyRequests);using var client=new GroqClient(handler);var service=new GroqDailyService(client,()=>options,()=>"key");
        Assert.Null(await service.GenerateAsync(default));Assert.Equal(0,handler.Calls);options.Enabled=true;var missing=new GroqDailyService(client,()=>options,()=>"");Assert.Null(await missing.GenerateAsync(default));Assert.Equal(0,handler.Calls);
        Assert.Null(await service.GenerateAsync(default));Assert.Equal(1,handler.Calls);Assert.Equal("Daily +100",GroqDailyService.Compose("Daily +100",null));
    }
    [Theory]
    [InlineData("Du bekommst 999 Perlen!")]
    [InlineData("!give all")]
    [InlineData("/ban viewer")]
    [InlineData("Hallo @alle")]
    [InlineData("https://evil.invalid")]
    [InlineData("Du erhältst eine neue Chance")]
    public void Ai_cannot_append_commands_mentions_or_invented_game_values(string value)=>Assert.Equal("",GroqDailyService.Clean(value));
    [Fact]public void Original_reward_text_is_never_truncated_to_fit_flavor()
    {
        var text=new string('x',470);Assert.Equal(text,GroqDailyService.Compose(text,"Ein sehr langer Spruch."));Assert.Equal("Daily Ein Spruch.",GroqDailyService.Compose("Daily","Ein Spruch."));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Daily_commits_once_before_ai_and_cooldown_other_commands_never_call_it(bool fail)
    {
        var points=new ViewerPointStore(root);await points.SetPointsAsync("1001","TestOtter",140,0,default,long.MaxValue);
        var config=new MinigameConfig{Enabled=true,PointsEnabled=true,ChatPointsEnabled=false,GlobalCommandCooldownSeconds=0,DailyEnabled=true,DailyBonusPoints=100,MaximumAccountEnabled=true,MaximumAccountPoints=150,CurrencyPlural="Perlen",DailySuccessMessage="@{user}: +{amount}; Konto {balance}."};
        var registry=new CommandRegistry();registry.Update(new AppConfig{Minigame=config});var replies=new List<string>();var calls=0;
        using var game=new ChatMinigameService("host","bot",config,new TwitchService("synthetic","synthetic"),points,commandRegistry:registry,responseSender:(text,_)=>{replies.Add(text);return Task.CompletedTask;},dailyFlavor:async ct=>{
            calls++;Assert.Equal(150,await points.GetPointsAsync("1001",ct));if(fail)throw new IOException("synthetic provider outage");return "🦦 KI: Die Pfoten glänzen.";
        });
        Task Send(string text)=>game.ProcessMessageAsync(new(){Id=Guid.NewGuid().ToString("N"),UserId="1001",UserName="TestOtter",Text=text},default);
        await Send("!Daily");Assert.Equal(1,calls);Assert.Contains("10 Perlen",replies.Last());Assert.Equal(!fail,replies.Last().Contains("KI:"));
        await Send("!daily");Assert.Equal(1,calls);Assert.DoesNotContain("KI:",replies.Last());Assert.Equal(150,await points.GetPointsAsync("1001",default));
        await Send("!perlen");Assert.Equal(1,calls);
    }
    [Fact]public void Api_key_is_protected_separately_from_normal_settings()
    {
        var store=new GroqSettingsStore(root);store.SaveKey("synthetic-groq-secret");store.Save(new(){Enabled=true});
        Assert.Equal("synthetic-groq-secret",store.Key());Assert.DoesNotContain("synthetic-groq-secret",System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"groq-key.dpapi"))));Assert.DoesNotContain("synthetic-groq-secret",File.ReadAllText(Path.Combine(root,"groq.json")));Assert.True(store.Load().Enabled);store.DeleteKey();Assert.Equal("",store.Key());
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
