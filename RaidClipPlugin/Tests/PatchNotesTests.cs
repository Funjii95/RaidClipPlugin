using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class PatchNotesTests
{
    [Fact]
    public void EmbeddedReleaseNotesCompleteTheLegacyHistoryOfflineAndInOrder()
    {
        const string legacy = "2.1.45 – bisherige Hinweise\n\nKeine Datenänderung.";
        var history = PatchNotesService.BuildHistory(legacy);
        foreach(var version in new[]{"2.2.0","2.3.0","2.3.1","2.4.0","2.4.1","2.5.0","2.5.1","2.8.4","2.8.5","2.8.15","2.8.16"})
            Assert.Contains(version, history);
        Assert.True(history.IndexOf("2.8.16",StringComparison.Ordinal)<history.IndexOf("2.8.15",StringComparison.Ordinal));
        Assert.EndsWith(legacy, history);
        Assert.Equal(1,history.Split("2.1.45").Length-1);
    }
    [Fact]
    public void NewerCallerHistoryIsNotDuplicated()
    {
        const string legacy = "99.0.0 – zukünftige Hinweise";
        Assert.Equal(legacy, PatchNotesService.BuildHistory(legacy));
    }
}
