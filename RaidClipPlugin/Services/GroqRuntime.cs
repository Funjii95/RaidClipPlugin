using System.Threading.Channels;
using System.Text.RegularExpressions;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

/// <summary>Bounded background narration; gameplay never waits for a language model.</summary>
public sealed class GroqRuntime
{
    private sealed record Work(DateTimeOffset Queued,GroqStoryEvent? Story,ChatMessage? Chat);
    private readonly Channel<Work> queue=Channel.CreateBounded<Work>(new BoundedChannelOptions(8){SingleReader=true,FullMode=BoundedChannelFullMode.DropOldest});
    private readonly CancellationTokenSource stop;
    private readonly ViewerPointStore store;
    private readonly GroqDailyService service;
    private readonly Func<bool> ready;
    private readonly Func<string,CancellationToken,Task> send;
    private readonly string botId,botLogin;
    private readonly HashSet<string> seen=[];
    private readonly Queue<string> ids=new();
    public Task Completion {get;}
    public GroqRuntime(ViewerPointStore store,GroqDailyService service,string botId,string botLogin,
        Func<bool> ready,Func<string,CancellationToken,Task> send,CancellationToken ct)
    {
        this.store=store;this.service=service;this.botId=botId;this.botLogin=botLogin;this.ready=ready;this.send=send;
        stop=CancellationTokenSource.CreateLinkedTokenSource(ct);store.StoryCommitted+=EnqueueStory;Completion=RunAsync();
    }
    private void EnqueueStory(GroqStoryEvent story){if(!stop.IsCancellationRequested&&ready())queue.Writer.TryWrite(new(DateTimeOffset.UtcNow,story,null));}
    public void EnqueueChat(ChatMessage message)
    {
        if(stop.IsCancellationRequested||!ready()||message.IsBot||message.UserId==botId||message.CommandAuthorization==CommandAuthorization.Denied||string.IsNullOrWhiteSpace(botLogin))return;
        if(!Regex.IsMatch(message.Text,@"(?<![A-Za-z0-9_])@"+Regex.Escape(botLogin)+@"(?![A-Za-z0-9_])",RegexOptions.IgnoreCase))return;
        queue.Writer.TryWrite(new(DateTimeOffset.UtcNow,null,message));
    }
    public void Stop(){store.StoryCommitted-=EnqueueStory;try{stop.Cancel();}catch(ObjectDisposedException){}queue.Writer.TryComplete();}
    private static string Mention(string name)=>Regex.IsMatch(name,@"\A[\p{L}\p{N}_]{1,30}\z")?"@"+name+" ":"";
    private async Task RunAsync()
    {
        try
        {
            await foreach(var work in queue.Reader.ReadAllAsync(stop.Token))
            {
                if(!ready()||DateTimeOffset.UtcNow-work.Queued>TimeSpan.FromSeconds(20))continue;
                var id=work.Story?.Id??"chat:"+work.Chat!.Id;if(!seen.Add(id))continue;ids.Enqueue(id);while(ids.Count>256)seen.Remove(ids.Dequeue());
                try
                {
                    var answer=work.Story is {} story?await service.StoryAsync(story,stop.Token):await service.ReplyAsync(work.Chat!,botId,botLogin,stop.Token);
                    if(string.IsNullOrWhiteSpace(answer)||!ready()||stop.IsCancellationRequested)continue;
                    var prefix=work.Story is {} e?"["+(e.Scene switch{GroqScene.Dive=>"Tauchen",GroqScene.Fishing=>"Angeln",GroqScene.Mimic=>"Mimic",GroqScene.Boss=>"Boss",GroqScene.LevelUp=>"Level "+e.Level,_=>"RPG"})+"] "+Mention(e.DisplayName):Mention(work.Chat!.UserLogin.Length>0?work.Chat.UserLogin:work.Chat.UserName);
                    await send(prefix+answer,stop.Token);
                }
                catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
                catch{ /* Keep game receipts and Twitch actions independent of narration failures. */ }
            }
        }
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        finally{store.StoryCommitted-=EnqueueStory;stop.Dispose();}
    }
}
