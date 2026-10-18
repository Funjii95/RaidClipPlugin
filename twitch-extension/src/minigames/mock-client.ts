import { demoSettlement, type PayoutFixture } from './payout-fixtures';
import { demoRules } from './rules';
import { MinigameError } from './contracts';
import type { MinigameClient,MinigameActionRequest,HeistEvent,HeistJoinResult,HeistStatus,MinigameId,Connection,FixtureWallet } from './contracts';

// Scripted integration fixture, NOT a second implementation of plugin games.
// Outcomes are scripted; payout fixtures scale with the plugin defaults and the requested stake.
export class MockMinigameClient implements MinigameClient {
 readonly mode='demo' as const;
 private streamId=crypto.randomUUID(); private revision=1; private serial=0;
 private states:HeistStatus[]=demoRules.map(r=>({game:r.id,eventId:`idle-${r.id}`,version:1,name:r.name,phase:'inactive',participants:[],participantCount:0,joined:false,personalStake:0}));
 private listeners=new Set<(e:HeistEvent)=>void>(); private failures=new Set<(reason:Connection)=>void>();
 private claims=new Map<string,{fingerprint:string;result:HeistJoinResult}>();
 private queue:Promise<unknown>=Promise.resolve(); private connection:Connection='connected';
 private payoutFixture:PayoutFixture='standard';
 private outcome:'success'|'failure'='success'; private moderator=false;
 constructor(private wallet:FixtureWallet,private now=()=>Date.now()){}
 private check(){if(this.connection!=='connected')throw new MinigameError(this.connection,this.connection==='unauthorized'?'Twitch-Anmeldung fehlt oder ist abgelaufen.':this.connection==='offline'?'Der Stream ist offline.':'Backend-Verbindung unterbrochen. Bitte neu verbinden.');}
 async getRules(){this.check();return structuredClone(demoRules.map(r=>({...r,canStart:this.moderator&&r.id==='heist'})));}
 async getSnapshot(){this.check();return this.snapshot();}
 private snapshot():HeistEvent{return structuredClone({streamId:this.streamId,revision:this.revision,serverTime:this.now(),states:this.states,balance:this.wallet.balance()});}
 subscribe(listener:(e:HeistEvent)=>void,disconnected:(reason:Connection)=>void){this.listeners.add(listener);this.failures.add(disconnected);return ()=>{this.listeners.delete(listener);this.failures.delete(disconnected);};}
 private publish(){this.revision++;for(const l of this.listeners)l(this.snapshot());}
 execute(request:MinigameActionRequest):Promise<HeistJoinResult>{const result=this.queue.then(()=>this.apply(request));this.queue=result.catch(()=>undefined);return result;}
 private async apply(req:MinigameActionRequest){
  this.check();const fingerprint=JSON.stringify(req),old=this.claims.get(req.requestId);
  if(old){if(old.fingerprint!==fingerprint)throw new MinigameError('invalid','Anfrage-ID wurde bereits anders verwendet.');return structuredClone(old.result);}
  const rule=demoRules.find(r=>r.id===req.game);if(!rule||!rule.enabled)throw new MinigameError('unavailable','Dieses Minispiel ist nicht verfügbar.');
  if(!Number.isSafeInteger(req.stake)||req.stake<rule.minimumStake||req.stake>rule.maximumStake||req.action!=='deny'&&req.stake>this.wallet.balance())throw new MinigameError('invalid','Einsatz ungültig oder zu wenige Demo-Perlen.');
  if(rule.choices.length&&!rule.choices.includes(req.choice??''))throw new MinigameError('invalid','Bitte eine gültige Auswahl treffen.');
  let state=this.states.find(s=>s.game===req.game)!;
  if(state.cooldownEndsAt&&state.cooldownEndsAt>this.now())throw new MinigameError('invalid','Dieses Spiel hat noch Cooldown.');
  if(req.action==='start'){
   if(req.game!=='heist'||!this.moderator)throw new MinigameError('unauthorized','Nur Moderatoren oder Broadcaster dürfen den Heist starten.');
   if(['joining','closed','running'].includes(state.phase))throw new MinigameError('invalid','Es läuft bereits ein Heist.');
   this.openRound('heist',false);state=this.states.find(s=>s.game==='heist')!;
  }
  else if(req.action==='join'){
   if(!['heist','race'].includes(req.game)||req.eventId!==state.eventId||req.expectedVersion!==state.version||state.phase!=='joining'||(state.joinEndsAt??0)<=this.now())throw new MinigameError('expired','Beitrittsphase beendet oder Status geändert. Bitte aktualisieren.');
   if(state.joined)throw new MinigameError('duplicate','Du nimmst bereits teil.');
   if(state.participantCount>=rule.maximumParticipants)throw new MinigameError('invalid','Diese Runde ist bereits voll.');
   if(req.game==='race'&&req.stake!==state.requiredStake)throw new MinigameError('invalid','Im Rennen gilt derselbe Einsatz für alle.');
   state.joined=true;state.personalStake=req.stake;state.participants.push({participantId:'demo-self',displayName:'Dein Demo-Otter'});state.participantCount++;state.totalStake=(state.totalStake??0)+req.stake;
  }
  else if(req.action==='challenge'){
   if(req.game!=='duel'||!req.targetLogin||! /^[a-zA-Z0-9_]{3,25}$/.test(req.targetLogin)||req.targetLogin.toLowerCase()==='demo_otter')throw new MinigameError('invalid','Bitte einen anderen gültigen Twitch-Namen angeben.');
   if(['joining','running'].includes(state.phase))throw new MinigameError('invalid','Es gibt bereits eine offene Herausforderung.');
   this.openRound('duel',false);state=this.states.find(s=>s.game==='duel')!;state.joined=true;state.personalStake=req.stake;state.challenge={challenger:'Dein Demo-Otter',target:req.targetLogin,canAccept:false};
  }
  else if(req.action==='accept'||req.action==='deny'){
   if(req.game!=='duel'||!state.challenge?.canAccept||req.eventId!==state.eventId||req.expectedVersion!==state.version||state.phase!=='joining'||(state.joinEndsAt??0)<=this.now())throw new MinigameError('expired','Keine gültige Einladung für dich.');
   if(req.stake!==state.personalStake)throw new MinigameError('invalid','Der Duell-Einsatz ist vorgegeben.');
   state.joined=req.action==='accept';state.phase=req.action==='deny'?'completed':'running';state.phaseEndsAt=this.now()+3000;
  }
  else {
   if(state.phase==='running')throw new MinigameError('duplicate','Eine Spielanfrage läuft bereits.');
   if(!['gamble','coinflip','slots','roulette'].includes(req.game))throw new MinigameError('invalid','Aktion nicht unterstützt.');
   state={...state,presentation:({gamble:['🎲','TEST'],coinflip:['🪙','Kopf'],slots:['💎','💎','💎'],roulette:['🎡','17 · Schwarz']} as Partial<Record<MinigameId,string[]>>)[req.game],eventId:`demo-${req.game}-${++this.serial}`,phase:'running',startedAt:this.now(),phaseEndsAt:this.now()+3000,joined:true,personalStake:req.stake,betChoice:req.choice,result:undefined};
   await this.wallet.settle(req.requestId,req.stake,0);this.states=this.states.map(s=>s.game===req.game?state:s);
  }
  state.version++;this.publish();const result={requestId:req.requestId,accepted:true,snapshot:this.snapshot()};this.claims.set(req.requestId,{fingerprint,result});if(this.claims.size>256)this.claims.delete(this.claims.keys().next().value!);return structuredClone(result);
 }
 // Explicit test controls supply event snapshots; never invoke live plugin services.
 openRound(game:'heist'|'race'|'duel',incoming=true){
  const r=demoRules.find(r=>r.id===game)!;const previous=this.states.find(s=>s.game===game)!;
  if(['joining','running','closed'].includes(previous.phase))throw new MinigameError('invalid','Die laufende Demo-Runde zuerst abschließen.');
  const now=this.now();const state:HeistStatus={game,eventId:`demo-${game}-${++this.serial}`,version:previous.version+1,name:r.name,phase:'joining',startedAt:now,joinEndsAt:now+r.joinSeconds*1000,participants:[{participantId:'crew1',displayName:'Perlenpfote'},{participantId:'crew2',displayName:'Käpt’n Otter'}],participantCount:2,joined:false,personalStake:game==='duel'?100:0,requiredStake:game==='race'?100:game==='duel'?100:0,totalStake:game==='race'?200:0,jackpot:game==='heist'?1200:undefined,...(game==='duel'?{challenge:{challenger:'Käpt’n Otter',target:'Dein Demo-Otter',canAccept:incoming}}:{})};
  this.states=this.states.map(s=>s.game===game?state:s);this.publish();
 }
 addParticipant(game:MinigameId){const s=this.states.find(s=>s.game===game)!;if(s.phase!=='joining')return;s.participants.push({participantId:`crew-${++this.serial}`,displayName:`Otter ${this.serial}`});s.participantCount++;if(s.game==='race')s.totalStake=(s.totalStake??0)+(s.requiredStake??0);s.version++;this.publish();}
 setModerator(value:boolean){this.moderator=value;this.publish();}
 setPayoutFixture(value:PayoutFixture){this.payoutFixture=value;}
 setOutcome(value:'success'|'failure'){this.outcome=value;}
 setConnection(value:Connection){this.connection=value;if(value!=='connected')for(const f of this.failures)f(value);}
 async advance(game:MinigameId){return this.enqueueTick(game);}
 async tick(){return this.enqueueTick();}
 private enqueueTick(force?:MinigameId){const result=this.queue.then(()=>this.progress(force));this.queue=result.catch(()=>undefined);return result;}
 private async progress(force?:MinigameId){
  if(this.connection!=='connected')return;
  let changed=false;for(const s of this.states){
   if(s.phase==='joining'&&(s.game===force||(s.joinEndsAt??Infinity)<=this.now())){
    s.phase=s.game==='duel'?'completed':'running';s.phaseEndsAt=this.now()+3000;s.version++;changed=true;
    if(s.game==='duel')s.result={outcome:'cancelled',personalPayout:0,personalLoss:0,totalPayout:0,totalLoss:0,message:'Testanfrage abgelaufen. Keine echten Perlen verändert.'};
   } else if(s.phase==='running'&&(s.game===force||(s.phaseEndsAt??Infinity)<=this.now())){
    const settlement=demoSettlement(s,this.outcome,this.payoutFixture);
    const payout=settlement.personalPayout;
    if(s.joined){try{await this.wallet.settle(`result:${s.eventId}`,['heist','race','duel'].includes(s.game)?s.personalStake:0,payout);}catch{s.phase='completed';s.result={outcome:'cancelled',personalPayout:0,personalLoss:0,totalPayout:0,totalLoss:0,message:'Testabrechnung abgebrochen: nicht genügend Demo-Perlen. Kein Einsatz abgebucht.'};s.version++;changed=true;continue;}}
    if(settlement.presentation)s.presentation=settlement.presentation;
    s.phase='result';s.result=settlement;
    s.cooldownEndsAt=this.now()+demoRules.find(r=>r.id===s.game)!.cooldownSeconds*1000;s.phaseEndsAt=this.now()+12000;s.version++;changed=true;
   } else if(s.phase==='result'&&(s.game===force||(s.phaseEndsAt??Infinity)<=this.now())){s.phase='completed';s.version++;changed=true;}
  }if(changed)this.publish();
 }
}
