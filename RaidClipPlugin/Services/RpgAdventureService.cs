using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgAdventureResult(bool Success, string PlayerId, long Stake, long Payout, long Balance,
    string Error, string Outcome, RpgItem? Item, int RemainingCooldown, IReadOnlyList<string> AppliedBuffs);

public sealed class RpgAdventureService(ViewerPointStore points, MinigameConfig config,
    IDuelRandom? random = null, Func<DateTimeOffset>? now = null)
{
    private readonly IDuelRandom _random = random ?? new CryptoDuelRandom();
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);

    public async Task<RpgAdventureResult> ExecuteAsync(string userId, string name, string requestId,
        RpgActivity activity, long stake, CancellationToken token)
    {
        var rpg = config.Adventures.Rpg;
        if (!config.Enabled || !config.PointsEnabled || !rpg.Enabled || activity == RpgActivity.Boss)
            return new(false, userId, stake, 0, 0, "RPG-Aktion ist deaktiviert.", "", null, 0, Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(requestId) || requestId.Length > 200)
            throw new ArgumentException("Twitch-ID und gültige Aktions-ID sind erforderlich.");
        rpg.Validate();
        var fishing = activity == RpgActivity.Fishing;
        SoloAdventureConfig game = fishing ? config.Adventures.Fishing : config.Adventures.Treasure;
        if (!game.Enabled || stake < 0 || stake > Math.Min(game.MaximumBet, long.MaxValue / 500) || (stake > 0 && stake < game.MinimumBet) || (fishing && stake == 0))
            return new(false, userId, stake, 0, 0, "Ungültiger Einsatz.", "", null, 0, Array.Empty<string>());
        var command = fishing ? "!fish" : "!dive";
        var (multiplier, outcome, tier) = AdventureGameService.SoloOutcome(command, _random.NextInclusive(1, 100), game);
        var payout = checked((long)decimal.Floor(stake * multiplier));
        // Keep the existing fish payout and collection exactly as before; RPG fish are additive.
        var fishName = fishing && tier > 0 ? tier == 1 ? "Rotfeder" : tier == 2 ? "Hecht" :
            _random.NextInclusive(1, 3) switch { 1 => "Goldener Koi", 2 => "Mondaal", _ => "Kraken" } : null;
        var grams = fishName is null ? 0 : _random.NextInclusive(tier == 3 ? 5000 : 100, tier == 3 ? 20000 : 4000);
        RpgItem? loot = null;
        var remaining = 0;
        var applied = Array.Empty<string>();
        var clock = _now();
        var result = await points.ApplyCasinoAsync(userId, name, fishing ? "Fischen" : "Schatzsuche", stake, payout,
            config.MinimumPoints, config.MaximumAccountEnabled ? config.MaximumAccountPoints : long.MaxValue,
            config.DailyGambleLimitEnabled ? config.DailyGambleLimit : 0,
            config.DailyLossLimitEnabled ? config.DailyLossLimit : 0,
            config.DailyWinLimitEnabled ? config.DailyWinLimit : 0,
            0, false, 0, config.HistoryLimit, token, fishName, grams, account =>
            {
                account.Rpg ??= new();
                if (account.Rpg.SchemaVersion != 1) throw new InvalidOperationException("Unbekannte RPG-Datenversion.");
                if (account.Rpg.CompletedActions.Contains(requestId)) return new(false, "Aktion wurde bereits verarbeitet.");
                if (account.Rpg.Cooldowns.TryGetValue(command, out var until) && clock < until)
                { remaining = (int)Math.Ceiling((until - clock).TotalSeconds); return new(false, $"Cooldown: noch {remaining} Sekunden."); }
                var stats = CharacterStatService.Calculate(account.Rpg, rpg, activity);
                applied = account.Rpg.Buffs.Where(b => b.Activity == activity && b.RemainingUses > 0).Select(b => b.RecipeId).ToArray();
                if (!fishing)
                {
                    var sources = CharacterStatService.DiveLuckSources(account.Rpg, rpg);
                    loot = new RpgLootService(_random).Roll(stake, sources.Perks, rpg.DiveLoot, clock, sources.Sushi, sources.Equipment).Item;
                    if (loot is not null)
                    {
                        if (loot.Kind == RpgItemKind.Consumable)
                        {
                            if (!rpg.Recipes.Any(r => r.Id == loot.ConsumableRecipeId)) throw new InvalidOperationException("Verbrauchsgegenstand ohne gültiges Rezept.");
                            account.Rpg.Sushi[loot.ConsumableRecipeId] = checked(account.Rpg.Sushi.GetValueOrDefault(loot.ConsumableRecipeId) + 1);
                        }
                        else account.Rpg.Inventory.Add(loot);
                    }
                }
                else if (fishName is not null)
                {
                    var species = rpg.FishSpecies.Where(f => f.Weight > 0 && f.Weight <= 1000000).ToArray();
                    if (species.Length == 0) throw new InvalidOperationException("Keine gültigen Fischarten konfiguriert.");
                    var weights = species.Select(f => checked((int)Math.Min(1000000, f.Weight *
                        (f.Rarity >= ItemRarity.Rare ? 1 + Math.Min(100, stats.Luck + stats.FishingLuck) / 100 : 1)))).ToArray();
                    var fishRoll = _random.NextInclusive(1, weights.Sum());
                    var index = 0;
                    while (index < species.Length - 1 && (fishRoll -= weights[index]) > 0) index++;
                    var id = species[index].Id;
                    if (!account.Rpg.Fish.TryGetValue(id, out var fish)) account.Rpg.Fish[id] = fish = new() { FishId = id, FirstCaughtAt = clock };
                    fish.Quantity = checked(fish.Quantity + 1); fish.LastCaughtAt = clock;
                    loot = new() { ItemId = id, DisplayName = species[index].Name, Rarity = species[index].Rarity, IconId = species[index].IconId, FoundAt = clock };
                }
                RpgProgressionService.ConsumeBuffs(account.Rpg, activity);
                account.Rpg.Cooldowns[command] = clock.AddSeconds(Math.Clamp(game.CooldownSeconds, 1, 86400));
                if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 200) throw new ArgumentException("Ungültige Aktions-ID.");
                account.Rpg.CompletedActions.Add(requestId);
                return new(true);
            }, (account, basePayout) =>
            {
                var stats = CharacterStatService.Calculate(account.Rpg, rpg, activity);
                payout = checked((long)decimal.Floor(basePayout * (1 + stats.PearlBonus / 100)));
                return payout;
            });
        return new(result.Success, userId, stake, payout, result.Balance, result.Error, outcome,
            result.Success ? loot : null, remaining, result.Success ? applied : Array.Empty<string>());
    }
}
