import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';

it('recovers a lost perk acknowledgement with GET only, then permits the next distinct purchase',async()=>{
 const current=profile(10000);let finished=false,checks=0;const posts:any[]=[];
 const fetcher:typeof fetch=async(url,request)=>{
  const path=String(url);
  if(path.endsWith('/me'))return Response.json(current);
  if(path.endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(catalog())});
  if(request?.method==='POST'){
   const intent=JSON.parse(request.body as string);posts.push(intent);
   current.points=Number(current.points)-500;current.rpg.perks.damage++;
   if(posts.length===1)throw Error('synthetic lost buy ACK');
   return Response.json({actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',message:'',dataJson:'{}'}});
  }
  checks++;
  return Response.json({actionId:path.split('/').pop(),state:finished?'applied':'executing',outcome:finished?{success:true,code:'applied',message:'',dataJson:'{}'}:undefined});
 };
 const source=new BackendRpgDataSource({fetch:fetcher,storage:memoryStorage().storage,helper:{onAuthorized:cb=>cb(authorization)},loadStats:false,pollMs:10});await source.refresh();source.start();
 await expect(source.dispatch({type:'perk',id:'damage'})).rejects.toThrow('lost buy ACK');
 await vi.waitFor(()=>expect(checks).toBeGreaterThan(0));
 expect(source.canDispatch('perk')).toBe(false);expect(posts).toHaveLength(1);expect(current.points).toBe(9500);
 finished=true;await vi.waitFor(()=>expect(source.canDispatch('perk')).toBe(true));
 expect(source.getSnapshot().perks.find(p=>p.id==='damage')!.level).toBe(3);
 await source.dispatch({type:'perk',id:'damage'});
 expect(posts).toHaveLength(2);expect(posts[1].actionId).not.toBe(posts[0].actionId);expect(current.points).toBe(9000);
 expect(source.canDispatch('perk')).toBe(true);source.dispose();
});
