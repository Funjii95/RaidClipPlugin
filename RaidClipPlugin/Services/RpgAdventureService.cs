using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgAdventureResult(bool Success, string PlayerId, long Stake, long Payout, long Balance,
    string Error, string Outcome, RpgItem? Item, int RemainingCooldown, IReadOnlyList<string> AppliedBuffs)
{
    public IReadOnlyList<RpgItem> AdditionalItems { get; init; } = Array.Empty<RpgItem>();
    public RpgEnemyResult? Enemy { get; init; }
    public IReadOnlyDictionary<string,int> MaterialRewards {get;init;}=new Dictionary<string,int>();
    public int ShellsAwarded { get; init; }
    public bool BossDiscovered { get; init; }
}

public sealed class RpgAdventureService(ViewerPointStore points, MinigameConfig config,
    IDuelRandom? random = null, Func<DateTimeOffset>? now = null)
{
    private readonly IDuelRandom _random = random ?? new CryptoDuelRandom();
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);

    public async Task<RpgAdventureResult> ExecuteAsync(string userId, string name, string requestId,
        RpgActivity activity, long stake, CancellationToken token)
    {
        var rpg = config.Adventures.Rpg;
        points.ConfigureHotTime(rpg);
        if (!config.Enabled || !config.PointsEnabled || !rpg.Enabled || activity == RpgActivity.Boss)
            return new(false, userId, stake, 0, 0, "RPG-Aktion ist deaktiviert.", "", null, 0, Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(requestId) || requestId.Length > 200)
            throw new ArgumentException("Twitch-ID und gültige Aktions-ID sind erforderlich.");
        rpg.Validate();
        var fishing = activity == RpgActivity.Fishing;
        SoloAdventureConfig game = fishing ? config.Adventures.Fishing : config.Adventures.Treasure;
        if (!game.Enabled || stake < 0 || stake > Math.Min(game.MaximumBet, long.MaxValue / 500) || (stake > 0 && stake < game.MinimumBet))
            return new(false, userId, stake, 0, 0, "Ungültiger Einsatz.", "", null, 0, Array.Empty<string>());
        var command = fishing ? "!fish" : "!dive";
        var outcomeRoll=_random.NextInclusive(1,100);
        var (multiplier, outcome, tier) = AdventureGameService.SoloOutcome(command, outcomeRoll, game);
        var payout = checked((long)decimal.Floor(stake * multiplier));
        // Keep the existing fish payout and collection exactly as before; RPG fish are additive.
        var fishName = fishing && tier > 0 ? tier == 1 ? "Rotfeder" : tier == 2 ? "Hecht" :
            _random.NextInclusive(1, 3) switch { 1 => "Goldener Koi", 2 => "Mondaal", _ => "Kraken" } : null;
        var grams = fishName is null ? 0 : _random.NextInclusive(tier == 3 ? 5000 : 100, tier == 3 ? 20000 : 4000);
        RpgItem? loot = null;
        RpgEnemyResult? enemy = null;
        RpgFishDefinition? caughtFish=null;
        var additional = new List<RpgItem>();
        IReadOnlyDictionary<string,int> materials=new Dictionary<string,int>();
        var remaining = 0;
        var bossDiscovered=false;
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
                if(rpg.Health.Enabled)
                {
                    RpgHealthService.Refresh(account.Rpg,rpg.Health,clock);
                    if(!fishing && account.Rpg.Health.CurrentHp<=0)
                        return new(false,"Du hast 0 HP. Erst heilen, dann tauchen: !hp. Kein Einsatz abgebucht.");
                }
                applied = account.Rpg.Buffs.Where(b => b.Activity == activity && b.RemainingUses > 0).Select(b => b.RecipeId).ToArray();
                if (!fishing)
                {
                    var sources = CharacterStatService.DiveLuckSources(account.Rpg, rpg);
                    var collectors=RpgCollectorService.Calculate(account.Rpg,rpg.DiveLoot);
                    loot = new RpgLootService(_random).Roll(stake, sources.Perks, rpg.DiveLoot, clock, sources.Sushi, sources.Equipment,points.HotTime.DropBonus(rpg.HotTime),collectors,stats.RareEquipmentLuck,RpgPerkCatalog.Combined(account.Rpg,"loot")).Item;
                    void StoreLoot(RpgItem found)
                    {
                        if (found.Kind == RpgItemKind.Consumable)
                        {
                            if (!rpg.Recipes.Any(r => r.Id == found.ConsumableRecipeId)) throw new InvalidOperationException("Verbrauchsgegenstand ohne gültiges Rezept.");
                            account.Rpg.Sushi[found.ConsumableRecipeId] = checked(account.Rpg.Sushi.GetValueOrDefault(found.ConsumableRecipeId) + 1);
                        }
                        else account.Rpg.Inventory.Add(found);
                    }
                    if (loot is not null) StoreLoot(loot);
                    if (RpgPassiveService.TryProc(account.Rpg, rpg, activity, RpgEffectTrigger.ExtraLoot, _random, clock, stake) > 0)
                    {
                        var extra = new RpgLootService(_random).Roll(stake, sources.Perks, rpg.DiveLoot, clock, sources.Sushi, sources.Equipment,points.HotTime.DropBonus(rpg.HotTime),collectors,stats.RareEquipmentLuck,RpgPerkCatalog.Combined(account.Rpg,"loot")).Item;
                        if (extra is not null) { StoreLoot(extra); additional.Add(extra); }
                    }
                    if(RpgDepthsCombatService.Has(account.Rpg,rpg,"true_eternal_reef",6))
                    foreach(var chance in new[]{20,10})if(_random.NextInclusive(0,9999)<chance*100){var reefLoot=new RpgLootService(_random).Roll(stake,sources.Perks,rpg.DiveLoot,clock,sources.Sushi,sources.Equipment,points.HotTime.DropBonus(rpg.HotTime),collectors,stats.RareEquipmentLuck,RpgPerkCatalog.Combined(account.Rpg,"loot")).Item;if(reefLoot is not null){StoreLoot(reefLoot);additional.Add(reefLoot);}}
                    enemy=new RpgEnemyService(_random).Encounter(account.Rpg,rpg,stake,clock,points.HotTime.DropBonus(rpg.HotTime));
                    if(enemy?.Loot is not null) { StoreLoot(enemy.Loot); additional.Add(enemy.Loot); }
                    if(enemy?.ExtraLoot is not null) { StoreLoot(enemy.ExtraLoot); additional.Add(enemy.ExtraLoot); }
                }
                else if (fishName is not null)
                {
                    var species=caughtFish??throw new InvalidOperationException("Fangdaten fehlen.");
                    var id = species.Id;
                    if (!account.Rpg.Fish.TryGetValue(id, out var fish)) account.Rpg.Fish[id] = fish = new() { FishId = id, FirstCaughtAt = clock };
                    var quantity=1;
                    if(RpgPerkCatalog.Proc(account.Rpg,"doubleFish",_random))quantity++;
                    if(RpgDepthsCombatService.Has(account.Rpg,rpg,"true_eternal_reef",4)&&_random.NextInclusive(0,9999)<1500)quantity++;
                    if(rpg.Recipes.Any(r=>RpgSushiService.Ingredients(r).ContainsKey(id)) && RpgPerkCatalog.Proc(account.Rpg,"ingredient",_random))quantity++;
                    fish.Quantity = checked(fish.Quantity + quantity); fish.LastCaughtAt = clock;
                    loot = new() { ItemId = id, DisplayName = species.Name, Rarity = species.Rarity, IconId = species.IconId, FoundAt = clock };
                    var equipment = new RpgLootService(_random).RollReward(rpg.DiveLoot, clock, RpgActivity.Fishing, rpg.FishingEquipmentChance, ItemRarity.Epic,points.HotTime.DropBonus(rpg.HotTime)).Item;
                    if (equipment is not null) { account.Rpg.Inventory.Add(equipment); additional.Add(equipment); }
                }
                if(fishing && tier==0){
                    account.Rpg.Materials ??= new();account.Rpg.Materials.Validate();
                    account.Rpg.Materials.Shells=checked(account.Rpg.Materials.Shells+1);
                    outcome="Eine Muschel gefunden!";
                }
                var hotTime=points.HotTime.DropBonus(rpg.HotTime);
                var materialFound=new Dictionary<string,int>(RpgMaterialLootService.Award(account.Rpg,rpg.Upgrades,fishing?"fish":"dive",_random,hotTime));
                if(!fishing&&tier>0&&account.Rpg.Health.CurrentHp is not <=0)
                    foreach(var reward in RpgMaterialLootService.Award(account.Rpg,rpg.Upgrades,tier==1?"chest":tier==2?"rareChest":"legendaryChest",_random,hotTime))materialFound[reward.Key]=checked(materialFound.GetValueOrDefault(reward.Key)+reward.Value);
                materials=materialFound;
                if(account.Rpg.Health.CurrentHp is not <=0)RpgDepthsLootService.Award(account.Rpg,rpg,fishing?"fish":"dive",_random,clock,hotTime);
                RpgPassiveService.ConsumeBuffs(account.Rpg, activity, rpg, _random, clock, stake);
                account.Rpg.Cooldowns[command] = clock.AddSeconds(RpgPerkCatalog.Cooldown(account.Rpg,game.CooldownSeconds,fishing?"fishCooldown":"diveCooldown")/(1+(double)Math.Clamp(stats.MovementSpeed,-90,200)/100));
                if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 200) throw new ArgumentException("Ungültige Aktions-ID.");
                bossDiscovered=!fishing && rpg.Health.Enabled && config.Adventures.Boss.Enabled && RpgPerkCatalog.DiscoverMimic(account.Rpg,rpg,_random);
                account.Rpg.CompletedActions.Add(requestId);
                return new(true);
            }, (account, basePayout) =>
            {
                var stats = CharacterStatService.Calculate(account.Rpg, rpg, activity);
                if(fishing && tier==0 && outcomeRoll>Math.Max(0,game.LossChance-RpgPerkCatalog.Combined(account.Rpg,"catch")))
                {
                    tier=1;fishName="Rotfeder";outcome="Ein kleiner Fisch dank Ruhige Hand!";
                    basePayout=checked((long)decimal.Floor(stake*game.SmallMultiplier));
                }
                if(fishing && fishName is not null)
                {
                    caughtFish=RpgFishingService.Select(rpg,stats,stake,_random,account.Rpg);
                    if(caughtFish.PearlReward is {} reward)basePayout=checked(stake+reward);
                    else if(stake==0)basePayout=rpg.FreeFishingReward;
                }
                payout = RpgPassiveService.RewardPayout(basePayout, stake, stats.PearlBonus);
                payout=RpgCollectorService.Reward(Math.Max(0,payout-stake),RpgPerkCatalog.Combined(account.Rpg,fishing?"fishPearls":"divePearls"))+Math.Min(payout,stake);
                var net=Math.Max(0,payout-stake);
                var collectors=RpgCollectorService.Calculate(account.Rpg,rpg.DiveLoot);
                var collectorPercent=fishing && fishName is not null ? collectors.FishingPearls : !fishing?collectors.DivePearls:0;
                payout=checked(payout-net+RpgCollectorService.Reward(net,collectorPercent));
                net=Math.Max(0,payout-stake);
                payout=checked(payout-net+points.RewardAmount(net,fishing?RpgRewardSource.Fishing:RpgRewardSource.DiveNetWin));
                return payout;
            },RpgRewardSource.AlreadyBoosted,rpgFishName:()=>caughtFish?.PearlReward.HasValue==true?caughtFish.Name:fishName);
        if(result.Success)points.PublishStory(new(fishing?GroqScene.Fishing:GroqScene.Dive,
            "adventure:"+userId+":"+requestId,
            fishing?(caughtFish is null?"Kein Fisch gefangen":$"Fisch gefangen, Seltenheit {caughtFish.Rarity}"):
                enemy is not null?"Unterwassergegner getroffen":loot is not null?$"Gegenstand gefunden, Seltenheit {loot.Rarity}":tier==0?"Tauchgang ohne Schatz":"Schatzfund",
            name));
        return new(result.Success, userId, stake, payout, result.Balance, result.Error, outcome,
            result.Success ? loot : null, remaining, result.Success ? applied : Array.Empty<string>())
            { MaterialRewards=result.Success?materials:new Dictionary<string,int>(), BossDiscovered=result.Success && bossDiscovered, AdditionalItems = result.Success ? additional : Array.Empty<RpgItem>(),Enemy=result.Success?enemy:null,ShellsAwarded=result.Success && fishing && tier==0?1:0 };
    }
}
