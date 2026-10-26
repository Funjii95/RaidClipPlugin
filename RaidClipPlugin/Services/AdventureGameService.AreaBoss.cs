using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed partial class AdventureGameService
{
 private bool _areaRecoveryChecked;
 private static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
 private static int AreaDisplayHp(decimal fraction)=>(int)decimal.Ceiling(Math.Clamp(fraction,0,1)*1000000);
 private async Task RecoverAreaBossAsync(CancellationToken ct)
 {
  if(_areaRecoveryChecked)return;
  var saved=await _points.ReadGlobalBossAsync(ct);_areaRecoveryChecked=true;
  if(saved is null||saved.RewardsSettled)return;
  if(!saved.Active&&!saved.Victory)return;
  _boss=new(){Id=saved.Id,Config=saved.EventConfig,End=saved.End,Hp=AreaDisplayHp(saved.Remaining),AreaState=saved};
  foreach(var p in saved.Participants)_boss.Players[p.Key]=p.Value.Name;
  _recoverHealthBoss=false;
 }
 internal async Task<RpgTransactionResult> SelectAreaBossAsync(string user,string name,string request,string bossId,CancellationToken ct)
 {
  await _gate.WaitAsync(ct);try{
   await RecoverAreaBossAsync(ct);
   if(!(_boss?.AreaState?.Bosses??_config.Adventures.Rpg.AreaBosses.Bosses).Any(b=>b.Id==bossId&&b.Enabled))return new(false,"Boss ist nicht verfügbar.",0);
   if(_boss?.AreaState?.Participants.ContainsKey(user)==true)return new(false,"Deine Bossauswahl ist für dieses Event bereits gebunden.",0);
   return await _points.ApplyRpgAsync(user,name,request,a=>{a.Rpg.SelectedBossId=bossId;return new(true);},ct);
  }finally{_gate.Release();}
 }
 internal async Task<object> ReadAreaBossAsync(string user,CancellationToken ct)
 {
  await _gate.WaitAsync(ct);try{
   await RecoverAreaBossAsync(ct);var a=(await _points.GetProfileAsync(user,ct)).Entry;
   var state=await _points.ReadGlobalBossAsync(ct);
   if(_boss?.AreaState is not null&&state?.Id==_boss.Id){_boss.AreaState=state;_boss.Players=state.Participants.ToDictionary(p=>p.Key,p=>p.Value.Name);}
   var personal=state?.Participants.GetValueOrDefault(user);
   var mimic=_boss?.AreaState?.EventConfig.IsMimic==true;var automatic=!mimic&&_config.Adventures.Rpg.BossAssignment.Automatic;
   var assigned=automatic?RpgCombatPowerService.Choose(a.Rpg,_config.Adventures.Rpg,_boss?.AreaState?.Bosses??_config.Adventures.Rpg.AreaBosses.Bosses,_now()).Id:a.Rpg.SelectedBossId;
   return new{automatic,enabled=mimic||_config.Adventures.Rpg.AreaBosses.Enabled,isMimic=mimic,bosses=RpgCombatDropRules.PreviewBosses(_boss?.AreaState?.Bosses??_config.Adventures.Rpg.AreaBosses.Bosses,mimic),selectedBossId=mimic?_boss!.AreaState!.Bosses[0].Id:personal is not null&&!state!.RewardsSettled?personal.BossId:assigned,
    locked=_boss?.AreaState?.Participants.ContainsKey(user)==true,remaining=state?.Remaining??1,active=_boss?.AreaState is not null,eventId=state?.Id,endsAt=state?.End,nextStart=_config.Adventures.Boss.AutoStartEnabled?_nextAutoBoss:(DateTimeOffset?)null,participants=state?.Participants.Count??0,personal,log=state?.Log??[],victory=state?.Victory??false,settled=state?.RewardsSettled??false};
  }finally{_gate.Release();}
 }
 internal async Task FinishAreaBossAdministrativeAsync(CancellationToken ct)
 {
  await _gate.WaitAsync(ct);try{
   await RecoverAreaBossAsync(ct);
   if(_boss?.AreaState is not {} state)throw new InvalidOperationException("Kein synchronisiertes Bossereignis aktiv.");
   var completed=Copy(state);completed.Remaining=0;completed.Active=false;completed.Victory=true;completed.Revision++;
   await _points.SaveGlobalBossAsync(completed,ct);_boss.AreaState=completed;_boss.Hp=0;
   await FinishAreaBossAsync(_boss,true,true,ct);
  }finally{PublishStatus();_gate.Release();}
 }
 private async Task<BossAttackResult> AttackAreaBossAsync(ChatMessage user,CancellationToken ct,bool communityVictory)
 {
  var round=_boss!;var state=await _points.ReadGlobalBossAsync(ct)??throw new InvalidOperationException("Bossereignis fehlt.");round.AreaState=state;var cfg=_config.Adventures.Rpg;var clock=_now();
  if(!state.Active||state.Remaining<=0||clock>=state.End)return new(false,user.UserId,0,false,round.Hp,false,0,"Das Bossereignis ist abgeschlossen.");
  var next=Copy(state);int actual=0,received=0;bool critical=false;
  var result=await _points.ApplyRpgAsync(user.UserId,user.UserName,"boss:"+round.Id+":"+user.Id,a=>{
   var p=a.Rpg;RpgHealthService.Refresh(p,cfg.Health,clock);
   if(p.Health.CurrentHp<=0||p.Health.BossId==round.Id&&p.Health.Defeated||next.Participants.GetValueOrDefault(user.UserId)?.Defeated==true)return new(false,"Für dieses Bossereignis bist du besiegt.");
   if(p.Cooldowns.TryGetValue("!attack",out var until)&&clock<until)return new(false,$"Angriff in {Math.Ceiling((until-clock).TotalSeconds)} Sekunden möglich.");
   if(_points.HasActivePartyCombatUnsafe(user.UserId,clock))return new(false,"Beende zuerst deinen Gruppenkampf.");
   var automatic=!next.EventConfig.IsMimic&&cfg.BossAssignment.Automatic;
   var joined=next.Participants.TryGetValue(user.UserId,out var participant);
   var definition=next.EventConfig.IsMimic?next.Bosses.SingleOrDefault(b=>b.Enabled):next.Bosses.SingleOrDefault(b=>b.Id==(participant?.BossId??(automatic?RpgCombatPowerService.Choose(p,cfg,next.Bosses,clock).Id:p.SelectedBossId))&&b.Enabled);
   if(definition is null)return new(false,"Persönlicher Boss ist nicht verfügbar.");
   participant??=new(){UserId=user.UserId,Name=user.UserName,BossId=definition.Id};
   if(automatic&&!participant.HasAssignment){participant.HasAssignment=true;participant.PersonalMaximumHp=RpgCombatPowerService.PersonalHp(p,cfg,definition,clock);participant.PersonalRemainingHp=participant.PersonalMaximumHp;participant.AssignedPower=RpgCombatPowerService.Evaluate(p,cfg,clock).Rating;participant.AttackScale=RpgCombatPowerService.AttackScale(p,cfg,definition,clock);}
   if(automatic&&participant.PersonalVictory)return new(false,"Dein persönlicher Boss ist bereits besiegt. Das globale Event läuft für andere Teilnehmer weiter.");
   var hit=RpgPassiveService.BossAttack(_random.NextInclusive(10,30),p,cfg,_random,clock);
   critical=hit.Critical;
   var reduction=!next.EventConfig.IsMimic&&(definition.Id=="boss_korallus"&&participant.Cycles<3||definition.Id=="boss_luminara")?20m:0;
   var damage=RpgDepthsCombatService.MitigateBoss(hit.Damage,reduction,hit.Critical,p,cfg);
   damage=Math.Max(0,damage-participant.PersonalShield);participant.PersonalShield=0;
   var available=automatic?participant.PersonalRemainingHp:(int)Math.Min(int.MaxValue,decimal.Ceiling(next.Remaining*definition.BaseHp));
   actual=RpgEternalCombat.Judgment(p,cfg)?available:Math.Min(available,damage);
   if(actual<=0&&!joined)return new(false,"Der Treffer hat keinen Schaden verursacht.");
   var denominator=automatic?Math.Max(participant.PersonalMaximumHp,RpgCombatPowerService.PersonalHp(p,cfg,RpgCombatPowerService.Choose(p,cfg,next.Bosses,clock),clock)):definition.BaseHp;
   var contribution=Math.Min(next.Remaining,(decimal)actual/denominator);
   if(automatic){participant.PersonalRemainingHp=Math.Max(0,participant.PersonalRemainingHp-actual);participant.PersonalVictory=participant.PersonalRemainingHp==0;}
   next.Remaining=Math.Max(0,next.Remaining-contribution);
   if(next.Remaining==0){next.Active=false;next.Victory=true;}
   RpgEarlyLegendaryEffects.AfterHit(actual,p,cfg,_random,clock);
   var direct=hit.Damage>0?(int)Math.Min(actual,(decimal)actual*hit.DirectDamage/hit.Damage):0;
   var soul=hit.Damage>0?(int)Math.Min(actual-direct,(decimal)actual*hit.SoulProcDamage/hit.Damage):0;
   var stats=CharacterStatService.Calculate(p,cfg,RpgActivity.Boss,now:clock);
   var leech=Math.Clamp(stats.BossLifeleech+(RpgDepthsCombatService.Has(p,cfg,"abyss_blood_dynasty",4)?10:0),0,100);
   RpgSetCombatService.Heal(p,cfg.Health,(int)(direct*leech/100),clock);
   RpgSushiService.HealFromBossAttack(direct,actual,p,cfg.Health,round.Id,clock,critical?stats.CriticalHealing:0);
   RpgEternalEightCombat.AfterDirect(p,cfg,direct,soul,critical,clock);RpgWeaponService.AfterHit(p,cfg,hit.Spell,actual,clock);RpgEternalCombat.AfterHit(p,cfg,direct,clock);
   if(!joined){p.Health.BossId=round.Id;p.Health.BossDamage=0;p.Health.Defeated=false;}
   if(!joined)RpgStatisticsService.Add(p,"bossParticipations");RpgStatisticsService.Hit(p,actual,critical);RpgStatisticsService.Add(p,"bossDamage",actual);
   participant.Cycles++;participant.Damage=checked(participant.Damage+actual);participant.Contribution+=contribution;
   if(next.Remaining>0&&(!automatic||!participant.PersonalVictory)&&cfg.Health.Counterattacks){
    void Counter(decimal raw,decimal armorIgnore=0,decimal reductionIgnore=0){
     if(p.Health.CurrentHp<=0)return;
     var counter=RpgBossDefenseService.Counter((int)Math.Min(int.MaxValue,decimal.Ceiling(raw)),p,cfg,_random,clock,armorIgnore,reductionIgnore);
     counter=RpgWeaponService.Counter(counter,p,hit.Spell,clock);counter=RpgEternalCombat.Receive(counter,p,cfg,round.Id,clock);counter=RpgEternalEightCombat.Receive(counter,p,cfg,round.Id,true,clock);counter=RpgEarlyLegendaryEffects.Receive(counter,p,cfg,round.Id,clock);
     var taken=Math.Min(p.Health.CurrentHp!.Value,counter);p.Health.CurrentHp-=taken;received+=taken;
    }
    var factor=definition.AttackFactor*(automatic?participant.AttackScale:1)*(!next.EventConfig.IsMimic&&definition.Id=="boss_maledictus"&&participant.Cycles>1?1.1m:1);
    for(var n=0;n<definition.Hits;n++)Counter(definition.Attack*factor,!next.EventConfig.IsMimic&&definition.Id=="boss_eonath"&&n==definition.Hits-1?30:definition.ArmorIgnore,definition.ReductionIgnore);
    if(!next.EventConfig.IsMimic&&definition.Id=="boss_moderklaue"){
     if(participant.CurseIntervals>0){Counter(definition.Attack*.1m);participant.CurseIntervals--;}
     participant.CurseIntervals=3;
    }
    if(!next.EventConfig.IsMimic&&definition.Id=="boss_nharzul"&&received>0){
     var drain=Math.Min(p.Health.CurrentHp!.Value,(int)decimal.Floor(received*.1m));p.Health.CurrentHp-=drain;received+=drain;
     participant.PersonalShield=Math.Min(definition.BaseHp/100,drain);
    }
    RpgDepthsCombatService.AfterCounter(p,cfg,round.Id,clock);
   }
   RpgStatisticsService.Add(p,"damageTaken",received);RpgStatisticsService.Maximum(p,"highestBossDamage",participant.Damage);if(p.Health.CurrentHp==0&&!participant.Defeated)RpgStatisticsService.Add(p,"bossDefeats");
   p.Health.BossDamage=participant.Damage;p.Health.BossRemainingHp=automatic?participant.PersonalRemainingHp:AreaDisplayHp(next.Remaining);
   participant.Defeated=p.Health.Defeated=p.Health.CurrentHp==0;
   next.Participants[user.UserId]=participant;next.Revision++;
   next.Log.Add($"{user.UserName} → {definition.Name}: {actual} Schaden{(critical?" · Krit":"")} · {received} Gegenschaden · {next.Remaining*100:0.0000} % HP{(participant.Defeated?" · besiegt":"")}");
   if(next.Log.Count>100)next.Log.RemoveRange(0,next.Log.Count-100);
   RpgPassiveService.ConsumeBuffs(p,RpgActivity.Boss,cfg,_random,clock);RpgSushiService.Participate(p,round.Id,round.End,clock);
   p.Cooldowns["!attack"]=clock.AddSeconds(RpgSetCombatService.AttackSeconds(p,cfg,RpgPerkCatalog.Cooldown(p,round.Config.AttackCooldownSeconds,"attackCooldown"),clock));
   return new(true);
  },ct,mutateGlobalBoss:current=>{
   if(current?.Id!=state.Id||current.Revision!=state.Revision||current.Remaining!=state.Remaining||!current.Active)throw new InvalidOperationException("Bossfortschritt wurde inzwischen geändert.");
   return next;
  });
  if(!result.Success)return new(false,user.UserId,0,false,round.Hp,false,0,result.Duplicate?"Angriff bereits verarbeitet.":result.Error);
  round.AreaState=next;round.Hp=AreaDisplayHp(next.Remaining);round.Players[user.UserId]=user.UserName;
  await Say(next.Log.Last(),ct);DataChanged?.Invoke();
  if(next.Remaining==0)await FinishAreaBossAsync(round,true,communityVictory,ct);
  return new(true,user.UserId,actual,critical,round.Hp,next.Remaining==0,0,"");
 }
 private async Task FinishAreaBossAsync(BossRound round,bool victory,bool communityVictory,CancellationToken ct)
 {
  var state=round.AreaState!;var cfg=_config.Adventures.Rpg;
  foreach(var participant in state.Participants.Values){
   var deletion=await _points.ReadPrivacyRequestAsync(participant.UserId,ct);if(deletion is not null)continue;
   await _points.ApplyRpgAsync(participant.UserId,participant.Name,"area-boss-reward:"+state.Id,a=>{
    RpgSushiService.Close(a.Rpg,state.Id,_now()<state.End?_now():state.End,_now(),cfg.Health);
    if(!victory||participant.Defeated||participant.Cycles<1||!RpgHealthService.Eligible(a.Rpg,state.Id))return new(true);
    var boss=state.Bosses.Single(b=>b.Id==participant.BossId);
    var rewardReceipt=new RpgCombatRewardService(a);var personalWin=state.EventConfig.IsMimic||!cfg.BossAssignment.Automatic||participant.PersonalVictory;
    if(!personalWin){var fraction=cfg.BossAssignment.ParticipationRewardFraction*Math.Clamp((decimal)participant.Damage/Math.Max(1,participant.PersonalMaximumHp),0,1);var partial=Math.Min((long)(boss.Pearls*fraction),Math.Max(0,Maximum-a.Points));a.Points+=partial;RpgStatisticsService.Add(a.Rpg,"pearlsEarned",partial);RpgHealthService.Experience(a.Rpg,cfg.Health,(int)(boss.Experience*fraction),_now());rewardReceipt.Complete("boss:"+state.Id,"participation",boss.Id,boss.Name,boss.AreaId,_now());return new(true);}
    var stats=CharacterStatService.Calculate(a.Rpg,cfg,RpgActivity.Boss,now:_now());
    var collectors=RpgCollectorService.Calculate(a.Rpg,cfg.DiveLoot);
    var pearls=RpgCollectorService.Reward(boss.Pearls,stats.PearlBonus+collectors.BossPearls+RpgPerkCatalog.Bonus(a.Rpg,"bossPearls"));
    pearls=_points.RewardAmount(pearls,RpgRewardSource.Boss);var granted=Math.Min(pearls,Math.Max(0,Maximum-a.Points));a.Points+=granted;RpgStatisticsService.Add(a.Rpg,"pearlsEarned",granted);if(state.EventConfig.IsMimic||participant.PersonalVictory||participant.Damage>=boss.BaseHp)RpgStatisticsService.BossKill(a.Rpg,boss.Id);
    RpgHealthService.Experience(a.Rpg,cfg.Health,(int)Math.Min(int.MaxValue,RpgCollectorService.Reward(boss.Experience,collectors.BossExperience)),_now());
    foreach(var material in boss.Materials)if(RpgUpgradeCatalog.Materials.Any(m=>m.Id==material))RpgUpgradeCatalog.Add(a.Rpg.Materials,material,_random.NextInclusive(1,3));
    if(state.EventConfig.IsMimic){var found=RpgRegionalMimics.Roll(cfg,boss,_random,_now());if(found is not null)a.Rpg.Inventory.Add(found);}
    else if(_random.NextInclusive(0,999999)<RpgCombatDropRules.Chance(boss.ItemChance)*10000){
     var pool=RpgLootLocations.BossPool(cfg,boss);
     if(pool.Length>0){var total=pool.Sum(i=>i.DropWeight);var roll=_random.NextInclusive(1,total);var definition=pool[^1];foreach(var candidate in pool){roll-=candidate.DropWeight;if(roll<=0){definition=candidate;break;}}var item=Copy(definition);item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=_now();a.Rpg.Inventory.Add(item);}
    }
    if(!state.EventConfig.IsMimic&&boss.Id=="boss_eonath")RpgEternalEightLoot.Victory(a.Rpg,cfg,boss.AreaId,boss.Level,"eonath",true,_random,_now(),boss.Id);
    if(state.EventConfig.IsMimic)RpgQuestService.Record(a.Rpg,"kills",boss.AreaId);
    RpgPerkCatalog.Victory(a.Rpg,_now());rewardReceipt.Complete("boss:"+state.Id,state.EventConfig.IsMimic?"mimic":"boss",boss.Id,boss.Name,boss.AreaId,_now());return new(true);
   },ct);
  }
  state.Active=false;state.Victory=victory;state.RewardsSettled=true;
  state.Log.Add(victory?(state.EventConfig.IsMimic?"Gemeinsame Mimic besiegt.":"Globaler Boss besiegt.")+" Persönliche Belohnungen wurden abgerechnet.":"Bossereignis beendet. Keine Siegesbelohnungen.");
  await SaveHealthBossAsync(null);_boss=null;_nextBoss=_now().AddSeconds(Math.Clamp(round.Config.RoundCooldownSeconds,1,86400));
  if(victory&&communityVictory)_points.HotTime.StartAfterBoss(state.Id,cfg.HotTime,true);
  if(victory)_points.PublishStory(new(state.EventConfig.IsMimic?GroqScene.Mimic:GroqScene.Boss,"boss-win:"+state.Id,"Kampf gewonnen"));
  await Say(state.Log.Last(),ct);DataChanged?.Invoke();
 }
}
