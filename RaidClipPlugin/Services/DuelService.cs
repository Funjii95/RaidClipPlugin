using System.Security.Cryptography;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public interface IDuelTwitchClient
{
    Task<TwitchUser?> GetUserAsync(string login, CancellationToken cancellationToken);
    Task SendChatMessageAsync(string broadcasterId, string senderId, string message, CancellationToken cancellationToken);
    Task<bool> IsFollowerAsync(string broadcasterId, string userId, CancellationToken cancellationToken);
}

public interface IDuelRandom
{
    int NextInclusive(int minimum, int maximum);
}

public sealed class CryptoDuelRandom : IDuelRandom
{
    public int NextInclusive(int minimum, int maximum) =>
        RandomNumberGenerator.GetInt32(minimum, checked(maximum + 1));
}

public static class DuelRules
{
    public static int ChallengerChance(bool fairMode, int configuredChance) =>
        fairMode ? 50 : Math.Clamp(configuredChance, 1, 99);

    public static bool ChallengerWins(bool fairMode, int configuredChance, int roll) =>
        roll >= 1 && roll <= 100 && roll <= ChallengerChance(fairMode, configuredChance);
}

public enum DuelState { Waiting, Accepted, Denied, Expired, Paid, Cancelled }

public sealed record DuelStatus(
    int OpenRequests,
    string Challenger,
    string Target,
    int Stake,
    int SecondsRemaining,
    DuelState State,
    bool TestMode = false);

public sealed class DuelService : IAsyncDisposable
{
    private sealed class PendingDuel
    {
        public required string Id { get; init; }
        public required ChatMessage Challenger { get; init; }
        public required TwitchUser Target { get; init; }
        public required int Stake { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required CancellationTokenSource TimeoutCts { get; init; }
        public DuelState State { get; set; } = DuelState.Waiting;
        public Task? TimeoutTask { get; set; }
    }

    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private readonly IDuelTwitchClient _twitch;
    private readonly ViewerPointStore _points;
    private readonly IDuelRandom _random;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, PendingDuel> _byUserId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _userCooldowns = new(StringComparer.Ordinal);
    private DuelConfig _config;
    private MinigameConfig _minigame;
    private DateTimeOffset _lastGlobalUse = DateTimeOffset.MinValue;
    private bool _disposed;

    public event Action<DuelStatus>? StatusChanged;

    public DuelService(string broadcasterId, string chatUserId, DuelConfig config,
        MinigameConfig minigame, IDuelTwitchClient twitch, ViewerPointStore points,
        IDuelRandom? random = null)
    {
        _broadcasterId = broadcasterId;
        _chatUserId = chatUserId;
        _config = config;
        _minigame = minigame;
        _twitch = twitch;
        _points = points;
        _random = random ?? new CryptoDuelRandom();
    }

    public bool Recognizes(string command)
    {
        var normalized = CommandRegistry.Normalize(command);
        return normalized == CommandRegistry.Normalize(_config.DuelCommand) ||
               normalized == CommandRegistry.Normalize(_config.AcceptCommand) ||
               normalized == CommandRegistry.Normalize(_config.DenyCommand);
    }

    public void UpdateConfig(DuelConfig config, MinigameConfig minigame)
    {
        _config = config;
        _minigame = minigame;
    }

    public async Task ProcessAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        var parts = message.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var command = CommandRegistry.Normalize(parts[0]);
        if (!Recognizes(command)) return;
        if (!_config.Enabled)
        {
            await SendAsync($"@{message.UserName}, das Duel-Modul ist aktuell deaktiviert.", cancellationToken);
            return;
        }

        if (command == CommandRegistry.Normalize(_config.AcceptCommand))
        {
            await AcceptAsync(message, cancellationToken);
            return;
        }
        if (command == CommandRegistry.Normalize(_config.DenyCommand))
        {
            await DenyAsync(message, cancellationToken);
            return;
        }
        await ChallengeAsync(message, parts, cancellationToken);
    }

