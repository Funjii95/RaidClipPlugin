using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record RpgAdventureResult(bool Success,string PlayerId,long Stake,long Payout,long Balance,string Error,string Outcome,RpgItem? Item,int RemainingCooldown,IReadOnlyList<string> AppliedBuffs)
{
 public IReadOnlyList<RpgItem> AdditionalItems {get;init;}=[];public RpgEnemyResult? Enemy {get;init;}
 public IReadOnlyDictionary<string,int> MaterialRewards {get;init;}=new Dictionary<string,int>();public int ShellsAwarded {get;init;}public bool BossDiscovered {get;init;}
 public string ZoneId {get;init;}="";public string ZoneName {get;init;}="";public string EventType {get;init;}="";
}
public sealed class RpgAdventureService(ViewerPointStore points,MinigameConfig config,IDuelRandom? random=null,Func<DateTimeOffset>? now=null)
{
 private readonly IDuelRandom rng=random??new CryptoDuelRandom();private readonly Func<DateTimeOffset> clock=now??(()=>DateTimeOffset.UtcNow);
 public async Task<RpgAdventureResult> ExecuteAsync(string userId,string name,string requestId,RpgActivity activity,long stake,CancellationToken token)
 {
  var c=config.Adventures.Rpg;var fishing=activity==RpgActivity.Fishing;var game=fishing?config.Adventures.Fishing:config.Adventures.Treasure;
  if(!config.Enabled||!config.PointsEnabled||!c.Enabled||!game.Enabled||activity==RpgActivity.Boss)return new(false,userId,0,0,0,"RPG-Aktion ist deaktiviert.","",null,0,[]);
  if(stake<0)return new(false,userId,0,0,0,"Ungültige Aktion.","",null,0,[]);
  // Legacy adapters may send a stake; all zone excursions are free and never debit it.
  c.Validate();points.ConfigureHotTime(c,config.MaximumAccountEnabled?config.MaximumAccountPoints:long.MaxValue);var time=clock();var command=fishing?"!fish":"!dive";
  RpgItem? item=null;RpgEnemyResult? enemy=null;var additional=new List<RpgItem>();var material=new Dictionary<string,int>();var applied=Array.Empty<string>();
  string eventType="",outcome="",zoneId="",zoneName="";long payout=0;int remaining=0,shells=0;
  var result=await points.ApplyRpgAsync(userId,name,requestId,a=>{
   var p=a.Rpg;var zone=c.Zones.Get(fishing?p.SelectedFishingZone:p.SelectedDivingZone);zoneId=zone.Id;zoneName=zone.Name;
   if(p.Cooldowns.TryGetValue(command,out var until)&&until>time){remaining=(int)Math.Ceiling((until-time).TotalSeconds);return new(false,$"Cooldown: noch {remaining} Sekunden.");}
   RpgHealthService.Refresh(p,c.Health,time);if(!fishing&&p.Health.CurrentHp<=0)return new(false,"Du hast 0 HP. Erst heilen, dann tauchen.");
   applied=p.Buffs.Where(b=>b.Activity==activity&&RpgSushiService.Active(b,time)).Select(b=>b.RecipeId).ToArray();var stats=CharacterStatService.Calculate(p,c,activity,now:time);
   void Merge(IReadOnlyDictionary<string,int> found){foreach(var pair in found)material[pair.Key]=checked(material.GetValueOrDefault(pair.Key)+pair.Value);}
   void PassiveRoll(){
    var pool=c.DiveLoot.Items.Where(i=>i.Slot is null&&i.Kind!=RpgItemKind.Consumable&&i.Rarity<=ItemRarity.Legendary).ToArray();if(pool.Length==0)return;
    var found=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(pool[rng.NextInclusive(0,pool.Length-1)]))!;
    found.FoundAt=time;
    for(int copy=0;copy<RpgEternalCombat.NormalLootMultiplier(p,c);copy++){
     var reward=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(found))!;
     reward.InstanceId=Guid.NewGuid().ToString("N");p.Inventory.Add(reward);additional.Add(reward);
    }
   }
   if(fishing){
    eventType="fish";
    if(rng.NextInclusive(0,9999)<Math.Clamp(100-game.LossChance+RpgPerkCatalog.Combined(p,"catch"),0,100)*100){
     var fish=RpgFishingService.Select(c,stats,0,rng,p);var quantity=1+RpgEternalLoot.FishRolls(p,c,rng)+RpgEternalEightLoot.FishRolls(p,c,rng)+(RpgPerkCatalog.Proc(p,"doubleFish",rng)?1:0)+(RpgPerkCatalog.Proc(p,"reserveFish",rng)?1:0);
     if(RpgDepthsCombatService.Has(p,c,"true_eternal_reef",4)&&rng.NextInclusive(0,9999)<1500)quantity++;
     quantity=checked(quantity*RpgEternalCombat.NormalLootMultiplier(p,c));
     if(!p.Fish.TryGetValue(fish.Id,out var stack))p.Fish[fish.Id]=stack=new(){FishId=fish.Id,FirstCaughtAt=time};stack.Quantity=checked(stack.Quantity+quantity);stack.LastCaughtAt=time;
     a.FishCollection[fish.Name]=checked(a.FishCollection.GetValueOrDefault(fish.Name)+quantity);
     item=new(){ItemId=fish.Id,DisplayName=fish.Name,IconId=fish.IconId,Rarity=fish.Rarity,FoundAt=time};payout=Math.Max(0,fish.PearlReward??c.FreeFishingReward);outcome=$"{quantity} × {fish.Name}";Merge(RpgZoneService.Materials(p,c,zone,"fish",rng));
    }else{p.Materials.Shells=checked(p.Materials.Shells+1);shells=1;outcome="Eine Muschel gefunden!";}
   }else{
    var roll=rng.NextInclusive(0,999999)/10000m;
    var enemyChance=EnemyChance(zone,stats);
    if(roll<zone.MimicChance+enemyChance){
     var source=roll<zone.MimicChance?"mimic":rng.NextInclusive(0,9999)<zone.EliteChance*100?"elite":"normal";
     if(zone.Level==100&&source=="elite"&&rng.NextInclusive(0,9)==0)source="endgame";
     enemy=new RpgEnemyService(rng).Fight(p,c,zone,source,rng.NextInclusive(0,2),time);eventType=source;
     if(enemy.Victory){item=enemy.Loot;if(enemy.ExtraLoot is not null)additional.Add(enemy.ExtraLoot);Merge(enemy.Materials);payout=zone.Level*2;}outcome=$"{enemy.Name} · Level {enemy.Level}: "+(enemy.Victory?"Besiegt":"Nicht besiegt");
    }else if(roll<zone.MimicChance+enemyChance+zone.ChestChance){eventType="chest";outcome="Schatztruhe geöffnet";Merge(RpgZoneService.Materials(p,c,zone,"chest",rng));PassiveRoll();payout=zone.Level;}
    else{eventType="materials";outcome="Die Tiefe erkundet";Merge(RpgZoneService.Materials(p,c,zone,"dive",rng));payout=zone.Level/2;}
    if(enemy is null||enemy.Victory){
     if(RpgPassiveService.TryProc(p,c,RpgActivity.Dive,RpgEffectTrigger.ExtraLoot,rng,time,0)>0)PassiveRoll();
     for(int i=0;i<RpgEternalLoot.DiveRolls(p,c,rng)+RpgEternalEightLoot.DiveRolls(p,c,rng);i++)PassiveRoll();
    }
    if(RpgEternalCombat.Has(p,c,"eternal_star_hunter",6)&&rng.NextInclusive(0,9999)<3000&&(enemy is null||enemy.Victory))Merge(RpgZoneService.Materials(p,c,zone,"dive",rng));
   }
   payout=RpgEternalCombat.Enhanced(p)?RpgCollectorService.Reward(payout,stats.PearlBonus):RpgPassiveService.RewardPayout(payout,0,stats.PearlBonus);payout=checked(payout*RpgEternalCombat.NormalLootMultiplier(p,c));
   var collector=RpgCollectorService.Calculate(p,c.DiveLoot);payout=RpgCollectorService.Reward(payout,fishing?collector.FishingPearls:collector.DivePearls);
   payout=points.RewardAmount(payout,fishing?RpgRewardSource.Fishing:RpgRewardSource.DiveNetWin);if(config.MaximumAccountEnabled)payout=Math.Min(payout,Math.Max(0,config.MaximumAccountPoints-a.Points));a.Points=checked(a.Points+payout);
   a.GamesPlayed=checked(a.GamesPlayed+1);
   RpgPassiveService.ConsumeBuffs(p,activity,c,rng,time,0);p.Cooldowns[command]=time.AddSeconds(RpgPerkCatalog.Cooldown(p,game.CooldownSeconds,fishing?"fishCooldown":"diveCooldown")/(1+(double)Math.Clamp(stats.MovementSpeed,-90,200)/100));return new(true);
  },token);
  if(result.Success&&!result.Duplicate)points.PublishStory(new(fishing?GroqScene.Fishing:GroqScene.Dive,"adventure:"+userId+":"+requestId,outcome,name));
  return new(result.Success,userId,0,result.Success?payout:0,result.Balance,result.Error,outcome,result.Success?item:null,remaining,result.Success?applied:[]){AdditionalItems=result.Success?additional:[],Enemy=result.Success?enemy:null,MaterialRewards=result.Success?material:new Dictionary<string,int>(),ShellsAwarded=result.Success?shells:0,ZoneId=zoneId,ZoneName=zoneName,EventType=eventType};
 }
 public static decimal EnemyChance(RpgZoneDefinition zone,RpgAttributes stats)=>Math.Clamp(zone.EnemyChance*(1+stats.EnemyEncounterChance/100),0,100-zone.MimicChance);
}
