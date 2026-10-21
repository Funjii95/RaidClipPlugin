namespace RaidClipPlugin.Services;

/// <summary>Queues OBS work away from game transactions; repeated status updates never reload twice.</summary>
public sealed class CommunityOverlayReloadService(Func<int> refresh,Action<string> log)
{
    private readonly object _gate=new();
    private readonly HashSet<string> _seen=new();
    private readonly Queue<string> _order=new();
    private Task _pending=Task.CompletedTask;
    public Task RequestAsync(CommunityEventType type,string eventId)
    {
        if(string.IsNullOrWhiteSpace(eventId))return Task.CompletedTask;
        lock(_gate)
        {
            var key=type+":"+eventId;
            if(!_seen.Add(key))return _pending;
            _order.Enqueue(key);while(_order.Count>256)_seen.Remove(_order.Dequeue());
            _pending=_pending.ContinueWith(_=>
            {
                try
                {
                    var count=refresh();
                    log(count>0?$"OBS: {count} Event-Browserquelle(n) für {type} neu geladen.":"OBS: Keine passende Event-Browserquelle gefunden. Unter Heist → OBS-Overlay auswählen oder URL prüfen.");
                }
                catch(Exception ex)
                {
                    // An unavailable overlay must never stop a game or roll back a payout.
                    try{log("OBS-Browserquelle konnte nicht neu geladen werden; das Event läuft weiter: "+ex.Message);}catch{}
                }
            },CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);
            return _pending;
        }
    }
}
