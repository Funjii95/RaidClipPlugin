import {MinigameError} from './contracts';
import type {MinigameClient,MinigameRules,HeistEvent,MinigameActionRequest,Connection} from './contracts';
export interface MinigameViewState {connection:Connection;rules:MinigameRules[];frame?:HeistEvent;error:string;pending:boolean;retryAvailable:boolean;}
export class MinigameController {
 private state:MinigameViewState={connection:'connecting',rules:[],error:'',pending:false,retryAvailable:false};
 private listeners=new Set<()=>void>();private unsubscribe?:()=>void;private request?:MinigameActionRequest;private generation=0;
 constructor(private client:MinigameClient){}
 getSnapshot=()=>this.state;
 subscribe=(listener:()=>void)=>{this.listeners.add(listener);return ()=>{this.listeners.delete(listener);};};
 private update(next:Partial<MinigameViewState>){this.state={...this.state,...next};for(const l of this.listeners)l();}
 accept(event:HeistEvent){const old=this.state.frame;if(!old||event.streamId!==old.streamId||event.revision<=old.revision)return;
  const versions=new Map(old.states.map(s=>[s.game,s.version]));if(event.states.some(s=>s.version<(versions.get(s.game)??0)))return;
  this.update({frame:event});
 }
 async connect(){const generation=++this.generation;this.unsubscribe?.();this.update({connection:'connecting',error:''});
  this.unsubscribe=this.client.subscribe(e=>{if(generation===this.generation){if(this.state.frame&&e.streamId!==this.state.frame.streamId){void this.connect();return;}this.accept(e);this.update({connection:'connected',error:''});}},reason=>{if(generation===this.generation)this.update({connection:reason,error:'Verbindung unterbrochen. Der letzte Status wird angezeigt.'});});
  try{const rules=await this.client.getRules();const frame=await this.client.getSnapshot();if(generation!==this.generation)return;
   this.update({rules,frame:!this.state.frame||frame.streamId!==this.state.frame.streamId||frame.revision>=this.state.frame.revision?frame:this.state.frame,connection:'connected',error:''});
  }catch(e){if(generation===this.generation)this.fail(e);}
 }
 dispose(){this.generation++;this.unsubscribe?.();}
 private fail(e:unknown){const code=e instanceof MinigameError?e.code: 'disconnected';const connection=['offline','unauthorized','disconnected'].includes(code)?code as Connection:this.state.connection;
  this.update({connection,error:e instanceof Error?e.message:'Backend nicht erreichbar.',pending:false});}
 async act(intent:Omit<MinigameActionRequest,'requestId'>){if(this.state.pending||this.request)return;if(this.state.connection!=='connected'){this.update({error:'Zuerst neu verbinden.'});return;}
  this.request={...intent,requestId:crypto.randomUUID()};await this.send();
 }
 private async send(){if(!this.request||this.state.pending)return;this.update({pending:true,error:'',retryAvailable:false});
  try{const response=await this.client.execute(this.request);this.accept(response.snapshot);this.request=undefined;this.update({pending:false,retryAvailable:false});}
  catch(e){const uncertain=!(e instanceof MinigameError)||['disconnected','offline'].includes(e.code);if(!uncertain)this.request=undefined;this.fail(e);this.update({retryAvailable:uncertain});}
 }
 async retry(){if(this.state.connection==='connected')await this.send();}
}
