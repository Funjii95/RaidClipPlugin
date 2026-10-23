import {useState} from 'react';
import type {Activity,DemoState} from './types';
import {availablePoints,earnedPoints,spentPoints,talents,blockedReason} from './talents';
const branches:{id:Activity;name:string;icon:string;subtitle:string}[]=[
 {id:'Boss',name:'Kämpfen',icon:'⚔',subtitle:'Stärke & Überleben'},
 {id:'Fishing',name:'Angeln',icon:'⚓',subtitle:'Geduld & Entdeckung'},
 {id:'Dive',name:'Tauchen',icon:'♆',subtitle:'Schätze der Tiefe'}];
const symbols:Record<string,string>={damage:'⚔',health:'♥',crit:'◎',boss:'☠',attackCooldown:'⚡',lifeleech:'♥',doubleAttack:'✦',catch:'⚓',rareFish:'♓',doubleFish:'♓',qualityFish:'✧',ingredient:'🍣',fishPearls:'◉',legendaryFish:'♛',underwaterDamage:'♆',divePearls:'◈',loot:'⚒',treasure:'✧',diveCooldown:'≋',underwaterDodge:'◌',enemyLoot:'♆'};
const effectLabels:Record<string,string>={damage:'Schaden',health:'maximale HP',crit:'Krit-Chance',boss:'Bossschaden',attackCooldown:'kürzerer Angriffscooldown',lifeleech:'Lebensraub',doubleAttack:'Doppelschlag-Chance',catch:'weniger Fehlfänge',rareFish:'seltene Fische',doubleFish:'Zusatzfang-Chance',qualityFish:'hochwertige Fische',ingredient:'Zutatenfisch-Chance',reserveFish:'Zusatzfang-Chance',legendaryFish:'legendäre Fische',underwaterDamage:'Unterwasserschaden',diveMaterials:'relative Materialfundchance',loot:'Item-Dropchance',treasure:'Schatzchance',diveCooldown:'kürzerer Tauchcooldown',underwaterDodge:'Unterwasser-Ausweichen',enemyLoot:'Zusatzbeute-Chance'};
export function TalentTree({state,demo,busy,onLearn,canLearn=false}:{state:DemoState;demo:boolean;busy:boolean;canLearn?:boolean;onLearn:(id:string)=>void}) {
 const [branch,setBranch]=useState<Activity>('Boss');const ranks=state.talents??{};
 const linked=!demo&&state.remote?.talentCatalogVersion===1&&state.talents!==undefined;
 const catalog=linked?state.remote!.talentCatalog!:talents;
 const ready=demo||linked;const selected=branches.find(b=>b.id===branch)!;
 const nodes=catalog.filter(t=>t.branch===branch);const branchSpent=nodes.reduce((n,t)=>n+(ranks[t.id]??0),0);
 return <section className="talent-tree" aria-label="Talentbaum">
 <div className="talent-toolbar">
 <div className="talent-points" role="status" aria-label="Verbleibende Talentpunkte" aria-live="polite" aria-atomic="true"><span aria-hidden="true">✦</span><span>Verbleibende Talentpunkte</span><strong>{ready?availablePoints(state.player.level,ranks):'–'}</strong></div>
 <p className="talent-points-detail">{ready?`${spentPoints(ranks)} vergeben · ${earnedPoints(state.player.level)} verdient`:'Talentpunkte: noch nicht vom Server übermittelt'}</p>
 </div>
 <div className="talent-tabs" aria-label="Talentbereiche">{branches.map(b=><button key={b.id} aria-pressed={branch===b.id} aria-controls="talent-panel" onClick={()=>setBranch(b.id)}><span aria-hidden="true">{b.icon}</span> {b.name}</button>)}</div>
 <div id="talent-panel" className={`talent-board talent-board-${branch}`} aria-label={selected.name}>
 <header className="talent-board-heading"><small>{selected.subtitle}</small><h3>{selected.name}</h3><span>33 Punkte gesamt</span></header>
 <div className="talent-nodes">{nodes.map(t=>{
 const rank=ranks[t.id]??0;const reason=blockedReason(t.id,state.player.level,ranks);const learnable=ready&&(demo||canLearn)&&!busy&&!reason;
 return <article key={t.id} className={`talent-node ${rank>0?'learned':''} ${rank===t.max?'complete':''} ${t.requires.length?'has-parent':''} ${learnable?'available':''}`}>
 <div className="talent-medallion" aria-hidden="true">{symbols[t.effect]??'✦'}</div>
 <span className="talent-rank">{rank} / {t.max}</span>
 <div className="talent-caption"><h4>{t.name}</h4><p>+{t.value} % {effectLabels[t.effect]??t.effect}<br/>pro Punkt</p>{['underwaterDamage','underwaterDodge'].includes(t.effect)&&<small>Keine Bosse</small>}</div>
 <details className="talent-details"><summary>Details</summary><p>{t.description}</p><p>{t.requires.length?`Voraussetzung: ${t.requires.map(id=>catalog.find(v=>v.id===id)?.name??id).join(' und ')} Rang 3`:'Keine Voraussetzung'}</p></details>
 <p className="talent-requirement">{t.requires.length?'Vorgänger jeweils Rang 3':'Einstiegstalent'}</p>
 <button className="talent-learn" disabled={!learnable} aria-label={`${t.name}: ${!ready?'Live noch nicht verfügbar':reason??'Lernen · 1 Talentpunkt'}`} onClick={()=>onLearn(t.id)}>{!ready?'Live noch nicht verfügbar':rank===t.max?'Voll ausgebaut':reason?'Gesperrt':!demo&&!canLearn?'Nicht verbunden':'✦ 1 Punkt'}</button>
 </article>;
 })}</div>
 <div className="talent-branch-total">{ready?branchSpent:'–'} / 33 <span>Punkte vergeben</span></div>
 </div>
 <p className="talent-footnote">{ready?`${demo?'Demo · ':''}1 Punkt pro Levelaufstieg, rückwirkend berücksichtigt. Maximal 33 Punkte je Bereich. Voraussetzungen: Vorgängertalente jeweils Rang 3.`:'Talentvorschau · Backend-Anbindung ausstehend. Punktevergabe und Boni sind live noch nicht verfügbar.'}</p>
 </section>;
}
