using System.Text.Json;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class LocalOutboxTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "funjii-outbox-tests", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-18T00:00:00Z");
    private const string Account = """
        {"UserId":"1001","DisplayName":"SYNTHETIC","Points":9007199254740993,
        "UpdatedAt":"2026-10-18T00:00:00Z","PrivateNote":"LOCAL ONLY",
        "Rpg":{"SchemaVersion":1,"CompletedActions":["private-action"],"Inventory":[]}}
        """;
    private LocalOutboxStore Store() => new(directory, "42");
    private Task<OutboxState> Commit(long expected = 0, Guid? id = null, string account = Account,
        CancellationToken ct = default) => Store().CommitAsync("1001", expected, id ?? Guid.NewGuid(),
            Time, SnapshotKind.Upsert, account, ct: ct);

    [Fact]
    public async Task Account_revision_and_projected_message_survive_restart_together()
    {
        var committed = await Commit();
        var restarted = (await Store().ReadAsync("1001"))!;
        Assert.Equal(1, restarted.Revision);
        Assert.Equal(Account, restarted.PrivateAccountJson);
        Assert.Equal(committed.CommitId, restarted.CommitId);
        var pending = Assert.Single(restarted.Pending);
        Assert.DoesNotContain("LOCAL ONLY", pending.AccountJson);
        Assert.DoesNotContain("private-action", pending.AccountJson);
        Assert.Equal(9007199254740993L, JsonDocument.Parse(pending.AccountJson!).RootElement.GetProperty("Points").GetInt64());
    }

    [Fact]
    public async Task Retry_keeps_message_identity_and_does_not_allocate_revision()
    {
        var id = Guid.NewGuid();
        var state = await Commit(id: id);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(await Commit(id: id)));
        await Store().AcknowledgeAsync("1001", id, 1, SnapshotDecision.Applied);
        var retried = await Commit(id: id);
        Assert.Empty(retried.Pending);
        Assert.Equal(1, retried.Revision);
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(id: id, account: Account.Replace("SYNTHETIC", "changed")));
    }

    [Fact]
    public async Task Crash_after_publisher_commit_before_ack_is_safe_to_repeat()
    {
        var outbox = await Commit();
        var message = outbox.Pending[0];
        var receiver = new LocalSnapshotStore(Path.Combine(directory, "receiver"), "42");
        Assert.Equal(SnapshotDecision.Applied, await receiver.ApplyAsync(message));
        // Simulate sender restart with no acknowledgement persisted.
        var retry = Assert.Single((await Store().ReadAsync("1001"))!.Pending);
        Assert.Equal(SnapshotDecision.Duplicate, await receiver.ApplyAsync(retry));
        await Store().AcknowledgeAsync("1001", retry.SnapshotId, retry.Revision, SnapshotDecision.Duplicate);
        var final = (await Store().ReadAsync("1001"))!;
        Assert.Empty(final.Pending);
        Assert.Equal(Account, final.PrivateAccountJson);
    }

    [Fact]
    public async Task Only_one_concurrent_writer_can_commit_a_given_previous_revision()
    {
        async Task<bool> Attempt()
        {
            try { await Commit(); return true; }
            catch (InvalidDataException) { return false; }
        }
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Attempt()));
        Assert.Single(outcomes, x => x);
        Assert.Single((await Store().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Acks_cannot_skip_messages_or_discard_conflicts()
    {
        var one = await Commit();
        var two = await Commit(1, account: Account.Replace("9007199254740993", "7"));
        var head = one.Pending[0];
        var tail = two.Pending[1];
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().AcknowledgeAsync("1001", tail.SnapshotId, 2, SnapshotDecision.Applied));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().AcknowledgeAsync("1001", head.SnapshotId, 1, SnapshotDecision.Conflict));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().AcknowledgeAsync("1001", head.SnapshotId, 2, SnapshotDecision.Applied));
        Assert.Equal(2, (await Store().ReadAsync("1001"))!.Pending.Length);
        await Store().AcknowledgeAsync("1001", head.SnapshotId, 1, SnapshotDecision.Stale);
        Assert.Equal(tail.SnapshotId, Assert.Single((await Store().ReadAsync("1001"))!.Pending).SnapshotId);
        Assert.Equal(2, (await Store().ReadAsync("1001"))!.Revision);
    }

    [Fact]
    public async Task Delete_requires_explicit_restore_and_retains_sequence()
    {
        await Commit();
        var deleted = await Store().CommitAsync("1001", 1, Guid.NewGuid(), Time, SnapshotKind.Delete, null);
        Assert.Null(deleted.PrivateAccountJson);
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(2));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().CommitAsync("1001", 2, Guid.NewGuid(), Time,
            SnapshotKind.Restore, Account, expectedDeletedRevision: 1));
        var restored = await Store().CommitAsync("1001", 2, Guid.NewGuid(), Time, SnapshotKind.Restore, Account, 2);
        Assert.Equal(3, restored.Revision);
        var receiver = new LocalSnapshotStore(Path.Combine(directory, "receiver"), "42");
        foreach (var message in restored.Pending)
        {
            Assert.Equal(SnapshotDecision.Applied, await receiver.ApplyAsync(message));
            await Store().AcknowledgeAsync("1001", message.SnapshotId, message.Revision, SnapshotDecision.Applied);
        }
        Assert.Empty((await Store().ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Failed_validation_or_cancellation_does_not_change_committed_file()
    {
        await Commit();
        var path = Path.Combine(directory, "42-1001.outbox.json");
        var before = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(1, account: Account.Replace("1001", "1002")));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Commit(1, ct: cancel.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task Capacity_is_bounded_and_a_full_queue_preserves_previous_commit()
    {
        for (var revision = 0; revision < LocalOutboxStore.MaxPending; revision++) await Commit(revision);
        var path = Path.Combine(directory, "42-1001.outbox.json");
        var before = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(LocalOutboxStore.MaxPending));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Corrupt_state_and_pending_identity_reuse_fail_closed()
    {
        var first = await Commit();
        await Commit(1);
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(2, id: first.CommitId));
        var path = Path.Combine(directory, "42-1001.outbox.json");
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, json.Replace("\"ChannelId\":\"42\"", "\"ChannelId\":\"43\""));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().ReadAsync("1001"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(2));
    }

    [Fact]
    public async Task Account_corruption_after_ack_is_detected_even_with_an_empty_queue()
    {
        var state = await Commit();
        await Store().AcknowledgeAsync("1001", state.CommitId, 1, SnapshotDecision.Applied);
        var path = Path.Combine(directory, "42-1001.outbox.json");
        // Simulate corruption in the private account while preserving valid JSON and its type.
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("LOCAL ONLY", json);
        await File.WriteAllTextAsync(path, json.Replace("LOCAL ONLY", "CORRUPTED"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().ReadAsync("1001"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Commit(1));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
