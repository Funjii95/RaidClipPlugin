using System.Text.Json;
namespace SnapshotProtocol;

public sealed record TalentDefinition(string Id,string Name,string Branch,int Max,decimal Value,string Effect,string Description,string[] Requires);
public static class TalentRules
{
    public const int CatalogVersion=1;
    public const int BranchMaximum=33;
    public static IReadOnlyList<TalentDefinition> Catalog {get;} = Array.AsReadOnly(JsonSerializer.Deserialize<TalentDefinition[]>(
        typeof(TalentRules).Assembly.GetManifestResourceStream("SnapshotProtocol.talents.json")!,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!);
    public static int Earned(int level)=>Math.Clamp(level,1,100)-1;
    public static int Spent(IReadOnlyDictionary<string,int> ranks)=>ranks.Values.Sum();
    public static int Available(int level,IReadOnlyDictionary<string,int> ranks)=>Math.Max(0,Earned(level)-Spent(ranks));
    public static void Validate(int level,IReadOnlyDictionary<string,int>? ranks)
    {
        if(ranks is null || ranks.Count>Catalog.Count)throw new InvalidDataException("Ungültiger Talentstand.");
        foreach(var (id,rank) in ranks)
        {
            var t=Catalog.SingleOrDefault(t=>t.Id==id);
            if(t is null || rank<0 || rank>t.Max || rank>0 && t.Requires.Any(r=>ranks.GetValueOrDefault(r)<3))
                throw new InvalidDataException("Ungültige Talentränge oder Voraussetzungen.");
        }
        if(Spent(ranks)>Earned(level) || Catalog.Select(t=>t.Branch).Distinct().Any(b=>Catalog.Where(t=>t.Branch==b).Sum(t=>ranks.GetValueOrDefault(t.Id))>BranchMaximum))
            throw new InvalidDataException("Talentpunkte überschreiten den Levelanspruch.");
    }
    public static string? Rejection(string id,int level,IReadOnlyDictionary<string,int> ranks,int expectedRank,int catalogVersion)
    {
        Validate(level,ranks);
        if(catalogVersion!=CatalogVersion)return "Talentkatalog veraltet. Bitte aktualisieren.";
        var t=Catalog.SingleOrDefault(t=>t.Id==id);
        if(t is null)return "Unbekanntes Talent.";
        var rank=ranks.GetValueOrDefault(id);
        if(rank!=expectedRank)return "Talentstand wurde bereits geändert. Bitte aktualisieren.";
        if(rank>=t.Max)return "Maximalrang erreicht.";
        if(t.Requires.Any(r=>ranks.GetValueOrDefault(r)<3))return "Voraussetzung: vorherige Talente auf Rang 3.";
        if(Available(level,ranks)<1)return "Keine Talentpunkte verfügbar.";
        if(Catalog.Where(v=>v.Branch==t.Branch).Sum(v=>ranks.GetValueOrDefault(v.Id))>=BranchMaximum)return "Bereich vollständig ausgebaut.";
        return null;
    }
    public static decimal Bonus(IReadOnlyDictionary<string,int> ranks,string effect)=>Catalog.Where(t=>t.Effect==effect)
        .Sum(t=>t.Value*Math.Clamp(ranks.GetValueOrDefault(t.Id),0,t.Max));
}
