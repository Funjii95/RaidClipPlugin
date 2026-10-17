using System.ComponentModel;
using System.Security.Principal;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum RpgDebugLootMode { Normal, Garantiert, KeinLoot }
public enum RpgDebugRollMode { Zufall, Minimum, Durchschnitt, Maximum, Benutzerdefiniert }
public sealed class RpgDebugOptions
{
    [DisplayName("Zufalls-Seed")] public int Seed {get;set;}=42;
    [DisplayName("Basis-Lootchance (%)")] public decimal BaseChance {get;set;}=25;
    [DisplayName("Maximale Lootchance (%)")] public decimal MaximumChance {get;set;}=60;
    [DisplayName("Common-Gewicht")] public int Common {get;set;}=6000;
    [DisplayName("Uncommon-Gewicht")] public int Uncommon {get;set;}=2900;
    [DisplayName("Rare-Gewicht")] public int Rare {get;set;}=850;
    [DisplayName("Epic-Gewicht")] public int Epic {get;set;}=200;
    [DisplayName("Legendary-Gewicht")] public int Legendary {get;set;}=50;
    [DisplayName("Nur Seltenheit (leer = alle)")] public ItemRarity? OnlyRarity {get;set;}
    [DisplayName("Dropmodus")] public RpgDebugLootMode LootMode {get;set;}
    [DisplayName("Luck-Bonus")] public decimal Luck {get;set;}
    [DisplayName("Statischer Eventbonus (%)")] public decimal EventBonus {get;set;}
    [DisplayName("Zusätzliche Testwürfe")] public int ExtraRolls {get;set;}
    [DisplayName("Legendary-Mindesteinsatz")] public long LegendaryMinimumStake {get;set;}=10000;
    [DisplayName("Test-Einsatz")] public long Stake {get;set;}=10000;
    [DisplayName("Boss-Lebenspunkte")] public int BossHp {get;set;}=200;
    [DisplayName("Kritischen Treffer erzwingen")] public bool ForceCritical {get;set;}
    [DisplayName("Doppelangriff erzwingen")] public bool ForceDouble {get;set;}
    [DisplayName("Dreifachangriff erzwingen")] public bool ForceTriple {get;set;}
    public void Validate()
    {
        if(BaseChance is <0 or >100||MaximumChance is <0 or >100||Luck is <0 or >1000||EventBonus is <0 or >100||ExtraRolls is <0 or >2||LegendaryMinimumStake<1||Stake is <0 or >1000000000||BossHp is <10 or >1000000||
            new[]{Common,Uncommon,Rare,Epic,Legendary}.Any(v=>v is <0 or >1000000)||OnlyRarity.HasValue&&!Enum.IsDefined(OnlyRarity.Value)||ForceDouble&&ForceTriple)
            throw new InvalidOperationException("Ungültige Testwerte: Chancen 0–100, maximal 2 Zusatzwürfe; Double/Triple nicht gleichzeitig erzwingen.");
    }
}
public sealed record RpgDebugPreferences(MinigameConfig Config,RpgDebugOptions Options);
public sealed record RpgDebugSimulation(int Runs,int Drops,IReadOnlyDictionary<ItemRarity,int> Rarities,int Crits,int Doubles,int Triples,decimal AverageDamage,decimal AveragePearlChange,int Seed,int LootRolls,int ExtraLootProcs)
{
    public IReadOnlyDictionary<ItemRarity,decimal> RarityPercentages=>Rarities.ToDictionary(p=>p.Key,p=>LootRolls>0?100m*p.Value/LootRolls:0);
    public int LegendaryCount=>Rarities.GetValueOrDefault(ItemRarity.Legendary);
    public decimal CritPercent=>100m*Crits/Runs;
    public decimal DoublePercent=>100m*Doubles/Runs;
    public decimal TriplePercent=>100m*Triples/Runs;
}

