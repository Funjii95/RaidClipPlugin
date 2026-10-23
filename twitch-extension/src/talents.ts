import catalog from './reference/talents.json';
import type {Activity,CharacterStats} from './types';
export type TalentRanks=Record<string,number>;
export interface Talent {id:string;name:string;branch:Activity;max:number;value:number;effect:string;description:string;requires:string[];bonus:Partial<CharacterStats>;}
export const catalogVersion=1;
export const talents:Talent[]=catalog.map(t=>({...t,branch:t.branch as Activity,bonus:({damage:{damage:t.value},crit:{critChance:t.value},boss:{bossDamage:t.value}} as Record<string,Partial<CharacterStats>>)[t.effect]??{}}));
export function earnedPoints(level:number):number{return Number.isSafeInteger(level)?Math.max(0,Math.min(100,level)-1):0;}
export function spentPoints(ranks:TalentRanks):number{return Object.values(ranks).reduce((n,v)=>n+(Number.isSafeInteger(v)&&v>0?v:0),0);}
export function availablePoints(level:number,ranks:TalentRanks):number{return Math.max(0,earnedPoints(level)-spentPoints(ranks));}
export function blockedReason(id:string,level:number,ranks:TalentRanks):string|undefined {
 const t=talents.find(t=>t.id===id);if(!t)return 'Unbekanntes Talent';
 if((ranks[id]??0)>=t.max)return 'Maximalrang erreicht';
 const missing=t.requires.find(r=>(ranks[r]??0)<3);
 if(missing)return `Voraussetzung: ${talents.find(t=>t.id===missing)!.name} Rang 3`;
 if(availablePoints(level,ranks)<1)return 'Keine Talentpunkte verfügbar';
 if(talents.filter(v=>v.branch===t.branch).reduce((n,v)=>n+(ranks[v.id]??0),0)>=33)return 'Bereich vollständig ausgebaut';
}
export function learnTalent(id:string,level:number,ranks:TalentRanks):TalentRanks {
 const reason=blockedReason(id,level,ranks);if(reason)throw Error(reason);
 return {...ranks,[id]:(ranks[id]??0)+1};
}
export function talentBonus(ranks:TalentRanks,activity:Activity):Partial<CharacterStats> {
 const result:Partial<CharacterStats>={};
 for(const t of talents.filter(t=>t.effect==='damage'||t.effect==='crit'||t.branch===activity))for(const [key,value]of Object.entries(t.bonus)){
 const k=key as keyof CharacterStats;const rank=ranks[t.id]??0;result[k]=(result[k]??0)+value*(Number.isSafeInteger(rank)?Math.max(0,Math.min(t.max,rank)):0);
 }return result;
}
export function underwaterDodgeChance(ranks:TalentRanks):number {
 const rank=ranks['dive-dodge']??0;return Number.isSafeInteger(rank)?Math.max(0,Math.min(5,rank))*4:0;
}
export function avoidsUnderwaterHit(ranks:TalentRanks,rollPercent:number):boolean {
 return Number.isFinite(rollPercent)&&rollPercent>=0&&rollPercent<100&&rollPercent<underwaterDodgeChance(ranks);
}
