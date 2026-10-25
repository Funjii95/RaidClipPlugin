using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record QuestRequirement(string Id,string Text,string Event,int Target,string FishId="",string RecipeFishId="");
public sealed record QuestChapter(string Name,IReadOnlyList<QuestRequirement> Requirements);
public sealed record QuestDefinition(string Id,string Name,int Level,string ZoneId,string SetId,IReadOnlyList<QuestChapter> Chapters);
public static class RpgQuestCatalog
{
 public const long TutorialReward=100000;
 private static QuestRequirement Need(string id,string text,string action,int count=1,string fish="",string recipeFish="")=>new(id,text,action,count,fish,recipeFish);
 public static readonly QuestDefinition Tutorial=new("tutorial","Die ersten Pfotenschritte",1,"sunny_lagoon","",[
  new("Abtauchen",[Need("dive","Einen Tauchgang abschließen","dive")]),new("Gut gerüstet",[Need("equip","Einen Gegenstand ausrüsten","equip")]),new("Ein bisschen stärker",[Need("upgrade","Einen Gegenstand einmal aufwerten","upgrade")]),new("Frischer Fang",[Need("fish","Drei Fische in der Sonnenlagune fangen","fish",3)]),new("Otter am Herd",[Need("craft","Ein Sushi kochen","craft")]),new("Guten Appetit",[Need("eat","Ein Sushi essen","eat")]),new("Bereit fürs Abenteuer",[Need("buffedDive","Mit aktivem Sushi einen Tauchgang abschließen","buffedDive")])]);
 public static readonly string[] SetNames=["Sonnenwächter","Runenhüter","Schatzkapitän","Korallensänger","Sturmflutjäger","Kristallwächter","Fluchbrecher","Ursprungsbewahrer","Abgrundbezwinger","Herrscher der zehn Meere"];
 public static readonly QuestDefinition[] Chains=Build();
 public static IEnumerable<QuestDefinition> All=>new[]{Tutorial}.Concat(Chains);
 private static QuestDefinition[] Build(){
  string[] ids=["lagoon","temple","wreck","coral","tides","crystal","curse","origin","abyss","throne"],zones=["sunny_lagoon","sunken_temples","shipwreck_graveyard","glowing_coral_forest","tidal_maelstrom","crystal_grotto","cursed_depths","true_abyss","abyss","primordial_throne"],names=["Das Vermächtnis der Sonnenlagune","Die Hüter der versunkenen Tempel","Der letzte Schatzkapitän","Das Lied des Korallenwaldes","Prüfung der Gezeiten","Das Herz der Kristallgrotten","Den Fluch brechen","Die ursprüngliche Strömung","Dem Abyss die Stirn bieten","Erbe der zehn Meere"],fish=["","rune_ray","eel","rainbow_kappa","tide_shark","crystal-carp","bloodfin","eternal_kappa","devourer_fish","primordial_kingfish"],fishNames=["Fische","Runenrochen","Tiefsee-Aale","Regenbogenkappa","Gezeitenhai","Kristallkarpfen","Blutflosse","Ewigen Kappa","Verschlingerfisch","Königsfisch der Urtiefe"];
  int[] dives=[2,3,3,3,3,3,4,4,4,4],kills=[2,3,3,3,4,4,4,4,5,5];
  return Enumerable.Range(0,10).Select(i=>{var catches=new List<QuestRequirement>{Need("fish",$"{(i==0?3:i==2?2:1)} × {fishNames[i]} fangen","fish",i==0?3:i==2?2:1,fish[i])};if(i==5)catches.Add(Need("fish2","Einen Prismatischen Aal fangen","fish",1,"prismatic_eel"));var recipe=i is 1 or 3 or 4 or 6 or 7 or 8 or 9?fish[i]:"";return new QuestDefinition(ids[i],names[i],i==9?99:(i+1)*10,zones[i],"quest_"+ids[i],[new("Das Gebiet erkunden",[Need("dive",$"{dives[i]} Tauchgänge im Gebiet abschließen","dive",dives[i])]),new("Frischer Fang",catches),new("Ein Festmahl vorbereiten",[Need("craft",recipe.Length>0?"Passendes Fisch-Sushi kochen":"Ein Sushi kochen","craft",1,recipeFish:recipe),Need("eat",recipe.Length>0?"Passendes Fisch-Sushi essen":"Ein Sushi essen","eat",1,recipeFish:recipe)]),new("Die Prüfung bestehen",[Need("kills",$"{kills[i]} Gegner im Gebiet besiegen","kills",kills[i])])]);}).ToArray();
 }
 public static List<RpgItem> Items(){
  EquipmentSlot[] slots=[EquipmentSlot.Helmet,EquipmentSlot.Chest,EquipmentSlot.Gloves,EquipmentSlot.Boots,EquipmentSlot.Weapon,EquipmentSlot.Ring1,EquipmentSlot.Ring1,EquipmentSlot.Amulet];string[] labels=["Helm","Brustpanzer","Handschuhe","Stiefel","Waffe","Ring der Ebbe","Ring der Flut","Amulett"];
  return Chains.SelectMany((q,index)=>slots.Select((slot,n)=>{var a=new RpgAttributes{Power=4+q.Level/5m,Stamina=5+q.Level/4m};if(n<4)a.Armor=8+q.Level*1.1m;if(slot==EquipmentSlot.Weapon)a.Attack=45+q.Level*4;if(n>=5)a.Luck=2+q.Level/20m;
   var item=new RpgItem{ItemId=q.SetId+"_"+n,IconId="quest-"+slot,DisplayName=SetNames[index]+" – "+labels[n],Description=$"Garantierte legendäre Questbelohnung ab Level {q.Level}. Achtteiliges Set.",Kind=RpgItemKind.Equipment,Slot=slot,SetId=q.SetId,Rarity=ItemRarity.Legendary,DropWeight=0,DropActivities=[],Attributes=a};RpgCoreStatService.Apply(item);return item;})).ToList();
 }
 public static List<RpgItemSet> Sets()=>Chains.Select((q,i)=>new RpgItemSet{Id=q.SetId,Name=SetNames[i],Description=$"Questreihe: {q.Name} · Level {q.Level}",ItemIds=Enumerable.Range(0,8).Select(n=>q.SetId+"_"+n).ToList(),Bonuses=Bonuses(i)}).ToList();
 private static List<RpgSetBonus> Bonuses(int i){
  var a=i switch{0=>new RpgAttributes{MaximumHpPercent=10,HealingPower=10},1=>new(){MagicPower=80,ManaRegeneration=15},2=>new(){Luck=10,ExtraLootChance=4},3=>new(){HealingPower=15,BossLifeleech=4},4=>new(){AttackSpeed=12,WaterDamage=15},5=>new(){Armor=40,BossDamageReduction=5},6=>new(){CritChance=8,BossDodgeChance=5},7=>new(){Power=20,Stamina=25},8=>new(){AbyssDamage=20,DoubleAttackChance=5},_=>new(){Damage=20,Luck=10,MaximumHpPercent=10}};
  return new(){new(){RequiredPieces=2,Description="Erste Verbundenheit: +5 Kraft",Attributes=new(){Power=5}},new(){RequiredPieces=4,Description="Gemeinsame Stärke: +10 Ausdauer",Attributes=new(){Stamina=10}},new(){RequiredPieces=6,Description="Legendäre Entschlossenheit: +10 % Schaden",Attributes=new(){Damage=10}},new(){RequiredPieces=8,Description="Vollständiges Questset: "+RpgChatPresentation.Bonuses(RpgChatPresentation.Snapshot(a)),Attributes=a}};
 }
 public static void AddMissing(RpgConfig c){foreach(var item in Items())if(!c.DiveLoot.Items.Any(i=>i.ItemId==item.ItemId))c.DiveLoot.Items.Add(item);foreach(var set in Sets())if(!c.Sets.Any(s=>s.Id==set.Id))c.Sets.Add(set);}
}
