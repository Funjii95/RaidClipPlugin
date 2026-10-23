import {it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import catalog from '../../expansions/eternal/catalog.json';
import art from '../src/eternal-art.json';
import zones from '../src/zone-art.json';
import {inventoryItem,mapProfile} from '../src/remote/model';
import {profile} from './remote-fixtures';
import {EquipmentUpgrade} from '../src/UpgradePanel';
import {ZonePanel} from '../src/ZonePanel';
import {CodexCharacter} from '../src/CodexCharacter';
import {emptyStats} from '../src/presentation';
it('ships 24 independent Eternal and Divine images with verified provenance',()=>{
 expect(catalog.items).toHaveLength(24);expect(catalog.items.filter(i=>i.Rarity==='Eternal')).toHaveLength(18);expect(catalog.items.filter(i=>i.Rarity==='Divine')).toHaveLength(6);
 for(const item of catalog.items){const entry=art[item.ItemId as keyof typeof art];expect(entry.name).toBe(item.DisplayName);expect(entry.width).toBe(256);expect(entry.height).toBe(256);expect(entry.sourceSha256).toMatch(/^[a-f0-9]{64}$/);expect(createHash('sha256').update(readFileSync('public/'+entry.src.slice(2))).digest('hex')).toBe(entry.assetSha256);}
});
it('maps new rarities and effective persisted upgrade values without rerolling',()=>{
 for(const item of catalog.items){const mapped=inventoryItem({...item,InstanceId:'saved',Rarity:item.Rarity==='Divine'?9:8,Slot:1,Kind:1,UpgradeLevel:item.Rarity==='Divine'?0:9,IsRefined:item.Rarity!=='Divine',Effects:[]});expect(mapped.rarity).toBe(item.Rarity);expect(mapped.itemId).toBe(item.ItemId);}
});
it('displays Divine as not upgradable without any upgrade button',()=>{
 const item=inventoryItem({...catalog.items.find(i=>i.Rarity==='Divine'),InstanceId:'saved',Rarity:9,Slot:1,Kind:1,Effects:[]});
 const html=renderToStaticMarkup(<EquipmentUpgrade item={item} busy={false} allowed={()=>true} onSubmit={()=>{}}/>);expect(html).toContain('Nicht aufwertbar');expect(html).not.toContain('<button');
});
it('reads all ten actual areas and keeps fishing and diving choices distinct',()=>{
 const raw:any=profile();raw.rpg.selectedDivingZone='abyss';raw.rpg.selectedFishingZone='sunny_lagoon';const mapped=mapProfile(raw,{zones:catalog.zones.Areas});
 expect(mapped.remote?.zones).toHaveLength(10);expect(mapped.remote?.selectedDivingZone).toBe('abyss');expect(mapped.remote?.selectedFishingZone).toBe('sunny_lagoon');
 const html=renderToStaticMarkup(<ZonePanel zones={catalog.zones.Areas as any} diving="abyss" fishing="sunny_lagoon" busy={false} cooldown={0} onSelect={async()=>{}} onStart={()=>{}}/>);expect((html.match(/<article/g)??[])).toHaveLength(10);expect(html).toContain('Thron der Urtiefe');expect(html).toContain('Gegnerlevel 100');expect(html).toContain('Hier tauchen');
});
it('ships the ten correctly keyed zone illustrations',()=>{
 expect(Object.keys(zones)).toHaveLength(10);for(const zone of catalog.zones.Areas){const entry=zones[zone.Id as keyof typeof zones];expect(entry).toBeDefined();expect(createHash('sha256').update(readFileSync('public/'+entry.src.slice(2))).digest('hex')).toBe(entry.assetSha256);}
});
it('keeps all eight real equipment slots beside the character including both rings and weapon',()=>{
 const state=mapProfile(profile());const html=renderToStaticMarkup(<CodexCharacter state={state} stats={emptyStats()} onItem={()=>{}}/>);for(const name of ['Helm','Brust','Handschuhe','Stiefel','Waffe','Amulett','Ring links','Ring rechts'])expect(html).toContain(`aria-label="${name}"`);expect((html.match(/class="paper-slot /g)??[])).toHaveLength(8);
});
it('preserves six cumulative stages and the requested Divine enemy bonus',()=>{
 for(const set of catalog.sets)expect(set.Bonuses.map(b=>b.RequiredPieces)).toEqual([1,2,3,4,5,6]);
 expect(catalog.sets.find(s=>s.Id==='divine_infinity')?.Bonuses[0].Attributes.EnemyEncounterChance).toBe(1000);
});
