import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';

it.each([false,true])('keeps polling a chat-started boss despite unavailable character status (status supported: %s)',async statusSupported=>{
 vi.useFakeTimers();
 const posts:string[]=[];let active=false;
 const current=profile();
 const fetcher:typeof fetch=async(url,request)=>{
  const path=String(url);
  if(path.endsWith('/me'))return Response.json(current);
  if(path.endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',streamLive:false,
   catalogJson:JSON.stringify({...catalog(),supported:['bossStatus','bossAttack',...(statusSupported?['status']:[])]})});
  if(request?.method==='POST'){
   const intent=JSON.parse(request.body as string);posts.push(intent.kind);
   if(intent.kind==='status')return Response.json({error:'synthetic_status_unavailable'},{status:500});
   return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',dataJson:JSON.stringify({
    status:{BossActive:active,BossHp:active?1000:0,BossMaxHp:active?1000:0,BossEventId:active?'chat-boss':'',BossEndsAt:null},
    damage:0,hotTime:{active:false,endsAt:0}
   })}});
  }
  throw Error('Unexpected request');
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,helper:{onAuthorized:cb=>cb(authorization)}});
 source.start();
 try{
  await vi.advanceTimersByTimeAsync(0);
  expect(source.getSnapshot().remote!.bossKnown).toBe(true);
  expect(source.getSnapshot().boss.status).toBe('waiting');
  active=true;
  await vi.advanceTimersByTimeAsync(5000);
  expect(source.getSnapshot().boss.status).toBe('active');
  expect(source.getSnapshot().boss.hp).toBe(1000);
  expect(source.canDispatch('boss')).toBe(true);
  active=false;
  await vi.advanceTimersByTimeAsync(5000);
  expect(source.getSnapshot().boss.status).toBe('waiting');
  expect(source.canDispatch('boss')).toBe(false);
  expect(posts.filter(k=>k==='bossStatus')).toHaveLength(3);
  expect(posts).not.toContain('bossAttack');
 }finally{source.dispose();vi.useRealTimers();}
});

