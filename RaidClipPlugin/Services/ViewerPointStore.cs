using System.Text.Json;
using RaidClipPlugin.Models;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed class ViewerPointStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private readonly string _historyPath;
    private readonly string _statePath;
    private Dictionary<string, ViewerPointEntry>? _entries;
    private List<MinigameHistoryEntry> _history = new();
    private int _jackpot;

    public ViewerPointStore(string? storageDirectory = null)
    {
        var directory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin",
            "minigame");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "viewer-points.json");
        _historyPath = Path.Combine(directory, "history.json");
        _statePath = Path.Combine(directory, "state.json");
        try
        {
            if (File.Exists(_historyPath))
                _history = JsonSerializer.Deserialize<List<MinigameHistoryEntry>>(
                    File.ReadAllText(_historyPath), JsonOptions) ?? new();
            if (File.Exists(_statePath))
                _jackpot = JsonSerializer.Deserialize<int>(
                    File.ReadAllText(_statePath), JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Minigame-Zusatzdaten konnten nicht geladen werden: " + exception.Message);
            _history = new(); _jackpot = 0;
        }
    }

    public async Task<long> GetPointsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries!.TryGetValue(userId, out var entry)
                ? Math.Max(0, entry.Points)
                : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<long> AddPointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        long maximumPoints = long.MaxValue) =>
        ChangePointsAsync(
            userId, displayName, current => amount > 0 && current > long.MaxValue - amount
                ? long.MaxValue
                : current + amount,
            minimumPoints, maximumPoints, cancellationToken);

    public Task<long> RemovePointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        long maximumPoints = long.MaxValue) =>
        ChangePointsAsync(
            userId, displayName,
            current => current - Math.Max(0, amount),
            minimumPoints, maximumPoints, cancellationToken);

    public Task<long> SetPointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        long maximumPoints = long.MaxValue) =>
        ChangePointsAsync(
            userId, displayName, _ => amount,
            minimumPoints, maximumPoints, cancellationToken);

    public async Task<PointTransferResult> TransferPointsAsync(
        string senderId,
        string senderDisplayName,
        string recipientId,
        string recipientDisplayName,
        int amount,
        int minimumPoints,
        long maximumPoints,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId) ||
            string.IsNullOrWhiteSpace(recipientId))
        {
            throw new ArgumentException("Die Twitch-User-ID fehlt.");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var ceiling = Math.Max(floor, maximumPoints);
            var sender = GetOrCreateEntry(
                senderId, senderDisplayName, floor);

            if (senderId.Equals(recipientId, StringComparison.Ordinal))
            {
                return new PointTransferResult(false,
                    "Du kannst dir nicht selbst Punkte schenken",
                    sender.Points, sender.Points);
            }

            var recipientPoints = _entries!.TryGetValue(
                recipientId, out var existingRecipient)
                ? existingRecipient.Points
                : floor;

            if (amount <= 0)
            {
                return new PointTransferResult(false,
                    "Der Betrag muss größer als 0 sein",
                    sender.Points, recipientPoints);
            }

            if (sender.Points - amount < floor)
            {
                return new PointTransferResult(false,
                    "Du hast nicht genug verfügbare Punkte",
                    sender.Points, recipientPoints);
            }

            if (amount > ceiling - recipientPoints)
            {
                return new PointTransferResult(false,
                    "Das Punktekonto des Empfängers würde das Maximum überschreiten",
                    sender.Points, recipientPoints);
            }

            var recipient = existingRecipient ?? GetOrCreateEntry(
                recipientId, recipientDisplayName, floor);
            sender.Points = checked(sender.Points - amount);
            recipient.Points = checked(recipient.Points + amount);
            sender.DisplayName = senderDisplayName;
            recipient.DisplayName = recipientDisplayName;
            sender.UpdatedAt = DateTimeOffset.Now;
            recipient.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);

            return new PointTransferResult(true, "",
                sender.Points, recipient.Points);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DuelReserveResult> ReserveDuelStakeAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        int historyLimit,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var entry = GetOrCreateEntry(userId, displayName, floor);
            if (amount <= 0 || entry.Points - amount < floor)
                return new DuelReserveResult(false, "Nicht genug verfügbare Punkte", entry.Points);

            var previous = entry.Points;
            var historyBefore = _history.ToList();
            entry.Points -= amount;
            entry.DisplayName = displayName;
            entry.UpdatedAt = DateTimeOffset.Now;
            AddDuelHistory(userId, displayName, "Einsatz reserviert", -amount, entry.Points, historyLimit);
            try { await SaveAllAsync(cancellationToken); }
            catch { entry.Points = previous; _history = historyBefore; throw; }
            return new DuelReserveResult(true, "", entry.Points);
        }
        finally { _lock.Release(); }
    }

    public async Task<long> RefundDuelStakeAsync(
        string userId,
        string displayName,
        int amount,
        int historyLimit,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = GetOrCreateEntry(userId, displayName, 0);
            var previous = entry.Points;
            var historyBefore = _history.ToList();
            entry.Points = entry.Points > long.MaxValue - Math.Max(0, amount)
                ? long.MaxValue
                : entry.Points + Math.Max(0, amount);
            entry.DisplayName = displayName;
            entry.UpdatedAt = DateTimeOffset.Now;
            AddDuelHistory(userId, displayName, "Einsatz zurückgegeben", amount, entry.Points, historyLimit);
            try { await SaveAllAsync(cancellationToken); }
            catch { entry.Points = previous; _history = historyBefore; throw; }
            return entry.Points;
        }
        finally { _lock.Release(); }
    }

    public async Task<DuelResolutionResult> ResolveDuelAsync(
        string challengerId,
        string challengerName,
        string targetId,
        string targetName,
        string winnerId,
        int stake,
        int minimumPoints,
        long maximumPoints,
        int historyLimit,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var ceiling = Math.Max(floor, maximumPoints);
            var challenger = GetOrCreateEntry(challengerId, challengerName, floor);
            var target = GetOrCreateEntry(targetId, targetName, floor);
            if (stake <= 0 || target.Points - stake < floor)
                return new DuelResolutionResult(false, "Zielspieler hat nicht genug Punkte", challenger.Points, target.Points, stake * 2);

            var pot = checked(stake * 2);
            var challengerBefore = challenger.Points;
            var targetBefore = target.Points;
            var historyBefore = _history.ToList();
            var challengerAfter = challengerBefore;
            var targetAfter = targetBefore - stake;
            if (winnerId.Equals(challengerId, StringComparison.Ordinal))
                challengerAfter = checked(challengerAfter + pot);
            else if (winnerId.Equals(targetId, StringComparison.Ordinal))
                targetAfter = checked(targetAfter + pot);
            else
                throw new ArgumentException("Der Duel-Gewinner ist kein Teilnehmer.", nameof(winnerId));

            if (challengerAfter > ceiling || targetAfter > ceiling)
                return new DuelResolutionResult(false, "Die Auszahlung würde das technische Punktelimit überschreiten", challengerBefore, targetBefore, pot);

            challenger.Points = challengerAfter;
            target.Points = targetAfter;
            challenger.DisplayName = challengerName;
            target.DisplayName = targetName;
            challenger.UpdatedAt = target.UpdatedAt = DateTimeOffset.Now;
            var challengerChange = winnerId.Equals(challengerId, StringComparison.Ordinal) ? pot : 0;
            var targetChange = winnerId.Equals(targetId, StringComparison.Ordinal) ? pot - stake : -stake;
            AddDuelHistory(challengerId, challengerName, winnerId == challengerId ? "Duel gewonnen" : "Duel verloren", challengerChange, challengerAfter, historyLimit);
            AddDuelHistory(targetId, targetName, winnerId == targetId ? "Duel gewonnen" : "Duel verloren", targetChange, targetAfter, historyLimit);
            try { await SaveAllAsync(cancellationToken); }
            catch { challenger.Points = challengerBefore; target.Points = targetBefore; _history = historyBefore; throw; }
            return new DuelResolutionResult(true, "", challengerAfter, targetAfter, pot);
        }
        finally { _lock.Release(); }
    }

    private void AddDuelHistory(string userId, string displayName, string action,
        long change, long balance, int historyLimit)
    {
        _history.Insert(0, new MinigameHistoryEntry
        {
            Timestamp = DateTimeOffset.Now,
            UserId = userId,
            DisplayName = displayName,
            Game = "Duel",
            Action = action,
            Change = change,
            Balance = balance
        });
        var limit = Math.Max(1, historyLimit);
        if (_history.Count > limit) _history.RemoveRange(limit, _history.Count - limit);
    }

    public async Task<int> AddPointsToAllAsync(
        int amount,
        int minimumPoints,
        long maximumPoints,
        CancellationToken cancellationToken)
    {
        if (amount <= 0) return 0;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var ceiling = Math.Max(floor, maximumPoints);
            foreach (var entry in _entries!.Values)
            {
                var increased = entry.Points > long.MaxValue - amount
                    ? long.MaxValue : entry.Points + amount;
                entry.Points = Math.Clamp(increased, floor, ceiling);
                entry.UpdatedAt = DateTimeOffset.Now;
            }
            await SaveAsync(cancellationToken);
            return _entries.Count;
        }
        finally { _lock.Release(); }
    }

    public async Task<int> AddPointsToUsersAsync(
        IEnumerable<(string UserId, string DisplayName)> users,
        int amount,
        int minimumPoints,
        long maximumPoints,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var recipients = users
            .Where(user => !string.IsNullOrWhiteSpace(user.UserId))
            .GroupBy(user => user.UserId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (recipients.Length == 0)
        {
            return 0;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var ceiling = Math.Max(floor, maximumPoints);
            foreach (var recipient in recipients)
            {
                var entry = GetOrCreateEntry(
                    recipient.UserId, recipient.DisplayName, floor);
                entry.Points = Math.Clamp(
                    entry.Points + amount, floor, ceiling);
                entry.DisplayName = recipient.DisplayName;
                entry.UpdatedAt = DateTimeOffset.Now;
            }

            await SaveAsync(cancellationToken);
            return recipients.Length;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> AwardAttendanceAsync(
        IEnumerable<(string UserId, string DisplayName, int Points)> awards,
        int minutes,
        int minimumPoints,
        long maximumPoints,
        CancellationToken cancellationToken)
    {
        var recipients = awards
            .Where(award => !string.IsNullOrWhiteSpace(award.UserId))
            .GroupBy(award => award.UserId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (recipients.Length == 0)
        {
            return 0;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var ceiling = Math.Max(floor, maximumPoints);
            foreach (var recipient in recipients)
            {
                var entry = GetOrCreateEntry(
                    recipient.UserId, recipient.DisplayName, floor);
                entry.WatchMinutes = (int)Math.Min(
                    int.MaxValue,
                    (long)entry.WatchMinutes + Math.Max(0, minutes));
                entry.Points = Math.Clamp(
                    entry.Points + Math.Max(0, recipient.Points),
                    floor,
                    ceiling);
                entry.DisplayName = recipient.DisplayName;
                entry.UpdatedAt = DateTimeOffset.Now;
            }

            await SaveAsync(cancellationToken);
            return recipients.Length;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GambleBalanceResult> ApplyGambleAsync(
        string userId,
        string displayName,
        int stake,
        int payout,
        int minimumPoints,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var entry = GetOrCreateEntry(userId, displayName, floor);

            if (stake < 0 || entry.Points - stake < floor)
            {
                return new GambleBalanceResult(false, entry.Points);
            }

            var gambleChange = (long)Math.Max(0, payout) - stake;
            entry.Points = gambleChange > 0 &&
                entry.Points > long.MaxValue - gambleChange
                    ? long.MaxValue
                    : Math.Max(floor, entry.Points + gambleChange);
            entry.DisplayName = displayName;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);
            return new GambleBalanceResult(true, entry.Points);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _entries = new Dictionary<string, ViewerPointEntry>(
                StringComparer.Ordinal);
            _history.Clear();
            _jackpot = 0;
            await SaveAllAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries!.Count;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddWatchtimeAsync(string userId, string name, int minutes,
        int points, int minimum, CancellationToken cancellationToken,
        long maximum = long.MaxValue)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = GetOrCreateEntry(userId, name, minimum);
            entry.WatchMinutes = checked(entry.WatchMinutes + Math.Max(0, minutes));
            entry.Points = Math.Clamp(
                points > 0 && entry.Points > long.MaxValue - points
                    ? long.MaxValue
                    : entry.Points + points,
                minimum, maximum);
            entry.DisplayName = name;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<DailyClaimResult> ClaimDailyAsync(string userId, string name,
        int bonus, int minimum, CancellationToken cancellationToken,
        long maximum = long.MaxValue)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = GetOrCreateEntry(userId, name, minimum);
            var now = DateTimeOffset.UtcNow;
            if (entry.LastDailyUtc is { } last && now - last < TimeSpan.FromHours(24))
                return new DailyClaimResult(false, entry.Points,
                    TimeSpan.FromHours(24) - (now - last));
            entry.LastDailyUtc = now;
            entry.Points = Math.Clamp(
                bonus > 0 && entry.Points > long.MaxValue - bonus
                    ? long.MaxValue
                    : entry.Points + bonus,
                minimum, maximum);
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);
            return new DailyClaimResult(true, entry.Points, TimeSpan.Zero);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<ViewerPointEntry>> GetTopAsync(int count,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries!.Values.OrderByDescending(x => x.Points)
                .ThenBy(x => x.DisplayName).Take(Math.Clamp(count, 1, 100))
                .Select(CloneEntry).ToArray();
        }
        finally { _lock.Release(); }
    }

    public async Task<ViewerProfileResult> GetProfileAsync(string userId,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var ordered = _entries!.Values.OrderByDescending(x => x.Points)
                .ThenBy(x => x.UserId).ToArray();
            var index = Array.FindIndex(ordered, x => x.UserId == userId);
            var entry = index >= 0 ? CloneEntry(ordered[index]) : new ViewerPointEntry
            { UserId = userId, Points = 0 };
            return new ViewerProfileResult(entry, index >= 0 ? index + 1 : 0);
        }
        finally { _lock.Release(); }
    }

    public async Task<CasinoApplyResult> ApplyCasinoAsync(string userId, string name,
        string game, int stake, int payout, int minimum, long maximum,
        int dailyGames, int dailyLoss, int dailyWin, int jackpotContribution,
        bool jackpotHit, int jackpotStart, int historyLimit,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = GetOrCreateEntry(userId, name, minimum);
            ResetDaily(entry);

            var safeStake = Math.Max(0, stake);
            var safePayout = Math.Max(0, payout);
            var safeMinimum = Math.Max(0, minimum);
            var safeMaximum = maximum > 0
                ? Math.Max(safeMinimum, maximum)
                : long.MaxValue;
            if ((long)entry.Points - safeStake < safeMinimum)
                return new CasinoApplyResult(false, "Nicht genug Punkte", entry.Points, 0);
            if (dailyGames > 0 && entry.DailyGambles >= dailyGames)
                return new CasinoApplyResult(false, "Tägliches Spielelimit erreicht", entry.Points, 0);

            var loss = Math.Max(0L, (long)safeStake - safePayout);
            var win = Math.Max(0L, (long)safePayout - safeStake);
            if (dailyLoss > 0 && (long)Math.Max(0, entry.DailyLoss) + loss > dailyLoss)
                return new CasinoApplyResult(false, "Tägliches Verlustlimit erreicht", entry.Points, 0);

            var jackpotCandidate = jackpotHit
                ? Math.Max(_jackpot, Math.Max(0, jackpotStart))
                : 0;
            if (dailyWin > 0 &&
                (long)Math.Max(0, entry.DailyWin) + win + jackpotCandidate > dailyWin)
                return new CasinoApplyResult(false,
                    "Tägliches Gewinnlimit erreicht", entry.Points, 0);

            var totalPayout = (long)safePayout + jackpotCandidate;
            var balanceChange = totalPayout - safeStake;
            if (totalPayout > int.MaxValue ||
                (balanceChange > 0 && entry.Points > long.MaxValue - balanceChange))
                return new CasinoApplyResult(false,
                    "Die Auszahlung überschreitet das technische Punktelimit",
                    entry.Points, 0);
            var projectedBalance = entry.Points + balanceChange;

            var jackpotWon = 0;
            if (jackpotHit)
            {
                jackpotWon = jackpotCandidate;
                _jackpot = Math.Max(0, jackpotStart);
                safePayout = (int)totalPayout;
            }
            else if (loss > 0)
            {
                var updatedJackpot = (long)Math.Max(_jackpot, Math.Max(0, jackpotStart)) +
                    Math.Max(0, jackpotContribution);
                _jackpot = (int)Math.Min(int.MaxValue, updatedJackpot);
            }

            var balanceMaximum = jackpotHit ? long.MaxValue : safeMaximum;
            entry.Points = Math.Clamp(
                projectedBalance, safeMinimum, balanceMaximum);
            entry.GamesPlayed = SaturatingIncrement(entry.GamesPlayed);
            entry.DailyGambles = SaturatingIncrement(entry.DailyGambles);
            entry.DailyLoss = (int)Math.Min(
                int.MaxValue, (long)Math.Max(0, entry.DailyLoss) + loss);
            entry.DailyWin = (int)Math.Min(
                int.MaxValue,
                (long)Math.Max(0, entry.DailyWin) + win + jackpotWon);
            if (safePayout > safeStake)
                entry.Wins = SaturatingIncrement(entry.Wins);
            else if (safePayout < safeStake)
                entry.Losses = SaturatingIncrement(entry.Losses);
            entry.BiggestWin = Math.Max(entry.BiggestWin, safePayout);
            entry.DisplayName = name;
            entry.UpdatedAt = DateTimeOffset.Now;
            _history.Insert(0, new MinigameHistoryEntry
            {
                UserId = userId, DisplayName = name, Game = game,
                Action = $"Einsatz {safeStake}, Auszahlung {safePayout}",
                Change = safePayout - safeStake, Balance = entry.Points
            });
            var safeHistoryLimit = Math.Max(1, historyLimit);
            if (_history.Count > safeHistoryLimit)
                _history.RemoveRange(safeHistoryLimit, _history.Count - safeHistoryLimit);
            await SaveAllAsync(cancellationToken);
            return new CasinoApplyResult(true, "", entry.Points, jackpotWon);
        }
        finally { _lock.Release(); }
    }

    private static int SaturatingIncrement(int value) =>
        value >= int.MaxValue ? int.MaxValue : Math.Max(0, value) + 1;

    public async Task<HeistPayoutResult> PayoutHeistJackpotAsync(
        IReadOnlyList<(string UserId, string DisplayName)> participants,
        IReadOnlyCollection<int> remainderRecipients,
        int jackpotStart,
        bool resetToStart,
        int historyLimit,
        CancellationToken cancellationToken)
    {
        if (participants.Count == 0)
            throw new ArgumentException("Mindestens ein Teilnehmer wird benötigt.", nameof(participants));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var jackpot = Math.Max(_jackpot, Math.Max(0, jackpotStart));
            var baseShare = jackpot / participants.Count;
            var remainder = jackpot % participants.Count;
            var extras = remainderRecipients.Distinct().Take(remainder).ToHashSet();
            if (extras.Count != remainder || extras.Any(index => index < 0 || index >= participants.Count))
                throw new InvalidOperationException("Die Restpunktverteilung ist ungültig.");

            var payouts = new List<HeistParticipantPayout>(participants.Count);
            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                var payout = checked(baseShare + (extras.Contains(index) ? 1 : 0));
                var entry = GetOrCreateEntry(participant.UserId, participant.DisplayName, 0);
                if (entry.Points > long.MaxValue - payout)
                    throw new InvalidOperationException("Eine Heist-Auszahlung überschreitet das technische Punktelimit.");
                payouts.Add(new HeistParticipantPayout(participant.UserId, participant.DisplayName, payout, entry.Points + payout));
            }

            foreach (var payout in payouts)
            {
                var entry = GetOrCreateEntry(payout.UserId, payout.DisplayName, 0);
                entry.Points = payout.NewBalance;
                entry.DisplayName = payout.DisplayName;
                entry.UpdatedAt = DateTimeOffset.Now;
                _history.Insert(0, new MinigameHistoryEntry
                {
                    UserId = payout.UserId,
                    DisplayName = payout.DisplayName,
                    Game = "Heist",
                    Action = $"Jackpot-Anteil {payout.Payout}",
                    Change = payout.Payout,
                    Balance = payout.NewBalance
                });
            }

            if (_history.Count > Math.Max(1, historyLimit))
                _history.RemoveRange(Math.Max(1, historyLimit), _history.Count - Math.Max(1, historyLimit));
            _jackpot = resetToStart ? Math.Max(0, jackpotStart) : 0;
            await SaveAllAsync(cancellationToken);
            return new HeistPayoutResult(jackpot, _jackpot, payouts);
        }
        finally { _lock.Release(); }
    }

    public async Task<(int Previous, int Current)> ResetJackpotAsync(
        int startValue,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var previous = Math.Max(0, _jackpot);
            _jackpot = Math.Max(0, startValue);
            await SaveAllAsync(cancellationToken);
            return (previous, _jackpot);
        }
        finally { _lock.Release(); }
    }

    public async Task<(int Previous, int Current)> AddJackpotAsync(
        int amount,
        int startValue,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var previous = Math.Max(_jackpot, Math.Max(0, startValue));
            _jackpot = (int)Math.Min(int.MaxValue, (long)previous + amount);
            await SaveAllAsync(cancellationToken);
            return (previous, _jackpot);
        }
        finally { _lock.Release(); }
    }

    public async Task<int> GetJackpotAsync(int startValue,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try { await EnsureLoadedAsync(cancellationToken); return Math.Max(_jackpot, startValue); }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<MinigameHistoryEntry>> GetHistoryAsync(int count,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try { await EnsureLoadedAsync(cancellationToken); return _history.Take(count).ToArray(); }
        finally { _lock.Release(); }
    }

    public async Task ExportAsync(string path, MinigameConfig settings,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var package = new MinigameExportPackage
            { Users = _entries!, History = _history, Jackpot = _jackpot, Settings = settings };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(package, JsonOptions), cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<MinigameConfig> ImportAsync(string path,
        MinigameConfig currentSettings, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var package = JsonSerializer.Deserialize<MinigameExportPackage>(json, JsonOptions)
            ?? throw new InvalidOperationException("Die Importdatei ist ungültig.");
        if (package.Users is null || package.Settings is null)
            throw new InvalidOperationException("Die Importdatei ist unvollständig.");
        ConfigurationService.ValidateMinigameSettings(package.Settings);
        if (package.Users.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
            pair.Value is null || pair.Value.Points < 0))
            throw new InvalidOperationException(
                "Die Importdatei enthält ungültige Punktedaten.");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var backupDirectory = Path.GetDirectoryName(_filePath)!;
            var backupPath = Path.Combine(backupDirectory,
                $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var backupPackage = new MinigameExportPackage
            { Users = _entries!, History = _history, Jackpot = _jackpot,
              Settings = currentSettings };
            await File.WriteAllTextAsync(backupPath,
                JsonSerializer.Serialize(backupPackage, JsonOptions),
                cancellationToken);
            _entries = new Dictionary<string, ViewerPointEntry>(package.Users,
                StringComparer.Ordinal);
            _history = package.History ?? new();
            _jackpot = Math.Max(0, package.Jackpot);
            await SaveAllAsync(cancellationToken);
            return package.Settings;
        }
        finally { _lock.Release(); }
    }

    private static void ResetDaily(ViewerPointEntry entry)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (entry.DailyLimitDate == today) return;
        entry.DailyLimitDate = today;
        entry.DailyGambles = 0; entry.DailyLoss = 0; entry.DailyWin = 0;
    }

    private static ViewerPointEntry CloneEntry(ViewerPointEntry x) => new()
    {
        UserId=x.UserId, DisplayName=x.DisplayName, Points=x.Points,
        UpdatedAt=x.UpdatedAt, WatchMinutes=x.WatchMinutes,
        GamesPlayed=x.GamesPlayed, Wins=x.Wins, Losses=x.Losses,
        BiggestWin=x.BiggestWin, LastDailyUtc=x.LastDailyUtc,
        DailyLimitDate=x.DailyLimitDate, DailyGambles=x.DailyGambles,
        DailyLoss=x.DailyLoss, DailyWin=x.DailyWin
    };

    private async Task<long> ChangePointsAsync(
        string userId,
        string displayName,
        Func<long, long> change,
        int minimumPoints,
        long maximumPoints,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Die Twitch-User-ID fehlt.", nameof(userId));
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var floor = Math.Max(0, minimumPoints);
            var entry = GetOrCreateEntry(userId, displayName, floor);
            entry.Points = Math.Clamp(change(entry.Points), floor,
                Math.Max(floor, maximumPoints));
            entry.DisplayName = displayName;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);
            return entry.Points;
        }
        finally
        {
            _lock.Release();
        }
    }

    private ViewerPointEntry GetOrCreateEntry(
        string userId,
        string displayName,
        int minimumPoints)
    {
        if (_entries!.TryGetValue(userId, out var entry))
        {
            return entry;
        }

        entry = new ViewerPointEntry
        {
            UserId = userId,
            DisplayName = displayName,
            Points = Math.Max(0, minimumPoints),
            UpdatedAt = DateTimeOffset.Now
        };
        _entries[userId] = entry;
        return entry;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_entries is not null)
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            _entries = new Dictionary<string, ViewerPointEntry>(
                StringComparer.Ordinal);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var entries = await JsonSerializer.DeserializeAsync<
                Dictionary<string, ViewerPointEntry>>(
                stream,
                JsonOptions,
                cancellationToken);
            _entries = entries is null
                ? new Dictionary<string, ViewerPointEntry>(StringComparer.Ordinal)
                : new Dictionary<string, ViewerPointEntry>(
                    entries,
                    StringComparer.Ordinal);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Minigame-Punktedatei konnte nicht geladen werden: " +
                exception.Message);
            _entries = new Dictionary<string, ViewerPointEntry>(
                StringComparer.Ordinal);
        }
    }

    private async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(cancellationToken);
        await File.WriteAllTextAsync(_historyPath,
            JsonSerializer.Serialize(_history, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(_statePath,
            JsonSerializer.Serialize(_jackpot, JsonOptions), cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                _entries,
                JsonOptions,
                cancellationToken);
        }
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
    private sealed class MinigameExportPackage
    {
        public Dictionary<string, ViewerPointEntry>? Users { get; set; }
        public List<MinigameHistoryEntry>? History { get; set; }
        public int Jackpot { get; set; }
        public MinigameConfig? Settings { get; set; }
    }

}