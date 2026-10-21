using System.Globalization;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

internal sealed partial class RemoteRpgExecutor
{
    private long _minigameRevision;
    private readonly Dictionary<(string,string),(Guid Id,ChatMinigameService.RemoteCasinoResult Result)> _casinoResults=[];
    private object AdventurePreview(ViewerPointEntry account,string activity,long stake)
    {
        var config=configuration();var rpg=config.Adventures.Rpg;
        var sources=CharacterStatService.DiveLuckSources(account.Rpg,rpg);
        var hot=points.HotTime.DropBonus(rpg.HotTime);
        var breakdown=RpgLootService.Chances(stake,sources.Perks,rpg.DiveLoot,sources.Sushi,sources.Equipment,hot);
        var maximum=activity=="fish"?ItemRarity.Epic:breakdown.MaximumRarity;
        var kind=activity=="fish"?RpgActivity.Fishing:RpgActivity.Dive;
        var hasPool=rpg.DiveLoot.Items.Any(i=>i.DropActivities.Contains(kind) && i.Rarity<=maximum && rpg.DiveLoot.RarityWeights.GetValueOrDefault(i.Rarity)>0 && (i.Collector is null || rpg.DiveLoot.CollectorPoolChance>0));
        var chance=Math.Min(Math.Min(100,rpg.DiveLoot.MaximumDropChance),breakdown.Total+RpgTalentService.Bonus(account.Rpg,"loot"));
        if(activity=="fish"){
            var caught=100-Math.Max(0,config.Adventures.Fishing.LossChance-RpgTalentService.Bonus(account.Rpg,"catch"));
            chance=caught/100m*Math.Min(Math.Min(100,rpg.DiveLoot.MaximumDropChance),rpg.FishingEquipmentChance*(1+Math.Clamp(hot,0,200)/100));
        }
        return new{stake=stake.ToString(CultureInfo.InvariantCulture),chance=hasPool?chance:0,rarities=Enum.GetValues<ItemRarity>().Where(r=>r<=maximum).Select(r=>r.ToString()),primaryEquipmentOnly=true,hotTimeIncluded=hot>0};
    }

    private async Task<object> MinigameDataAsync(ViewerPointEntry account,CancellationToken ct)
    {
        var c=configuration();var version=Interlocked.Increment(ref _minigameRevision);var now=DateTimeOffset.UtcNow;
        var games=new[]{
            (id:"gamble",name:"Gamble",enabled:c.GambleEnabled,min:c.MinimumBet,max:c.MaximumBet,cooldown:c.GambleCooldownSeconds,choices:Array.Empty<string>()),
            (id:"coinflip",name:"Münzwurf",enabled:c.CoinflipEnabled,min:c.CoinflipMinimumBet,max:c.CoinflipMaximumBet,cooldown:c.CoinflipCooldownSeconds,choices:new[]{"kopf","zahl"}),
            (id:"slots",name:"Slots",enabled:c.SlotsEnabled,min:c.SlotsMinimumBet,max:c.SlotsMaximumBet,cooldown:c.SlotsCooldownSeconds,choices:Array.Empty<string>()),
            (id:"roulette",name:"Roulette",enabled:c.RouletteEnabled,min:c.RouletteMinimumBet,max:c.RouletteMaximumBet,cooldown:c.RouletteCooldownSeconds,choices:new[]{"rot","schwarz","gerade","ungerade","niedrig","hoch"}.Concat(Enumerable.Range(0,37).Select(n=>n.ToString(CultureInfo.InvariantCulture))).ToArray())};
        var rules=games.Select(g=>new{id=g.id,name=g.name,enabled=minigames is not null && g.enabled && permitted("!"+g.id,account.DisplayName),canStart=false,canJoin=false,usesStake=true,minimumStake=g.min,maximumStake=g.max,cooldownSeconds=g.cooldown,joinSeconds=0,minimumParticipants=1,maximumParticipants=1,startCommand="!"+g.id,joinCommand="",choices=g.choices,description="Originale Plugin-Regeln, dieselben Cooldowns wie im Chat."}).ToArray();
        var states=games.Select(g=>{
            var found=_casinoResults.TryGetValue((account.UserId,g.id),out var last);
            var net=found?last.Result.Payout-last.Result.Stake:0;
            return new{game=g.id,name=g.name,eventId=found?last.Id.ToString():epoch+":"+g.id,version,phase=found?"result":"inactive",participants=Array.Empty<object>(),participantCount=0,joined=false,personalStake=found?last.Result.Stake:0,cooldownEndsAt=account.Rpg.Cooldowns.TryGetValue("!"+g.id,out var until)?until.ToUnixTimeMilliseconds():0,
                result=found?new{outcome=net>0?"success":net==0?"breakEven":"failure",personalNet=net,personalPayout=last.Result.Payout,personalLoss=Math.Max(0,-net),totalPayout=last.Result.Payout,totalLoss=Math.Max(0,-net),message=last.Result.Message}:null};
        }).ToArray();
        var allRules=rules.Cast<object>().ToList();
        if(minigames is not null){
            var community=System.Text.Json.JsonSerializer.SerializeToElement(minigames.ReadRemoteCommunityRules());
            var race=c.Adventures.Race;
            allRules.Add(minigames.RemoteHeistRules(permitted,account.DisplayName));
            allRules.Add(new{id="race",name="Otter-Rennen",enabled=race.Enabled,canStart=permitted("!start race",account.DisplayName),canJoin=permitted("!joinrace",account.DisplayName),usesStake=true,minimumStake=race.MinimumBet,maximumStake=race.MaximumBet,cooldownSeconds=race.CooldownSeconds,joinSeconds=race.JoinSeconds,minimumParticipants=2,maximumParticipants=race.MaximumPlayers,startCommand="!start race",joinCommand="!joinrace",choices=Array.Empty<string>(),description="Originales gemeinsames Rennen; Abbuchung bei der Auswertung."});
            allRules.Add(minigames.RemoteDuelRules(permitted,account.DisplayName));
        }
        var allStates=states.Cast<object>().Concat(minigames is null?[]:await minigames.ReadRemoteCommunityAsync(account.UserId,version,ct)).ToArray();
        return new{rules=allRules,frame=new{streamId=epoch.ToString(),revision=version,serverTime=now.ToUnixTimeMilliseconds(),states=allStates,balance=account.Points}};
    }
}
