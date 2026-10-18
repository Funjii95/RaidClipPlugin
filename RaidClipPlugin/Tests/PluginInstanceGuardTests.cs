using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class PluginInstanceGuardTests
{
    [Fact] public void Regular_executable_must_not_block_its_own_start() =>
        Assert.False(PluginInstanceGuard.IncludesAnotherInstance([123], 123));
    [Fact] public void An_additional_normal_plugin_still_blocks_the_test_connection() =>
        Assert.True(PluginInstanceGuard.IncludesAnotherInstance([123,456], 123));
    [Fact] public void Renamed_test_executable_still_detects_the_normal_plugin() =>
        Assert.True(PluginInstanceGuard.IncludesAnotherInstance([456],123));
}
