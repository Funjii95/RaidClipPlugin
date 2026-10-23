namespace RaidClipPlugin.Services;
internal static class ChatOverlayPage
{
    internal const string Html="""
<!doctype html><html lang="de"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Funjii · Chat-Overlay</title>
<style>
*{box-sizing:border-box}html,body{margin:0;width:100%;height:100%;background:transparent!important;overflow:hidden}body{font-family:"Segoe UI",Arial,sans-serif;color:#fff;font-size:26px}#chat{position:absolute;inset:0;display:flex;flex-direction:column;justify-content:flex-end;gap:12px;padding:16px;overflow:hidden}.message{flex-shrink:0;line-height:1.4;overflow-wrap:anywhere;white-space:pre-wrap;text-shadow:0 2px 4px #000,0 0 8px #000,1px 0 2px #000;border-left:3px solid var(--color,#7ce0e6);padding-left:12px;max-height:50vh;overflow:hidden}.new{animation:arrive .22s ease-out}.name{font-weight:750;color:var(--color,#7ce0e6)}.badge{font-size:.8em;margin-right:5px}.time{font-size:.62em;color:#ddd;margin-right:8px}.emote{height:var(--emote,28px);max-width:112px;object-fit:contain;vertical-align:middle;margin:0 2px;filter:drop-shadow(0 1px 2px #000)}#preview{position:absolute;top:10px;left:16px;color:#ffdf8c;font:700 14px "Segoe UI",sans-serif;text-shadow:0 1px 3px #000;display:none}@keyframes arrive{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}@media(prefers-reduced-motion:reduce){.new{animation:none}}
</style><div id="preview">VORSCHAU · TESTNACHRICHTEN</div><main id="chat" aria-label="Twitch-Chat"></main><script>
const chat=document.getElementById('chat'),preview=location.pathname==='/preview',query=new URLSearchParams(location.search);
function number(name,initial,min,max){const raw=query.get(name),value=raw===null?initial:Number(raw);return Number.isFinite(value)?Math.max(min,Math.min(max,value)):initial}
const count=Math.floor(number('count',8,1,30)),fade=number('fade',60,0,600),font=number('font',26,14,64);document.body.style.fontSize=font+'px';
let state=null,offset=0,known=new Set(),renderKey="";
function imageUrl(value){try{const u=new URL(value);return u.protocol==='https:'&&['static-cdn.jtvnw.net','cdn.betterttv.net','cdn.7tv.app'].includes(u.hostname)?u.href:null}catch{return null}}
function render(){
 const now=Date.now()+offset,messages=(state?.Messages??[]).filter(m=>Number.isFinite(Date.parse(m.Timestamp))&&(!fade||now-Date.parse(m.Timestamp)<fade*1000)).slice(-count),next=new Set();const key=JSON.stringify([messages,state?.ShowTimestamps,state?.ShowBadges,state?.ShowUserColors,state?.EmoteSize]);if(key===renderKey)return;renderKey=key;chat.replaceChildren();
 for(const m of messages){
  const key=String(m.Id)+'|'+m.Timestamp;next.add(key);const row=document.createElement('article');row.className='message'+(!known.has(key)?' new':'');row.dataset.id=String(m.Id);
  const color=state.ShowUserColors&&/^#[a-f0-9]{6}$/i.test(m.Color)?m.Color:'#7ce0e6';row.style.setProperty('--color',color);row.style.setProperty('--emote',Math.max(16,Math.min(64,state.EmoteSize||28))+'px');
  if(state.ShowTimestamps){const time=document.createElement('span');time.className='time';time.textContent=new Date(m.Timestamp).toLocaleTimeString('de-DE',{hour:'2-digit',minute:'2-digit'});row.append(time)}
  if(state.ShowBadges){const badges=[[m.Broadcaster,'👑','Broadcaster'],[m.Moderator,'🛡','Moderator'],[m.Vip,'💎','VIP'],[m.Subscriber,'★','Abonnent']];for(const [active,icon,title]of badges)if(active){const badge=document.createElement('span');badge.className='badge';badge.textContent=icon;badge.title=title;row.append(badge)}}
  const name=document.createElement('span');name.className='name';name.textContent=String(m.Name)+': ';row.append(name);
  const emotes=new Map((m.Emotes??[]).map(e=>[e.Code,imageUrl(e.Url)]));
  for(const token of String(m.Text).split(/(\s+)/)){const url=emotes.get(token);if(url){const img=document.createElement('img');img.className='emote';img.src=url;img.alt=token;img.onerror=()=>img.replaceWith(document.createTextNode(token));row.append(img)}else row.append(document.createTextNode(token))}
  chat.append(row);
 }
 while(chat.scrollHeight>chat.clientHeight&&chat.children.length>1)chat.firstElementChild.remove();known=next;
}
async function poll(){try{const response=await fetch('/state',{cache:'no-store'});if(!response.ok)throw Error();state=await response.json();offset=Date.parse(state.ServerNow)-Date.now();if(!Number.isFinite(offset))throw Error();render()}catch{state=null;chat.replaceChildren();known.clear();renderKey=""}setTimeout(poll,500)}
if(preview){document.getElementById('preview').style.display='block';const t=new Date().toISOString();state={ServerNow:t,ShowBadges:true,ShowUserColors:true,EmoteSize:32,Messages:[
{Id:'1',Timestamp:t,Name:'Funjii95',Text:'Willkommen im Otter-Chat! 🦦',Color:'#ffcb76',Broadcaster:true},
{Id:'2',Timestamp:t,Name:'Perlentaucher',Text:'Die Mimic hat heute richtig Beute dabei! 💎',Color:'#7ce0e6',Subscriber:true},
{Id:'3',Timestamp:t,Name:'Otterpfote',Text:'Bereit für das nächste Abenteuer? 🌊',Color:'#dca1ff',Vip:true},
{Id:'4',Timestamp:t,Name:'KleineKrabbe',Text:'Ein transparenter Chat direkt aus dem Plugin. ✨',Color:'#9ae8aa',Moderator:true}]};render()}else poll();
addEventListener('resize',()=>{renderKey='';render()});
</script></html>
""";
}
