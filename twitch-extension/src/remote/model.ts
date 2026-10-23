import type {Activity,CharacterStats,CollectorStats,DemoState,InventoryItem,ItemSet,Perk,Rarity,Slot,SushiCategory,SushiRecipe} from '../types';
export type RecipeView=Omit<SushiRecipe,'activity'|'category'|'bossUses'|'attributes'|'passiveHealing'|'chatHealing'|'bossDamageReduction'|'bossLifeleech'> & Partial<Pick<SushiRecipe,'activity'|'category'|'bossUses'|'attributes'|'passiveHealing'|'chatHealing'|'bossDamageReduction'|'bossLifeleech'>> & {effectsText?:string;durationSeconds?:number;allActivities?:boolean};
export interface RemoteView {
 zones?:import("../ZonePanel").Zone[];selectedDivingZone?:string;selectedFishingZone?:string;collectorItems?:{Id:string;Name:string;Rarity:number}[];
 perkMaxPurchase?:boolean;itemUpgrade?:import("../UpgradePanel").UpgradeRules;materialCatalog?:import("../UpgradePanel").MaterialDefinition[];
 talentCatalogVersion?:number;talentCatalog?:import("../talents").Talent[];
 loaded:boolean; connection:'loading'|'unauthorized'|'offline'|'disconnected'|'ready'|'readonly'|'error'; message:string;
 updatedAt?:string; pointsText:string; pointsExact:string; experienceText?:string; sets:ItemSet[]; recipes:RecipeView[];
 stats?:CharacterStats; collectors?:CollectorStats; perkPrices:Record<string,number>;selfDeletionEnabled?:boolean;
 supported:string[]; pending:boolean; pendingState?:string; syncing:boolean; bossKnown:boolean; hotKnown:boolean;
 adventurePreview?:{dive:{stake:string;chance:number;rarities:string[]};fish:{stake:string;chance:number;rarities:string[]}};
}
export const unknown=()=>Number.NaN;
export const field=(obj:any,name:string):any=>obj?.[name]??obj?.[name[0].toLowerCase()+name.slice(1)];
const own=(obj:any,name:string)=>obj&&Object.keys(obj).find(k=>k.toLowerCase()===name.toLowerCase());
const object=(v:any,label:string):Record<string,any>=>{if(!v||typeof v!=='object'||Array.isArray(v))throw Error(label+' fehlt oder ist ungültig.');return v;};
const string=(v:any,label:string):string=>{if(typeof v!=='string'||!v)throw Error(label+' fehlt.');return v;};
const number=(v:any,label:string):number=>{if(typeof v!=='number'||!Number.isFinite(v))throw Error(label+' ist ungültig.');return v;};
const optionalNumber=(v:any)=>typeof v==='number'&&Number.isFinite(v)?v:unknown();
const enumValue=<T extends string>(v:any,names:readonly T[],label:string):T=>{if(typeof v==='number'&&Number.isInteger(v)&&names[v])return names[v];if(names.includes(v))return v;throw Error(label+' ist unbekannt.');};
const rarities=['Common','Uncommon','Rare','Epic','Legendary','True','Cursed','Abyss','Eternal','Divine'] as const;
const slots=['Weapon','Helmet','Chest','Gloves','Boots','Ring1','Ring2','Amulet'] as const;
const activities=['Dive','Fishing','Boss'] as const;
const time=(v:any)=>typeof v==='string'&&Number.isFinite(Date.parse(v))?Date.parse(v):unknown();
export function attributes(v:any):Partial<CharacterStats>{const result:Record<string,number>={};for(const [key,value]of Object.entries(object(v,'Attributdaten')))result[key[0].toLowerCase()+key.slice(1)]=number(value,'Attribut');return result;}
function counts(v:any,label:string){const result:Record<string,number>={};for(const [key,value]of Object.entries(object(v,label))){const n=number(value,label);if(!Number.isSafeInteger(n)||n<0)throw Error(label+' ist ungültig.');result[key]=n;}return result;}
export function inventoryItem(v:any):InventoryItem {
 object(v,'Gegenstand');const id=string(field(v,'InstanceId'),'Item-ID');
 const effects=field(v,'Effects');if(!Array.isArray(effects))throw Error('Itemeffekte fehlen.');
 const upgradeLevel=field(v,'UpgradeLevel')??0;if(!Number.isInteger(upgradeLevel)||upgradeLevel<0||upgradeLevel>9)throw Error('Ungültige Aufwertungsstufe.');
 const isRefined=field(v,'IsRefined')===true;if(isRefined&&upgradeLevel!==9)throw Error('Ungültige Veredelung.');
 return {upgradeLevel,isRefined,instanceId:id,itemId:string(field(v,'ItemId'),'Itemkatalog-ID'),name:string(field(v,'DisplayName')||field(v,'ItemId'),'Itemname'),description:field(v,'Description')??'',icon:field(v,'IconId')??'✧',
 rarity:enumValue<Rarity>(field(v,'Rarity'),rarities,'Seltenheit'),slot:field(v,'Slot')==null?undefined:enumValue<Slot>(field(v,'Slot'),slots,'Slot'),kind:enumValue(field(v,'Kind'),['Treasure','Equipment','Consumable'],'Itemart'),
 attributes:Object.fromEntries(Object.entries(attributes(field(v,'Attributes'))).map(([k,value])=>[k,Number(value)>0&&(upgradeLevel>0||isRefined)?Number((Number(value)*(isRefined?1.5:1+[0,.03,.06,.09,.12,.17,.22,.28,.35,.45][upgradeLevel])).toFixed(4)):Number(value)])),effects:effects.map((e:any)=>({id:string(field(e,'Id'),'Effekt-ID'),description:field(e,'Description')??field(e,'Name')??field(e,'Id'),trigger:enumValue(field(e,'Trigger'),['DoubleAttack','TripleAttack','ExtraLoot','SushiPreservation'],'Effekt'),chance:number(field(e,'Chance'),'Chance'),strength:number(field(e,'Strength'),'Stärke'),requiresCritical:field(e,'RequiresCritical')===true,minimumStake:number(field(e,'MinimumStake'),'Mindesteinsatz'),cooldownSeconds:number(field(e,'CooldownSeconds'),'Cooldown'),activity:field(e,'Activity')==null?undefined:enumValue<Activity>(field(e,'Activity'),activities,'Aktivität')})),
 favorite:field(v,'IsFavorite')===true,locked:field(v,'IsLocked')===true,salvageValue:optionalNumber(field(v,'SalvageValue')),setId:field(v,'SetId')||undefined,
 collector:field(v,'Collector')==null?undefined:attributes(field(v,'Collector')) as Partial<CollectorStats> as CollectorStats,
 dropWeight:optionalNumber(field(v,'DropWeight')),activities:Array.isArray(field(v,'DropActivities'))?field(v,'DropActivities').map((a:any)=>enumValue<Activity>(a,activities,'Aktivität')):[]};
}
export function emptyRemoteState():DemoState{return {
 player:{id:'',username:'Spielstand wird geladen …',avatarUrl:'./otter-mage-compact.webp',pearls:unknown(),hp:unknown(),maxHp:unknown(),level:unknown(),experience:unknown(),lastPassiveRegen:unknown(),regenRemainder:unknown(),lastChatHeal:unknown(),baseStats:{} as CharacterStats,buffs:[]},
 inventory:[],equipment:{},perks:[],fish:{},sushi:{},boss:{id:'',name:'Bossstatus nicht verfügbar',hp:unknown(),maxHp:unknown(),status:'waiting',damage:unknown(),nextAt:unknown(),endsAt:unknown()},round:unknown(),hotTime:{endsAt:unknown(),active:false},cooldowns:{dive:unknown(),fishing:unknown(),boss:unknown()},effectCooldowns:{},
 remote:{loaded:false,connection:'loading',message:'Twitch-Anmeldung wird erwartet.',pointsText:'–',pointsExact:'',sets:[],recipes:[],perkPrices:{},supported:[],pending:false,syncing:false,bossKnown:false,hotKnown:false}};}
