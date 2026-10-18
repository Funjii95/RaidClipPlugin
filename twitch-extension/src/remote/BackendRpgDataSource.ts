import type {Action,CharacterStats,DemoState,RpgDataSource} from '../types';
import {LiveHTTPError,LiveRpgClient,parseWire} from '../live/client';
import type {Authorization,Receipt,Availability} from '../live/client';
import {attributes,emptyRemoteState,field,inventoryItem,mapProfile} from './model';
export type TwitchHelper={onAuthorized:(callback:(auth:Authorization)=>void)=>void;actions?:{requestIdShare:()=>void}};
const actionKinds:Partial<Record<Action['type'],string>>={dive:'dive',fishing:'fish',equip:'equip',unequip:'unequip',perk:'buyPerk',cook:'craft',activateSushi:'eat'};
const canonical=(v:any):any=>Array.isArray(v)?v.map(canonical):v&&typeof v==='object'?Object.fromEntries(Object.entries(v).sort(([a],[b])=>a.localeCompare(b)).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),canonical(v)])):v;
const signature=(v:any)=>JSON.stringify(canonical([field(v,'UserId'),field(v,'Points'),field(v,'Rpg')]));
export class BackendRpgDataSource implements RpgDataSource {
 readonly mode='remote' as const;
 private state=emptyRemoteState();private authorization?:Authorization;private generation=0;private refreshing?:Promise<DemoState>;private mutating=false;
 private listeners=new Set<()=>void>();private client:LiveRpgClient;private catalog:any={};private profile:any;private availability?:Availability;
 private lastRead=-Infinity;private availabilityAt=-Infinity;private statusSeen='';private statusPayload?:{signature:string;stats:CharacterStats};
 private pollTimer?:ReturnType<typeof setInterval>;
 constructor(private options:{helper?:TwitchHelper;storage?:Storage;fetch?:typeof fetch;pollMs?:number;actionPollMs?:number;actionTimeoutMs?:number;loadStats?:boolean}={}){
  this.client=new LiveRpgClient(()=>this.authorization,options.storage??(()=>{try{return sessionStorage;}catch{return {getItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");},setItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");},removeItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");}} as unknown as Storage;}})(),options.fetch);
  options.helper?.onAuthorized(auth=>this.authorize(auth));
 }
 private authorize(auth:Authorization){
  const scope=(a?:Authorization)=>a?`${a.channelId}:${a.userId}`:'';
  if(scope(auth)!==scope(this.authorization)){
   this.generation++;this.state=emptyRemoteState();this.profile=undefined;this.catalog={};this.statusSeen='';this.statusPayload=undefined;this.availability=undefined;this.availabilityAt=-Infinity;this.lastRead=-Infinity;
  }
  this.authorization=auth?.token&&auth.channelId&&auth.userId?auth:undefined;
  if(!this.authorization){this.state.remote!.connection='unauthorized';this.state.remote!.message='Bitte deine Twitch-Identität freigeben.';this.emit();return;}
  void this.refresh(true).then(()=>this.maybeLoadStats()).catch(()=>{});
 }
 requestIdentity(){this.options.helper?.actions?.requestIdShare();}
 start(){if(this.pollTimer)return;this.pollTimer=setInterval(()=>void this.refresh().then(()=>this.maybeLoadStats()).catch(()=>{}),this.options.pollMs??5000);}
 dispose(){if(this.pollTimer)clearInterval(this.pollTimer);this.listeners.clear();}
 subscribe(listener:()=>void){this.listeners.add(listener);return()=>{this.listeners.delete(listener)};}
 getSnapshot(){return structuredClone(this.state);}
 private emit(){for(const callback of this.listeners)callback();}
 canDispatch(type:Action['type']){
  if(type==='refresh')return true;
  const kind=actionKinds[type];return !!kind && !!this.authorization && this.state.remote!.loaded && this.state.remote!.connection==='ready' &&
   performance.now()-this.availabilityAt<=7000 && this.availability?.available===true && this.state.remote!.supported.includes(kind) &&
   !this.mutating && !this.state.remote!.pending;
 }
 private markPending(){try{this.state.remote!.pending=!!this.client.current();}catch{this.state.remote!.pending=true;this.state.remote!.message='Der Browser kann den sicheren Aktionsbeleg nicht speichern. Spielaktionen bleiben gesperrt.';}}
 async refresh(force=false):Promise<DemoState>{
  while(this.refreshing){await this.refreshing;if(!force)return this.getSnapshot();}
  if(!force&&performance.now()-this.lastRead<4000)return this.getSnapshot();
  const version=this.generation;
  const work=this.read(version);this.refreshing=work;
  try{return await work;}finally{if(this.refreshing===work)this.refreshing=undefined;}
 }
 private async read(version:number){
  if(!this.authorization){this.state.remote!.connection='unauthorized';this.state.remote!.message='Bitte mit Twitch anmelden und die Identität freigeben.';this.emit();return this.getSnapshot();}
  const [profile,availability]=await Promise.allSettled([this.client.snapshot(),this.client.state()]);
  if(version!==this.generation)return this.getSnapshot();
  this.lastRead=performance.now();
  const authError=[profile,availability].some(r=>r.status==='rejected'&&r.reason instanceof LiveHTTPError&&[401,403].includes(r.reason.status));
  if(authError){this.state=emptyRemoteState();this.state.remote!.connection='unauthorized';this.state.remote!.message='Twitch-Zugriff abgelehnt. Bitte Identität freigeben oder Anmeldung erneuern.';this.availability=undefined;this.profile=undefined;this.emit();return this.getSnapshot();}
  this.availability=availability.status==='fulfilled'?availability.value:undefined;
  this.availabilityAt=availability.status==='fulfilled'?performance.now():-Infinity;
  let metadataError=false;
  if(availability.status==='fulfilled'){
   try{const c=parseWire(availability.value.catalogJson??'{}');if(!c||typeof c!=='object'||Array.isArray(c))throw Error();this.catalog=c;}catch{metadataError=true;this.catalog={};this.availability=undefined;}
  }
  if(profile.status==='fulfilled'){
   try{
    const old=this.state;const mapped=mapProfile(profile.value,this.catalog);
    if(old.remote!.loaded&&old.player.id!==mapped.player.id)throw Error('Unerwarteter Spielerwechsel ohne neue Twitch-Autorisierung.');
    mapped.result=old.result;mapped.remote!.pendingState=old.remote!.pendingState;
    mapped.remote!.syncing=old.remote!.syncing&&signature(profile.value)===signature(this.profile);
    this.profile=profile.value;this.state=mapped;
    if(this.statusPayload?.signature===signature(this.profile))this.state.remote!.stats=this.statusPayload.stats;
   }catch(error){this.state=emptyRemoteState();this.state.remote!.connection='error';this.state.remote!.message=error instanceof Error?error.message:'Spielstand ungültig.';this.availability=undefined;this.emit();return this.getSnapshot();}
  }
  const view=this.state.remote!;
  view.supported=Array.isArray(this.catalog?.supported)?this.catalog.supported.filter((v:unknown)=>typeof v==='string'):[];
  if(profile.status==='rejected'){view.connection='disconnected';view.message='Spielstand nicht erreichbar. Angezeigte Daten können veraltet sein.';this.availability=undefined;}
  else if(metadataError){view.connection='error';view.message='Die Spieldaten des Plugins sind unvollständig. Aktionen bleiben gesperrt.';}
  else if(availability.status==='rejected'){view.connection=availability.reason instanceof LiveHTTPError&&availability.reason.status===404?'readonly':'disconnected';view.message=view.connection==='readonly'?'Spielstand lesbar. Die Aktionsverbindung ist auf diesem Server noch nicht verfügbar.':'Aktionsverbindung unterbrochen. Spielaktionen sind gesperrt.';}
  else if(!availability.value.available){view.connection='offline';view.message='Das Plugin ist offline oder das RPG noch nicht spielbereit. Dein gespeicherter Spielstand bleibt lesbar.';}
  else {view.connection='ready';view.message='Plugin online · RPG spielbereit';}
  this.markPending();this.emit();return this.getSnapshot();
 }
 async dispatch(action:Action):Promise<DemoState>{
  if(action.type==='refresh')return this.refresh();
  if(!this.canDispatch(action.type))throw Error(actionKinds[action.type]?'Plugin oder RPG nicht bereit bzw. eine Aktion ist noch offen.':'Diese Aktion hat noch keine sichere Backend-Anbindung.');
  this.mutating=true;const generation=this.generation;this.state.remote!.pending=true;this.state.remote!.message='Aktion wird an das Spiel gesendet …';this.emit();
  try {
   let args:Record<string,string>={};
   if(action.type==='dive'||action.type==='fishing'){const n=action.stake??0;if(!Number.isSafeInteger(n)||n<0)throw Error('Ungültiger Einsatz.');args={stake:String(n)};}
   if(action.type==='equip')args={instanceId:action.id,slot:action.slot};
   if(action.type==='unequip')args={slot:action.slot};
   if(action.type==='perk')args={perkId:action.id};
   if(action.type==='cook'||action.type==='activateSushi')args={recipeId:action.recipeId};
   const receipt=await this.client.submit(actionKinds[action.type]!,args);
   await this.complete(receipt,generation,action);
   return this.getSnapshot();
  }catch(error){if(generation===this.generation){this.markPending();this.state.remote!.message=error instanceof Error?error.message:'Ergebnis unklar. Dieselbe Aktion prüfen, keine neue Buchung starten.';this.emit();}throw error;}
  finally{this.mutating=false;if(generation===this.generation){this.markPending();this.emit();}}
 }
 async retryPending(){
  if(this.mutating)throw Error('Eine Aktion läuft bereits.');this.mutating=true;
  const generation=this.generation;
  try{await this.complete(await this.client.sendSame(),generation);return this.getSnapshot();}
  finally{this.mutating=false;if(generation===this.generation){this.markPending();this.emit();}}
 }
 private async complete(first:Receipt,generation:number,action?:Action){
  let receipt=first;const deadline=performance.now()+(this.options.actionTimeoutMs??25000);
  while(receipt.state==='queued'||receipt.state==='executing'){
   if(generation!==this.generation)return;
   this.state.remote!.pendingState=receipt.state;this.state.remote!.message=receipt.state==='queued'?'Aktion wartet auf das verbundene Spiel …':'Aktion wird verarbeitet …';this.emit();
   if(performance.now()>=deadline)throw Error('Noch keine endgültige Bestätigung. Bitte dieselbe Aktion prüfen.');
   await new Promise(resolve=>setTimeout(resolve,this.options.actionPollMs??1000));receipt=await this.client.check();
  }
  if(generation!==this.generation)return;
  this.state.remote!.pendingState=receipt.state;this.markPending();
  if(receipt.state==='unknown')throw Error('Ergebnis unklar. Keine neue Aktion buchen; zuerst dieselbe Aktion prüfen und den gespeicherten Stand laden.');
  if(receipt.state==='rejected'||receipt.outcome?.success!==true){await this.refresh(true);throw Error(receipt.outcome?.message||'Das Spiel hat die Aktion abgelehnt.');}
  let data:any;try{data=receipt.outcome.dataJson?parseWire(receipt.outcome.dataJson):undefined;}catch{}
  const before=signature(this.profile);
  await this.refresh(true);
  if(generation!==this.generation)return;
  this.state.remote!.syncing=signature(this.profile)===before&&!!action;
  this.state.remote!.message=this.state.remote!.syncing?'Aktion bestätigt. Der gespeicherte Spielstand wird nachgeladen.':'Aktion bestätigt. Gespeicherten Spielstand aktualisiert.';
  if(action&&(action.type==='dive'||action.type==='fishing')&&data&&field(data,'Success')===true){
   const payout=field(data,'Payout');const stake=field(data,'Stake');
   if(typeof payout==='number'&&Number.isSafeInteger(payout)&&typeof stake==='number'&&Number.isSafeInteger(stake)){
    const items=[field(data,'Item'),...(field(data,'AdditionalItems')??[])].filter(Boolean).map(inventoryItem);
    this.state.result={kind:action.type==='dive'?'dive':'fishing',pearls:payout,stake,items,message:field(data,'Outcome')||'Aktion abgeschlossen.'};
   }
  }
  this.emit();
 }
 private async maybeLoadStats(){
  if(this.options.loadStats===false||!this.profile||!this.availability?.available||!this.state.remote!.supported.includes('status')||this.mutating||this.state.remote!.pending)return;
  const sig=signature(this.profile);if(sig===this.statusSeen)return;this.statusSeen=sig;this.mutating=true;const generation=this.generation;
  try{
   let receipt=await this.client.submit('status',{});this.markPending();this.emit();const deadline=performance.now()+(this.options.actionTimeoutMs??25000);
   while(receipt.state==='queued'||receipt.state==='executing'){
    if(generation!==this.generation||performance.now()>deadline)return;
    await new Promise(resolve=>setTimeout(resolve,this.options.actionPollMs??1000));receipt=await this.client.check();
   }
   if(generation!==this.generation||receipt.state!=='applied'||!receipt.outcome?.dataJson)return;
   const data=parseWire(receipt.outcome.dataJson);
   if(data?.profile&&data?.stats){
    const origin={...data.profile,Source:'plugin-snapshot'};mapProfile(origin,this.catalog);
    const actual=attributes(data.stats) as CharacterStats;
    this.statusPayload={signature:signature(origin),stats:actual};
    if(this.statusPayload.signature===signature(this.profile))this.state.remote!.stats=actual;
   }
  }catch{}finally{this.mutating=false;if(generation===this.generation){this.markPending();this.emit();}}
 }
}
