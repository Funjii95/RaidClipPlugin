using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CommandRegistryTests
{
    [Fact]
    public void AllNewCommandsAreRegisteredWithoutJoinCollisionAndCorrectRoles()
    {
        var config = new AppConfig(); config.Minigame.Enabled = true; config.Heist.Enabled = true;
        var registry = new CommandRegistry(); registry.Update(config);
        foreach (var command in new[] { "!fischen", "!schatzsuche", "!start race", "!joinrace", "!joinboss", "!joinheist", "!sammlung", "!profil" })
            Assert.Contains(registry.VisibleFor(new ChatMessage()), c => c.CommandText == command);
        foreach (var command in new[] { "!boss", "!heist", "!start boss", "!start heist" })
        {
            Assert.DoesNotContain(registry.VisibleFor(new ChatMessage()), c => c.CommandText == command);
            Assert.Contains(registry.VisibleFor(new ChatMessage { IsModerator = true }), c => c.CommandText == command);
        }
        Assert.DoesNotContain(registry.FindCollisions(false), c => c.Command is "!joinheist" or "!joinrace" or "!joinboss");
    }
    [Fact]
    public void ConfiguredHeistCommandsAppearImmediately()
    {
        var config=new AppConfig(); config.Heist.Enabled=true; config.Heist.StartCommand="raid"; config.Heist.JoinCommand="crew";
        var registry=new CommandRegistry(); registry.Update(config);
        Assert.Contains(registry.Commands,x=>x.CommandId=="heist.start"&&x.CommandText=="!raid"&&x.Enabled);
        Assert.Contains(registry.Commands,x=>x.CommandId=="heist.join"&&x.CommandText=="!crew"&&x.Enabled);
    }

    [Fact]
    public void CollisionNamesBothModules()
    {
        var config=new AppConfig(); config.Heist.Enabled=true; config.Heist.JoinCommand="!clip"; config.ClipCommand.Enabled=true; config.ClipCommand.Command="!clip";
        var registry=new CommandRegistry(); registry.Update(config);
        var collision=Assert.Single(registry.FindCollisions(),x=>x.Command=="!clip");
        Assert.Contains("Heist",collision.Message); Assert.Contains("Clips",collision.Message);
    }

    [Fact]
    public void ViewerDoesNotSeeModeratorOrBroadcasterCommands()
    {
        var config=new AppConfig(); config.Minigame.PointsEnabled=true; var registry=new CommandRegistry(); registry.Update(config);
        var visible=registry.VisibleFor(new ChatMessage{UserId="1",UserName="viewer"});
        Assert.DoesNotContain(visible,x=>x.RequiredRole is CommandRole.Moderator or CommandRole.Broadcaster);
    }

    [Fact]
    public void ModeratorSeesModeratorButNotBroadcasterCommands()
    {
        var config=new AppConfig(); config.Minigame.PointsEnabled=true; var registry=new CommandRegistry(); registry.Update(config);
        var visible=registry.VisibleFor(new ChatMessage{UserId="1",UserName="mod",IsModerator=true});
        Assert.Contains(visible,x=>x.RequiredRole==CommandRole.Moderator);
        Assert.DoesNotContain(visible,x=>x.RequiredRole==CommandRole.Broadcaster);
    }

    [Fact]
    public void BroadcasterSeesEveryEnabledVisibleCommand()
    {
        var config=new AppConfig(); config.Minigame.PointsEnabled=true; var registry=new CommandRegistry(); registry.Update(config);
        var visible=registry.VisibleFor(new ChatMessage{UserId="1",UserName="caster",IsBroadcaster=true});
        Assert.Equal(registry.Commands.Count(x=>x.Enabled&&x.IsVisible),visible.Count);
    }
}
