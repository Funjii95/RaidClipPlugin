namespace RaidClipPlugin.Models;

public sealed record RpgMaterialDefinition(string Id,string Name,ItemRarity Rarity,string Description,string[] Sources);
public static class RpgUpgradeCatalog
{
    public static readonly RpgMaterialDefinition[] Materials = [
        new("pearl_dust","Perlenstaub",ItemRarity.Uncommon,"Feiner magischer Staub für einfache Aufwertungen.",["Tauchen","Angeln","Kleine Gegner","Truhen"]),
        new("white_pearl","Weiße Perle",ItemRarity.Uncommon,"Allgemeine Ausrüstungsaufwertung auf +7.",["Tauchen","Angeln","Kleine Gegner","Truhen"]),
        new("blue_pearl","Blaue Perle",ItemRarity.Rare,"Gebündelte Meeresströmungen für +8.",["Tauchen","Angeln","Truhen"]),
        new("red_pearl","Rote Perle",ItemRarity.Epic,"Wilde Energie für die Aufwertung auf +9.",["Bosse","Truhen","Mimics"]),
        new("luminous_scale","Leuchtende Flossenschuppe",ItemRarity.Epic,"Verbessert epische Ausrüstung.",["Angeln","Truhen"]),
        new("rune_shell","Runenmuschel",ItemRarity.Legendary,"Vergessene Runen verbessern legendäre Ausrüstung.",["Bosse","Truhen","Mimics"]),
        new("eternal_depth_core","Kern der Ewigen Tiefe",ItemRarity.Legendary,"Einmalige Endveredelung auf +9.",["Bosse","Seltene und legendäre Truhen","Mimics"]),
        new("true_coral_core","Wahrer Korallenkern",ItemRarity.True,"Reine Korallenenergie verbessert wahre Ausrüstung.",["Bosse","Truhen","Mimics"]),
        new("cursed_silk","Verfluchter Seidenfaden",ItemRarity.Cursed,"Uralte Magie für verfluchte Ausrüstung.",["Tauchen","Truhen","Mimics"]),
        new("abyss_shard","Abyss-Scherbe",ItemRarity.Abyss,"Endlose Finsternis für Abyss-Aufwertungen.",["Bosse","Seltene und legendäre Truhen","Mimics"]),
        new("reinforcement_core","Verstärkungskern",ItemRarity.Rare,"Stabilisiert garantierte Aufwertungen ab +5.",["Tauchen","Angeln","Kleine Gegner","Bosse","Truhen"])
    ];
    public static readonly IReadOnlyDictionary<string,string> Properties = new Dictionary<string,string>{
        ["pearl_dust"]="PearlDust",["white_pearl"]="WhitePearls",["blue_pearl"]="BluePearls",["red_pearl"]="RedPearls",
        ["luminous_scale"]="LuminousScale",["rune_shell"]="RuneShell",["eternal_depth_core"]="EternalDepthCore",
        ["true_coral_core"]="TrueCoralCore",["cursed_silk"]="CursedSilk",["abyss_shard"]="AbyssShard",["reinforcement_core"]="ReinforcementCore"};
    public static int Count(RpgMaterials stock,string id)=>(int)typeof(RpgMaterials).GetProperty(Properties[id])!.GetValue(stock)!;
    public static void Add(RpgMaterials stock,string id,int amount)
    {
        var value=checked(Count(stock,id)+amount);if(value<0)throw new InvalidOperationException("Nicht genügend Materialien.");
        typeof(RpgMaterials).GetProperty(Properties[id])!.SetValue(stock,value);
    }
}
