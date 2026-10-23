using RaidClipPlugin.Models;
namespace RaidClipPlugin.Config;
public sealed class RpgZoneDefinition
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public int Level {get;set;}
    public int Hp {get;set;}
    public int Attack {get;set;}
    public int Armor {get;set;}
    public string[] Enemies {get;set;}=[];
    public string[] Materials {get;set;}=[];
    public string[] Fish {get;set;}=[];
    public ItemRarity MaximumRarity {get;set;}
    public decimal ChestChance {get;set;}
    public decimal MimicChance {get;set;}
    public decimal EnemyChance {get;set;}=45;
    public decimal EliteChance {get;set;}=10;
}
public sealed class RpgZoneConfig
{
    public List<RpgZoneDefinition> Areas {get;set;}=Defaults();
    public Dictionary<string,decimal> EternalChances {get;set;}=new(){["normal"]=.03m,["elite"]=.12m,["mimic"]=.20m,["endgame"]=.35m};
    public Dictionary<string,decimal> EternalEightChances {get;set;}=new(){["normal"]=.01m,["elite"]=.04m,["mimic"]=.08m,["eonath"]=.10m};
    public RpgZoneDefinition Get(string id)=>Areas.SingleOrDefault(z=>z.Id==id)??throw new InvalidOperationException("Unbekanntes Gebiet: "+id+". Bitte ein gültiges Tauchgebiet auswählen.");
    public void Validate()
    {
        var standard=Defaults();
        if(Areas.Count!=10||Areas.Select(z=>z.Id).Distinct().Count()!=10||Areas.Any(z=>!standard.Any(s=>s.Id==z.Id&&s.Level==z.Level&&s.Name==z.Name)||z.Hp is <1 or >1000000||z.Attack is <1 or >1000000||z.Armor is <0 or >100000||z.Enemies.Length!=3||z.Enemies.Any(string.IsNullOrWhiteSpace)||z.ChestChance is <0 or >100||z.MimicChance is <0 or >100||z.EnemyChance is <0 or >100||z.EliteChance is <0 or >100||z.ChestChance+z.MimicChance+z.EnemyChance>100||z.MaximumRarity==ItemRarity.Divine))throw new InvalidOperationException("Ungültige Tauchgebietsregeln.");
        if(EternalChances.Count!=4||new[]{"normal","elite","mimic","endgame"}.Any(k=>!EternalChances.TryGetValue(k,out var v)||v is <0 or >100))throw new InvalidOperationException("Ungültige Eternal-Chancen.");
        if(EternalEightChances.Count!=4||new[]{"normal","elite","mimic","eonath"}.Any(k=>!EternalEightChances.TryGetValue(k,out var v)||v is <0 or >100))throw new InvalidOperationException("Ungültige Eternal-8er-Chancen.");
    }
    public static List<RpgZoneDefinition> Defaults()
    {
        string[] ids=["sunny_lagoon","sunken_temples","shipwreck_graveyard","glowing_coral_forest","tidal_maelstrom","crystal_grotto","cursed_depths","true_abyss","abyss","primordial_throne"];
        string[] names=["Sonnenlagune","Versunkene Tempel","Wrackfriedhof","Leuchtender Korallenwald","Strudel der Gezeiten","Kristallgrotten","Verfluchte Tiefen","Wahrer Abgrund","Abyss","Thron der Urtiefe"];
        string[][] enemies=[["Kleine Riffkrabbe","Frecher Seeigel","Junger Lagunenräuber"],["Tempelwächterkrabbe","Runenqualle","Vergessener Muschelgolem"],["Verfluchter Piratenkrebs","Wrackräuber","Geisteraal"],["Korallenbestie","Leuchtender Kappa","Kristallqualle"],["Sturmrochen","Strömungsjäger","Gezeitenkoloss"],["Kristallgolem","Frostpanzerkrabbe","Prismatischer Tiefenjäger"],["Fluchbestie","Schattenkrabbe","Verderbter Tiefenwächter"],["Wahrer Korallenwächter","Uralter Riffkoloss","Wächter der Ewigen Strömung"],["Abyss-Verschlinger","Leerenjäger","Schattenleviathan"],["Urzeit-Verschlinger","Königlicher Abyss-Wächter","Avatar der Urtiefe"]];
        string[][] mats=[["pearl_dust","white_pearl"],["blue_pearl","reinforcement_core"],["cursed_silk"],["luminous_scale","blue_pearl"],["red_pearl","reinforcement_core"],["rune_shell"],["cursed_silk"],["true_coral_core"],["abyss_shard"],["eternal_depth_core","abyss_shard"]];
        // Reuse registry fish, with a distinct three-species pool per area. New local species
        // supplement the five recipe fish, which remain available in multiple areas.
        string[] recipe=["sardine","perch","eel","crystal-carp","leviathan"];
        int[] hp=[70,150,270,430,650,950,1350,1900,2650,3700],attack=[8,16,28,44,65,92,130,185,260,360],armor=[3,7,12,19,28,40,55,75,100,135];
        int[] chest=[8,10,12,14,16,18,20,22,24,28],mimic=[1,2,4,3,4,5,7,8,10,12];
        ItemRarity[] maximum=[ItemRarity.Uncommon,ItemRarity.Rare,ItemRarity.Epic,ItemRarity.Epic,ItemRarity.Legendary,ItemRarity.Legendary,ItemRarity.Cursed,ItemRarity.True,ItemRarity.Abyss,ItemRarity.Eternal];
        return Enumerable.Range(0,10).Select(i=>new RpgZoneDefinition{Id=ids[i],Name=names[i],Level=(i+1)*10,Hp=hp[i],Attack=attack[i],Armor=armor[i],Enemies=enemies[i],Materials=mats[i],MaximumRarity=maximum[i],ChestChance=chest[i],MimicChance=mimic[i],Fish=(i==0?new[]{"sardine","perch","eel","crystal-carp","leviathan","sea_cucumber","shrimp","giant_shrimp","pufferfish","golden_pufferfish","vampire_squid","tuna"}:new[]{recipe[i%5],recipe[(i+1)%5],new[]{"shrimp","sea_cucumber","giant_shrimp","pufferfish","tuna","golden_pufferfish","vampire_squid","giant_shrimp","tuna","vampire_squid"}[i]})}).ToList();
    }
}
