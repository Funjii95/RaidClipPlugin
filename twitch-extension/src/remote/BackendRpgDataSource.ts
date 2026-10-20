import type {Action,CharacterStats,DemoState,RpgDataSource} from '../types';
import {LiveHTTPError,LiveRpgClient,parseWire,readOnlyIntents} from '../live/client';
import type {Authorization,Receipt,Availability} from '../live/client';
import {attributes,emptyRemoteState,field,inventoryItem,mapProfile} from './model';
import {RemoteMinigameClient} from '../minigames/remote-client';
export type TwitchHelper={onAuthorized:(callback:(auth:Authorization)=>void)=>void;actions?:{requestIdShare:()=>void}};
import {PluginRuleError} from './errors';
const actionKinds:Partial<Record<Action['type'],string>>={dive:'dive',fishing:'fish',equip:'equip',unequip:'unequip',perk:'buyPerk',cook:'craft',activateSushi:'eat',boss:'bossAttack',favorite:'favorite',lock:'itemlock',dismantle:'salvagePreview',salvageBatch:'salvageBatch',confirmSalvage:'salvageConfirm',cancelSalvage:'salvageCancel'};
const canonical=(v:any):any=>Array.isArray(v)?v.map(canonical):v&&typeof v==='object'?Object.fromEntries(Object.entries(v).sort(([a],[b])=>a.localeCompare(b)).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),canonical(v)])):v;
const signature=(v:any)=>JSON.stringify(canonical([field(v,'UserId'),field(v,'Points'),field(v,'Rpg')]));
export class BackendRpgDataSource implements RpgDataSource {
 readonly minigames=new RemoteMinigameClient(this);
 private previewCache?:{key:string;value:NonNullable<DemoState['remote']>['adventurePreview']};
 readonly mode='remote' as const;
 private state=emptyRemoteState();private authorization?:Authorization;private generation=0;private refreshing?:Promise<DemoState>;private mutating=false;
 private listeners=new Set<()=>void>();private client:LiveRpgClient;private catalog:any={};private profile:any;private availability?:Availability;
 private lastRead=-Infinity;private availabilityAt=-Infinity;private statusSeen='';private statusPayload?:{signature:string;stats:CharacterStats};
 private pollTimer?:ReturnType<typeof setInterval>;
 private deleting=false;private deletionCompleted=false;
 private readingStats=false;
 private reads=new Map<string,Promise<any>>();
 constructor(private options:{helper?:TwitchHelper;storage?:Storage;fetch?:typeof fetch;pollMs?:number;actionPollMs?:number;actionTimeoutMs?:number;loadStats?:boolean}={}){
  this.client=new LiveRpgClient(()=>this.authorization,options.storage??(()=>{try{return sessionStorage;}catch{return {getItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");},setItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");},removeItem(){throw Error("Sicherer Aktionsspeicher nicht verfügbar.");}} as unknown as Storage;}})(),options.fetch);
  options.helper?.onAuthorized(auth=>this.authorize(auth));
 }
 private authorize(auth:Authorization){
  const scope=(a?:Authorization)=>a?`${a.channelId}:${a.userId}`:'';
  if(scope(auth)!==scope(this.authorization)){
   this.generation++;this.deleting=false;this.deletionCompleted=false;this.state=emptyRemoteState();this.profile=undefined;this.catalog={};this.statusSeen='';this.statusPayload=undefined;this.availability=undefined;this.availabilityAt=-Infinity;this.lastRead=-Infinity;
  }
  this.authorization=auth?.token&&auth.channelId&&auth.userId?auth:undefined;
  if(!this.authorization){this.state.remote!.connection='unauthorized';this.state.remote!.message='Bitte deine Twitch-Identität freigeben.';this.emit();return;}
  try{this.client.clearReadOnlyPending();}catch{}
  void this.refresh(true).then(()=>this.reconcilePending()).then(()=>this.maybeLoadStats()).catch(()=>{});
 }
 requestIdentity(){this.options.helper?.actions?.requestIdShare();}
 start(){if(this.pollTimer)return;this.pollTimer=setInterval(()=>void this.refresh().then(()=>this.reconcilePending()).then(()=>this.maybeLoadStats()).catch(()=>{}),this.options.pollMs??5000);}
 dispose(){if(this.pollTimer)clearInterval(this.pollTimer);this.listeners.clear();}
 subscribe(listener:()=>void){this.listeners.add(listener);return()=>{this.listeners.delete(listener)};}
 getSnapshot(){return structuredClone(this.state);}
 private emit(){for(const callback of this.listeners)callback();}
 canDispatch(type:Action['type']){
  if(type==='refresh'||type==='cancelSalvage')return true;
  const kind=actionKinds[type];return !this.deleting && !!kind && !!this.authorization && this.state.remote!.loaded && this.state.remote!.connection==='ready' &&
   performance.now()-this.availabilityAt<=7000 && this.availability?.available===true && this.state.remote!.supported.includes(kind) &&
   !this.mutating && !this.state.remote!.pending && (type!=='boss'||this.state.remote!.bossKnown&&this.state.boss.status==='active');
 }
 private markPending(){try{this.state.remote!.pending=!!this.client.current();}catch{this.state.remote!.pending=true;this.state.remote!.message='Der Browser kann den sicheren Aktionsbeleg nicht speichern. Spielaktionen bleiben gesperrt.';}}
 async selfDeletionStatus(){const generation=this.generation;const status=await this.client.selfDeletion();if(generation!==this.generation)throw Error('Twitch-Identität gewechselt.');if(status.request){this.deleting=true;this.state.remote!.connection='readonly';if(status.request.completed){this.deletionCompleted=true;this.client.clearAfterConfirmedDeletion();this.profile=undefined;this.state=emptyRemoteState();this.state.remote!.selfDeletionEnabled=true;this.state.remote!.message='RPG-Spielstand gelöscht.';}this.emit();}return status;}
 async requestSelfDeletion(requestId:string){const generation=this.generation;const job=await this.client.requestSelfDeletion(requestId);if(generation!==this.generation)throw Error('Twitch-Identität gewechselt.');this.deleting=true;this.state.remote!.connection='readonly';this.emit();return job;}
 async refresh(force=false):Promise<DemoState>{
  while(this.refreshing){await this.refreshing;if(!force)return this.getSnapshot();}
  if(!force&&performance.now()-this.lastRead<4000)return this.getSnapshot();
  const version=this.generation;
  const work=this.read(version);this.refreshing=work;
  try{return await work;}finally{if(this.refreshing===work)this.refreshing=undefined;}
 }
 private async read(version:number){
  if(this.deletionCompleted)return this.getSnapshot();
  if(!this.authorization){this.state.remote!.connection='unauthorized';this.state.remote!.message='Bitte mit Twitch anmelden und die Identität freigeben.';this.emit();return this.getSnapshot();}
  const [profile,availability]=await Promise.allSettled([this.client.snapshot(),this.client.state()]);
  if(version!==this.generation||this.deletionCompleted)return this.getSnapshot();
  this.lastRead=performance.now();
  const authError=[profile,availability].some(r=>r.status==='rejected'&&r.reason instanceof LiveHTTPError&&[401,403].includes(r.reason.status));
  if(authError){
   const errors=[profile,availability].flatMap(r=>r.status==='rejected'&&r.reason instanceof LiveHTTPError?[r.reason]:[]);
   this.state=emptyRemoteState();this.state.remote!.connection='unauthorized';
   this.state.remote!.message=errors.some(e=>e.status===401)?'Twitch-Zugriff abgelehnt. Die Anmeldung ist ungültig oder abgelaufen. Bitte die Twitch-Kanalseite neu laden.':errors.some(e=>e.code==='test_user_not_allowed')?'Dieses Twitch-Konto ist für den geschlossenen Aktionstest noch nicht freigegeben.':'Twitch-Zugriff abgelehnt. Bitte unten deine Twitch-Identität freigeben. Falls sie bereits freigegeben ist, die Twitch-Kanalseite neu laden.';
   this.availability=undefined;this.profile=undefined;this.emit();return this.getSnapshot();
  }
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
    mapped.result=old.result;mapped.salvageQuote=old.salvageQuote;if(old.remote!.bossKnown){mapped.boss=old.boss;mapped.remote!.bossKnown=true;}if(old.remote!.hotKnown){mapped.hotTime=old.hotTime;mapped.remote!.hotKnown=true;}mapped.remote!.pendingState=old.remote!.pendingState;
    mapped.remote!.syncing=old.remote!.syncing&&signature(profile.value)===signature(this.profile);
    this.profile=profile.value;this.state=mapped;
    if(this.statusPayload?.signature===signature(this.profile))this.state.remote!.stats=this.statusPayload.stats;
    if(this.previewCache?.key.startsWith(signature(this.profile)+'|'+JSON.stringify(this.catalog)+'|'))this.state.remote!.adventurePreview=this.previewCache.value;
   }catch(error){this.state=emptyRemoteState();this.state.remote!.connection='error';this.state.remote!.message=error instanceof Error?error.message:'Spielstand ungültig.';this.availability=undefined;this.emit();return this.getSnapshot();}
  }
  const view=this.state.remote!;
  view.selfDeletionEnabled=availability.status==='fulfilled'&&availability.value.selfDeletionEnabled===true;
  view.supported=Array.isArray(this.catalog?.supported)?this.catalog.supported.filter((v:unknown)=>typeof v==='string'):[];
  if(profile.status==='rejected'){view.connection='disconnected';view.message='Spielstand nicht erreichbar. Angezeigte Daten können veraltet sein.';this.availability=undefined;}
  else if(metadataError){view.connection='error';view.message='Die Spieldaten des Plugins sind unvollständig. Aktionen bleiben gesperrt.';}
  else if(availability.status==='rejected'){view.connection=availability.reason instanceof LiveHTTPError&&availability.reason.status===404?'readonly':'disconnected';view.message=view.connection==='readonly'?'Spielstand lesbar. Die Aktionsverbindung ist auf diesem Server noch nicht verfügbar.':'Aktionsverbindung unterbrochen. Spielaktionen sind gesperrt.';}
  else if(!availability.value.available){view.connection='offline';view.message='Das Plugin ist offline oder das RPG noch nicht spielbereit. Dein gespeicherter Spielstand bleibt lesbar.';}
  else {view.connection='ready';view.message='Plugin online · RPG spielbereit';}
  if(this.deleting){view.connection='readonly';view.message='Datenlöschung beantragt. Neue Spielaktionen sind gesperrt.';}
  this.markPending();this.emit();return this.getSnapshot();
 }
 async dispatch(action:Action):Promise<DemoState>{
  if(action.type==='refresh')return this.refresh();
  if(action.type==='cancelSalvage'){this.state.salvageQuote=undefined;this.emit();return this.getSnapshot();}
  if(!this.canDispatch(action.type))throw Error(actionKinds[action.type]?'Plugin oder RPG nicht bereit bzw. eine Aktion ist noch offen.':'Diese Aktion hat noch keine sichere Backend-Anbindung.');
  this.mutating=true;const generation=this.generation;this.state.remote!.pending=true;this.state.remote!.message='Aktion wird an das Spiel gesendet …';this.emit();
  try {
   let args:Record<string,string>={};
   if(action.type==='dive'||action.type==='fishing'){const n=action.stake??0;if(!Number.isSafeInteger(n)||n<0)throw Error('Ungültiger Einsatz.');args={stake:String(n)};}
   if(action.type==='equip')args={instanceId:action.id,slot:action.slot};
   if(action.type==='unequip')args={slot:action.slot};
   if(action.type==='perk')args={perkId:action.id};
   if(action.type==='cook'||action.type==='activateSushi')args={recipeId:action.recipeId};
   if(action.type==='favorite'||action.type==='lock'){
    const item=this.state.inventory.find(i=>i.instanceId===action.id);if(!item)throw Error('Gegenstand nicht mehr vorhanden.');
    args={instanceId:action.id,enabled:String(!(action.type==='favorite'?item.favorite:item.locked))};
   }
   if(action.type==='dismantle')args={instanceId:action.id};
   if(action.type==='salvageBatch')args={selection:action.selection};
   if(action.type==='confirmSalvage')args={token:action.token};
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
 private async reconcilePending(){
  if(this.mutating||!this.authorization)return;
  let pending;try{pending=this.client.current();}catch{return;}
  if(!pending)return;
  const generation=this.generation;this.mutating=true;
  try{
   // Recover acknowledgements with GET only: never resubmit an uncertain buy.
   const receipt=await this.client.check();
   if(generation!==this.generation)return;
   if(receipt.state==='applied'||receipt.state==='rejected')await this.complete(receipt,generation);
  }catch{}finally{this.mutating=false;if(generation===this.generation){this.markPending();this.emit();}}
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
  if(action?.type==='dismantle'||action?.type==='salvageBatch'){
   const q=data?.quote;if(!q||typeof q.token!=='string'||!Number.isFinite(q.expiresAt)||!Number.isSafeInteger(q.total)||q.total<0||!Array.isArray(q.items)||q.items.some((i:any)=>typeof i.id!=='string'||!Number.isSafeInteger(i.value)||i.value<0))throw Error('Ungültige Zerlegevorschau.');
   this.state.salvageQuote=q;
  }
  if(action?.type==='confirmSalvage')this.state.salvageQuote=undefined;
  if(action?.type==='boss'&&data?.boss)this.acceptBoss(data.boss);
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
  if(this.options.loadStats===false||this.readingStats||!this.profile||!this.availability?.available||this.mutating||this.state.remote!.pending)return;
  const sig=signature(this.profile);this.readingStats=true;const generation=this.generation;
  try{
   if(sig!==this.statusSeen&&this.state.remote!.supported.includes('status')){
   try{
   const data=await this.readPlugin('status');
   if(generation!==this.generation)return;
   if(data?.profile&&data?.stats){
    const origin={...data.profile,Source:'plugin-snapshot'};mapProfile(origin,this.catalog);
    const actual=attributes(data.stats) as CharacterStats;
    this.statusPayload={signature:signature(origin),stats:actual};
    this.statusSeen=sig;
    if(this.statusPayload.signature===signature(this.profile))this.state.remote!.stats=actual;
   }
   }catch{} // Character status failures must not suppress the independent live boss read.
   }
   if(generation!==this.generation)return;
   if(this.state.remote!.supported.includes('bossStatus')&&!this.state.remote!.pending){
    const boss=await this.readPlugin('bossStatus');if(generation===this.generation)this.acceptBoss(boss);
   }
  }catch{}finally{this.readingStats=false;if(generation===this.generation)this.emit();}
 }
 async readPlugin(kind:string,args:Record<string,string>={},actionId?:string):Promise<any>{
  if(this.deleting)throw Error('Datenlöschung beantragt. Neue Spielaktionen sind gesperrt.');
  if(readOnlyIntents.has(kind))return this.readObservation(kind,args);
  const waitingGeneration=this.generation,waitUntil=performance.now()+8000;
  while(this.mutating&&performance.now()<waitUntil){await new Promise(r=>setTimeout(r,50));if(waitingGeneration!==this.generation)throw Error('Twitch-Identität gewechselt.');}
  if(!this.authorization||this.state.remote!.connection!=='ready'||!this.state.remote!.supported.includes(kind))throw Error('Plugin oder diese Funktion noch nicht spielbereit.');
  if(this.mutating)throw Error('Eine andere Aktion läuft bereits. Bitte denselben Auftrag erneut prüfen.');
  const generation=this.generation;this.mutating=true;
  try{
   const pending=this.client.current();
   if(pending&&(!actionId||pending.actionId!==actionId||pending.kind!==kind||JSON.stringify(pending.arguments)!==JSON.stringify(args)))throw Error('Vorherige Aktion zuerst klären.');
   let receipt=pending?await this.client.sendSame():await this.client.submit(kind,args,actionId);
   this.markPending();this.emit();const deadline=performance.now()+(this.options.actionTimeoutMs??25000);
   while(receipt.state==='queued'||receipt.state==='executing'){
    if(generation!==this.generation||performance.now()>deadline)throw Error('Ergebnis noch offen. Ausschließlich dieselbe Anfrage prüfen.');
    await new Promise(r=>setTimeout(r,this.options.actionPollMs??1000));receipt=await this.client.check();
   }
   if(generation!==this.generation)throw Error('Twitch-Identität gewechselt.');
   if(receipt.state==='rejected'||receipt.outcome?.success===false)throw new PluginRuleError(receipt.outcome?.message||'Auftrag abgelehnt.');
   if(receipt.state!=='applied'||receipt.outcome?.success!==true)throw Error(receipt.outcome?.message||'Auftrag nicht bestätigt.');
   if(!receipt.outcome.dataJson)throw Error('Bestätigte Spieldaten fehlen.');
   return parseWire(receipt.outcome.dataJson);
  }finally{this.mutating=false;this.markPending();this.emit();}
 }
 private readObservation(kind:string,args:Record<string,string>):Promise<any>{
  const generation=this.generation,key=JSON.stringify([generation,kind,args]);
  const existing=this.reads.get(key);if(existing)return existing;
  // Only allowlisted reads use transient receipts. Every economic action keeps
  // the durable, shared ActionId journal and its exclusive mutation lock.
  const work=(async()=>{
   if(!this.authorization||this.state.remote!.connection!=='ready'||!this.state.remote!.supported.includes(kind))throw Error('Plugin oder diese Funktion noch nicht spielbereit.');
   const values=new Map<string,string>();
   const storage={getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>{values.set(k,v);},removeItem:(k:string)=>{values.delete(k);}} as Storage;
   const auth=this.authorization;
   const reader=new LiveRpgClient(()=>generation===this.generation?this.authorization:undefined,storage,this.options.fetch);
   let receipt=await reader.submit(kind,args);const deadline=performance.now()+(this.options.actionTimeoutMs??25000);
   while(receipt.state==='queued'||receipt.state==='executing'){
    if(generation!==this.generation||this.authorization!==auth&&this.authorization?.userId!==auth.userId)throw Error('Twitch-Identität gewechselt.');
    if(performance.now()>=deadline)throw Error('Status noch nicht verfügbar. Bitte erneut laden.');
    await new Promise(resolve=>setTimeout(resolve,this.options.actionPollMs??1000));receipt=await reader.check();
   }
   if(generation!==this.generation)throw Error('Twitch-Identität gewechselt.');
   if(receipt.state!=='applied'||receipt.outcome?.success!==true||!receipt.outcome.dataJson)throw Error(receipt.outcome?.message||'Status nicht bestätigt.');
   return parseWire(receipt.outcome.dataJson);
  })();
  this.reads.set(key,work);
  void work.finally(()=>{if(this.reads.get(key)===work)this.reads.delete(key);}).catch(()=>{});
  return work;
 }
 private acceptBoss(data:any){
  const status=data?.status;if(!status||typeof field(status,'BossActive')!=='boolean')throw Error('Bossstatus fehlt.');
  const active=field(status,'BossActive'),hp=field(status,'BossHp'),max=field(status,'BossMaxHp');
  if(!Number.isSafeInteger(hp)||hp<0||!Number.isSafeInteger(max)||max<0)throw Error('Bossstatus ungültig.');
  this.state.boss={id:field(status,'BossEventId')||'',name:'Boss der Tiefsee',hp,maxHp:max,status:active?'active':'waiting',damage:typeof data.damage==='number'?data.damage:0,nextAt:0,endsAt:field(status,'BossEndsAt')?Date.parse(field(status,'BossEndsAt')):0};
  this.state.remote!.bossKnown=true;
  if(data.hotTime&&typeof data.hotTime.active==='boolean'&&Number.isFinite(data.hotTime.endsAt)){
   this.state.hotTime=data.hotTime;this.state.remote!.hotKnown=true;
  }
 }
 async previewAdventures(stake:number,fishingStake:number){
  if(!Number.isSafeInteger(stake)||stake<0||!Number.isSafeInteger(fishingStake)||fishingStake<0)return;
  const key=signature(this.profile)+'|'+JSON.stringify(this.catalog)+'|'+stake+'|'+fishingStake;
  if(this.previewCache?.key===key||this.mutating||this.state.remote!.pending||!this.state.remote!.supported.includes('preview'))return;
  const data=await this.readPlugin('preview',{stake:String(stake),fishingStake:String(fishingStake)});
  if(signature(data.profile)!==signature(this.profile))return;
  for(const [kind,n]of [['dive',stake],['fish',fishingStake]] as const){const p=data[kind];if(p?.stake!==String(n)||typeof p.chance!=='number'||!Number.isFinite(p.chance)||p.chance<0||p.chance>100||!Array.isArray(p.rarities))throw Error('Ungültige Lootvorschau.');}
  this.previewCache={key,value:{dive:data.dive,fish:data.fish}};this.state.remote!.adventurePreview=this.previewCache.value;this.emit();
 }
}
