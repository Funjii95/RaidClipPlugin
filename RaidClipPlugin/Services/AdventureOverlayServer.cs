using System.Net;
using System.Text;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed record AdventureStatus(bool BossActive = false, int BossHp = 0, int BossMaxHp = 0,
    int BossPlayers = 0, DateTimeOffset? BossEndsAt = null, bool RaceActive = false,
    int RacePlayers = 0, long RaceStake = 0, DateTimeOffset? RaceEndsAt = null,
    string BossEventId="",DateTimeOffset? BossStartedAt=null,string RaceEventId="",DateTimeOffset? RaceStartedAt=null,string BossName="Perlenwächter",bool BossIsMimic=false,decimal BossLegendaryChance=5);

public sealed class AdventureOverlayServer
{
    private static readonly Lazy<byte[]> Artwork = new(() =>
    {
        using var stream = typeof(AdventureOverlayServer).Assembly.GetManifestResourceStream("RaidClipPlugin.Overlay.OtterEvents.png")
            ?? throw new InvalidOperationException("Die eingebettete Otter-Overlaygrafik fehlt.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });
    public const string Url = "http://127.0.0.1:18769/";
    private readonly Func<AdventureStatus> _status;
    private readonly string _url;
    private readonly Func<CommunityOverlayState>? _community;
    public AdventureOverlayServer(Func<AdventureStatus> status, int port = 18769,Func<CommunityOverlayState>? community=null)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _status = status;
        _community=community;
        _url = $"http://127.0.0.1:{port}/";
    }
    public async Task RunAsync(CancellationToken token)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(_url);
        try
        {
            listener.Start();
            Console.WriteLine("Event-Overlay bereit: " + _url);
            using var stop = token.Register(listener.Stop);
            while (!token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                try
                {
                    var path = context.Request.Url?.AbsolutePath;
                    if (context.Request.HttpMethod != "GET" || path is not ("/" or "/state" or "/community" or "/preview" or "/assets/otter-events.png"))
                    { context.Response.StatusCode = 404; continue; }
                    if (path == "/assets/otter-events.png")
                    {
                        context.Response.ContentType = "image/png";
                        context.Response.Headers["Cache-Control"] = "public, max-age=3600";
                        var image = Artwork.Value;
                        context.Response.ContentLength64 = image.Length;
                        await context.Response.OutputStream.WriteAsync(image, token);
                        continue;
                    }
                    context.Response.ContentType = path is "/state" or "/community" ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    var bytes = Encoding.UTF8.GetBytes(path switch {"/state"=>JsonSerializer.Serialize(_status()),"/community"=>JsonSerializer.Serialize(_community?.Invoke()),_=>Html});
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, token);
                }
                catch (Exception ex) when (ex is IOException or HttpListenerException) { }
                finally { context.Response.Close(); }
            }
        }
        catch (Exception ex) when (token.IsCancellationRequested && ex is HttpListenerException or ObjectDisposedException) { }
        catch (HttpListenerException ex) { Console.WriteLine("Event-Overlay nicht verfügbar (Port 18769): " + ex.Message); }
    }
    internal const string Html = HeistOverlayPage.Html;
}