export function catalogView(catalog:any):{sets:ItemSet[];recipes:RecipeView[];perks:Record<string,any>} {
 const sets:ItemSet[]=(catalog?.sets??[]).map((s:any)=>({id:string(field(s,'Id'),'Set-ID'),name:string(field(s,'Name'),'Setname'),description:field(s,'Description')??'',demoOnly:false,itemIds:field(s,'ItemIds')??[],bonuses:(field(s,'Bonuses')??[]).map((b:any)=>({pieces:number(field(b,'RequiredPieces'),'Setteile'),description:field(b,'Description')??'',attributes:attributes(field(b,'Attributes'))}))}));
 const recipes:RecipeView[]=(catalog?.recipes??[]).map((r:any)=>({id:string(field(r,'Id'),'Rezept-ID'),name:string(field(r,'Name')??field(r,'DisplayName'),'Rezeptname'),pearlCost:optionalNumber(field(r,'PearlCost')),ingredients:Object.entries(object(field(r,'Ingredients'),'Zutaten')).map(([id,n])=>({fishId:id,name:field((catalog?.fish??[]).find((f:any)=>field(f,'Id')===id),'Name')??id,quantity:number(n,'Zutat')})),
 activity:field(r,'Activity')==null?undefined:enumValue<Activity>(field(r,'Activity'),activities,'Aktivität'),category:field(r,'Category')==null?undefined:enumValue<SushiCategory>(field(r,'Category'),['Offensive','Defensive'],'Sushi-Slot'),durationSeconds:optionalNumber(field(r,'DurationSeconds')),allActivities:field(r,'AllActivities')===true,bossUses:optionalNumber(field(r,'BossUses')),effectsText:field(r,'Effects'),attributes:field(r,'Attributes')?attributes(field(r,'Attributes')):undefined}));
 return {sets,recipes,perks:Object.fromEntries((catalog?.perks??[]).map((p:any)=>[string(field(p,'Id'),'Perk-ID'),p]))};
}
export function mapProfile(raw:any,catalog:any={}):DemoState {
 object(raw,'Profil');if(field(raw,'Source')!=='plugin-snapshot')throw Error('Das Backend liefert keinen echten gespeicherten Spielstand.');
 const user=string(field(raw,'UserId'),'Spieler-ID');if(!/^\d{1,25}$/.test(user))throw Error('Ungültige Spieleridentität.');
 const points=field(raw,'Points');if(!/^(0|[1-9]\d*)$/.test(String(points))||BigInt(points)>9223372036854775807n || typeof points==='number'&&!Number.isSafeInteger(points))throw Error('Ungültiger Perlenstand.');
 const rpg=object(field(raw,'Rpg'),'RPG-Profil');if(field(rpg,'SchemaVersion')!==1)throw Error('Unbekannte RPG-Datenversion.');
 const state=emptyRemoteState(),view=catalogView(catalog),health=field(rpg,'Health');
 const ranks=field(rpg,'TalentRanks');
 if(ranks!=null)state.talents=counts(ranks,'Talente');
 if(catalog.talentCatalogVersion===1&&Array.isArray(catalog.talents)){
  state.remote!.talentCatalogVersion=1;
  state.remote!.talentCatalog=catalog.talents.map((t:any)=>({id:string(field(t,'Id'),'Talent-ID'),name:string(field(t,'Name'),'Talentname'),branch:enumValue<Activity>(field(t,'Branch'),activities,'Talentbereich'),max:number(field(t,'Max'),'Talentränge'),value:number(field(t,'Value'),'Talentbonus'),effect:string(field(t,'Effect'),'Talenteffekt'),description:string(field(t,'Description'),'Talentbeschreibung'),requires:field(t,'Requires'),bonus:{}}));
  if(state.remote!.talentCatalog!.length!==21||state.remote!.talentCatalog!.some(t=>!Array.isArray(t.requires)||t.requires.some(id=>typeof id!=='string')))throw Error('Ungültiger Talentkatalog.');
 }
 const items=field(rpg,'Inventory');if(!Array.isArray(items))throw Error('Inventar fehlt.');state.inventory=items.map(inventoryItem);
 if(new Set(state.inventory.map(i=>i.instanceId)).size!==state.inventory.length)throw Error('Doppelte Item-Instanz.');
 for(const [slot,id]of Object.entries(object(field(rpg,'Equipment'),'Ausrüstung'))){const target=enumValue<Slot>(/^\d+$/.test(slot)?Number(slot):slot,slots,'Ausrüstungsslot');if(typeof id!=='string'||!state.inventory.some(i=>i.instanceId===id))throw Error('Ausrüstung verweist auf ein fehlendes Item.');state.equipment[target]=id;}
 const perkLevels=counts(field(rpg,'Perks'),'Perks');state.perks=Array.from(new Set([...Object.keys(view.perks),...Object.keys(perkLevels)])).map(id=>({id,name:field(view.perks[id],'Name')??id,level:Math.min(5,perkLevels[id]??0),maxLevel:Math.min(5,optionalNumber(field(view.perks[id],'MaximumLevel'))),presentation:field(view.perks[id],'Presentation')?{category:field(field(view.perks[id],'Presentation'),'Category'),symbol:field(field(view.perks[id],'Presentation'),'Symbol'),value:field(field(view.perks[id],'Presentation'),'Value'),label:field(field(view.perks[id],'Presentation'),'Label')}:undefined,bonus:field(view.perks[id],'BonusPerLevel')?attributes(field(view.perks[id],'BonusPerLevel')):{}} as Perk));
 state.remote!.perkMaxPurchase=catalog?.perkMaxPurchase===true;
 const upgrade=catalog?.itemUpgrade;if(upgrade&&upgrade.maximumLevel===9&&Array.isArray(upgrade.bonuses)&&upgrade.bonuses.length===10&&upgrade.costsByRarity&&upgrade.refinement)state.remote!.itemUpgrade=upgrade;
 state.remote!.materialCatalog=catalog?.materialCatalog;
 state.remote!.zones=catalog?.zones;state.remote!.collectorItems=catalog?.collectorItems;state.remote!.selectedDivingZone=field(rpg,"SelectedDivingZone")??"sunny_lagoon";state.remote!.selectedFishingZone=field(rpg,"SelectedFishingZone")??"sunny_lagoon";
 for(const perk of state.perks){const cost=field(view.perks[perk.id],'Costs')?.[perk.level];if(/^[1-9]\d*$/.test(String(cost))&&Number.isSafeInteger(Number(cost)))state.remote!.perkPrices[perk.id]=Number(cost);}
 for(const [id,stock]of Object.entries(object(field(rpg,'Fish'),'Fische'))){state.fish[id]=number(field(stock,'Quantity'),'Fischanzahl');if(!Number.isSafeInteger(state.fish[id])||state.fish[id]<0)throw Error('Ungültiger Fischvorrat.');}
 state.sushi=counts(field(rpg,'Sushi'),'Sushi');
 const materials=field(rpg,'Materials');
 if(materials!=null){
  const stock=object(materials,'Materialvorrat');
  const read=(name:string)=>{const value=number(field(stock,name),'Materialanzahl');if(!Number.isSafeInteger(value)||value<0||value>2147483647)throw Error('Ungültiger Materialvorrat.');return value;};
  state.materials={shells:read('Shells'),whitePearls:read('WhitePearls'),bluePearls:read('BluePearls'),redPearls:read('RedPearls'),stoneChips:read('StoneChips'),...Object.fromEntries(['PearlDust','LuminousScale','RuneShell','EternalDepthCore','TrueCoralCore','CursedSilk','AbyssShard','ReinforcementCore'].filter(key=>field(stock,key)!=null).map(key=>[key[0].toLowerCase()+key.slice(1),field(stock,key)==null?0:read(key)]))};
 }
 const buffs=field(rpg,'Buffs');if(!Array.isArray(buffs))throw Error('Buffs fehlen.');
 state.player.buffs=buffs.map((b:any)=>({recipeId:string(field(b,'RecipeId'),'Buff-ID'),name:view.recipes.find(r=>r.id===field(b,'RecipeId'))?.name??field(b,'RecipeId'),activity:enumValue<Activity>(field(b,'Activity'),activities,'Buff-Aktivität'),category:enumValue<SushiCategory>(field(b,'Category'),['Offensive','Defensive'],'Sushi-Slot'),remainingUses:number(field(b,'RemainingUses'),'Aufladungen'),attributes:attributes(field(b,'Attributes')),activatedAt:time(field(b,'ActivatedAt')),expiresAt:field(b,'ExpiresAt')==null?undefined:time(field(b,'ExpiresAt')),lastBossId:field(b,'LastBossId')||undefined,passiveHealing:optionalNumber(field(b,'PassiveHealing')),chatHealing:optionalNumber(field(b,'ChatHealing')),bossDamageReduction:optionalNumber(field(b,'BossDamageReduction')),bossLifeleech:optionalNumber(field(b,'BossLifeleech'))}));
 const cooldowns=object(field(rpg,'Cooldowns'),'Cooldowns');for(const [target,key]of [['dive','!dive'],['fishing','!fish'],['boss','!attack']] as const){state.cooldowns[target]=own(cooldowns,key)?time(cooldowns[own(cooldowns,key)!]):0;if(!Number.isFinite(state.cooldowns[target]))throw Error('Ungültiger Cooldown.');}
 Object.assign(state.player,{id:user,username:string(field(raw,'DisplayName'),'Spielername'),pearls:Number.isSafeInteger(Number(points))?Number(points):unknown(),hp:optionalNumber(field(health,'CurrentHp')),maxHp:optionalNumber(field(health,'AppliedMaximumHp')),level:optionalNumber(field(rpg,'PlayerLevel')),experience:optionalNumber(field(rpg,'Experience')),lastPassiveRegen:time(field(health,'LastPassiveRegen')),regenRemainder:optionalNumber(field(health,'SushiRegenRemainder')),lastChatHeal:time(field(health,'LastChatHeal')),defeatedBossId:field(health,'Defeated')?field(health,'BossId'):undefined});
 state.boss.damage=optionalNumber(field(health,'BossDamage'));
 state.player.shieldHp=field(health,'SetShieldHp')==null?0:number(field(health,'SetShieldHp'),'Schild');
 Object.assign(state.remote!,{loaded:true,pointsText:BigInt(points).toLocaleString('de-DE'),pointsExact:String(points),experienceText:/^\d+$/.test(String(field(rpg,'Experience')))?BigInt(field(rpg,'Experience')).toLocaleString('de-DE'):undefined,updatedAt:string(field(raw,'UpdatedAt'),'Speicherzeit'),sets:view.sets,recipes:view.recipes});
 return state;
}

