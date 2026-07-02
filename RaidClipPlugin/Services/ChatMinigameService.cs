using System.Collections.Concurrent;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ChatMinigameService : IDisposable
{
    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private readonly MinigameConfig _config;
    private readonly TwitchService _twitch;
    private readonly ViewerPointStore _points;
    private readonly object _activityLock = new();
    private readonly Dictionary<string, string> _activeUsers =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cooldownLock = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _pointsCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _gambleCooldowns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _leaderboardCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _profileCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _coinflipCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _slotsCooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _chatPointCooldowns = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _processedMessages =
        new(StringComparer.Ordinal);
    private DateTimeOffset _lastGlobalCommand = DateTimeOffset.MinValue;
    private bool _disposed;

    private int MaximumPoints => _config.MaximumAccountEnabled
        ? _config.MaximumAccountPoints
        : int.MaxValue;

    public event Action<int, int>? PointsAwarded;
    public event Action? DataChanged;

    public ChatMinigameService(
        string broadcasterId,
        string chatUserId,
        MinigameConfig config,
        TwitchService twitch,
        ViewerPointStore points)
    {
        _broadcasterId = broadcasterId;
        _chatUserId = chatUserId;
        _config = config;
        _twitch = twitch;
        _points = points;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await RunPointAwardLoopAsync(cancellationToken);
    }

    public async Task ProcessMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
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

            if (_config.PointsEnabled && !message.IsBroadcaster)
            {
                lock (_activityLock)
                {
                    _activeUsers[message.UserId] = message.UserName;
                }
            }
            if (_config.ChatPointsEnabled && !message.IsBroadcaster &&
                await TryPassiveCooldownAsync(message.UserId,
                    _config.ChatMessagePointsCooldownSeconds, cancellationToken))
            {
                await _points.AddPointsAsync(message.UserId, message.UserName,
                    _config.ChatMessagePoints, _config.MinimumPoints,
                    cancellationToken, MaximumPoints);
                DataChanged?.Invoke();
            }

            if (message.Text.StartsWith("!", StringComparison.Ordinal))
            {
                await HandleCommandAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Minigame-Chatnachricht konnte nicht verarbeitet werden: " +
                exception.Message);
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

                KeyValuePair<string, string>[] activeUsers;
                lock (_activityLock)
                {
                    activeUsers = _activeUsers.ToArray();
                    _activeUsers.Clear();
                }

                var awardedUsers = 0;
                foreach (var user in activeUsers)
                {
                    try
                    {
                        await _points.AddWatchtimeAsync(
                            user.Key, user.Value, _config.IntervalMinutes,
                            _config.PointsPerInterval, _config.MinimumPoints,
                            cancellationToken, MaximumPoints);
                        awardedUsers++;
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(
                            $"Punkte für {user.Value} konnten nicht gespeichert werden: " +
                            exception.Message);
                    }
                }

                if (awardedUsers > 0)
                {
                    PointsAwarded?.Invoke(
                        awardedUsers,
                        _config.PointsPerInterval);
                    DataChanged?.Invoke();
                    Console.WriteLine(
                        $"Minigame: {awardedUsers} aktive Zuschauer erhalten " +
                        $"je {_config.PointsPerInterval} Punkte.");
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
            var command = parts[0].ToLowerInvariant();

            if (command == "!daily")
            {
                if (!_config.DailyEnabled ||
                    !await TryEnterCooldownAsync(message.UserId, null, 0, cancellationToken)) return;
                var daily = await _points.ClaimDailyAsync(message.UserId,
                    message.UserName, _config.DailyBonusPoints,
                    _config.MinimumPoints, cancellationToken, MaximumPoints);
                var text = daily.Success
                    ? $"@{message.UserName} hat den täglichen Bonus abgeholt: +{_config.DailyBonusPoints} Punkte."
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
                        $"mit {profile.Entry.Points:N0} Punkten.",
                        cancellationToken);
                }
                else
                {
                    var requested = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 5;
                    var top = await _points.GetTopAsync(Math.Min(requested,
                        _config.MaximumTopEntries), cancellationToken);
                    var text = "Top: " + string.Join(" | ", top.Select((x,i) => $"#{i+1} {x.DisplayName}: {x.Points}"));
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
                await TrySendChatAsync($"@{message.UserName} | Punkte: {e.Points:N0} | Rang: #{p.Rank} | Watchtime: {e.WatchMinutes / 60}h | Spiele: {e.GamesPlayed} | Gamble: {e.Wins}W/{e.Losses}L | Größter Gewinn: {e.BiggestWin:N0}", cancellationToken);
                return;
            }

            if (command == "!punkte")
            {
                var action = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "";
                if (action is "add" or "remove" or "set")
                { await HandleAdminPointsCommandAsync(message, parts, action, cancellationToken); return; }
                if (!await TryEnterCooldownAsync(message.UserId, _pointsCooldowns,
                    _config.PointsCommandCooldownSeconds, cancellationToken)) return;
                var points = await _points.GetPointsAsync(message.UserId, cancellationToken);
                await TrySendChatAsync(points == 0
                    ? $"@{message.UserName}, du hast aktuell 0 Punkte."
                    : $"@{message.UserName}, du hast {points} Punkte.", cancellationToken);
                return;
            }

            if (command == "!coinflip")
            {
                if (!_config.CoinflipEnabled || parts.Length != 3 ||
                    !int.TryParse(parts[2], out var stake)) return;
                if (!await TryEnterCooldownAsync(message.UserId, _coinflipCooldowns,
                    _config.CoinflipCooldownSeconds, cancellationToken)) return;
                var choice = parts[1].ToLowerInvariant();
                if (choice is not ("kopf" or "zahl"))
                { await TrySendChatAsync($"@{message.UserName}, nutze !coinflip <kopf|zahl> <einsatz>.", cancellationToken); return; }
                if (stake < _config.CoinflipMinimumBet || stake > _config.CoinflipMaximumBet) return;
                var resultSide = Random.Shared.Next(2) == 0 ? "kopf" : "zahl";
                var payout = resultSide == choice ? (int)Math.Floor(stake * _config.CoinflipMultiplier) : 0;
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
                    !int.TryParse(parts[1], out var stake)) return;
                if (!await TryEnterCooldownAsync(message.UserId, _slotsCooldowns,
                    _config.SlotsCooldownSeconds, cancellationToken)) return;
                if (stake < _config.SlotsMinimumBet || stake > _config.SlotsMaximumBet) return;
                var symbols = _config.SlotSymbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (symbols.Length < 2) throw new InvalidOperationException("Mindestens zwei Slot-Symbole erforderlich.");
                var draw = new[] { symbols[Random.Shared.Next(symbols.Length)], symbols[Random.Shared.Next(symbols.Length)], symbols[Random.Shared.Next(symbols.Length)] };
                var multiplier = draw.All(x => x == "7️⃣") ? _config.SlotsSevenMultiplier
                    : draw.Distinct().Count() == 1 ? _config.SlotsThreeMultiplier
                    : draw.Distinct().Count() == 2 ? _config.SlotsTwoMultiplier : 0m;
                var payout = (int)Math.Floor(stake * multiplier);
                var result = await ApplyCasinoAsync(message, "Slots", stake, payout, cancellationToken);
                if (result.Success) await TrySendChatAsync($"@{message.UserName} | {string.Join(" ", draw)} | Auszahlung {payout} | Stand {result.Balance}" + JackpotText(result), cancellationToken);
                return;
            }

            if (command != "!gamble" || !_config.GambleEnabled) return;
            if (!await TryEnterCooldownAsync(message.UserId, _gambleCooldowns,
                _config.GambleCooldownSeconds, cancellationToken)) return;
            if (parts.Length != 2 || !int.TryParse(parts[1], out var gambleStake) ||
                gambleStake < _config.MinimumBet || gambleStake > _config.MaximumBet)
            { await TrySendChatAsync($"@{message.UserName}, nutze !gamble <einsatz>.", cancellationToken); return; }
            var roll = Random.Shared.Next(1, 101);
            var range = _config.GambleRanges.Single(x => roll >= x.From && roll <= x.To);
            var gamblePayout = (int)Math.Floor(gambleStake * range.Multiplier);
            var gambleResult = await ApplyCasinoAsync(message, "Gamble", gambleStake,
                gamblePayout, cancellationToken);
            if (!gambleResult.Success) return;
            var resultText = range.ChatText.Replace("{name}", message.UserName)
                .Replace("{roll}", roll.ToString()).Replace("{stake}", gambleStake.ToString())
                .Replace("{payout}", gamblePayout.ToString())
                .Replace("{balance}", gambleResult.Balance.ToString());
            await TrySendChatAsync(resultText + JackpotText(gambleResult), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        { Console.WriteLine("Minigame-Command fehlgeschlagen: " + exception.Message); }
    }

    public async Task ProcessPassiveEventAsync(
        MinigamePassiveEvent passiveEvent,
        CancellationToken cancellationToken)
    {
        try
        {
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

            if (points <= 0 || string.IsNullOrWhiteSpace(passiveEvent.UserId))
            {
                return;
            }

            var balance = await _points.AddPointsAsync(
                passiveEvent.UserId, passiveEvent.DisplayName, points,
                _config.MinimumPoints, cancellationToken, MaximumPoints);
            Console.WriteLine(
                $"Minigame: {passiveEvent.DisplayName} erhält {points} Punkte " +
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
        ChatMessage message, string game, int stake, int payout,
        CancellationToken cancellationToken)
    {
        var maximum = _config.MaximumAccountEnabled
            ? _config.MaximumAccountPoints : int.MaxValue;
        var dailyGames = _config.DailyGambleLimitEnabled
            ? _config.DailyGambleLimit : 0;
        var dailyLoss = _config.DailyLossLimitEnabled
            ? _config.DailyLossLimit : 0;
        var dailyWin = _config.DailyWinLimitEnabled
            ? _config.DailyWinLimit : 0;
        var contribution = _config.JackpotEnabled && payout < stake
            ? (int)Math.Floor(
                Math.Max(0, stake - payout) *
                _config.JackpotContributionPercent / 100m)
            : 0;
        var jackpotHit = _config.JackpotEnabled &&
            Random.Shared.NextDouble() <
            (double)(_config.JackpotChancePercent / 100m);

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

    private static string JackpotText(CasinoApplyResult result) =>
        result.JackpotWon > 0
            ? $" · JACKPOT! +{result.JackpotWon} Punkte!"
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
        if (!message.IsBroadcaster && !message.IsModerator)
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
            !int.TryParse(parts[3], out var amount) ||
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

        int newBalance;
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
            $"@{user.DisplayName} hat jetzt {newBalance} Punkte.",
            cancellationToken);
        DataChanged?.Invoke();
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

        _cooldownLock.Dispose();
        _disposed = true;
    }
}
