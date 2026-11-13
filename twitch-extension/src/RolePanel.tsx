import type {Action} from './types';
export type CombatRole='tank'|'healer'|'damage';
export function RolePanel({selected,busy,allowed,onSelect}:{selected?:CombatRole;busy:boolean;allowed:boolean;onSelect:(role:CombatRole)=>void}){
 return <section className="role-panel" aria-label="Kampfrolle"><h3>Deine Rolle</h3><p>Eine Rolle ergänzt deine Ausrüstung. Alle Rollen bleiben solo spielbar. Im laufenden Bosskampf ist der Wechsel gesperrt.</p><div className="actions">{([['tank','🛡 Tank','Rüstung, Schadensschutz und mehr Bedrohung'],['healer','✚ Heiler','Stärkere Heilung und Unterstützung'],['damage','⚔ Schaden','Mehr offensiver Schaden']] as const).map(([id,label,text])=><button key={id} aria-pressed={selected===id} disabled={busy||!allowed||selected===id} onClick={()=>onSelect(id)}><strong>{label}</strong><small>{text}</small></button>)}</div>{!selected&&<small>Noch keine Rolle gewählt; dein bisheriger Spielstil bleibt aktiv.</small>}</section>;
}
