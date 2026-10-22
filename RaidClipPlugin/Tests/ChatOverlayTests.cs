using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class ChatOverlayTests
{
    private static LiveChatService Service(LiveChatConfig? config=null)=>new(config??new(),"bot",[],new CommandRegistry());
    private static ChatMessage Message(string id,string text="Hallo",bool bot=false)=>new(){Id=id,UserId="private-user-id",UserName="Otter",UserLogin="otter",Text=text,IsBot=bot,ReceivedAt=DateTimeOffset.UtcNow};
    [Fact]public void Public_snapshot_is_bounded_filtered_and_does_not_expose_raw_or_private_fields()
    {
        var service=Service(new(){HideBotMessages=true});
        for(var i=0;i<40;i++)service.ProcessMessage(Message(i.ToString()));
        service.ProcessMessage(Message("bot","secret-bot-text",true));
        service.AddSystemMessage("internal-status");
        var snapshot=service.GetOverlaySnapshot();Assert.Equal(30,snapshot.Messages.Count);Assert.Equal("10",snapshot.Messages[0].Id);
        var json=JsonSerializer.Serialize(snapshot);Assert.DoesNotContain("private-user-id",json);Assert.DoesNotContain("secret-bot-text",json);Assert.DoesNotContain("internal-status",json);Assert.DoesNotContain("RawMessage",json);
        service.Clear();Assert.Empty(service.GetOverlaySnapshot().Messages);
    }
    [Fact]public void Disable_and_filter_changes_apply_immediately_to_overlay()
    {
        var commands=new CommandRegistry();commands.Update(new AppConfig());
        var service=new LiveChatService(new(),"bot",[],commands);service.ProcessMessage(Message("one","!sim"));
        Assert.Single(service.GetOverlaySnapshot().Messages);
        service.UpdateConfig(new(){HideCommands=true});Assert.Empty(service.GetOverlaySnapshot().Messages);
        service.UpdateConfig(new(){Enabled=false});Assert.Empty(service.GetOverlaySnapshot().Messages);
    }
    [Fact]public void Emotes_respect_settings_and_reject_non_cdn_urls()
    {
        var service=Service(new(){EnableBttvEmotes=true,EnableAnimatedEmotes=false});
        service.ProcessMessage(new(){Id="one",UserName="Otter",Text="Kappa Fish Nope",Emotes=[new("25","Kappa"),new("../invalid","Nope")]});
        var external=new Dictionary<string,ExternalEmote>{["Fish"]=new("Fish","https://cdn.betterttv.net/emote/one/2x",false,"BTTV","https://cdn.betterttv.net/emote/one/2x"),["Nope"]=new("Nope","https://evil.invalid/image",false,"BTTV","https://evil.invalid/image")};
        var emotes=Assert.Single(service.GetOverlaySnapshot(external).Messages).Emotes;
        Assert.Equal(2,emotes.Count);Assert.Contains(emotes,e=>e.Code=="Kappa"&&e.Url.Contains("/static/"));Assert.Contains(emotes,e=>e.Code=="Fish");
        service.UpdateConfig(new(){EnableTwitchEmotes=false});Assert.Empty(Assert.Single(service.GetOverlaySnapshot(external).Messages).Emotes);
    }
    [Fact]public async Task Loopback_routes_are_read_only_uncached_and_stop_cleanly()
    {
        using var portReservation=new TcpListener(IPAddress.Loopback,0);portReservation.Start();var port=((IPEndPoint)portReservation.LocalEndpoint).Port;portReservation.Stop();
        using var stop=new CancellationTokenSource();var service=Service();service.ProcessMessage(Message("one"));
        var task=new ChatOverlayServer(()=>service.GetOverlaySnapshot(),port).RunAsync(stop.Token);
        try{
            using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}/"),Timeout=TimeSpan.FromSeconds(5)};
            using var response=await http.GetAsync("state");Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
            Assert.DoesNotContain("private-user-id",await response.Content.ReadAsStringAsync());
            Assert.Contains("frame-ancestors 'none'",Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
            Assert.Contains("background:transparent",await http.GetStringAsync(""));Assert.Contains("TESTNACHRICHTEN",await http.GetStringAsync("preview"));
            using var post=await http.PostAsync("state",null);Assert.Equal(HttpStatusCode.NotFound,post.StatusCode);
            using var secret=await http.GetAsync("settings.json");Assert.Equal(HttpStatusCode.NotFound,secret.StatusCode);
            Assert.Single(service.GetOverlaySnapshot().Messages);
        }finally{stop.Cancel();await task.WaitAsync(TimeSpan.FromSeconds(5));}
    }
}
