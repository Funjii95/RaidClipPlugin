namespace RaidClipPlugin.Services;

public sealed class GameplayUnavailableException : InvalidOperationException
{
    public GameplayUnavailableException() : base("Plugin connection or RPG readiness is unavailable for this session.") { }
}

// Historical type name retained. No stream status: callback checks authenticated chat connection,
// enabled RPG/module and a fresh local heartbeat; epoch and commit deadlines fence stale work.
public sealed class StreamAdmission
{
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly AsyncLocal<Guid?> ambient = new();
    private readonly AsyncLocal<(long Start,TimeSpan Window)?> executionWindow=new();
    private readonly AsyncLocal<DateTimeOffset?> executionDeadline=new();
    private readonly AsyncLocal<bool> rollback = new();
    private Guid epoch;
    private DateTimeOffset started;
    private Func<bool> ready = () => false;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(20);
    public StreamAdmission(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;

    public Guid Begin(Func<bool> connectedAndReady)
    {
        lock (gate)
        {
            epoch=Guid.NewGuid();
            started=clock.GetUtcNow(); ready=connectedAndReady; return epoch;
        }
    }
    public void End() { lock(gate) { epoch=Guid.Empty; ready=()=>false; } }
    public bool CanPlay(Guid expected, DateTimeOffset? receivedAt = null)
    {
        lock(gate) return Allowed(expected) && (receivedAt is null ||
            receivedAt >= started && receivedAt <= clock.GetUtcNow().AddSeconds(5) &&
            clock.GetUtcNow()-receivedAt <= MaximumAge);
    }
    private bool Allowed(Guid expected) => expected != Guid.Empty && expected==epoch && ready() &&
        (executionDeadline.Value is not {} deadline || clock.GetUtcNow()<deadline) &&
        (executionWindow.Value is not {} limit || clock.GetElapsedTime(limit.Start)<limit.Window);
    public IDisposable Enter(Guid expected)
    {
        var previous=ambient.Value; ambient.Value=expected;
        return new Scope(()=>ambient.Value=previous);
    }
    internal IDisposable EnterRemote(Guid expected,DateTimeOffset deadline,TimeSpan window)
    {
        var previousDeadline=executionDeadline.Value;var previousWindow=executionWindow.Value;
        var scope=Enter(expected);executionDeadline.Value=deadline;executionWindow.Value=(clock.GetTimestamp(),window);
        return new Scope(()=>{executionDeadline.Value=previousDeadline;executionWindow.Value=previousWindow;scope.Dispose();});
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
