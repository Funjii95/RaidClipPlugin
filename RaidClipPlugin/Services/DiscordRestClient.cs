using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class DiscordRestClient : IDiscordClipClient, IDisposable
{
    private const string ApiBase = "https://discord.com/api/v10";
    private const ulong Administrator = 1UL << 3;
    private const ulong ViewChannel = 1UL << 10;
    private const ulong SendMessages = 1UL << 11;
    private const ulong EmbedLinks = 1UL << 14;
    private const ulong SendMessagesInThreads = 1UL << 38;
    private readonly HttpClient _http = new();
    private readonly bool _hasBotToken;

    public DiscordRestClient(string botToken)
    {
        _hasBotToken = !string.IsNullOrWhiteSpace(botToken);
        if (_hasBotToken)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bot", botToken.Trim());
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<DiscordChannelValidation> ValidateChannelAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken)
    {
        if (!_hasBotToken)
            return new(channelId, "", false, false, false, false,
                "Discord-Bot-Token fehlt.");
        try
        {
            using var channel = await GetJsonAsync(
                $"{ApiBase}/channels/{Uri.EscapeDataString(channelId)}",
                cancellationToken);
            var root = channel.RootElement;
            var type = root.GetProperty("type").GetInt32();
            var name = root.TryGetProperty("name", out var nameValue)
                ? nameValue.GetString() ?? channelId : channelId;
            var actualGuild = root.TryGetProperty("guild_id", out var guild)
                ? guild.GetString() ?? "" : "";
            if (!actualGuild.Equals(guildId, StringComparison.Ordinal))
                return new(channelId, name, false, false, false, false,
                    "Der Channel gehört nicht zum ausgewählten Discord-Server.");
            if (type is not (0 or 5 or 10 or 11 or 12))
                return new(channelId, name, false, false, false, false,
                    "Der Discord-Channel ist kein geeigneter Textkanal.");

            var permissions = await GetEffectivePermissionsAsync(
                guildId, root, cancellationToken);
            var administrator = (permissions & Administrator) != 0;
            var canView = administrator || (permissions & ViewChannel) != 0;
            var canSend = administrator || (type is 10 or 11 or 12
                ? (permissions & SendMessagesInThreads) != 0
                : (permissions & SendMessages) != 0);
            var canEmbed = administrator || (permissions & EmbedLinks) != 0;
            var valid = canView && canSend && canEmbed;
            var error = valid ? "" :
                "Dem Bot fehlen VIEW_CHANNEL, SEND_MESSAGES oder EMBED_LINKS.";
            return new(channelId, name, valid, canView, canSend, canEmbed, error);
        }
        catch (Exception exception)
        {
            return new(channelId, "", false, false, false, false,
                SafeError(exception));
        }
    }

    public async Task<DiscordChannelValidation> ValidateWebhookAsync(
        string webhookUrl,
        string guildId,
        string channelId,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedWebhook(webhookUrl))
            return new(channelId, "", false, false, false, false,
                "Die Discord-Webhook-URL ist ungültig.");
        try
        {
            using var response = await _http.GetAsync(webhookUrl, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var actualGuild = root.TryGetProperty("guild_id", out var guild)
                ? guild.GetString() ?? "" : "";
            var actualChannel = root.TryGetProperty("channel_id", out var channel)
                ? channel.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var nameValue)
                ? nameValue.GetString() ?? "Webhook" : "Webhook";
            var valid = actualGuild.Equals(guildId, StringComparison.Ordinal) &&
                        actualChannel.Equals(channelId, StringComparison.Ordinal);
            return new(channelId, name, valid, valid, valid, valid,
                valid ? "" : "Webhook gehört nicht zum konfigurierten Server oder Channel.");
        }
        catch (Exception exception)
        {
            return new(channelId, "", false, false, false, false,
                SafeError(exception));
        }
    }

    public async Task SendMessageAsync(
        string channelId,
        object payload,
        CancellationToken cancellationToken)
    {
        if (!_hasBotToken)
            throw new InvalidOperationException("Discord-Bot-Token fehlt.");
        using var response = await _http.PostAsJsonAsync(
            $"{ApiBase}/channels/{Uri.EscapeDataString(channelId)}/messages",
            payload, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SendWebhookAsync(
        string webhookUrl,
        object payload,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedWebhook(webhookUrl))
            throw new InvalidOperationException("Die Discord-Webhook-URL ist ungültig.");
        using var response = await _http.PostAsJsonAsync(
            webhookUrl, payload, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<ulong> GetEffectivePermissionsAsync(
        string guildId,
        JsonElement channel,
        CancellationToken cancellationToken)
    {
        using var me = await GetJsonAsync(
            $"{ApiBase}/users/@me", cancellationToken);
        var botId = me.RootElement.GetProperty("id").GetString() ?? "";
        using var member = await GetJsonAsync(
            $"{ApiBase}/guilds/{Uri.EscapeDataString(guildId)}/members/" +
            Uri.EscapeDataString(botId), cancellationToken);
        using var rolesDocument = await GetJsonAsync(
            $"{ApiBase}/guilds/{Uri.EscapeDataString(guildId)}/roles",
            cancellationToken);

        var memberRoles = member.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        memberRoles.Add(guildId);

        ulong permissions = 0;
        foreach (var role in rolesDocument.RootElement.EnumerateArray())
        {
            var id = role.GetProperty("id").GetString() ?? "";
            if (memberRoles.Contains(id))
                permissions |= ParsePermissions(role.GetProperty("permissions"));
        }
        if ((permissions & Administrator) != 0) return permissions;

        if (!channel.TryGetProperty("permission_overwrites", out var overwrites))
            return permissions;

        var entries = overwrites.EnumerateArray().ToArray();
        var everyone = entries.FirstOrDefault(item =>
            item.GetProperty("type").GetInt32() == 0 &&
            (item.GetProperty("id").GetString() ?? "") == guildId);
        if (everyone.ValueKind != JsonValueKind.Undefined)
            ApplyOverwrite(ref permissions, everyone);

        ulong roleAllow = 0;
        ulong roleDeny = 0;
        foreach (var overwrite in entries.Where(item =>
                     item.GetProperty("type").GetInt32() == 0 &&
                     memberRoles.Contains(item.GetProperty("id").GetString() ?? "")))
        {
            roleAllow |= ParsePermissions(overwrite.GetProperty("allow"));
            roleDeny |= ParsePermissions(overwrite.GetProperty("deny"));
        }
        permissions &= ~roleDeny;
        permissions |= roleAllow;

        var user = entries.FirstOrDefault(item =>
            item.GetProperty("type").GetInt32() == 1 &&
            (item.GetProperty("id").GetString() ?? "") == botId);
        if (user.ValueKind != JsonValueKind.Undefined)
            ApplyOverwrite(ref permissions, user);
        return permissions;
    }

    private async Task<JsonDocument> GetJsonAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static void ApplyOverwrite(ref ulong permissions, JsonElement value)
    {
        permissions &= ~ParsePermissions(value.GetProperty("deny"));
        permissions |= ParsePermissions(value.GetProperty("allow"));
    }

    private static ulong ParsePermissions(JsonElement value) =>
        ulong.TryParse(value.GetString(), out var permissions) ? permissions : 0;

    private static bool IsAllowedWebhook(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)) &&
        uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            ? "Discord-Ratenlimit wurde erreicht."
            : $"Discord meldet {(int)response.StatusCode} {response.StatusCode}.";
        if (body.Contains("Missing Permissions", StringComparison.OrdinalIgnoreCase))
            message = "Dem Discord-Bot fehlen Berechtigungen.";
        throw new HttpRequestException(message);
    }

    private static string SafeError(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException
            ? exception.Message
            : "Discord-Channel konnte nicht geprüft werden.";

    public void Dispose() => _http.Dispose();
}
