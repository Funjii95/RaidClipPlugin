using System.Globalization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record BossAttackResult(bool Success, string PlayerId, int Damage, bool Critical, int BossHp,
    bool Defeated, int RemainingCooldown, string Error);

public sealed partial class AdventureGameService
{
    internal Func<bool>? GameplayAllowed;

    public event Action? DataChanged;
    public event Action<CommunityEventType,string>? OverlayEventStarted;
    public Func<CommunityOverlayState>? CommunityOverlay { get; set; }
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
    private bool _recoverHealthBoss;
    public sealed class HealthBossSnapshot
    {
        public bool Active {get;set;}
        public string Id {get;set;}="";
        public BossAdventureConfig Config {get;set;}=new();
        public bool DynamicScaling {get;set;}
        public DateTimeOffset End {get;set;}
    }
    private void SaveHealthBoss(BossRound? boss)
    {
        if((!_config.Adventures.Rpg.Enabled || !_config.Adventures.Rpg.Health.Enabled) && (boss is not null || !File.Exists(_points.BossRoundPath)))return;
        RpgAtomicFile.Save(_points.BossRoundPath,new HealthBossSnapshot {Active=boss is not null,Id=boss?.Id??"",Config=boss?.Config??new(),End=boss?.End??default,DynamicScaling=boss?.DynamicScaling??false},_=>{});
    }
    public AdventureStatus Status => Volatile.Read(ref _status);
    private void PublishStatus()
    {
        var previous=Status;
        var current=new AdventureStatus(
        _boss is not null, _boss?.Hp ?? 0, _boss?.MaximumHp ?? 0,
        _boss?.Players.Count ?? 0, _boss?.End, _race is not null, _race?.Players.Count ?? 0, _race?.Stake ?? 0, _race?.End,
        _boss?.Id??"",_boss?.End.AddSeconds(-Math.Clamp(_boss.Config.DurationSeconds,10,1800)),_race?.Id??"",_race?.End.AddSeconds(-Math.Clamp(_race.Config.Adventures.Race.JoinSeconds,5,600)),_boss?.Config.DisplayName??"Perlenwächter",_boss?.Config.IsMimic??false,_boss?.Config.IsMimic==true?_config.Adventures.Rpg.Mimic.LegendaryChance:_config.Adventures.Rpg.Mimic.NormalBossLegendaryChance);
        Volatile.Write(ref _status,current);
        if(current.BossActive && current.BossEventId!=previous.BossEventId)NotifyOverlayStart(CommunityEventType.Boss,current.BossEventId);
        if(current.RaceActive && current.RaceEventId!=previous.RaceEventId)NotifyOverlayStart(CommunityEventType.Race,current.RaceEventId);
    }
    private void NotifyOverlayStart(CommunityEventType type,string id)
    {
        if(OverlayEventStarted is null)return;
        foreach(Action<CommunityEventType,string> callback in OverlayEventStarted.GetInvocationList())
            try{callback(type,id);}catch(Exception ex){Console.WriteLine("Event-Anzeige konnte nicht aktualisiert werden: "+ex.Message);}
    }
    private sealed record RaceRound(long Stake, DateTimeOffset End, int MaximumPlayers,
        MinigameConfig Config, Dictionary<string, string> Players)
    {
        public string Id {get;}=Guid.NewGuid().ToString("N");
    }
    private sealed class BossRound
    {
        public string Id = Guid.NewGuid().ToString("N");
        public required BossAdventureConfig Config;
        public required DateTimeOffset End;
        public int Hp;
        public bool DynamicScaling;
        public int MaximumHp => DynamicScaling ? checked(100*Math.Max(1,Players.Count)) : Math.Clamp(Config.HitPoints,10,1000000);
        public Dictionary<string, string> Players = new();
    }

    public AdventureGameService(ViewerPointStore points, MinigameConfig config,
        Func<string, CancellationToken, Task> send, IDuelRandom? random = null,
        Func<DateTimeOffset>? now = null, string broadcasterId = "")
    {
        _points = points; _config = config; _points.ConfigureHotTime(config.Adventures.Rpg); _send = send; _random = random ?? new CryptoDuelRandom(); _now = now ?? (() => DateTimeOffset.UtcNow); _broadcasterId = broadcasterId; ScheduleAutoBoss();
        if(config.Adventures.Rpg.Enabled && config.Adventures.Rpg.Health.Enabled && File.Exists(points.BossRoundPath))
        {
            var saved=RpgAtomicFile.Load<HealthBossSnapshot>(points.BossRoundPath,_=>{});
            if(saved.Active)
            {
                _boss=new(){Id=saved.Id,Config=saved.Config,End=saved.End,Hp=Math.Clamp(saved.Config.HitPoints,10,1000000),DynamicScaling=saved.DynamicScaling};
                _recoverHealthBoss=true;
            }
        }
    }

