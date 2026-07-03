using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class DiscordClipService : IDisposable
{
    private readonly IDiscordClipClient _client;
    private readonly DiscordCredentials _credentials;
    private readonly ClipTemplateService _templates;
    private DiscordClipsConfig _config;
    private bool _disposed;

    public DiscordClipService(
        DiscordClipsConfig config,
        DiscordCredentials credentials,
        IDiscordClipClient client,
        ClipTemplateService templates)
    {
        _config = config;
        _credentials = credentials;
        _client = client;
        _templates = templates;
    }

    public void UpdateConfig(DiscordClipsConfig config) => _config = config;

    public async Task<IReadOnlyList<DiscordChannelValidation>>
        ValidateConfiguredChannelsAsync(CancellationToken cancellationToken)
    {
        var results = new List<DiscordChannelValidation>();
        foreach (var channel in _config.Channels.Where(item => item.Enabled))
        {
            if (channel.UseWebhook &&
                _credentials.WebhookUrls.TryGetValue(
                    channel.ChannelId, out var webhook) &&
                !string.IsNullOrWhiteSpace(webhook))
            {
                results.Add(await _client.ValidateWebhookAsync(
                    webhook, _config.GuildId, channel.ChannelId,
                    cancellationToken));
            }
            else
            {
                results.Add(await _client.ValidateChannelAsync(
                    _config.GuildId, channel.ChannelId, cancellationToken));
            }
        }
        return results;
    }

    public async Task<DiscordClipPostResult> PostClipAsync(
        ClipDiscordContext context,
        CancellationToken cancellationToken)
    {
        var deliveries = new List<DiscordClipDelivery>();
        if (!_config.Enabled)
            return new DiscordClipPostResult(deliveries);

        var enabledChannels = _config.Channels
            .Where(item => item.Enabled)
            .ToArray();
        if (enabledChannels.Length == 0)
        {
            deliveries.Add(new DiscordClipDelivery(
                "", false,
                "Discord ist aktiviert, aber es ist kein Ziel-Channel aktiv."));
            Console.WriteLine(
                "Discord-Clip übersprungen: kein aktiver Ziel-Channel.");
            return new DiscordClipPostResult(deliveries);
        }

        foreach (var channel in enabledChannels)
        {
            try
            {
                var template = string.IsNullOrWhiteSpace(channel.MessageTemplate)
                    ? _config.MessageTemplate
                    : channel.MessageTemplate;
                var payload = BuildPayload(_config, template, context, _templates);
                if (channel.UseWebhook)
                {
                    if (!_credentials.WebhookUrls.TryGetValue(
                            channel.ChannelId, out var webhook) ||
                        string.IsNullOrWhiteSpace(webhook))
                        throw new InvalidOperationException(
                            "Für diesen Channel ist keine Webhook-URL gespeichert.");
                    Console.WriteLine(
                        $"Sende Discord-Clip über Webhook für Channel {channel.ChannelId}.");
                    await _client.SendWebhookAsync(
                        webhook, payload, cancellationToken);
                }
                else
                {
                    await _client.SendMessageAsync(
                        channel.ChannelId, payload, cancellationToken);
                }
                deliveries.Add(new(channel.ChannelId, true));
                Console.WriteLine(
                    $"Discord-Clip erfolgreich in Channel {channel.ChannelId} gesendet.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var safe = SafeError(exception);
                deliveries.Add(new(channel.ChannelId, false, safe));
                Console.WriteLine(
                    $"Discord-Clip in Channel {channel.ChannelId} fehlgeschlagen: {safe}");
            }
        }
        return new DiscordClipPostResult(deliveries);
    }

    public async Task SendTestMessageAsync(
        string channelId,
        CancellationToken cancellationToken)
    {
        var channel = _config.Channels.FirstOrDefault(item =>
            item.ChannelId.Equals(channelId, StringComparison.Ordinal));
        if (channel is null)
            throw new InvalidOperationException("Discord-Channel ist nicht konfiguriert.");
        var now = DateTimeOffset.Now;
        var context = new ClipDiscordContext(
            new PublishedClip("test", "https://clips.twitch.tv/", "Testclip", "", 30),
            "RaidClip Testnachricht", "Testnutzer", "Testkanal", "Just Chatting", now);
        var template = string.IsNullOrWhiteSpace(channel.MessageTemplate)
            ? _config.MessageTemplate : channel.MessageTemplate;
        var payload = BuildPayload(_config, template, context, _templates);
        if (channel.UseWebhook)
        {
            if (!_credentials.WebhookUrls.TryGetValue(channelId, out var webhook) ||
                string.IsNullOrWhiteSpace(webhook))
                throw new InvalidOperationException("Webhook-URL fehlt.");
            await _client.SendWebhookAsync(webhook, payload, cancellationToken);
        }
        else
        {
            await _client.SendMessageAsync(channelId, payload, cancellationToken);
        }
    }

    public static object BuildPayload(
        DiscordClipsConfig config,
        string template,
        ClipDiscordContext context,
        ClipTemplateService templates)
    {
        var message = templates.ApplyClipTemplate(template, context);
        if (message.Length > 3900) message = message[..3900];
        var roleId = NormalizeSnowflake(config.MentionRoleId);
        var mention = roleId.Length > 0 ? $"<@&{roleId}>" : "";
        var allowedMentions = new
        {
            parse = Array.Empty<string>(),
            roles = roleId.Length > 0 ? new[] { roleId } : Array.Empty<string>(),
            users = Array.Empty<string>(),
            replied_user = false
        };

        if (!config.UseEmbed)
        {
            var content = string.IsNullOrWhiteSpace(mention)
                ? message : mention + "\n" + message;
            return new { content = Limit(content, 2000), allowed_mentions = allowedMentions };
        }

        var fields = new object[]
        {
            new { name = "Erstellt von", value =
                ClipTemplateService.SanitizeDiscordUserContent(context.Username), inline = true },
            new { name = "Spiel", value =
                ClipTemplateService.SanitizeDiscordUserContent(context.Game), inline = true },
            new { name = "Twitch-Kanal", value =
                ClipTemplateService.SanitizeDiscordUserContent(context.Channel), inline = true },
            new { name = "Uhrzeit", value =
                context.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), inline = true }
        };
        var embed = new Dictionary<string, object>
        {
            ["title"] = "🎬 Neuer Twitch-Clip",
            ["description"] = message,
            ["url"] = context.Clip.Url,
            ["color"] = ParseColor(config.EmbedColor),
            ["fields"] = fields
        };
        if (config.UseThumbnail &&
            !string.IsNullOrWhiteSpace(context.ThumbnailUrl))
            embed["thumbnail"] = new { url = context.ThumbnailUrl };
        if (!string.IsNullOrWhiteSpace(config.FooterText))
            embed["footer"] = new { text = Limit(config.FooterText, 2048) };
        return new
        {
            content = mention,
            embeds = new object[] { embed },
            allowed_mentions = allowedMentions
        };
    }

    private static int ParseColor(string value)
    {
        var text = (value ?? "").Trim().TrimStart('#');
        return int.TryParse(text,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out var color) ? Math.Clamp(color, 0, 0xFFFFFF) : 0x9146FF;
    }

    private static string NormalizeSnowflake(string? value) =>
        ulong.TryParse((value ?? "").Trim(), out var id) ? id.ToString() : "";

    private static string Limit(string value, int limit) =>
        value.Length <= limit ? value : value[..limit];

    private static string SafeError(Exception exception) =>
        exception is HttpRequestException or InvalidOperationException or TaskCanceledException
            ? exception.Message
            : "Discord-Versand ist fehlgeschlagen.";

    public void Dispose()
    {
        if (_disposed) return;
        if (_client is IDisposable disposable) disposable.Dispose();
        _disposed = true;
    }
}