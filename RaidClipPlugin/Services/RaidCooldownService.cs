namespace RaidClipPlugin.Services;

public sealed class RaidCooldownService
{
    private readonly object _sync = new();
    private DateTimeOffset? _lastAcceptedRaid;

    public bool TryAcquire(
        TimeSpan cooldown,
        out TimeSpan remaining)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;

            if (_lastAcceptedRaid is not null)
            {
                var elapsed = now - _lastAcceptedRaid.Value;

                if (elapsed < cooldown)
                {
                    remaining = cooldown - elapsed;
                    return false;
                }
            }

            _lastAcceptedRaid = now;
            remaining = TimeSpan.Zero;
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _lastAcceptedRaid = null;
        }
    }
}
