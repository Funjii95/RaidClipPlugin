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
   return new{enabled=_config.Adventures.Rpg.AreaBosses.Enabled,bosses=_boss?.AreaState?.Bosses??_config.Adventures.Rpg.AreaBosses.Bosses,selectedBossId=personal is not null&&!state!.RewardsSettled?personal.BossId:a.Rpg.SelectedBossId,
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
   var joined=next.Participants.TryGetValue(user.UserId,out var participant);
   var definition=next.Bosses.SingleOrDefault(b=>b.Id==(participant?.BossId??p.SelectedBossId)&&b.Enabled);
   if(definition is null)return new(false,"Persönlicher Boss ist nicht verfügbar.");
   participant??=new(){UserId=user.UserId,Name=user.UserName,BossId=definition.Id};
   var hit=RpgPassiveService.BossAttack(_random.NextInclusive(10,30),p,cfg,_random,clock);
   critical=hit.Critical;
   var reduction=definition.Id=="boss_korallus"&&participant.Cycles<3||definition.Id=="boss_luminara"?20m:0;
   var damage=RpgDepthsCombatService.MitigateBoss(hit.Damage,reduction,hit.Critical,p,cfg);
   damage=Math.Max(0,damage-participant.PersonalShield);participant.PersonalShield=0;
   var available=(int)Math.Min(int.MaxValue,decimal.Ceiling(next.Remaining*definition.BaseHp));
   actual=RpgEternalCombat.Judgment(p,cfg)?available:Math.Min(available,damage);
   if(actual<=0&&!joined)return new(false,"Der Treffer hat keinen Schaden verursacht.");
   var contribution=Math.Min(next.Remaining,(decimal)actual/definition.BaseHp);
   next.Remaining=Math.Max(0,next.Remaining-contribution);
   if(next.Remaining==0){next.Active=false;next.Victory=true;}
   var direct=hit.Damage>0?(int)Math.Min(actual,(decimal)actual*hit.DirectDamage/hit.Damage):0;
   var soul=hit.Damage>0?(int)Math.Min(actual-direct,(decimal)actual*hit.SoulProcDamage/hit.Damage):0;
   var stats=CharacterStatService.Calculate(p,cfg,RpgActivity.Boss,now:clock);
   var leech=Math.Clamp(stats.BossLifeleech+(RpgDepthsCombatService.Has(p,cfg,"abyss_blood_dynasty",4)?10:0),0,100);
   RpgSetCombatService.Heal(p,cfg.Health,(int)(direct*leech/100),clock);
   RpgSushiService.HealFromBossAttack(direct,actual,p,cfg.Health,round.Id,clock,critical?stats.CriticalHealing:0);
   RpgEternalEightCombat.AfterDirect(p,cfg,direct,soul,critical,clock);RpgWeaponService.AfterHit(p,cfg,hit.Spell,actual,clock);RpgEternalCombat.AfterHit(p,cfg,direct,clock);
   if(!joined){p.Health.BossId=round.Id;p.Health.BossDamage=0;p.Health.Defeated=false;}
   participant.Cycles++;participant.Damage=checked(participant.Damage+actual);participant.Contribution+=contribution;
   if(next.Remaining>0&&cfg.Health.Counterattacks){
    void Counter(decimal raw,decimal armorIgnore=0,decimal reductionIgnore=0){
     if(p.Health.CurrentHp<=0)return;
     var counter=RpgBossDefenseService.Counter((int)Math.Min(int.MaxValue,decimal.Ceiling(raw)),p,cfg,_random,clock,armorIgnore,reductionIgnore);
     counter=RpgWeaponService.Counter(counter,p,hit.Spell,clock);counter=RpgEternalCombat.Receive(counter,p,cfg,round.Id,clock);counter=RpgEternalEightCombat.Receive(counter,p,cfg,round.Id,true,clock);
     var taken=Math.Min(p.Health.CurrentHp!.Value,counter);p.Health.CurrentHp-=taken;received+=taken;
    }
    var factor=definition.AttackFactor*(definition.Id=="boss_maledictus"&&participant.Cycles>1?1.1m:1);
    for(var n=0;n<definition.Hits;n++)Counter(definition.Attack*factor,definition.Id=="boss_eonath"&&n==definition.Hits-1?30:definition.ArmorIgnore,definition.ReductionIgnore);
    if(definition.Id=="boss_moderklaue"){
     if(participant.CurseIntervals>0){Counter(definition.Attack*.1m);participant.CurseIntervals--;}
     participant.CurseIntervals=3;
    }
    if(definition.Id=="boss_nharzul"&&received>0){
     var drain=Math.Min(p.Health.CurrentHp!.Value,(int)decimal.Floor(received*.1m));p.Health.CurrentHp-=drain;received+=drain;
     participant.PersonalShield=Math.Min(definition.BaseHp/100,drain);
    }
    RpgDepthsCombatService.AfterCounter(p,cfg,round.Id,clock);
   }
   p.Health.BossDamage=participant.Damage;p.Health.BossRemainingHp=AreaDisplayHp(next.Remaining);
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
    var stats=CharacterStatService.Calculate(a.Rpg,cfg,RpgActivity.Boss,now:_now());
    var collectors=RpgCollectorService.Calculate(a.Rpg,cfg.DiveLoot);
    var pearls=RpgCollectorService.Reward(boss.Pearls,stats.PearlBonus+collectors.BossPearls+RpgPerkCatalog.Bonus(a.Rpg,"bossPearls"));
    pearls=_points.RewardAmount(pearls,RpgRewardSource.Boss);a.Points+=Math.Min(pearls,Math.Max(0,Maximum-a.Points));
    RpgHealthService.Experience(a.Rpg,cfg.Health,(int)Math.Min(int.MaxValue,RpgCollectorService.Reward(boss.Experience,collectors.BossExperience)),_now());
    foreach(var material in boss.Materials)if(RpgUpgradeCatalog.Materials.Any(m=>m.Id==material))RpgUpgradeCatalog.Add(a.Rpg.Materials,material,_random.NextInclusive(1,3));
    if(_random.NextInclusive(0,999999)<boss.ItemChance*10000){
     var pool=cfg.DiveLoot.Items.Where(i=>i.Rarity.ToString()==boss.Rarity&&i.Slot.HasValue&&i.DropWeight>0).Where(i=>i.Slot!=EquipmentSlot.Weapon||boss.Level>=80||boss.Id=="boss_vortexion"&&i.ItemId=="weapon_tide_crown"||boss.Id=="boss_krysalith"&&i.ItemId=="weapon_leviathan_call").ToArray();
     if(boss.Rarity=="Eternal")pool=pool.Where(i=>!i.SetId.StartsWith("eternal_cosmic_forge")&&!i.SetId.StartsWith("eternal_soul_devourer")&&!i.SetId.StartsWith("eternal_ten_seas_treasure")).ToArray();
     if(pool.Length>0){var total=pool.Sum(i=>i.DropWeight);var roll=_random.NextInclusive(1,total);var definition=pool[^1];foreach(var candidate in pool){roll-=candidate.DropWeight;if(roll<=0){definition=candidate;break;}}var item=Copy(definition);item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=_now();a.Rpg.Inventory.Add(item);}
    }
    if(boss.Id=="boss_eonath")RpgEternalEightLoot.Victory(a.Rpg,cfg,boss.AreaId,boss.Level,"eonath",true,_random,_now(),boss.Id);
    RpgPerkCatalog.Victory(a.Rpg,_now());return new(true);
   },ct);
  }
  state.Active=false;state.Victory=victory;state.RewardsSettled=true;
  state.Log.Add(victory?"Globaler Boss besiegt. Persönliche Belohnungen wurden abgerechnet.":"Bossereignis beendet. Keine Siegesbelohnungen.");
  await SaveHealthBossAsync(null);_boss=null;_nextBoss=_now().AddSeconds(Math.Clamp(round.Config.RoundCooldownSeconds,1,86400));
  if(victory&&communityVictory)_points.HotTime.StartAfterBoss(state.Id,cfg.HotTime,true);
  await Say(state.Log.Last(),ct);DataChanged?.Invoke();
 }
}
