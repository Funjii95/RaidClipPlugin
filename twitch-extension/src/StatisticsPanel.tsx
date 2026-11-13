import type {Zone} from './ZonePanel';
export type PlayerStatistics={version:number;counters:Record<string,number>;zones:Record<string,Record<string,number>>;fish:Record<string,number>;bossKills:Record<string,number>;dungeons:Record<string,number>};
const field=(v:any,k:string)=>v?.[k]??v?.[k[0].toUpperCase()+k.slice(1)];
const counts=(v:any):Record<string,number>=>Object.fromEntries(Object.entries(v??{}).filter(([k,n])=>k.length<=80&&typeof n==='number'&&Number.isSafeInteger(n)&&n>=0)) as Record<string,number>;
export function readStatistics(raw:any):PlayerStatistics|undefined{
 if(raw==null)return;if(field(raw,'version')!==1)throw Error('Unbekannte Statistikversion.');
 return {version:1,counters:counts(field(raw,'counters')),zones:Object.fromEntries(Object.entries(field(raw,'zones')??{}).map(([id,v])=>[id,counts(v)])),fish:counts(field(raw,'fish')),bossKills:counts(field(raw,'bossKills')),dungeons:counts(field(raw,'dungeons'))};
}
const groups:Record<string,Record<string,string>>={
 'Gesamtübersicht':{dives:'Tauchgänge',successfulDives:'Erfolgreich',failedDives:'Fehlgeschlagen',legendaryFinds:'Legendäre Funde',pearlsEarned:'Erhaltene Perlen',salvagedItems:'Zerlegte Gegenstände'},
 'Kampf':{enemyKills:'Besiegte Gegner',lostFights:'Verlorene Kämpfe',damage:'Gesamtschaden',highestHit:'Höchster Treffer',criticalHits:'Kritische Treffer',damageTaken:'Erhaltener Schaden',damageBlocked:'Abgefangener Schaden',healing:'Ausgeteilte Heilung'},
 'Bosse':{bossParticipations:'Teilnahmen',bossKills:'Besiegte Bosse',bossDefeats:'Niederlagen',bossDamage:'Persönlicher Schaden',highestBossDamage:'Bester Bosskampf'},
 'Gruppen':{groupDives:'Gemeinsame Tauchgänge',groupBossKills:'Gruppenboss-Siege',groupHealing:'Gruppenheilung'}
};
const zoneLabels:Record<string,string>={dives:'Tauchgänge',success:'Erfolgreich',failure:'Fehlgeschlagen',kills:'Besiegte Gegner',materials:'Materialien',legendary:'Legendäre Funde'};
export function StatisticsPanel({statistics,zones=[],fish=[],bosses=[]}:{statistics?:PlayerStatistics;zones?:Zone[];fish?:{Id:string;Name:string}[];bosses?:{Id:string;Name:string}[]}){
 const n=(value?:number)=>(value??0).toLocaleString('de-DE');
 return <section aria-label="Persönliche Statistiken"><h3>Deine Abenteuer in Zahlen</h3><p>Neue Ereignisse werden seit Einführung dieser Statistik dauerhaft gezählt. Frühere Abenteuer werden nicht geschätzt.</p>{Object.entries(groups).map(([name,labels],i)=><details key={name} open={i===0}><summary>{name}</summary><dl>{Object.entries(labels).map(([key,label])=><div className="stat" key={key}><dt>{label}</dt><dd>{n(statistics?.counters[key])}</dd></div>)}</dl>{name==='Bosse'&&Object.entries(statistics?.bossKills??{}).map(([id,count])=><p key={id}>{bosses.find(b=>b.Id===id)?.Name??id}: {n(count)}</p>)}</details>)}
 <details><summary>Tauchgänge nach Biom</summary>{zones.map(z=><details key={z.Id}><summary>{z.Name} · {n(statistics?.zones[z.Id]?.dives)} Tauchgänge</summary><dl>{Object.entries(zoneLabels).map(([key,label])=><div className="stat" key={key}><dt>{label}</dt><dd>{n(statistics?.zones[z.Id]?.[key])}</dd></div>)}</dl></details>)}</details>
 <details><summary>Angeln · {n(statistics?.counters.fishCaught)} Fische</summary>{fish.map(f=><p key={f.Id}>{f.Name}: {n(statistics?.fish[f.Id])}</p>)}</details>
 <details><summary>Dungeons</summary>{[['normal','Normal'],['heroic','Heroisch'],['mythic','Mythisch']].map(([id,label])=><p key={id}>{label}: {n(statistics?.dungeons[id])}</p>)}</details></section>;
}
