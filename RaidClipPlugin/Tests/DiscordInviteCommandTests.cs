using Xunit;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Tests;

public sealed class DiscordInviteCommandTests
{
    [Theory]
    [InlineData("!dc", true)]
    [InlineData("  !DC  ", true)]
    [InlineData("!discord", false)]
    [InlineData("!join", false)]
    [InlineData("!dc extra", false)]
    public void OnlyConfiguredCommandMatches(string text, bool expected)
    {
        Assert.Equal(expected,
            DiscordInviteCommandService.IsCommand(
                text, "!dc"));
    }

    [Theory]
    [InlineData("https://discord.gg/raidclip", true)]
    [InlineData("https://discord.com/invite/raidclip", true)]
    [InlineData("http://discord.gg/raidclip", false)]
    [InlineData("https://example.com/raidclip", false)]
    [InlineData("", false)]
    public void InviteUrlValidationIsStrict(string value, bool expected)
    {
        Assert.Equal(expected,
            DiscordInviteCommandService.IsValidInviteUrl(value));
    }
}
