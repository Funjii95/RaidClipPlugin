import {it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {renderToStaticMarkup} from 'react-dom/server';
import {createElement} from 'react';
import catalog from '../../expansions/weapons/catalog.json';
import art from '../src/weapons-art.json';
import {mapProfile,inventoryItem} from '../src/remote/model';
import {profile} from './remote-fixtures';
import {CodexCharacter} from '../src/CodexCharacter';
import {emptyStats,statsText} from '../src/presentation';
it('ships all twelve weapons with individual original artwork and exact staff values',()=>{
 expect(catalog.items).toHaveLength(12);expect(new Set(catalog.items.map(i=>i.ItemId)).size).toBe(12);
 for(const item of catalog.items){expect(item.Slot).toBe('Weapon');const image=art[item.ItemId as keyof typeof art];expect(image.name).toBe(item.DisplayName);expect(createHash('sha256').update(readFileSync('public/'+image.src.slice(2))).digest('hex')).toBe(image.assetSha256);}
 const staff=catalog.items.find(i=>i.ItemId==='weapon_leviathan_call')!;expect(staff.Attributes.Attack).toBe(300);expect(staff.Attributes.MagicPower).toBe(350);expect(staff.Attributes.ManaRegeneration).toBe(40);
});
it('preserves all magic stats and upgrades them with the saved weapon level',()=>{
 const item=inventoryItem({...catalog.items[1],InstanceId:'owned',Rarity:4,Slot:0,Kind:1,UpgradeLevel:9,IsRefined:false});
 expect(item.attributes.attack).toBe(435);expect(item.attributes.magicPower).toBe(507.5);expect(item.attributes.manaRegeneration).toBe(58);
 expect(statsText(item.attributes).join(' ')).toContain('507.5 Magiekraft');
});
it('shows the authoritative mana and shield balances in the character data',()=>{
 const raw:any=profile();raw.rpg.health.CurrentMana=37.125;raw.rpg.health.ManaShieldHp=25;
 const state=mapProfile(raw);expect(state.player.mana).toBe(37.125);expect(state.player.manaShieldHp).toBe(25);
 const html=renderToStaticMarkup(createElement(CodexCharacter,{state,stats:emptyStats(),onItem:()=>{}}));expect(html).toContain('aria-label="Mana"');expect(html).toContain('37.125');expect(html).toContain('Ohne Mana bleiben normale Angriffe möglich');
});
