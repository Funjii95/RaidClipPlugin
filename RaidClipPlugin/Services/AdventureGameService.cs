using System.Globalization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class AdventureGameService
{
    public event Action? DataChanged;
    private readonly ViewerPointStore _points;
    private readonly Func<string, CancellationToken, Task> _send;
    private readonly IDuelRandom _random;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _broadcasterId;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new();
    private MinigameConfig _config;
    private RaceRound? _race;
    private BossRound? _boss;
    private DateTimeOffset _nextRace, _nextBoss;
    private DateTimeOffset _nextAutoBoss;
    private AdventureStatus _status = new();
    public AdventureStatus Status => Volatile.Read(ref _status);
    private void PublishStatus() => Volatile.Write(ref _status, new(
        _boss is not null, _boss?.Hp ?? 0, _boss is null ? 0 : Math.Clamp(_boss.Config.HitPoints, 10, 1000000),
        _boss?.Players.Count ?? 0, _boss?.End, _race is not null, _race?.Players.Count ?? 0, _race?.Stake ?? 0, _race?.End));
    private sealed record RaceRound(long Stake, DateTimeOffset End, int MaximumPlayers,
        MinigameConfig Config, Dictionary<string, string> Players);
    private sealed class BossRound
    {
        public required BossAdventureConfig Config;
        public required DateTimeOffset End;
        public int Hp;
        public Dictionary<string, string> Players = new();
    }

    public AdventureGameService(ViewerPointStore points, MinigameConfig config,
        Func<string, CancellationToken, Task> send, IDuelRandom? random = null,
        Func<DateTimeOffset>? now = null, string broadcasterId = "")
    { _points = points; _config = config; _send = send; _random = random ?? new CryptoDuelRandom(); _now = now ?? (() => DateTimeOffset.UtcNow); _broadcasterId = broadcasterId; ScheduleAutoBoss(); }

    public void UpdateConfig(MinigameConfig config) { _config = config; ScheduleAutoBoss(); }
    private void ScheduleAutoBoss() => _nextAutoBoss = _now().AddMinutes(Math.Clamp(_config.Adventures.Boss.AutoStartMinutes, 1, 1440));
    public static bool Recognizes(string command) => command is "!fischen" or "!sammlung" or "!schatzsuche" or "!rennen" or "!joinrace" or "!boss" or "!joinboss";
    public async Task RunAsync(CancellationToken token)
    {
        var overlay = new AdventureOverlayServer(() => Status).RunAsync(token);
        try
        {
            while (true)
            {
                await Task.Delay(1000, token);
                try { await TickAsync(token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { Console.WriteLine("Abenteuerspiele: " + ex.Message); }
            }
        }
        finally { await CancelAsync(); await overlay; }
    }
    public async Task CancelAsync()
    {
        await _gate.WaitAsync();
        try { _race = null; _boss = null; }
        finally { PublishStatus(); _gate.Release(); }
    }
    public async Task ControlAsync(string action, long stake, CancellationToken token)
    {
        if (action is "cancelrace" or "cancelboss")
        {
            await _gate.WaitAsync(token);
            try
            {
                if (action == "cancelrace" && _race is not null)
                {
                    var template = _race.Config.Adventures.Race.CancelMessage;
                    _nextRace = _now().AddSeconds(Math.Clamp(_race.Config.Adventures.Race.RoundCooldownSeconds, 1, 86400));
                    _race = null;
                    await Say(Format(template, ("reason", "manuell abgebrochen")), token);
                }
                if (action == "cancelboss" && _boss is not null)
                {
                    var template = _boss.Config.CancelMessage;
                    _nextBoss = _now().AddSeconds(Math.Clamp(_boss.Config.RoundCooldownSeconds, 1, 86400));
                    _boss = null;
                    await Say(Format(template), token);
                }
            }
            finally { PublishStatus(); _gate.Release(); }
            return;
        }
        var command = action == "boss" ? "!boss" : "!rennen";
        var parts = action == "boss" ? new[] { command } : new[] { command, stake.ToString(CultureInfo.InvariantCulture) };
        await HandleAsync(new ChatMessage { UserId = _broadcasterId, UserName = "Streamer", UserLogin = "streamer",
            IsBroadcaster = true, Text = string.Join(' ', parts) }, command, parts, token);
    }
    private long Maximum => _config.MaximumAccountEnabled ? _config.MaximumAccountPoints : long.MaxValue;
    private string Currency => _config.CurrencyPlural;
    private Task Say(string text, CancellationToken token) => string.IsNullOrWhiteSpace(text) ? Task.CompletedTask :
        _send(text.Length <= 480 ? text : text[..477] + "…", token);
    internal static string Format(string template, params (string Key, object Value)[] values)
    {
        var text = template ?? "";
        foreach (var (key, value) in values) text = text.Replace("{" + key + "}", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", StringComparison.OrdinalIgnoreCase);
        return text.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
    private bool Cooling(string key, int seconds)
    {
        if (_cooldowns.TryGetValue(key, out var until) && _now() < until) return true;
        if (_cooldowns.Count > 5000)
            foreach (var expired in _cooldowns.Where(p => p.Value <= _now()).Select(p => p.Key).ToArray()) _cooldowns.Remove(expired);
        _cooldowns[key] = _now().AddSeconds(Math.Clamp(seconds, 1, 86400));
        return false;
    }
    public async Task HandleAsync(ChatMessage message, string command, string[] parts, CancellationToken token)
    {
        if (!Recognizes(command) || !_config.Enabled || !_config.PointsEnabled ||
            message.CommandAuthorization == CommandAuthorization.Denied ||
            _config.PointsBlacklist.Any(p => p.TrimStart('@').Equals(message.UserLogin, StringComparison.OrdinalIgnoreCase) ||
                p.TrimStart('@').Equals(message.UserName, StringComparison.OrdinalIgnoreCase))) return;
        await _gate.WaitAsync(token);
        try
        {
            await TickCoreAsync(token);
            var settings = _config.Adventures;
            if (command == "!sammlung")
            {
                if (Cooling("collection:" + message.UserId, 15)) return;
                var profile = await _points.GetProfileAsync(message.UserId, token);
                var fish = profile.Entry.FishCollection ?? new();
                await Say($"@{message.UserName}, Fischsammlung: " + (fish.Count == 0 ? "Noch keine Fänge. Nutze !fischen <Einsatz>." :
                    string.Join(", ", fish.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}x")) + $" | Größter Fang: {profile.Entry.BiggestFishName} ({profile.Entry.BiggestFishGrams / 1000.0:0.00} kg)."), token);
                return;
            }
            if (command is "!boss" or "!joinboss")
            {
                if (!settings.Boss.Enabled) return;
                if (parts.Length != 1) { await Say("Nutzung: !boss zum Starten, !joinboss zum Angreifen – ohne Einsatz.", token); return; }
                if (command == "!boss")
                {
                    if (!EventStartCommand.IsAllowed(message, _broadcasterId))
                    {
                        await Say($"@{message.UserName}, nur Mods/Broadcaster dürfen den Boss starten. Mit !joinboss kannst du einem aktiven Kampf beitreten.", token);
                        return;
                    }
                    if (_boss is not null) await Say($"Boss aktiv: {_boss.Hp} LP. Mit !joinboss angreifen!", token);
                    else if (_now() < _nextBoss) await Say("Der nächste Boss erscheint nach der Rundenpause.", token);
                    else await SpawnBossAsync(token);
                }
                else if (_boss is null) await Say("Aktuell ist kein Boss aktiv. Start mit !boss oder auf den Auto-Start warten.", token);
                else await AttackBossAsync(message, token);
                return;
            }
            var game = command == "!fischen" ? settings.Fishing : command == "!schatzsuche" ? settings.Treasure : settings.Race;
            if (!game.Enabled) return;
            if (command == "!joinrace" && _race is null) { await Say("Aktuell läuft keine Beitrittsphase. Rennen mit !rennen <Betrag> starten.", token); return; }
            var balance = await _points.GetPointsAsync(message.UserId, token);
            var available = Math.Max(0, balance - Math.Max(0, _config.MinimumPoints));
            var max = Math.Clamp(game.MaximumBet, 1, long.MaxValue / 500);
            var min = Math.Clamp(game.MinimumBet, 1, max);
            long stake = 0;
            var valid = command == "!joinrace" && parts.Length == 1 && _race is not null
                ? (stake = _race.Stake) > 0
                : parts.Length == 2 && (parts[1].Equals("all", StringComparison.OrdinalIgnoreCase)
                ? (stake = Math.Min(available, max)) > 0
                : long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out stake));
            if (!valid || stake < min || stake > max || stake > available)
            {
                await Say($"@{message.UserName}, Nutzung: {command} <Betrag|all>. Einsatz {min}–{max} {Currency}, verfügbar: {available}.", token);
                return;
            }
            if (command is "!rennen" or "!joinrace") { await JoinRaceAsync(message, stake, token); return; }
            if (Cooling(command + ":" + message.UserId, game.CooldownSeconds)) return;
            var roll = _random.NextInclusive(1, 100);
            var (multiplier, outcome) = SoloOutcome(command, roll);
            var payout = checked(stake * multiplier);
            var fishName = command == "!fischen" && multiplier > 0 ? roll <= 80 ? "Rotfeder" : roll <= 97 ? "Hecht" : roll == 98 ? "Goldener Koi" : roll == 99 ? "Mondaal" : "Kraken" : null;
            var fishGrams = fishName is null ? 0 : _random.NextInclusive(multiplier >= 8 ? 5000 : 100, multiplier >= 8 ? 20000 : 4000);
            var result = await _points.ApplyCasinoAsync(message.UserId, message.UserName,
                command == "!fischen" ? "Fischen" : "Schatzsuche", stake, payout,
                _config.MinimumPoints, Maximum,
                _config.DailyGambleLimitEnabled ? _config.DailyGambleLimit : 0,
                _config.DailyLossLimitEnabled ? _config.DailyLossLimit : 0,
                _config.DailyWinLimitEnabled ? _config.DailyWinLimit : 0,
                0, false, 0, _config.HistoryLimit, token, fishName, fishGrams);
            if (result.Success) DataChanged?.Invoke();
            await Say(result.Success
                ? Format(game.ResultMessage, ("user", message.UserName), ("outcome", outcome + (fishName is null ? "" : $" {fishName}: {fishGrams / 1000.0:0.00} kg.")),
                    ("stake", stake), ("payout", payout), ("currency", Currency), ("balance", result.Balance))
                : $"@{message.UserName}, {result.Error}.", token);
        }
        finally { PublishStatus(); _gate.Release(); }
    }
    internal static (long Multiplier, string Text) SoloOutcome(string command, int roll) => command == "!fischen"
        ? roll <= 50 ? (0, "Ein alter Stiefel – kein Fang!") : roll <= 80 ? (1, "Ein kleiner Fisch – Einsatz zurück!") : roll <= 97 ? (2, "Ein prächtiger Fang!") : (8, "Ein legendärer Fisch beißt an!")
        : roll <= 50 ? (0, "Die Höhle war eine Falle – keine Beute!") : roll <= 75 ? (1, "Eine kleine Truhe deckt deinen Einsatz!") : roll <= 95 ? (2, "Du entdeckst eine Schatzkammer!") : (6, "Du findest den versunkenen Königsschatz!");

    private async Task JoinRaceAsync(ChatMessage user, long stake, CancellationToken token)
    {
        if (_race is null)
        {
            if (_now() < _nextRace) { await Say("Das nächste Rennen startet nach der Rundenpause.", token); return; }
            var c = _config.Adventures.Race;
            _race = new(stake, _now().AddSeconds(Math.Clamp(c.JoinSeconds, 5, 600)), Math.Clamp(c.MaximumPlayers, 2, 500),
                System.Text.Json.JsonSerializer.Deserialize<MinigameConfig>(System.Text.Json.JsonSerializer.Serialize(_config))!, new());
            await Say(Format(c.StartMessage, ("seconds", Math.Clamp(c.JoinSeconds, 5, 600)), ("stake", stake), ("currency", Currency)), token);
        }
        if (_race.Stake != stake) { await Say($"@{user.UserName}, für dieses Rennen gilt ein Einsatz von {_race.Stake}. Beitritt mit !joinrace.", token); return; }
        if (_race.Players.ContainsKey(user.UserId)) { await Say($"@{user.UserName}, du bist bereits im Rennen.", token); return; }
        if (_race.Players.Count >= _race.MaximumPlayers) { await Say("Das Rennen ist voll.", token); return; }
        if (Cooling("!rennen:" + user.UserId, _config.Adventures.Race.CooldownSeconds)) return;
        _race.Players.Add(user.UserId, user.UserName);
        await Say(Format(_race.Config.Adventures.Race.JoinMessage, ("user", user.UserName), ("count", _race.Players.Count), ("stake", stake), ("currency", Currency)), token);
    }
    private async Task SpawnBossAsync(CancellationToken token)
    {
        if (_boss is null)
        {
            if (_now() < _nextBoss) { await Say("Der nächste Boss erscheint nach der Rundenpause.", token); return; }
            var c = System.Text.Json.JsonSerializer.Deserialize<BossAdventureConfig>(System.Text.Json.JsonSerializer.Serialize(_config.Adventures.Boss))!;
            _boss = new() { Config = c, Hp = Math.Clamp(c.HitPoints, 10, 1000000), End = _now().AddSeconds(Math.Clamp(c.DurationSeconds, 10, 1800)) };
            ScheduleAutoBoss();
            await Say(Format(c.StartMessage, ("hp", _boss.Hp), ("seconds", Math.Clamp(c.DurationSeconds, 10, 1800)), ("reward", Math.Clamp(c.RewardPool, 0, 1000000000)), ("currency", Currency)), token);
        }
    }
    private async Task AttackBossAsync(ChatMessage user, CancellationToken token)
    {
        if (_boss is null) return;
        if (Cooling("!boss:" + user.UserId, _boss.Config.AttackCooldownSeconds)) return;
        _boss.Players[user.UserId] = user.UserName;
        var damage = _random.NextInclusive(10, 30);
        _boss.Hp = Math.Max(0, _boss.Hp - damage);
        if (_boss.Hp > 0) { await Say(Format(_boss.Config.AttackMessage, ("user", user.UserName), ("damage", damage), ("hp", _boss.Hp)), token); return; }
        var finished = _boss;
        _boss = null;
        _nextBoss = _now().AddSeconds(Math.Clamp(finished.Config.RoundCooldownSeconds, 1, 86400));
        var reward = Math.Clamp(finished.Config.RewardPool, 0, 1000000000) / finished.Players.Count;
        await _points.AddPointsToUsersAsync(finished.Players.Select(p => (p.Key, p.Value)), reward,
            _config.MinimumPoints, Maximum, token, "Bosskampf", _config.HistoryLimit);
        DataChanged?.Invoke();
        await Say(Format(finished.Config.WinMessage, ("count", finished.Players.Count), ("reward", reward), ("currency", Currency)), token);
    }
    public async Task TickAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try { await TickCoreAsync(token); }
        finally { PublishStatus(); _gate.Release(); }
    }
    private async Task TickCoreAsync(CancellationToken token)
    {
        if (!_config.Enabled || !_config.PointsEnabled)
        { _race = null; _boss = null; return; }
        if (!_config.Adventures.Race.Enabled) _race = null;
        if (!_config.Adventures.Boss.Enabled) _boss = null;
        if (_boss is not null && _now() >= _boss.End)
        {
            _nextBoss = _now().AddSeconds(Math.Clamp(_boss.Config.RoundCooldownSeconds, 1, 86400));
            var template = _boss.Config.TimeoutMessage;
            _boss = null;
            await Say(Format(template), token);
        }
        if (_boss is null && _config.Adventures.Boss.Enabled && _config.Adventures.Boss.AutoStartEnabled &&
            _now() >= _nextAutoBoss && _now() >= _nextBoss) await SpawnBossAsync(token);
        if (_race is null || _now() < _race.End) return;
        var race = _race;
        _race = null;
        _nextRace = _now().AddSeconds(Math.Clamp(race.Config.Adventures.Race.RoundCooldownSeconds, 1, 86400));
        if (race.Players.Count < 2) { await Say(Format(race.Config.Adventures.Race.CancelMessage, ("reason", "weniger als 2 Teilnehmer")), token); return; }
        var players = race.Players.Select(p => (UserId:p.Key, DisplayName:p.Value)).ToArray();
        var winner = _random.NextInclusive(0, players.Length - 1);
        var result = await _points.ResolveRaceAsync(players, winner, race.Stake, race.Config, token);
        if (result.Success) DataChanged?.Invoke();
        await Say(result.Success ? Format(race.Config.Adventures.Race.WinMessage, ("user", players[winner].DisplayName),
            ("pot", race.Stake * players.Length), ("currency", Currency), ("balance", result.Balance))
            : Format(race.Config.Adventures.Race.CancelMessage, ("reason", result.Error)), token);
    }
}
