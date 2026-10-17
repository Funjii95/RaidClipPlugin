using RaidClipPlugin.Services;

// Standalone loopback preview. No stores, Twitch clients or OBS connections are constructed.
using var stop=new CancellationTokenSource();
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();};
Console.WriteLine("Mock-Vorschau: http://127.0.0.1:18879/preview – Strg+C beendet die Vorschau.");
await new AdventureOverlayServer(()=>new(),18879,()=>new CommunityOverlayEventManager().Read(new(),DateTimeOffset.UtcNow)).RunAsync(stop.Token);
