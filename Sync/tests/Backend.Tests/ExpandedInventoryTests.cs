using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class ExpandedInventoryTests
{
    private static string Inventory => JsonSerializer.Serialize(Enumerable.Range(0, 600).Select(i => new
    {
        InstanceId = "expanded-" + i, ItemId = "quest_abyss_0",
        DisplayName = "Helm des Abgrundbezwingers " + new string('a', 90),
        Rarity = 4, Slot = 1, Kind = 1, UpgradeLevel = i % 10,
        IsLocked = i % 2 == 0, IsFavorite = i % 3 == 0, SetId = "quest_abyss",
        Attributes = new { Armor = 45, Power = 18, Stamina = 22, MaximumHp = 100 },
        Effects = Array.Empty<object>()
    }));

    [Fact]
    public async Task Expanded_inventory_survives_wire_validation_and_firestore_roundtrip()
    {
        var message = PublisherTests.Message() with
        {
            AccountJson = PublisherTests.Account.Replace("\"Inventory\":[]", "\"Inventory\":" + Inventory)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        Assert.InRange(bytes.Length, 256 * 1024 + 1, SnapshotValidation.MaxMessageBytes);
        using var input = new MemoryStream(bytes);
        var validated = await SnapshotValidation.ReadAsync(input, "42");
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(message with { AccountJson = validated.AccountJson });
        var player = (await fixture.Reader.GetAsync("1001", default))!;
        Assert.Equal(600, player.Rpg.Inventory.Length);
        var last = player.Rpg.Inventory[599];
        Assert.Equal("expanded-599", last.GetProperty("InstanceId").GetString());
        Assert.Equal(9, last.GetProperty("UpgradeLevel").GetInt32());
        Assert.Equal(45, last.GetProperty("Attributes").GetProperty("Armor").GetInt32());
    }

    [Fact]
    public async Task Large_inventory_action_result_remains_complete_and_idempotent()
    {
        await using var f = new ActionApiFixture();
        var session = await f.ReadyAsync();
        var intent = ActionApiFixture.Intent("status", "{}");
        (await f.Viewer.PostAsJsonAsync("/api/v1/play/actions", intent)).EnsureSuccessStatusCode();
        (await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/" + session, null)).EnsureSuccessStatusCode();
        var data = "{\"inventory\":" + Inventory + ",\"catalogDescription\":\"" + new string('x', 100_000) + "\"}";
        var outcome = new ActionOutcome(true, "read", "Inventar geladen.", data);
        Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(outcome).Length, 384 * 1024 + 1, ActionResultLimits.MaximumOutcomeBytes);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete", new RelayComplete(session, intent.ActionId, outcome));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var receipt = await f.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/" + intent.ActionId);
        Assert.Equal(data, receipt!.Outcome!.DataJson);
    }
}