    private async Task ChallengeAsync(ChatMessage challenger, string[] parts,
        CancellationToken cancellationToken)
    {
        if (parts.Length != 3)
        {
            await SendTemplateAsync(_config.InvalidBetMessage, challenger.UserName, null, 0, "", "", cancellationToken);
            return;
        }
        if (!await IsAllowedAsync(challenger, cancellationToken))
        {
            await SendAsync($"@{challenger.UserName}, du darfst aktuell kein Duel starten.", cancellationToken);
            return;
        }

        var targetLogin = parts[1].Trim().TrimStart('@');
        if (targetLogin.Length == 0)
        {
            await SendTemplateAsync(_config.InvalidBetMessage, challenger.UserName, null, 0, "", "", cancellationToken);
            return;
        }
        TwitchUser? target;
        try { target = await _twitch.GetUserAsync(targetLogin, cancellationToken); }
        catch (Exception exception)
        {
            Console.WriteLine("Duel-Ziel konnte nicht geladen werden: " + exception.Message);
            await SendAsync($"@{challenger.UserName}, der Zielspieler konnte nicht gefunden werden.", cancellationToken);
            return;
        }
        if (target is null || string.IsNullOrWhiteSpace(target.Id))
        {
            await SendAsync($"@{challenger.UserName}, der Zielspieler wurde nicht gefunden.", cancellationToken);
            return;
        }
        if (target.Id.Equals(challenger.UserId, StringComparison.Ordinal) ||
            target.Login.Equals(challenger.UserLogin, StringComparison.OrdinalIgnoreCase))
        {
            await SendTemplateAsync(_config.SelfDuelMessage, challenger.UserName, target, 0, "", "", cancellationToken);
            return;
        }
        if (target.Id.Equals(_chatUserId, StringComparison.Ordinal) || IsBlacklisted(target.Login, target.DisplayName))
        {
            await SendAsync($"@{challenger.UserName}, dieser Benutzer kann nicht herausgefordert werden.", cancellationToken);
            return;
        }

        var balance = await _points.GetPointsAsync(challenger.UserId, cancellationToken);
        int stake;
        if (parts[2].Equals("all", StringComparison.OrdinalIgnoreCase) && _config.AllowAllIn)
            stake = Math.Min(_config.MaximumBet, Math.Max(0, balance - Math.Max(0, _minigame.MinimumPoints)));
        else if (!int.TryParse(parts[2], out stake))
        {
            await SendTemplateAsync(_config.InvalidBetMessage, challenger.UserName, target, 0, "", "", cancellationToken);
            return;
        }
        if (stake < _config.MinimumBet || stake > _config.MaximumBet)
        {
            await SendTemplateAsync(_config.InvalidBetMessage, challenger.UserName, target, stake, "", "", cancellationToken);
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_byUserId.ContainsKey(challenger.UserId) || _byUserId.ContainsKey(target.Id))
            {
                await SendTemplateAsync(_config.AlreadyPendingDuelMessage, challenger.UserName, target, stake, "", "", cancellationToken);
                return;
            }
            var now = DateTimeOffset.UtcNow;
            if (now - _lastGlobalUse < TimeSpan.FromSeconds(_config.GlobalCooldownSeconds))
            {
                await SendAsync($"@{challenger.UserName}, der globale Duel-Cooldown ist noch aktiv.", cancellationToken);
                return;
            }
            if (_userCooldowns.TryGetValue(challenger.UserId, out var last) &&
                now - last < TimeSpan.FromSeconds(_config.UserCooldownSeconds))
            {
                await SendAsync($"@{challenger.UserName}, dein Duel-Cooldown ist noch aktiv.", cancellationToken);
                return;
            }

            var reserve = await _points.ReserveDuelStakeAsync(challenger.UserId,
                challenger.UserName, stake, _minigame.MinimumPoints,
                _minigame.HistoryLimit, cancellationToken);
            if (!reserve.Success)
            {
                await SendTemplateAsync(_config.NotEnoughPointsChallengerMessage,
                    challenger.UserName, target, stake, "", "", cancellationToken);
                return;
            }

            var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var duel = new PendingDuel
            {
                Id = Guid.NewGuid().ToString("N"), Challenger = challenger,
                Target = target, Stake = stake,
                ExpiresAt = now.AddSeconds(_config.RequestTimeoutSeconds),
                TimeoutCts = timeoutCts
            };
            _byUserId[challenger.UserId] = duel;
            _byUserId[target.Id] = duel;
            _lastGlobalUse = now;
            _userCooldowns[challenger.UserId] = now;
            duel.TimeoutTask = RunTimeoutAsync(duel, timeoutCts.Token);
            Console.WriteLine($"Duel-Anfrage erstellt: {challenger.UserName} -> {target.DisplayName}; Einsatz {stake}; reserviert; ID {duel.Id}.");
            PublishStatus(duel);
            if (_config.SendRequestMessage)
                await SendTemplateAsync(_config.DuelRequestMessage, challenger.UserName, target, stake, "", "", cancellationToken);
        }
        catch
        {
            if (!_byUserId.ContainsKey(challenger.UserId))
                await _points.RefundDuelStakeAsync(challenger.UserId, challenger.UserName,
                    stake, _minigame.HistoryLimit, CancellationToken.None);
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task AcceptAsync(ChatMessage user, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_byUserId.TryGetValue(user.UserId, out var duel))
            {
                await SendTemplateAsync(_config.NoPendingDuelMessage, user.UserName, null, 0, "", "", cancellationToken);
                return;
            }
            if (!duel.Target.Id.Equals(user.UserId, StringComparison.Ordinal))
            {
                await SendTemplateAsync(_config.WrongTargetMessage, user.UserName, duel.Target, duel.Stake, "", "", cancellationToken);
                return;
            }
            if (duel.State != DuelState.Waiting) return;
            if (!await IsAllowedAsync(user, cancellationToken))
            {
                await SendAsync($"@{user.UserName}, du darfst dieses Duel nicht annehmen.", cancellationToken);
                return;
            }

            var chance = DuelRules.ChallengerChance(_config.FairMode, _config.ChallengerWinChancePercent);
            var roll = _random.NextInclusive(1, 100);
            var challengerWins = DuelRules.ChallengerWins(_config.FairMode, _config.ChallengerWinChancePercent, roll);
            var winnerId = challengerWins ? duel.Challenger.UserId : duel.Target.Id;
            var winnerName = challengerWins ? duel.Challenger.UserName : duel.Target.DisplayName;
            var loserName = challengerWins ? duel.Target.DisplayName : duel.Challenger.UserName;
            var result = await _points.ResolveDuelAsync(duel.Challenger.UserId,
                duel.Challenger.UserName, duel.Target.Id, duel.Target.DisplayName,
                winnerId, duel.Stake, _minigame.MinimumPoints,
                _minigame.MaximumAccountEnabled ? _minigame.MaximumAccountPoints : int.MaxValue,
                _minigame.HistoryLimit, cancellationToken);
            if (!result.Success)
            {
                Console.WriteLine($"Duel-Annahme abgelehnt: {result.Error}; Ziel {duel.Target.DisplayName}.");
                await SendTemplateAsync(_config.NotEnoughPointsTargetMessage,
                    duel.Challenger.UserName, duel.Target, duel.Stake, "", "", cancellationToken);
                return;
            }

            duel.State = DuelState.Paid;
            duel.TimeoutCts.Cancel();
            Remove(duel);
            Console.WriteLine($"Duel ausgezahlt: Gewinner {winnerName}; Verlierer {loserName}; Pot {result.Pot}; Zufallswert {roll}; Chance Herausforderer {chance}%.");
            PublishStatus(duel);
            if (_config.SendResultMessage)
            {
                await SendTemplateAsync(_config.DuelAcceptedMessage, duel.Challenger.UserName,
                    duel.Target, duel.Stake, winnerName, loserName, cancellationToken);
                await SendTemplateAsync(_config.DuelWinMessage, duel.Challenger.UserName,
                    duel.Target, duel.Stake, winnerName, loserName, cancellationToken);
            }
        }
        finally { _gate.Release(); }
    }

    private async Task DenyAsync(ChatMessage user, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_byUserId.TryGetValue(user.UserId, out var duel))
            {
                await SendTemplateAsync(_config.NoPendingDuelMessage, user.UserName, null, 0, "", "", cancellationToken);
                return;
            }
            if (!duel.Target.Id.Equals(user.UserId, StringComparison.Ordinal))
            {
                await SendTemplateAsync(_config.WrongTargetMessage, user.UserName, duel.Target, duel.Stake, "", "", cancellationToken);
                return;
            }
            if (duel.State != DuelState.Waiting) return;
            duel.State = DuelState.Denied;
            duel.TimeoutCts.Cancel();
            await RefundAsync(duel, "abgelehnt", CancellationToken.None);
            Remove(duel);
            Console.WriteLine($"Duel abgelehnt: {duel.Target.DisplayName}; Rückerstattung {duel.Stake} an {duel.Challenger.UserName}.");
            PublishStatus(duel);
            if (_config.SendDenyMessage)
                await SendTemplateAsync(_config.DuelDeniedMessage, duel.Challenger.UserName,
                    duel.Target, duel.Stake, "", "", cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task RunTimeoutAsync(PendingDuel duel, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var delay = duel.ExpiresAt - DateTimeOffset.UtcNow;
                if (delay <= TimeSpan.Zero) break;
                await Task.Delay(delay > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay, cancellationToken);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    if (duel.State != DuelState.Waiting || !_byUserId.ContainsKey(duel.Challenger.UserId)) return;
                    PublishStatus(duel);
                }
                finally { _gate.Release(); }
            }
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (duel.State != DuelState.Waiting ||
                    !_byUserId.TryGetValue(duel.Challenger.UserId, out var current) ||
                    !ReferenceEquals(current, duel)) return;
                duel.State = DuelState.Expired;
                await RefundAsync(duel, "Timeout", CancellationToken.None);
                Remove(duel);
                Console.WriteLine($"Duel-Timeout: {duel.Challenger.UserName} gegen {duel.Target.DisplayName}; Rückerstattung {duel.Stake}.");
                PublishStatus(duel);
                if (_config.SendTimeoutMessage)
                    await SendTemplateAsync(_config.DuelTimeoutMessage, duel.Challenger.UserName,
                        duel.Target, duel.Stake, "", "", CancellationToken.None);
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { Console.WriteLine("Duel-Timeoutfehler: " + exception.Message); }
    }

    public async Task CancelAllAsync(bool announce, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var duels = _byUserId.Values.Distinct().ToArray();
            foreach (var duel in duels)
            {
                if (duel.State != DuelState.Waiting) continue;
                duel.State = DuelState.Cancelled;
                duel.TimeoutCts.Cancel();
                await RefundAsync(duel, "Abbruch", CancellationToken.None);
                duel.TimeoutCts.Dispose();
                Console.WriteLine($"Duel abgebrochen: {duel.Id}; Rückerstattung {duel.Stake}.");
            }
            _byUserId.Clear();
            StatusChanged?.Invoke(new DuelStatus(0, "", "", 0, 0, DuelState.Cancelled));
            if (announce && duels.Length > 0)
                await SendAsync("Alle offenen Duel-Anfragen wurden abgebrochen und die Einsätze zurückgegeben.", cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task RunTestAsync(CancellationToken cancellationToken)
    {
        var chance = DuelRules.ChallengerChance(_config.FairMode, _config.ChallengerWinChancePercent);
        var roll = _random.NextInclusive(1, 100);
        var winner = DuelRules.ChallengerWins(_config.FairMode, _config.ChallengerWinChancePercent, roll) ? "TestChallenger" : "TestTarget";
        var loser = winner == "TestChallenger" ? "TestTarget" : "TestChallenger";
        var target = new TwitchUser("test-target", "testtarget", "TestTarget");
        var stake = Math.Max(_config.MinimumBet, 100);
        Console.WriteLine($"TEST-DUEL: Zufallswert {roll}; Chance {chance}%; Gewinner {winner}; keine Punkte verändert.");
        await SendTemplateAsync("[TEST] " + _config.DuelRequestMessage, "TestChallenger", target, stake, "", "", cancellationToken);
        await SendTemplateAsync("[TEST] " + _config.DuelAcceptedMessage, "TestChallenger", target, stake, winner, loser, cancellationToken);
        await SendTemplateAsync("[TEST] " + _config.DuelWinMessage + " Keine echten Punkte verändert.", "TestChallenger", target, stake, winner, loser, cancellationToken);
        StatusChanged?.Invoke(new DuelStatus(0, "TestChallenger", "TestTarget", stake, 0, DuelState.Paid, true));
    }

    private async Task RefundAsync(PendingDuel duel, string reason, CancellationToken token)
    {
        var balance = await _points.RefundDuelStakeAsync(duel.Challenger.UserId,
            duel.Challenger.UserName, duel.Stake, _minigame.HistoryLimit, token);
        Console.WriteLine($"Duel-Rückerstattung: {duel.Challenger.UserName} +{duel.Stake}; Stand {balance}; Grund {reason}.");
    }

    private void Remove(PendingDuel duel)
    {
        _byUserId.Remove(duel.Challenger.UserId);
        _byUserId.Remove(duel.Target.Id);
        duel.TimeoutCts.Dispose();
    }

    private bool IsBlacklisted(params string[] values) =>
        _minigame.PointsBlacklist.Any(blocked => values.Any(value =>
            blocked.Equals(value, StringComparison.OrdinalIgnoreCase)));

    private async Task<bool> IsAllowedAsync(ChatMessage user, CancellationToken token)
    {
        if (user.CommandAuthorization == CommandAuthorization.Allowed) return true;
        if (user.CommandAuthorization == CommandAuthorization.Denied) return false;
        if (user.IsBroadcaster || user.UserId == _broadcasterId) return true;
        if (_config.AllowEveryone) return true;
        if (_config.AllowModerators && user.IsModerator || _config.AllowVips && user.IsVip ||
            _config.AllowSubscribers && user.IsSubscriber) return true;
        return _config.AllowFollowers && await _twitch.IsFollowerAsync(_broadcasterId, user.UserId, token);
    }

    private void PublishStatus(PendingDuel duel)
    {
        var remaining = Math.Max(0, (int)Math.Ceiling((duel.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));
        try
        {
            StatusChanged?.Invoke(new DuelStatus(_byUserId.Values.Distinct().Count(),
                duel.Challenger.UserName, duel.Target.DisplayName, duel.Stake,
                remaining, duel.State));
        }
        catch (Exception exception) { Console.WriteLine("Duel-Statusanzeige fehlgeschlagen: " + exception.Message); }
    }

    private Task SendTemplateAsync(string template, string challenger,
        TwitchUser? target, int amount, string winner, string loser,
        CancellationToken cancellationToken)
    {
        var chance = DuelRules.ChallengerChance(_config.FairMode, _config.ChallengerWinChancePercent);
        var text = template
            .Replace("{user}", challenger)
            .Replace("{challenger}", challenger)
            .Replace("{target}", target?.DisplayName ?? "")
            .Replace("{winner}", winner)
            .Replace("{loser}", loser)
            .Replace("{amount}", amount.ToString("N0"))
            .Replace("{pot}", checked(amount * 2).ToString("N0"))
            .Replace("{currencyName}", _minigame.CurrencyPlural)
            .Replace("{seconds}", _config.RequestTimeoutSeconds.ToString())
            .Replace("{duelCommand}", _config.DuelCommand)
            .Replace("{acceptCommand}", _config.AcceptCommand)
            .Replace("{denyCommand}", _config.DenyCommand)
            .Replace("{winChance}", chance.ToString());
        return SendAsync(text, cancellationToken);
    }

    private async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        try { await _twitch.SendChatMessageAsync(_broadcasterId, _chatUserId, message, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { Console.WriteLine("Duel-Chatantwort fehlgeschlagen: " + exception.Message); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await CancelAllAsync(false, CancellationToken.None);
        _gate.Dispose();
    }
}
