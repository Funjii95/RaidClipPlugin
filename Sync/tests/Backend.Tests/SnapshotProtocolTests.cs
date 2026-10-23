using System.Text;
using System.Text.Json;
using Backend;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class SnapshotProtocolTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "funjii-snapshot-tests", Guid.NewGuid().ToString("N"));
    private const string Account = """
        {"UserId":"1001","DisplayName":"Synthetic Otter","Points":9007199254740993,
        "UpdatedAt":"2026-10-17T00:00:00Z","WatchMinutes":999,"PrivateRoot":"secret-root",
        "Rpg":{"SchemaVersion":1,"CompletedActions":["secret-action"],
        "Inventory":[{"InstanceId":"item-1","ItemId":"helmet","Rarity":2,"Kind":1,
        "PrivateItem":"secret-item","Attributes":{"Damage":1.25,"PrivateAttribute":"secret-attribute"}}],
        "Fish":{"eel":{"FishId":"eel","Quantity":2,"PrivateFish":"secret-fish"}},
        "Buffs":[{"RecipeId":"sushi","Activity":1,"RemainingUses":2,"PrivateBuff":"secret-buff"}],
        "Equipment":{"0":"item-1"}}}
        """;
    private static SnapshotMessage Message(long revision = 1) => new(1, "42", "1001", revision,
        Guid.NewGuid(), DateTimeOffset.Parse("2026-10-17T00:00:00Z"), SnapshotKind.Upsert, Account);
    private LocalSnapshotStore Store() => new(directory, "42");
    private async Task<StoredSnapshot> StoredAsync() => JsonSerializer.Deserialize<StoredSnapshot>(
        await File.ReadAllTextAsync(Path.Combine(directory, "42-1001.json")))!;

    [Fact]
    public async Task Repeating_after_restart_is_idempotent()
    {
        var message = Message();
        Assert.Equal(SnapshotDecision.Applied, await Store().ApplyAsync(message));
        var committed = await File.ReadAllBytesAsync(Path.Combine(directory, "42-1001.json"));
        for (var i = 0; i < 10; i++)
            Assert.Equal(SnapshotDecision.Duplicate, await Store().ApplyAsync(message));
        Assert.Equal(committed, await File.ReadAllBytesAsync(Path.Combine(directory, "42-1001.json")));
        Assert.Equal(9007199254740993L, SnapshotParser.Parse((await StoredAsync()).AccountJson!, "1001").Points);
    }

    [Fact]
    public async Task Late_snapshot_cannot_overwrite_newer_revision()
    {
        Assert.Equal(SnapshotDecision.Applied, await Store().ApplyAsync(Message(12)));
        Assert.Equal(SnapshotDecision.Stale, await Store().ApplyAsync(Message(11)));
        Assert.Equal(12, (await StoredAsync()).Revision);
    }

    [Fact]
    public async Task Same_revision_with_changed_content_or_identity_is_a_conflict()
    {
        var message = Message();
        await Store().ApplyAsync(message);
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(message with
            { AccountJson = Account.Replace("9007199254740993", "7") }));
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(message with { SnapshotId = Guid.NewGuid() }));
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(message with { CapturedAt = message.CapturedAt.AddSeconds(1) }));
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(message with { Revision = 2 }));
        Assert.Equal(1, (await StoredAsync()).Revision);
    }

    [Fact]
    public void Projection_removes_private_fields_at_every_supported_object_level()
    {
        var validated = SnapshotValidation.Validate(Message(), "42");
        Assert.DoesNotContain("secret-", validated.AccountJson);
        Assert.DoesNotContain("WatchMinutes", validated.AccountJson);
        var player = SnapshotParser.Parse(validated.AccountJson!, "1001");
        Assert.Equal(9007199254740993L, player.Points);
        Assert.Equal(1.25m, player.Rpg.Inventory[0].GetProperty("Attributes").GetProperty("Damage").GetDecimal());
        Assert.Equal(2, player.Rpg.Fish["eel"].GetProperty("Quantity").GetInt32());
    }

    [Fact]
    public async Task Formatting_and_property_order_do_not_change_content_identity()
    {
        var message = Message();
        await Store().ApplyAsync(message);
        var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Account)!;
        var reordered = JsonSerializer.Serialize(root.Reverse().ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(SnapshotDecision.Duplicate, await Store().ApplyAsync(message with { AccountJson = reordered }));
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("user")]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("snapshot-id")]
    [InlineData("date")]
    [InlineData("balance")]
    [InlineData("schema")]
    [InlineData("duplicate-json")]
    [InlineData("oversize")]
    public async Task Invalid_messages_do_not_create_storage(string scenario)
    {
        var message = Message();
        message = scenario switch
        {
            "channel" => message with { ChannelId = "99" },
            "user" => message with { UserId = "../private" },
            "version" => message with { ContractVersion = 2 },
            "revision" => message with { Revision = 0 },
            "snapshot-id" => message with { SnapshotId = Guid.Empty },
            "date" => message with { CapturedAt = message.CapturedAt.ToOffset(TimeSpan.FromHours(1)) },
            "balance" => message with { AccountJson = Account.Replace("9007199254740993", "-1") },
            "schema" => message with { AccountJson = Account.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2") },
            "duplicate-json" => message with { AccountJson = Account.Replace("\"Points\":9007199254740993", "\"Points\":9007199254740993,\"points\":5") },
            _ => message with { AccountJson = new string('x', SnapshotValidation.MaxMessageBytes + 1) }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().ApplyAsync(message));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Complete_envelope_is_bounded_before_json_parsing()
    {
        using var large = new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', SnapshotValidation.MaxMessageBytes + 1)));
        await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotValidation.ReadAsync(large, "42"));
        using var valid = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Message()));
        var snapshot = await SnapshotValidation.ReadAsync(valid, "42");
        Assert.Equal("1001", snapshot.UserId);
    }

    [Fact]
    public async Task Concurrent_writers_keep_highest_revision()
    {
        var messages = Enumerable.Range(1, 20).Select(i => Message(i)).ToArray();
        await Task.WhenAll(messages.Select(message => Store().ApplyAsync(message)));
        Assert.Equal(20, (await StoredAsync()).Revision);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task Tombstone_blocks_late_and_new_upserts_until_explicit_restore()
    {
        await Store().ApplyAsync(Message());
        var deletion = Message(2) with { Kind = SnapshotKind.Delete, AccountJson = null };
        Assert.Equal(SnapshotDecision.Applied, await Store().ApplyAsync(deletion));
        Assert.True((await StoredAsync()).Deleted);
        Assert.Null((await StoredAsync()).AccountJson);
        Assert.Equal(SnapshotDecision.Stale, await Store().ApplyAsync(Message(1)));
        Assert.Equal(SnapshotDecision.RestoreRequired, await Store().ApplyAsync(Message(3)));
        var restore = Message(3) with { Kind = SnapshotKind.Restore, ExpectedDeletedRevision = 2 };
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(restore with { ExpectedDeletedRevision = 1 }));
        Assert.Equal(SnapshotDecision.Applied, await Store().ApplyAsync(restore));
        Assert.False((await StoredAsync()).Deleted);
        Assert.Equal(SnapshotDecision.Duplicate, await Store().ApplyAsync(restore));
        Assert.Equal(SnapshotDecision.Stale, await Store().ApplyAsync(deletion));
    }

    [Fact]
    public async Task Restore_requires_existing_matching_tombstone()
    {
        var restore = Message(2) with { Kind = SnapshotKind.Restore, ExpectedDeletedRevision = 1 };
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(restore));
        await Store().ApplyAsync(Message());
        Assert.Equal(SnapshotDecision.Conflict, await Store().ApplyAsync(restore));
    }

    [Fact]
    public async Task Corrupt_state_fails_closed_without_replacing_it()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "42-1001.json");
        await File.WriteAllTextAsync(path, "{broken");
        await Assert.ThrowsAsync<JsonException>(() => Store().ApplyAsync(Message()));
        Assert.Equal("{broken", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("\"Rarity\":2", "\"Rarity\":10")]
    [InlineData("\"Damage\":1.25", "\"Damage\":\"invalid\"")]
    [InlineData("\"Quantity\":2", "\"Quantity\":-1")]
    [InlineData("\"RemainingUses\":2", "\"RemainingUses\":-1")]
    [InlineData("\"Equipment\":{\"0\":\"item-1\"}", "\"Equipment\":{\"0\":null}")]
    public async Task Invalid_nested_rpg_values_do_not_enter_projection(string before, string after)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().ApplyAsync(
            Message() with { AccountJson = Account.Replace(before, after) }));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Delete_before_first_export_still_blocks_resurrection()
    {
        var deletion = Message(5) with { Kind = SnapshotKind.Delete, AccountJson = null };
        Assert.Equal(SnapshotDecision.Applied, await Store().ApplyAsync(deletion));
        Assert.Equal(SnapshotDecision.Stale, await Store().ApplyAsync(Message(4)));
        Assert.Equal(SnapshotDecision.RestoreRequired, await Store().ApplyAsync(Message(6)));
    }

    [Fact]
    public async Task Duplicate_envelope_properties_are_rejected()
    {
        var json = JsonSerializer.Serialize(Message()).Replace("\"ContractVersion\":1", "\"ContractVersion\":1,\"contractVersion\":2");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotValidation.ReadAsync(stream, "42"));
    }

    [Fact]
    public async Task Cancellation_before_commit_leaves_no_account_file()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store().ApplyAsync(Message(), cancellation.Token));
        Assert.False(File.Exists(Path.Combine(directory, "42-1001.json")));
    }

    [Fact]
    public void Newer_progression_health_and_attributes_survive_without_private_fields()
    {
        var account = Account.Replace("\"SchemaVersion\":1", """
            "SchemaVersion":1,"PlayerLevel":3,"Experience":321,"SushiVersion":2,
            "Health":{"CurrentHp":150,"AppliedMaximumHp":250,"AppliedBaseMaximumHp":100,
            "LastChatHeal":null,"LastPassiveRegen":"2026-10-18T00:00:00Z","SushiRegenRemainder":0,
            "BossId":"synthetic-boss","Defeated":false,"BossDamage":10,"BossRemainingHp":900,
            "PrivateHealth":"secret-health"}
            """).Replace("\"Damage\":1.25", "\"Damage\":1.25,\"Power\":7,\"Stamina\":8,\"MaximumHp\":50,\"BossDodgeChance\":0.02");
        var projected = SnapshotValidation.Validate(Message() with { AccountJson = account }, "42").AccountJson!;
        Assert.DoesNotContain("secret-", projected);
        var player = SnapshotParser.Parse(projected, "1001");
        Assert.Equal(3, player.Rpg.PlayerLevel);
        Assert.Equal(321, player.Rpg.Experience);
        Assert.Equal(2, player.Rpg.SushiVersion);
        Assert.Equal(150, player.Rpg.Health!.CurrentHp);
        Assert.Null(player.Rpg.Health.LastChatHeal);
        Assert.Equal(7, player.Rpg.Inventory[0].GetProperty("Attributes").GetProperty("Power").GetDecimal());
        Assert.Equal(0.02m, player.Rpg.Inventory[0].GetProperty("Attributes").GetProperty("BossDodgeChance").GetDecimal());
    }

    [Theory]
    [InlineData("\"PlayerLevel\":0")]
    [InlineData("\"Experience\":-1")]
    [InlineData("\"SushiVersion\":\"2\"")]
    [InlineData("\"Health\":{\"CurrentHp\":-1}")]
    [InlineData("\"Health\":{\"Defeated\":\"false\"}")]
    [InlineData("\"Health\":{\"LastChatHeal\":\"invalid-date\"}")]
    public void Invalid_newer_fields_are_rejected(string field)
    {
        var account = Account.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1," + field);
        Assert.Throws<InvalidDataException>(() => SnapshotValidation.Validate(Message() with { AccountJson = account }, "42"));
    }

    public void Dispose()
    {
        // Unique test-only directory; never points to plugin or production data.
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

