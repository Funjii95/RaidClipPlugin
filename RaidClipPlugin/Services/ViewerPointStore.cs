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

    public ViewerPointStore()
    {
        var directory = Path.Combine(
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

    public async Task<int> GetPointsAsync(
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

    public Task<int> AddPointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        int maximumPoints = int.MaxValue) =>
        ChangePointsAsync(
            userId, displayName, current => checked(current + amount),
            minimumPoints, maximumPoints, cancellationToken);

    public Task<int> RemovePointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        int maximumPoints = int.MaxValue) =>
        ChangePointsAsync(
            userId, displayName,
            current => current - Math.Max(0, amount),
            minimumPoints, maximumPoints, cancellationToken);

    public Task<int> SetPointsAsync(
        string userId,
        string displayName,
        int amount,
        int minimumPoints,
        CancellationToken cancellationToken,
        int maximumPoints = int.MaxValue) =>
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
        int maximumPoints,
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

            entry.Points = Math.Max(
                floor,
                checked(entry.Points - stake + Math.Max(0, payout)));
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
        int maximum = int.MaxValue)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = GetOrCreateEntry(userId, name, minimum);
            entry.WatchMinutes = checked(entry.WatchMinutes + Math.Max(0, minutes));
            entry.Points = Math.Clamp(checked(entry.Points + points),
                minimum, maximum);
            entry.DisplayName = name;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task<DailyClaimResult> ClaimDailyAsync(string userId, string name,
        int bonus, int minimum, CancellationToken cancellationToken,
        int maximum = int.MaxValue)
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
            entry.Points = Math.Clamp(checked(entry.Points + bonus),
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
        string game, int stake, int payout, int minimum, int maximum,
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
            if (entry.Points - stake < minimum)
                return new CasinoApplyResult(false, "Nicht genug Punkte", entry.Points, 0);
            if (dailyGames > 0 && entry.DailyGambles >= dailyGames)
                return new CasinoApplyResult(false, "Tägliches Spielelimit erreicht", entry.Points, 0);
            var loss = Math.Max(0, stake - payout);
            var win = Math.Max(0, payout - stake);
            if (dailyLoss > 0 && entry.DailyLoss + loss > dailyLoss)
                return new CasinoApplyResult(false, "Tägliches Verlustlimit erreicht", entry.Points, 0);
            var jackpotCandidate = jackpotHit
                ? Math.Max(_jackpot, jackpotStart)
                : 0;
            if (dailyWin > 0 &&
                entry.DailyWin + win + jackpotCandidate > dailyWin)
                return new CasinoApplyResult(false,
                    "Tägliches Gewinnlimit erreicht", entry.Points, 0);

            var jackpotWon = 0;
            if (jackpotHit)
            {
                jackpotWon = jackpotCandidate;
                _jackpot = jackpotStart;
                payout = checked(payout + jackpotWon);
            }
            else if (loss > 0)
                _jackpot = checked(Math.Max(_jackpot, jackpotStart) + jackpotContribution);

            entry.Points = Math.Clamp(checked(entry.Points - stake + payout),
                minimum, maximum > 0 ? maximum : int.MaxValue);
            entry.GamesPlayed++;
            entry.DailyGambles++;
            entry.DailyLoss = checked(entry.DailyLoss + loss);
            entry.DailyWin = checked(entry.DailyWin + win + jackpotWon);
            if (payout > stake) entry.Wins++;
            else if (payout < stake) entry.Losses++;
            entry.BiggestWin = Math.Max(entry.BiggestWin, payout);
            entry.DisplayName = name;
            entry.UpdatedAt = DateTimeOffset.Now;
            _history.Insert(0, new MinigameHistoryEntry
            {
                UserId = userId, DisplayName = name, Game = game,
                Action = $"Einsatz {stake}, Auszahlung {payout}",
                Change = payout - stake, Balance = entry.Points
            });
            if (_history.Count > historyLimit)
                _history.RemoveRange(historyLimit, _history.Count - historyLimit);
            await SaveAllAsync(cancellationToken);
            return new CasinoApplyResult(true, "", entry.Points, jackpotWon);
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

    private async Task<int> ChangePointsAsync(
        string userId,
        string displayName,
        Func<int, int> change,
        int minimumPoints,
        int maximumPoints,
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
