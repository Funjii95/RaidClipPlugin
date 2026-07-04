using System.Collections.Concurrent;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class CommandPermissionService
{
    private readonly string _broadcasterId;
    private readonly string _senderId;
    private readonly IClipChatClient _chat;
    private readonly ITwitchClipClient _twitch;
    private readonly CommandRegistry _registry;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _denialCooldowns = new();
    private CommandsConfig _config;

    public CommandPermissionService(string broadcasterId, string senderId,
        CommandsConfig config, IClipChatClient chat, ITwitchClipClient twitch,
        CommandRegistry registry)
    {
        _broadcasterId = broadcasterId;
        _senderId = senderId;
        _config = config;
        _chat = chat;
        _twitch = twitch;
        _registry = registry;
    }

    public void UpdateConfig(CommandsConfig config) => _config = config;

    public async Task AuthorizeAsync(ChatMessage message,
        CancellationToken cancellationToken)
    {
        message.CommandAuthorization = CommandAuthorization.Default;
        var input = CommandRegistry.Normalize(message.Text);
        var definition = _registry.Commands
            .Where(item => item.Enabled)
            .SelectMany(item => new[] { item.CommandText }.Concat(item.Aliases)
                .Select(text => new { Definition = item, Text = CommandRegistry.Normalize(text) }))
            .Where(item => input.Equals(item.Text, StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith(item.Text + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Text.Length)
            .Select(item => item.Definition)
            .FirstOrDefault();
        if (definition is null) return;

        var explicitlyConfigured = definition.ModuleId.Equals("custom", StringComparison.Ordinal) ||
            _config.CommandRoleOverrides.ContainsKey(definition.CommandId);
        if (!explicitlyConfigured) return;

        var allowed = await HasRoleAsync(message, definition.RequiredRole,
            _broadcasterId, _twitch, cancellationToken);
        message.CommandAuthorization = allowed
            ? CommandAuthorization.Allowed : CommandAuthorization.Denied;
        if (allowed) return;

        Console.WriteLine($"Command {definition.CommandText} von {message.UserName} wegen Berechtigung {definition.RequiredRole} abgelehnt.");
        var key = definition.CommandId + ":" +
            (string.IsNullOrWhiteSpace(message.UserId) ? message.UserLogin : message.UserId);
        var now = DateTimeOffset.UtcNow;
        if (_denialCooldowns.TryGetValue(key, out var last) &&
            now - last < TimeSpan.FromSeconds(15)) return;
        _denialCooldowns[key] = now;
        try
        {
            await _chat.SendChatMessageAsync(_broadcasterId, _senderId,
                $"@{message.UserName}, du darfst {definition.CommandText} nicht verwenden. Benötigte Rolle: {RoleText(definition.RequiredRole)}.",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Console.WriteLine("Command-Berechtigungsantwort fehlgeschlagen: " + exception.Message);
        }
    }

    public static async Task<bool> HasRoleAsync(ChatMessage message,
        CommandRole role, string broadcasterId, ITwitchClipClient twitch,
        CancellationToken cancellationToken)
    {
        if (message.IsBroadcaster || message.UserId.Equals(broadcasterId, StringComparison.Ordinal))
            return true;
        return role switch
        {
            CommandRole.Broadcaster => false,
            CommandRole.Moderator => message.IsModerator,
            CommandRole.Vip => message.IsModerator || message.IsVip,
            CommandRole.Subscriber => message.IsModerator || message.IsVip || message.IsSubscriber,
            CommandRole.Follower => message.IsModerator || message.IsVip || message.IsSubscriber ||
                await twitch.IsFollowerAsync(broadcasterId, message.UserId, cancellationToken),
            _ => true
        };
    }

    public static bool Resolve(ChatMessage message, bool fallback) =>
        message.CommandAuthorization switch
        {
            CommandAuthorization.Allowed => true,
            CommandAuthorization.Denied => false,
            _ => fallback
        };

    private static string RoleText(CommandRole role) => role switch
    {
        CommandRole.Viewer => "Zuschauer",
        CommandRole.Follower => "Follower",
        CommandRole.Subscriber => "Subscriber",
        CommandRole.Vip => "VIP",
        CommandRole.Moderator => "Moderator",
        CommandRole.Broadcaster => "Broadcaster",
        _ => role.ToString()
    };
}

public sealed class CustomCommandService
{
    private readonly string _broadcasterId;
    private readonly string _senderId;
    private readonly IClipChatClient _chat;
    private readonly ITwitchClipClient _twitch;
    private readonly SemaphoreSlim _cooldownLock = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _globalCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _userCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private CommandsConfig _config;

    public CustomCommandService(string broadcasterId, string senderId,
        CommandsConfig config, IClipChatClient chat, ITwitchClipClient twitch)
    {
        _broadcasterId = broadcasterId;
        _senderId = senderId;
        _config = config;
        _chat = chat;
        _twitch = twitch;
    }

    public void UpdateConfig(CommandsConfig config) => _config = config;

    public async Task<bool> HandleMessageAsync(ChatMessage message,
        CancellationToken cancellationToken)
    {
        if (!_config.CustomCommandsEnabled || string.IsNullOrWhiteSpace(message.Text))
            return false;
        var parts = message.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var input = CommandRegistry.Normalize(message.Text);
        var match = _config.CustomCommands
            .Where(item => item.Enabled)
            .SelectMany(item => new[] { item.Command }.Concat(item.Aliases)
                .Select(text => new { Command = item, Text = CommandRegistry.Normalize(text) }))
            .Where(item => input.Equals(item.Text, StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith(item.Text + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Text.Length)
            .FirstOrDefault();
        if (match is null) return false;
        var command = match.Command;
        var commandText = match.Text;
        if (message.CommandAuthorization == CommandAuthorization.Denied) return true;
        if (message.CommandAuthorization == CommandAuthorization.Default &&
            !await CommandPermissionService.HasRoleAsync(message,
                CommandRegistry.ParseRole(command.RequiredRole), _broadcasterId,
                _twitch, cancellationToken))
            return true;
        if (!await EnterCooldownAsync(command, message, cancellationToken))
            return true;

        var commandParts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var args = parts.Length > commandParts ? string.Join(' ', parts.Skip(commandParts)) : "";
        var target = parts.Length > commandParts ? parts[commandParts].TrimStart('@') : message.UserName;
        var response = Format(command.Response, message, args, target);
        if (string.IsNullOrWhiteSpace(response)) return true;
        try
        {
            await _chat.SendChatMessageAsync(_broadcasterId, _senderId,
                response.Length <= 480 ? response : response[..477] + "…",
                cancellationToken);
            Console.WriteLine($"Custom Command {commandText} für {message.UserName} beantwortet.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            Console.WriteLine($"Custom Command {commandText} konnte nicht antworten: {exception.Message}");
        }
        return true;
    }

    private async Task<bool> EnterCooldownAsync(CustomChatCommandConfig command,
        ChatMessage message, CancellationToken cancellationToken)
    {
        await _cooldownLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var id = string.IsNullOrWhiteSpace(command.Id) ? command.Command : command.Id;
            var user = string.IsNullOrWhiteSpace(message.UserId) ? message.UserLogin : message.UserId;
            if (_globalCooldowns.TryGetValue(id, out var global) &&
                now - global < TimeSpan.FromSeconds(Math.Max(0, command.GlobalCooldownSeconds))) return false;
            var userKey = id + ":" + user;
            if (_userCooldowns.TryGetValue(userKey, out var personal) &&
                now - personal < TimeSpan.FromSeconds(Math.Max(0, command.UserCooldownSeconds))) return false;
            _globalCooldowns[id] = now;
            _userCooldowns[userKey] = now;
            return true;
        }
        finally { _cooldownLock.Release(); }
    }

    public static string Format(string template, ChatMessage message,
        string args, string target)
    {
        var result = (template ?? "").Trim();
        foreach (var replacement in new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{user}"] = message.UserName,
            ["{username}"] = message.UserName,
            ["{login}"] = message.UserLogin,
            ["{args}"] = args,
            ["{target}"] = target,
            ["{command}"] = message.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ""
        })
            result = result.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
        return result;
    }
}
