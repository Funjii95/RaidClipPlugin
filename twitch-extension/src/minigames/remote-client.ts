import {MinigameError} from './contracts';
import type {MinigameClient,MinigameActionRequest,HeistEvent,MinigameRules,Connection} from './contracts';
import {PluginRuleError} from '../remote/errors';
import type {BackendRpgDataSource} from '../remote/BackendRpgDataSource';

// All outcomes and rules arrive through the existing authenticated action relay.
export class RemoteMinigameClient implements MinigameClient {
 readonly mode='live' as const;
 private cached?:{at:number;player:string;rules:MinigameRules[];frame:HeistEvent};private loading?:Promise<void>;
 constructor(private source:BackendRpgDataSource){}
 private async load(force=false){
  const player=this.source.getSnapshot().player.id;
  if(!force&&this.cached?.player===player&&Date.now()-this.cached.at<4000)return;
  if(this.loading)return this.loading;
  this.loading=(async()=>{const data=await this.source.readPlugin('minigames');this.accept(data,player);})();
  try{await this.loading;}finally{this.loading=undefined;}
 }
 private accept(data:any,player:string){
  if(!Array.isArray(data?.rules)||!Array.isArray(data?.frame?.states)||!Number.isSafeInteger(data.frame.balance)||data.frame.balance<0||typeof data.frame.streamId!=='string'||!Number.isSafeInteger(data.frame.revision))throw Error('Unvollständiger Spielhallenstatus.');
  const games=['gamble','coinflip','slots','roulette','heist','race','duel'];
  const actual=data.rules.map((r:any)=>r.id);if(games.slice(0,4).some(id=>!actual.includes(id)))throw Error('Casino-Katalog unvollständig.');
  const amount=(n:unknown)=>typeof n==='number'&&Number.isSafeInteger(n)&&n>=0;
  if(!Number.isFinite(data.frame.serverTime)||data.frame.revision<0||data.rules.length>games.length||new Set(data.rules.map((r:any)=>r.id)).size!==data.rules.length||data.frame.states.length!==data.rules.length||new Set(data.frame.states.map((s:any)=>s.game)).size!==data.rules.length||
   data.rules.some((r:any)=>!games.includes(r.id)||typeof r.name!=='string'||typeof r.enabled!=='boolean'||!amount(r.minimumStake)||!amount(r.maximumStake)||r.maximumStake<r.minimumStake||!Array.isArray(r.choices)||r.choices.some((v:unknown)=>typeof v!=='string'))||
   data.frame.states.some((s:any)=>!games.includes(s.game)||!amount(s.version)||!amount(s.personalStake)||!Array.isArray(s.participants)||!['inactive','joining','closed','running','result','completed'].includes(s.phase)||!actual.includes(s.game)||s.result&&(!amount(s.result.personalPayout)||!amount(s.result.personalLoss)||!Number.isSafeInteger(s.result.personalNet))))throw Error('Ungültige Spielhallendaten.');
  if(this.source.getSnapshot().player.id!==player)throw Error('Spieler gewechselt.');
  this.cached={at:Date.now(),player,rules:data.rules,frame:data.frame};
 }
 async getRules(){await this.load();return this.cached!.rules;}
 async getSnapshot(){await this.load();return this.cached!.frame;}
 async execute(request:MinigameActionRequest){
  const casino=['gamble','coinflip','slots','roulette'].includes(request.game)&&request.action==='play';
  const community=request.game==='heist'&&request.action==='join'||request.game==='race'&&['start','join'].includes(request.action)||request.game==='duel'&&['challenge','accept','deny'].includes(request.action);
  if(!casino&&!community)throw new MinigameError('unavailable','Dieser Aufruf bleibt Chat/Plugin vorbehalten.');
  if(!Number.isSafeInteger(request.stake)||request.stake<0)throw new MinigameError('invalid','Ungültiger Einsatz.');
  const player=this.source.getSnapshot().player.id;
  let data:any;try{data=await this.source.readPlugin(casino?'casino':'community',casino?{game:request.game,stake:String(request.stake),choice:request.choice||'-'}:{game:request.game,operation:request.action,eventId:['start','challenge'].includes(request.action)?'-':request.eventId||'-',stake:String(request.stake),target:request.targetLogin||'-'},request.requestId);}catch(error){if(error instanceof PluginRuleError)throw new MinigameError('invalid',error.message);throw error;}
  this.accept(data,player);await this.source.refresh(true);
  return {requestId:request.requestId,accepted:true,snapshot:this.cached!.frame};
 }
 subscribe(listener:(frame:HeistEvent)=>void,disconnected:(reason:Connection)=>void){
  let alive=true,busy=false;const poll=async()=>{if(busy)return;busy=true;try{
   const state=this.source.getSnapshot().remote!;if(state.connection!=='ready'){if(alive)disconnected(state.connection==='unauthorized'?'unauthorized':'offline');return;}
   await this.load(true);if(alive)listener(this.cached!.frame);
  }catch{if(alive)disconnected('disconnected');}finally{busy=false;}};
  const timer=setInterval(()=>void poll(),5000);return()=>{alive=false;clearInterval(timer);};
 }
}
