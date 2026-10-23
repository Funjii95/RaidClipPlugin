import {it,expect} from 'vitest';
import fs from 'node:fs';
import {createHash} from 'node:crypto';
import artwork from '../src/eternal-eight-art.json';
import {itemArt} from '../src/item-art';
import {catalogView,mapProfile} from '../src/remote/model';
import {profile} from './remote-fixtures';
it('registers 24 unique PNG icons and preserves every image checksum',()=>{
 expect(Object.keys(artwork)).toHaveLength(24);const hashes=new Set<string>();
 for(const [id,entry]of Object.entries(artwork)){
  expect(itemArt[id]).toEqual(entry);expect(entry.width).toBeGreaterThanOrEqual(512);expect(entry.height).toBeGreaterThanOrEqual(512);
  const bytes=fs.readFileSync('public/'+entry.src.slice(2));expect(bytes.subarray(1,4).toString()).toBe('PNG');
  expect(createHash('sha256').update(bytes).digest('hex')).toBe(entry.assetSha256);hashes.add(entry.assetSha256);
 }
 expect(hashes.size).toBe(24);
});
it('renders all eight cumulative thresholds from the authoritative set catalog',()=>{
 const sets=catalogView({sets:[{Id:'eternal_cosmic_forge',Name:'Sternenschmied des Urkosmos',ItemIds:Object.keys(artwork).filter(id=>id.startsWith('eternal_cosmic_forge')),Bonuses:Array.from({length:8},(_,n)=>({RequiredPieces:n+1,Description:`Effekt ${n+1}`,Attributes:{}}))}]}).sets;
 expect(sets[0].itemIds).toHaveLength(8);expect(sets[0].bonuses.map(b=>b.pieces)).toEqual([1,2,3,4,5,6,7,8]);
});
it('expands compact shared upgrade costs without changing their authoritative values',()=>{
 const state=mapProfile(profile(),{itemUpgrade:{maximumLevel:9,bonuses:Array(10).fill(0),baseCosts:Array.from({length:6},(_,n)=>[n+10,{reinforcement_core:n+1}]),highCostsByRarity:{Eternal:[[70,{abyss_shard:2}],[80,{abyss_shard:3}],[90,{abyss_shard:5,eternal_depth_core:3}]]},refinement:{TargetLevel:9,Pearls:15000,Materials:{eternal_depth_core:1}}}});
 expect(state.remote?.itemUpgrade?.costsByRarity.Eternal[8]).toEqual({TargetLevel:9,Pearls:90,Materials:{abyss_shard:5,eternal_depth_core:3}});
});
