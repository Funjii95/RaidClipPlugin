using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed class RpgPartyCombatService(ViewerPointStore points,MinigameConfig settings,IDuelRandom? random=null,Func<DateTimeOffset>? clock=null)
{
 private RpgConfig C=>settings.Adventures.Rpg;private IDuelRandom Rng=>random??new CryptoDuelRandom();private DateTimeOffset Now=>clock?.Invoke()??DateTimeOffset.UtcNow;
 internal static void Log(PartyInstance i,string actor,string target,string action,int amount=0){i.Log.Add(new(){Turn=i.Turn,ActorId=actor,TargetId=target,Action=action,Amount=Math.Max(0,amount)});if(i.Log.Count>60)i.Log.RemoveRange(0,i.Log.Count-60);}
 internal static void AddThreat(PartyInstance i,string actor,decimal value)=>i.Threat[actor]=Math.Min(RpgStatistics.Maximum,i.Threat.GetValueOrDefault(actor)+Math.Max(0,value));
 internal Task<RpgTransactionResult> ExecuteAsync(string actor,string name,string request,string operation,string zoneId,string dungeonId,string difficulty,string target,CancellationToken ct,string? expectedInstance=null,long? expectedRevision=null)=>points.ApplyPartyAsync(actor,name,request,(world,get)=>{
  var c=C;var now=Now;if(!settings.Enabled||!settings.PointsEnabled||!c.Enabled||!c.Multiplayer.Enabled)return new(false,"Gemeinsame Abenteuer sind nicht freigegeben.");var self=get(actor);var party=world.For(actor);
  if(expectedInstance is not null){if(party?.Instance is not {Status:"active",AutomaticCombat:true} automatic||automatic.Id!=expectedInstance||party.Revision!=expectedRevision||automatic.NextAutomaticAt>now)return new(false,"Automatischer Kampfstand hat sich geändert.");automatic.NextAutomaticAt=now.AddSeconds(2);}
  if(operation=="recover"){
   var camp=party?.Instance is {Status:"active",WaveCleared:true} campInstance&&campInstance.ExpiresAt>now;
   if(points.HasActivePartyCombatUnsafe(actor,now)&&!camp||points.HasGlobalBossParticipantsUnsafe([actor]))return new(false,"Ausruhen ist erst nach dem Kampf möglich.");
   if(camp){foreach(var member in party!.Members)get(member);var instance=party!.Instance!;if(instance.Cooldowns.GetValueOrDefault(actor+":camp")>instance.Stage)return new(false,"An diesem Lager hast du dich bereits erholt.");instance.Cooldowns[actor+":camp"]=instance.Stage+1;party.Revision++;}
   if(!camp&&self.Rpg.Cooldowns.GetValueOrDefault("rpg:recover")>now)return new(false,"Du kannst dich einmal pro Minute ausruhen.");
   RpgHealthService.Refresh(self.Rpg,c.Health,now);var hp=RpgHealthService.Maximum(self.Rpg,c.Health);var restored=Math.Min(hp,self.Rpg.Health.CurrentHp!.Value+(int)(hp*c.Dungeons.RecoveryPercent/100m));var healed=restored-self.Rpg.Health.CurrentHp.Value;self.Rpg.Health.CurrentHp=restored;RpgStatisticsService.Add(self.Rpg,"healing",healed);self.Rpg.Cooldowns["rpg:recover"]=now.AddSeconds(c.Dungeons.RecoveryCooldownSeconds);return new(true);
  }
  if(operation=="startDungeon"&&party is null){if(!c.Dungeons.Enabled)return new(false,"Dungeons sind nicht freigegeben.");party=new(){Id=Guid.NewGuid().ToString("N"),LeaderId=actor,Members=[actor],CreatedAt=now};world.Parties[party.Id]=party;}
  if(party is null)return new(false,"Erstelle zuerst eine Gruppe.");foreach(var id in party.Members){var p=get(id).Rpg;RpgHealthService.Refresh(p,c.Health,now);}
  if(operation is "startDive" or "startBoss" or "startDungeon"){
   if(party.Members.Any(id=>get(id).Rpg.DiveSeries is {Status:"active"}))return new(false,"Beendet zuerst die laufenden Tauchserien.");
   if(party.LeaderId!=actor)return new(false,"Nur die Gruppenleitung startet ein Abenteuer.");
   if(party.Instance is {Status:"active"} active&&active.ExpiresAt>now)return new(false,"Es läuft bereits eine gemeinsame Instanz.");
   if(points.HasGlobalBossParticipantsUnsafe(party.Members))return new(false,"Beendet zuerst eure Teilnahme am globalen Bossereignis.");
   if(party.Members.Any(id=>get(id).Rpg.Health.CurrentHp<=0))return new(false,"Alle Mitglieder müssen vor dem Start leben. Außerhalb des Kampfes ist Ausruhen möglich.");
   RpgDungeonDefinition? dungeon=null;RpgAreaBossDefinition? boss=null;
   if(operation=="startDungeon"){
    if(!c.Dungeons.Enabled)return new(false,"Dungeons sind nicht freigegeben.");dungeon=RpgDungeonConfig.Definitions.SingleOrDefault(d=>d.Id==dungeonId);if(dungeon is null||!c.Dungeons.HpMultipliers.ContainsKey(difficulty))return new(false,"Unbekannter Dungeon oder Schwierigkeitsgrad.");
    var min=RpgDungeonConfig.Minimum(dungeon,difficulty);if(party.Members.Any(id=>get(id).Rpg.PlayerLevel<min))return new(false,$"Alle Mitglieder benötigen Stufe {min}.");zoneId=dungeon.ZoneId;
   }else if(operation=="startBoss"){
    var middle=party.Members.OrderBy(id=>RpgCombatPowerService.Evaluate(get(id).Rpg,c,now).Rating).ElementAt(party.Members.Count/2);boss=RpgCombatPowerService.Choose(get(middle).Rpg,c,c.AreaBosses.Bosses,now);zoneId=boss.AreaId;difficulty="normal";
   }else difficulty="normal";
   var zone=c.Zones.Get(zoneId);var hits=party.Members.ToDictionary(id=>id,id=>RpgCombatPowerService.Evaluate(get(id).Rpg,c,now).ExpectedHit);var weakest=hits.Values.Min();
   var instance=new PartyInstance{Id=Guid.NewGuid().ToString("N"),Kind=dungeon is not null?"dungeon":boss is not null?"boss":"dive",DungeonId=dungeon?.Id??"",Difficulty=difficulty,ZoneId=zone.Id,ExpiresAt=now.AddMinutes(c.Multiplayer.InstanceMinutes)};
   foreach(var id in party.Members){instance.Threat[id]=0;instance.DamageScale[id]=Math.Min(1,weakest*4/Math.Max(1,hits[id]));}
   instance.AutomaticCombat=instance.Kind=="dungeon";instance.NextAutomaticAt=now.AddSeconds(2);party.Instance=instance;InitializeEnemy(party,get,c,boss,now);party.Revision++;
   if(instance.Kind=="dive")foreach(var id in party.Members)RpgStatisticsService.Add(get(id).Rpg,"groupDives");
   Log(instance,actor,"",instance.Kind+"-start");return new(true);
  }
  if(party.Instance is not {} i)return new(false,"Keine gemeinsame Instanz aktiv.");
  if(i.Status!="active")return new(false,"Diese Instanz ist bereits abgeschlossen.");
  if(i.ExpiresAt<=now){i.Status="expired";Log(i,actor,"","expired");party.Revision++;return new(true);}
  if(operation is "pauseAutomatic" or "resumeAutomatic"){if(party.LeaderId!=actor)return new(false,"Nur die Gruppenleitung steuert den Autokampf.");i.AutomaticCombat=operation=="resumeAutomatic";i.NextAutomaticAt=now.AddSeconds(2);party.Revision++;Log(i,actor,"",i.AutomaticCombat?"automatic-resumed":"automatic-paused");return new(true);}
  if(operation=="abandon"){i.Status="aborted";Log(i,actor,"","aborted");party.Revision++;return new(true);}
  if(operation=="advance"){
   if(party.LeaderId!=actor||!i.WaveCleared||i.Kind!="dungeon")return new(false,"Die nächste Begegnung ist noch nicht verfügbar.");
   if(party.Members.All(id=>get(id).Rpg.Health.CurrentHp<=0)){i.Status="lost";return new(true);}i.Stage++;InitializeEnemy(party,get,c,null,now);party.Revision++;return new(true);
  }
  if(i.WaveCleared)return new(false,"Die Gruppenleitung muss die nächste Begegnung öffnen.");
  if(self.Rpg.Health.CurrentHp<=0)return new(false,"Besiegte Mitglieder können nicht angreifen oder heilen.");
  if(self.Rpg.Cooldowns.GetValueOrDefault("group:action")>now)return new(false,"Deine nächste Gruppenaktion ist gleich bereit.");
  var ownHealthBefore=self.Rpg.Health.CurrentHp??0;bool critical=false;RpgWeaponSpell? turnSpell=null;var cooldownKey=actor+":"+operation;
  if(operation!="attack"&&i.Cooldowns.GetValueOrDefault(cooldownKey)>i.Turn)return new(false,"Diese Gruppenfähigkeit lädt noch auf.");
  if(operation=="attack"){
   var hit=RpgPassiveService.BossAttack(Rng.NextInclusive(10,30),self.Rpg,c,Rng,now);critical=hit.Critical;turnSpell=hit.Spell;var bonus=i.RallyUntilTurn>i.Turn?1.1m:1;var combo=i.LastActorId.Length>0&&i.LastActorId!=actor?1.1m:1;
   var raw=(int)Math.Min(int.MaxValue,decimal.Floor(hit.Damage*i.DamageScale.GetValueOrDefault(actor,1)*bonus*combo*100/(100+i.Armor)));
   var damage=RpgEternalCombat.Judgment(self.Rpg,c)?(i.AddsHp>0?i.AddsHp:i.Hp):Math.Max(1,raw);var actual=Math.Min(i.AddsHp>0?i.AddsHp:i.Hp,damage);
   if(i.AddsHp>0)i.AddsHp-=actual;else i.Hp-=actual;
   RpgStatisticsService.Hit(self.Rpg,actual,critical);AddThreat(i,actor,RpgRoleService.Threat(self.Rpg,c,actual,0));Log(i,actor,i.EnemyId,critical?"critical-hit":"hit",actual);
   var direct=hit.Damage>0?(int)Math.Min(actual,(decimal)actual*hit.DirectDamage/hit.Damage):0;var soul=hit.Damage>0?(int)Math.Min(actual-direct,(decimal)actual*hit.SoulProcDamage/hit.Damage):0;
   var stats=CharacterStatService.Calculate(self.Rpg,c,RpgActivity.Boss,now:now);RpgSetCombatService.Heal(self.Rpg,c.Health,(int)(direct*Math.Clamp(stats.BossLifeleech,0,100)/100),now);
   RpgEarlyLegendaryEffects.AfterHit(actual,self.Rpg,c,Rng,now);RpgEternalEightCombat.AfterDirect(self.Rpg,c,direct,soul,critical,now);RpgWeaponService.AfterHit(self.Rpg,c,hit.Spell,actual,now);RpgEternalCombat.AfterHit(self.Rpg,c,direct,now);i.LastActorId=actor;
  }else if(operation=="heal"){
   if(!RpgGroupHealing.CanHeal(self.Rpg,c))return new(false,"Dafür benötigst du die Heilerrolle oder ein passendes Heiler-Set.");
   var recipient=target=="auto"?RpgGroupHealing.Lowest(party,get,c):target;if(recipient is null||!party.Members.Contains(recipient)||get(recipient).Rpg.Health.CurrentHp<=0)return new(false,"Heilziel muss ein lebendes Gruppenmitglied sein.");
   var raw=(int)(RpgHealthService.Maximum(self.Rpg,c.Health)*c.Multiplayer.GroupHealMaxHpPercent/100);RpgGroupHealing.Heal(actor,recipient,raw,i,get,c,Rng,now);
   var extraTargets=RpgGroupHealing.Has(self.Rpg,c,"eternal_tide_blessing",6)?3:RpgGroupHealing.Has(self.Rpg,c,"coral_oracle",4)||RpgGroupHealing.Has(self.Rpg,c,"eternal_tide_blessing",4)?1:0;
   foreach(var other in party.Members.Where(id=>id!=recipient&&get(id).Rpg.Health.CurrentHp>0).OrderBy(id=>(decimal)get(id).Rpg.Health.CurrentHp!.Value/RpgHealthService.Maximum(get(id).Rpg,c.Health)).Take(extraTargets))RpgGroupHealing.Heal(actor,other,raw/2,i,get,c,Rng,now);
  }else if(operation=="guard"){
   if(self.Rpg.Role!=RpgRole.Tank)return new(false,"Diese Schutzfähigkeit gehört zur Tankrolle.");var recipient=target=="auto"?RpgGroupHealing.Lowest(party,get,c,actor)??actor:target;
   if(!party.Members.Contains(recipient)||get(recipient).Rpg.Health.CurrentHp<=0)return new(false,"Schutzziel muss ein lebendes Gruppenmitglied sein.");
   var shield=(int)(RpgHealthService.Maximum(get(recipient).Rpg,c.Health)*c.Multiplayer.ShieldMaxHpPercent/100);i.Shields[recipient]=Math.Max(i.Shields.GetValueOrDefault(recipient),shield);if(recipient!=actor)i.Shields[actor]=Math.Max(i.Shields.GetValueOrDefault(actor),shield/2);AddThreat(i,actor,Math.Max(1,i.Threat.Values.DefaultIfEmpty().Max()+shield*c.Roles.TankThreatMultiplier));Log(i,actor,recipient,"guard",shield);
  }else if(operation=="rally"){
   if(self.Rpg.Role!=RpgRole.Damage)return new(false,"Der Angriffsruf gehört zur Schadensrolle.");i.RallyUntilTurn=i.Turn+party.Members.Count+1;AddThreat(i,actor,1);Log(i,actor,"","rally");
  }else if(operation=="interrupt"){
   if(i.PendingAbility.Length==0)return new(false,"Der Gegner bereitet gerade keine unterbrechbare Fähigkeit vor.");i.PendingAbility="";i.PendingTurn=0;AddThreat(i,actor,1);Log(i,actor,i.EnemyId,"interrupt");
  }else return new(false,"Unbekannte Kampfaktion.");
  if(operation!="attack")i.Cooldowns[cooldownKey]=i.Turn+c.Multiplayer.SkillCooldownTurns;
  self.Rpg.Cooldowns["group:action"]=now.AddSeconds(1);i.Turn++;party.Revision++;
  RpgGroupHealing.AfterTurn(actor,Math.Max(0,(self.Rpg.Health.CurrentHp??0)-ownHealthBefore),critical,party,get,c,Rng,now);
  if(i.Hp<=0){if(i.Kind=="dungeon"&&i.Stage<6){i.WaveCleared=true;i.AddsHp=0;i.PendingAbility="";Log(i,actor,i.EnemyId,"wave-cleared");}else{Reward(party,get,c,now);i.Status="won";Log(i,actor,i.EnemyId,"victory");}return new(true);}
  EnemyTurn(party,get,c,now,actor,turnSpell);
  if(party.Members.All(id=>get(id).Rpg.Health.CurrentHp<=0)){i.Status="lost";Log(i,actor,"","wipe");}
  return new(true);
 },ct);
 private void InitializeEnemy(PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,RpgAreaBossDefinition? boss,DateTimeOffset now)
 {
  var i=party.Instance!;var z=c.Zones.Get(i.ZoneId);var hit=party.Members.Sum(id=>RpgCombatPowerService.Evaluate(get(id).Rpg,c,now).ExpectedHit*i.DamageScale.GetValueOrDefault(id,1));
  var stage=i.Kind=="dungeon"?new[]{1m,1.4m,2m,1.3m,2.4m,1.5m,3m}[Math.Min(i.Stage,6)]:1;
  i.EnemyId=boss?.Id??(i.Kind=="dungeon"&&i.Stage==6?RpgDungeonConfig.Definitions.Single(d=>d.Id==i.DungeonId).BossId:z.Id+"_"+i.Stage);
  i.EnemyName=boss?.Name??(i.Kind=="dungeon"&&i.Stage==6?c.AreaBosses.Bosses.Single(b=>b.Id==i.EnemyId).Name:z.Enemies[i.Stage%3]+(i.Kind=="dungeon"&&i.Stage is 2 or 4?" (Zwischenboss)":i.Kind=="dungeon"&&i.Stage is 1 or 5?" (Elite)":""));
  i.MaximumHp=(int)Math.Clamp(decimal.Ceiling(Math.Max(z.Hp,hit/party.Members.Count*(i.Kind=="dive"?2:6))*(1+(party.Members.Count-1)*c.Multiplayer.GroupHpPerExtraMember)*stage*(i.Kind=="dungeon"?c.Dungeons.HpMultipliers[i.Difficulty]:1)),1,int.MaxValue);i.Hp=i.MaximumHp;
  // Counterattack budget is based on the lowest living effective HP, so a stronger member does not make a weaker one unplayable.
  var minimumHp=party.Members.Min(id=>RpgHealthService.Maximum(get(id).Rpg,c.Health));var baseAttack=Math.Min(boss?.Attack??z.Attack,Math.Max(1,minimumHp/24));i.Attack=(int)Math.Clamp(decimal.Ceiling(baseAttack*(1+(party.Members.Count-1)*c.Multiplayer.GroupAttackPerExtraMember)*(i.Kind=="dungeon"?c.Dungeons.AttackMultipliers[i.Difficulty]:1)),1,1000000);i.Armor=z.Armor;
  foreach(var id in party.Members){i.Threat[id]=0;get(id).Rpg.Cooldowns.Remove("tank:taunt");}
  i.WaveCleared=false;i.AddsHp=0;i.PendingAbility="";i.PendingTurn=0;i.Enraged=false;i.LastActorId="";Log(i,"",i.EnemyId,"encounter");
 }
 private void EnemyTurn(PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,DateTimeOffset now,string actor,RpgWeaponSpell? spell)
 {
  var i=party.Instance!;var alive=party.Members.Where(id=>get(id).Rpg.Health.CurrentHp>0).ToArray();if(alive.Length==0)return;
  foreach(var member in alive)RpgTankCombat.Refresh(get(member).Rpg,c.Health,now);
  var focus=alive.OrderByDescending(id=>RpgTankCombat.Active(get(id).Rpg,"taunt",now)).ThenByDescending(id=>i.Threat.GetValueOrDefault(id)).ThenBy(id=>id).First();
  void Strike(string id,decimal multiplier=1,decimal ignore=0,decimal reductionIgnore=0){var p=get(id).Rpg;if(p.Health.CurrentHp<=0)return;var raw=(int)Math.Min(int.MaxValue,i.Attack*multiplier*(i.Enraged?1.5m:1));var damage=RpgBossDefenseService.Counter(raw,p,c,Rng,now,ignore,reductionIgnore,i.Kind!="dive");damage=RpgTankCombat.Protect(id,damage,party,get,c,now);damage=RpgWeaponService.Counter(damage,p,id==actor?spell:null,now);var absorb=Math.Min(i.Shields.GetValueOrDefault(id),damage);i.Shields[id]=Math.Max(0,i.Shields.GetValueOrDefault(id)-absorb);damage-=absorb;RpgStatisticsService.Add(p,"damageBlocked",absorb);damage=RpgEternalCombat.Receive(damage,p,c,i.Id,now);damage=RpgEternalEightCombat.Receive(damage,p,c,i.Id,i.Kind!="dive",now);damage=RpgEarlyLegendaryEffects.Receive(damage,p,c,i.Id,now);damage=RpgTankCombat.Lethal(damage,p,c,now);var taken=Math.Min(p.Health.CurrentHp!.Value,damage);p.Health.CurrentHp-=taken;RpgStatisticsService.Add(p,"damageTaken",taken);Log(i,i.EnemyId,id,"counter",taken);if(p.Health.CurrentHp==0&&i.Cooldowns.GetValueOrDefault("dead:"+id)==0){RpgStatisticsService.Add(p,"lostFights");i.Cooldowns["dead:"+id]=1;}}
  var ability=i.PendingAbility;
  if(ability.Length>0&&i.PendingTurn<=i.Turn){i.PendingAbility="";i.PendingTurn=0;if(ability=="healing"){var healed=Math.Min(i.MaximumHp-i.Hp,(int)(i.MaximumHp*.08m));i.Hp+=healed;Log(i,i.EnemyId,i.EnemyId,"enemy-heal",healed);}else if(ability=="adds"){i.AddsHp=(int)(i.MaximumHp*.15m);Log(i,i.EnemyId,"","summon",i.AddsHp);}else if(ability=="cleave"){foreach(var id in alive)Strike(id,.6m);}else if(ability=="tankbuster")Strike(focus,1.8m,20);else if(ability=="enrage"){i.Enraged=true;Log(i,i.EnemyId,"","enrage");}}
  if(i.Kind=="boss"){var boss=c.AreaBosses.Bosses.Single(b=>b.Id==i.EnemyId);for(var n=0;n<boss.Hits;n++)Strike(focus,boss.AttackFactor,boss.ArmorIgnore,boss.ReductionIgnore);}else Strike(focus);if(i.AddsHp>0)Strike(focus,.3m);
  if(i.Kind=="dungeon"&&i.Turn%4==3){i.PendingAbility=i.Stage switch{0=>"cleave",1=>"adds",2=>"tankbuster",3=>"healing",4=>"enrage",5=>"cleave",_=>i.Hp<i.MaximumHp/2?"adds":"cleave"};i.PendingTurn=i.Turn+1;Log(i,i.EnemyId,"","preparing-"+i.PendingAbility);}
  if(i.Kind=="dungeon"&&i.Stage==6&&i.Hp<i.MaximumHp*.3m)i.Enraged=true;
 }
 private void Reward(PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,DateTimeOffset now)
 {
  var i=party.Instance!;var zone=c.Zones.Get(i.ZoneId);foreach(var id in party.Members){var a=get(id);var p=a.Rpg;if(i.Rewarded.Contains(id)||p.Health.CurrentHp<=0||i.Threat.GetValueOrDefault(id)<=0)continue;
   var rewardReceipt=new RpgCombatRewardService(a);var eligible=p.PlayerLevel+c.Multiplayer.MaximumLevelSpreadForRewards>=zone.Level;var rewardLevel=Math.Min(zone.Level,p.PlayerLevel+c.Multiplayer.MaximumLevelSpreadForRewards);var multiplier=i.Kind=="dungeon"?c.Dungeons.RewardMultipliers[i.Difficulty]*3:1;
   var amount=(long)(rewardLevel*20*multiplier);if(settings.MaximumAccountEnabled)amount=Math.Min(amount,Math.Max(0,settings.MaximumAccountPoints-a.Points));a.Points=checked(a.Points+amount);RpgStatisticsService.Add(p,"pearlsEarned",amount);RpgHealthService.Experience(p,c.Health,i.Kind=="dungeon"?c.Dungeons.CompletionExperience(p.PlayerLevel,c.Health.ExperiencePerLevel,i.Difficulty):(int)(rewardLevel*20*multiplier),now);
   var previous=p.Inventory.Select(x=>x.InstanceId).ToHashSet();
   if(eligible){var materials=RpgZoneService.Materials(p,c,zone,"enemy",Rng);RpgStatisticsService.Zone(p,zone.Id,"materials",materials.Values.Sum());RpgItem? item=null;
    if(i.Kind=="dungeon"){
     var chance=RpgCombatDropRules.Chance(c.Dungeons.LegendaryChances[i.Difficulty]);if(Rng.NextInclusive(0,999999)<chance*10000){var pool=c.DiveLoot.Items.Where(x=>x.Rarity==ItemRarity.Legendary&&x.MinimumLevel<=p.PlayerLevel&&x.Slot.HasValue&&(x.DropWeight>0||x.ItemId.StartsWith("starter_"))&&!x.SetId.StartsWith("quest_")).ToArray();if(pool.Length>0){item=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(pool[Rng.NextInclusive(0,pool.Length-1)]))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;RpgItemGenerationService.Generate(item,c.DiveLoot,Rng);}}
     var material=i.DungeonId=="runen_halls"?"dungeon_rune_shard":i.DungeonId=="coral_labyrinth"?"dungeon_coral_heart":i.Difficulty=="mythic"?"dungeon_void_sigil":"abyss_shard";RpgUpgradeCatalog.Add(p.Materials,material,i.Difficulty=="mythic"?3:i.Difficulty=="heroic"?2:1);
    }else if(i.Kind=="boss"){
     var boss=c.AreaBosses.Bosses.Single(b=>b.Id==i.EnemyId);var pool=RpgLootLocations.BossPool(c,boss);if(pool.Length>0&&Rng.NextInclusive(0,999999)<RpgCombatDropRules.Chance(boss.ItemChance)*10000){item=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(pool[Rng.NextInclusive(0,pool.Length-1)]))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;RpgItemGenerationService.Generate(item,c.DiveLoot,Rng);}
    }else{item=RpgZoneService.Equipment(p,c,zone,Rng,now);var early=RpgEarlyLegendaryCatalog.Roll(p,c,zone,Rng,now);if(early is not null)p.Inventory.Add(early);}
    if(i.Kind=="dungeon"&&i.Difficulty=="mythic"&&p.PlayerLevel>=80&&Rng.NextInclusive(0,99)<25){var tanks=c.DiveLoot.Items.Where(x=>(x.SetId=="tank_sentinel"||x.SetId=="tank_leviathan")&&x.MinimumLevel<=p.PlayerLevel).ToArray();if(tanks.Length>0)p.Inventory.Add(RpgZoneService.Instance(tanks[Rng.NextInclusive(0,tanks.Length-1)],c,Rng,now));}
   if(item is not null)p.Inventory.Add(item);
   }
   RpgStatisticsService.Add(p,"enemyKills");RpgStatisticsService.Zone(p,zone.Id,"kills");if(i.Kind=="boss"){RpgStatisticsService.Add(p,"groupBossKills");RpgStatisticsService.BossKill(p,i.EnemyId);}if(i.Kind=="dungeon")p.Statistics.Dungeons[i.Difficulty]=Math.Min(SnapshotProtocol.RpgStatistics.Maximum,p.Statistics.Dungeons.GetValueOrDefault(i.Difficulty)+1);
   var legendary=p.Inventory.Count(x=>!previous.Contains(x.InstanceId)&&x.Rarity==ItemRarity.Legendary);RpgStatisticsService.Add(p,"legendaryFinds",legendary);RpgStatisticsService.Zone(p,zone.Id,"legendary",legendary);rewardReceipt.Complete("party:"+i.Id,i.Kind=="dungeon"?"dungeon":i.Kind=="boss"?"groupBoss":"groupDive",i.EnemyId,i.EnemyName,i.ZoneId,now);i.Rewarded.Add(id);Log(i,id,"","reward",(int)Math.Min(int.MaxValue,amount));
  }
 }
}
