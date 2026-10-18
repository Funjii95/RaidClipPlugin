using Backend;
using Google.Cloud.Firestore.V1;
using Grpc.Core;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotDelivery;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class FirestorePublisherTests
{
    [Fact]
    public async Task Bootstrap_inspection_reads_import_and_newer_revision_without_writing()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        Assert.Null(await fixture.Sink.ReadCurrentAsync("1001"));
        fixture.Server.Seed(Import(Message()));
        var initial = await fixture.Sink.ReadCurrentAsync("1001");
        Assert.Equal(1, initial!.Revision);
        Assert.False(initial.Deleted);
        Assert.Equal(0, fixture.Server.WriteCount);
        await fixture.Sink.ApplyAsync(Message(2));
        Assert.Equal(2, (await fixture.Sink.ReadCurrentAsync("1001"))!.Revision);
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Bootstrap_inspection_rejects_corrupt_metadata_without_repair()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var doc = Import(Message());
        doc.Fields["channelId"] = new Value { StringValue = "99" };
        fixture.Server.Seed(doc);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Sink.ReadCurrentAsync("1001"));
        Assert.Equal(0, fixture.Server.WriteCount);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Sink.ReadCurrentAsync("../1001"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submicrosecond_capture_time_remains_idempotent_after_firestore_truncation(bool previousFormat)
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var message = Message() with { CapturedAt = DateTimeOffset.Parse("2026-10-18T00:00:00.1234567Z") };
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(message));
        if (previousFormat)
        {
            var doc = fixture.Server.Peek()!;
            doc.Fields.Remove("capturedAtIso");
            fixture.Server.Seed(doc);
        }
        Assert.Equal(SnapshotDecision.Duplicate, await fixture.Sink.ApplyAsync(message));
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(Message(2)));
    }
    private static SnapshotMessage Message(long revision = 1) => PublisherTests.Message(revision);
    private static Document Import(SnapshotMessage message)
    {
        var doc = new Document { Name = TransactionalFirestoreFixture.DocumentName };
        doc.Fields.Add("accountJson", new Value { StringValue = SnapshotValidation.Validate(message, "42").AccountJson });
        doc.Fields.Add("channelId", new Value { StringValue = "42" });
        doc.Fields.Add("revision", new Value { IntegerValue = 1 });
        doc.Fields.Add("snapshotId", new Value { StringValue = message.SnapshotId.ToString() });
        doc.Fields.Add("capturedAt", new Value { TimestampValue = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(message.CapturedAt.UtcDateTime) });
        doc.Fields.Add("importKind", new Value { StringValue = "explicit-initial-profile-import" });
        doc.Fields.Add("importSourceHash", new Value { StringValue = new string('A', 64) });
        return doc;
    }

    [Fact]
    public async Task Actual_sdk_commits_sanitized_projection_and_retries_are_write_free()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var message = Message();
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(message));
        var saved = fixture.Server.Peek()!;
        Assert.Equal(1, saved.Fields["revision"].IntegerValue);
        Assert.Equal("42", saved.Fields["channelId"].StringValue);
        Assert.DoesNotContain("private-action", saved.Fields["accountJson"].StringValue);
        Assert.DoesNotContain("local-private", saved.Fields["accountJson"].StringValue);
        Assert.Equal(9007199254740993L, (await fixture.Reader.GetAsync("1001", default))!.Points);
        for (var i = 0; i < 3; i++) Assert.Equal(SnapshotDecision.Duplicate, await fixture.Sink.ApplyAsync(message));
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Initial_import_is_preserved_until_a_strictly_newer_revision_arrives()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var initial = Message();
        fixture.Server.Seed(Import(initial));
        Assert.Equal(SnapshotDecision.Duplicate, await fixture.Sink.ApplyAsync(initial));
        Assert.Equal(SnapshotDecision.Conflict, await fixture.Sink.ApplyAsync(Message()));
        Assert.Equal(0, fixture.Server.WriteCount);
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(Message(2)));
        var saved = fixture.Server.Peek()!;
        Assert.Equal(2, saved.Fields["revision"].IntegerValue);
        Assert.False(saved.Fields.ContainsKey("importSourceHash"));
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Aborted_commit_reexecutes_transaction_with_no_duplicate_write()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        fixture.Server.AbortNextCommit();
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(Message()));
        Assert.True(fixture.Server.BeginCount >= 2);
        Assert.True(fixture.Server.CommitCount >= 2);
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Newer_revision_wins_when_two_sdk_transactions_race()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(Message());
        var results = await Task.WhenAll(fixture.Sink.ApplyAsync(Message(2)), fixture.Sink.ApplyAsync(Message(3)));
        Assert.Contains(SnapshotDecision.Applied, results);
        Assert.Equal(3, fixture.Server.Peek()!.Fields["revision"].IntegerValue);
        var writes = fixture.Server.WriteCount;
        Assert.Equal(SnapshotDecision.Stale, await fixture.Sink.ApplyAsync(Message(2)));
        Assert.Equal(writes, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Delete_removes_account_but_keeps_revision_and_restore_is_explicit()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(Message());
        var deletion = Message(2) with { Kind = SnapshotKind.Delete, AccountJson = null };
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(deletion));
        Assert.True(fixture.Server.Peek()!.Fields["deleted"].BooleanValue);
        Assert.False(fixture.Server.Peek()!.Fields.ContainsKey("accountJson"));
        Assert.Null(await fixture.Reader.GetAsync("1001", default));
        Assert.Equal(SnapshotDecision.Duplicate, await fixture.Sink.ApplyAsync(deletion));
        Assert.Equal(SnapshotDecision.Stale, await fixture.Sink.ApplyAsync(Message()));
        Assert.Equal(SnapshotDecision.RestoreRequired, await fixture.Sink.ApplyAsync(Message(3)));
        Assert.Equal(SnapshotDecision.Conflict, await fixture.Sink.ApplyAsync(Message(3) with
            { Kind = SnapshotKind.Restore, ExpectedDeletedRevision = 1 }));
        var restore = Message(3) with { Kind = SnapshotKind.Restore, ExpectedDeletedRevision = 2 };
        Assert.Equal(SnapshotDecision.Applied, await fixture.Sink.ApplyAsync(restore));
        Assert.Equal(SnapshotDecision.Duplicate, await fixture.Sink.ApplyAsync(restore));
        Assert.NotNull(await fixture.Reader.GetAsync("1001", default));
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("importKind")]
    [InlineData("importHash")]
    [InlineData("metadataMissing")]
    public async Task Unknown_or_foreign_existing_documents_fail_closed(string scenario)
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var doc = Import(Message());
        if (scenario == "channel") doc.Fields["channelId"].StringValue = "43";
        if (scenario == "importKind") doc.Fields["importKind"].StringValue = "unknown";
        if (scenario == "importHash") doc.Fields["importSourceHash"].StringValue = "invalid";
        if (scenario == "metadataMissing") doc.Fields.Remove("revision");
        fixture.Server.Seed(doc);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Sink.ApplyAsync(Message(2)));
        Assert.Equal(0, fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Tampered_content_hash_is_not_silently_repaired()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(Message());
        var doc = fixture.Server.Peek()!;
        doc.Fields["contentHash"].StringValue = new string('0', 64);
        fixture.Server.Seed(doc);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Sink.ApplyAsync(Message(2)));
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("deleted")]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("owner")]
    public async Task Corrupt_normalized_metadata_is_never_overwritten(string scenario)
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(Message());
        var doc = fixture.Server.Peek()!;
        switch (scenario)
        {
            case "kind": doc.Fields["kind"].IntegerValue = 3; break;
            case "deleted": doc.Fields["deleted"].BooleanValue = true; break;
            case "version": doc.Fields.Remove("contractVersion"); break;
            case "revision": doc.Fields["revision"].IntegerValue = -1; break;
            case "owner": doc.Fields["userId"].StringValue = "1002"; break;
        }
        fixture.Server.Seed(doc);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Sink.ApplyAsync(Message(2)));
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("missingRevision")]
    [InlineData("wrongOwner")]
    public async Task Malformed_tombstone_is_a_source_error_not_a_missing_profile(string scenario)
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(Message() with { Kind = SnapshotKind.Delete, AccountJson = null });
        var doc = fixture.Server.Peek()!;
        if (scenario == "account") doc.Fields.Add("accountJson", new Value { StringValue = PublisherTests.Account });
        if (scenario == "missingRevision") doc.Fields.Remove("revision");
        if (scenario == "wrongOwner") doc.Fields["userId"].StringValue = "1002";
        fixture.Server.Seed(doc);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.GetAsync("1001", default));
    }

    [Fact]
    public async Task Full_http_pipeline_updates_import_then_deletes_and_restores_readable_profile()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var directory = Path.Combine(Path.GetTempPath(), "funjii-firestore-pipeline", Guid.NewGuid().ToString("N"));
        try
        {
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            using var publisher = new PublisherFactory(Path.Combine(directory, "unused-file-store"), key);
            using var configured = publisher.WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.RemoveAll<ISnapshotSink>();
                services.AddSingleton<ISnapshotSink>(fixture.Sink);
            }));
            using var sender = new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"), key,
                configured.Server.CreateHandler(), true);
            var initial = Message();
            fixture.Server.Seed(Import(initial));
            var outbox = new LocalOutboxStore(Path.Combine(directory, "outbox"), "42");
            var dispatcher = new OutboxDispatcher(outbox, sender);
            await outbox.CommitAsync("1001", 0, initial.SnapshotId, initial.CapturedAt, initial.Kind, initial.AccountJson);
            Assert.Equal(SnapshotDecision.Duplicate, (await dispatcher.SendNextAsync("1001")).Decision);
            Assert.Equal(0, fixture.Server.WriteCount);
            var changed = initial.AccountJson!.Replace("9007199254740993", "7");
            await outbox.CommitAsync("1001", 1, Guid.NewGuid(), initial.CapturedAt, SnapshotKind.Upsert, changed);
            Assert.Equal(SnapshotDecision.Applied, (await dispatcher.SendNextAsync("1001")).Decision);
            Assert.Equal(7, (await fixture.Reader.GetAsync("1001", default))!.Points);
            using var api = new ApiFactory();
            using var readApi = api.WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.RemoveAll<IPlayerReader>();
                services.AddSingleton<IPlayerReader>(fixture.Reader);
            }));
            using var client = readApi.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.PluginKey);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/plugin/players/1001")).StatusCode);
            await outbox.CommitAsync("1001", 2, Guid.NewGuid(), initial.CapturedAt, SnapshotKind.Delete, null);
            Assert.Equal(SnapshotDecision.Applied, (await dispatcher.SendNextAsync("1001")).Decision);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/plugin/players/1001")).StatusCode);
            await outbox.CommitAsync("1001", 3, Guid.NewGuid(), initial.CapturedAt, SnapshotKind.Restore, changed, 3);
            Assert.Equal(SnapshotDecision.Applied, (await dispatcher.SendNextAsync("1001")).Decision);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/plugin/players/1001")).StatusCode);
            Assert.Empty((await outbox.ReadAsync("1001"))!.Pending);
            Assert.False(Directory.Exists(Path.Combine(directory, "unused-file-store")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Cancellation_invalid_channel_and_permission_denial_never_create_profiles()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Sink.ApplyAsync(Message() with { ChannelId = "43" }));
        Assert.Equal(0, fixture.Server.BeginCount);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Sink.ApplyAsync(Message(), cancelled.Token));
        Assert.Equal(0, fixture.Server.BeginCount);
        fixture.Server.DenyAccess = true;
        var denied = await Assert.ThrowsAsync<RpcException>(() => fixture.Sink.ApplyAsync(Message()));
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
        Assert.Null(fixture.Server.Peek());
    }
}
