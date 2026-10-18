import defaults from './defaults.json';
import type {HeistStatus,HeistResult} from './contracts';
export type PayoutFixture='standard'|'partial'|'refund'|'pair'|'seven';
// Local adapter only. No random game logic and no live settlements: the outcome is a supplied fixture.
// React displays these response fields unchanged; production responses must come from the backend.
export function demoSettlement(s:HeistStatus,outcome:'success'|'failure',fixture:PayoutFixture):HeistResult & {presentation?:string[]} {
 const m=defaults.minigame;const won=outcome==='success';let multiplier=0;let totalPayout=0;let presentation:string[]|undefined;
 if(won)switch(s.game){
  case 'coinflip':multiplier=m.CoinflipMultiplier;break;
  case 'gamble':multiplier=m.GambleRanges[fixture==='partial'?1:fixture==='refund'?2:3].multiplier;break;
  case 'slots':multiplier=fixture==='pair'?m.SlotsTwoMultiplier:fixture==='seven'?m.SlotsSevenMultiplier:m.SlotsThreeMultiplier;break;
  case 'roulette':multiplier=/^\d+$/.test(s.betChoice??'')?m.RouletteNumberMultiplier:m.RouletteEvenMoneyMultiplier;break;
  case 'duel':multiplier=2;break;
 }
 let personalPayout=s.joined?Math.floor(s.personalStake*multiplier):0;
 if(s.game==='heist'){totalPayout=won?(s.jackpot??0):0;personalPayout=s.joined?Math.floor(totalPayout/Math.max(1,s.participantCount)):0;}
 else if(s.game==='race'){const pool=s.totalStake??0;personalPayout=won&&s.joined?pool:0;totalPayout=pool;multiplier=s.personalStake>0?personalPayout/s.personalStake:0;}
 else if(s.game==='duel')totalPayout=s.personalStake*2;
 else totalPayout=personalPayout;
 const personalNet=personalPayout-(s.joined?s.personalStake:0),personalLoss=Math.max(0,-personalNet);
 const resolvedOutcome=s.joined?(personalNet>0?'success':personalNet===0&&s.game!=='heist'?'breakEven':'failure'):outcome;
 if(s.game==='slots')presentation=!won?['🍒','🍋','🔔']:fixture==='pair'?['🍒','🍒','🍋']:fixture==='seven'?['7️⃣','7️⃣','7️⃣']:['💎','💎','💎'];
 if(s.game==='coinflip'){const side=won?s.betChoice:s.betChoice==='kopf'?'zahl':'kopf';presentation=['🪙',side==='zahl'?'Zahl':'Kopf'];}
 if(s.game==='roulette'){const winning:Record<string,string>={rot:'1 · Rot',schwarz:'2 · Schwarz',gerade:'2 · Schwarz',ungerade:'1 · Rot',niedrig:'1 · Rot',hoch:'19 · Rot'};presentation=['🎡',won?(winning[s.betChoice??'']??s.betChoice??'0'):(s.betChoice==='0'?'1 · Rot':'0 · Grün')];}
 return {outcome:s.game==='heist'?outcome:resolvedOutcome,personalPayout,personalLoss,personalNet,totalPayout,totalLoss:s.game==='race'?Math.max(0,totalPayout-(s.requiredStake??0)):s.game==='duel'?s.personalStake:personalLoss,...(s.game==='heist'?{}:{multiplier}),message:'Demo-Ausgang; Auszahlung nach Plugin-Standardmultiplikatoren. Keine echten Perlen.',presentation};
}
