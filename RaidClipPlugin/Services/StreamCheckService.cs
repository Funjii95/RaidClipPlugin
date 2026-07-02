using System.Diagnostics;

namespace RaidClipPlugin.Services;

public enum StreamCheckSeverity
{
    NotChecked,
    Running,
    Success,
    Warning,
    Error,
    Skipped
}

public sealed record StreamCheckResult(
    string Key,
    string Name,
    StreamCheckSeverity Severity,
    string Description,
    string ErrorReason = "",
    string FixAction = "",
    TimeSpan Duration = default)
{
    public bool IsFailure => Severity == StreamCheckSeverity.Error;
}

public interface IStreamReadinessCheck
{
    string Key { get; }
    string Name { get; }
    bool Critical { get; }
    Task<StreamCheckResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class DelegateStreamReadinessCheck : IStreamReadinessCheck
{
    private readonly Func<CancellationToken, Task<StreamCheckResult>> _run;
    public string Key { get; }
    public string Name { get; }
    public bool Critical { get; }

    public DelegateStreamReadinessCheck(
        string key, string name, bool critical,
        Func<CancellationToken, Task<StreamCheckResult>> run)
    {
        Key = key;
        Name = name;
        Critical = critical;
        _run = run;
    }

    public Task<StreamCheckResult> RunAsync(
        CancellationToken cancellationToken) => _run(cancellationToken);
}

public sealed class StreamCheckService
{
    private readonly IReadOnlyList<IStreamReadinessCheck> _checks;

    public StreamCheckService(IEnumerable<IStreamReadinessCheck> checks) =>
        _checks = checks.ToArray();

    public async Task<IReadOnlyList<StreamCheckResult>> RunAsync(
        IReadOnlySet<string> disabledChecks,
        IReadOnlySet<string>? onlyKeys,
        IProgress<StreamCheckResult>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<StreamCheckResult>();
        foreach (var check in _checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (onlyKeys is not null && !onlyKeys.Contains(check.Key)) continue;
            if (disabledChecks.Contains(check.Key))
            {
                var skipped = new StreamCheckResult(check.Key, check.Name,
                    StreamCheckSeverity.Skipped, "Diese Prüfung ist im Profil deaktiviert.");
                results.Add(skipped);
                progress?.Report(skipped);
                continue;
            }

            progress?.Report(new StreamCheckResult(check.Key, check.Name,
                StreamCheckSeverity.Running, "Prüfung läuft …"));
            var stopwatch = Stopwatch.StartNew();
            StreamCheckResult result;
            try
            {
                result = await check.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = new StreamCheckResult(check.Key, check.Name,
                    check.Critical ? StreamCheckSeverity.Error : StreamCheckSeverity.Warning,
                    "Die Prüfung konnte nicht abgeschlossen werden.",
                    exception.Message);
            }
            stopwatch.Stop();
            result = result with { Duration = stopwatch.Elapsed };
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    public static string CreateSummary(IReadOnlyCollection<StreamCheckResult> results)
    {
        var errors = results.Count(result => result.Severity == StreamCheckSeverity.Error);
        var warnings = results.Count(result => result.Severity == StreamCheckSeverity.Warning);
        var successful = results.Count(result => result.Severity == StreamCheckSeverity.Success);
        return errors > 0
            ? $"Nicht streambereit – {errors} kritische Fehler gefunden."
            : $"Streambereit – {successful} von {results.Count} Prüfungen erfolgreich, {warnings} Warnungen.";
    }
}
