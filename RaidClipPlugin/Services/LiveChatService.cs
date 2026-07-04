using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class LiveChatService
{
    private readonly object _sync = new();
    private readonly List<LiveChatMessage> _history = new();
    private readonly string _botUserId;
    private readonly HashSet<string> _botNames;
    private readonly CommandRegistry _commands;
    private LiveChatConfig _config;
    private bool _paused;

    public event Action<LiveChatMessage>? MessageAdded;
    public event Action? HistoryChanged;

    public LiveChatService(LiveChatConfig config, string botUserId,
        IEnumerable<string> botNames, CommandRegistry commands)
    {
        _config = NormalizeConfig(config);
        _botUserId = botUserId;
        _botNames = botNames.Select(NormalizeName).Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _commands = commands;
        Console.WriteLine(_config.Enabled ? "Livechat aktiviert." : "Livechat deaktiviert.");
        Console.WriteLine("Emote-Cache " + (_config.CacheEmotes ? "aktiviert." : "deaktiviert."));
    }

    public bool IsPaused { get { lock (_sync) return _paused; } }
    public int StoredCount { get { lock (_sync) return _history.Count; } }

    public static LiveChatConfig NormalizeConfig(LiveChatConfig? config)
    {
        config ??= new LiveChatConfig();
        if (config.MaxMessages is < 100 or > 10000)
        {
            Console.WriteLine($"Ungültiger Livechat-MaxMessages-Wert {config.MaxMessages}; Standard 1000 wird verwendet.");
            config.MaxMessages = 1000;
        }
        if (config.EmoteSize is < 16 or > 64)
        {
            Console.WriteLine($"Ungültige Livechat-EmoteSize {config.EmoteSize}; Standard 28 wird verwendet.");
            config.EmoteSize = 28;
        }
        if (config.PopoutWidth is < 360 or > 4000) config.PopoutWidth = 520;
        if (config.PopoutHeight is < 420 or > 4000) config.PopoutHeight = 760;
        if (config.PopoutLeft < -1) config.PopoutLeft = -1;
        if (config.PopoutTop < -1) config.PopoutTop = -1;
        return config;
    }

    public void UpdateConfig(LiveChatConfig config)
    {
        lock (_sync)
        {
            _config = NormalizeConfig(config);
            TrimLocked();
        }
        HistoryChanged?.Invoke();
    }

    public bool ProcessMessage(ChatMessage source)
    {
        LiveChatMessage message;
        lock (_sync)
        {
            if (!_config.Enabled || _paused) return false;
            var command = IsRegisteredCommand(source.Text);
            var bot = source.IsBot || source.UserId.Equals(_botUserId, StringComparison.Ordinal) ||
                      _botNames.Contains(NormalizeName(source.UserLogin)) ||
                      _botNames.Contains(NormalizeName(source.UserName));
            message = new LiveChatMessage(
                source.Id, source.ReceivedAt, source.UserLogin, source.UserName,
                Limit(source.Text), source.Text, source.UserColor,
                source.IsBroadcaster, source.IsModerator, source.IsVip,
                source.IsSubscriber, bot, command,
                source.Badges.ToArray(), source.Emotes.ToArray(),
                command ? LiveChatMessageType.Command : LiveChatMessageType.Normal);
            _history.Add(message);
            TrimLocked();
        }
        MessageAdded?.Invoke(message);
        return true;
    }

    public void AddSystemMessage(string text, LiveChatMessageType type = LiveChatMessageType.System)
    {
        LiveChatMessage message;
        lock (_sync)
        {
            if (!_config.Enabled || _paused) return;
            message = new LiveChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.Now,
                "system", "System", Limit(text), text, "", false, false, false,
                false, true, false, Array.Empty<string>(), Array.Empty<ChatEmoteFragment>(), type);
            _history.Add(message);
            TrimLocked();
        }
        MessageAdded?.Invoke(message);
    }

    public bool IsVisible(LiveChatMessage message, string? search = null)
    {
        lock (_sync) return IsVisibleLocked(message, search);
    }

    public IReadOnlyList<LiveChatMessage> GetVisibleSnapshot(string? search = null)
    {
        lock (_sync) return _history.Where(x => IsVisibleLocked(x, search)).ToArray();
    }

    private bool IsVisibleLocked(LiveChatMessage message, string? search)
    {
        if (_config.HideCommands && message.IsCommand) return false;
        if (_config.HideBotMessages && message.IsBot) return false;
        if (!_config.ShowSystemMessages && message.MessageType is
            LiveChatMessageType.System or LiveChatMessageType.Deleted or LiveChatMessageType.Timeout or LiveChatMessageType.Ban) return false;
        var term = (search ?? "").Trim();
        return term.Length == 0 || message.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            message.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            message.Message.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public void SetPaused(bool paused)
    {
        lock (_sync) _paused = paused;
        Console.WriteLine(paused ? "Livechat pausiert." : "Livechat fortgesetzt.");
        HistoryChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_sync) _history.Clear();
        Console.WriteLine("Livechat-Historie geleert.");
        HistoryChanged?.Invoke();
    }

    private bool IsRegisteredCommand(string text)
    {
        var first = (text ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (!first.StartsWith('!')) return false;
        var normalized = CommandRegistry.Normalize(first);
        return _commands.Commands.Any(command =>
            command.CommandText.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            command.Aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private void TrimLocked()
    {
        var excess = _history.Count - _config.MaxMessages;
        if (excess > 0) _history.RemoveRange(0, excess);
    }

    private static string NormalizeName(string? value) =>
        (value ?? "").Trim().TrimStart('@');

    private static string Limit(string? value)
    {
        var text = value ?? "";
        return text.Length <= 2000 ? text : text[..1997] + "…";
    }
}
