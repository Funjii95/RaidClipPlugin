using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Perls buy perks; level-up talent points are deliberately not used here.
public static class RpgPerkCatalog
{
    public sealed record Entry(string Id,string Name,string Category,string Symbol,string Effect,decimal Value,string Label);
    public static IReadOnlyList<Entry> Entries {get;} = new Entry[]
    {
        new("damage","Scharfe Krallen","combat","✹","",0,""),
        new("crit","Kritische Welle","combat","✦","",0,""),
        new("critdamage","Kritische Wucht","combat","✷","",0,""),
        new("boss","Bossjäger","combat","☠","",0,""),
        new("iron-skin","Eiserne Haut","combat","⛨","health",4,"% mehr Basis-MaxHP"),
        new("battle-rush","Kampfrausch","combat","⚔","attackCooldown",3,"% kürzerer Boss-Angriffscooldown"),
        new("life-steal","Lebensraub","combat","♥","lifeleech",1,"% Heilung aus tatsächlichem Kampfschaden"),
        new("otter-rage","Otterzorn","combat","✺","victoryDamage",3,"% mehr Schaden für 60 Sekunden nach einem Kampfsieg"),
        new("pearl-blade","Perlenklinge","combat","✧","bossPearls",5,"% mehr Boss-Siegperlen"),
        new("fishing","Angelglück","fishing","⚓","",0,""),
        new("steady-hand","Ruhige Hand","fishing","◈","catch",4,"Prozentpunkte weniger Fehlfänge"),
        new("double-catch","Doppelfang","fishing","♊","doubleFish",3,"% Chance auf einen zusätzlichen Fisch"),
        new("bait-expert","Köderkunde","fishing","∿","rareFish",5,"% mehr relatives Fanggewicht ab selten"),
        new("deep-hook","Tiefseehaken","fishing","♧","qualityFish",4,"% mehr relatives Fanggewicht ab episch"),
        new("fresh-catch","Frische Beute","fishing","✚","ingredient",5,"% Chance auf einen zusätzlichen Zutatenfisch"),
        new("quick-reel","Schnelle Rolle","fishing","↻","fishCooldown",3,"% kürzerer Angelcooldown"),
        new("pearl-scales","Reicher Fang","fishing","◌","reserveFish",3,"% Chance auf einen zusätzlichen Fisch derselben Art"),
        new("legendary-catch","Legendärer Fang","fishing","★","legendaryFish",2,"% mehr relatives Fanggewicht legendärer Fische"),
        new("dive","Tauchglück","diving","◉","",0,""),
        new("deep-diver","Tiefentaucher","diving","◎","extraLoot",3,"% Chance auf einen zusätzlichen Item-Lootwurf"),
        new("treasure-hunter","Schatzsucher","diving","▣","treasure",4,"% höhere relative Sammlerschatzchance"),
        new("relic-hunter","Reliktjäger","diving","♜","rareTreasure",4,"% höhere relative Chance auf seltene Sammlerschätze"),
        new("swift-flippers","Flinke Flossen","diving","➤","diveCooldown",3,"% kürzerer Tauchcooldown"),
        new("safe-ascent","Sicherer Aufstieg","diving","↑","underwaterDodge",4,"% Chance auf vermiedene Unterwasser-Gegnertreffer (keine Bosse)"),
        new("salvage-pro","Bergungsprofi","diving","⚒","loot",4,"Prozentpunkte mehr Item-Dropchance beim Tauchen"),
        new("pearl-chest","Materialspürsinn","diving","◍","diveMaterials",5,"% höhere relative Materialfundchance beim Tauchen (keine Mimics)"),
        new("abyss-sense","Abgrundsinn","diving","♆","bossDiscovery",0.1m,"Prozentpunkte zusätzliche Mimic-Chance (plus Grundchance)"),
        new("luck","Glücksotter","support","☘","",0,""),
        new("sushi-master","Sushi-Meister","support","❖","sushiPower",5,"% stärkere persönliche Sushi-Boni"),
        new("emergency-stock","Notvorrat","support","✚","regen",1,"zusätzliche HP je regulärem Heilintervall"),
        new("protective-bubble","Schutzblase","support","◯","defense",2,"% weniger Boss-Gegenschaden")
    };
    public static Entry? Find(string id)=>Entries.FirstOrDefault(e=>e.Id==id);
    public static decimal Bonus(RpgProfile profile,string effect)=>Entries.Where(e=>e.Effect==effect && effect.Length>0)
        .Sum(e=>e.Value*Math.Clamp(profile.Perks.GetValueOrDefault(e.Id),0,5));
    public static decimal Combined(RpgProfile profile,string effect)=>RpgTalentService.Bonus(profile,effect)+Bonus(profile,effect);
    public static bool Proc(RpgProfile profile,string effect,IDuelRandom random)
    {
        var chance=Math.Clamp(Combined(profile,effect),0,100);
        return chance>0 && random.NextInclusive(0,9999)<chance*100;
    }
    public static void Victory(RpgProfile profile,DateTimeOffset now)
    {if(Bonus(profile,"victoryDamage")>0)profile.Cooldowns["perk:otter-rage"]=now.AddSeconds(60);}
    public static decimal MimicChance(RpgProfile profile,RpgConfig config)=>config.Mimic.Enabled?Math.Clamp(config.Mimic.BaseSpawnChance+Bonus(profile,"bossDiscovery"),0,100):0;
    public static bool DiscoverMimic(RpgProfile profile,RpgConfig config,IDuelRandom random)
    {var chance=MimicChance(profile,config);return chance>0 && random.NextInclusive(0,9999)<chance*100;}
    public static double Cooldown(RpgProfile profile,int seconds,string effect)=>Math.Max(1,Math.Clamp(seconds,1,86400)*(1-(double)Math.Clamp(Combined(profile,effect),0,95)/100));
    public static void AddMissing(RpgConfig config)
    {
        foreach(var e in Entries)
        {
            var perk=config.Perks.FirstOrDefault(p=>p.Id==e.Id);
            if(perk is null){perk=new(){Id=e.Id};config.Perks.Add(perk);}
            if(string.IsNullOrWhiteSpace(perk.DisplayName))perk.DisplayName=e.Name;
            perk.MaximumLevel=5;
        }
    }
}
