using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public enum ModuleHealthState
{
    Disabled,
    Healthy,
    Warning,
    Failed
}

public sealed record ModuleProbeResult(
    bool IsRelevant,
    bool IsHealthy,
    string? Message = null,
    ModuleHealthState? State = null)
{
    public static ModuleProbeResult Healthy(string? message = null) =>
        new(true, true, message, ModuleHealthState.Healthy);

    public static ModuleProbeResult Warning(string message) =>
        new(true, false, message, ModuleHealthState.Warning);

    public static ModuleProbeResult Failed(string message) =>
        new(true, false, message, ModuleHealthState.Failed);

    public static ModuleProbeResult Disabled(string? message = null) =>
        new(false, true, message, ModuleHealthState.Disabled);
}

public sealed record ModuleHealthStatus(
    string ModuleName,
    ModuleHealthState State,
    string? LastError,
    DateTimeOffset CheckedAtUtc,
    int RestartAttempts,
    DateTimeOffset? LastRestartUtc);

public sealed class ModuleHealthService : IDisposable
{
    private ModuleHealthConfig _config;
    private readonly Dictionary<string, ModuleRegistration> _modules =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly object _statusLock = new();
    private readonly Dictionary<string, ModuleHealthStatus> _statuses =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ModuleHealthService(ModuleHealthConfig config)
    {
        _config = config;
    }

    public event Action<IReadOnlyList<ModuleHealthStatus>>? StatusChanged;
    public event Action<string>? LogMessage;

    public void UpdateConfig(ModuleHealthConfig config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _config = config;
    }

    public void Register(
        string moduleName,
        Func<CancellationToken, Task<ModuleProbeResult>> probe,
        Func<CancellationToken, Task>? restart = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(probe);

        _modules[moduleName] = new ModuleRegistration(moduleName, probe, restart);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_config.Enabled)
        {
            PublishStatuses(Array.Empty<ModuleHealthStatus>());
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckNowAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Log("Healthcheck-Durchlauf fehlgeschlagen: " + exception.Message);
            }

            var delay = TimeSpan.FromSeconds(Math.Clamp(
                _config.IntervalSeconds, 5, 600));
            await Task.Delay(delay, cancellationToken);
        }
    }

    public async Task CheckNowAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            var results = new List<ModuleHealthStatus>();
            foreach (var module in _modules.Values.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var status = await ProbeModuleAsync(module, cancellationToken);
                results.Add(status);
            }

            PublishStatuses(results);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public async Task<bool> RestartModuleAsync(
        string moduleName,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_modules.TryGetValue(moduleName, out var module) ||
            module.Restart is null)
        {
            return false;
        }

        await module.Restart(cancellationToken);
        module.LastRestartUtc = DateTimeOffset.UtcNow;
        module.RestartAttempts++;
        Log($"Modul '{module.Name}' wurde neu gestartet.");
        return true;
    }

    public async Task<int> RestartFailedModulesAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ModuleHealthStatus[] failed;
        lock (_statusLock)
        {
            failed = _statuses.Values
                .Where(status => status.State == ModuleHealthState.Failed)
                .ToArray();
        }

        var restarted = 0;
        foreach (var status in failed)
        {
            if (await RestartModuleAsync(status.ModuleName, cancellationToken))
            {
                restarted++;
            }
        }

        return restarted;
    }

    private async Task<ModuleHealthStatus> ProbeModuleAsync(
        ModuleRegistration module,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        try
        {
            var probeTimeoutSeconds = Math.Clamp(
                Math.Min(20, Math.Max(5, _config.IntervalSeconds / 2)),
                5,
                20);
            using var timeout = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(probeTimeoutSeconds));

            var result = await module.Probe(timeout.Token);
            var state = result.State ?? (
                !result.IsRelevant
                    ? ModuleHealthState.Disabled
                    : result.IsHealthy
                        ? ModuleHealthState.Healthy
                        : ModuleHealthState.Failed);

            var status = new ModuleHealthStatus(
                module.Name,
                state,
                state is ModuleHealthState.Healthy or ModuleHealthState.Disabled
                    ? null
                    : result.Message,
                checkedAt,
                module.RestartAttempts,
                module.LastRestartUtc);

            if (state == ModuleHealthState.Failed)
            {
                await TryAutoRestartAsync(module, result.Message, cancellationToken);
            }

            return status;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            await TryAutoRestartAsync(
                module,
                "Healthcheck-Timeout",
                cancellationToken);
            return new ModuleHealthStatus(
                module.Name,
                ModuleHealthState.Failed,
                "Healthcheck-Timeout",
                checkedAt,
                module.RestartAttempts,
                module.LastRestartUtc);
        }
        catch (Exception exception)
        {
            await TryAutoRestartAsync(module, exception.Message, cancellationToken);
            return new ModuleHealthStatus(
                module.Name,
                ModuleHealthState.Failed,
                exception.Message,
                checkedAt,
                module.RestartAttempts,
                module.LastRestartUtc);
        }
    }

    private async Task TryAutoRestartAsync(
        ModuleRegistration module,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!_config.AutoRestartEnabled || module.Restart is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (module.RestartWindowStartedUtc is null ||
            now - module.RestartWindowStartedUtc >
            TimeSpan.FromMinutes(_config.RestartWindowMinutes))
        {
            module.RestartWindowStartedUtc = now;
            module.RestartAttempts = 0;
        }

        if (module.RestartAttempts >= _config.MaxRestartAttempts)
        {
            Log($"Auto-Recovery für '{module.Name}' übersprungen: Neustartlimit erreicht.");
            return;
        }

        if (module.LastRestartUtc is not null &&
            now - module.LastRestartUtc <
            TimeSpan.FromSeconds(_config.RestartCooldownSeconds))
        {
            return;
        }

        try
        {
            Log($"Auto-Recovery startet '{module.Name}' neu. Grund: {reason}");
            await module.Restart(cancellationToken);
            module.RestartAttempts++;
            module.LastRestartUtc = DateTimeOffset.UtcNow;
            Log($"Auto-Recovery für '{module.Name}' abgeschlossen.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log($"Auto-Recovery für '{module.Name}' fehlgeschlagen: {exception.Message}");
        }
    }

    private void PublishStatuses(IReadOnlyList<ModuleHealthStatus> statuses)
    {
        lock (_statusLock)
        {
            _statuses.Clear();
            foreach (var status in statuses)
            {
                _statuses[status.ModuleName] = status;
            }
        }

        StatusChanged?.Invoke(statuses);
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _checkLock.Dispose();
    }

    private sealed class ModuleRegistration
    {
        public ModuleRegistration(
            string name,
            Func<CancellationToken, Task<ModuleProbeResult>> probe,
            Func<CancellationToken, Task>? restart)
        {
            Name = name;
            Probe = probe;
            Restart = restart;
        }

        public string Name { get; }
        public Func<CancellationToken, Task<ModuleProbeResult>> Probe { get; }
        public Func<CancellationToken, Task>? Restart { get; }
        public int RestartAttempts { get; set; }
        public DateTimeOffset? RestartWindowStartedUtc { get; set; }
        public DateTimeOffset? LastRestartUtc { get; set; }
    }
}
