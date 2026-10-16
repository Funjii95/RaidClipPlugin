using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

const int seed = 20261016;
const int samples = 100000;
var rpg = new RpgConfig();
var dive = new DiveAdventureConfig();
var fishing = new SoloAdventureConfig();
var scenarios = new[] { "Base", "MaxPerks", "Equipment", "Sushi", "Combined" };
var rows = new List<object>();
var index = 0;
foreach (var stake in new long[] { 0, 250, 1000, 5000, 10000 })
foreach (var scenario in scenarios)
{
    var profile = new RpgProfile();
    if (scenario is "MaxPerks" or "Combined")
        foreach (var perk in rpg.Perks) profile.Perks[perk.Id] = perk.MaximumLevel;
    if (scenario is "Equipment" or "Combined")
        foreach (var id in new[] { "deep-ring", "amulet", "boots" })
        {
            var item = rpg.DiveLoot.Items.Single(i => i.ItemId == id);
            profile.Inventory.Add(item);
            profile.Equipment[item.Slot!.Value] = item.InstanceId;
        }
    var buffRecipe = rpg.Recipes.Single(r => r.Id == "dive");
    var sushi = scenario is "Sushi" or "Combined";
    var random = new Seeded(seed + index++);
    var loot = new RpgLootService(random);
    var counts = Enum.GetValues<ItemRarity>().ToDictionary(r => r.ToString(), _ => 0);
    long totalPayout = 0, totalCrafting = 0;
    var wins = 0; var losses = 0; var buffsConsumed = 0;
    LootChanceBreakdown? chance = null;
    var fixedTime = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    for (var n = 0; n < samples; n++)
    {
        if (sushi && profile.Buffs.Count == 0)
        {
            profile.Buffs.Add(new() { RecipeId = buffRecipe.Id, Activity = buffRecipe.Activity, RemainingUses = 3, Attributes = buffRecipe.Attributes });
            totalCrafting += buffRecipe.PearlCost;
        }
        var (factor, _, _) = AdventureGameService.SoloOutcome("!dive", random.NextInclusive(1, 100), dive);
        var stats = CharacterStatService.Calculate(profile, rpg, RpgActivity.Dive);
        var payout = (long)decimal.Floor(decimal.Floor(stake * factor) * (1 + stats.PearlBonus / 100));
        totalPayout += payout;
        if (payout > stake) wins++; else if (payout < stake) losses++;
        var sources = CharacterStatService.DiveLuckSources(profile, rpg);
        var result = loot.Roll(stake, sources.Perks, rpg.DiveLoot, fixedTime, sources.Sushi, sources.Equipment);
        chance = result.Chances;
        if (result.Item is not null) counts[result.Item.Rarity.ToString()]++;
        if (sushi) buffsConsumed++;
        RpgProgressionService.ConsumeBuffs(profile, RpgActivity.Dive);
    }
    var eligible = rpg.DiveLoot.RarityWeights.Where(p => p.Key <= chance!.MaximumRarity && rpg.DiveLoot.Items.Any(i => i.Rarity == p.Key)).ToArray();
    var dropProbability = decimal.Floor(chance!.Total * 100) / 10000;
    var legendaryProbability = eligible.Any(p => p.Key == ItemRarity.Legendary) ? dropProbability * rpg.DiveLoot.RarityWeights[ItemRarity.Legendary] / eligible.Sum(p => p.Value) : 0;
    var baseExpectedPayout = Enumerable.Range(1, 100).Average(roll => (double)decimal.Floor(stake * AdventureGameService.SoloOutcome("!dive", roll, dive).Multiplier));
    var finalStats = CharacterStatService.Calculate(profile, rpg, RpgActivity.Dive);
    var expectedPayout = Enumerable.Range(1, 100).Average(roll => (double)decimal.Floor(decimal.Floor(stake * AdventureGameService.SoloOutcome("!dive", roll, dive).Multiplier) * (1 + finalStats.PearlBonus / 100)));
    rows.Add(new { Stake = stake, Scenario = scenario, Samples = samples, Chance = chance, Counts = counts,
        TheoreticalDropProbability = dropProbability, TheoreticalLegendaryProbability = legendaryProbability,
        TheoreticalBasePayout = baseExpectedPayout, TheoreticalPayout = expectedPayout, TheoreticalNet = expectedPayout - stake,
        SimulatedPayout = (decimal)totalPayout / samples, SimulatedNet = (decimal)totalPayout / samples - stake,
        CraftingCosts = totalCrafting, BuffsConsumed = buffsConsumed, SimulatedNetAfterSushi = (decimal)(totalPayout - totalCrafting) / samples - stake, Wins = wins, Losses = losses });
    Console.Error.WriteLine($"{stake}/{scenario} completed");
}

