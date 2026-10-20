import {it,expect,vi} from 'vitest';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
import {authorization,profile,catalog,memoryStorage} from './remote-fixtures';

it('keeps inventory usable during slow background reads and retains an uncertain real mutation',async()=>{
 const {storage,data}=memoryStorage();const posts:any[]=[];
 let finishRead:(r:Response)=>void=()=>{};
 const fetcher:typeof fetch=async(url,request)=>{
  if(String(url).endsWith('/me'))return Response.json(profile());
  if(String(url).endsWith('/state'))return Response.json({available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:[...catalog().supported,'status','favorite','itemlock','salvagePreview']})});
  if(request?.method==='POST'){
   const intent=JSON.parse(request.body as string);posts.push(intent);
   if(intent.kind==='status')return new Promise<Response>(resolve=>{finishRead=resolve;});
   throw Error('synthetic lost mutation ACK');
  }
  throw Error('unexpected request');
 };
 const source=new BackendRpgDataSource({storage,fetch:fetcher,helper:{onAuthorized:cb=>cb(authorization)}});
 await vi.waitFor(()=>expect(posts.some(p=>p.kind==='status')).toBe(true));
 for(const kind of ['equip','unequip','favorite','lock','dismantle'] as const)expect(source.canDispatch(kind)).toBe(true);
 expect(data.size).toBe(0);
 await expect(source.dispatch({type:'favorite',id:'owned-trident'})).rejects.toThrow('lost mutation ACK');
 const mutation=posts.find(p=>p.kind==='favorite');expect(mutation.arguments).toEqual({instanceId:'owned-trident',enabled:'false'});
 expect(source.canDispatch('equip')).toBe(false);expect(source.getSnapshot().remote!.pending).toBe(true);
 expect([...data.values()].join()).toContain(mutation.actionId);
 const read=posts.find(p=>p.kind==='status');finishRead(Response.json({actionId:read.actionId,state:'applied',outcome:{success:true,code:'read',message:'',dataJson:JSON.stringify({profile:profile(),stats:{}})}}));
 await vi.waitFor(()=>expect(source.getSnapshot().remote!.stats).toBeDefined());
 expect(source.canDispatch('favorite')).toBe(false);expect([...data.values()].join()).toContain(mutation.actionId);source.dispose();
});

it.each(['status','bossStatus','minigames','preview'])('migrates only an old pending %s read without replaying it',async kind=>{
 const {storage,data}=memoryStorage();const key='funjii.action:'+authorization.channelId+':'+authorization.userId;
 storage.setItem(key,JSON.stringify({actionId:crypto.randomUUID(),kind,arguments:{}}));
 const fetcher:typeof fetch=async url=>String(url).endsWith('/me')?Response.json(profile()):Response.json({available:true,sessionId:'session',catalogJson:JSON.stringify(catalog())});
 const source=new BackendRpgDataSource({storage,fetch:fetcher,loadStats:false,helper:{onAuthorized:cb=>cb(authorization)}});await source.refresh();
 expect(data.size).toBe(0);expect(source.canDispatch('equip')).toBe(true);source.dispose();
});
