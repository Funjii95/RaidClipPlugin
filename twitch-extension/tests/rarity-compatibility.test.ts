import {it,expect} from 'vitest';
import {inventoryItem} from '../src/remote/model';
import {profile} from './remote-fixtures';
const names=['Common','Uncommon','Rare','Epic','Legendary','True','Cursed','Abyss','Eternal','Divine'];
it.each(names.map((name,index)=>({name,index})))('reads $name equipment from the current plugin',({name,index})=>{const item=profile().rpg.inventory[0];expect(inventoryItem({...item,Rarity:index}).rarity).toBe(name);expect(inventoryItem({...item,Rarity:name}).rarity).toBe(name);});
