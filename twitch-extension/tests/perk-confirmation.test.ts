import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';
it('shows a committed perk immediately without waiting for cloud GET and rejects older snapshots',async()=>{
 vi.useFakeTimers();let cloud=profile(2000),hold=false;let release:((value:Response)=>void)|undefined;let posts=0;
 const committed=profile(1500);committed.updatedAt='2026-10-18T12:00:01Z';committed.rpg.perks.damage=3;
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return hold?new Promise<Response>(resolve=>release=resolve):Response.json(cloud);
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(catalog())});
  posts++;const intent=JSON.parse(request!.body as string);expect(intent.kind).toBe('buyPerk');
  return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({Balance:1500,profile:committed,stats:{Damage:6}})}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,loadStats:false,helper:{onAuthorized:cb=>cb(authorization)}});
 try{
  await vi.advanceTimersByTimeAsync(0);hold=true;
  const result=await source.dispatch({type:'perk',id:'damage'});
  expect(result.player.pearls).toBe(1500);expect(result.perks[0].level).toBe(3);
  expect(result.remote!.stats!.damage).toBe(6);expect(source.canDispatch('perk')).toBe(true);expect(posts).toBe(1);
  expect(release).toBeTypeOf('function');hold=false;release!(Response.json(cloud));await vi.advanceTimersByTimeAsync(0);
  expect(source.getSnapshot().player.pearls).toBe(1500);expect(source.getSnapshot().perks[0].level).toBe(3);
  await source.refresh(true);expect(source.getSnapshot().player.pearls).toBe(1500);
  cloud=structuredClone(committed);await source.refresh(true);expect(source.getSnapshot().remote!.syncing).toBe(false);
  cloud=profile(1200);cloud.updatedAt='2026-10-18T12:00:02Z';cloud.rpg.perks.damage=4;
  await source.refresh(true);expect(source.getSnapshot().player.pearls).toBe(1200);expect(source.getSnapshot().perks[0].level).toBe(4);
 }finally{source.dispose();vi.useRealTimers();}
});
it('does not accept a committed perk profile for a different Twitch user',async()=>{
 vi.useFakeTimers();const other=profile();other.userId='1002';
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(profile());
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(catalog())});
  const intent=JSON.parse(request!.body as string);
  return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({profile:other})}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,loadStats:false,helper:{onAuthorized:cb=>cb(authorization)}});
 try{await vi.advanceTimersByTimeAsync(0);await expect(source.dispatch({type:'perk',id:'damage'})).rejects.toThrow('anderen Spieler');expect(source.getSnapshot().player.id).toBe('1001');}
 finally{source.dispose();vi.useRealTimers();}
});
