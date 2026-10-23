import {describe,it,expect} from 'vitest';
import {availablePoints,earnedPoints,learnTalent,talentBonus,underwaterDodgeChance,avoidsUnderwaterHit} from '../src/talents';
import {initialState} from '../src/data';
import {MockRpgDataSource,characterStats} from '../src/mock';
import {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
describe('level-based talent points',()=>{
 it('retroactively grants once, including legacy profiles, and adds new levels',()=>{
  expect(earnedPoints(1)).toBe(0);expect(earnedPoints(30)).toBe(29);expect(earnedPoints(100)).toBe(99);
  const ranks=learnTalent('combat-power',30,{});
  expect(availablePoints(30,ranks)).toBe(28);
  expect(availablePoints(30,JSON.parse(JSON.stringify(ranks)))).toBe(28);
  expect(availablePoints(31,ranks)).toBe(29);
  for(const invalid of [NaN,Infinity,1.5,-1])expect(earnedPoints(invalid)).toBe(0);
 });
 it('enforces prerequisites, rank caps and a common budget across branches',()=>{
  expect(()=>learnTalent('combat-crit',100,{})).toThrow('Voraussetzung');
  let ranks={};for(let i=0;i<3;i++)ranks=learnTalent('combat-power',100,ranks);
  expect(learnTalent('combat-crit',100,ranks)).toMatchObject({'combat-crit':1});
  for(let i=3;i<5;i++)ranks=learnTalent('combat-power',100,ranks);
  expect(()=>learnTalent('combat-power',100,ranks)).toThrow('Maximalrang');
  expect(()=>learnTalent('dive-enemy',2,{'fishing-luck':1})).toThrow('Keine Talentpunkte');
  expect(()=>learnTalent('unknown',100,{})).toThrow('Unbekannt');
 });
 it('serializes concurrent allocations and keeps failed requests atomic',async()=>{
  const seed=initialState(1000);seed.player.level=2;
  const source=new MockRpgDataSource(()=>1000,()=>0.5,seed);
  const results=await Promise.allSettled([source.dispatch({type:'learnTalent',id:'combat-power'}),source.dispatch({type:'learnTalent',id:'dive-enemy'})]);
  expect(results.map(r=>r.status)).toEqual(['fulfilled','rejected']);
  expect(source.getSnapshot().talents).toEqual({'combat-power':1});
  expect(source.getSnapshot().player.pearls).toBe(seed.player.pearls);
  const reloaded=new MockRpgDataSource(()=>1000,()=>0.5,source.getSnapshot());
  await expect(reloaded.dispatch({type:'learnTalent',id:'fishing-luck'})).rejects.toThrow('Keine Talentpunkte');
 });
 it('keeps underwater bonuses out of boss stats and applies combat damage',()=>{
  const seed=initialState(1000);const before=characterStats(seed,'Boss');
  seed.talents={'combat-power':2,'dive-enemy':5,'dive-dodge':5};
  expect(characterStats(seed,'Boss').damage-before.damage).toBe(6);
  expect(talentBonus(seed.talents,'Boss').bossDodgeChance).toBeUndefined();
  expect(talentBonus({'dive-enemy':5},'Boss').damage??0).toBe(0);
 });
 it('never sends an unsupported talent intent to production',async()=>{
  const source=new BackendRpgDataSource({});
  expect(source.canDispatch('learnTalent')).toBe(false);
  await expect(source.dispatch({type:'learnTalent',id:'combat-power'})).rejects.toThrow('Plugin oder RPG');
 });
});

describe('Unterwasser-Ausweichen',()=>{
 it('grants four percentage points per rank with a strict twenty percent cap',()=>{
  for(let rank=0;rank<=5;rank++)expect(underwaterDodgeChance({'dive-dodge':rank})).toBe(rank*4);
  expect(underwaterDodgeChance({'dive-dodge':99})).toBe(20);
  for(const rank of [-1,NaN,Infinity,1.5])expect(underwaterDodgeChance({'dive-dodge':rank})).toBe(0);
 });
 it('avoids the entire hit below the chance boundary only',()=>{
  expect(avoidsUnderwaterHit({'dive-dodge':5},19.99)).toBe(true);
  expect(avoidsUnderwaterHit({'dive-dodge':5},20)).toBe(false);
  expect(avoidsUnderwaterHit({},0)).toBe(false);
  for(const roll of [-1,NaN,Infinity,100])expect(avoidsUnderwaterHit({'dive-dodge':5},roll)).toBe(false);
 });
 it('does not grant boss dodge or modify boss combat stats',()=>{
  const seed=initialState(1000);const before=characterStats(seed,'Boss');
  seed.talents={'dive-dodge':5};
  expect(characterStats(seed,'Boss')).toEqual(before);
  expect(talentBonus(seed.talents,'Boss').bossDodgeChance).toBeUndefined();
 });
});
