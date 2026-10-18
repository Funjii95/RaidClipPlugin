using SnapshotPublisher;
using SnapshotFirestore;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class ProfilePublisherScopeTests
{
    [Fact]
    public void Profile_mode_requires_exact_test_scope_and_keeps_writes_off_by_default()
    {
        var settings = new PublisherSettings { Mode = "FirestoreProfilesTest", ProjectId = "funjii-otter-rpg",
            ChannelId = "42812664", Collection = "rpgReadSnapshots", ApiKeyHash = new string('A', 64),
            AllowedUserIds = ["1", "2", "3", "4", "5", "6"] };
        settings.Validate();
        Assert.False(settings.WritesEnabled);
        settings.AllowedUserIds[5] = "5";
        Assert.Throws<InvalidOperationException>(settings.Validate);
        settings.AllowedUserIds[5] = "6";
        settings.Collection = "other";
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public async Task Sequential_mode_refuses_new_accounts_revision_jumps_and_deletes()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var sink = new FirestoreSnapshotSink(fixture.Database, "42", sequentialUpdatesOnly: true);
        Assert.Equal(SnapshotDecision.Conflict, await sink.ApplyAsync(PublisherTests.Message()));
        await fixture.Sink.ApplyAsync(PublisherTests.Message());
        Assert.Equal(SnapshotDecision.Conflict, await sink.ApplyAsync(PublisherTests.Message(3)));
        Assert.Equal(SnapshotDecision.Conflict, await sink.ApplyAsync(PublisherTests.Message(2) with { Kind = SnapshotKind.Delete, AccountJson = null }));
        var next = PublisherTests.Message(2);
        Assert.Equal(SnapshotDecision.Applied, await sink.ApplyAsync(next));
        Assert.Equal(SnapshotDecision.Duplicate, await sink.ApplyAsync(next));
        Assert.Equal(2, fixture.Server.WriteCount);
    }
}
