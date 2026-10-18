import {useEffect,useRef,useState} from 'react';
import {ItemArtwork} from '../item-art';
import {LiveRpgClient,parseWire} from './client';
import type {Authorization,Availability,Receipt} from './client';
type Helper={onAuthorized:(callback:(a:Authorization)=>void)=>void;actions?:{requestIdShare:()=>void}};
const statNames:Record<string,string>={Damage:'Schaden',CritChance:'Krit-Chance',CritDamage:'Krit-Schaden',Luck:'Glück',BossDamage:'Boss-Schaden',FishingLuck:'Angelglück',DiveLuck:'Tauchglück',PearlBonus:'Perlenbonus',Power:'Stärke',Stamina:'Ausdauer',MaximumHp:'Maximale HP',DoubleAttackChance:'Doppelangriff',TripleAttackChance:'Dreifachangriff'};
const describe=(data:any)=>Object.entries(data??{}).filter(([,v])=>typeof v==='number'&&v!==0).map(([k,v])=>(statNames[k]??k)+': '+String(v)).join(' · ');
const tabs=['Charakter','Abenteuer','Inventar','Ausrüstung','Itemsets','Perks','Boss','Kochen'];
const slotNames=['Weapon','Helmet','Chest','Gloves','Boots','Ring1','Ring2','Amulet'];
const value=(o:any,k:string)=>o?.[k]??o?.[k[0].toLowerCase()+k.slice(1)];
export function LivePanel({demo}:{demo:()=>void}){
 const auth=useRef<Authorization|undefined>(undefined);
 const [client]=useState(()=>new LiveRpgClient(()=>auth.current,sessionStorage));
 const [authorized,setAuthorized]=useState(false),[authScope,setAuthScope]=useState(''),[tab,setTab]=useState('Charakter'),[availability,setAvailability]=useState<Availability>(),[profile,setProfile]=useState<any>(),[catalog,setCatalog]=useState<any>(),[receipt,setReceipt]=useState<Receipt>(),[pending,setPending]=useState(false),[working,setWorking]=useState(false),[error,setError]=useState(''),[stake,setStake]=useState('100'),[details,setDetails]=useState<any>();
 const helper=()=>((window as unknown as {Twitch?:{ext?:Helper}}).Twitch?.ext);
 const refresh=async()=>{
  try {const [state,player]=await Promise.allSettled([client.state(),client.snapshot()]);
   if(player.status==='fulfilled')setProfile(player.value);
   if(state.status==='fulfilled'){setAvailability(state.value);setCatalog(parseWire(state.value.catalogJson??'{}'));}else setAvailability(undefined);
   setError(state.status==='rejected'?state.reason.message:player.status==='rejected'?player.reason.message:'');}
  catch(e){setAvailability(undefined);setError(e instanceof Error?e.message:'Verbindung unterbrochen.');}
 };
 const accept=async(result:Receipt)=>{setReceipt(result);setPending(!!client.current());
  if(result.outcome?.dataJson)setDetails(parseWire(result.outcome.dataJson));
  if(result.state==='applied'||result.state==='rejected')await refresh();
 };
 useEffect(()=>{
  let active=true;
  helper()?.onAuthorized(a=>{if(!active)return;if(auth.current?.channelId!==a?.channelId||auth.current?.userId!==a?.userId){setProfile(undefined);setDetails(undefined);setReceipt(undefined);setAvailability(undefined);setCatalog(undefined);}
  auth.current=a;setAuthScope((a?.channelId??'')+':'+(a?.userId??''));setAuthorized(!!a?.token);try{setPending(!!client.current());}catch{};});
  return()=>{active=false;};
 },[client]);
 useEffect(()=>{if(!authorized)return;void refresh();const timer=setInterval(()=>void refresh(),5000);return()=>clearInterval(timer);},[authorized,authScope]);
 useEffect(()=>{if(!pending||!authorized||receipt?.state==='unknown')return;const timer=setInterval(()=>void client.check().then(accept).catch(e=>setError(e.message)),1500);return()=>clearInterval(timer);},[pending,authorized,receipt?.state]);
 const act=async(kind:string,args:Record<string,string>={})=>{
  setWorking(true);setDetails(undefined);
  try{await accept(await client.submit(kind,args));}catch(e){setError(e instanceof Error?e.message:'Ergebnis unklar.');try{setPending(!!client.current());}catch{}}
  finally{setWorking(false);}
 };
 const retry=async()=>{setWorking(true);try{await accept(await client.sendSame());}catch(e){setError(e instanceof Error?e.message:'Ergebnis unklar.');}finally{setWorking(false);}};
 const rpg=value(profile,'Rpg');const items:any[]=value(rpg,'Inventory')??[];const equipment=value(rpg,'Equipment')??{};const perks=value(rpg,'Perks')??{};
 const enabled=authorized&&availability?.available&&!pending&&!working;
 const actionButton=(kind:string,label:string,args:Record<string,string>={})=><button disabled={!enabled} onClick={()=>void act(kind,args)}>{label}</button>;
 return <main className="live-rpg"><header><h1>Funjii Otter RPG</h1><p>Dein gespeicherter Spielstand</p><button onClick={demo}>Lokale Demo öffnen</button></header>
 <p role="status">{availability?.available?'● Plugin online · RPG spielbereit':authorized?'Spielaktionen gesperrt · Plugin nicht bereit':'Twitch-Anmeldung und Identitätsfreigabe erforderlich'}</p>
 {!authorized&&<button onClick={()=>helper()?.actions?.requestIdShare()}>Twitch-Identität freigeben</button>}
 {error&&<p role="alert">{error}</p>}
 {pending&&<aside>Eine Aktion ist noch offen. Keine neue Buchung starten. <button disabled={working} onClick={()=>void retry()}>Dieselbe Aktion prüfen</button></aside>}
 {receipt&&<p role="status">Aktion: {{queued:"Wartet auf das Spiel",executing:"Wird ausgeführt",applied:"Abgeschlossen",rejected:"Abgelehnt",unknown:"Ergebnis unklar"}[receipt.state]} {receipt.outcome?.message} {receipt.state==='applied'?'Der gespeicherte Snapshot wird automatisch aktualisiert.':''}</p>}
 <nav aria-label="RPG-Bereiche">{tabs.map(t=><button key={t} aria-pressed={t===tab} onClick={()=>setTab(t)}>{t}</button>)}</nav>
 <section><h2>{tab}</h2>
 {tab==='Charakter'&&<><p>{value(profile,'DisplayName')??'Kein Profil geladen'}</p><p>Perlen: {String(value(profile,'Points')??'–')}</p><p>Level: {String(value(rpg,'PlayerLevel')??'–')} · EXP: {String(value(rpg,'Experience')??'–')}</p><p>HP: {String(value(value(rpg,'Health'),'CurrentHp')??'–')} / {String(value(value(rpg,'Health'),'AppliedMaximumHp')??'–')}</p>{actionButton('status','Aktuelle Charakterwerte abrufen')}
 <p>Gespeichert: {value(profile,'UpdatedAt')??'–'}</p></>}
 {tab==='Abenteuer'&&<><label>Einsatz in Perlen <input inputMode="numeric" pattern="[0-9]*" value={stake} onChange={e=>setStake(e.target.value)}/></label>{actionButton('dive','Tauchgang starten',{stake})}{actionButton('fish','Angel auswerfen',{stake})}{Object.entries(value(rpg,'Cooldowns')??{}).map(([name,until])=><p key={name}>{name==='!dive'?'Tauchen':name==='!fish'?'Angeln':'Aktion'} wieder bereit: {new Date(String(until)).toLocaleTimeString('de-DE')}</p>)}</>}
 {tab==='Inventar'&&<div>{items.map(item=><article key={value(item,'InstanceId')}><ItemArtwork id={value(item,'IconId')??value(item,'ItemId')} name={value(item,'DisplayName')}/><h3>{value(item,'DisplayName')??value(item,'ItemId')}</h3><p>{describe(value(item,'Attributes'))}</p>{value(item,'Slot')!==null&&value(item,'Slot')!==undefined&&actionButton('equip','Anlegen',{instanceId:value(item,'InstanceId')})}</article>)}</div>}
 {tab==='Ausrüstung'&&<>{Object.entries(equipment).map(([slot,id])=><p key={slot}>{slotNames[Number(slot)]??slot}: {value(items.find(i=>value(i,'InstanceId')===id),'DisplayName')??'Gegenstand'} {actionButton('unequip','Ablegen',{slot:slotNames[Number(slot)]??slot})}</p>)}<label>Gegenstand <select id="live-item-id">{items.filter(i=>value(i,'Slot')!==null&&value(i,'Slot')!==undefined).map(i=><option key={value(i,'InstanceId')} value={value(i,'InstanceId')}>{value(i,'DisplayName')??value(i,'ItemId')}</option>)}</select></label><label>Slot <select id="live-slot">{slotNames.map(s=><option key={s}>{s}</option>)}</select></label><button disabled={!enabled} onClick={()=>void act('equip',{instanceId:(document.getElementById('live-item-id') as HTMLInputElement).value,slot:(document.getElementById('live-slot') as HTMLSelectElement).value})}>Item anlegen</button></>}
 {tab==='Itemsets'&&<>{actionButton('sets','Setfortschritt abrufen')}{(catalog?.sets??[]).map((s:any)=><article key={value(s,'Id')}><h3>{value(s,'Name')}</h3><p>{value(s,'Description')}</p>{(value(s,'Bonuses')??[]).map((b:any,n:number)=><p key={n}>{value(b,'RequiredPieces')} Teile: {describe(value(b,'Attributes')??value(b,'Bonus'))}</p>)}</article>)}</>}
 {tab==='Perks'&&<>{(catalog?.perks??[]).map((p:any)=><article key={value(p,'Id')}><h3>{value(p,'Name')}</h3><p>{String(perks[value(p,'Id')]??0)} / {value(p,'MaximumLevel')}</p>{actionButton('buyPerk','Perk kaufen',{perkId:value(p,'Id')})}</article>)}</>}
 {tab==='Boss'&&<p>Bossangriffe sind in dieser Testversion noch nicht verfügbar. Bosskämpfe im Plugin bleiben erhalten.</p>}
 {tab==='Kochen'&&<><p>Fischvorrat: {Object.entries(value(rpg,'Fish')??{}).map(([id,fish])=>id+': '+String(value(fish,'Quantity')??0)).join(' · ')||'leer'}</p><p>Sushi: {Object.entries(value(rpg,'Sushi')??{}).map(([id,count])=>(value((catalog?.recipes??[]).find((r:any)=>value(r,'Id')===id),'Name')??id)+': '+String(count)).join(' · ')||'leer'}</p>{(catalog?.recipes??[]).map((r:any)=><article key={value(r,'Id')}><h3>{value(r,'Name')}</h3><p>{value(r,'PearlCost')} Perlen · {Object.entries(value(r,'Ingredients')??{}).map(([fish,n])=>String(n)+' × '+fish).join(' + ')}</p><p>{value(r,'Effects')}</p>{actionButton('craft','Herstellen',{recipeId:value(r,'Id')})}{actionButton('eat','Benutzen',{recipeId:value(r,'Id')})}</article>)}{(value(rpg,'Buffs')??[]).map((b:any,n:number)=><p key={n}>Aktives Sushi: {value(b,'RecipeId')} · {value(b,'RemainingUses')} Teilnahmen · {describe(value(b,'Attributes'))}</p>)}</>}
 </section>{details&&<section aria-label="Plugin-Ergebnis"><h2>Ergebnis</h2>{details.stats&&<p>{describe(details.stats)}</p>}{value(details,'Balance')!==undefined&&<p>Perlenstand nach der Aktion: {String(value(details,'Balance'))}</p>}{value(details,'Outcome')&&<p>{value(details,'Outcome')}</p>}{details.sets&&<p>{details.sets.map((s:any)=>value(value(s,'Set'),'Name')+': '+String(value(s,'Equipped')??0)+' ausgerüstet').join(' · ')}</p>}</section>}
 <footer>Dein angezeigter Spielstand wird automatisch aktualisiert.</footer></main>;
}
