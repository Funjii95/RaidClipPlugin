import {useState} from 'react';
import type {BackendRpgDataSource} from './remote/BackendRpgDataSource';
export function EternalAdmin({source,target,disabled}:{source:BackendRpgDataSource;target:string;disabled:boolean}){
 const [message,setMessage]=useState(''),[busy,setBusy]=useState(false),[zone,setZone]=useState('sunny_lagoon'),[enemy,setEnemy]=useState('0'),[value,setValue]=useState('0.03'),[items,setItems]=useState<{InstanceId:string;DisplayName:string;equipped:boolean}[]>([]);
 const zones=source.getSnapshot().remote?.zones??[];
 async function run(kind:string,args:Record<string,string>){setBusy(true);try{const result=await source.readPlugin(kind,args);setMessage(result.message??'Bestätigt.');if(result.items)setItems(result.items);if(result.result)setMessage(JSON.stringify(result.result));}catch(e){setMessage(e instanceof Error?e.message:String(e));}finally{setBusy(false);}}
 return <section aria-label="Eternal und Divine Verwaltung"><h2>Eternal &amp; Divine</h2><p>Divine-Vergabe nur an die authentifizierte Kanalinhaber-ID. Einzelstücke stehen im Itemkatalog. Beim Vervollständigen bleiben vorhandene Teile erhalten; nur fehlende Teile werden ergänzt.</p>
 <button disabled={disabled||busy} onClick={()=>void run('adminDivine',{operation:'grantSet',targetUserId:target})}>Göttliches Set vervollständigen · 8 Teile</button>
 <button disabled={disabled||busy} onClick={()=>void run('adminDivine',{operation:'list',targetUserId:target})}>Göttliche Items anzeigen</button>
 <button disabled={disabled||busy} onClick={()=>void run('adminDivine',{operation:'remove',targetUserId:target})}>Alle göttlichen Items entfernen</button>
 <ul>{items.map(i=><li key={i.InstanceId}>{i.DisplayName}{i.equipped?' · ausgerüstet':''}</li>)}</ul>
 <details><summary>Gebiete prüfen und konfigurieren</summary><p>Gegner-, Truhen-, Mimic- und Lootproben sind Simulationen ohne gespeicherte Belohnungen.</p>
 <label>Debuggebiet<select value={zone} onChange={e=>setZone(e.target.value)}>{zones.map(z=><option key={z.Id} value={z.Id}>{z.Name} · Level {z.Level}</option>)}</select></label>
 <label>Gegner<select value={enemy} onChange={e=>setEnemy(e.target.value)}>{(zones.find(z=>z.Id===zone)?.Enemies??[]).map((n,i)=><option key={n} value={i}>{n}</option>)}</select></label>
 {['enemy','chest','mimic','simulate'].map((operation,i)=><button key={operation} disabled={disabled||busy} onClick={()=>void run('adminZoneDebug',{zoneId:zone,operation,enemyIndex:enemy,value:'0'})}>{['Gegner simulieren','Truhe simulieren','Mimic simulieren','1.000 Eternal-Würfe simulieren'][i]}</button>)}
 <label>Chance in Prozent<input type="number" min={0} max={100} step={0.01} value={value} onChange={e=>setValue(e.target.value)}/></label>
 {['chestChance','mimicChance','enemyChance','eternalNormal','eternalElite','eternalMimic','eternalEndgame'].map((operation,i)=><button key={operation} disabled={disabled||busy} onClick={()=>void run('adminZoneDebug',{zoneId:zone,operation,enemyIndex:enemy,value})}>{['Truhenchance speichern','Mimic-Chance speichern','Gegnerchance speichern','Eternal normal speichern','Eternal Elite speichern','Eternal Mimic speichern','Eternal Endgame speichern'][i]}</button>)}
 </details><p role="status">{message}</p></section>;
}
