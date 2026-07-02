using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class StreamCheckServiceTests
{
    [Fact]
    public async Task SuccessfulCheckIsReported()
    {
        var service = Service(StreamCheckSeverity.Success);
        var results = await service.RunAsync(new HashSet<string>(), null, null, CancellationToken.None);
        Assert.Single(results);
        Assert.Equal(StreamCheckSeverity.Success, results[0].Severity);
        Assert.StartsWith("Streambereit", StreamCheckService.CreateSummary(results));
    }

    [Fact]
    public async Task DisabledCheckIsSkippedWithoutExecution()
    {
        var executed = false;
        var check = new DelegateStreamReadinessCheck("test", "Test", true, _ =>
        {
            executed = true;
            return Task.FromResult(Result(StreamCheckSeverity.Success));
        });
        var service = new StreamCheckService(new[] { check });
        var results = await service.RunAsync(new HashSet<string> { "test" }, null, null, CancellationToken.None);
        Assert.False(executed);
        Assert.Equal(StreamCheckSeverity.Skipped, results[0].Severity);
    }

    [Fact]
    public async Task CriticalExceptionBecomesError()
    {
        var check = new DelegateStreamReadinessCheck("test", "Test", true,
            _ => throw new InvalidOperationException("Verbindung fehlt"));
        var results = await new StreamCheckService(new[] { check })
            .RunAsync(new HashSet<string>(), null, null, CancellationToken.None);
        Assert.Equal(StreamCheckSeverity.Error, results[0].Severity);
        Assert.Contains("Verbindung fehlt", results[0].ErrorReason);
        Assert.StartsWith("Nicht streambereit", StreamCheckService.CreateSummary(results));
    }

    [Fact]
    public async Task NonCriticalExceptionBecomesWarning()
    {
        var check = new DelegateStreamReadinessCheck("test", "Test", false,
            _ => throw new HttpRequestException("offline"));
        var results = await new StreamCheckService(new[] { check })
            .RunAsync(new HashSet<string>(), null, null, CancellationToken.None);
        Assert.Equal(StreamCheckSeverity.Warning, results[0].Severity);
    }

    private static StreamCheckService Service(StreamCheckSeverity severity) =>
        new(new[] { new DelegateStreamReadinessCheck("test", "Test", true,
            _ => Task.FromResult(Result(severity))) });

    private static StreamCheckResult Result(StreamCheckSeverity severity) =>
        new("test", "Test", severity, "Ergebnis");
}
