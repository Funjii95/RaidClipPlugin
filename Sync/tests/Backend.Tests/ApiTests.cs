using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Backend;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Tests;
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly string Secret = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string('s', 32)));
    public const string PluginKey = "test-only-plugin-key-with-32-bytes-minimum";
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Twitch:SecretBase64"] = Secret, ["Twitch:ChannelId"] = "42",
            ["Twitch:AllowedOrigins:0"] = "https://extension.example",
            ["Plugin:Enabled"] = "true", ["Plugin:ApiKey"] = PluginKey, ["Data:Mode"] = "Mock"
        }));
}
public class ApiTests
{
    private static string Token(string user = "1001", string channel = "42", string role = "viewer",
        string opaque = "Udemo", bool expired = false, string? secret = null, bool unlinked = false,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var claims = new[] { new Claim("user_id", user), new Claim("channel_id", channel), new Claim("role", role),
            new Claim("opaque_user_id", opaque), new Claim("is_unlinked", unlinked ? "true" : "false") };
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(expired ? -5 : 5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(secret ?? ApiFactory.Secret)), algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private static HttpRequestMessage Request(string path, string token, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_is_public(string path)
    {
        await using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Missing_credentials_are_rejected()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/plugin/players/1001")).StatusCode);
    }
    [Fact]
    public async Task Viewer_reads_only_claim_identity_even_with_query_override()
    {
        await using var factory = new ApiFactory();
        var response = await factory.CreateClient().SendAsync(Request("/api/v1/me?userId=9999", Token()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"userId\":\"1001\"", json);
        Assert.Contains("\"source\":\"mock\"", json);
        Assert.DoesNotContain("completedActions", json);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }
    [Theory]
    [InlineData("channel", HttpStatusCode.Unauthorized)]
    [InlineData("role", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("signature", HttpStatusCode.Unauthorized)]
    [InlineData("anonymous", HttpStatusCode.Forbidden)]
    [InlineData("identity", HttpStatusCode.Forbidden)]
    [InlineData("unlinked", HttpStatusCode.Forbidden)]
    public async Task Invalid_twitch_context_is_rejected(string scenario, HttpStatusCode expected)
    {
        var token = Token(user: scenario == "identity" ? "" : "1001", channel: scenario == "channel" ? "99" : "42",
            role: scenario == "role" ? "external" : "viewer", opaque: scenario == "anonymous" ? "Ademo" : "Udemo",
            expired: scenario == "expired", secret: scenario == "signature" ? Convert.ToBase64String(new byte[32]) : null,
            unlinked: scenario == "unlinked");
        await using var factory = new ApiFactory();
        Assert.Equal(expected, (await factory.CreateClient().SendAsync(Request("/api/v1/me", token))).StatusCode);
    }
    [Fact]
    public async Task Plugin_and_twitch_credentials_are_not_interchangeable()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request("/api/v1/plugin/players/1001", ApiFactory.PluginKey))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("/api/v1/plugin/players/1001", Token()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("/api/v1/me", ApiFactory.PluginKey))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("/api/v1/plugin/players/1001", "wrong"))).StatusCode);
    }
    [Fact]
    public async Task Unknown_player_has_no_implicit_account_creation()
    {
        await using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().SendAsync(Request("/api/v1/me", Token(user: "9999")))).StatusCode);
    }
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Writes_are_not_routed(string method)
    {
        await using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await factory.CreateClient().SendAsync(Request("/api/v1/me", Token(), new HttpMethod(method)))).StatusCode);
    }
    [Fact]
    public async Task Cors_only_allows_configured_origin()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        foreach (var origin in new[] { "https://extension.example", "https://evil.example" })
        {
            var request = Request("/api/v1/me", Token());
            request.Headers.Add("Origin", origin);
            var response = await client.SendAsync(request);
            Assert.Equal(origin == "https://extension.example", response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }
    [Fact]
    public async Task Reads_are_rate_limited()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request("/api/v1/me", Token()))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Request("/api/v1/me", Token()))).StatusCode);
    }
    [Fact]
    public void Snapshot_preserves_long_balance_and_rpg_but_drops_internal_tokens()
    {
        var player = SnapshotParser.Parse("""
            {"UserId":"1001","DisplayName":"Otter","Points":9007199254740993,
            "UpdatedAt":"2026-10-17T00:00:00Z","Rpg":{"SchemaVersion":1,
            "Equipment":{"0":"item-1"},"Inventory":[{"InstanceId":"item-1","Rarity":2,"Attributes":{"Damage":1.25}}],
            "CompletedActions":["private-token"]}}
            """, "1001");
        Assert.Equal(9007199254740993L, player.Points);
        Assert.Equal("item-1", player.Rpg.Equipment["0"]);
        Assert.DoesNotContain("private-token", System.Text.Json.JsonSerializer.Serialize(player));
    }
    [Theory]
    [InlineData("{\"UserId\":\"other\"}")]
    [InlineData("{\"UserId\":\"1001\",\"Points\":-1}")]
    [InlineData("{\"UserId\":\"1001\",\"Points\":0,\"Rpg\":{\"SchemaVersion\":2}}")]
    public void Invalid_snapshots_are_rejected(string json) => Assert.Throws<InvalidDataException>(() => SnapshotParser.Parse(json, "1001"));
    [Fact]
    public void Missing_secrets_fail_closed() => Assert.Throws<InvalidOperationException>(() => new BackendSettings().Validate());
    [Fact]
    public async Task Disabled_plugin_rejects_even_correct_key()
    {
        await using var factory = new ApiFactory();
        await using var disabled = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Plugin:Enabled"] = "false" })));
        Assert.Equal(HttpStatusCode.Unauthorized, (await disabled.CreateClient().SendAsync(
            Request("/api/v1/plugin/players/1001", ApiFactory.PluginKey))).StatusCode);
    }
    [Fact]
    public async Task Other_signing_algorithms_are_rejected()
    {
        await using var factory = new ApiFactory();
        // Same 256-bit key, valid HMAC-SHA384 signature, explicitly forbidden algorithm.
        var key = Convert.ToBase64String(new byte[64]);
        await using var configured = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Twitch:SecretBase64"] = key })));
        Assert.Equal(HttpStatusCode.Unauthorized, (await configured.CreateClient().SendAsync(
            Request("/api/v1/me", Token(secret: key, algorithm: SecurityAlgorithms.HmacSha384)))).StatusCode);
    }
    [Fact]
    public async Task Source_failure_is_503_without_mock_fallback_and_liveness_stays_ok()
    {
        await using var factory = new ApiFactory();
        await using var broken = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IPlayerReader>();
            s.AddSingleton<IPlayerReader>(new FailingReader());
        }));
        var client = broken.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(Request("/api/v1/me", Token()))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    private sealed class FailingReader : IPlayerReader
    {
        public Task<PlayerView?> GetAsync(string userId, CancellationToken ct) => throw new IOException("private details");
        public Task CheckAsync(CancellationToken ct) => throw new IOException("private details");
    }
}