public sealed class RpgDebugSession
{
    public const string PlayerId="isolated-rpg-debug-player";
    private readonly Func<bool> _authorize;
    private readonly string _directory;
    private readonly SemaphoreSlim _operations=new(1,1);
    private ViewerPointStore? _points;
    public bool Active {get;private set;}
    public MinigameConfig Config {get;private set;}=new();
    public RpgDebugOptions Options {get;private set;}=new();
    public string DirectoryPath=>_directory;
    public int RemainingBossHp {get;private set;}=200;
    private string PreferencesPath=>Path.Combine(_directory,"debug-settings.json");
    public static bool IsLocalAdministrator()
    {
        using var identity=WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)||identity.Groups?.Any(g=>g is SecurityIdentifier sid&&sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))==true;
    }
    public RpgDebugSession(string? root=null):this(root,IsLocalAdministrator) { }
    internal RpgDebugSession(string? root,Func<bool> authorization)
    { _directory=Path.Combine(root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RaidClipPlugin"),"rpg-debug");_authorize=authorization; }
    private void Ensure()
    { if(!Active||!_authorize())throw new UnauthorizedAccessException("TESTMODUS ist deaktiviert oder das lokale Konto ist kein Administrator."); }
    private static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static void Validate(RpgDebugPreferences value)
    { value.Options.Validate();value.Config.Adventures.Rpg.Validate();value.Config.Adventures.Fishing.Validate("Test-Angeln");value.Config.Adventures.Treasure.Validate("Test-Dive"); }
    public void Activate(MinigameConfig source)
    {
        if(!_authorize())throw new UnauthorizedAccessException("Nur autorisierte lokale Administratoren dürfen den Testmodus aktivieren.");
        Directory.CreateDirectory(_directory);
        var saved=File.Exists(PreferencesPath)?RpgAtomicFile.Load<RpgDebugPreferences>(PreferencesPath,Validate):new(Clone(source),new());
        Config=saved.Config;Options=saved.Options;Config.Enabled=true;Config.PointsEnabled=true;Config.Adventures.Rpg.Enabled=true;
        _points=new ViewerPointStore(Path.Combine(_directory,"players"));_points.ConfigureHotTime(Config.Adventures.Rpg);Active=true;RemainingBossHp=Options.BossHp;Log("Aktiviert",new {Storage="isolierter Testdatenspeicher"});
    }
    public void Deactivate() { Active=false;_points=null; }
    public void SaveSettings()
    { Ensure();Validate(new(Config,Options));RpgAtomicFile.Save(PreferencesPath,new RpgDebugPreferences(Config,Options),Validate);_points!.ConfigureHotTime(Config.Adventures.Rpg); }
    public void RestoreSettings()
    { if(!_authorize())throw new UnauthorizedAccessException();RpgAtomicFile.Restore<RpgDebugPreferences>(PreferencesPath,Validate);var saved=RpgAtomicFile.Load<RpgDebugPreferences>(PreferencesPath,Validate);Config=saved.Config;Options=saved.Options;_points?.ConfigureHotTime(Config.Adventures.Rpg); }
    public async Task<ViewerPointEntry> ProfileAsync(CancellationToken token=default)
    { Ensure();return (await _points!.GetProfileAsync(PlayerId,token)).Entry; }
    private void Log(string action,object? result=null)
    {
        try { File.AppendAllText(Path.Combine(_directory,"debug-actions.jsonl"),JsonSerializer.Serialize(new {Time=DateTimeOffset.UtcNow,Action=action,Player="TEST",Options,Configuration=Config.Adventures.Rpg,Result=result})+Environment.NewLine); }
        catch(Exception ex){Console.WriteLine("Debug-Protokoll konnte nicht geschrieben werden: "+ex.GetType().Name);}
    }
    private async Task<T> Execute<T>(string action,Func<Task<T>> execute)
    {
        await _operations.WaitAsync();
        try
        {
            Ensure();
            try {SaveSettings();var result=await execute();var profile=await ProfileAsync();Log(action,new {Result=result,Stats=CharacterStatService.Calculate(profile.Rpg,Config.Adventures.Rpg,RpgActivity.Boss),profile.Rpg.Cooldowns,profile.Rpg.Buffs});return result;}
            catch(Exception ex){Log(action,new {Error=ex.GetType().Name,ex.Message});throw;}
        }
        finally {_operations.Release();}
    }
    private sealed class DebugRandom(int seed,RpgDebugRollMode mode=RpgDebugRollMode.Zufall):IDuelRandom
    {
        private readonly Random _rng=new(seed);
        public readonly List<int> Trace=new();
        public int Calls {get;private set;}
        public int NextInclusive(int min,int max)
        {
            var value=mode switch {RpgDebugRollMode.Minimum=>min,RpgDebugRollMode.Maximum=>max,RpgDebugRollMode.Durchschnitt=>min+(max-min)/2,_=>(int)_rng.NextInt64(min,(long)max+1)};
            Calls++;if(Trace.Count<200)Trace.Add(value);return value;
        }
    }
    private RpgLootConfig LootConfig()
    {
        Options.Validate();var loot=Clone(Config.Adventures.Rpg.DiveLoot);
        loot.BaseDropChance=Options.BaseChance;loot.MaximumDropChance=Options.MaximumChance;loot.EventBonus=Options.EventBonus;loot.MaximumEventBonus=100;loot.LegendaryMinimumStake=Options.LegendaryMinimumStake;
        loot.RarityWeights=new() { [ItemRarity.Common]=Options.Common,[ItemRarity.Uncommon]=Options.Uncommon,[ItemRarity.Rare]=Options.Rare,[ItemRarity.Epic]=Options.Epic,[ItemRarity.Legendary]=Options.Legendary };
        if(Options.OnlyRarity is {} only)loot.RarityWeights=new(){[only]=1};
        if(Options.LootMode==RpgDebugLootMode.Garantiert)loot.BaseDropChance=loot.MaximumDropChance=100;
        if(Options.LootMode==RpgDebugLootMode.KeinLoot){loot.BaseDropChance=loot.MaximumDropChance=0;}
        loot.Validate();return loot;
    }
    public Task<object> RollLootAsync(CancellationToken token=default)=>Execute<object>("Lootwurf",async()=>
    {
        var rng=new DebugRandom(Options.Seed);var loot=LootConfig();var results=new List<RpgLootResult>();
        var mutation=await _points!.ApplyRpgAsync(PlayerId,"TEST",Guid.NewGuid().ToString("N"),a=>
        {
            for(var i=0;i<=Options.ExtraRolls;i++)
            {
                var sources=CharacterStatService.DiveLuckSources(a.Rpg,Config.Adventures.Rpg);
                var result=new RpgLootService(rng).Roll(Options.Stake,Options.Luck+sources.Perks,loot,DateTimeOffset.UtcNow,sources.Sushi,sources.Equipment,_points.HotTime.DropBonus(Config.Adventures.Rpg.HotTime));results.Add(result);
                if(result.Item is {} item) { if(item.Kind==RpgItemKind.Consumable)a.Rpg.Sushi[item.ConsumableRecipeId]=a.Rpg.Sushi.GetValueOrDefault(item.ConsumableRecipeId)+1;else a.Rpg.Inventory.Add(item); }
            }
            return new(true);
        },token);
        return new {mutation.Success,mutation.Error,Results=results,Rng=rng.Trace,rng.Calls};
    });
    private RpgItem Generate(string definitionId,RpgDebugRollMode mode,IReadOnlyDictionary<string,int>? custom,DebugRandom rng,string? effectId=null)
    {
        var item=Clone(Config.Adventures.Rpg.DiveLoot.Items.Single(i=>i.ItemId==definitionId));
        item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=DateTimeOffset.UtcNow;
        if(mode==RpgDebugRollMode.Benutzerdefiniert)
        {
            if(item.Generation is null||custom is null)throw new InvalidOperationException("Dieses feste Item hat keine benutzerdefinierten Wertebereiche.");
            foreach(var pair in custom)
            {
                var range=item.Generation.Attributes.SingleOrDefault(a=>a.Attribute==pair.Key)??throw new InvalidOperationException("Unbekanntes Testattribut.");
                if(pair.Value<range.Minimum||pair.Value>range.Maximum)throw new InvalidOperationException("Testwert außerhalb der Itemgrenzen: "+pair.Key);
                range.Minimum=range.Maximum=pair.Value;
            }
        }
        RpgItemGenerationService.Generate(item,Config.Adventures.Rpg.DiveLoot,rng,effectId);return item;
    }
    public Task<RpgItem> GenerateAsync(string definitionId,RpgDebugRollMode mode,IReadOnlyDictionary<string,int>? custom=null,CancellationToken token=default,string? effectId=null)=>Execute("Item erzeugen",async()=>
    {
        var rng=new DebugRandom(Options.Seed,mode);var item=Generate(definitionId,mode,custom,rng,effectId);
        var result=await _points!.ApplyRpgAsync(PlayerId,"TEST",Guid.NewGuid().ToString("N"),a=>{a.Rpg.Inventory.Add(item);return new(true);},token);
        if(!result.Success)throw new InvalidOperationException(result.Error);Log("Item-RNG",new {rng.Trace,rng.Calls});return Clone(item);
    });
    public Task<object> CombatAsync(bool defeatBoss=false,CancellationToken token=default)=>Execute<object>(defeatBoss?"Boss-Sieg simulieren":"Bossangriff",async()=>
    {
        var rng=new DebugRandom(Options.Seed);RpgBossHit? hit=null;var attempts=0;
        var result=await _points!.ApplyRpgAsync(PlayerId,"TEST",Guid.NewGuid().ToString("N"),a=>
        {
            // Forced results are rejection-sampled using the REAL combat calculation on detached test profiles.
            var source=Clone(a.Rpg);
            if(Options.ForceCritical||Options.ForceDouble||Options.ForceTriple)
            {
                if(Options.ForceCritical&&Config.Adventures.Rpg.CritChanceCap<=0||Options.ForceDouble&&Config.Adventures.Rpg.DoubleAttackChanceCap<=0||Options.ForceTriple&&Config.Adventures.Rpg.TripleAttackChanceCap<=0)throw new InvalidOperationException("Erzwingen benötigt einen positiven passenden Cap im TESTMODUS.");
                source.Buffs.Add(new(){RecipeId="debug-force-only",Activity=RpgActivity.Boss,RemainingUses=1,Attributes=new() {CritChance=Options.ForceCritical?50:0,DoubleAttackChance=Options.ForceDouble?20:0,TripleAttackChance=Options.ForceTriple?5:0}});
            }
            RpgProfile candidate=source;
            do
            {
                if(++attempts>100000)throw new InvalidOperationException("Erzwungenes Ergebnis unter den aktuellen Caps nicht erreichbar.");
                candidate=Clone(source);hit=RpgPassiveService.BossAttack(20,candidate,Config.Adventures.Rpg,rng,DateTimeOffset.UtcNow);
            }while(Options.ForceCritical&&!hit.Critical||Options.ForceDouble&&hit.ExtraHits!=1||Options.ForceTriple&&hit.ExtraHits!=2);
            // Keep only actual buff/cooldown changes; synthetic forcing gear never persists.
            RpgPassiveService.ConsumeBuffs(candidate,RpgActivity.Boss,Config.Adventures.Rpg,rng,DateTimeOffset.UtcNow);
            a.Rpg.Buffs=candidate.Buffs.Where(b=>b.RecipeId!="debug-force-only").ToList();candidate.Cooldowns.Remove("sushi-save:debug-force-only");a.Rpg.Cooldowns=candidate.Cooldowns;return new(true);
        },token);
        if(!result.Success)throw new InvalidOperationException(result.Error);
        RemainingBossHp=defeatBoss?0:Math.Max(0,RemainingBossHp-hit!.Damage);
        return new {Hit=hit,Attempts=attempts,RemainingBossHp,SimulatedVictory=RemainingBossHp==0,HotTimeTriggered=false,Rng=rng.Trace,rng.Calls};
    });
    public void ResetBoss(){Ensure();Options.Validate();RemainingBossHp=Options.BossHp;}
    public Task<object> ProgressAsync(string action,string selection="",long value=0,CancellationToken token=default)=>Execute<object>(action,async()=>
    {
        var id=Guid.NewGuid().ToString("N");var service=new RpgProgressionService(_points!,Config.Adventures.Rpg);
        if(action=="Sushi herstellen")return await service.CraftAsync(PlayerId,"TEST",id,selection,token);
        if(action=="Sushi aktivieren")return await service.ActivateSushiAsync(PlayerId,"TEST",id,selection,token);
        if(action=="Perlen ändern")return await _points!.AddPointsAsync(PlayerId,"TEST",value,0,token);
        if(action=="Aktivitätsbelohnung")return await _points!.AddPointsAsync(PlayerId,"TEST",Math.Max(0,value),0,token,source:RpgRewardSource.Activity);
        if(action=="Hot Time testen")return _points!.HotTime.StartAfterBoss("isolated-manual-"+id,Config.Adventures.Rpg.HotTime,true);
        if(action=="Hot Time beenden"){_points!.HotTime.End();return _points.HotTime.Status;}
        return await _points!.ApplyRpgAsync(PlayerId,"TEST",id,a=>
        {
            switch(action)
            {
                case "Perk setzen":
                    var perk=Config.Adventures.Rpg.Perks.Single(p=>p.Id==selection);if(value<0||value>perk.MaximumLevel)throw new InvalidOperationException("Ungültiges Perk-Level.");a.Rpg.Perks[selection]=(int)value;break;
                case "Fische erzeugen":
                    if(value<1||value>1000000||!Config.Adventures.Rpg.FishSpecies.Any(f=>f.Id==selection))throw new InvalidOperationException("Ungültiger Testfisch/-bestand.");
                    if(!a.Rpg.Fish.TryGetValue(selection,out var fish))a.Rpg.Fish[selection]=fish=new(){FishId=selection};fish.Quantity=checked(fish.Quantity+(int)value);break;
                case "Inventar zurücksetzen":a.Rpg.Inventory.Clear();a.Rpg.Equipment.Clear();break;
                case "Cooldowns zurücksetzen":a.Rpg.Cooldowns.Clear();break;
                case "Sushi-Verbrauch testen":
                    if(!Enum.TryParse<RpgActivity>(selection,out var activity)||!Enum.IsDefined(activity))throw new InvalidOperationException("Ungültige Testaktivität.");
                    RpgPassiveService.ConsumeBuffs(a.Rpg,activity,Config.Adventures.Rpg,new DebugRandom(Options.Seed),DateTimeOffset.UtcNow,Options.Stake);break;
                case "Ausrüstung anlegen":
                    a.Rpg.Equipment.Clear();
                    foreach(var item in a.Rpg.Inventory.Where(i=>i.Slot.HasValue))
                    {var slot=item.Slot!.Value;if(slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2&&a.Rpg.Equipment.ContainsKey(EquipmentSlot.Ring1))slot=EquipmentSlot.Ring2;a.Rpg.Equipment[slot]=item.InstanceId;}break;
                case "Set testen":
                    var set=Config.Adventures.Rpg.Sets.Single(s=>s.Id==selection);a.Rpg.Equipment.Clear();var rng=new DebugRandom(Options.Seed);
                    foreach(var itemId in set.ItemIds){var item=Generate(itemId,RpgDebugRollMode.Zufall,null,rng);a.Rpg.Inventory.Add(item);if(item.Slot.HasValue)a.Rpg.Equipment[item.Slot.Value]=item.InstanceId;}break;
                default:throw new InvalidOperationException("Unbekannte Testaktion.");
            }
            return new(true);
        },token);
    });
    public Task<object> HealthAsync(string action,int value,CancellationToken token=default)=>Execute<object>("HP-Test: "+action,async()=>
    {
        var cfg=Config.Adventures.Rpg.Health;var clock=DateTimeOffset.UtcNow;
        var result=await _points!.ApplyRpgAsync(PlayerId,"HP-TEST",Guid.NewGuid().ToString("N"),a=>
        {
            RpgHealthService.Refresh(a.Rpg,cfg,clock);
            var h=a.Rpg.Health;
            switch(action)
            {
                case "HP setzen": h.CurrentHp=Math.Clamp(value,0,RpgHealthService.Maximum(a.Rpg,cfg));break;
                case "Gegenangriff": h.CurrentHp=Math.Max(0,h.CurrentHp!.Value-RpgSushiService.BossDamage(RpgHealthService.CounterDamage(Math.Max(10,value),cfg,new DebugRandom(Options.Seed)),a.Rpg,clock));break;
                case "Boss-Lifeleech": RpgSushiService.HealFromBossAttack(Math.Max(0,value),Math.Max(0,value),a.Rpg,cfg,h.BossId,clock);break;
                case "Boss-Teilnahme": RpgSushiService.Participate(a.Rpg,"debug-sushi-"+value,clock.AddMinutes(2),clock);RpgSushiService.Close(a.Rpg,"debug-sushi-"+value,clock,clock,cfg);break;
                case "Besiegt markieren": h.BossId="debug-boss";h.Defeated=true;h.CurrentHp=0;break;
                case "Regeneration":
                    var elapsed=TimeSpan.FromMinutes(Math.Clamp(value,0,1440));h.LastPassiveRegen=clock-elapsed;
                    foreach(var b in a.Rpg.Buffs){b.ActivatedAt-=elapsed;if(b.ExpiresAt is {} end)b.ExpiresAt=end-elapsed;}
                    RpgHealthService.Refresh(a.Rpg,cfg,clock);break;
                case "Chat-Heilung":
                    if(h.LastChatHeal is null || (clock-h.LastChatHeal.Value).TotalSeconds>=cfg.ChatCooldownSeconds)
                    {h.CurrentHp=Math.Min(RpgHealthService.Maximum(a.Rpg,cfg),h.CurrentHp!.Value+cfg.ChatHp+RpgSushiService.ChatBonus(a.Rpg,clock));h.LastChatHeal=clock;}break;
            }
            return new(true);
        },token);
        var profile=(await ProfileAsync(token)).Rpg;
        return new {result,profile.PlayerLevel,profile.Health,MaximumHp=RpgHealthService.Maximum(profile,cfg),Eligible=RpgHealthService.Eligible(profile,profile.Health.BossId)};
    });
    public Task<RpgDebugSimulation> SimulateAsync(int runs,CancellationToken token=default)=>Execute("Simulation",async()=>
    {
        if(runs is not (100 or 1000 or 10000 or 100000))throw new ArgumentOutOfRangeException(nameof(runs));
        var profile=(await ProfileAsync(token)).Rpg;var loot=LootConfig();var rpg=Config.Adventures.Rpg;var rng=new DebugRandom(Options.Seed);
        var counts=Enum.GetValues<ItemRarity>().ToDictionary(r=>r,_=>0);int drops=0,crits=0,doubles=0,triples=0,rolls=0,extraProcs=0;decimal damage=0,pearls=0;
        var hotActive=_points!.HotTime.Status.Active;var hotPercent=hotActive&&rpg.HotTime.Enabled?rpg.HotTime.DropBonusPercent:0;
        var start=DateTimeOffset.UtcNow;
        for(var i=0;i<runs;i++)
        {
            token.ThrowIfCancellationRequested();if(!Active)throw new OperationCanceledException("Testmodus beendet.");var clock=start.AddSeconds(i*30L);
            var extra=RpgPassiveService.TryProc(profile,rpg,RpgActivity.Dive,RpgEffectTrigger.ExtraLoot,rng,clock,Options.Stake)>0?1:0;extraProcs+=extra;
            var sources=CharacterStatService.DiveLuckSources(profile,rpg);
            for(var j=0;j<=Options.ExtraRolls+extra;j++){rolls++;var result=new RpgLootService(rng).Roll(Options.Stake,Options.Luck+sources.Perks,loot,clock,sources.Sushi,sources.Equipment,hotPercent);if(result.Item is {} item){counts[item.Rarity]++;drops++;}}
            var hit=RpgPassiveService.BossAttack(20,profile,rpg,rng,clock);damage+=hit.Damage;if(hit.Critical)crits++;if(hit.ExtraHits==1)doubles++;if(hit.ExtraHits==2)triples++;
            var outcome=AdventureGameService.SoloOutcome("!dive",rng.NextInclusive(1,100),Config.Adventures.Treasure);
            var payout=RpgPassiveService.RewardPayout((long)decimal.Floor(Options.Stake*outcome.Item1),Options.Stake,CharacterStatService.Calculate(profile,rpg,RpgActivity.Dive).PearlBonus);
            var net=Math.Max(0,payout-Options.Stake);pearls+=payout-net+RpgHotTimeService.ApplyAt(new(net,RpgRewardSource.DiveNetWin),rpg.HotTime,hotActive).Amount-Options.Stake;
            RpgPassiveService.ConsumeBuffs(profile,RpgActivity.Boss,rpg,rng,clock);
            RpgPassiveService.ConsumeBuffs(profile,RpgActivity.Dive,rpg,rng,clock,Options.Stake);
        }
        Log("Simulations-RNG",new {rng.Trace,rng.Calls,HotTimeSnapshot=hotActive});return new RpgDebugSimulation(runs,drops,counts,crits,doubles,triples,damage/runs,pearls/runs,Options.Seed,rolls,extraProcs);
    });
}
