import {describe,it,expect} from 'vitest';
import {readFileSync,existsSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import art from '../src/item-art.json';
import {catalog,recipes,fishSpecies} from '../src/data';
const entries:Record<string,{src:string;sourceSha256:string;assetSha256:string;width:number;height:number}>=art.entries;
describe('user item illustrations',()=>{
 it('maps every inventory item, fish and recipe by stable ID',()=>{expect(Object.keys(entries)).toHaveLength(102);for(const id of [...catalog.map(i=>i.itemId),...fishSpecies.map(f=>'fish-'+f.id),...recipes.map(r=>'sushi-'+r.id)])expect(entries[id],id).toBeDefined();});
 it('ships only local compact images with a verified digest',()=>{for(const [id,e] of Object.entries(entries)){expect(e.src).toBe(`./items/${id}.webp`);const path=resolve('public',e.src.slice(2));expect(existsSync(path),id).toBe(true);expect(createHash('sha256').update(readFileSync(path)).digest('hex')).toBe(e.assetSha256);expect(e.sourceSha256).toMatch(/^[a-f0-9]{64}$/);expect(Math.max(e.width,e.height)).toBeLessThanOrEqual(384);}});
});
