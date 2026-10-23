using RaidClipPlugin.Models;

namespace RaidClipPlugin.Config;

public sealed class RpgMaterialDropRule
{
    public decimal Chance {get;set;}
    public int Minimum {get;set;}=1;
    public int Maximum {get;set;}=1;
}
public sealed class RpgUpgradeConfig
{
    public Dictionary<string,decimal> EquipmentDropChances {get;set;}=new(){["True"]=0.25m,["Cursed"]=0.4m,["Abyss"]=0.1m};
    public long[] PearlCosts {get;set;}=[100,250,500,1000,2000,3500,5000,8000,12000];
    public int[] CoreCosts {get;set;}=[0,0,0,0,1,2,3,4,5];
    public int[] ColoredPearlCosts {get;set;}=[1,1,1];
    public Dictionary<string,int[]> RarityCosts {get;set;}=Enum.GetValues<ItemRarity>().ToDictionary(r=>r.ToString(),r=>r<=ItemRarity.Rare?new[]{2,3,5}:new[]{1,2,3});
    public long RefinementPearls {get;set;}=15000;
    public Dictionary<string,Dictionary<string,RpgMaterialDropRule>> Drops {get;set;}=Defaults();
    public Dictionary<string,int> MimicWeights {get;set;}=new(){["red_pearl"]=30,["rune_shell"]=25,["cursed_silk"]=18,["true_coral_core"]=15,["abyss_shard"]=10,["eternal_depth_core"]=2};
    public void Validate()
    {
        if(ColoredPearlCosts.Length!=3||ColoredPearlCosts.Any(n=>n is <0 or >1000000)||RarityCosts.Count!=Enum.GetValues<ItemRarity>().Length||Enum.GetValues<ItemRarity>().Any(r=>!RarityCosts.TryGetValue(r.ToString(),out var counts)||counts.Length!=3||counts.Any(n=>n is <0 or >1000000)))throw new InvalidOperationException("Ungültige Materialkosten.");
        if(EquipmentDropChances.Count!=3||new[]{"True","Cursed","Abyss"}.Any(k=>!EquipmentDropChances.ContainsKey(k))||EquipmentDropChances.Values.Any(v=>v is <0 or >100))throw new InvalidOperationException("Ungültige Erweiterungs-Dropchancen.");
        if(PearlCosts.Length!=9||CoreCosts.Length!=9||PearlCosts.Any(n=>n<0||n>1_000_000_000)||CoreCosts.Any(n=>n<0||n>1000000)||RefinementPearls<0||RefinementPearls>1_000_000_000)throw new InvalidOperationException("Ungültige Aufwertungskosten.");
        var expected=Defaults();
        if(Drops.Count!=expected.Count||expected.Keys.Any(k=>!Drops.ContainsKey(k))||Drops.Any(s=>s.Value.Count!=expected[s.Key].Count||s.Value.Any(v=>!expected[s.Key].ContainsKey(v.Key)||v.Value.Chance is <0 or >100||v.Value.Minimum<1||v.Value.Maximum<v.Value.Minimum||v.Value.Maximum>1000))||MimicWeights.Count!=6||new RpgUpgradeConfig(false).MimicWeights.Keys.Any(k=>!MimicWeights.ContainsKey(k))||MimicWeights.Values.Any(v=>v<0||v>1000000)||MimicWeights.Values.Sum()==0)throw new InvalidOperationException("Ungültige Material-Fundregeln.");
    }
    public RpgUpgradeConfig(){}
    private RpgUpgradeConfig(bool unused){}
    private static Dictionary<string,Dictionary<string,RpgMaterialDropRule>> Defaults()
    {
        Dictionary<string,RpgMaterialDropRule> Pool(params (string Id,decimal Chance)[] values)=>values.ToDictionary(v=>v.Id,v=>new RpgMaterialDropRule{Chance=v.Chance,Maximum=v.Id=="pearl_dust"?3:v.Id=="luminous_scale"?2:1});
        var result=new Dictionary<string,Dictionary<string,RpgMaterialDropRule>>{
            ["dive"]=Pool(("pearl_dust",25),("white_pearl",12),("blue_pearl",5),("reinforcement_core",10),("cursed_silk",2)),
            ["fish"]=Pool(("luminous_scale",8),("pearl_dust",12),("white_pearl",6),("blue_pearl",3),("reinforcement_core",4)),
            ["boss"]=Pool(("reinforcement_core",20),("red_pearl",8),("rune_shell",12),("true_coral_core",3),("abyss_shard",0.5m),("eternal_depth_core",0.25m)),
            ["enemy"]=Pool(("pearl_dust",15),("white_pearl",5),("reinforcement_core",3))};
        string[] ids=["pearl_dust","white_pearl","blue_pearl","red_pearl","reinforcement_core","luminous_scale","rune_shell","cursed_silk","true_coral_core","abyss_shard","eternal_depth_core"];
        decimal[][] chances=[[35,20,8,1,20,10,0.5m,2,0.5m,0,0],[45,30,18,5,35,20,4,5,2,0.2m,0.1m],[50,40,30,12,50,30,12,10,6,1,0.5m]];
        for(int n=0;n<3;n++)result[new[]{"chest","rareChest","legendaryChest"}[n]]=Pool(ids.Select((id,i)=>(id,chances[n][i])).ToArray());
        result["legendaryChest"]["reinforcement_core"].Maximum=2;return result;
    }
}
