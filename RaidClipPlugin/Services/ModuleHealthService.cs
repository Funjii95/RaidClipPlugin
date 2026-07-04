using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public enum ModuleHealthState
{
    Healthy,
    Warning,
    Failed,
    Disabled
}

public sealed record ModuleProbeResult(
    bool Enabled,
    bool Running,
    string? Error = null)
{
    public static ModuleProbeResult Healthy() => new(true, true);
    public static ModuleProbeResult Failed(string error) => new(true, false, error);
    public static ModuleProbeResult Disabled() => new(false, false);
}

public sealed record ModuleHealthStatus(
    string ModuleName,
    bool ConfiguredEnabled,
    bool IsRunning,
    DateTimeOffset LastHeartbeatUtc,
    string? LastError,
    int RestartCount,
    DateTimeOffset? LastRestartUtc,
    ModuleHealthState State);

public sealed class ModuleHealthService : IDisposable
{
    private sealed record Registration(
        string Name,
        Func<CancellationToken, Task<ModuleProbeResult>> Probe,
        Func<CancellationToken, Task>? Restart);

    private readonly ModuleHealthConfig _config;
    private readonly Dictionary<string, Registration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModuleHealthStatus> _statuses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<DateTimeOffset>> _restartAttempts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private bool _disposed;

    public event Action<IReadOnlyList<ModuleHealthStatus>>? StatusChanged;
    public DateTimeOffset? LastCheckUtc { get; private set; }

    public ModuleHealthService(ModuleHealthConfig config)
    {
        _config = config ?? new ModuleHealthConfig();
    }

    public void Register(
        string name,
        Func<CancellationToken, Task<ModuleProbeResult>> probe,
        Func<CancellationToken, Task>? restart = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _registrations[name] = new Registration(name, probe, restart);
        _statuses[name] = new ModuleHealthStatus(
            name, false, false, DateTimeOffset.MinValue,
            null, 0, null, ModuleHealthState.Disabled);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Modul-Healthcheck gestartet.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckNowAsync(cancellationToken);
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(
                        _config.IntervalSeconds, 5, 600)),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Console.WriteLine("Modul-Healthcheck gestoppt.");
        }
    }

    public async Task<IReadOnlyList<ModuleHealthStatus>> CheckNowAsync(
        CancellationToken cancellationToken)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var registration in _registrations.Values)
            {
                await CheckRegistrationAsync(registration, cancellationToken);
            }
            LastCheckUtc = DateTimeOffset.UtcNow;
            var snapshot = Snapshot();
            StatusChanged?.Invoke(snapshot);
            return snapshot;
        }
        finally { _checkLock.Release(); }
    }

    public async Task<bool> RestartModuleAsync(
        string moduleName,
        CancellationToken cancellationToken)
    {
        if (!_registrations.TryGetValue(moduleName, out var registration) ||
            registration.Restart is null)
            return false;

        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            return await TryRestartAsync(
                registration, DateTimeOffset.UtcNow, cancellationToken, true);
        }
        finally { _checkLock.Release(); }
    }

    public IReadOnlyList<ModuleHealthStatus> Snapshot() =>
        _statuses.Values.OrderBy(status => status.ModuleName).ToArray();

    private async Task CheckRegistrationAsync(
        Registration registration,
        CancellationToken cancellationToken)
    {
        ModuleProbeResult result;
        try
        {
            result = await registration.Probe(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = ModuleProbeResult.Failed(exception.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var previous = _statuses[registration.Name];
        var state = !result.Enabled
            ? ModuleHealthState.Disabled
            : result.Running
                ? ModuleHealthState.Healthy
                : ModuleHealthState.Failed;
        _statuses[registration.Name] = previous with
        {
            ConfiguredEnabled = result.Enabled,
            IsRunning = result.Running,
            LastHeartbeatUtc = result.Running ? now : previous.LastHeartbeatUtc,
            LastError = result.Error,
            State = state
        };

        if (result.Enabled && !result.Running &&
            _config.AutoRestartEnabled && registration.Restart is not null)
        {
            Console.WriteLine(
                $"Healthcheck erkennt Problem bei {registration.Name}: " +
                (result.Error ?? "Modul läuft nicht"));
            await TryRestartAsync(registration, now, cancellationToken, false);
        }
    }

    private async Task<bool> TryRestartAsync(
        Registration registration,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool manual)
    {
        var status = _statuses[registration.Name];
        var cooldown = TimeSpan.FromSeconds(Math.Clamp(
            _config.RestartCooldownSeconds, 5, 3600));
        if (!manual && status.LastRestartUtc is { } last && now - last < cooldown)
            return false;

        if (!_restartAttempts.TryGetValue(registration.Name, out var attempts))
        {
            attempts = new Queue<DateTimeOffset>();
            _restartAttempts[registration.Name] = attempts;
        }
        var window = TimeSpan.FromMinutes(Math.Clamp(
            _config.RestartWindowMinutes, 1, 60));
        while (attempts.Count > 0 && now - attempts.Peek() > window)
            attempts.Dequeue();
        if (!manual && attempts.Count >= Math.Clamp(
                _config.MaxRestartAttempts, 1, 20))
        {
            _statuses[registration.Name] = status with
            {
                State = ModuleHealthState.Failed,
                LastError = "Neustartlimit erreicht"
            };
            Console.WriteLine(
                $"Auto-Restart für {registration.Name} gestoppt: Neustartlimit erreicht.");
            return false;
        }

        attempts.Enqueue(now);
        Console.WriteLine($"Auto-Restart gestartet: {registration.Name}.");
        try
        {
            await registration.Restart!(cancellationToken);
            var restartCount = status.RestartCount >= int.MaxValue
                ? int.MaxValue : status.RestartCount + 1;
            _statuses[registration.Name] = status with
            {
                RestartCount = restartCount,
                LastRestartUtc = now,
                LastError = null,
                IsRunning = true,
                State = ModuleHealthState.Healthy,
                LastHeartbeatUtc = DateTimeOffset.UtcNow
            };
            Console.WriteLine($"Auto-Restart erfolgreich: {registration.Name}.");
            StatusChanged?.Invoke(Snapshot());
            return true;
        }
        catch (Exception exception)
        {
            _statuses[registration.Name] = status with
            {
                LastRestartUtc = now,
                LastError = exception.Message,
                State = ModuleHealthState.Failed
            };
            Console.WriteLine(
                $"Auto-Restart fehlgeschlagen: {registration.Name}: {exception}");
            StatusChanged?.Invoke(Snapshot());
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _checkLock.Dispose();
        _disposed = true;
    }
}
