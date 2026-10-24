using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static class RpgFishBiomes
{
 public static readonly string[][] Pools=[
  ["sardine","sea_cucumber","sunfin"],["perch","shrimp","rune_ray"],
  ["eel","cursefin"],["pufferfish","rainbow_kappa"],["tuna","tide_shark"],
  ["crystal-carp","prismatic_eel"],["vampire_squid","bloodfin"],["giant_shrimp","eternal_kappa"],
  ["golden_pufferfish","devourer_fish"],["leviathan","primordial_kingfish"]];
 public static void Apply(RpgConfig c)
 {
  if(c.Zones.FishBiomesVersion is <0 or >1)throw new InvalidOperationException("Unbekannte Fischbiom-Version.");
  if(c.Zones.FishBiomesVersion==1)return;
  var known=Pools.SelectMany(p=>p).ToHashSet(StringComparer.Ordinal);var defaults=RpgZoneConfig.Defaults();
  for(int n=0;n<defaults.Count;n++){
   var zone=c.Zones.Get(defaults[n].Id);
   zone.Fish=zone.Fish.Where(id=>!known.Contains(id)).Concat(Pools[n]).Distinct(StringComparer.Ordinal).ToArray();
   foreach(var id in Pools[n]){var fish=c.FishSpecies.SingleOrDefault(f=>f.Id==id);if(fish is not null)fish.Zone=zone.Id;}
  }
  c.Zones.FishBiomesVersion=1;
 }
}
