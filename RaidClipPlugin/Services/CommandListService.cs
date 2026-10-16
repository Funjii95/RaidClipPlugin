using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class CommandListService
{
    private readonly CommandRegistry _registry;
    private readonly string _broadcasterId;
    private readonly string _senderId;
    private readonly IClipChatClient _chat;
    private readonly ITwitchClipClient _twitch;
    private readonly Func<string, bool> _hasPermit;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastResponse = DateTimeOffset.MinValue;

    public CommandListService(CommandRegistry registry, string broadcasterId, string senderId,
        IClipChatClient chat, ITwitchClipClient twitch, Func<string, bool> hasPermit)
    {
        _registry = registry; _broadcasterId = broadcasterId; _senderId = senderId;
        _chat = chat; _twitch = twitch; _hasPermit = hasPermit;
    }

    public async Task<bool> HandleMessageAsync(ChatMessage message, CancellationToken token)
    {
        var parts = message.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var directory = _registry.Commands.FirstOrDefault(c => c.CommandId == "commands.list");
        if (parts.Length == 0 || directory is null ||
            !new[] { directory.CommandText }.Concat(directory.Aliases)
                .Contains(parts[0], StringComparer.OrdinalIgnoreCase)) return false;
        if (!directory.Enabled) return true;
        if (message.IsBot || message.CommandAuthorization == CommandAuthorization.Denied) return true;
        await _gate.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow - _lastResponse < TimeSpan.FromSeconds(5)) return true;
            _lastResponse = DateTimeOffset.UtcNow;
            var page = 1;
            if (parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], out page) || page < 1)))
            {
                await SendAsync(message, "Nutzung: !command [Seite]", token);
                return true;
            }
            var commands = await AvailableAsync(message, token);
            var pages = BuildPages(commands);
            if (page > pages.Count)
                await SendAsync(message, $"Es gibt {pages.Count} Seite(n). Nutzung: !command <1–{pages.Count}>.", token);
            else
                await SendAsync(message, $"Deine Befehle ({page}/{pages.Count}): {pages[page - 1]}" +
                    (page < pages.Count ? $" | Weiter: !command {page + 1}" : ""), token);
            return true;
        }
        finally { _gate.Release(); }
    }

    internal async Task<IReadOnlyList<ChatCommandDefinition>> AvailableAsync(ChatMessage message, CancellationToken token)
    {
        var roles = new Dictionary<CommandRole, bool>();
        var result = new List<ChatCommandDefinition>();
        foreach (var command in _registry.Commands.Where(c => c.Enabled && c.IsVisible))
        {
            if (command.ModuleId == "music")
            {
                if (MusicRequestService.CanUseMusicCommands(message, _broadcasterId) ||
                    (command.CommandId == "music.request" && _hasPermit(message.UserId))) result.Add(command);
                continue;
            }
            if (!roles.TryGetValue(command.RequiredRole, out var allowed))
            {
                allowed = await CommandPermissionService.HasRoleAsync(message, command.RequiredRole,
                    _broadcasterId, _twitch, token);
                roles[command.RequiredRole] = allowed;
            }
            if (!allowed) continue;
            result.Add(command);
        }
        return result;
    }

    internal static IReadOnlyList<string> BuildPages(IEnumerable<ChatCommandDefinition> commands)
    {
        var pages = new List<string>();
        var current = "";
        foreach (var command in commands.DistinctBy(c => c.CommandText, StringComparer.OrdinalIgnoreCase))
        {
            var text = string.IsNullOrWhiteSpace(command.Usage) ? command.CommandText : command.Usage;
            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length > 200) text = command.CommandText.Length <= 200 ? command.CommandText : command.CommandText[..197] + "…";
            if (current.Length > 0 && current.Length + 3 + text.Length > 350)
            { pages.Add(current); current = ""; }
            current += (current.Length == 0 ? "" : " | ") + text;
        }
        if (current.Length > 0) pages.Add(current);
        if (pages.Count == 0) pages.Add("Aktuell keine Befehle verfügbar.");
        return pages;
    }

    private Task SendAsync(ChatMessage message, string text, CancellationToken token) =>
        _chat.SendChatMessageAsync(_broadcasterId, _senderId, $"@{message.UserName}, {text}", token);
}
