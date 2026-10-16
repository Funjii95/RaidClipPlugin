using System.Net;
using System.Text;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed record AdventureStatus(bool BossActive = false, int BossHp = 0, int BossMaxHp = 0,
    int BossPlayers = 0, DateTimeOffset? BossEndsAt = null, bool RaceActive = false,
    int RacePlayers = 0, long RaceStake = 0, DateTimeOffset? RaceEndsAt = null);

public sealed class AdventureOverlayServer
{
    public const string Url = "http://127.0.0.1:18769/";
    private readonly Func<AdventureStatus> _status;
    public AdventureOverlayServer(Func<AdventureStatus> status) => _status = status;
    public async Task RunAsync(CancellationToken token)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(Url);
        try
        {
            listener.Start();
            Console.WriteLine("Event-Overlay bereit: " + Url);
            using var stop = token.Register(listener.Stop);
            while (!token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                try
                {
                    var path = context.Request.Url?.AbsolutePath;
                    if (context.Request.HttpMethod != "GET" || path is not ("/" or "/state"))
                    { context.Response.StatusCode = 404; continue; }
                    context.Response.ContentType = path == "/state" ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    var bytes = Encoding.UTF8.GetBytes(path == "/state" ? JsonSerializer.Serialize(_status()) : Html);
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
    internal const string Html = """
        <!doctype html><html lang="de"><meta charset="utf-8"><title>RaidClip Events</title>
        <style>html,body{margin:0;background:transparent;color:#fff;font:24px 'Segoe UI',sans-serif}main{width:560px;padding:20px;box-sizing:border-box}.card{background:linear-gradient(130deg,#151321ed,#25203ded);border:1px solid #8575da;border-radius:20px;padding:24px;margin-bottom:16px;box-shadow:0 10px 30px #0006}h2{margin:0 0 15px;font-size:30px}progress{width:100%;height:24px;accent-color:#ae78ff}.meta{display:flex;justify-content:space-between;margin-top:12px;color:#ddd5f4}.hidden{display:none}</style>
        <main><section id="boss" class="card hidden"><h2>⚔ Bosskampf</h2><progress id="health"></progress><div class="meta"><span id="hp"></span><span id="bossTime"></span></div><p id="fighters"></p><small>Angreifen: !joinboss</small></section><section id="race" class="card hidden"><h2>🏁 Rennen</h2><div class="meta"><span id="racers"></span><span id="raceTime"></span></div><p id="stake"></p><small>Mitmachen: !joinrace</small></section></main>
        <script>const el=id=>document.getElementById(id);let state={};const seconds=end=>Math.max(0,Math.ceil((Date.parse(end)-Date.now())/1000));function render(){el('boss').classList.toggle('hidden',!state.BossActive);el('race').classList.toggle('hidden',!state.RaceActive);el('health').max=state.BossMaxHp||1;el('health').value=state.BossHp||0;el('hp').textContent=`${state.BossHp||0} / ${state.BossMaxHp||0} LP`;el('fighters').textContent=`${state.BossPlayers||0} Kämpfer`;el('bossTime').textContent=`${seconds(state.BossEndsAt)||0}s`;el('racers').textContent=`${state.RacePlayers||0} Teilnehmer`;el('raceTime').textContent=`Start in ${seconds(state.RaceEndsAt)||0}s`;el('stake').textContent=`Einsatz: ${state.RaceStake||0}`;}async function poll(){try{const r=await fetch('/state',{cache:'no-store'});if(!r.ok)throw Error();state=await r.json();render();}catch{state={};render();}setTimeout(poll,1000);}setInterval(render,250);poll();</script></html>
        """;
}
