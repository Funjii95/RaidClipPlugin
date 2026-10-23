namespace RaidClipPlugin.Config;
public sealed class RpgAreaBossDefinition
{
 public string Id {get;set;}="";public string Name {get;set;}="";public string AreaId {get;set;}="";public int Level {get;set;}
 public int BaseHp {get;set;} public int Attack {get;set;} public int Experience {get;set;} public long Pearls {get;set;}
 public decimal ItemChance {get;set;} public string Rarity {get;set;}="";public string[] Materials {get;set;}=[];
 public string Ability {get;set;}="";public decimal AttackFactor {get;set;}=1;public int Hits {get;set;}=1;public decimal ArmorIgnore {get;set;} public decimal ReductionIgnore {get;set;}
 public bool Enabled {get;set;}=true;
}
public sealed class RpgAreaBossConfig
{
 public int SchemaVersion {get;set;} public bool Enabled {get;set;}
 public List<RpgAreaBossDefinition> Bosses {get;set;}=Defaults();
 public static List<RpgAreaBossDefinition> Defaults()
 {
  string[] ids=["korallus","thalruun","moderklaue","luminara","vortexion","krysalith","maledictus","aethernos","nharzul","eonath"];
  string[] names=["Korallus, der Riffkönig","Thal'Ruun, der Tempelhüter","Käpt'n Moderklaue","Luminara, Herz des Riffs","Vortexion, Herr der Strudel","Krysalith, der Splitterfürst","Maledictus, der Fluchträger","Aethernos, Wächter der Wahrheit","Nhar'Zul, Verschlinger der Leere","Eonath, der Ewige Herrscher"];
  int[] hp=[15000,35000,70000,120000,200000,320000,500000,850000,1500000,3000000],attack=[25,50,90,145,220,320,450,650,950,1400],xp=[500,1000,2000,3500,6000,10000,16000,25000,40000,65000];
  decimal[] chances=[12,10,8,6,3,3,2,1,.5m,.15m],factors=[1,1.25m,1,1.35m,.55m,1.6m,1.4m,1.85m,2.2m,1.1m];
  string[] rarities=["Uncommon","Rare","Epic","Epic","Legendary","Legendary","Cursed","True","Abyss","Eternal"];
  string[] abilities=["Korallenpanzer: 20 % weniger eingehender Schaden für drei eigene Angriffszyklen","Runenentladung: magischer Angriff mit 125 % Basisangriff","Geisterfluch: direkter Angriff plus drei Intervalle zu 10 % Basisangriff","Lichtblüte: 135 % Basisangriff, 20 % persönlicher Eigenschutz","Gezeitensturm: drei Treffer zu 55 % Basisangriff","Kristallbruch: 160 % Basisangriff, ignoriert 20 % Rüstung","Seelenraub: 140 % Basisangriff, nächster Zyklus +10 % Schaden","Wahrer Gezeitenschlag: 185 % Basisangriff, ignoriert 25 % Schadensreduktion","Leerenverschlingung: 220 % Basisangriff plus 10 % tatsächlicher Schaden als Lebensentzug","Urteil der Ewigkeit: drei Treffer zu 110 %, letzter ignoriert 30 % Rüstung"];
  var zones=RpgZoneConfig.Defaults();
  return Enumerable.Range(0,10).Select(n=>new RpgAreaBossDefinition{Id="boss_"+ids[n],Name=names[n],AreaId=zones[n].Id,Level=(n+1)*10,BaseHp=hp[n],Attack=attack[n],Experience=xp[n],Pearls=zones[n].Level*10,ItemChance=chances[n],Rarity=rarities[n],Materials=zones[n].Materials,Ability=abilities[n],AttackFactor=factors[n],Hits=n is 4 or 9?3:1,ArmorIgnore=n==5?20:0,ReductionIgnore=n==7?25:0}).ToList();
 }
 public void Validate()
 {
  var defaults=Defaults();
  if(Bosses.Count!=10||Bosses.Select(b=>b.Id).Distinct().Count()!=10||Bosses.Any(b=>!defaults.Any(d=>d.Id==b.Id&&d.Name==b.Name&&d.AreaId==b.AreaId&&d.Level==b.Level)||b.BaseHp is <1 or >1000000000||b.Attack is <1 or >1000000||b.Experience is <0 or >1000000000||b.Pearls is <0 or >1000000000||b.ItemChance is <0 or >100||b.AttackFactor is <0 or >20||b.Hits is <1 or >10||b.ArmorIgnore is <0 or >100||b.ReductionIgnore is <0 or >100))throw new InvalidOperationException("Ungültige Gebietsbossregeln.");
 }
}