    public void UpdateConfig(MinigameConfig config) { _config = config; _points.ConfigureHotTime(config.Adventures.Rpg); ScheduleAutoBoss(); }
    private void ScheduleAutoBoss() => _nextAutoBoss = _now().AddMinutes(Math.Clamp(_config.Adventures.Boss.AutoStartMinutes, 1, 1440));
    public static bool Recognizes(string command) => RpgChatCommand.Recognizes(command) || command is "!fish" or "!sammlung" or "!dive" or "!startrace" or "!joinrace" or "!heal" or "!kill" or "!sim" or "!boss" or "!start boss" or "!start mimic" or "!attack" or "!joinboss";
    public async Task RunAsync(CancellationToken token)
    {
        var overlay = new AdventureOverlayServer(() => Status,community:()=>CommunityOverlay?.Invoke()??new CommunityOverlayEventManager().Read(Status,_now())).RunAsync(token);
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
    public async Task ControlAsync(string action, long stake, CancellationToken token, TwitchUser? broadcaster = null)
    {
        if (GameplayAllowed?.Invoke() == false) return;
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
                    await CloseBossSushiAsync(_boss,token);
                    var template = _boss.Config.CancelMessage;
                    _nextBoss = _now().AddSeconds(Math.Clamp(_boss.Config.RoundCooldownSeconds, 1, 86400));
                    SaveHealthBoss(null);
                    _boss = null;
                    await Say(Format(template), token);
                }
            }
            finally { PublishStatus(); _gate.Release(); }
            return;
        }
        var command = action == "boss" ? "!start boss" : "!startrace";
        if (action != "boss" && (broadcaster is null || broadcaster.Id != _broadcasterId))
            throw new InvalidOperationException("Das verbundene Twitch-Konto konnte nicht zugeordnet werden. Bitte Module neu verbinden.");
        var parts = action == "boss" ? new[] { command } : new[] { command, stake.ToString(CultureInfo.InvariantCulture) };
        await HandleAsync(new ChatMessage { UserId = _broadcasterId, UserName = broadcaster?.DisplayName ?? _broadcasterId, UserLogin = broadcaster?.Login ?? "",
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
    public async Task HandleAsync(ChatMessage message, string command, string[] parts, CancellationToken token, string? expectedRace=null)
    {
        if (GameplayAllowed?.Invoke() == false) return;
        if (!Recognizes(command) || !_config.Enabled || !_config.PointsEnabled ||
            message.CommandAuthorization == CommandAuthorization.Denied ||
            _config.PointsBlacklist.Any(p => p.TrimStart('@').Equals(message.UserLogin, StringComparison.OrdinalIgnoreCase) ||
                p.TrimStart('@').Equals(message.UserName, StringComparison.OrdinalIgnoreCase))) return;
        await _gate.WaitAsync(token);
        try
        {
            if(expectedRace is not null && (_race?.Id??"-")!=expectedRace)return;
            if (command == "!sim") { await SimulateBossAsync(message,parts,token); return; }
            await TickCoreAsync(token);
            if (command == "!joinboss") command = "!attack";
            var settings = _config.Adventures;
            if (await HandleAdministrationAsync(message,command,parts,token)) return;
            if (RpgChatCommand.Recognizes(command))
            {
                var rpgCooldown = "rpgview:" + message.UserId;
                if (Cooling(rpgCooldown, 2))
                {
                    var remaining = Math.Max(1, (int)Math.Ceiling((_cooldowns[rpgCooldown] - _now()).TotalSeconds));
                    await Say($"🦦 @{message.UserName}, bitte {remaining} Sekunden bis zum nächsten RPG-Befehl warten.", token);
                    return;
                }
                await Say(await new RpgChatCommand(_points, settings.Rpg, Maximum).ExecuteAsync(message, command, parts, token), token);
                DataChanged?.Invoke();
                return;
            }
            if (settings.Rpg.Enabled && command is "!fish" or "!dive")
            {
                var gameConfig = command == "!fish" ? settings.Fishing : settings.Treasure;
                long rpgStake = 0;
                var availablePoints = Math.Max(0, await _points.GetPointsAsync(message.UserId, token) - Math.Max(0, _config.MinimumPoints));
                var validStake = parts.Length == 1 || parts.Length == 2 &&
                    (parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) ?
                        (rpgStake = Math.Min(availablePoints, gameConfig.MaximumBet)) > 0 :
                        long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out rpgStake));
                if (!validStake) { await Say($"@{message.UserName}, Nutzung: {command} [Betrag|all].", token); return; }
                var actionId = string.IsNullOrWhiteSpace(message.Id) ? Guid.NewGuid().ToString("N") : message.Id;
                var rpgResult = await new RpgAdventureService(_points, _config, _random, _now).ExecuteAsync(
                    message.UserId, message.UserName, actionId, command == "!fish" ? RpgActivity.Fishing : RpgActivity.Dive, rpgStake, token);
                if (rpgResult.Success) DataChanged?.Invoke();
                if(rpgResult.BossDiscovered)await StartDiscoveredBossCoreAsync(message.UserName,token);
                await Say(rpgResult.Success ? Format(gameConfig.ResultMessage, ("user", message.UserName),
                    ("outcome", RpgChatPresentation.AdventureOutcome(rpgResult, command == "!fish" ? RpgActivity.Fishing : RpgActivity.Dive)), ("stake", rpgStake), ("payout", rpgResult.Payout),
                    ("currency", Currency), ("balance", rpgResult.Balance)) : $"@{message.UserName}, {rpgResult.Error}", token);
                if (rpgResult.Success && rpgResult.Item?.Rarity == ItemRarity.Legendary)
                    await Say(Format(settings.Rpg.LegendaryMessage, ("user", message.UserName), ("item", rpgResult.Item.DisplayName)), token);
                return;
            }
            if (command == "!sammlung")
            {
                if (Cooling("collection:" + message.UserId, 15)) return;
                var profile = await _points.GetProfileAsync(message.UserId, token);
                if (settings.Rpg.Enabled && profile.Entry.Rpg.LegacyFishImported)
                {
                    var stocks = profile.Entry.Rpg.Fish.Where(p => p.Value.Quantity > 0).Select(p =>
                        $"{settings.Rpg.FishSpecies.SingleOrDefault(f => f.Id == p.Key)?.Name ?? p.Key}: {p.Value.Quantity}×").ToArray();
                    await Say($"🎣 @{message.UserName}, Fischvorrat: " + (stocks.Length == 0 ? "Noch keine Fische. Nutze !fish <Einsatz>." : string.Join(", ", stocks)), token);
                    return;
                }
                var fish = profile.Entry.FishCollection ?? new();
                await Say($"@{message.UserName}, Fischsammlung: " + (fish.Count == 0 ? "Noch keine Fänge. Nutze !fish <Einsatz>." :
                    string.Join(", ", fish.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}x")) + $" | Größter Fang: {profile.Entry.BiggestFishName} ({profile.Entry.BiggestFishGrams / 1000.0:0.00} kg)."), token);
                return;
            }
            if (command == "!start mimic")
            {
                if (string.IsNullOrWhiteSpace(_broadcasterId) || message.UserId != _broadcasterId)
                { await Say("Nur der Broadcaster darf !start mimic verwenden.", token); return; }
                if (parts.Length != 1) { await Say("Nutzung: !start mimic.", token); return; }
                if (!settings.Boss.Enabled || !settings.Rpg.Enabled || !settings.Rpg.Mimic.Enabled)
                { await Say("Mimic oder RPG-Bosskämpfe sind deaktiviert.", token); return; }
                if (_boss is not null) { await Say("Es läuft bereits ein Bosskampf. Mit !attack teilnehmen!", token); return; }
                await SpawnBossAsync(token, mimic:true);
                return;
            }
            if (command is "!boss" or "!start boss" or "!attack")
            {
                if (!settings.Boss.Enabled) return;
                if (parts.Length != 1) { await Say("Nutzung: !boss für die Boss-Pause, !start boss zum Starten, !attack zum Angreifen – ohne Einsatz.", token); return; }
                if (command == "!boss")
                {
                    if (_boss is not null) await Say($"Boss aktiv: {_boss.Hp} LP. Mit !attack angreifen! Die nächste Boss-Pause beginnt nach dem Kampf.", token);
                    else
                    {
                        var next = settings.Boss.AutoStartEnabled && _nextAutoBoss > _nextBoss ? _nextAutoBoss : _nextBoss;
                        var seconds = Math.Max(0, (long)Math.Ceiling((next - _now()).TotalSeconds));
                        await Say(seconds > 0
                            ? $"Nächster Boss in {seconds / 60} Min. {seconds % 60} Sek."
                            : "Boss-Pause abgelaufen. Mods/Broadcaster können mit !start boss starten.", token);
                    }
                }
                else if (command == "!start boss")
                {
                    if (!EventStartCommand.IsAllowed(message, _broadcasterId))
                    {
                        await Say($"@{message.UserName}, nur Mods/Broadcaster dürfen mit !start boss einen Boss starten.", token);
                        return;
                    }
                    if (_boss is not null) await Say($"Boss aktiv: {_boss.Hp} LP. Mit !attack angreifen!", token);
                    else if (_now() < _nextBoss) await Say("Der nächste Boss erscheint nach der Rundenpause. Verbleibende Zeit mit !boss.", token);
                    else await SpawnBossAsync(token);
                }
                else if (_boss is null) await Say("Aktuell ist kein Boss aktiv. Start mit !start boss oder auf den Auto-Start warten.", token);
                else await AttackBossAsync(message, token);
                return;
            }
            StakeAdventureConfig game = command == "!fish" ? settings.Fishing : command == "!dive" ? settings.Treasure : settings.Race;
            if (!game.Enabled) return;
            if (command == "!joinrace" && _race is null) { await Say("Aktuell läuft keine Beitrittsphase. Rennen mit !start race <Betrag> starten.", token); return; }
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
                await Say($"@{message.UserName}, Nutzung: {(command == "!startrace" ? "!start race" : command)} <Betrag|all>. Einsatz {min}–{max} {Currency}, verfügbar: {available}.", token);
                return;
            }
            if (command is "!startrace" or "!joinrace") { await JoinRaceAsync(message, stake, token,expectedRace is not null); return; }
            var cooldownKey = command + ":" + message.UserId;
            if (Cooling(cooldownKey, game.CooldownSeconds))
            {
                var remaining = Math.Max(1, (int)Math.Ceiling((_cooldowns[cooldownKey] - _now()).TotalSeconds));
                await Say(Format(game.CooldownMessage, ("user", message.UserName), ("command", command), ("remaining", remaining)), token);
                return;
            }
            var roll = _random.NextInclusive(1, 100);
            var (multiplier, outcome, tier) = SoloOutcome(command, roll, (SoloAdventureConfig)game);
            var payout = checked((long)decimal.Floor(stake * multiplier));
            var fishName = command == "!fish" && tier > 0 ? tier == 1 ? "Rotfeder" : tier == 2 ? "Hecht" :
                _random.NextInclusive(1, 3) switch { 1 => "Goldener Koi", 2 => "Mondaal", _ => "Kraken" } : null;
            var fishGrams = fishName is null ? 0 : _random.NextInclusive(tier == 3 ? 5000 : 100, tier == 3 ? 20000 : 4000);
            var result = await _points.ApplyCasinoAsync(message.UserId, message.UserName,
                command == "!fish" ? "Fischen" : "Schatzsuche", stake, payout,
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
    internal static (decimal Multiplier, string Text, int Tier) SoloOutcome(string command, int roll, SoloAdventureConfig config)
    {
        config.Validate(command);
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        var tier = roll <= config.LossChance ? 0 : roll <= config.LossChance + config.SmallChance ? 1 :
            roll <= config.LossChance + config.SmallChance + config.LargeChance ? 2 : 3;
        var multipliers = new[] { config.LossMultiplier, config.SmallMultiplier, config.LargeMultiplier, config.RareMultiplier };
        var texts = command == "!fish"
            ? new[] { "Ein alter Stiefel – kein Fang!", "Ein kleiner Fisch!", "Ein prächtiger Fang!", "Ein legendärer Fisch beißt an!" }
            : new[] { "Die Höhle war eine Falle!", "Du findest eine kleine Truhe!", "Du entdeckst eine Schatzkammer!", "Du findest den versunkenen Königsschatz!" };
        return (multipliers[tier], texts[tier], tier);
    }

    private async Task JoinRaceAsync(ChatMessage user, long stake, CancellationToken token,bool remote=false)
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
        if (Cooling("!startrace:" + user.UserId, _config.Adventures.Race.CooldownSeconds)) return;
        void AddParticipant()=>_race.Players.Add(user.UserId,user.UserName);
        if(remote && _points.Admission is {} admission)admission.Commit(AddParticipant);else AddParticipant();
        await Say(Format(_race.Config.Adventures.Race.JoinMessage, ("user", user.UserName), ("count", _race.Players.Count), ("stake", stake), ("currency", Currency)), token);
    }
    private async Task SpawnBossAsync(CancellationToken token,bool mimic=false)
    {
        if (_boss is null)
        {
            if (_now() < _nextBoss) { await Say("Der nächste Boss erscheint nach der Rundenpause.", token); return; }
            var c = System.Text.Json.JsonSerializer.Deserialize<BossAdventureConfig>(System.Text.Json.JsonSerializer.Serialize(_config.Adventures.Boss))!;
            c.IsMimic=mimic;
            c.DisplayName=mimic?_config.Adventures.Rpg.Mimic.Name:"Perlenwächter";
            if(mimic){var rules=_config.Adventures.Rpg.Mimic;c.HpMode=BossHpMode.Fixed;c.HitPoints=rules.HitPoints;c.DurationSeconds=rules.DurationSeconds;c.AttackCooldownSeconds=rules.AttackCooldownSeconds;c.RewardPool=rules.RewardPool;}
            if(mimic)c.StartMessage="Eine Schatztruhe schnappt zu: MIMIC! {hp} LP, {seconds} Sekunden. Alle greifen mit !attack an! Legendary-Chance nach Sieg: "+_config.Adventures.Rpg.Mimic.LegendaryChance+" % je Überlebendem.";
            if(c.HpMode == BossHpMode.Dynamic)c.HitPoints=100;
            _boss = new() { Config = c, DynamicScaling=c.HpMode == BossHpMode.Dynamic, Hp = Math.Clamp(c.HitPoints, 10, 1000000), End = _now().AddSeconds(Math.Clamp(c.DurationSeconds, 10, 1800)) };
            SaveHealthBoss(_boss);
            ScheduleAutoBoss();
            _points.PublishStory(new(mimic?GroqScene.Mimic:GroqScene.Boss,"boss-start:"+_boss.Id,"Kampf gestartet"));
            await Say(Format(c.StartMessage, ("hp", _boss.Hp), ("seconds", Math.Clamp(c.DurationSeconds, 10, 1800)), ("reward", Math.Clamp(c.RewardPool, 0, 1000000000)), ("currency", Currency)), token);
        }
    }
    private async Task<bool> StartDiscoveredBossCoreAsync(string finder,CancellationToken token)
    {
        if(GameplayAllowed?.Invoke()==false || !_config.Enabled || !_config.PointsEnabled || !_config.Adventures.Rpg.Enabled || !_config.Adventures.Rpg.Mimic.Enabled || !_config.Adventures.Boss.Enabled || _boss is not null || _now()<_nextBoss)return false;
        await SpawnBossAsync(token,mimic:true);
        PublishStatus();
        if(_boss is null)return false;
        DataChanged?.Invoke();
        await Say($"@{finder} hat beim Tauchen eine Mimic entdeckt! Alle können mit !attack teilnehmen.",token);
        return true;
    }
    public async Task<bool> StartDiscoveredBossAsync(string finder,CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try{return await StartDiscoveredBossCoreAsync(finder,token);}
        finally{PublishStatus();_gate.Release();}
    }
    public async Task<BossAttackResult> ExecuteBossAttackAsync(string verifiedUserId, string displayName, string requestId, CancellationToken token, bool communityVictory=false)
    {
        if (GameplayAllowed?.Invoke() == false) return new(false, verifiedUserId, 0, false, 0, false, 0, "Plugin nicht verbunden oder Sitzung nicht spielbereit.");
        if (string.IsNullOrWhiteSpace(verifiedUserId) || string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Twitch-ID und Aktions-ID fehlen.");
        await _gate.WaitAsync(token);
        try
        {
            if (!_config.Enabled || !_config.PointsEnabled || !_config.Adventures.Boss.Enabled)
                return new(false, verifiedUserId, 0, false, _boss?.Hp ?? 0, false, 0, "Bosskampf ist deaktiviert.");
            await TickCoreAsync(token);
            return await AttackBossAsync(new() { UserId = verifiedUserId, UserName = displayName, Id = requestId }, token,communityVictory);
        }
        finally { PublishStatus(); _gate.Release(); }
    }

    private async Task<BossAttackResult> AttackBossAsync(ChatMessage user,CancellationToken token,bool communityVictory=true)
    {
        var round=_boss;
        var expanding=round is {DynamicScaling:true} && round.Players.Count>0 && !round.Players.ContainsKey(user.UserId);
        if(expanding)round!.Hp=checked(round.Hp+100);
        try{return await AttackBossCoreAsync(user,token,communityVictory);}
        finally
        {
            // Admission/cooldown/failed save must not count as participation. A committed hit registers before output.
            if(expanding && !round!.Players.ContainsKey(user.UserId))round.Hp-=100;
        }
    }
    private async Task<BossAttackResult> AttackBossCoreAsync(ChatMessage user, CancellationToken token,bool communityVictory=true)
    {
        if (_boss is null) return new(false, user.UserId, 0, false, 0, false, 0, "Kein Boss aktiv.");
        var cooldownKey = "!boss:" + user.UserId;
        if (!_config.Adventures.Rpg.Enabled && Cooling(cooldownKey, _boss.Config.AttackCooldownSeconds))
        {
            var remaining = Math.Max(1, (int)Math.Ceiling((_cooldowns[cooldownKey] - _now()).TotalSeconds));
            await Say(Format(_boss.Config.AttackCooldownMessage, ("user", user.UserName), ("remaining", remaining)), token);
            return new(false, user.UserId, 0, false, _boss.Hp, false, remaining, "Angriffs-Cooldown aktiv.");
        }
        var damage = _random.NextInclusive(10, 30);
        var critical = false;
        var extraHits = 0;
        var hpEnabled = _config.Adventures.Rpg.Enabled && _config.Adventures.Rpg.Health.Enabled;
        var receivedDamage = 0;
        var lifeleechHealing = 0;
        var healthText = "";
        var playerDied = false;
        if (_config.Adventures.Rpg.Enabled)
        {
            var mutation = await _points.ApplyRpgAsync(user.UserId, user.UserName,
                string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id, account =>
                {
                    var clock = _now();
                    if (hpEnabled)
                    {
                        RpgHealthService.Refresh(account.Rpg,_config.Adventures.Rpg.Health,clock);
                        var health=account.Rpg.Health;
                        if(health.CurrentHp<=0 || health.BossId==_boss.Id && health.Defeated)
                            return new(false,"Du bist besiegt. Für diesen Boss ist kein weiterer Angriff möglich; HP: !hp.");
                    }
                    if (account.Rpg.Cooldowns.TryGetValue("!attack", out var until) && clock < until)
                        return new(false, $"Angriff in {Math.Ceiling((until - clock).TotalSeconds)} Sekunden wieder möglich.");
                    var hit = RpgPassiveService.BossAttack(damage, account.Rpg, _config.Adventures.Rpg, _random, clock);
                    damage = RpgDepthsCombatService.MitigateBoss(hit.Damage,_boss.Config.DamageReductionPercent,hit.Critical,account.Rpg,_config.Adventures.Rpg); critical = hit.Critical; extraHits = hit.ExtraHits;
                    if(hpEnabled)
                    {
                        var health=account.Rpg.Health;
                        if(health.BossId!=_boss.Id) {health.BossId=_boss.Id;health.Defeated=false;health.BossDamage=0;}
                        health.BossDamage=checked(health.BossDamage+Math.Min(damage,_boss.Hp));
                        health.BossRemainingHp=Math.Max(0,_boss.Hp-damage);
                        var combatStats=CharacterStatService.Calculate(account.Rpg,_config.Adventures.Rpg,RpgActivity.Boss,now:clock);
                        var bloodLeech=RpgDepthsCombatService.Has(account.Rpg,_config.Adventures.Rpg,"abyss_blood_dynasty",4)?10m:0;
                        var gearLeech=Math.Clamp(combatStats.BossLifeleech,0,100);
                        if(bloodLeech+gearLeech>0)RpgSetCombatService.Heal(account.Rpg,_config.Adventures.Rpg.Health,(int)(Math.Min(RpgDepthsCombatService.MitigateBoss(hit.DirectDamage,_boss.Config.DamageReductionPercent,hit.Critical,account.Rpg,_config.Adventures.Rpg),_boss.Hp)*(bloodLeech+gearLeech)/100),clock);
                        lifeleechHealing=RpgSushiService.HealFromBossAttack(damage,_boss.Hp,account.Rpg,_config.Adventures.Rpg.Health,_boss.Id,clock,critical?combatStats.CriticalHealing:0);
                        if(damage<_boss.Hp)
                            receivedDamage=RpgBossDefenseService.Counter(RpgHealthService.CounterDamage(_boss.Config.HitPoints,_config.Adventures.Rpg.Health,_random),account.Rpg,_config.Adventures.Rpg,_random,clock);
                        health.CurrentHp=Math.Max(0,health.CurrentHp!.Value-receivedDamage);
                        RpgDepthsCombatService.AfterCounter(account.Rpg,_config.Adventures.Rpg,_boss.Id,clock);
                        playerDied=health.Defeated=health.CurrentHp==0;
                        healthText=$" Gegenangriff: -{receivedDamage} HP. ❤️ {health.CurrentHp}/{RpgHealthService.Maximum(account.Rpg,_config.Adventures.Rpg.Health)} HP.";
                        if(lifeleechHealing>0)healthText=$" Lifeleech: +{lifeleechHealing} HP."+healthText;
                        if(playerDied) healthText+=" 💀 Besiegt: keine Belohnungen für diesen Boss!";
                    }
                    damage=Math.Min(damage,_boss.Hp);
                    RpgPassiveService.ConsumeBuffs(account.Rpg, RpgActivity.Boss, _config.Adventures.Rpg, _random, clock);
                    RpgSushiService.Participate(account.Rpg,_boss.Id,_boss.End,clock);
                    account.Rpg.Cooldowns["!attack"] = clock.AddSeconds(RpgSetCombatService.AttackSeconds(account.Rpg,_config.Adventures.Rpg,RpgPerkCatalog.Cooldown(account.Rpg,_boss.Config.AttackCooldownSeconds,"attackCooldown"),clock));
                    return new(true);
                }, token);
            if(mutation.Duplicate)return new(false,user.UserId,0,false,_boss.Hp,false,0,"Angriff bereits verarbeitet.");
            if (!mutation.Success) { await Say($"@{user.UserName}, {mutation.Error}", token); return new(false, user.UserId, 0, false, _boss.Hp, false, 0, mutation.Error); }
        }
        _boss.Players[user.UserId] = user.UserName;
        _boss.Hp = Math.Max(0, _boss.Hp - damage);
        if (_boss.Hp > 0)
        {
            var template = _boss.Config.AttackMessage;
            var attackText = Format(template, ("user", user.UserName), ("damage", damage), ("hp", _boss.Hp), ("critical", critical ? "Kritischer Treffer!" : ""));
            if (critical && !string.IsNullOrWhiteSpace(attackText) && !template.Contains("{critical}", StringComparison.OrdinalIgnoreCase)) attackText += " Kritischer Treffer!";
            if (extraHits > 0 && !string.IsNullOrWhiteSpace(attackText)) attackText += $" {extraHits} Zusatztreffer enthalten.";
            if (_config.Adventures.Rpg.Enabled && !string.IsNullOrWhiteSpace(attackText)) attackText = "⚔️ " + attackText;
            attackText += healthText;
            await Say(attackText, token);
            return new(true, user.UserId, damage, critical, _boss.Hp, false, 0, "");
        }
        await FinishBossVictoryAsync(_boss,communityVictory,token,user.UserName,lifeleechHealing);
        return new(true,user.UserId,damage,critical,0,true,0,"");
    }
    private async Task FinishBossVictoryAsync(BossRound finished,bool communityVictory,CancellationToken token,string finisherName="",int lifeleechHealing=0)
    {
        var hpEnabled = _config.Adventures.Rpg.Enabled && _config.Adventures.Rpg.Health.Enabled;
        if(hpEnabled)
        {
            await RewardSurvivorsAsync(finished,true,token);
            SaveHealthBoss(null);
            _boss=null;
            _nextBoss=_now().AddSeconds(Math.Clamp(finished.Config.RoundCooldownSeconds,1,86400));
            await Say("🏆 Boss besiegt! Überlebende Teilnehmer erhalten Perlen und " + _config.Adventures.Rpg.Health.VictoryExperience + " EXP; besiegte Teilnehmer erhalten nichts."+
                (lifeleechHealing>0?$" @{finisherName}: Lifeleech +{lifeleechHealing} HP.":""),token);
            _points.PublishStory(new(finished.Config.IsMimic?GroqScene.Mimic:GroqScene.Boss,"boss-win:"+finished.Id,"Kampf gewonnen"));
            if(communityVictory) _points.HotTime.StartAfterBoss(finished.Id,_config.Adventures.Rpg.HotTime,true);
            foreach(var text in _points.HotTime.PollMessages(_config.Adventures.Rpg.HotTime)) await Say(text,token);
            DataChanged?.Invoke();
            return;
        }
        _boss = null;
        _nextBoss = _now().AddSeconds(Math.Clamp(finished.Config.RoundCooldownSeconds, 1, 86400));
        await CloseBossSushiAsync(finished,token);
        var reward = Math.Clamp(finished.Config.RewardPool, 0, 1000000000) / finished.Players.Count;
        reward=_points.RewardAmount(reward,RpgRewardSource.Boss);
        if(_config.Adventures.Rpg.Enabled)
        {
            foreach(var player in finished.Players)
            {
                long credited=0;
                var paid=await _points.ApplyRpgAsync(player.Key,player.Value,"bosscollect:"+finished.Id,a=>
                {
                    var before=a.Points;
                    RpgPerkCatalog.Victory(a.Rpg,_now());
                    var bonus=RpgCollectorService.Calculate(a.Rpg,_config.Adventures.Rpg.DiveLoot).BossPearls;
                    a.Points=Math.Max(a.Points,(long)Math.Min((decimal)Maximum,(decimal)a.Points+RpgCollectorService.Reward(reward,bonus+RpgPerkCatalog.Bonus(a.Rpg,"bossPearls"))));
                    credited=a.Points-before;
                    return new(true);
                },token);
                if(paid.Success)await AnnounceBossRewardAsync(player.Value,credited,token);
                if(!paid.Success)throw new InvalidOperationException(paid.Error);
            }
        }
        else
        {
            var credits=new Dictionary<string,long>();
            await _points.AddPointsToUsersAsync(finished.Players.Select(p => (p.Key, p.Value)), reward,
                _config.MinimumPoints, Maximum, token, "Bosskampf", _config.HistoryLimit,RpgRewardSource.AlreadyBoosted,credits);
            foreach(var player in finished.Players)await AnnounceBossRewardAsync(player.Value,credits[player.Key],token);
        }
        if (_config.Adventures.Rpg.Enabled)
        {
            var lootService = new RpgBossLootService(_points, _config.Adventures.Rpg, _random);
            foreach (var player in finished.Players)
            {
                var found = await lootService.AwardAsync(player.Key, player.Value, finished.Id, Math.Clamp(finished.Config.HitPoints, 10, 1000000), finished.Players.Count, _now(), token);
                if (found is not null) await Say($"{(found.Rarity == ItemRarity.Legendary ? "💎" : "⚔️")} @{player.Value} findet {found.DisplayName} [{found.Rarity}]! Details: !inv.", token);
            }
        }
        DataChanged?.Invoke();
        var winText = Format(finished.Config.WinMessage, ("count", finished.Players.Count), ("reward", reward), ("currency", Currency));
        if (_config.Adventures.Rpg.Enabled && !string.IsNullOrWhiteSpace(winText)) winText = "⚔️ " + winText;
        await Say(winText, token);
        _points.PublishStory(new(finished.Config.IsMimic?GroqScene.Mimic:GroqScene.Boss,"boss-win:"+finished.Id,"Kampf gewonnen"));
        if(_config.Adventures.Rpg.Enabled && communityVictory)
        {
            _points.HotTime.StartAfterBoss(finished.Id,_config.Adventures.Rpg.HotTime,true);
            foreach(var text in _points.HotTime.PollMessages(_config.Adventures.Rpg.HotTime)) await Say(text,token);
        }

    }
    private async Task CloseBossSushiAsync(BossRound boss,CancellationToken token)
    {
        if(!_config.Adventures.Rpg.Enabled)return;
        var participants=boss.Players.Select(p=>(Id:p.Key,Name:p.Value)).Concat((await _points.BossParticipantsAsync(boss.Id,token)).Select(p=>(p.Id,p.Name))).DistinctBy(p=>p.Id);
        foreach(var p in participants)
            await _points.ApplyRpgAsync(p.Id,p.Name,"sushiclose:"+boss.Id,a=>
            {RpgSushiService.Close(a.Rpg,boss.Id,_now()<boss.End?_now():boss.End,_now(),_config.Adventures.Rpg.Health);return new(true);},token);
    }
    private async Task RewardSurvivorsAsync(BossRound boss,bool victory,CancellationToken token)
    {
        await CloseBossSushiAsync(boss,token);
        var rpg=_config.Adventures.Rpg;
        var survivors=new List<(string Id,string Name,long Damage)>();
        foreach(var player in boss.Players)
        {
            var p=(await _points.GetProfileAsync(player.Key,token)).Entry.Rpg;
            // Evaluate before lazy regeneration: healing must not resurrect a participant.
            if(RpgHealthService.Eligible(p,boss.Id)) survivors.Add((player.Key,player.Value,p.Health.BossDamage));
        }
        var top=survivors.OrderByDescending(p=>p.Damage).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault().Id;
        var reward=survivors.Count==0 || !victory ? 0 : _points.RewardAmount(Math.Clamp(boss.Config.RewardPool,0,1000000000)/survivors.Count,RpgRewardSource.Boss);
        foreach(var player in survivors)
        {
            RpgItem? found=null,legendary=null;long credited=0;
            var result=await _points.ApplyRpgAsync(player.Id,player.Name,"bosssurvival:"+boss.Id,a=>
            {
                if(!RpgHealthService.Eligible(a.Rpg,boss.Id)) return new(false,"Kein Belohnungsanspruch.");
                var collectors=RpgCollectorService.Calculate(a.Rpg,rpg.DiveLoot);
                var before=a.Points;
                var personalReward=RpgCollectorService.Reward(reward,collectors.BossPearls+RpgPerkCatalog.Bonus(a.Rpg,"bossPearls"));
                a.Points=Math.Max(a.Points,(long)Math.Min((decimal)Maximum,(decimal)a.Points+personalReward));
                credited=a.Points-before;
                var experience=victory ? checked(rpg.Health.VictoryExperience+(player.Id==top?rpg.Health.TopExperience:0)) : rpg.Health.EscapeExperience;
                RpgHealthService.Experience(a.Rpg,rpg.Health,checked((int)RpgCollectorService.Reward(experience,collectors.BossExperience)),_now());
                if(victory && rpg.BossLootChance>0)
                {
                    found=new RpgLootService(_random).RollReward(rpg.DiveLoot,_now(),RpgActivity.Boss,rpg.BossLootChance,
                        ItemRarity.Epic,_points.HotTime.DropBonus(rpg.HotTime)).Item;
                    if(found is not null) a.Rpg.Inventory.Add(found);
                }
                if(victory){RpgDepthsLootService.Award(a.Rpg,rpg,boss.Config.IsMimic?"mimic":"boss",_random,_now(),_points.HotTime.DropBonus(rpg.HotTime),boss.MaximumHp>=rpg.Health.LegendaryThreshold);RpgMaterialLootService.Award(a.Rpg,rpg.Upgrades,boss.Config.IsMimic?"mimic":"boss",_random,_points.HotTime.DropBonus(rpg.HotTime));RpgPerkCatalog.Victory(a.Rpg,_now());legendary=RpgBossLootService.RollLegendary(rpg,_random,_now(),boss.Config.IsMimic);if(legendary is not null)a.Rpg.Inventory.Add(legendary);}
                return new(true);
            },token);
            if(result.Success && victory)await AnnounceBossRewardAsync(player.Name,credited,token);
            if(result.Success && legendary is not null)await Say($"LEGENDÄR! @{player.Name} findet {legendary.DisplayName} bei {boss.Config.DisplayName}! Details: !inv.",token);
            if(result.Success && found is not null) await Say($"⚔️ @{player.Name} findet {found.DisplayName} [{found.Rarity}]! Details: !inv.",token);
        }
    }
    private Task AnnounceBossRewardAsync(string name,long credited,CancellationToken ct) =>
        Say($"💰 @{name} erhält {RpgChatPresentation.Number(credited)} {Currency} Boss-Belohnung.",ct);
    public async Task TickAsync(CancellationToken token)
    {
        if (GameplayAllowed?.Invoke() == false) return;
        await _gate.WaitAsync(token);
        try { await TickCoreAsync(token); }
        finally { PublishStatus(); _gate.Release(); }
    }
    private async Task TickCoreAsync(CancellationToken token)
    {
        if(_config.Enabled&&_config.PointsEnabled)await _points.TickSetHealthAsync(_config.Adventures.Rpg,_now(),token);
        if(_recoverHealthBoss && _boss is not null)
        {
            var participants=await _points.BossParticipantsAsync(_boss.Id,token);
            long totalDamage=0;
            foreach(var p in participants){
                _boss.Players[p.Id]=p.Name;
                if(_boss.DynamicScaling)totalDamage+=(await _points.GetProfileAsync(p.Id,token)).Entry.Rpg.Health.BossDamage;
                else _boss.Hp=Math.Min(_boss.Hp,p.BossHp);
            }
            if(_boss.DynamicScaling)_boss.Hp=(int)Math.Max(0,_boss.MaximumHp-Math.Min(totalDamage,_boss.MaximumHp));
            _recoverHealthBoss=false;
        }
        if(_boss is not null && _boss.Hp==0 && _config.Adventures.Rpg.Enabled && _config.Adventures.Rpg.Health.Enabled)
        {
            await RewardSurvivorsAsync(_boss,true,token);
            SaveHealthBoss(null);_boss=null;
            _nextBoss=_now().AddSeconds(Math.Clamp(_config.Adventures.Boss.RoundCooldownSeconds,1,86400));
        }
        foreach(var text in _points.HotTime.PollMessages(_config.Adventures.Rpg.HotTime)) await Say(text,token);
        if (!_config.Enabled || !_config.PointsEnabled)
        { if(_boss is not null)await CloseBossSushiAsync(_boss,token); SaveHealthBoss(null); _race = null; _boss = null; return; }
        if (!_config.Adventures.Race.Enabled) _race = null;
        if (!_config.Adventures.Boss.Enabled) {if(_boss is not null)await CloseBossSushiAsync(_boss,token);SaveHealthBoss(null);_boss = null;}
        if (_boss is not null && _now() >= _boss.End)
        {
            if(_config.Adventures.Rpg.Enabled && _config.Adventures.Rpg.Health.Enabled)
                await RewardSurvivorsAsync(_boss,false,token);
            else await CloseBossSushiAsync(_boss,token);
            SaveHealthBoss(null);
            _nextBoss = _now().AddSeconds(Math.Clamp(_boss.Config.RoundCooldownSeconds, 1, 86400));
            _points.PublishStory(new(_boss.Config.IsMimic?GroqScene.Mimic:GroqScene.Boss,"boss-escape:"+_boss.Id,"Boss entkommen"));
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
        if (race.Players.Count < 2) { _lastRemoteRace=new(race.Id,race.Players.Keys.ToArray(),0,race.Stake,false,"Rennen abgesagt: weniger als zwei Teilnehmer; keine Abbuchung.");await Say(Format(race.Config.Adventures.Race.CancelMessage, ("reason", "weniger als 2 Teilnehmer")), token); return; }
        var players = race.Players.Select(p => (UserId:p.Key, DisplayName:p.Value)).ToArray();
        var winner = _random.NextInclusive(0, players.Length - 1);
        var result = await _points.ResolveRaceAsync(players, winner, race.Stake, race.Config, token);
        _lastRemoteRace=new(race.Id,players.Select(p=>p.UserId).ToArray(),winner,race.Stake,result.Success,result.Success?"Abrechnung des gemeinsamen Plugin-Rennens.":result.Error);
        if (result.Success) DataChanged?.Invoke();
        await Say(result.Success ? Format(race.Config.Adventures.Race.WinMessage, ("user", players[winner].DisplayName),
            ("pot", race.Stake * players.Length), ("currency", Currency), ("balance", result.Balance))
            : Format(race.Config.Adventures.Race.CancelMessage, ("reason", result.Error)), token);
    }
}



