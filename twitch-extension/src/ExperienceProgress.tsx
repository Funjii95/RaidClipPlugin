import './experience-progress.css';
export type ProgressionRules={ExperiencePerLevel:number;MaximumLevel:number};
export function experienceProgress(level:number,experience:string|number,rules?:ProgressionRules){
 if(!rules||!Number.isInteger(level)||level<1||!Number.isSafeInteger(rules.ExperiencePerLevel)||rules.ExperiencePerLevel<1||!Number.isInteger(rules.MaximumLevel)||rules.MaximumLevel<1||!/^\d+$/.test(String(experience)))return;
 if(level>=rules.MaximumLevel)return {max:true,current:0n,required:0n,next:rules.MaximumLevel,percentage:100};
 const unit=BigInt(rules.ExperiencePerLevel),n=BigInt(level),start=unit*(n-1n)*n/2n,end=unit*n*(n+1n)/2n;
 const required=end-start,raw=BigInt(experience)-start,current=raw<0n?0n:raw>required?required:raw;
 return {max:false,current,required,next:level+1,percentage:Number(current*10000n/required)/100};
}
export function ExperienceProgress({level,experience,rules}:{level:number;experience:string|number;rules?:ProgressionRules}){
 const p=experienceProgress(level,experience,rules);if(!p)return <small>EP-Fortschritt wird geladen …</small>;
 return <div className="experience-progress">{p.max?<strong>Stufe {p.next} · Maximalstufe erreicht</strong>:<><small>{p.current.toLocaleString('de-DE')} / {p.required.toLocaleString('de-DE')} EP bis Stufe {p.next}</small><progress aria-label={`EP bis Stufe ${p.next}`} value={p.percentage} max={100}/></>}</div>;
}
