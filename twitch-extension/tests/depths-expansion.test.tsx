import {it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {inventoryItem,mapProfile} from '../src/remote/model';
import {profile} from './remote-fixtures';
import {EquipmentUpgrade,MaterialInventory} from '../src/UpgradePanel';
import artwork from '../src/depths-art.json';
import catalog from '../../expansions/depths/catalog.json';
it('maps all 65 new definitions to unique supplied artwork with verified hashes',()=>{
 const definitions=[...catalog.items.map(i=>i.ItemId),...catalog.materials.map(m=>m.Id)];expect(definitions).toHaveLength(65);
 for(const id of definitions){const art=artwork[id as keyof typeof artwork];expect(art).toBeDefined();expect(createHash('sha256').update(readFileSync('public/'+art.src.replace(/^\.\//,''))).digest('hex')).toBe(art.assetSha256);expect(art.sourceSha256).toMatch(/^[a-f0-9]{64}$/);}
 expect(new Set(definitions.map(id=>artwork[id as keyof typeof artwork].src)).size).toBe(65);
});
it('preserves cursed penalties while applying the final original-stat multiplier exactly once',()=>{
 const raw={...profile().rpg.inventory[0],Rarity:6,UpgradeLevel:9,IsRefined:true,Attributes:{CritChance:28,HealingPower:-12}};
 const item=inventoryItem(raw);expect(item.rarity).toBe('Cursed');expect(item.attributes).toEqual({critChance:42,healingPower:-12});expect(item.isRefined).toBe(true);
 expect(()=>inventoryItem({...raw,UpgradeLevel:8})).toThrow('Veredelung');
});
it('reads expanded balances without merging currency and materials',()=>{
 const p:any=profile(1234);p.rpg.materials={Shells:1,WhitePearls:2,BluePearls:3,RedPearls:4,StoneChips:5,ReinforcementCore:6,AbyssShard:7};
 const mapped=mapProfile(p);expect(mapped.materials?.reinforcementCore).toBe(6);expect(mapped.materials?.abyssShard).toBe(7);expect(mapped.player.pearls).toBe(1234);
});
it('shows eleven material images and authoritative server costs with missing requirements',()=>{
 const stock={shells:0,whitePearls:0,bluePearls:0,redPearls:0,stoneChips:0};
 const html=renderToStaticMarkup(<MaterialInventory stock={stock} catalog={catalog.materials as any}/>);expect((html.match(/<img /g)??[]).length).toBe(11);
 const rules={maximumLevel:9,bonuses:[0,.03,.06,.09,.12,.17,.22,.28,.35,.45],costsByRarity:{Epic:[{TargetLevel:1,Pearls:777,Materials:{reinforcement_core:2}}]},refinement:{TargetLevel:9,Pearls:15000,Materials:{eternal_depth_core:1}}};
 const panel=renderToStaticMarkup(<EquipmentUpgrade item={inventoryItem(profile().rpg.inventory[0])} rules={rules} stock={stock} points="1000" busy={false} allowed={()=>true} onSubmit={()=>{}}/>);expect(panel).toContain('777');expect(panel).toContain('Verstärkungskern');expect(panel).toContain('disabled');
});
