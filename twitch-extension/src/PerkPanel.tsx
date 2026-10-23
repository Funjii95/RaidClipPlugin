import {useState} from 'react';
import type {Perk} from './types';
import {statsText} from './presentation';
import './perks.css';

const groups=[['all','✦','Alle Perks'],['combat','⚔','Kampf'],['fishing','⚓','Angeln'],['diving','◉','Tauchen'],['support','♥','Support']] as const;
const legacy:Record<string,[string,string]>={damage:['combat','✹'],crit:['combat','✦'],critdamage:['combat','✷'],boss:['combat','☠'],luck:['support','☘'],dive:['diving','◉'],fishing:['fishing','⚓']};
const categoryOf=(p:Perk)=>p.presentation?.category??legacy[p.id]?.[0]??'support';
const symbolOf=(p:Perk)=>p.presentation?.symbol??legacy[p.id]?.[1]??'✧';
const format=(n:number)=>n.toLocaleString('de-DE');
export function PerkPanel({perks,price,affordable,canBuy,busy,demo,pearls,canBuyMax,onBuy}:{perks:Perk[];price:(p:Perk)=>number;affordable:(n:number)=>boolean;canBuy:boolean;busy:boolean;demo:boolean;pearls:string;canBuyMax:boolean;onBuy:(id:string,maximum?:boolean)=>void}){
 const [category,setCategory]=useState('all'),[search,setSearch]=useState(''),[selected,setSelected]=useState(''),[filter,setFilter]=useState('all');
 const visible=perks.filter(p=>(category==='all'||categoryOf(p)===category)&&`${p.name} ${p.presentation?.label??''} ${statsText(p.bonus).join(' ')}`.toLocaleLowerCase('de').includes(search.toLocaleLowerCase('de'))&&(filter==='all'||(filter==='max'?p.level>=p.maxLevel:p.level<p.maxLevel)));
 const current=visible.find(p=>p.id===selected)??visible[0];
 const bonus=(p:Perk,rank:number)=>p.presentation?.value?`${(p.presentation.value*rank).toLocaleString('de-DE',{maximumFractionDigits:3})} ${p.presentation.label}`:statsText(Object.fromEntries(Object.entries(p.bonus).map(([k,v])=>[k,Number(((v??0)*rank).toFixed(4))]))).join(', ')||'Bonusdaten nicht übermittelt';

 return <section className="perk-panel" aria-label="Perk-Verwaltung">
  <div className="perk-banner"><div><strong>Perks</strong><span>Otter RPG · Deine passiven Vorteile</span></div></div>
  <div className="perk-toolbar"><span>◉ Verfügbare {demo?'Demo-Perlen':'Perlen'}: <b>{pearls}</b></span><label><span className="perk-sr">Perks suchen</span><input placeholder="Perks suchen …" value={search} onChange={e=>setSearch(e.target.value)}/></label><select aria-label="Perks filtern" value={filter} onChange={e=>setFilter(e.target.value)}><option value="all">Alle Ränge</option><option value="upgrade">Ausbaubar</option><option value="max">Maximallevel</option></select></div>
  <div className="perk-layout"><aside className="perk-categories" aria-label="Perk-Kategorien">{groups.map(([id,icon,label])=><button key={id} aria-pressed={category===id} onClick={()=>setCategory(id)}><span>{icon}</span>{label}<small>{perks.filter(p=>id==='all'||categoryOf(p)===id).length}</small></button>)}</aside>
  <div className="perk-cards">{visible.map(p=><article className="perk-card" key={p.id} data-selected={current?.id===p.id} aria-label={p.name}>
   <button type="button" className="perk-select" aria-pressed={current?.id===p.id} aria-label={`${p.name} – Details anzeigen`} onClick={()=>setSelected(p.id)}>
    <span className={`perk-symbol perk-symbol-${categoryOf(p)}`} aria-hidden="true">{symbolOf(p)}</span><div><strong>{p.name}</strong><span className="perk-passive">Passiv</span><p>{bonus(p,1)} pro Rang</p></div><span className="perk-card-rank">{`${format(p.level)} / ${format(p.maxLevel)}${p.level>=p.maxLevel?' · MAX':''}`}</span><progress aria-label={`${p.name}: Rang ${p.level} von ${p.maxLevel}`} value={p.level} max={Number.isFinite(p.maxLevel)?p.maxLevel:undefined}/>
   </button>
   <div className="perk-purchase"><button type="button" className="perk-buy" disabled={!canBuy||busy||p.level>=p.maxLevel||(Number.isFinite(price(p))&&!affordable(price(p)))} onClick={()=>{setSelected(p.id);onBuy(p.id);}}>{p.level>=p.maxLevel?'Maximallevel':Number.isFinite(price(p))?`Aufwerten · ${format(price(p))} ${demo?'Demo-Perlen':'Perlen'}`:'Aufwerten · Preisprüfung im Spiel'}</button><button type="button" className="perk-buy-max" title="Alle noch bezahlbaren Ränge bis maximal 5/5 kaufen" aria-label={`Max. kaufen: ${p.name}`} disabled={!canBuyMax||!canBuy||busy||p.level>=p.maxLevel||!Number.isFinite(price(p))||!affordable(price(p))} onClick={()=>{setSelected(p.id);onBuy(p.id,true);}}>Max.</button></div>
   {Number.isFinite(price(p))&&!affordable(price(p))&&p.level<p.maxLevel&&<small className="perk-card-hint">Nicht genügend Perlen</small>}
  </article>)}{!visible.length&&<p className="perk-empty">Keine passenden Perks gefunden.</p>}</div>
  {current&&<aside className="perk-detail" aria-label="Perk-Details"><div className="perk-portrait"><span className={`perk-symbol perk-symbol-${categoryOf(current)}`} aria-hidden="true">{symbolOf(current)}</span></div><h3>{current.name}</h3><span className="perk-passive">Passiv</span><h4>Rang {format(current.level)} / {format(current.maxLevel)}</h4><p>{bonus(current,1)} pro Rang</p><small>Maximal 5 Ränge · Kauf mit Perlen. Einsatzgrenzen und bestehende Gesamtgrenzen gelten weiterhin.</small><div className="perk-detail-box"><h4>Aktuelle Boni</h4><p>{current.level>0?bonus(current,current.level):'Noch nicht freigeschaltet'}</p></div><div className="perk-detail-box"><h4>Rang-Fortschritt</h4><ol className="perk-ranks">{Array.from({length:Number.isFinite(current.maxLevel)?Math.min(100,current.maxLevel):0},(_,i)=>i+1).map(rank=><li key={rank} className={rank<=current.level?'earned':''}><b>Rang {rank}</b><span>{bonus(current,rank)}</span><span aria-label={rank<=current.level?'Erreicht':'Offen'}>{rank<=current.level?'✓':'○'}</span></li>)}</ol></div></aside>}
 </div></section>;
}
