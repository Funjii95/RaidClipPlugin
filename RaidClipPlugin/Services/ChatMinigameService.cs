using System.Collections.Concurrent;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ChatMinigameService : IDisposable
{
    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private MinigameConfig _config;
    private readonly TwitchService _twitch;
    private readonly ViewerPointStore _points;
    private readonly CommandRegistry _commandRegistry;
    private readonly HeistService? _heist;
    private readonly DuelService? _duel;
    private CommandsConfig _commandsConfig;
    private readonly object _activityLock = new();
    private readonly Dictionary<string, string> _activeUsers =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _lurkingUsers =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cooldownLock = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _pointsCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _gambleCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _jackpotCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _giveCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _leaderboardCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _profileCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _coinflipCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _slotsCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _rouletteCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _chatPointCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _commandsCooldowns = new(StringComparer.Ordinal);
    private DateTimeOffset _lastCommandsGlobal = DateTimeOffset.MinValue;
    private readonly ConcurrentDictionary<string, byte> _processedMessages =
        new(StringComparer.Ordinal);
    private DateTimeOffset _lastGlobalCommand = DateTimeOffset.MinValue;
    private bool _disposed;
    private volatile bool _isRunning;
    private DateTimeOffset _lastHeartbeatUtc = DateTimeOffset.MinValue;
    private string? _lastRuntimeError;

    public bool IsRunning => _isRunning;
    public DateTimeOffset LastHeartbeatUtc => _lastHeartbeatUtc;
    public string? LastRuntimeError => _lastRuntimeError;
    public bool IsGambleReady => _isRunning && _config.Enabled &&
        _config.GambleEnabled && !_disposed;

    public static string NormalizeIncomingCommand(string command) =>
        (command ?? "").Trim().ToLowerInvariant() switch
        {
            "!gambel" => "!gamble",
            _ => (command ?? "").Trim().ToLowerInvariant()
        };

    private void TouchHeartbeat() =>
        _lastHeartbeatUtc = DateTimeOffset.UtcNow;

    private long MaximumPoints => _config.MaximumAccountEnabled
        ? _config.MaximumAccountPoints
        : long.MaxValue;

    private string CurrencyName(long amount) => Math.Abs(amount) == 1
        ? _config.CurrencySingular
        : _config.CurrencyPlural;

    private string FormatCurrency(long amount) =>
        $"{amount:N0} {CurrencyName(amount)}";

    private static long ToNonNegativeLong(decimal value)
    {
        if (value <= 0m) return 0L;
        if (value >= long.MaxValue) return long.MaxValue;
        return (long)Math.Floor(value);
    }

    private bool IsPointsBlacklisted(string? login, string? displayName = null)
    {
        static string NormalizeName(string? value) =>
            (value ?? "").Trim().TrimStart('@');
        var normalizedLogin = NormalizeName(login);
        var normalizedDisplayName = NormalizeName(displayName);
        return _config.PointsBlacklist.Any(entry =>
            entry.Equals(normalizedLogin, StringComparison.OrdinalIgnoreCase) ||
            entry.Equals(normalizedDisplayName, StringComparison.OrdinalIgnoreCase));
    }

    public static long CalculateAllInStake(long availablePoints) =>
        Math.Max(0L, availablePoints);

    public static bool ShouldRun(MinigameConfig config) =>
        config.Enabled || config.PointsEnabled;

    public static bool IsPointSystemCommand(
        MinigameConfig config,
        string command)
    {
        var normalized = (command ?? "").Trim().ToLowerInvariant();
        return normalized is "!punkte" or "!points" or "!perlen" or
                   "!lurk" or "!unlurk" or "!daily" or "!top" or
                   "!rang" or "!profil" or "!give" or "!addpoints" or
                   "!removepoints" ||
               (!string.IsNullOrWhiteSpace(config.CustomPointsCommand) &&
                normalized.Equals(config.CustomPointsCommand.Trim(),
                    StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsGameCommand(string command) =>
        (command ?? "").Trim().ToLowerInvariant() is
            "!coinflip" or "!slots" or "!roulette" or "!gamble" or
            "!jackpot" or "!dumpjackpot" or "!addjackpot" or "!addjockpot";

    public static bool IsCommandModuleEnabled(
        MinigameConfig config,
        string command) =>
        IsPointSystemCommand(config, command)
            ? config.PointsEnabled
            : IsGameCommand(command) && config.Enabled;

    private bool IsPointsQueryCommand(string command) =>
        (_config.PointsCommandPunkteEnabled && command == "!punkte") ||
        (_config.PointsCommandPointsEnabled && command == "!points") ||
        (_config.PointsCommandPerlenEnabled && command == "!perlen") ||
        (!string.IsNullOrWhiteSpace(_config.CustomPointsCommand) &&
         command.Equals(_config.CustomPointsCommand,
             StringComparison.OrdinalIgnoreCase));

    private string LocalizeCurrencyText(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(
                text, @"\bPunkte\b", _config.CurrencyPlural,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            @"\bPunkt\b", _config.CurrencySingular,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private bool SkipBlacklisted(string? login, string? displayName = null)
    {
        if (!IsPointsBlacklisted(login, displayName)) return false;
        Console.WriteLine(
            $"Punktevergabe übersprungen: {displayName ?? login} befindet sich auf der Punkte-Blacklist.");
        return true;
    }

    public event Action<int, long>? PointsAwarded;
    public event Action? DataChanged;
    public event Action<HeistStatus>? HeistStatusChanged;
    public event Action<DuelStatus>? DuelStatusChanged;

    public ChatMinigameService(
        string broadcasterId,
        string chatUserId,
        MinigameConfig config,
        TwitchService twitch,
        ViewerPointStore points,
        HeistConfig? heistConfig = null,
        DuelConfig? duelConfig = null,
        CommandsConfig? commandsConfig = null,
        CommandRegistry? commandRegistry = null)
    {
        _broadcasterId = broadcasterId;
        _chatUserId = chatUserId;
        _config = config;
        _twitch = twitch;
        _points = points;
        _commandsConfig = commandsConfig ?? new CommandsConfig();
        _commandRegistry = commandRegistry ?? new CommandRegistry();
        if (heistConfig is not null)
        {
            _heist = new HeistService(broadcasterId, chatUserId, heistConfig, config, twitch, points);
            _heist.StatusChanged += status => HeistStatusChanged?.Invoke(status);
        }
        if (duelConfig is not null)
        {
            _duel = new DuelService(broadcasterId, chatUserId, duelConfig, config, twitch, points);
            _duel.StatusChanged += status => DuelStatusChanged?.Invoke(status);
        }
    }

    public void UpdateConfig(MinigameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public void UpdateConfiguration(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config.Minigame;
        _commandsConfig = config.Commands;
        _commandRegistry.Update(config);
        _heist?.UpdateConfig(config.Heist, config.Minigame);
        _duel?.UpdateConfig(config.Duel, config.Minigame);
    }

    public Task RunTestHeistAsync(CancellationToken cancellationToken) =>
        _heist?.RunTestAsync(cancellationToken) ?? Task.CompletedTask;

    public Task CancelHeistAsync(CancellationToken cancellationToken) =>
        _heist?.CancelAsync(true, cancellationToken) ?? Task.CompletedTask;

    public Task RunTestDuelAsync(CancellationToken cancellationToken) =>
        _duel?.RunTestAsync(cancellationToken) ?? Task.CompletedTask;

    public Task CancelDuelsAsync(CancellationToken cancellationToken) =>
        _duel?.CancelAllAsync(true, cancellationToken) ?? Task.CompletedTask;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isRunning = true;
        _lastRuntimeError = null;
        TouchHeartbeat();
        Console.WriteLine("Chat-Minigame Modul gestartet.");
        try
        {
            await Task.WhenAll(
                RunPointAwardLoopAsync(cancellationToken),
                RunHeartbeatLoopAsync(cancellationToken));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
            Console.WriteLine("Chat-Minigame Modul ausgefallen: " + exception);
            throw;
        }
        finally
        {
            _isRunning = false;
            Console.WriteLine("Chat-Minigame Modul gestoppt.");
        }
    }

    private async Task RunHeartbeatLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TouchHeartbeat();
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    public async Task ProcessMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            TouchHeartbeat();
            if (!ShouldRun(_config) && _heist is null && _duel is null && !_commandsConfig.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Id) ||
                !_processedMessages.TryAdd(message.Id, 0))
            {
                return;
            }

            if (_processedMessages.Count > 2_000)
            {
                _processedMessages.Clear();
                _processedMessages.TryAdd(message.Id, 0);
            }

            var pointsBlacklisted = IsPointsBlacklisted(message.UserName);
            if (_config.PointsEnabled && !pointsBlacklisted)
            {
                lock (_activityLock)
                {
                    _activeUsers[message.UserId] = message.UserName;
                }
            }
            if (_config.PointsEnabled && _config.ChatPointsEnabled &&
                !pointsBlacklisted &&
                await TryPassiveCooldownAsync(message.UserId,
                    _config.ChatMessagePointsCooldownSeconds, cancellationToken))
            {
                await _points.AddPointsAsync(message.UserId, message.UserName,
                    _config.ChatMessagePoints, _config.MinimumPoints,
                    cancellationToken, MaximumPoints);
                DataChanged?.Invoke();
            }

            var parsedCommand = ChatCommandParser.Parse(message.Text);
            if (parsedCommand.IsCommand)
            {
                await HandleCommandAsync(message, cancellationToken);
            }
            else
            {
                Console.WriteLine(
                    $"Minigame ignoriert Nachricht von {message.UserName}: {parsedCommand.IgnoreReason}");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
            Console.WriteLine(
                "Minigame-Chatnachricht konnte nicht verarbeitet werden: " +
                exception);
        }
    }

    private async Task RunPointAwardLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_config.IntervalMinutes),
                    cancellationToken);

                if (!_config.PointsEnabled)
                {
                    continue;
                }

                var config = _config;
                Dictionary<string, string> activeUsers;
                HashSet<string> lurkingUsers;
                lock (_activityLock)
                {
                    activeUsers = new Dictionary<string, string>(
                        _activeUsers, StringComparer.Ordinal);
                    lurkingUsers = new HashSet<string>(
                        _lurkingUsers, StringComparer.Ordinal);
                    _activeUsers.Clear();
                }

                List<TwitchUser> chatters;
                try
                {
                    chatters = await _twitch.GetChattersAsync(
                        _broadcasterId, _chatUserId, cancellationToken);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "Twitch-Chatter konnten nicht geladen werden; " +
                        "aktive Zuschauer werden als Ersatz verwendet: " +
                        exception.Message);
                    chatters = activeUsers.Select(user => new TwitchUser(
                        user.Key, "", user.Value)).ToList();
                }

                var eligibleChatters = chatters
                    .Where(user =>
                        (!user.Id.Equals(_chatUserId, StringComparison.Ordinal) ||
                         user.Id.Equals(_broadcasterId, StringComparison.Ordinal)) &&
                        !IsPointsBlacklisted(user.Login, user.DisplayName))
                    .GroupBy(user => user.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
                var awards = eligibleChatters.Select(user =>
                {
                    var isLurking = lurkingUsers.Contains(user.Id) ||
                                    !activeUsers.ContainsKey(user.Id);
                    return (
                        UserId: user.Id,
                        DisplayName: user.DisplayName,
                        Points: isLurking
                            ? config.LurkerPointsPerInterval
                            : config.PointsPerInterval);
                }).ToArray();

                var awardedUsers = await _points.AwardAttendanceAsync(
                    awards,
                    config.IntervalMinutes,
                    config.MinimumPoints,
                    MaximumPoints,
                    cancellationToken);
                var activeCount = awards.Count(award =>
                    award.Points == config.PointsPerInterval &&
                    activeUsers.ContainsKey(award.UserId) &&
                    !lurkingUsers.Contains(award.UserId));
                var lurkerCount = awards.Length - activeCount;

                if (awardedUsers > 0)
                {
                    PointsAwarded?.Invoke(
                        awardedUsers,
                        config.PointsPerInterval);
                    DataChanged?.Invoke();
                    Console.WriteLine(
                        $"Minigame-Anwesenheit: {activeCount} aktive Zuschauer " +
                        $"erhalten je {FormatCurrency(config.PointsPerInterval)}; " +
                        $"{lurkerCount} stille Zuschauer/Lurker erhalten je " +
                        $"{FormatCurrency(config.LurkerPointsPerInterval)}.");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Minigame-Punkteintervall fehlgeschlagen: " +
                    exception.Message);
            }
        }
    }

    private async Task HandleCommandAsync(ChatMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var parts = message.Text.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            var command = NormalizeIncomingCommand(parts[0]);
            parts[0] = command;

            if (_commandsConfig.Enabled &&
                command == CommandRegistry.Normalize(_commandsConfig.Command))
            {
                await HandleCommandsCommandAsync(message, parts, cancellationToken);
                return;
            }

            if (_duel?.Recognizes(command) == true)
            {
                await _duel.ProcessAsync(message, cancellationToken);
                return;
            }

            if (_heist?.Recognizes(command) == true)
            {
                await _heist.ProcessAsync(message, command, cancellationToken);
                return;
            }

            if (!IsPointSystemCommand(_config, command) &&
                !IsGameCommand(command))
            {
                Console.WriteLine(
                    $"Unbekannter Minigame-Command {command} ignoriert.");
                return;
            }

            if (!IsCommandModuleEnabled(_config, command))
            {
                Console.WriteLine(
                    $"Command {command} ignoriert: Modul ist laut Konfiguration deaktiviert.");
                return;
            }

            if (command is "!lurk" or "!unlurk")
            {
                lock (_activityLock)
                {
                    if (command == "!lurk")
                    {
                        _lurkingUsers.Add(message.UserId);
                    }
                    else
                    {
                        _lurkingUsers.Remove(message.UserId);
                    }
                }

                await TrySendChatAsync(
                    command == "!lurk"
                        ? $"@{message.UserName} ist jetzt im Lurk und erhält weiterhin Anwesenheitspunkte."
                        : $"@{message.UserName} ist zurück und erhält wieder normale Anwesenheitspunkte.",
                    cancellationToken);
                return;
            }

            if (command == "!daily")
            {
                if (SkipBlacklisted(message.UserName)) return;
                if (!_config.DailyEnabled ||
                    !await TryEnterCooldownAsync(message.UserId, null, 0, cancellationToken)) return;
                var daily = await _points.ClaimDailyAsync(message.UserId,
                    message.UserName, _config.DailyBonusPoints,
                    _config.MinimumPoints, cancellationToken, MaximumPoints);
                var text = daily.Success
                    ? $"@{message.UserName} hat den täglichen Bonus abgeholt: +{FormatCurrency(_config.DailyBonusPoints)}."
                    : $"@{message.UserName}, dein Daily ist wieder verfügbar in {(int)daily.Remaining.TotalHours:00}:{daily.Remaining.Minutes:00}.";
                await TrySendChatAsync(text, cancellationToken);
                if (daily.Success) DataChanged?.Invoke();
                return;
            }

            if (command is "!top" or "!rang")
            {
                if (!_config.LeaderboardEnabled ||
                    !await TryEnterCooldownAsync(message.UserId, _leaderboardCooldowns,
                        _config.LeaderboardCooldownSeconds, cancellationToken)) return;
                if (command == "!rang")
                {
                    var targetId = message.UserId;
                    var targetName = message.UserName;
                    if (parts.Length > 1 &&
                        (message.IsBroadcaster || message.IsModerator))
                    {
                        var login = parts[1].TrimStart('@');
                        var target = await _twitch.GetUserAsync(
                            login, cancellationToken);
                        if (target is null)
                        {
                            await TrySendChatAsync(
                                $"@{message.UserName}, Nutzer @{login} nicht gefunden.",
                                cancellationToken);
                            return;
                        }
                        targetId = target.Id;
                        targetName = target.DisplayName;
                    }
                    var profile = await _points.GetProfileAsync(
                        targetId, cancellationToken);
                    await TrySendChatAsync(
                        $"@{targetName} ist auf Rang #{profile.Rank} " +
                        $"mit {FormatCurrency(profile.Entry.Points)}.",
                        cancellationToken);
                }
                else
                {
                    var requested = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 5;
                    var top = await _points.GetTopAsync(Math.Min(requested,
                        _config.MaximumTopEntries), cancellationToken);
                    var text = "Top: " + string.Join(" | ", top.Select((x,i) =>
                        $"#{i+1} {x.DisplayName}: {FormatCurrency(x.Points)}"));
                    await TrySendChatAsync(text, cancellationToken);
                }
                return;
            }

            if (command == "!profil")
            {
                if (!_config.ProfileEnabled ||
                    !await TryEnterCooldownAsync(message.UserId, _profileCooldowns,
                        _config.ProfileCooldownSeconds, cancellationToken)) return;
                var p = await _points.GetProfileAsync(message.UserId, cancellationToken);
                var e = p.Entry;
                await TrySendChatAsync(
                    $"@{message.UserName} | {_config.CurrencyPlural}: {e.Points:N0} | " +
                    $"Rang: #{p.Rank} | Watchtime: {e.WatchMinutes / 60}h | " +
                    $"Spiele: {e.GamesPlayed} | Gamble: {e.Wins}W/{e.Losses}L | " +
                    $"Größter Gewinn: {FormatCurrency(e.BiggestWin)}",
                    cancellationToken);
                return;
            }

            if (command == "!punkte")
            {
                var action = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "";
                if (action is "add" or "remove" or "set")
                {
                    await HandleAdminPointsCommandAsync(
                        message, parts, action, cancellationToken);
                    return;
                }
            }

            if (IsPointsQueryCommand(command))
            {
                if (!await TryEnterCooldownAsync(message.UserId, _pointsCooldowns,
                    _config.PointsCommandCooldownSeconds, cancellationToken)) return;
                var points = await _points.GetPointsAsync(
                    message.UserId, cancellationToken);
                await TrySendChatAsync(
                    $"@{message.UserName}, du besitzt aktuell {FormatCurrency(points)}.",
                    cancellationToken);
                return;
            }

            if (command == "!give")
            {
                await HandleGiveCommandAsync(message, parts, cancellationToken);
                return;
            }

            if (command == "!addpoints")
            {
                await HandleAddPointsCommandAsync(
                    message, parts, cancellationToken);
                return;
            }

            if (command == "!removepoints")
            {
                await HandleRemovePointsCommandAsync(
                    message, parts, cancellationToken);
                return;
            }

            if (command is "!addjackpot" or "!addjockpot")
            {
                if (!CommandPermissionService.Resolve(message,
                        message.IsBroadcaster || message.IsModerator))
                {
                    return;
                }
                if (parts.Length != 2 ||
                    !long.TryParse(parts[1], out var jackpotAmount) ||
                    jackpotAmount <= 0)
                {
                    await TrySendChatAsync(
                        $"@{message.UserName}, nutze !addjackpot <betrag>.",
                        cancellationToken);
                    return;
                }
                var updated = await _points.AddJackpotAsync(
                    jackpotAmount, _config.JackpotStartValue,
                    cancellationToken);
                Console.WriteLine(
                    $"Minigame-Admin: {message.UserName} erhöht den Jackpot " +
                    $"von {updated.Previous} auf {updated.Current}.");
                await TrySendChatAsync(
                    $"🎰 Jackpot +{FormatCurrency(jackpotAmount)} · Neuer Stand: " +
                    $"{FormatCurrency(updated.Current)}.", cancellationToken);
                DataChanged?.Invoke();
                return;
            }

            if (command == "!dumpjackpot")
            {
                if (!CanUseRemovePointsCommand(message, _broadcasterId))
                {
                    Console.WriteLine(
                        $"Minigame-Admin: !dumpjackpot von {message.UserName} abgelehnt; " +
                        "nur der Broadcaster darf diesen Befehl verwenden.");
                    await TrySendChatAsync(
                        $"@{message.UserName}, !dumpjackpot darf nur der Broadcaster verwenden.",
                        cancellationToken);
                    return;
                }

                var dumped = await _points.ResetJackpotAsync(
                    _config.JackpotStartValue,
                    cancellationToken);
                Console.WriteLine(
                    $"Minigame-Admin: Broadcaster {message.UserName} leert den Jackpot " +
                    $"von {dumped.Previous} auf {dumped.Current}.");
                await TrySendChatAsync(
                    $"🎰 Jackpot geleert: {FormatCurrency(dumped.Previous)} wurden entfernt. " +
                    $"Neuer Stand: {FormatCurrency(dumped.Current)}.",
                    cancellationToken);
                DataChanged?.Invoke();
                return;
            }

            if (command == "!jackpot")
            {
                if (!await TryEnterCooldownWithReplyAsync(
                        message,
                        message.UserId,
                        _jackpotCooldowns,
                        _config.GambleCooldownSeconds,
                        "!jackpot",
                        cancellationToken))
                {
                    return;
                }

                var jackpot = await _points.GetJackpotAsync(
                    _config.JackpotStartValue,
                    cancellationToken);
                await TrySendChatAsync(
                    $"🎰 Aktueller Jackpot: {FormatCurrency(jackpot)}.",
                    cancellationToken);
                Console.WriteLine(
                    $"Jackpot-Abfrage von {message.UserName}: {jackpot}.");
                return;
            }

            if (command == "!coinflip")
            {
                if (!_config.CoinflipEnabled || parts.Length != 3 ||
                    !long.TryParse(parts[2], out var stake)) return;
                if (!await TryEnterCooldownWithReplyAsync(message, message.UserId,
                    _coinflipCooldowns, _config.CoinflipCooldownSeconds,
                    "!coinflip", cancellationToken)) return;
                var choice = parts[1].ToLowerInvariant();
                if (choice is not ("kopf" or "zahl"))
                { await TrySendChatAsync($"@{message.UserName}, nutze !coinflip <kopf|zahl> <einsatz>.", cancellationToken); return; }
                if (stake < _config.CoinflipMinimumBet || stake > _config.CoinflipMaximumBet) return;
                var resultSide = Random.Shared.Next(2) == 0 ? "kopf" : "zahl";
                var payout = resultSide == choice
                    ? ToNonNegativeLong(stake * _config.CoinflipMultiplier)
                    : 0L;
                var result = await ApplyCasinoAsync(
                    message, "Coinflip", stake, payout, cancellationToken);
                if (result.Success)
                {
                    var outcome = payout > 0 ? $"Gewinn {payout}" : "Verloren";
                    await TrySendChatAsync(
                        $"@{message.UserName}: {resultSide}! {outcome} · " +
                        $"Stand {result.Balance}{JackpotText(result)}",
                        cancellationToken);
                }
                return;
            }

            if (command == "!slots")
            {
                if (!_config.SlotsEnabled || parts.Length != 2 ||
                    !long.TryParse(parts[1], out var stake)) return;
                if (!await TryEnterCooldownWithReplyAsync(message, message.UserId,
                    _slotsCooldowns, _config.SlotsCooldownSeconds,
                    "!slots", cancellationToken)) return;
                if (stake < _config.SlotsMinimumBet || stake > _config.SlotsMaximumBet) return;
                var symbols = _config.SlotSymbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (symbols.Length < 2) throw new InvalidOperationException("Mindestens zwei Slot-Symbole erforderlich.");
                var draw = new[] { symbols[Random.Shared.Next(symbols.Length)], symbols[Random.Shared.Next(symbols.Length)], symbols[Random.Shared.Next(symbols.Length)] };
                var multiplier = draw.All(x => x == "7️⃣") ? _config.SlotsSevenMultiplier
                    : draw.Distinct().Count() == 1 ? _config.SlotsThreeMultiplier
                    : draw.Distinct().Count() == 2 ? _config.SlotsTwoMultiplier : 0m;
                var payout = ToNonNegativeLong(stake * multiplier);
                var result = await ApplyCasinoAsync(message, "Slots", stake, payout, cancellationToken);
                if (result.Success) await TrySendChatAsync($"@{message.UserName} | {string.Join(" ", draw)} | Auszahlung {payout} | Stand {result.Balance}" + JackpotText(result), cancellationToken);
                return;
            }

            if (command == "!roulette")
            {
                if (!_config.RouletteEnabled) return;
                if (parts.Length != 3 ||
                    !long.TryParse(parts[2], out var rouletteStake) ||
                    !RouletteRules.TryParseBet(parts[1], out var rouletteBet))
                {
                    await TrySendChatAsync(
                        $"@{message.UserName}, nutze !roulette " +
                        "<rot|schwarz|gerade|ungerade|niedrig|hoch|0-36> <einsatz>.",
                        cancellationToken);
                    return;
                }
                if (!await TryEnterCooldownWithReplyAsync(message, message.UserId,
                    _rouletteCooldowns, _config.RouletteCooldownSeconds,
                    "!roulette",
                    cancellationToken)) return;
                if (rouletteStake < _config.RouletteMinimumBet ||
                    rouletteStake > _config.RouletteMaximumBet)
                {
                    await TrySendChatAsync(
                        $"@{message.UserName}, der Roulette-Einsatz muss zwischen " +
                        $"{FormatCurrency(_config.RouletteMinimumBet)} und " +
                        $"{FormatCurrency(_config.RouletteMaximumBet)} liegen.",
                        cancellationToken);
                    return;
                }

                var rouletteNumber = Random.Shared.Next(0, 37);
                var rouletteWon = RouletteRules.IsWinner(
                    rouletteBet, rouletteNumber);
                var rouletteMultiplier = rouletteBet.Kind ==
                    RouletteBetKind.Number
                    ? _config.RouletteNumberMultiplier
                    : _config.RouletteEvenMoneyMultiplier;
                var roulettePayoutValue = rouletteWon
                    ? Math.Floor(rouletteStake * rouletteMultiplier)
                    : 0m;
                var roulettePayout = ToNonNegativeLong(roulettePayoutValue);
                var rouletteResult = await ApplyCasinoAsync(
                    message, "Roulette", rouletteStake, roulettePayout,
                    cancellationToken);
                if (!rouletteResult.Success)
                {
                    await TrySendChatAsync(
                        $"@{message.UserName}, {rouletteResult.Error}. " +
                        $"Stand: {FormatCurrency(rouletteResult.Balance)}.",
                        cancellationToken);
                    return;
                }

                var rouletteOutcome = rouletteWon
                    ? $"Tipp {rouletteBet.DisplayName} gewinnt · Auszahlung " +
                      FormatCurrency(roulettePayout)
                    : $"Tipp {rouletteBet.DisplayName} verliert";
                await TrySendChatAsync(
                    $"@{message.UserName}: Die Kugel fällt auf {rouletteNumber} " +
                    $"{RouletteRules.ColorName(rouletteNumber)}. " +
                    $"{rouletteOutcome} · Stand " +
                    $"{FormatCurrency(rouletteResult.Balance)}" +
                    JackpotText(rouletteResult),
                    cancellationToken);
                return;
            }

            if (command != "!gamble" || !_config.GambleEnabled) return;
            Console.WriteLine($"!gamble Command empfangen von {message.UserName}.");
            if (!await TryEnterCooldownWithReplyAsync(message, message.UserId,
                _gambleCooldowns, _config.GambleCooldownSeconds,
                "!gamble", cancellationToken)) return;
            if (parts.Length != 2)
            {
                await TrySendChatAsync(
                    $"@{message.UserName}, nutze !gamble <einsatz|all>.",
                    cancellationToken);
                return;
            }

            var isAllIn = parts[1].Equals(
                "all", StringComparison.OrdinalIgnoreCase);
            var availableForGamble = isAllIn
                ? Math.Max(0L,
                    await _points.GetPointsAsync(
                        message.UserId, cancellationToken) -
                    Math.Max(0L, _config.MinimumPoints))
                : 0L;
            var gambleStake = isAllIn
                ? CalculateAllInStake(availableForGamble)
                : long.TryParse(parts[1], out var parsedStake)
                    ? parsedStake
                    : -1;

            if (gambleStake < _config.MinimumBet ||
                (!isAllIn && gambleStake > _config.MaximumBet))
            {
                await TrySendChatAsync(
                    isAllIn
                        ? $"@{message.UserName}, du hast nicht genug verfügbare {_config.CurrencyPlural} für All-in."
                        : $"@{message.UserName}, nutze !gamble <einsatz|all>.",
                    cancellationToken);
                return;
            }
            var roll = Random.Shared.Next(1, 101);
            var range = _config.GambleRanges.Single(x => roll >= x.From && roll <= x.To);
            var payoutValue = Math.Floor(
                gambleStake * range.Multiplier);
            var gamblePayout = ToNonNegativeLong(payoutValue);
            var gambleResult = await ApplyCasinoAsync(message, "Gamble", gambleStake,
                gamblePayout, cancellationToken, forceJackpot: roll == 100);
            if (!gambleResult.Success) return;
            Console.WriteLine($"!gamble erfolgreich verarbeitet für {message.UserName}; Stand {gambleResult.Balance}.");
            if (roll == 100 && gambleResult.JackpotWon > 0)
            {
                Console.WriteLine(
                    $"JACKPOT: {message.UserName} gewinnt " +
                    $"{FormatCurrency(gambleResult.JackpotWon)}; neuer Jackpot " +
                    $"{_config.JackpotStartValue:N0}.");
                await TrySendChatAsync(
                    $"🎉 JACKPOT! {message.UserName} hat eine 100 gewürfelt " +
                    $"und gewinnt den gesamten Jackpot von " +
                    $"{FormatCurrency(gambleResult.JackpotWon)}! Der Jackpot wurde " +
                    $"auf {FormatCurrency(_config.JackpotStartValue)} zurückgesetzt. " +
                    $"Neuer Stand: {gambleResult.Balance:N0}.",
                    cancellationToken);
                return;
            }

            var resultText = LocalizeCurrencyText(range.ChatText)
                .Replace("{name}", message.UserName)
                .Replace("{roll}", roll.ToString()).Replace("{stake}", gambleStake.ToString())
                .Replace("{payout}", gamblePayout.ToString())
                .Replace("{balance}", gambleResult.Balance.ToString());
            await TrySendChatAsync(resultText + JackpotText(gambleResult), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _lastRuntimeError = exception.ToString();
            TouchHeartbeat();
            Console.WriteLine("Minigame-Command fehlgeschlagen: " + exception);
            var failedCommand = message.Text.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (NormalizeIncomingCommand(failedCommand) == "!gamble")
            {
                await TrySendChatAsync(
                    $"@{message.UserName}, Gamble ist gerade kurz nicht verfügbar. Bitte versuche es erneut.",
                    cancellationToken);
            }
        }
    }

    public async Task ProcessPassiveEventAsync(
        MinigamePassiveEvent passiveEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_config.PointsEnabled)
            {
                return;
            }
            var points = passiveEvent.Kind switch
            {
                MinigamePassiveEventKind.Follow
                    when _config.FollowPointsEnabled => _config.FollowPoints,
                MinigamePassiveEventKind.Subscription
                    when _config.SubPointsEnabled => _config.SubPoints,
                MinigamePassiveEventKind.Raid
                    when _config.RaidPointsEnabled => _config.RaidPoints,
                MinigamePassiveEventKind.ChannelReward
                    when _config.ChannelRewardPointsEnabled =>
                    _config.ChannelRewardPoints,
                _ => 0
            };

            if (points <= 0 || string.IsNullOrWhiteSpace(passiveEvent.UserId) ||
                SkipBlacklisted(passiveEvent.DisplayName))
            {
                return;
            }

            var balance = await _points.AddPointsAsync(
                passiveEvent.UserId, passiveEvent.DisplayName, points,
                _config.MinimumPoints, cancellationToken, MaximumPoints);
            Console.WriteLine(
                $"Minigame: {passiveEvent.DisplayName} erhält {FormatCurrency(points)} " +
                $"für {passiveEvent.Kind}. Neuer Stand: {balance}.");
            DataChanged?.Invoke();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Passive Minigame-Punkte konnten nicht vergeben werden: " +
                exception.Message);
        }
    }

    private async Task<CasinoApplyResult> ApplyCasinoAsync(
        ChatMessage message, string game, long stake, long payout,
        CancellationToken cancellationToken, bool forceJackpot = false)
    {
        if (SkipBlacklisted(message.UserName))
        {
            return new CasinoApplyResult(false,
                "Dieses Konto nimmt nicht am Punktesystem teil", 0, 0);
        }
        var maximum = _config.MaximumAccountEnabled
            ? _config.MaximumAccountPoints : long.MaxValue;
        var dailyGames = _config.DailyGambleLimitEnabled
            ? _config.DailyGambleLimit : 0;
        var dailyLoss = _config.DailyLossLimitEnabled
            ? _config.DailyLossLimit : 0;
        var dailyWin = _config.DailyWinLimitEnabled
            ? _config.DailyWinLimit : 0;
        var contribution = _config.JackpotEnabled && payout < stake
            ? ToNonNegativeLong(
                Math.Max(0L, stake - payout) *
                _config.JackpotContributionPercent / 100m)
            : 0L;
        var jackpotHit = _config.JackpotEnabled && forceJackpot;

        var result = await _points.ApplyCasinoAsync(
            message.UserId, message.UserName, game, stake, payout,
            _config.MinimumPoints, maximum, dailyGames, dailyLoss, dailyWin,
            contribution, jackpotHit, _config.JackpotStartValue,
            _config.HistoryEnabled ? _config.HistoryLimit : 0,
            cancellationToken);

        if (!result.Success)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, {result.Error}.", cancellationToken);
            return result;
        }

        Console.WriteLine(
            $"Minigame {game}: {message.UserName}, Einsatz {stake}, " +
            $"Auszahlung {payout}, Stand {result.Balance}.");
        return result;
    }

    private string JackpotText(CasinoApplyResult result) =>
        result.JackpotWon > 0
            ? $" · JACKPOT! +{FormatCurrency(result.JackpotWon)}!"
            : "";

    private async Task<bool> TryPassiveCooldownAsync(
        string userId, int seconds, CancellationToken cancellationToken)
    {
        await _cooldownLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_chatPointCooldowns.TryGetValue(userId, out var last) &&
                now - last < TimeSpan.FromSeconds(seconds))
            {
                return false;
            }

            _chatPointCooldowns[userId] = now;
            return true;
        }
        finally
        {
            _cooldownLock.Release();
        }
    }

    private async Task HandleAdminPointsCommandAsync(
        ChatMessage message,
        string[] parts,
        string action,
        CancellationToken cancellationToken)
    {
        if (!CommandPermissionService.Resolve(message,
                message.IsBroadcaster || message.IsModerator))
        {
            return;
        }

        if (!await TryEnterCooldownAsync(
                message.UserId,
                null,
                0,
                cancellationToken))
        {
            return;
        }

        if (parts.Length != 4 ||
            !long.TryParse(parts[3], out var amount) ||
            amount < 0)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, nutze !punkte add/remove/set <user> <betrag>.",
                cancellationToken);
            return;
        }

        var login = parts[2].Trim().TrimStart('@');
        var user = await _twitch.GetUserAsync(login, cancellationToken);
        if (user is null)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, Twitch-Nutzer @{login} wurde nicht gefunden.",
                cancellationToken);
            return;
        }

        if (SkipBlacklisted(user.Login, user.DisplayName)) return;

        long newBalance;
        switch (action)
        {
            case "add":
                newBalance = await _points.AddPointsAsync(
                    user.Id,
                    user.DisplayName,
                    amount,
                    _config.MinimumPoints,
                    cancellationToken, MaximumPoints);
                break;

            case "remove":
                newBalance = await _points.RemovePointsAsync(
                    user.Id,
                    user.DisplayName,
                    amount,
                    _config.MinimumPoints,
                    cancellationToken, MaximumPoints);
                break;

            case "set":
                newBalance = await _points.SetPointsAsync(
                    user.Id,
                    user.DisplayName,
                    amount,
                    _config.MinimumPoints,
                    cancellationToken, MaximumPoints);
                break;

            default:
                return;
        }

        Console.WriteLine(
            $"Minigame-Admin: {message.UserName} führt {action} " +
            $"für {user.DisplayName} mit {amount} aus. Neuer Stand: {newBalance}.");
        await TrySendChatAsync(
            $"@{user.DisplayName} hat jetzt {FormatCurrency(newBalance)}.",
            cancellationToken);
        DataChanged?.Invoke();
    }

    private async Task HandleGiveCommandAsync(
        ChatMessage message,
        string[] parts,
        CancellationToken cancellationToken)
    {
        if (SkipBlacklisted(message.UserName)) return;
        if (!await TryEnterCooldownAsync(
                message.UserId, _giveCooldowns,
                _config.PointsCommandCooldownSeconds, cancellationToken))
        {
            return;
        }

        if (parts.Length == 3 &&
            parts[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (!CommandPermissionService.Resolve(message,
                message.IsBroadcaster || message.IsModerator))
            {
                return;
            }

            if (!long.TryParse(parts[2], out var amountForAll) ||
                amountForAll <= 0)
            {
                await TrySendChatAsync(
                    $"@{message.UserName}, nutze !give all <betrag>.",
                    cancellationToken);
                return;
            }

            var chatters = await _twitch.GetChattersAsync(
                _broadcasterId, _chatUserId, cancellationToken);
            var recipients = chatters
                .Where(user =>
                    !user.Id.Equals(_broadcasterId, StringComparison.Ordinal) &&
                    !user.Id.Equals(_chatUserId, StringComparison.Ordinal) &&
                    !IsPointsBlacklisted(user.Login, user.DisplayName))
                .Select(user => (user.Id, user.DisplayName))
                .ToArray();
            var recipientCount = await _points.AddPointsToUsersAsync(
                recipients, amountForAll, _config.MinimumPoints,
                MaximumPoints, cancellationToken);

            if (recipientCount == 0)
            {
                await TrySendChatAsync(
                    $"@{message.UserName}, aktuell wurden keine Chatnutzer erfasst.",
                    cancellationToken);
                return;
            }

            Console.WriteLine(
                $"Minigame-Admin: {message.UserName} vergibt jeweils " +
                $"{FormatCurrency(amountForAll)} an {recipientCount} Chatnutzer.");
            await TrySendChatAsync(
                $"Allen {recipientCount} erfassten Chatnutzern wurden jeweils " +
                $"{FormatCurrency(amountForAll)} gutgeschrieben.",
                cancellationToken);
            DataChanged?.Invoke();
            return;
        }

        if (parts.Length != 3 ||
            !long.TryParse(parts[2], out var amount) || amount <= 0)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, nutze !give @name <betrag>.",
                cancellationToken);
            return;
        }

        var login = parts[1].Trim().TrimStart('@');
        var recipient = await _twitch.GetUserAsync(login, cancellationToken);
        if (recipient is null)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, Twitch-Nutzer @{login} wurde nicht gefunden.",
                cancellationToken);
            return;
        }

        if (SkipBlacklisted(recipient.Login, recipient.DisplayName)) return;

        var result = await _points.TransferPointsAsync(
            message.UserId, message.UserName,
            recipient.Id, recipient.DisplayName, amount,
            _config.MinimumPoints, MaximumPoints, cancellationToken);

        if (!result.Success)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, {result.Error}.", cancellationToken);
            return;
        }

        Console.WriteLine(
            $"Minigame-Geschenk: {message.UserName} schenkt " +
            $"{recipient.DisplayName} {FormatCurrency(amount)}. " +
            $"Neue Stände: {result.SenderBalance}/{result.RecipientBalance}.");
        await TrySendChatAsync(
            $"@{message.UserName} schenkt @{recipient.DisplayName} " +
            $"{FormatCurrency(amount)}! Neuer Stand: {FormatCurrency(result.SenderBalance)}.",
            cancellationToken);
        DataChanged?.Invoke();
    }

    private async Task HandleAddPointsCommandAsync(
        ChatMessage message,
        string[] parts,
        CancellationToken cancellationToken)
    {
        if (!CommandPermissionService.Resolve(message,
                message.IsBroadcaster || message.IsModerator))
        {
            return;
        }

        if (!await TryEnterCooldownAsync(
                message.UserId, null, 0, cancellationToken))
        {
            return;
        }

        if (parts.Length != 3 ||
            !long.TryParse(parts[2], out var amount) || amount <= 0)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, nutze !addpoints <@name|all> <betrag>.",
                cancellationToken);
            return;
        }

        if (parts[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var recipients = await _points.AddPointsToAllAsync(
                amount, _config.MinimumPoints, MaximumPoints,
                cancellationToken);
            Console.WriteLine(
                $"Minigame-Admin: {message.UserName} erzeugt " +
                $"{FormatCurrency(amount)} für alle {recipients} gespeicherten Nutzer.");
            await TrySendChatAsync(
                $"@{message.UserName} hat {FormatCurrency(amount)} an " +
                $"{recipients} Nutzer vergeben.", cancellationToken);
            DataChanged?.Invoke();
            return;
        }

        var login = parts[1].Trim().TrimStart('@');
        var user = await _twitch.GetUserAsync(login, cancellationToken);
        if (user is null)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, Twitch-Nutzer @{login} wurde nicht gefunden.",
                cancellationToken);
            return;
        }

        if (SkipBlacklisted(user.Login, user.DisplayName)) return;

        var newBalance = await _points.AddPointsAsync(
            user.Id, user.DisplayName, amount, _config.MinimumPoints,
            cancellationToken, MaximumPoints);
        Console.WriteLine(
            $"Minigame-Admin: {message.UserName} erzeugt {FormatCurrency(amount)} " +
            $"für {user.DisplayName}. Neuer Stand: {newBalance}.");
        await TrySendChatAsync(
            $"@{user.DisplayName} erhält {FormatCurrency(amount)} und hat jetzt " +
            $"{FormatCurrency(newBalance)}.", cancellationToken);
        DataChanged?.Invoke();
    }

    public static bool CanUseRemovePointsCommand(
        ChatMessage message,
        string broadcasterId) =>
        CommandPermissionService.Resolve(message,
            message.IsBroadcaster ||
            (!string.IsNullOrWhiteSpace(message.UserId) &&
             message.UserId.Equals(broadcasterId, StringComparison.Ordinal)));

    private async Task HandleRemovePointsCommandAsync(
        ChatMessage message,
        string[] parts,
        CancellationToken cancellationToken)
    {
        if (!CanUseRemovePointsCommand(message, _broadcasterId))
        {
            Console.WriteLine(
                $"Minigame-Admin: !removepoints von {message.UserName} abgelehnt; " +
                "nur der Broadcaster darf diesen Befehl verwenden.");
            await TrySendChatAsync(
                $"@{message.UserName}, !removepoints darf nur der Broadcaster verwenden.",
                cancellationToken);
            return;
        }

        if (parts.Length != 2)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, nutze !removepoints @name.",
                cancellationToken);
            return;
        }

        var login = parts[1].Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(login))
        {
            await TrySendChatAsync(
                $"@{message.UserName}, nutze !removepoints @name.",
                cancellationToken);
            return;
        }

        var user = await _twitch.GetUserAsync(login, cancellationToken);
        if (user is null)
        {
            await TrySendChatAsync(
                $"@{message.UserName}, Twitch-Nutzer @{login} wurde nicht gefunden.",
                cancellationToken);
            return;
        }

        var newBalance = await _points.SetPointsAsync(
            user.Id, user.DisplayName, 0, 0,
            cancellationToken, MaximumPoints);
        Console.WriteLine(
            $"Minigame-Admin: Broadcaster {message.UserName} setzt die Punkte " +
            $"von {user.DisplayName} auf 0. Neuer Stand: {newBalance}.");
        await TrySendChatAsync(
            $"@{user.DisplayName}s Punktestand wurde vom Broadcaster auf 0 gesetzt.",
            cancellationToken);
        DataChanged?.Invoke();
    }

    private async Task HandleCommandsCommandAsync(
        ChatMessage message,
        string[] parts,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await _cooldownLock.WaitAsync(cancellationToken);
        try
        {
            if (now - _lastCommandsGlobal < TimeSpan.FromSeconds(_commandsConfig.GlobalCooldownSeconds) ||
                _commandsCooldowns.TryGetValue(message.UserId, out var last) &&
                now - last < TimeSpan.FromSeconds(_commandsConfig.UserCooldownSeconds))
            {
                Console.WriteLine($"!commands von {message.UserName} wegen Cooldown abgelehnt.");
                return;
            }
            _lastCommandsGlobal = now;
            _commandsCooldowns[message.UserId] = now;
        }
        finally { _cooldownLock.Release(); }

        var available = _commandRegistry.VisibleFor(message);
        var request = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "1";
        Console.WriteLine($"!commands-Aufruf von {message.UserName}; Anforderung: {request}.");
        if (request == "help")
        {
            await TrySendChatAsync($"@{message.UserName}, nutze {_commandsConfig.Command} [Seite] oder {_commandsConfig.Command} <Modul>, z. B. {_commandsConfig.Command} heist.", cancellationToken);
            return;
        }

        if (!int.TryParse(request, out var page))
        {
            available = available.Where(command =>
                command.ModuleId.Equals(request, StringComparison.OrdinalIgnoreCase) ||
                command.ModuleDisplayName.Equals(request, StringComparison.OrdinalIgnoreCase) ||
                (request == "musik" && command.ModuleId == "music") ||
                (request == "clips" && command.ModuleId == "clips")).ToArray();
            page = 1;
        }

        var pageSize = Math.Max(1, _commandsConfig.CommandsPerPage);
        var pages = Math.Max(1, (int)Math.Ceiling(available.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        var selected = available.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var messages = new List<string> { $"@{message.UserName}, verfügbare Commands – Seite {page}/{pages}:" };
        foreach (var group in selected.GroupBy(command => command.ModuleDisplayName))
        {
            var entries = group.Select(command => _commandsConfig.ShowDescriptions
                ? $"{command.Usage} – {command.Description}"
                : command.Usage + (_commandsConfig.ShowAliases && command.Aliases.Count > 0
                    ? $" ({string.Join(", ", command.Aliases)})" : ""));
            messages.Add($"{group.Key}: {string.Join(" | ", entries)}");
        }
        if (page < pages) messages.Add($"Weitere Seiten: {_commandsConfig.Command} {page + 1} | Nach Modul: {_commandsConfig.Command} heist");

        var sent = 0;
        foreach (var raw in messages)
        {
            if (sent >= Math.Clamp(_commandsConfig.MaximumMessagesPerRequest, 1, 5)) break;
            var text = raw.Length <= 480 ? raw : raw[..477] + "…";
            await TrySendChatAsync(text, cancellationToken);
            sent++;
            if (sent < messages.Count) await Task.Delay(250, cancellationToken);
        }
    }

    private async Task<bool> TryEnterCooldownAsync(
        string userId,
        Dictionary<string, DateTimeOffset>? userCooldowns,
        int userCooldownSeconds,
        CancellationToken cancellationToken)
    {
        await _cooldownLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastGlobalCommand <
                TimeSpan.FromSeconds(_config.GlobalCommandCooldownSeconds))
            {
                return false;
            }

            if (userCooldowns is not null &&
                userCooldowns.TryGetValue(userId, out var lastUse) &&
                now - lastUse < TimeSpan.FromSeconds(userCooldownSeconds))
            {
                return false;
            }

            _lastGlobalCommand = now;
            if (userCooldowns is not null)
            {
                userCooldowns[userId] = now;
            }
            return true;
        }
        finally
        {
            _cooldownLock.Release();
        }
    }

    private async Task<bool> TryEnterCooldownWithReplyAsync(
        ChatMessage message,
        string userId,
        Dictionary<string, DateTimeOffset>? userCooldowns,
        int userCooldownSeconds,
        string commandName,
        CancellationToken cancellationToken)
    {
        TimeSpan? remaining = null;
        await _cooldownLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var globalCooldown =
                TimeSpan.FromSeconds(_config.GlobalCommandCooldownSeconds);
            var sinceGlobal = now - _lastGlobalCommand;
            if (sinceGlobal < globalCooldown)
            {
                remaining = globalCooldown - sinceGlobal;
                return false;
            }

            if (userCooldowns is not null &&
                userCooldowns.TryGetValue(userId, out var lastUse))
            {
                var userCooldown = TimeSpan.FromSeconds(userCooldownSeconds);
                var sinceUser = now - lastUse;
                if (sinceUser < userCooldown)
                {
                    remaining = userCooldown - sinceUser;
                    return false;
                }
            }

            _lastGlobalCommand = now;
            if (userCooldowns is not null)
            {
                userCooldowns[userId] = now;
            }

            return true;
        }
        finally
        {
            _cooldownLock.Release();
            if (remaining is { } value)
            {
                await TrySendChatAsync(
                    $"@{message.UserName}, {commandName} ist noch " +
                    $"{FormatCooldown(value)} im Cooldown.",
                    cancellationToken);
            }
        }
    }

    private static string FormatCooldown(TimeSpan remaining)
    {
        var seconds = Math.Max(0,
            (int)Math.Ceiling(remaining.TotalSeconds));
        return seconds >= 60
            ? $"{seconds / 60}m {seconds % 60}s"
            : $"{seconds}s";
    }

    private async Task TrySendChatAsync(
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _twitch.SendChatMessageAsync(
                _broadcasterId,
                _chatUserId,
                message,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Minigame-Chatantwort konnte nicht gesendet werden: " +
                exception.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_heist is not null)
            _heist.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (_duel is not null)
            _duel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _isRunning = false;
        _cooldownLock.Dispose();
        _disposed = true;
    }
}
