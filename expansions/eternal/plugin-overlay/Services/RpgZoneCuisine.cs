using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static class RpgZoneCuisine
{
 public static readonly string[] FishIds=["sunfin","rune_ray","cursefin","rainbow_kappa","tide_shark","prismatic_eel","bloodfin","eternal_kappa","devourer_fish","primordial_kingfish"];
 public static readonly string[] RecipeIds=["sunfin_sushi","rune_ray_maki","cursefin_nigiri","rainbow_kappa_roll","tide_shark_sashimi","prismatic_eel_sushi","bloodfin_nigiri","eternal_kappa_deluxe","devourer_sushi","primordial_kingfish_platter"];
 public static void AddMissing(RpgConfig c)
 {
  string[] fish=["Sonnenflosse","Runenrochen","Fluchflosse","Regenbogenkappa","Gezeitenhai","Prismatischer Aal","Blutflosse","Ewiger Kappa","Verschlingerfisch","Königsfisch der Urtiefe"];
  string[] names=["Sonnenflossen-Sushi","Runenrochen-Maki","Fluchflossen-Nigiri","Regenbogenkappa-Rolle","Gezeitenhai-Sashimi","Prismen-Aal-Sushi","Blutflossen-Nigiri","Ewiger-Kappa-Deluxe","Verschlingerfisch-Sushi","Königsfisch-Platte der Urtiefe"];
  RpgAttributes[] attributes=[new(),new(){CritChance=6},new(){AttackPercent=10},new(){Luck=12},new(){BossDamage=12},new(){BossDodgeChance=8},new(){BossLifeleech=8},new(){DiveLuck=15},new(){DoubleAttackChance=8},new(){Damage=20}];
  var zones=RpgZoneConfig.Defaults();
  for(int n=0;n<10;n++) {
   if(!c.FishSpecies.Any(f=>f.Id==FishIds[n]))c.FishSpecies.Add(new(){Id=FishIds[n],Name=fish[n],Zone=zones[n].Id,IconId="fish-"+FishIds[n],Weight=10,MinimumStake=0,PearlReward=40+(n*20),Rarity=n>=7?ItemRarity.Legendary:n>=3?ItemRarity.Epic:ItemRarity.Rare});
   if(!c.Recipes.Any(r=>r.Id==RecipeIds[n]))c.Recipes.Add(new(){Id=RecipeIds[n],DisplayName=names[n],FishId=FishIds[n],FishQuantity=1,PearlCost=0,Category=n is 0 or 5 or 6?SushiCategory.Defensive:SushiCategory.Offensive,Activity=n==7?RpgActivity.Dive:RpgActivity.Boss,AllActivities=true,Attributes=attributes[n],PassiveHealing=n==0?3:0});
   var zone=c.Zones.Get(zones[n].Id);if(!zone.Fish.Contains(FishIds[n]))zone.Fish=zone.Fish.Append(FishIds[n]).ToArray();
  }
 }
}
