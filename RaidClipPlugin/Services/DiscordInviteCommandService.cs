using System.Collections.Concurrent;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class DiscordInviteCommandService
{
    private readonly string _broadcasterId;
    private readonly string _senderId;
    private readonly TwitchService _twitch;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUses =
        new(StringComparer.Ordinal);
    private DiscordClipsConfig _config;

    public DiscordInviteCommandService(
        string broadcasterId,
        string senderId,
        DiscordClipsConfig config,
        TwitchService twitch)
    {
        _broadcasterId = broadcasterId;
        _senderId = senderId;
        _config = config;
        _twitch = twitch;
    }

    public void UpdateConfig(DiscordClipsConfig config) => _config = config;

    public async Task ProcessMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_config.InviteCommandEnabled ||
                !IsCommand(message.Text, _config.InviteCommand)) return;
            if (!IsValidInviteUrl(_config.InviteUrl))
            {
                Console.WriteLine(
                    "Discord-Einladungsbefehl ignoriert: Einladungslink fehlt oder ist ungültig.");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var cooldown = TimeSpan.FromSeconds(
                Math.Clamp(_config.InviteCooldownSeconds, 0, 86400));
            if (_lastUses.TryGetValue(message.UserId, out var last) &&
                now - last < cooldown) return;
            _lastUses[message.UserId] = now;

            var template = string.IsNullOrWhiteSpace(_config.InviteMessage)
                ? "@{username}, komm auf unseren Discord: {inviteUrl}"
                : _config.InviteMessage;
            var response = template
                .Replace("{username}", message.UserName,
                    StringComparison.OrdinalIgnoreCase)
                .Replace("{inviteUrl}", _config.InviteUrl,
                    StringComparison.OrdinalIgnoreCase);
            await _twitch.SendChatMessageAsync(
                _broadcasterId, _senderId, response, cancellationToken);
            Console.WriteLine(
                $"Discord-Einladungslink an {message.UserName} gesendet.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Discord-Einladungsbefehl fehlgeschlagen: " + exception.Message);
        }
    }

    public static bool IsCommand(string text, string command)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(command)) return false;
        return text.Trim().Equals(command.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidInviteUrl(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.Host.TrimStart().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];
        return host.Equals("discord.gg", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) &&
               uri.AbsolutePath.StartsWith("/invite/",
                   StringComparison.OrdinalIgnoreCase);
    }
}
