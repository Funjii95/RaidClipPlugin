import {useState} from 'react';
import type {RpgDataSource,InventoryItem,CharacterStats} from './types';
import {labels,numberStat} from './presentation';
export function ItemComparison({source,item}:{source:RpgDataSource;item:InventoryItem}){
 const [data,setData]=useState<{current:Record<string,number>;proposed:Record<string,number>;currentHp:number;proposedHp:number}>(),[busy,setBusy]=useState(false),[error,setError]=useState('');
 if(!item.slot||!source.readPlugin)return null;
 return <><button disabled={busy} onClick={()=>{setBusy(true);setError('');void source.readPlugin!('compareEquipment',{instanceId:item.instanceId,slot:item.slot!}).then(setData).catch(e=>setError(e.message)).finally(()=>setBusy(false));}}>Mit Ausrüstung vergleichen</button>
 {error&&<p role="alert">{error}</p>}{data&&<details open><summary>Änderung der effektiven Werte · vom Plugin berechnet</summary><p>Leben: {data.currentHp} → {data.proposedHp}</p><ul>{Object.keys(data.proposed).filter(k=>data.proposed[k]!==data.current[k]).map(k=>{const key=k[0].toLowerCase()+k.slice(1);return <li key={k}>{labels[key as keyof CharacterStats]??k}: {numberStat(key,data.current[k])} → {numberStat(key,data.proposed[k])}</li>;})}</ul></details>}</>;
}