// Separate population models: zero starting balance, verified stated attendance rates.
// Each weekly simulation uses actual solo outcomes and cash-constrained 10k dives;
// no fractional dive, no debt, no automatic perk purchases. Progression times below
// are saving benchmarks (alternative goals), not simultaneous purchases.
var profiles = new List<object>();
foreach (var (name, hours, hourly, bosses, chatCount) in new[] {
    ("A", 2, 1000, 0, 0), ("B", 8, 2200, 2, 20), ("C", 20, 2600, 4, 60) })
{
    const int weeks = 100000;
    const int participants = 10;
    const decimal bossSuccess = 0.8m;
    var bossConfig = new BossAdventureConfig();
    var baseIncome = hours * hourly;
    var meanBossIncome = hours * bosses * bossSuccess * (bossConfig.RewardPool / participants);
    long balance = 0, aggregateDives = 0, aggregateLegendary = 0, aggregateFishNet = 0;
    decimal diveBudget = 0;
    var rng = new Seeded(seed + 100 + hours);
    var loot = new RpgLootService(rng);
    for (var week = 0; week < weeks; week++)
    {
        balance += baseIncome;
        diveBudget += baseIncome * 0.25m;
        for (var boss = 0; boss < hours * bosses; boss++)
            if (rng.NextInclusive(1, 100) <= 80) balance += bossConfig.RewardPool / participants;
        // One 100-pearl fish attempt per stream hour, subject to available balance.
        for (var fish = 0; fish < hours; fish++)
        {
            if (balance < 100) break;
            var payout = (long)(100 * AdventureGameService.SoloOutcome("!fish", rng.NextInclusive(1, 100), fishing).Multiplier);
            balance += payout - 100; aggregateFishNet += payout - 100;
        }
        // One possible 10k dive per 2 watched hours; budget permitting, base stats.
        for (var attempt = 0; attempt < hours / 2; attempt++)
        {
            if (balance < 10000 || diveBudget < 10000) break;
            diveBudget -= 10000;
            balance += (long)(10000 * AdventureGameService.SoloOutcome("!dive", rng.NextInclusive(1, 100), dive).Multiplier) - 10000;
            aggregateDives++;
            if (loot.Roll(10000, 0, rpg.DiveLoot, DateTimeOffset.UnixEpoch).Item?.Rarity == ItemRarity.Legendary) aggregateLegendary++;
        }
    }
    var meanIncome = baseIncome + meanBossIncome;
    profiles.Add(new { Profile = name, HoursPerWeek = hours, AttendanceAndChatIncome = baseIncome,
        ChatMessagesPerHour = chatCount, BossAttemptsPerHour = bosses, BossParticipants = participants, BossSuccessProbability = bossSuccess,
        TheoreticalBossIncome = meanBossIncome, TheoreticalTotalBeforeGames = meanIncome,
        SavingHoursFirstPerk = 500m / (meanIncome / hours), SavingHoursMaxPerk = 15500m / (meanIncome / hours),
        SavingWeeksMaxPerk = 15500m / meanIncome, SavingWeeksAllSeven = 108500m / meanIncome,
        SavingHoursFirst10k = 10000m / (meanIncome / hours),
        SimulatedWeeks = weeks, SimulatedNetIncomePerWeek = (decimal)balance / weeks,
        SimulatedFishingNetPerWeek = (decimal)aggregateFishNet / weeks,
        Simulated10kDivesPerWeek = (decimal)aggregateDives / weeks, SimulatedLegendariesPerWeek = (decimal)aggregateLegendary / weeks });
}
var output = JsonSerializer.Serialize(new { Seed = seed, SamplesPerCell = samples, TotalDives = samples * rows.Count,
    Assumptions = "No external rewards assumed (follow/sub/raid/channel rewards are event-dependent). Sushi stress scenarios assume unlimited matching fish and debit recipe cost every 3 valid dives. Production RNG is crypto; fixed .NET Random is injected here. Population profiles start at zero, carry balance forward, reserve 25% of attendance/chat income as a gross 10k-dive budget and make at most one dive per 2 watched hours. No automatic perk purchases; saving times are alternative goals. All arithmetic uses the existing solo, loot, stat and buff services.",
    Dives = rows, Profiles = profiles }, new JsonSerializerOptions { WriteIndented = true });
if (args.Length != 1) throw new ArgumentException("Provide output JSON path inside the workspace.");
File.WriteAllText(args[0], output);

sealed class Seeded(int seed) : IDuelRandom
{
    private readonly Random _random = new(seed);
    public int NextInclusive(int min, int max) => _random.Next(min, checked(max + 1));
}
