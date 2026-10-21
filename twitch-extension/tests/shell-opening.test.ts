import {it,expect,vi} from 'vitest';
import {mapProfile} from '../src/remote/model';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {profile,catalog,authorization,memoryStorage} from './remote-fixtures';
const stock=(shells=10)=>({shells,whitePearls:0,bluePearls:0,redPearls:0,stoneChips:0});
it('maps only actual material balances, preserving unknown legacy stock instead of inventing zero',()=>{
 expect(mapProfile(profile()).materials).toBeUndefined();
 const p:any=profile();p.rpg.materials=stock();
 expect(mapProfile(p).materials).toEqual(stock());
 p.rpg.materials.shells=-1;expect(()=>mapProfile(p)).toThrow('Material');
});
it('dispatches a bounded shell action once and immediately displays the committed profile',async()=>{
 vi.useFakeTimers();let posts=0;let cloud:any=profile(100);
 cloud.rpg.materials=stock();
 const committed=structuredClone(cloud);committed.updatedAt='2026-10-18T12:00:01Z';committed.rpg.materials={...stock(0),whitePearls:10};
 const rules=catalog();rules.supported.push('openShells');
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(cloud);
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)});
  const intent=JSON.parse(request!.body as string);posts++;
  expect(intent.kind).toBe('openShells');expect(intent.arguments).toEqual({count:'10'});expect(intent).not.toHaveProperty('userId');
  return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,message:'10 Muscheln geöffnet.',dataJson:JSON.stringify({profile:committed})}});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,loadStats:false,helper:{onAuthorized:cb=>cb(authorization)}});
 try{
  await vi.advanceTimersByTimeAsync(0);
  await expect(source.dispatch({type:'openShells',count:101})).rejects.toThrow('1 bis 100');expect(posts).toBe(0);
  const result=await source.dispatch({type:'openShells',count:10});
  expect(posts).toBe(1);expect(result.materials?.shells).toBe(0);expect(result.materials?.whitePearls).toBe(10);
  expect(result.remote!.message).toContain('Muscheln');
  await source.refresh(true);expect(source.getSnapshot().materials?.shells).toBe(0);
 }finally{source.dispose();vi.useRealTimers();}
});
