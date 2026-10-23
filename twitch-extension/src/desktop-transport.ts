import {backend} from './live/client';
export interface NativeBridge {
 postMessage(message:unknown):void;
 addEventListener(type:'message',listener:(event:{data:any})=>void):void;
 removeEventListener(type:'message',listener:(event:{data:any})=>void):void;
}
export function desktopTransport(bridge:NativeBridge):typeof fetch {
 return (async(input:RequestInfo|URL,init?:RequestInit)=>{
  const url=new URL(String(input));
  if(url.origin!==new URL(backend).origin)throw Error('Unbekanntes API-Ziel.');
  const id=crypto.randomUUID();const headers=new Headers(init?.headers);
  return await new Promise<Response>((resolve,reject)=>{
   let timer:ReturnType<typeof setTimeout>;
   const cleanup=()=>{clearTimeout(timer);bridge.removeEventListener('message',receive);init?.signal?.removeEventListener('abort',aborted);};
   const aborted=()=>{cleanup();reject(new Error('Anfrage abgebrochen. Eine offene Aktion wird mit derselben ID geprüft.'));};
   const receive=(event:{data:any})=>{const value=event.data;if(value?.id!==id)return;cleanup();if(value.error)reject(new Error(value.error));else resolve(new Response(value.body,{status:value.status,headers:{'Content-Type':'application/json'}}));};
   bridge.addEventListener('message',receive);
   timer=setTimeout(()=>{cleanup();reject(new Error('Backend antwortet nicht. Eine offene Aktion wird mit derselben ID geprüft.'));},15000);
   if(init?.signal?.aborted){aborted();return;}
   init?.signal?.addEventListener('abort',aborted,{once:true});
   try{bridge.postMessage({type:'api',id,path:url.pathname+url.search,method:init?.method??'GET',body:typeof init?.body==='string'?init.body:null,session:headers.get('X-Rpg-Session')});}catch(error){cleanup();reject(error);}
  });
 }) as typeof fetch;
}
