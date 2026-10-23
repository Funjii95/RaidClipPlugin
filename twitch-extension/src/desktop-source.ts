import {MockRpgDataSource} from './mock';
import type {Action,DemoState} from './types';
export type DesktopIdentity={userId:string;displayName:string};
export class DesktopSource extends MockRpgDataSource {
 private lastSaved='';
 readonly storageKey:string;
 constructor(readonly identity:DesktopIdentity,private storage:Storage){
  const key='funjii.desktop.preview.v1:'+identity.userId;
  const raw=storage.getItem(key);let saved:DemoState|undefined;
  if(raw){
   const value=JSON.parse(raw);
   if(value.version!==1||value.userId!==identity.userId||!value.state?.player||!Array.isArray(value.state?.inventory))throw Error('Der lokale Testspielstand ist beschädigt. Er wurde nicht überschrieben.');
   saved=value.state;
  }
  super(()=>Date.now(),()=>Math.random(),saved);
  this.storageKey=key;
 }
 override getSnapshot(){const state=super.getSnapshot();state.player.id=this.identity.userId;state.player.username=this.identity.displayName;return state;}
 flush(){const serialized=JSON.stringify({version:1,userId:this.identity.userId,state:this.getSnapshot()});if(serialized!==this.lastSaved){this.storage.setItem(this.storageKey,serialized);this.lastSaved=serialized;}}
 override async dispatch(action:Action){await super.dispatch(action);this.flush();return this.getSnapshot();}
 override async settleMinigameFixture(id:string,stake:number,payout:number){await super.settleMinigameFixture(id,stake,payout);this.flush();}
}
