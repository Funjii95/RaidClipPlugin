import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';

async function fixture(){
 let current=profile();let posts:any[]=[];const receipts=new Map<string,any>();
 const supported=[...catalog().supported,'status','bossStatus','bossAttack','favorite','itemlock','salvagePreview','salvageBatch','salvageConfirm'];
 const boss={status:{BossActive:true,BossHp:900,BossMaxHp:1000,BossEventId:'synthetic-boss',BossEndsAt:new Date(Date.now()+60000).toISOString()},damage:100};
 const fetcher:typeof fetch=async(url,request)=>{
  const path=String(url);
  if(path.endsWith('/me'))return Response.json(current);
  if(path.endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported})});
  if(request?.method==='POST'){
   const intent=JSON.parse(request.body as string);posts.push(intent);let data:any={};
   if(intent.kind==='status')data={profile:current,stats:{}};
   if(intent.kind==='bossStatus')data=boss;
   if(intent.kind==='bossAttack'){boss.status.BossHp=800;boss.damage=200;data={boss,attack:{Success:true,Damage:100}};}
   if(intent.kind==='favorite')current.rpg.inventory[0].IsFavorite=intent.arguments.enabled==='true';
   if(intent.kind==='itemlock')current.rpg.inventory[0].IsLocked=intent.arguments.enabled==='true';
   if(intent.kind==='salvagePreview')data={quote:{token:'server-quote',expiresAt:Date.now()+30000,total:33,fingerprint:'server-quote',items:[{id:'owned-trident',name:'Gespeicherter Dreizack',value:33,warning:false}]}};
   if(intent.kind==='salvageConfirm'){current.rpg.inventory=[];current.points=1267;}
   const receipt={actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',message:'',dataJson:JSON.stringify(data)}};receipts.set(intent.actionId,receipt);return Response.json(receipt);
  }
  return Response.json(receipts.get(path.split('/').pop()!));
 };
 const source=new BackendRpgDataSource({storage:memoryStorage().storage,fetch:fetcher,helper:{onAuthorized:cb=>cb(authorization)},actionPollMs:1});
 await vi.waitFor(()=>expect(source.getSnapshot().remote!.bossKnown).toBe(true));
 return{source,posts};
}
it('loads the actual boss and sends an authenticated attack without simulating combat',async()=>{
 const {source,posts}=await fixture();expect(source.getSnapshot().boss.hp).toBe(900);expect(source.canDispatch('boss')).toBe(true);
 await source.dispatch({type:'boss'});expect(posts.find(p=>p.kind==='bossAttack').arguments).toEqual({});expect(source.getSnapshot().boss.hp).toBe(800);source.dispose();
});
it('uses stable owned IDs and explicit protection state, then requires the server salvage quote',async()=>{
 const {source,posts}=await fixture();await source.dispatch({type:'favorite',id:'owned-trident'});
 expect(posts.find(p=>p.kind==='favorite').arguments).toEqual({instanceId:'owned-trident',enabled:'false'});expect(source.getSnapshot().inventory[0].favorite).toBe(false);
 await source.dispatch({type:'dismantle',id:'owned-trident'});expect(source.getSnapshot().salvageQuote?.total).toBe(33);expect(source.getSnapshot().player.pearls).toBe(1234);
 await source.dispatch({type:'confirmSalvage',token:'server-quote'});expect(source.getSnapshot().player.pearls).toBe(1267);expect(source.getSnapshot().inventory).toEqual([]);expect(source.getSnapshot().salvageQuote).toBeUndefined();source.dispose();
});
