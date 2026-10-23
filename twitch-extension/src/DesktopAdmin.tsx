import {useEffect,useState} from 'react';
import type {BackendRpgDataSource} from './remote/BackendRpgDataSource';
import {labels} from './presentation';
import type {CharacterStats} from './types';

type Identity={userId:string;channelId?:string;connected?:boolean};
type AdminCatalog={items:{id:string;name:string;rarity:string;slot:string}[];attributes:string[];events:string[]};
export const isDesktopAdmin=(identity?:Identity)=>!!identity?.connected&&!!identity.channelId&&identity.userId===identity.channelId;
const eventNames:Record<string,string>={boss:'Boss',mimic:'Mimic-Boss',race:'Otter-Rennen',heist:'Heist',hotTime:'Hot-Time'};
export function DesktopAdmin({source,identity}:{source:BackendRpgDataSource;identity?:Identity}){
 const [open,setOpen]=useState(false),[catalog,setCatalog]=useState<AdminCatalog>(),[busy,setBusy]=useState(false),[message,setMessage]=useState(''),[state,setState]=useState(()=>source.getSnapshot());
 const [itemId,setItem]=useState(''),[target,setTarget]=useState(identity?.userId??''),[count,setCount]=useState(1),[roll,setRoll]=useState('random'),[stats,setStats]=useState<Record<string,number>>({}),[stake,setStake]=useState('100');
 useEffect(()=>source.subscribe(()=>setState(source.getSnapshot())),[source]);
 if(!isDesktopAdmin(identity))return null;
 const disabled=busy||state.remote?.connection!=='ready'||state.remote?.pending===true;
 async function execute(kind:string,args:Record<string,string>={}){
  setBusy(true);setMessage('Auftrag wird im Plugin geprüft …');
  try{const result=await source.readPlugin(kind,args);setMessage(result.message??'Adminbereich geladen.');if(kind==='adminCatalog'){setCatalog(result);setItem(result.items[0]?.id??'');}else await source.refresh(true);}
  catch(error){setMessage(error instanceof Error?error.message:'Auftrag konnte nicht bestätigt werden.');}
  finally{setBusy(false);}
 }
 return <section className="desktop-admin" aria-label="Adminbereich">
  <button className="admin-toggle" aria-expanded={open} onClick={()=>setOpen(!open)}>♛ Adminbereich {open?'schließen':'öffnen'}</button>
  {open&&<div className="admin-content">
   <p>Angemeldet als Kanalinhaber. Aktionen werden im verbundenen Plugin ausgeführt.</p>
   {!catalog?<button disabled={disabled} onClick={()=>void execute('adminCatalog')}>Adminfunktionen laden</button>:<>
    <h2>Bosse &amp; Events</h2>
    <label>Rennen: Einsatz in Perlen <input inputMode="numeric" value={stake} onChange={e=>setStake(e.target.value)}/></label>
    <div className="admin-events">{catalog.events.map(event=><button key={event} disabled={disabled} onClick={()=>void execute('adminStart',{event,stake:event==='race'?stake:'0'})}>{eventNames[event]??event} starten</button>)}</div>
    <h2>Items erstellen</h2>
    <div className="admin-fields">
     <label>Gegenstand <select value={itemId} disabled={busy} onChange={e=>setItem(e.target.value)}>{catalog.items.map(i=><option key={i.id} value={i.id}>{i.name} · {i.rarity}</option>)}</select></label>
     <label>Ziel: Twitch-ID <input value={target} disabled={busy} inputMode="numeric" onChange={e=>setTarget(e.target.value)}/></label>
     <label>Anzahl (1–20) <input type="number" min={1} max={20} value={count} disabled={busy} onChange={e=>setCount(Number(e.target.value))}/></label>
     <label>Itemwerte <select value={roll} disabled={busy} onChange={e=>setRoll(e.target.value)}><option value="random">Zufällig auswürfeln</option><option value="minimum">Minimale Würfe</option><option value="maximum">Maximale Würfe</option><option value="custom">Eigene Werte / Cheat</option></select></label>
    </div>
    {roll==='custom'&&<fieldset disabled={busy}><legend>Eigene Attribute · nicht gesetzte Werte bleiben 0</legend><div className="admin-stats">{catalog.attributes.map(key=>{const camel=key[0].toLowerCase()+key.slice(1);const max=['MaximumHp','Power','Stamina'].includes(key)?1000000:['Damage','CritDamage','BossDamage','Armor'].includes(key)?1000:100;return <label key={key}>{labels[camel as keyof CharacterStats]??key}<input type="number" min={0} max={max} step="0.1" value={stats[key]??0} onChange={e=>setStats({...stats,[key]:Number(e.target.value)})}/></label>;})}</div></fieldset>}
    <p>Neue Items werden dauerhaft ins Inventar gelegt. Vorhandene Items bleiben erhalten. Die Kampfgrenzen des Plugins gelten weiter.</p>
    <button className="primary" disabled={disabled||!/^\d{1,25}$/.test(target)||count<1||count>20||!Number.isInteger(count)} onClick={()=>void execute('adminGrant',{itemId,targetUserId:target,count:String(count),roll,stats:JSON.stringify(roll==='custom'?stats:{})})}>Items erstellen &amp; speichern</button>
   </>}
   <p role="status">{message||'Das Plugin muss für Adminaktionen verbunden sein.'}</p>
  </div>}
 </section>;
}
