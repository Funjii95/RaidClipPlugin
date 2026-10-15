using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class MusicCommandPermissionTests
{
    [Fact]
    public void ViewerAndVipCannotBypassMusicPermissionWithOverride()
    {
        Assert.False(MusicRequestService.CanUseMusicCommands(new ChatMessage
            { UserId = "viewer", IsVip = true, IsSubscriber = true,
                CommandAuthorization = CommandAuthorization.Allowed }, "owner"));
    }

    [Fact]
    public void ModeratorsAndBroadcasterCanUseMusicCommands()
    {
        Assert.True(MusicRequestService.CanUseMusicCommands(new ChatMessage
            { IsModerator = true }, "owner"));
        Assert.True(MusicRequestService.CanUseMusicCommands(new ChatMessage
            { UserId = "owner" }, "owner"));
    }

    [Fact]
    public void RegistryCannotLowerMusicRoleBelowModerator()
    {
        var config = new AppConfig();
        config.Commands.CommandRoleOverrides["music.request"] = "Viewer";
        var registry = new CommandRegistry();
        registry.Update(config);
        Assert.All(registry.Commands.Where(command => command.ModuleId == "music"),
            command => Assert.Contains(command.RequiredRole,
                new[] { CommandRole.Moderator, CommandRole.Broadcaster }));
    }
}
