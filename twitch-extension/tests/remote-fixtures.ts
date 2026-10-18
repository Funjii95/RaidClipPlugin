import type {Authorization} from '../src/live/client';
export const authorization:Authorization={token:'synthetic-helper-token',channelId:'42',userId:'Usynthetic'};
export function profile(points:number|string=1234,equipped=false){return {
 userId:'1001',displayName:'SYNTHETIC Twitch-Testotter',points,source:'plugin-snapshot',updatedAt:equipped?'2026-10-18T12:00:01Z':'2026-10-18T12:00:00Z',
 rpg:{schemaVersion:1,playerLevel:7,experience:4321,sushiVersion:2,legacyFishImported:true,
 health:{currentHp:87,appliedMaximumHp:150,appliedBaseMaximumHp:100,lastPassiveRegen:'2026-10-18T12:00:00Z',sushiRegenRemainder:.25,lastChatHeal:'2026-10-18T11:59:00Z',bossId:'boss-saved',bossDamage:17,bossRemainingHp:400,defeated:false},
 inventory:[{InstanceId:'owned-trident',ItemId:'trident',DisplayName:'Gespeicherter Dreizack',Description:'Synthetisches Testitem',IconId:'trident',Rarity:3,Slot:0,Kind:1,Attributes:{Damage:12.5,Power:3,MaximumHp:5},Effects:[],IsFavorite:true,IsLocked:false,SalvageValue:33,SetId:'ocean',Collector:null,DropWeight:2,DropActivities:[0,2]}],
 equipment:equipped?{'0':'owned-trident'}:{},perks:{damage:2},fish:{sardine:{FishId:'sardine',Quantity:4}},sushi:{fishing:2},buffs:[{RecipeId:'fighter',Activity:2,Category:0,RemainingUses:2,Attributes:{Damage:4},ActivatedAt:'2026-10-18T12:00:00Z',ExpiresAt:null,LastBossId:'boss-saved',PassiveHealing:0,ChatHealing:0,BossDamageReduction:0,BossLifeleech:0}],cooldowns:{}}
};}
export function catalog(){return {supported:['equip','unequip','buyPerk','dive','fish','craft','eat'],
 perks:[{Id:'damage',Name:'Starke Pfoten',MaximumLevel:10}],
 sets:[{Id:'ocean',Name:'Ozeanset',Description:'Set aus dem Testspiel',ItemIds:['trident','helmet'],Bonuses:[{RequiredPieces:2,Attributes:{Damage:5}}]}],
 recipes:[{Id:'fishing',Name:'Sushi des Anglers',PearlCost:250,Ingredients:{sardine:1},Effects:'+10 Angelglück'}]};}
export const availability=()=>({available:true,reason:'ready',sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(catalog())});
export function memoryStorage(){const data=new Map<string,string>();return {storage:{getItem:(key:string)=>data.get(key)??null,setItem:(key:string,v:string)=>{data.set(key,v)},removeItem:(key:string)=>{data.delete(key)}} as Storage,data};}
