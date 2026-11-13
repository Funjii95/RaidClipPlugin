using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;

/// <summary>Authoritative tank effects. Called on transaction drafts, never by client input.</summary>
public static class RpgTankCombat
{
 public const decimal BlockCap=75;
 public static bool Has(RpgProfile p,RpgHealthConfig c,string set,int n)=>RpgSetCombatService.Active(p,c.EquipmentSets,"tank_"+set,n);
 public static bool Item(RpgProfile p,string id)=>p.Inventory.Any(i=>i.ItemId=="tank_"+id&&p.Equipment.Values.Contains(i.InstanceId));
 public static bool Active(RpgProfile p,string key,DateTimeOffset now)=>p.Cooldowns.GetValueOrDefault("tank:"+key)>now;
 static bool Ready(RpgProfile p,string key,int seconds,DateTimeOffset now){if(Active(p,key,now))return false;p.Cooldowns["tank:"+key]=now.AddSeconds(seconds);return true;}
 static void Buff(RpgProfile p,string key,int seconds,DateTimeOffset now)=>p.Cooldowns["tank:"+key]=now.AddSeconds(seconds);
 static decimal Stat(RpgProfile p,RpgHealthConfig c,string key)=>RpgSetCombatService.Gear(p,key)+RpgSetCombatService.Bonus(p,c.EquipmentSets,key);
 public static void Refresh(RpgProfile p,RpgHealthConfig c,DateTimeOffset now)
 {
  var h=p.Health;
  var loadout=string.Join("|",p.Inventory.Where(i=>i.ItemId.StartsWith("tank_")&&p.Equipment.Values.Contains(i.InstanceId)).Select(i=>i.ItemId).Distinct().OrderBy(id=>id));
  if(h.TankLoadout!=loadout){h.TankBarrierHp=0;foreach(var key in new[]{"avatar","sentinel","fortress","taunt","barrier-break","last-defense","shell"})p.Cooldowns.Remove("tank:"+key);h.TankLoadout=loadout;}
  if(h.TankAvatarHp>0&&(!Has(p,c,"leviathan",8)||!Active(p,"avatar",now))){h.CurrentHp=Math.Min(h.CurrentHp??0,Math.Max(1,RpgHealthService.Maximum(p,c)-h.TankAvatarHp));h.TankAvatarHp=0;}
  if(!p.Inventory.Any(i=>i.ItemId.StartsWith("tank_")&&p.Equipment.Values.Contains(i.InstanceId)))h.TankBarrierHp=0;
  if(!Item(p,"depth_chest"))h.TankHitCount=0;
 }
 public static decimal ReceivedHealing(RpgProfile p,RpgHealthConfig c,DateTimeOffset now)
 {
  var value=Stat(p,c,"ReceivedHealing");
  if(Has(p,c,"sentinel",6))value+=MissingStacks(p,c);
  if(Has(p,c,"sentinel",7)&&Active(p,"last-defense",now))value+=100;
  if(Has(p,c,"leviathan",8)&&Active(p,"avatar",now))value+=50;
  return Math.Clamp(value,0,200);
 }
 static int MissingStacks(RpgProfile p,RpgHealthConfig c)=>Math.Clamp((int)(10*(1-(decimal)(p.Health.CurrentHp??0)/Math.Max(1,RpgHealthService.Maximum(p,c)))),0,5);
 public static void Dynamic(RpgProfile p,RpgConfig c,RpgAttributes stats,DateTimeOffset now)
 {
  if(Item(p,"shell_chest")&&Active(p,"shell",now))stats.ArmorPercent+=3;
  if(Has(p,c.Health,"sentinel",6))stats.ArmorPercent+=2*MissingStacks(p,c.Health);
  if(Has(p,c.Health,"leviathan",6)&&Active(p,"barrier-break",now))stats.ArmorPercent+=15;
  if(Has(p,c.Health,"leviathan",8)&&Active(p,"avatar",now)){stats.ArmorPercent+=50;stats.BlockChance+=30;stats.BossLifeleech+=15;}
  if(Has(p,c.Health,"coral",8)&&Active(p,"fortress",now))stats.BlockChance+=50;
  stats.BlockChance=Math.Clamp(stats.BlockChance,0,BlockCap);
  stats.ReceivedHealing=ReceivedHealing(p,c.Health,now);
 }
 public static void Shield(RpgProfile p,RpgHealthConfig c,int raw,decimal cap,bool stack=false)
 {
  var max=RpgHealthService.Maximum(p,c);
  var amount=(int)Math.Min(int.MaxValue,raw*(1+Math.Clamp(Stat(p,c,"BarrierPower"),0,100)/100));
  p.Health.TankBarrierHp=Math.Max(p.Health.TankBarrierHp,(int)Math.Min(max*cap,stack?(long)p.Health.TankBarrierHp+amount:amount));
 }
 public static void Before(RpgProfile p,RpgConfig c,int raw,bool boss,DateTimeOffset now)
 {
  Refresh(p,c.Health,now);if(raw<=0||p.Health.CurrentHp is null or <=0)return;
  var max=RpgHealthService.Maximum(p,c.Health);
  if(Has(p,c.Health,"leviathan",4)&&Ready(p,"regen",5,now))RpgSetCombatService.Heal(p,c.Health,(int)(max*.02m),now);
  if(Has(p,c.Health,"leviathan",8)&&p.Health.CurrentHp<max*.25m&&Ready(p,"avatar-cd",90,now)){
   p.Health.TankAvatarHp=max/2;p.Health.CurrentHp+=p.Health.TankAvatarHp;Buff(p,"avatar",10,now);Buff(p,"taunt",10,now);
  }
  if(Has(p,c.Health,"sentinel",8)&&boss&&Ready(p,"sentinel-cd",40,now)){Buff(p,"sentinel",6,now);Buff(p,"taunt",6,now);}
  if(Has(p,c.Health,"coral",8)&&raw>=max*.2m&&Ready(p,"fortress-cd",30,now)){Buff(p,"fortress",5,now);Buff(p,"taunt",5,now);}
 }
 public static int Defend(int damage,RpgProfile p,RpgConfig c,IDuelRandom random,DateTimeOffset now,bool heavy)
 {
  if(damage<=0)return 0;var max=RpgHealthService.Maximum(p,c.Health);var stats=CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now);
  decimal factor=1;
  if(Has(p,c.Health,"coral",6)&&p.Health.CurrentHp>max*.7m)factor*=.92m;
  if(Has(p,c.Health,"coral",8)&&Active(p,"fortress",now))factor*=.6m;
  if(Has(p,c.Health,"sentinel",8)&&Active(p,"sentinel",now))factor*=.5m;
  if(Item(p,"depth_chest")){p.Health.TankHitCount=(p.Health.TankHitCount+1)%5;if(p.Health.TankHitCount==0)factor*=.75m;}
  var blocked=stats.BlockChance>0&&random.NextInclusive(0,9999)<Math.Clamp(stats.BlockChance,0,BlockCap)*100;
  if(blocked){factor*=.5m;if(Has(p,c.Health,"coral",4))Shield(p,c.Health,(int)(max*.03m),.15m);if(Has(p,c.Health,"sentinel",4))Shield(p,c.Health,(int)(max*.02m),.15m,true);}
  damage=(int)Math.Ceiling(damage*Math.Max(.2m,factor));
  if(heavy&&Has(p,c.Health,"leviathan",5))damage=Math.Min(damage,(int)Math.Ceiling(max*.35m));
  if(Item(p,"shell_chest"))Buff(p,"shell",3,now);
  if(Item(p,"reef_chest")&&p.Health.CurrentHp-damage<max*.4m&&Ready(p,"stand",30,now))Shield(p,c.Health,(int)(max*.08m),.15m);
  return Absorb(damage,p,c,now);
 }
 public static int Absorb(int damage,RpgProfile p,RpgConfig c,DateTimeOffset now)
 {
  var before=p.Health.TankBarrierHp;var absorbed=Math.Min(Math.Max(0,damage),before);p.Health.TankBarrierHp-=absorbed;
  if(before>0&&p.Health.TankBarrierHp==0&&Has(p,c.Health,"leviathan",6)){
   RpgSetCombatService.Heal(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.05m),now);Buff(p,"barrier-break",5,now);
  }
  return Math.Max(0,damage-absorbed);
 }
 public static int Lethal(int damage,RpgProfile p,RpgConfig c,DateTimeOffset now)
 {
  if(p.Health.CurrentHp>0&&damage>=p.Health.CurrentHp&&Has(p,c.Health,"sentinel",7)&&Ready(p,"rescue-cd",90,now)){
   Shield(p,c.Health,(int)(RpgHealthService.Maximum(p,c.Health)*.3m),.4m);Buff(p,"last-defense",5,now);return p.Health.CurrentHp.Value-1;
  }
  return damage;
 }
 // Max group reduction wins; only one living protector redirects each hit. No recursive Strike calls.
 internal static int Protect(string target,int damage,PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,DateTimeOffset now)
 {
  var allies=party.Members.Where(id=>id!=target&&get(id).Rpg.Health.CurrentHp>0).OrderBy(id=>id).ToArray();var p=get(target).Rpg;
  decimal reduction=0;
  foreach(var id in allies){var tank=get(id).Rpg;var max=RpgHealthService.Maximum(tank,c.Health);
   if(Has(tank,c.Health,"coral",7)&&p.Health.CurrentHp<RpgHealthService.Maximum(p,c.Health)*.3m&&Ready(tank,"ally:"+target,20,now))Shield(p,c.Health,(int)(max*.1m),.4m);
   if(Has(tank,c.Health,"coral",8)&&Active(tank,"fortress",now))reduction=Math.Max(reduction,.1m);
   if(Has(tank,c.Health,"sentinel",8)&&Active(tank,"sentinel",now)||Has(tank,c.Health,"leviathan",8)&&Active(tank,"avatar",now))reduction=.2m;
  }
  damage=(int)Math.Ceiling(damage*(1-reduction));
  var guardian=allies.FirstOrDefault(id=>Has(get(id).Rpg,c.Health,"leviathan",7)&&p.Health.CurrentHp<RpgHealthService.Maximum(p,c.Health)*.4m);
  if(guardian is not null){var tank=get(guardian).Rpg;var redirected=damage/5;var taken=Lethal(Absorb((redirected+1)/2,tank,c,now),tank,c,now);RpgStatisticsService.Add(tank,"damageTaken",Math.Min(taken,tank.Health.CurrentHp!.Value));tank.Health.CurrentHp=Math.Max(0,tank.Health.CurrentHp!.Value-taken);damage-=redirected;RpgPartyCombatService.Log(party.Instance!,guardian,target,"tank-redirect",redirected);}
  else {var sentinel=allies.FirstOrDefault(id=>Has(get(id).Rpg,c.Health,"sentinel",8)&&Active(get(id).Rpg,"sentinel",now)&&get(id).Rpg.Health.TankBarrierHp>0);if(sentinel is not null){var tank=get(sentinel).Rpg;var diverted=Math.Min(damage/10,tank.Health.TankBarrierHp);Absorb(diverted,tank,c,now);damage-=diverted;}}
  return Absorb(damage,p,c,now);
 }
}
