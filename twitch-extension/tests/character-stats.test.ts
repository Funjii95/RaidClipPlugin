import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';

it('keeps authoritative character stats despite pearls and HP changing independently in snapshots',async()=>{
 vi.useFakeTimers();let current=profile(),statusReads=0;
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(current);
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:['status']})});
  const intent=JSON.parse(request!.body as string);expect(intent.kind).toBe('status');statusReads++;
  const origin=structuredClone(current);origin.points=9999;origin.rpg.health.currentHp=12;
  origin.rpg.health.lastPassiveRegen='2026-10-20T12:00:00Z';origin.rpg.fish.sardine.Quantity=99;
  origin.rpg.buffs[0].ActivatedAt='2026-10-18T14:00:00+02:00';
  return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({profile:origin,stats:{Damage:12.5,CritChance:3}})}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,helper:{onAuthorized:cb=>cb(authorization)}});source.start();
 try{
  await vi.advanceTimersByTimeAsync(0);
  expect(source.getSnapshot().remote!.stats).toMatchObject({damage:12.5,critChance:3});
  expect(source.getSnapshot().player.pearls).toBe(1234);
  current.points=1300;current.rpg.health.currentHp=90;
  await source.refresh(true);
  expect(source.getSnapshot().remote!.stats).toMatchObject({damage:12.5});
  expect(source.getSnapshot().player.pearls).toBe(1300);
  current.rpg.perks.damage=3;
  await source.refresh(true);
  expect(source.getSnapshot().remote!.stats).toBeUndefined();
  await vi.advanceTimersByTimeAsync(5000);
  expect(source.getSnapshot().remote!.stats).toMatchObject({damage:12.5});
  expect(statusReads).toBeGreaterThanOrEqual(2);
 }finally{source.dispose();vi.useRealTimers();}
});

it.each(['foreign-user','changed-equipment'])('rejects mismatched status data and retries instead of caching its absence (%s)',async mismatch=>{
 vi.useFakeTimers();let mismatchActive=true,reads=0;
 const current=profile();
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(current);
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:['status']})});
  const intent=JSON.parse(request!.body as string);reads++;
  const origin=structuredClone(current);
  if(mismatchActive){if(mismatch==='foreign-user')origin.userId='1002';else origin.rpg.equipment={'0':'owned-trident'};}
  return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({profile:origin,stats:{Damage:17}})}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,helper:{onAuthorized:cb=>cb(authorization)}});source.start();
 try{
  await vi.advanceTimersByTimeAsync(0);expect(source.getSnapshot().remote!.stats).toBeUndefined();
  mismatchActive=false;await vi.advanceTimersByTimeAsync(5000);
  expect(source.getSnapshot().remote!.stats).toMatchObject({damage:17});expect(reads).toBe(2);
 }finally{source.dispose();vi.useRealTimers();}
});
