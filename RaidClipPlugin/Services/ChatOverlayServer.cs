using System.Net;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record ChatOverlayEmote(string Code,string Url);
public sealed record ChatOverlayMessage(string Id,DateTimeOffset Timestamp,string Name,string Text,string Color,
    bool Broadcaster,bool Moderator,bool Vip,bool Subscriber,IReadOnlyList<ChatOverlayEmote> Emotes);
public sealed record ChatOverlayState(DateTimeOffset ServerNow,IReadOnlyList<ChatOverlayMessage> Messages,
    bool ShowTimestamps=false,bool ShowBadges=true,bool ShowUserColors=true,int EmoteSize=28)
{
    public static ChatOverlayState Empty()=>new(DateTimeOffset.UtcNow,[]);
}
public sealed class ChatOverlayServer(Func<ChatOverlayState> snapshot,int port=18770)
{
    public const string Url="http://127.0.0.1:18770/";
    public async Task RunAsync(CancellationToken ct)
    {
        if(port is <1 or >65535)throw new ArgumentOutOfRangeException(nameof(port));
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        try
        {
            listener.Start();using var stop=ct.Register(listener.Stop);
            while(!ct.IsCancellationRequested)
            {
                var context=await listener.GetContextAsync();
                try
                {
                    var path=context.Request.Url?.AbsolutePath;
                    if(context.Request.HttpMethod!="GET" || path is not ("/" or "/state" or "/preview")){
                        context.Response.StatusCode=404;continue;
                    }
                    context.Response.Headers["Cache-Control"]="no-store";
                    context.Response.Headers["X-Content-Type-Options"]="nosniff";
                    context.Response.Headers["Content-Security-Policy"]="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src https://static-cdn.jtvnw.net https://cdn.betterttv.net https://cdn.7tv.app; frame-ancestors 'none'";
                    context.Response.ContentType=path=="/state"?"application/json; charset=utf-8":"text/html; charset=utf-8";
                    var bytes=Encoding.UTF8.GetBytes(path=="/state"?JsonSerializer.Serialize(snapshot()):ChatOverlayPage.Html);
                    context.Response.ContentLength64=bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes,ct);
                }
                catch(Exception error) when(error is IOException or HttpListenerException){}
                finally{context.Response.Close();}
            }
        }
        catch(Exception error) when(ct.IsCancellationRequested && error is HttpListenerException or ObjectDisposedException or OperationCanceledException){}
        catch(HttpListenerException){Console.WriteLine("Chat-Overlay nicht verfügbar: Port "+port+" bereits belegt oder Zugriff verweigert.");}
    }
}
