using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class ModuleHealthServiceTests
{
    [Fact]
    public async Task FailedModuleIsRestartedAndBecomesHealthy()
    {
        var running = false;
        var restartCalls = 0;
        using var service = new ModuleHealthService(new ModuleHealthConfig
        {
            AutoRestartEnabled = true,
            MaxRestartAttempts = 3,
            RestartCooldownSeconds = 5
        });
        service.Register("Minigame",
            _ => Task.FromResult(new ModuleProbeResult(true, running,
                running ? null : "gestoppt")),
            _ =>
            {
                restartCalls++;
                running = true;
                return Task.CompletedTask;
            });

        IReadOnlyList<ModuleHealthStatus>? statuses = null;
        service.StatusChanged += current => statuses = current;

        await service.CheckNowAsync(CancellationToken.None);

        Assert.NotNull(statuses);
        var status = Assert.Single(statuses!);
        Assert.Equal(1, restartCalls);
        Assert.Equal(ModuleHealthState.Healthy, status.State);
        Assert.Null(status.LastError);
        Assert.Equal(1, status.RestartAttempts);
    }

    [Fact]
    public async Task RestartCooldownPreventsRestartLoop()
    {
        var restartCalls = 0;
        using var service = new ModuleHealthService(new ModuleHealthConfig
        {
            AutoRestartEnabled = true,
            MaxRestartAttempts = 3,
            RestartCooldownSeconds = 60
        });
        service.Register("Minigame",
            _ => Task.FromResult(ModuleProbeResult.Failed("weiterhin gestoppt")),
            _ => { restartCalls++; return Task.CompletedTask; });

        await service.CheckNowAsync(CancellationToken.None);
        await service.CheckNowAsync(CancellationToken.None);

        Assert.Equal(1, restartCalls);
    }

    [Theory]
    [InlineData("!gamble", "!gamble")]
    [InlineData("!GAMBEL", "!gamble")]
    [InlineData(" !gambel ", "!gamble")]
    public void GambleTypoAliasIsNormalized(string input, string expected)
    {
        Assert.Equal(expected,
            ChatMinigameService.NormalizeIncomingCommand(input));
        Assert.True(ChatMinigameService.IsGameCommand(expected));
    }

    [Fact]
    public void HealthDefaultsAreSafeForExistingConfigurations()
    {
        var config = new ModuleHealthConfig();
        Assert.True(config.Enabled);
        Assert.True(config.AutoRestartEnabled);
        Assert.True(config.GambleHealthcheckEnabled);
        Assert.Equal(30, config.IntervalSeconds);
        Assert.Equal(3, config.MaxRestartAttempts);
    }
}
