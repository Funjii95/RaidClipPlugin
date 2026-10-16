using System.Net;
using System.Text;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed class WheelOverlayServer
{
    public const string Url = "http://127.0.0.1:18770/";
    private readonly WheelService _wheel;
    private readonly int _port;
    public WheelOverlayServer(WheelService wheel, int port = 18770) { _wheel = wheel; _port = port; }
    public Task RunAsync(CancellationToken token)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        try { listener.Start(); } catch { listener.Close(); throw; }
        return ServeAsync(listener, token);
    }
    private async Task ServeAsync(HttpListener listener, CancellationToken token)
    {
        using (listener)
        using (token.Register(listener.Stop))
        try
        {
            while (!token.IsCancellationRequested)
            {
                var request = await listener.GetContextAsync();
                try
                {
                    var path = request.Request.Url?.AbsolutePath;
                    if (request.Request.HttpMethod != "GET" || path is not ("/" or "/state" or "/design")) { request.Response.StatusCode = 404; continue; }
                    if (path == "/design")
                    {
                        var design = _wheel.Design;
                        if (design is null) { request.Response.StatusCode = 404; continue; }
                        request.Response.ContentType = design.ContentType;
                        request.Response.Headers["Cache-Control"] = "no-store";
                        request.Response.ContentLength64 = design.Bytes.Length;
                        await request.Response.OutputStream.WriteAsync(design.Bytes, token);
                        continue;
                    }
                    request.Response.ContentType = path == "/state" ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
                    request.Response.Headers["Cache-Control"] = "no-store";
                    var content = Encoding.UTF8.GetBytes(path == "/state" ? JsonSerializer.Serialize(_wheel.State) : Html);
                    request.Response.ContentLength64 = content.Length;
                    await request.Response.OutputStream.WriteAsync(content, token);
                }
                catch (Exception ex) when (ex is IOException or HttpListenerException) { }
                finally { request.Response.Close(); }
            }
        }
        catch (Exception ex) when (token.IsCancellationRequested && ex is HttpListenerException or ObjectDisposedException) { }
    }
    internal const string Html = """
        <!doctype html><html lang="de"><meta charset="utf-8"><title>RaidClip Glücksrad</title>
        <style>html,body{margin:0;background:transparent;color:white;font:24px 'Segoe UI',sans-serif}main{display:none;width:700px;text-align:center;padding:15px;box-sizing:border-box}canvas{width:620px;height:620px;filter:drop-shadow(0 10px 16px #0008)}#result{background:#141220eF;border:1px solid #aa8dee;border-radius:18px;padding:15px;overflow-wrap:anywhere;min-height:35px}</style>
        <main id="wheel"><canvas id="disk" width="700" height="700"></canvas><div id="result"></div></main>
        <script>
        const canvas=document.getElementById('disk'),ctx=canvas.getContext('2d'),root=document.getElementById('wheel'),label=document.getElementById('result');let s=null,design=null,designId='';
        function draw(){ctx.clearRect(0,0,700,700);if(!s||!s.Entries.length){root.style.display='none';requestAnimationFrame(draw);return;}root.style.display='block';const start=Date.parse(s.StartedAt),end=Date.parse(s.EndsAt),p=Math.max(0,Math.min(1,(Date.now()-start)/(end-start))),n=s.Entries.length,step=2*Math.PI/n,angle=(6*2*Math.PI-(s.WinnerIndex+.5)*step)*(1-Math.pow(1-p,4));ctx.save();ctx.translate(350,350);ctx.rotate(angle);const custom=design&&design.complete&&design.naturalWidth>0;if(custom){ctx.save();ctx.beginPath();ctx.arc(0,0,310,0,Math.PI*2);ctx.clip();const k=Math.max(620/design.naturalWidth,620/design.naturalHeight);ctx.drawImage(design,-design.naturalWidth*k/2,-design.naturalHeight*k/2,design.naturalWidth*k,design.naturalHeight*k);ctx.restore();}for(let i=0;i<n;i++){let a=-Math.PI/2+i*step;ctx.beginPath();ctx.moveTo(0,0);ctx.arc(0,0,310,a,a+step);ctx.closePath();ctx.fillStyle=`hsl(${(i*137.508)%360} 65% 39%)`;ctx.globalAlpha=custom?.3:1;ctx.fill();ctx.globalAlpha=1;ctx.strokeStyle='#ffffff55';ctx.stroke();ctx.save();ctx.rotate(a+step/2);ctx.fillStyle='white';ctx.textAlign='right';ctx.font=`bold ${n>30?12:18}px sans-serif`;const t=s.Entries[i];ctx.fillText(t.length>25?t.slice(0,22)+'…':t,290,5);ctx.restore();}ctx.restore();ctx.beginPath();ctx.moveTo(350,61);ctx.lineTo(333,20);ctx.lineTo(367,20);ctx.closePath();ctx.fillStyle='#fff2b0';ctx.fill();label.textContent=p<1?'Das Rad dreht sich …':'Ergebnis: '+s.Entries[s.WinnerIndex];requestAnimationFrame(draw);}
        async function poll(){try{const r=await fetch('/state',{cache:'no-store'});if(!r.ok)throw Error();const next=await r.json();if(next.SpinId!==designId){designId=next.SpinId;design=null;if(next.HasDesign){design=new Image();design.src='/design?id='+encodeURIComponent(designId);}}s=next;}catch{s=null;}setTimeout(poll,300);}poll();draw();
        </script></html>
        """;
}
