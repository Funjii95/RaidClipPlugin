import {describe,it,expect} from 'vitest';
import {LiveRpgClient,parseWire} from '../src/live/client';
const auth={token:'synthetic-jwt',channelId:'42',userId:'Usynthetic'};
function memory(){const data=new Map<string,string>();return {getItem:(k:string)=>data.get(k)??null,setItem:(k:string,v:string)=>{data.set(k,v)},removeItem:(k:string)=>{data.delete(k)}} as Storage;}
describe('live RPG client',()=>{
 it('fails closed without Helper authorization',async()=>{let calls=0;const client=new LiveRpgClient(()=>undefined,memory(),async()=>{calls++;return new Response('{}')});await expect(client.snapshot()).rejects.toThrow('Twitch');expect(calls).toBe(0);});
 it('persists the same ActionId after lost ACK, reload and retry without sending a UserId',async()=>{
  const storage=memory();let calls=0;const ids:string[]=[];
  const fetcher:typeof fetch=async(_url,options)=>{if(String(_url).endsWith('/state'))return Response.json({available:true,sessionId:'session-1'});const body=JSON.parse(options!.body as string);expect(body.userId).toBeUndefined();ids.push(body.actionId);calls++;if(calls===1)throw Error('lost ack');return Response.json({actionId:body.actionId,state:'applied',outcome:{success:true}});};
  const client=new LiveRpgClient(()=>auth,storage,fetcher);await expect(client.submit('buyPerk',{perkId:'damage'})).rejects.toThrow('lost ack');
  const restarted=new LiveRpgClient(()=>({...auth,token:'refreshed'}),storage,fetcher);await expect(restarted.submit('buyPerk',{perkId:'damage'})).rejects.toThrow('Vorherige');
  await restarted.sendSame();expect(ids[0]).toBe(ids[1]);expect(restarted.current()).toBeUndefined();
 });
 it('retains unknown results and refuses automatic new booking',async()=>{
  const client=new LiveRpgClient(()=>auth,memory(),async(_url,options)=>String(_url).endsWith('/state')?Response.json({available:true,sessionId:'session-1'}):Response.json({actionId:JSON.parse(options!.body as string).actionId,state:'unknown',outcome:{success:null}}));
  await client.submit('fish',{stake:'0'});expect(client.current()).toBeDefined();await expect(client.submit('fish',{stake:'0'})).rejects.toThrow('Vorherige');
 });
 it('uses refreshed Helper token and rejects responses from a previous identity',async()=>{
  let identity=auth;let observed='';const client=new LiveRpgClient(()=>identity,memory(),async(_url,options)=>{observed=(options!.headers as Record<string,string>).Authorization;identity={...auth,userId:'Uother'};return Response.json({points:20});});
  await expect(client.snapshot()).rejects.toThrow('Konto');expect(observed).toBe('Bearer synthetic-jwt');
 });
 it('releases a pending intent only after a durable terminal refusal',async()=>{
  const client=new LiveRpgClient(()=>auth,memory(),async(url,options)=>String(url).endsWith('/state')?Response.json({available:true,sessionId:'old-session'}):Response.json({actionId:JSON.parse(options!.body as string).actionId,state:'rejected',outcome:{success:false,code:'not_ready'}},{status:409}));
  expect((await client.submit('fish',{stake:'0'})).state).toBe('rejected');expect(client.current()).toBeUndefined();
 });
 it('preserves large Int64 values without touching quoted strings or decimal game values',()=>{expect(parseWire('{"points":9007199254740993,"text":"9007199254740993","decimal":0.25}')).toEqual({points:'9007199254740993',text:'9007199254740993',decimal:.25});});
});
