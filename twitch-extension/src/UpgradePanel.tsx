import {ItemArtwork} from './item-art';
import type {InventoryItem,MaterialStock,Action,CharacterStats} from './types';
import {labels,numberStat} from './presentation';
export const materialNames:Record<string,string>={pearl_dust:'Perlenstaub',white_pearl:'Weiße Perle',blue_pearl:'Blaue Perle',red_pearl:'Rote Perle',luminous_scale:'Leuchtende Flossenschuppe',rune_shell:'Runenmuschel',eternal_depth_core:'Kern der Ewigen Tiefe',true_coral_core:'Wahrer Korallenkern',cursed_silk:'Verfluchter Seidenfaden',abyss_shard:'Abyss-Scherbe',reinforcement_core:'Verstärkungskern'};
export const materialKeys:Record<string,keyof MaterialStock>={pearl_dust:'pearlDust',white_pearl:'whitePearls',blue_pearl:'bluePearls',red_pearl:'redPearls',luminous_scale:'luminousScale',rune_shell:'runeShell',eternal_depth_core:'eternalDepthCore',true_coral_core:'trueCoralCore',cursed_silk:'cursedSilk',abyss_shard:'abyssShard',reinforcement_core:'reinforcementCore'};
export type UpgradeCost={TargetLevel:number;Pearls:number;Materials:Record<string,number>};
export type UpgradeRules={maximumLevel:number;bonuses:number[];costsByRarity:Record<string,UpgradeCost[]>;refinement:UpgradeCost};
export type MaterialDefinition={Id:string;Name:string;Rarity:number;Description:string;Sources:string[]};
const rarities=['Common','Uncommon','Rare','Epic','Legendary','True','Cursed','Abyss','Eternal','Divine'];
export function MaterialInventory({stock,catalog}:{stock?:MaterialStock;catalog?:MaterialDefinition[]}){
 return <div className="cards material-inventory">{(catalog??Object.keys(materialNames).map(Id=>({Id,Name:materialNames[Id],Rarity:1,Description:'',Sources:[]}))).map(m=><article className={`item ${rarities[m.Rarity]}`} key={m.Id}>
  <ItemArtwork id={m.Id} name={m.Name}/><h3>{m.Name}</h3><strong>{stock?(stock[materialKeys[m.Id]]??0).toLocaleString('de-DE'):'–'} im Besitz</strong><p>{m.Description}</p><small>Fundorte: {m.Sources.join(', ')||'Plugin verbinden'}</small>
 </article>)}</div>;
}
export function EquipmentUpgrade({item,rules,stock,points,busy,allowed,onSubmit}:{item?:InventoryItem;rules?:UpgradeRules;stock?:MaterialStock;points?:string;busy:boolean;allowed:(type:Action['type'])=>boolean;onSubmit:(action:Action)=>void}){
 if(!item)return <p>Bitte einen Ausrüstungsgegenstand auswählen.</p>;
 if(item.rarity==='Divine')return <><h3>{item.name}</h3><ItemArtwork id={item.itemId} name={item.name}/><p>Nicht aufwertbar · Göttliche Ausrüstung besitzt feste Werte.</p></>;
 const level=item.upgradeLevel??0;const refine=level===9;const cost=refine?rules?.refinement:rules?.costsByRarity[item.rarity]?.[level];
 const enoughPearls=!!cost&&!!points&&BigInt(points)>=BigInt(cost.Pearls);
 const enoughMaterials=!!stock&&!!cost&&Object.entries(cost.Materials).every(([id,n])=>(stock[materialKeys[id]]??0)>=n);
 const baseMultiplier=item.isRefined?1.5:1+(rules?.bonuses[level]??0);const nextMultiplier=refine?1.5:1+(rules?.bonuses[level+1]??0);
 return <><h3>{item.name} · +{level}{item.isRefined?' · Veredelt':''}</h3><ItemArtwork id={item.itemId} name={item.name}/>
  <p>Jede Aufwertung gelingt garantiert. Setboni, Spezialeffekte und negative Fluchwerte bleiben unverändert.</p>
  {item.isRefined?<p>Vollständig aufgewertet und veredelt.</p>:!cost?<p>Aufwertungsregeln werden vom Plugin noch nicht übermittelt.</p>:<>
   <h4>{refine?'Einmalige Endveredelung: +45 % → +50 %':`Nächste Stufe: +${cost.TargetLevel}`}</h4>
   <p className={enoughPearls?'':'missing'}>{cost.Pearls.toLocaleString('de-DE')} normale Perlen{!enoughPearls?' · fehlen':''}</p>
   <ul>{Object.entries(cost.Materials).map(([id,n])=><li key={id} className={(stock?.[materialKeys[id]]??0)>=n?'':'missing'}>{n} × {materialNames[id]} · vorhanden: {stock?.[materialKeys[id]]??'–'}</li>)}</ul>
   <table><thead><tr><th>Attribut</th><th>Jetzt</th><th>Danach</th></tr></thead><tbody>{Object.entries(item.attributes).filter(([,v])=>v!==0).map(([key,value])=><tr key={key}><td>{labels[key as keyof CharacterStats]??key}</td><td>{numberStat(key,value!)}</td><td>{numberStat(key,value!>0?Number((value!/baseMultiplier*nextMultiplier).toFixed(4)):value!)}</td></tr>)}</tbody></table>
   <button disabled={busy||!enoughPearls||!enoughMaterials||!allowed(refine?'refine':'upgrade')} onClick={()=>onSubmit({type:refine?'refine':'upgrade',id:item.instanceId,expectedLevel:level})}>{busy?'Wird gespeichert …':refine?'Jetzt veredeln':`Garantiert auf +${cost.TargetLevel} aufwerten`}</button>
  </>}
 </>;
}
