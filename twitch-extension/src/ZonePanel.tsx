import {useState} from 'react';
import art from './zone-art.json';
import {materialNames} from './UpgradePanel';
export type Zone={Id:string;Name:string;Level:number;Enemies:string[];Materials:string[];MaximumRarity:number;ChestChance:number;MimicChance:number};
export function ZonePanel({zones,diving,fishing,busy,cooldown,fishingCooldown=0,onSelect,onStart}:{zones:Zone[];diving:string;fishing:string;busy:boolean;cooldown:number;fishingCooldown?:number;onSelect:(activity:'dive'|'fish',zone:string)=>Promise<void>;onStart:(activity:'dive'|'fish')=>void}){
 const [activity,setActivity]=useState<'dive'|'fish'>('dive'),[error,setError]=useState('');
 const selected=activity==='dive'?diving:fishing;
 const remaining=activity==='dive'?cooldown:fishingCooldown;
 return <section className="zone-panel"><h2>Die zehn Reiche der Tiefe</h2><p>Freie Gebietswahl für jedes Level · kostenlos · Ausrüstung ausschließlich nach gewonnenen Kämpfen.</p>
 <div role="group" aria-label="Aktivität"><button aria-pressed={activity==='dive'} onClick={()=>setActivity('dive')}>Tauchen</button><button aria-pressed={activity==='fish'} onClick={()=>setActivity('fish')}>Angeln</button></div>
 <div className="zone-grid">{zones.map(z=><article key={z.Id} className={selected===z.Id?'zone selected':'zone'}>
 <img src={art[z.Id as keyof typeof art]?.src} alt={z.Name} loading="lazy" width={280} height={350}/><h3>{z.Name}</h3><p>Gegnerlevel {z.Level} · Gefahr {z.Level/10}/10</p>
 <details><summary>Gebiet erkunden</summary><p>Gegner: {z.Enemies.join(', ')}</p><p>Materialien: {z.Materials.map(m=>materialNames[m]??m).join(', ')}</p><p>Truhen: {z.ChestChance} % · Mimics: {z.MimicChance} %</p><p>Setdrops: {z.Level===100?'Eternal, Abyss und Wahr':z.Level>=90?'Abyss und Wahr':z.Level>=80?'Wahre Sets':z.Level>=70?'Cursed-Ausrüstung':z.Level>=50?'Legendäre Sets':'Gebietsausrüstung'}</p></details>
 <button disabled={busy} aria-pressed={selected===z.Id} onClick={()=>{setError('');void onSelect(activity,z.Id).catch(e=>setError(String(e.message??e)));}}>{selected===z.Id?'Ausgewählt':'Gebiet auswählen'}</button>
 {selected===z.Id&&<button className="primary" disabled={busy||remaining>0} onClick={()=>onStart(activity)}>{remaining>0?`Abklingzeit: ${Math.ceil(remaining)} s`:activity==='dive'?'Hier tauchen':'Hier angeln'}</button>}
 </article>)}</div><p role="status">{error}</p></section>;
}
