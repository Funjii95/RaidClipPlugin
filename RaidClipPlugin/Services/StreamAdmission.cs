namespace RaidClipPlugin.Services;

public sealed class GameplayUnavailableException : InvalidOperationException
{
    public GameplayUnavailableException() : base("Twitch has not freshly confirmed live, connected, gameplay-ready status for this session.") { }
}

public sealed class StreamAdmission
{
    public readonly record struct Check(Guid Epoch, long Sequence, long Issued);
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly AsyncLocal<Guid?> ambient = new();
    private readonly AsyncLocal<bool> rollback = new();
    private Guid epoch;
    private long sequence, applied, confirmed;
    private DateTimeOffset started;
    private string? streamId;
    private bool live;
    private Func<bool> ready = () => false;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(20);
    public StreamAdmission(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;

    public Guid Begin(Func<bool> connectedAndReady)
    {
        lock (gate)
        {
            epoch=Guid.NewGuid(); sequence=applied=0; live=false; streamId=null;
            started=clock.GetUtcNow(); ready=connectedAndReady; return epoch;
        }
    }
    public void End() { lock(gate) { epoch=Guid.Empty; live=false; ready=()=>false; } }
    public Check StartCheck(Guid expected)
    {
        lock(gate) return new(expected, ++sequence, clock.GetTimestamp());
    }
    public void Observe(Check check, string? confirmedStreamId)
    {
        lock(gate)
        {
            if(check.Epoch != epoch || epoch == Guid.Empty || check.Sequence <= applied) return;
            applied=check.Sequence;
            if(clock.GetElapsedTime(check.Issued) > MaximumAge || string.IsNullOrWhiteSpace(confirmedStreamId))
            { live=false; return; }
            // A new broadcast invalidates queued messages from the previous stream.
            if(streamId is not null && streamId != confirmedStreamId) {epoch=Guid.Empty;live=false;return;}
            streamId=confirmedStreamId; confirmed=check.Issued; live=true;
        }
    }
    public bool CanPlay(Guid expected, DateTimeOffset? receivedAt = null)
    {
        lock(gate) return Allowed(expected) && (receivedAt is null ||
            receivedAt >= started && receivedAt <= clock.GetUtcNow().AddSeconds(5) &&
            clock.GetUtcNow()-receivedAt <= MaximumAge);
    }
    private bool Allowed(Guid expected) => expected != Guid.Empty && expected==epoch && live &&
        clock.GetElapsedTime(confirmed) <= MaximumAge && ready();
    public IDisposable Enter(Guid expected)
    {
        var previous=ambient.Value; ambient.Value=expected;
        return new Scope(()=>ambient.Value=previous);
    }
    internal IDisposable EnterOperation()
    {
        lock(gate)return Enter(ambient.Value??epoch);
    }
    // Technical reversal of an already reserved stake, never a new play or reward.
    internal IDisposable EnterRollback()
    {
        var previous=rollback.Value;rollback.Value=true;return new Scope(()=>rollback.Value=previous);
    }
    public void Commit(Action commit)
    {
        lock(gate)
        {
            if(!rollback.Value && !Allowed(ambient.Value ?? epoch)) throw new GameplayUnavailableException();
            commit();
        }
    }
    private sealed class Scope(Action release) : IDisposable { public void Dispose()=>release(); }
}
