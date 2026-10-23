import {EternalAdmin} from './EternalAdmin';
import {MaterialDebug} from './MaterialDebug';
import {useEffect,useState} from 'react';
import type {BackendRpgDataSource} from './remote/BackendRpgDataSource';
import {labels} from './presentation';
import {ItemArtwork} from './item-art';
import type {CharacterStats} from './types';

type Identity={userId:string;channelId?:string;connected?:boolean};
type AdminCatalog={eternalEightSets?:{Id:string;Name:string}[];settings?:import("./MaterialDebug").MaterialSettings;materials?:import("./UpgradePanel").MaterialDefinition[];items:{id:string;name:string;rarity:string;slot:string}[];attributes:string[];events:string[]};
export const isDesktopAdmin=(identity?:Identity)=>!!identity?.connected&&!!identity.channelId&&identity.userId===identity.channelId;
const eventNames:Record<string,string>={boss:'Boss',mimic:'Mimic-Boss',race:'Otter-Rennen',heist:'Heist',hotTime:'Hot-Time'};
export function DesktopAdmin({source,identity}:{source:BackendRpgDataSource;identity?:Identity}){
 const [open,setOpen]=useState(true),[catalog,setCatalog]=useState<AdminCatalog>(),[busy,setBusy]=useState(false),[message,setMessage]=useState(''),[state,setState]=useState(()=>source.getSnapshot());
 const [itemId,setItem]=useState(''),[target,setTarget]=useState(identity?.userId??''),[count,setCount]=useState(1),[roll,setRoll]=useState('random'),[stats,setStats]=useState<Record<string,number>>({}),[stake,setStake]=useState('100');
 const [itemSearch,setItemSearch]=useState(''),[rarityFilter,setRarityFilter]=useState('');
 useEffect(()=>source.subscribe(()=>setState(source.getSnapshot())),[source]);
 if(!isDesktopAdmin(identity))return null;
 const disabled=busy||state.remote?.connection!=='ready'||state.remote?.pending===true;
 async function execute(kind:string,args:Record<string,string>={}){
  setBusy(true);setMessage('Auftrag wird im Plugin geprüft …');
  try{const result=await source.readPlugin(kind,args);setMessage(result.message??'Adminbereich geladen.');if(kind==='adminCatalog'){setCatalog(result);setItem(current=>result.items.some((i:{id:string})=>i.id===current)?current:result.items[0]?.id??'');}else await source.refresh(true);}
  catch(error){setMessage(error instanceof Error?error.message:'Auftrag konnte nicht bestätigt werden.');}
  finally{setBusy(false);}
 }
 return <section className="desktop-admin" aria-label="Adminbereich">
  <button className="admin-toggle" aria-expanded={open} onClick={()=>setOpen(!open)}>♛ Adminbereich {open?'schließen':'öffnen'}</button>
  {open&&<div className="admin-content">
   <p>Angemeldet als Kanalinhaber. Aktionen werden im verbundenen Plugin ausgeführt.</p>
   {!catalog?<button disabled={disabled} onClick={()=>void execute('adminCatalog')}>Adminfunktionen laden</button>:<>
    <MaterialDebug source={source} settings={catalog.settings} materials={catalog.materials} target={target} disabled={disabled}/>
    <EternalAdmin source={source} target={target} disabled={disabled}/><h2>Bosse &amp; Events</h2>
    <button className="primary" disabled={disabled} onClick={()=>void execute('adminHealAll')}>♥ Alle Spieler heilen (Heal all)</button>
    <label>Rennen: Einsatz in Perlen <input inputMode="numeric" value={stake} onChange={e=>setStake(e.target.value)}/></label>
    <div className="admin-events">{catalog.events.map(event=><button key={event} disabled={disabled} onClick={()=>void execute('adminStart',{event,stake:event==='race'?stake:'0'})}>{eventNames[event]??event} starten</button>)}</div>
    <h2>Items erstellen</h2><article><h3>Eternal-8er-Sets</h3><p>Ergänzt nur fehlende Teile. Vorhandene Instanzen und Aufwertungen bleiben erhalten.</p>{catalog.eternalEightSets?.map(set=><button key={set.Id} disabled={disabled||!/^\d{1,25}$/.test(target)} onClick={()=>{if(confirm(`${set.Name} an Twitch-ID ${target} vergeben?`))void execute('adminGrantSet',{setId:set.Id,targetUserId:target});}}>{set.Name} vollständig vergeben</button>)}</article>
    <button disabled={disabled} onClick={()=>void execute('adminCatalog')}>Itemkatalog aktualisieren</button>
    <label>Items suchen<input value={itemSearch} onChange={e=>setItemSearch(e.target.value)} placeholder="Name oder Item-ID"/></label>
    <label>Seltenheit filtern<select value={rarityFilter} onChange={e=>{setRarityFilter(e.target.value);setItem('');}}><option value="">Alle Seltenheiten</option><option value="Divine">Göttlich / Divine</option><option value="Eternal">Eternal</option><option value="True">Wahr / True</option><option value="Abyss">Abyss</option><option value="Cursed">Cursed / Verflucht</option><option value="Legendary">Legendär</option><option value="Epic">Episch</option><option value="Rare">Selten</option><option value="Uncommon">Ungewöhnlich</option><option value="Common">Gewöhnlich</option></select></label>
    <p>{catalog.items.filter(i=>(!rarityFilter||i.rarity===rarityFilter)&&(i.name+' '+i.id).toLowerCase().includes(itemSearch.toLowerCase())).length} passende Ausrüstungsgegenstände · Neue Wahre Sets, Abyss-Sets, Abyss-Schmuck und Cursed-Items sind enthalten.</p>
    <div className="admin-fields">
     <label>Gegenstand <select value={itemId} disabled={busy} onChange={e=>setItem(e.target.value)}><option value="">Gegenstand auswählen</option>{catalog.items.filter(i=>(!rarityFilter||i.rarity===rarityFilter)&&(i.name+' '+i.id).toLowerCase().includes(itemSearch.toLowerCase())).map(i=><option key={i.id} value={i.id}>{i.name} · {i.rarity}</option>)}</select></label>
     {itemId&&<ItemArtwork id={itemId} name={catalog.items.find(i=>i.id===itemId)?.name}/>}
     <label>Ziel: Twitch-ID <input value={target} disabled={busy} inputMode="numeric" onChange={e=>setTarget(e.target.value)}/></label>
     <label>Anzahl (1–20) <input type="number" min={1} max={20} value={count} disabled={busy} onChange={e=>setCount(Number(e.target.value))}/></label>
     <label>Itemwerte <select value={roll} disabled={busy} onChange={e=>setRoll(e.target.value)}><option value="random">Zufällig auswürfeln</option><option value="minimum">Minimale Würfe</option><option value="maximum">Maximale Würfe</option><option value="custom">Eigene Werte / Cheat</option></select></label>
    </div>
    {roll==='custom'&&<fieldset disabled={busy}><legend>Eigene Attribute · nicht gesetzte Werte bleiben 0</legend><div className="admin-stats">{catalog.attributes.map(key=>{const camel=key[0].toLowerCase()+key.slice(1);const max=['Attack','MagicPower','MaximumHp','Power','Stamina'].includes(key)?1000000:['Damage','CritDamage','BossDamage','Armor'].includes(key)?1000:100;return <label key={key}>{labels[camel as keyof CharacterStats]??key}<input type="number" min={0} max={max} step="0.1" value={stats[key]??0} onChange={e=>setStats({...stats,[key]:Number(e.target.value)})}/></label>;})}</div></fieldset>}
    <p>Neue Items werden dauerhaft ins Inventar gelegt. Vorhandene Items bleiben erhalten. Die Kampfgrenzen des Plugins gelten weiter.</p>
    <button className="primary" disabled={disabled||!itemId||!catalog.items.some(i=>i.id===itemId&&(!rarityFilter||i.rarity===rarityFilter)&&(i.name+' '+i.id).toLowerCase().includes(itemSearch.toLowerCase()))||!/^\d{1,25}$/.test(target)||count<1||count>20||!Number.isInteger(count)} onClick={()=>void execute('adminGrant',{itemId,targetUserId:target,count:String(count),roll,stats:JSON.stringify(roll==='custom'?stats:{})})}>Items erstellen &amp; speichern</button>
   </>}
   <p role="status">{message||'Das Plugin muss für Adminaktionen verbunden sein.'}</p>
  </div>}
 </section>;
}

