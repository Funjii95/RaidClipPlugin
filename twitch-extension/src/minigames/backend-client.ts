import {MinigameError} from './contracts';
import type {MinigameClient,MinigameRules,HeistEvent,MinigameActionRequest,HeistJoinResult,Connection} from './contracts';
// Injection seam only: no public HTTP routes or localhost/OBS connection are invented here.
export interface AuthenticatedMinigameTransport {
 rules(token:string):Promise<MinigameRules[]>;
 snapshot(token:string):Promise<HeistEvent>;
 execute(token:string,request:MinigameActionRequest):Promise<HeistJoinResult>;
 subscribe(token:()=>string,onEvent:(event:HeistEvent)=>void,onDisconnect:(reason:Connection)=>void):()=>void;
}
export class BackendMinigameClient implements MinigameClient {
 readonly mode='live' as const;
 constructor(private transport:AuthenticatedMinigameTransport,private currentHelperToken:()=>string|undefined){}
 private token=()=>{const token=this.currentHelperToken();if(!token)throw new MinigameError('unauthorized','Bitte deine Twitch-Identität freigeben.');return token;};
 // Having a token is not authentication: the backend must verify its signature and claims.
 async getRules(){return this.transport.rules(this.token());}
 async getSnapshot(){return this.transport.snapshot(this.token());}
 async execute(request:MinigameActionRequest){return this.transport.execute(this.token(),request);}
 subscribe(onEvent:(event:HeistEvent)=>void,onDisconnect:(reason:Connection)=>void){
  try{this.token();return this.transport.subscribe(this.token,onEvent,onDisconnect);}
  catch{onDisconnect('unauthorized');return ()=>{};}
 }
}
