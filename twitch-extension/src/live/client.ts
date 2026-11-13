export type Intent={actionId:string;kind:string;arguments:Record<string,string>;sessionId?:string};
export type Receipt={actionId:string;state:'queued'|'executing'|'applied'|'rejected'|'unknown';outcome?:{success:boolean|null;code:string;message:string;dataJson?:string}|null};
export type Availability={available:boolean;reason:string;catalogJson?:string;sessionId?:string;selfDeletionEnabled?:boolean};
export type Authorization={token:string;channelId:string;userId:string};
export type SelfDeletionJob={userId:string;requestId:string;requestedAt:string;completed:boolean};
export const readOnlyIntents=new Set(['dungeonStatus','partyStatus','questStatus','status','bossStatus','minigames','preview','adminCatalog']);
export const backend=import.meta.env.MODE==='hosted-test'?'https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app':'https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app';
// Preserve Int64 values as display strings. Never calculate game costs or rewards here.
export function parseWire(text:string):any{return JSON.parse(text.replace(/"(?:[^"\\]|\\.)*"|-?(?:0|[1-9]\d*)(?:\.\d+)?(?:e[+-]?\d+)?/gi,token=>token[0]!== '"' && /^-?\d+$/.test(token) && !Number.isSafeInteger(Number(token))?JSON.stringify(token):token));}
export class LiveHTTPError extends Error {constructor(readonly status:number,message:string,readonly code?:string){super(message);}}
export class LiveRpgClient {
 private submitting=false;
 private pending?:Intent;
 constructor(private auth:()=>Authorization|undefined,private storage:Storage,private transport:typeof fetch=(...args)=>fetch(...args)){}
 private key(){const a=this.auth();if(!a?.token||!a.channelId||!a.userId)throw Error('Twitch-Anmeldung und Identitätsfreigabe erforderlich.');return 'funjii.action:'+a.channelId+':'+a.userId;}
 private load(){const json=this.storage.getItem(this.key());this.pending=json?JSON.parse(json):undefined;return this.pending;}
 current(){return this.load();}
 // Older clients persisted background reads in the mutation journal. These
 // requests cannot book rewards/items and must not lock the inventory forever.
 clearReadOnlyPending(){const action=this.load();if(action&&readOnlyIntents.has(action.kind)){this.storage.removeItem(this.key());this.pending=undefined;}}
 private async request(path:string,body?:unknown):Promise<any>{
  const scope=this.key();const response=await this.transport(backend+path,{method:body===undefined?'GET':'POST',headers:{Authorization:'Bearer '+this.auth()!.token,...(body===undefined?{}:{'Content-Type':'application/json','X-Rpg-Session':this.pending?.sessionId??''})},body:body===undefined?undefined:JSON.stringify(body),cache:'no-store',credentials:'omit',redirect:'error',signal:AbortSignal.timeout(8000)});
  const text=await response.text();if(this.key()!==scope)throw Error("Twitch-Konto wurde gewechselt; alte Antwort verworfen.");
  let parsed:any;try{parsed=text?parseWire(text):undefined;}catch{}
  const refusal=response.status===409&&parsed?.actionId===this.pending?.actionId&&parsed?.state==='rejected';
  if(!response.ok&&!refusal)throw new LiveHTTPError(response.status,response.status===401||response.status===403?'Twitch-Zugriff abgelehnt.':response.status===409?'Plugin nicht spielbereit / Aktionskonflikt.':'Anfrage nicht bestätigt ('+response.status+').',parsed?.error==='test_user_not_allowed'?'test_user_not_allowed':undefined);
  if(!parsed)throw Error('Ungültige Backend-Antwort.');return parsed;
 }
 state():Promise<Availability>{return this.request('/api/v1/play/state');}
 snapshot(){return this.request('/api/v1/me');}
 private deletionJob(value:any):SelfDeletionJob{if(!value||!/^\d{1,25}$/.test(value.userId)||!/^[0-9a-f-]{36}$/i.test(value.requestId)||!Number.isFinite(Date.parse(value.requestedAt))||typeof value.completed!=='boolean')throw Error('Ungültiger Löschstatus.');return value;}
 async selfDeletion():Promise<{request:SelfDeletionJob|null}>{const value=await this.request('/api/v1/play/privacy/deletion');if(!Object.hasOwn(value,'request'))throw Error('Löschstatus fehlt.');return {request:value.request===null?null:this.deletionJob(value.request)};}
 async requestSelfDeletion(requestId:string):Promise<SelfDeletionJob>{return this.deletionJob(await this.request('/api/v1/play/privacy/deletion',{requestId,confirmation:'DELETE_MY_RPG_ACCOUNT'}));}
 clearAfterConfirmedDeletion(){this.storage.removeItem(this.key());this.pending=undefined;}
 async submit(kind:string,args:Record<string,string>,actionId?:string):Promise<Receipt>{
  if(this.submitting)throw Error('Eine Aktion läuft bereits.');this.submitting=true;try{
  if(this.load())throw Error('Vorherige Aktion zuerst klären.');
  if(Object.entries(args).some(([key,v])=>!v||v.length>(kind==='adminConfigSave'&&key==='changes'?6000:kind==='adminGrant'&&key==='stats'?1500:150)))throw Error('Aktionsparameter fehlen oder sind zu lang.');
  if((kind==='fish'||kind==='dive')&&(!/^(0|[1-9]\d*)$/.test(args.stake??'')||BigInt(args.stake)>9223372036854775807n))throw Error('Einsatz als gültige ganze Zahl eingeben.');
  const ready=await this.state();if(!ready.available||!ready.sessionId)throw Error('Plugin nicht spielbereit.');
  if(actionId&&!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(actionId))throw Error('Ungültige Aktions-ID.');
  this.pending={actionId:actionId??crypto.randomUUID(),kind,arguments:args,sessionId:ready.sessionId};this.storage.setItem(this.key(),JSON.stringify(this.pending));
  return await this.sendSame();
  }finally{this.submitting=false;}
 }
 async sendSame():Promise<Receipt>{const action=this.load();if(!action)throw Error('Keine offene Aktion.');const receipt:Receipt=await this.request('/api/v1/play/actions',{actionId:action.actionId,kind:action.kind,arguments:action.arguments});return this.finish(receipt);}
 async check():Promise<Receipt>{const action=this.load();if(!action)throw Error('Keine offene Aktion.');return this.finish(await this.request('/api/v1/play/actions/'+encodeURIComponent(action.actionId)));}
 private finish(receipt:Receipt){if(receipt.actionId!==this.pending?.actionId)throw Error('Aktionszuordnung ungültig.');if(receipt.state==='applied'||receipt.state==='rejected'){this.storage.removeItem(this.key());this.pending=undefined;}return receipt;}
}
