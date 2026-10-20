import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';
it('checks the same accepted read at 250ms then retains the one-second polling bound',async()=>{
 vi.useFakeTimers();let checks=0,posts=0,id='';
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(profile());
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:['status']})});
  if(request?.method==='POST'){posts++;id=JSON.parse(request.body as string).actionId;return Response.json({actionId:id,state:'queued'});}
  checks++;expect(String(url)).toContain(id);return Response.json({actionId:id,state:checks===1?'executing':'applied',outcome:checks===1?null:{success:true,dataJson:'{"verified":true}'}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,loadStats:false,helper:{onAuthorized:cb=>cb(authorization)}});
 try{
  await vi.advanceTimersByTimeAsync(0);await source.refresh(true);
  const result=source.readPlugin('status');
  await vi.advanceTimersByTimeAsync(249);expect(checks).toBe(0);
  await vi.advanceTimersByTimeAsync(1);expect(checks).toBe(1);
  await vi.advanceTimersByTimeAsync(999);expect(checks).toBe(1);
  await vi.advanceTimersByTimeAsync(1);expect(await result).toEqual({verified:true});
  expect(checks).toBe(2);expect(posts).toBe(1);
 }finally{source.dispose();vi.useRealTimers();}
});
