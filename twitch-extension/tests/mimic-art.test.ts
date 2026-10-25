import {it,expect} from 'vitest';
import {readFileSync,existsSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {mimicArt,mimicImage} from '../src/mimic-art';
import zones from '../src/zone-art.json';
it('assigns one distinct verified portrait to each of the ten biomes',()=>{
 expect(Object.keys(mimicArt).sort()).toEqual(Object.keys(zones).sort());expect(new Set(Object.values(mimicArt).map(a=>a.assetSha256)).size).toBe(10);
 for(const [zone,art] of Object.entries(mimicArt)){
  expect(mimicImage(zone)).toBe(`./mimics/${zone}.webp`);const path=new URL('../public/'+art.src.slice(2),import.meta.url);expect(existsSync(path)).toBe(true);expect(createHash('sha256').update(readFileSync(path)).digest('hex')).toBe(art.assetSha256);expect(art.width).toBe(art.height);expect(art.width).toBeGreaterThanOrEqual(1024);
 }
});
it('retains the legacy image for missing biome metadata',()=>{expect(mimicImage()).toBe('./boss-mimic.png');expect(mimicImage('unknown')).toBe('./boss-mimic.png');});
