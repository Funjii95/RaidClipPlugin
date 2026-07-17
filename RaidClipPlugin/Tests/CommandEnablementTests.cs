using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CommandEnablementTests
{
    [Fact]
    public void Commands_are_enabled_by_default_when_no_override_exists()
    {
        var config = new AppConfig();
        var registry = new CommandRegistry();

        registry.Update(config);

        Assert.True(registry.IsCommandEnabledForMessage("!punkte"));
    }

    [Fact]
    public void Disabled_command_override_blocks_primary_command()
    {
        var config = new AppConfig();
        config.Commands.CommandEnabledOverrides["points.de"] = false;
        var registry = new CommandRegistry();

        registry.Update(config);

        Assert.False(registry.IsCommandEnabledForMessage("!punkte"));
    }

    [Fact]
    public void Disabled_command_override_blocks_alias()
    {
        var config = new AppConfig();
        config.Minigame.Enabled = true;
        config.Minigame.GambleEnabled = true;
        config.Commands.CommandEnabledOverrides["casino.gamble"] = false;
        var registry = new CommandRegistry();

        registry.Update(config);

        Assert.False(registry.IsCommandEnabledForMessage("!gambel 100"));
    }

    [Fact]
    public void Disabling_one_command_does_not_disable_other_commands()
    {
        var config = new AppConfig();
        config.Commands.CommandEnabledOverrides["points.de"] = false;
        var registry = new CommandRegistry();

        registry.Update(config);

        Assert.False(registry.IsCommandEnabledForMessage("!punkte"));
        Assert.True(registry.IsCommandEnabledForMessage("!daily"));
    }

    [Fact]
    public void Custom_commands_can_be_disabled_through_global_overview()
    {
        var config = new AppConfig();
        var custom = new CustomChatCommandConfig
        {
            Id = "raid",
            Enabled = true,
            Command = "!raid",
            Response = "Raid!"
        };
        config.Commands.CustomCommands = new List<CustomChatCommandConfig> { custom };
        config.Commands.CommandEnabledOverrides["custom.raid"] = false;
        var registry = new CommandRegistry();

        registry.Update(config);

        Assert.False(registry.IsCommandEnabledForMessage("!raid"));
    }
}
