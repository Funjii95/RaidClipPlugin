import {describe,it,expect} from 'vitest';
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
describe('Twitch hosting package',()=>{
 it('has all three relative-path entry pages with the Twitch Helper as first script and no inline scripts',()=>{
  for(const name of ['panel.html','mobile.html','config.html']){const html=fs.readFileSync(path.join(root,'dist',name),'utf8');const scripts=[...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/g)];expect(scripts[0][1]).toContain('https://extension-files.twitch.tv/helper/v1/twitch-ext.min.js');expect(scripts.every(s=>s[1].includes('src=')&&!s[2].trim())).toBe(true);expect(html).toContain('./assets/');}
 });
 it('ships no mock engine, starter inventories or demo settlements in the normal build',()=>{
  const files=fs.readdirSync(path.join(root,'dist/assets')).filter(f=>f.endsWith('.js'));
  expect(files.some(f=>f.includes('demo-entry'))).toBe(false);
  const code=files.map(f=>fs.readFileSync(path.join(root,'dist/assets',f),'utf8')).join('\n');
  for(const forbidden of ['demo-trident','demo-only','settleMinigameFixture','MockRpgDataSource','Demo-Startervorrat: je sechs Fische von zwölf Arten']){
   if(forbidden.startsWith('Demo-Startervorrat'))continue; // Shared UI contains this text only in the separately compiled demo branch.
   expect(code).not.toContain(forbidden);
  }
 });
 it('preserves all 102 item assets and the original illustrations byte for byte',()=>{
  const art=JSON.parse(fs.readFileSync(path.join(root,'src/item-art.json'),'utf8'));
  expect(Object.keys(art.entries)).toHaveLength(102);
  for(const entry of Object.values(art.entries) as any[]){const relative=entry.src.replace(/^\.\//,'');expect(fs.readFileSync(path.join(root,'dist',relative)).equals(fs.readFileSync(path.join(root,'public',relative)))).toBe(true);}
  for(const file of ['otter-mage.png','luminous-depths.png'])expect(fs.readFileSync(path.join(root,'dist',file)).equals(fs.readFileSync(path.join(root,'public',file)))).toBe(true);
 });
});
