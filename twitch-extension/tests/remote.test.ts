import {describe,it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {mapProfile} from '../src/remote/model';
import {profile,catalog,availability,authorization,memoryStorage} from './remote-fixtures';
import type {Authorization} from '../src/live/client';
const json=(body:unknown,status=200)=>Response.json(body,{status});
async function fixture(options:{offline?:boolean;missingRoute?:boolean;unauthorized?:boolean;forbidden?:boolean;testUserDenied?:boolean;lostAck?:boolean;pending?:boolean;stats?:boolean}={}){
 let current=profile(),posts:any[]=[],processed=new Set<string>();let applied=false;let authorize:(a:Authorization)=>void=()=>{};
 const {storage,data}=memoryStorage();
 const fetcher:typeof fetch=async(url,request)=>{
  const path=String(url);
  if(options.unauthorized)return new Response('',{status:401});
  if(options.forbidden)return new Response('',{status:403});
  if(options.testUserDenied&&path.endsWith('/state'))return json({error:'test_user_not_allowed'},403);
  if(path.endsWith('/me'))return json(current);
  if(path.endsWith('/state'))return options.missingRoute?json({error:'missing'},404):json({...availability(),available:!options.offline,reason:options.offline?'plugin_not_ready':'ready',streamLive:false});
  if(request?.method==='POST'){
   const intent=JSON.parse(request.body as string);posts.push(intent);expect(intent.userId).toBeUndefined();
   expect((request.headers as Record<string,string>).Authorization).toBe('Bearer '+authorization.token);
   if(!processed.has(intent.actionId)){processed.add(intent.actionId);current=profile(1234,true);applied=true;}
   if(options.lostAck&&posts.length===1)throw Error('synthetic lost ACK');
   return json({actionId:intent.actionId,state:'queued'});
  }
  const id=path.split('/').pop();return json({actionId:id,state:options.pending?'queued':'applied',outcome:{success:true,code:'applied',message:'',dataJson:'{}'}});
 };
 const source=new BackendRpgDataSource({storage,fetch:fetcher,helper:{onAuthorized:callback=>{authorize=callback;callback(authorization)}},loadStats:false,actionPollMs:1,actionTimeoutMs:15});
 await source.refresh();return {source,posts,processed,data,fetcher,storage,authorize,get applied(){return applied},get current(){return current}};
}
describe('authoritative UI mapping',()=>{
 it('maps actual profile, item rolls, numeric enums, equipment and all stored stocks without demo data',()=>{
  const view=mapProfile(profile(1234,true),catalog());expect(view.player.pearls).toBe(1234);expect(view.player.level).toBe(7);expect(view.player.hp).toBe(87);
  expect(view.equipment.Weapon).toBe('owned-trident');expect(view.inventory[0]).toMatchObject({rarity:'Epic',slot:'Weapon',favorite:true,locked:false,attributes:{damage:12.5,power:3,maximumHp:5}});
  expect(view.perks[0]).toMatchObject({id:'damage',level:2,maxLevel:5});expect(view.player.buffs[0]).toMatchObject({activity:'Boss',category:'Offensive',remainingUses:2});
  expect(view.fish.sardine).toBe(4);expect(view.sushi.fishing).toBe(2);expect(view.remote!.perkPrices.damage).toBeUndefined();expect(view.remote!.stats).toBeUndefined();expect(view.remote!.bossKnown).toBe(false);
  expect(view.remote!.recipes[0].category).toBeUndefined();expect(view.remote!.recipes[0].effectsText).toBe('+10 Angelglück');
 });
 it('preserves Int64 pearl display exactly and never substitutes a demo balance',()=>{
  const state=mapProfile(profile('9007199254740993'));expect(state.remote!.pointsText).toBe('9.007.199.254.740.993');expect(Number.isNaN(state.player.pearls)).toBe(true);
 });
 it('fails closed for a mock backend, corrupt inventory and unknown schemas',()=>{
  expect(()=>mapProfile({...profile(),source:'mock'})).toThrow('keinen echten');
  const corrupt=profile();(corrupt.rpg.inventory[0] as any).Attributes=undefined;expect(()=>mapProfile(corrupt)).toThrow('Attributdaten');
  expect(()=>mapProfile({...profile(),rpg:{...profile().rpg,schemaVersion:99}})).toThrow('Datenversion');
  expect(()=>mapProfile({...profile(),rpg:{...profile().rpg,equipment:{'0':'foreign'}}})).toThrow('fehlendes Item');
 });
});
describe('productive RpgDataSource',()=>{
 it('distinguishes expired login, missing identity and closed-test permission without allowing actions',async()=>{
  for(const [options,message] of [[{unauthorized:true},'ungültig oder abgelaufen'],[{forbidden:true},'Identität freigeben'],[{testUserDenied:true},'geschlossenen Aktionstest']] as const){
   const f=await fixture(options);expect(f.source.getSnapshot().remote!.message).toContain(message);expect(f.source.canDispatch('equip')).toBe(false);expect(f.posts).toHaveLength(0);f.source.dispose();
  }
 });
 it('loads real API state and submits exactly one intent on parallel clicks then reloads the server snapshot',async()=>{
  const f=await fixture();expect(f.source.mode).toBe('remote');expect(f.source.getSnapshot().player.pearls).toBe(1234);
  const results=await Promise.allSettled([f.source.dispatch({type:'equip',id:'owned-trident',slot:'Weapon'}),f.source.dispatch({type:'equip',id:'owned-trident',slot:'Weapon'})]);
  expect(results.filter(r=>r.status==='fulfilled')).toHaveLength(1);expect(f.posts).toHaveLength(1);expect(f.source.getSnapshot().equipment.Weapon).toBe('owned-trident');expect(f.source.getSnapshot().player.pearls).toBe(1234);
  expect(JSON.stringify([...f.data.values()])).not.toContain(authorization.token);f.source.dispose();
 });
 it('does not mutate game data on offline streams or missing action endpoints while keeping the profile readable',async()=>{
  for(const opts of [{offline:true},{missingRoute:true}]){const f=await fixture(opts);expect(f.source.getSnapshot().player.pearls).toBe(1234);expect(f.source.canDispatch('equip')).toBe(false);await expect(f.source.dispatch({type:'equip',id:'owned-trident',slot:'Weapon'})).rejects.toThrow('nicht bereit');expect(f.posts).toHaveLength(0);f.source.dispose();}
 });
 it('clears old private state on authentication failure and never falls back to mocks',async()=>{
  const f=await fixture({unauthorized:true});const state=f.source.getSnapshot();expect(state.remote!.connection).toBe('unauthorized');expect(state.remote!.loaded).toBe(false);expect(state.inventory).toEqual([]);expect(Number.isNaN(state.player.pearls)).toBe(true);expect(f.posts).toHaveLength(0);f.source.dispose();
 });
 it('retains the same pending ActionId over lost ACK and page reload',async()=>{
  const f=await fixture({lostAck:true});await expect(f.source.dispatch({type:'equip',id:'owned-trident',slot:'Weapon'})).rejects.toThrow('lost ACK');
  expect(f.source.getSnapshot().remote!.pending).toBe(true);const id=f.posts[0].actionId;
  const second=new BackendRpgDataSource({storage:f.storage,fetch:f.fetcher,helper:{onAuthorized:cb=>cb(authorization)},loadStats:false,actionPollMs:1});await second.refresh();
  expect(second.getSnapshot().equipment.Weapon).toBe('owned-trident');
  await vi.waitFor(()=>expect(second.canDispatch('equip')).toBe(true));
  expect(f.posts).toHaveLength(1);expect(f.posts[0].actionId).toBe(id);expect(f.processed.size).toBe(1);expect(second.getSnapshot().remote!.pending).toBe(false);f.source.dispose();second.dispose();
 });
 it('leaves timeouts pending instead of booking another action',async()=>{
  const f=await fixture({pending:true});await expect(f.source.dispatch({type:'equip',id:'owned-trident',slot:'Weapon'})).rejects.toThrow('Bestätigung');expect(f.source.canDispatch('equip')).toBe(false);expect(f.posts).toHaveLength(1);f.source.dispose();
 });
 it('refuses every demo-only or unsupported game action',async()=>{
  const f=await fixture();for(const type of ['reset','newBoss','demoWound','healChat','hot'] as const){expect(f.source.canDispatch(type)).toBe(false);await expect(f.source.dispatch({type} as any)).rejects.toThrow('sichere Backend');}expect(f.posts).toHaveLength(0);f.source.dispose();
 });
});
