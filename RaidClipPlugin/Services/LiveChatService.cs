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

    public ChatOverlayState GetOverlaySnapshot(IReadOnlyDictionary<string,ExternalEmote>? external=null)
    {
        lock(_sync)
        {
            if(!_config.Enabled)return ChatOverlayState.Empty();
            var messages=_history.Where(m=>m.MessageType is LiveChatMessageType.Normal or LiveChatMessageType.Command)
                .Where(m=>IsVisibleLocked(m,null)).TakeLast(30).Select(m=>
                {
                    var emotes=new Dictionary<string,ChatOverlayEmote>(StringComparer.Ordinal);
                    if(_config.EnableTwitchEmotes)
                        foreach(var e in m.Emotes.Where(e=>e.Provider=="Twitch" && System.Text.RegularExpressions.Regex.IsMatch(e.Id,@"\A[A-Za-z0-9_-]{1,100}\z")))
                            emotes[e.Code]=new(e.Code,$"https://static-cdn.jtvnw.net/emoticons/v2/{e.Id}/{(_config.EnableAnimatedEmotes?"default":"static")}/dark/2.0");
                    if(external is not null)
                        foreach(var word in m.Message.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Distinct())
                            if(external.TryGetValue(word,out var e) && (e.Provider=="BTTV" && _config.EnableBttvEmotes || e.Provider=="7TV" && _config.EnableSevenTvEmotes)){
                                var url=_config.EnableAnimatedEmotes?e.Url:e.StaticUrl;
                                if(Uri.TryCreate(url,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host is "cdn.betterttv.net" or "cdn.7tv.app")emotes[word]=new(word,url);
                            }
                    return new ChatOverlayMessage(m.Id,m.Timestamp,m.DisplayName,m.Message,m.UserColor,m.IsBroadcaster,m.IsModerator,m.IsVip,m.IsSubscriber,emotes.Values.ToArray());
                }).ToArray();
            return new(DateTimeOffset.UtcNow,messages,_config.ShowTimestamps,_config.ShowBadges,_config.ShowUserColors,_config.EmoteSize);
        }
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
