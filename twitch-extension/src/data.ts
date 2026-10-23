import snapshot from './reference/v2.1.21.json';
import type { CharacterStats, InventoryItem, ItemSet, SushiRecipe, DemoState, Activity, Rarity, Slot, CollectorStats, ItemEffect } from './types';
export const reference = snapshot;
export const rules = snapshot.config;
export const rarities: Rarity[] = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary', 'True', 'Cursed', 'Abyss'];
export const labels: Record<keyof CharacterStats, string> = { attack:'Angriff',magicPower:'Magiekraft',manaRegeneration:'Manaregeneration',waterDamage:'Wasserschaden',elementalDamage:'Elementarschaden',abyssDamage:'Abgrundschaden',lightDamage:'Lichtschaden',enemyEncounterChance:'Gegnerchance relativ',materialFind:'Materialfund',attackPercent:'Angriffsbonus',experienceBonus:'EXP-Bonus',divineDamage:'Göttlicher Schaden',movementSpeed: 'Bewegungstempo', bossLifeleech: 'Boss-Lebensraub', armor: 'Rüstung', armorPercent: 'Rüstungsbonus', maximumHpPercent: 'Maximales Leben', attackSpeed: 'Angriffstempo', healingPower: 'Heilungswirkung', damage: 'Schadensbonus', critChance: 'Kritische Trefferchance', critDamage: 'Kritischer Schaden', luck: 'Luck', bossDamage: 'Bossschaden', fishingLuck: 'Fishing Luck', diveLuck: 'Dive Luck', pearlBonus: 'Perlenbonus', power: 'Kraft', stamina: 'Ausdauer', maximumHp: 'Ausrüstungs-HP', doubleAttackChance: 'Doppelangriff', tripleAttackChance: 'Dreifachangriff', extraLootChance: 'Zusatzbeute', sushiPreservationChance: 'Sushi-Erhaltung (Altbuffs)', bossDamageReduction: 'Boss-Schadensreduktion', bossDodgeChance: 'Boss-Ausweichen', criticalHealing: 'Krit-Heilung', rareEquipmentLuck: 'Seltene Ausrüstung relativ' };
export const emptyStats = (): CharacterStats => Object.fromEntries(Object.keys(labels).map(k => [k, 0])) as unknown as CharacterStats;
export const numberStat = (key: string, value: number) => `${value}${['attack', 'magicPower', 'power', 'stamina', 'maximumHp', 'armor'].includes(key) ? '' : ' %'}`;
export const statsText = (a: Partial<CharacterStats>) => Object.entries(a).filter(([, v]) => v !== 0).map(([k, v]) => `${v!>0?'+':''}${numberStat(k, v!)} ${labels[k as keyof CharacterStats]}`);
export const collectorLabels: Record<keyof CollectorStats, string> = { fishingPearls: 'Fischfang-Perlen', divePearls: 'Positive Dive-Netto-Perlen', bossPearls: 'Boss-Siegperlen', bossExperience: 'Boss-EXP (überlebt)', treasureChance: 'Relative Sammlerschatzchance', rareTreasureChance: 'Relative seltene Sammlerschatzchance' };
export const collectorText = (a: Partial<CollectorStats>) => Object.entries(a).filter(([, v]) => v! > 0).map(([k, v]) => `+${v} % ${collectorLabels[k as keyof CollectorStats]}`);
const icon = (slot?: string | null, kind?: string) => slot ? ({ Weapon: '🔱', Helmet: '👑', Chest: '🛡️', Gloves: '🧤', Boots: '🥾', Ring1: '💍', Ring2: '💍', Amulet: '📿' }[slot] ?? '✧') : kind === 'Consumable' ? '🍣' : '🐚';
export function effectText(e: {
    name: string;
    trigger: string;
    chance: number;
    strength: number;
    requiresCritical: boolean;
    minimumStake: number;
    cooldownSeconds: number;
    activity: string | null;
}) {
    const action = e.trigger === 'DoubleAttack' ? `ein Zusatztreffer mit ${e.strength * 100} % Schaden` : e.trigger === 'TripleAttack' ? `zwei Zusatztreffer mit je ${e.strength * 100} % Schaden (Vorrang)` : e.trigger === 'ExtraLoot' ? 'ein weiterer Tauch-Lootwurf, der leer ausgehen kann' : 'eine Aufladung erhalten (nur alte aktionsbasierte Buffs; verlängert neue Sushi nicht)';
    return `${e.name}: ${e.chance} % Chance auf ${action}.${e.requiresCritical ? ' Nur bei kritischem Treffer.' : ''}${e.activity ? ' Aktivität: ' + e.activity + '.' : ''}${e.minimumStake ? ' Einsatz ab ' + e.minimumStake + '.' : ''}${e.cooldownSeconds ? ' Cooldown ' + e.cooldownSeconds + ' s.' : ''}`;
}
function present(raw: typeof snapshot.samples[number]): InventoryItem {
    return { instanceId: raw.instanceId, itemId: raw.itemId, name: raw.displayName, description: raw.description || (raw.slot ? 'Ein Ausrüstungsstück aus dem Ozean. Werte dieses Demo-Rolls bleiben beim Ausrüsten erhalten.' : 'Ein Fundstück aus der Tiefe.'), icon: icon(raw.slot, raw.kind), rarity: raw.rarity as Rarity, slot: (raw.slot ?? undefined) as Slot | undefined, kind: raw.kind, attributes: Object.fromEntries(Object.entries(raw.attributes).filter(([, v]) => v !== 0)), effects: raw.effects.map(e => ({ ...e, description: effectText(e), activity: e.activity as Activity | null })), favorite: false, locked: false, salvageValue: 0, setId: raw.setId || undefined, collector: raw.collector ?? undefined, dropWeight: raw.dropWeight, activities: raw.dropActivities as Activity[] };
}
export const catalog: InventoryItem[] = snapshot.samples.map(present);
export const sets: ItemSet[] = rules.sets.map(s => ({ id: s.id, name: s.name, description: s.description, demoOnly: false, itemIds: s.itemIds, bonuses: s.bonuses.map(b => ({ pieces: b.requiredPieces, description: statsText(b.attributes).join(', '), attributes: b.attributes })) }));
export const fishSpecies = rules.fishSpecies;
const recipeNames: Record<string, string> = { fishing: 'Sushi des Anglers', fighter: 'Sushi des Kämpfers', critical: 'Sushi der Präzision', dive: 'Sushi der Tiefe' };
export const recipes: SushiRecipe[] = rules.recipes.map(r => ({ id: r.id, name: r.displayName || recipeNames[r.id] || r.id, ingredients: Object.entries(Object.keys(r.ingredients).length ? r.ingredients : { [r.fishId]: r.fishQuantity }).map(([id, q]) => ({ fishId: id, name: fishSpecies.find(f => f.id === id)?.name ?? id, quantity: q })), pearlCost: r.pearlCost, activity: r.activity as Activity, category: r.category as 'Offensive' | 'Defensive', bossUses: r.bossUses || rules.buffUses, attributes: r.attributes, passiveHealing: r.passiveHealing, chatHealing: r.chatHealing, bossDamageReduction: r.bossDamageReduction, bossLifeleech: r.bossLifeleech }));
export const recipeEffects = (r: SushiRecipe) => [...statsText(r.attributes), ...(r.passiveHealing ? [`+${r.passiveHealing} HP/Minute`] : []), ...(r.chatHealing ? [`+${r.chatHealing} HP je Chat-Heilung`] : []), ...(r.bossDamageReduction ? [`${r.bossDamageReduction} % weniger Boss-Gegenschaden`] : []), ...(r.bossLifeleech ? [`${r.bossLifeleech} % Boss-Lebensraub (kein festes HP-Cap)`] : [])].join(', ');
export function newBoss(now: number, round: number): DemoState['boss'] { return { id: `demo-boss-${round}`, name: 'Perlenwächter', hp: reference.adventures.boss.hitPoints, maxHp: reference.adventures.boss.hitPoints, status: 'active', damage: 0, nextAt: 0, endsAt: now + reference.adventures.boss.durationSeconds * 1000 }; }
export function initialState(now = Date.now()): DemoState {
    const inventory = structuredClone(catalog);
    const equipment = { Weapon: 'demo-trident', Helmet: 'demo-deepsea-crown', Chest: 'demo-deepsea-chest', Gloves: 'demo-deepsea-grip', Boots: 'demo-deepsea-boots' };
    const hp = rules.health.baseHp + Object.values(equipment).reduce((n, id) => { const a = inventory.find(i => i.instanceId === id)!.attributes; return n + (a.maximumHp ?? 0) + (a.stamina ?? 0) * rules.health.hpPerStamina; }, 0);
    return { player: { id: 'demo-only', username: 'Funjii · Demo-Otter', avatarUrl: './otter-mage.png', pearls: 25000, hp, maxHp: hp, level: 1, experience: 0, lastPassiveRegen: now, regenRemainder: 0, lastChatHeal: now - rules.health.chatCooldownSeconds * 1000, baseStats: emptyStats(), buffs: [] }, inventory, equipment, fish: Object.fromEntries(fishSpecies.map(f => [f.id, 6])), sushi: Object.fromEntries(recipes.map(r => [r.id, 0])), perks: rules.perks.map(p => ({ id: p.id, name: p.displayName || ({ damage: 'Starke Pfoten', crit: 'Scharfer Blick', critdamage: 'Wucht der Welle', luck: 'Otterglück', boss: 'Bezwinger der Tiefe', dive: 'Tiefentaucher', fishing: 'Meisterangler' }[p.id] ?? p.id), level: 0, maxLevel: p.maximumLevel, bonus: p.bonusPerLevel })), boss: newBoss(now, 1), round: 1, hotTime: { active: true, endsAt: now + rules.hotTime.durationMinutes * 60000 }, cooldowns: { dive: 0, fishing: 0, boss: 0 }, effectCooldowns: {} };
}
export function generateItem(id: string, instanceId: string, random: () => number): InventoryItem {
    const template = rules.diveLoot.items.find(i => i.itemId === id)!;
    const item = structuredClone(catalog.find(i => i.itemId === id)!);
    item.instanceId = instanceId;
    const generation = template.generation;
    const ri = rarities.indexOf(item.rarity);
    const next = (min: number, max: number) => min + Math.floor(random() * (max - min + 1));
    if (generation) {
        const counts = [[1, 1, 0, 0], [1, 2, 0, 0], [2, 3, 0, 1], [3, 4, 1, 1], [4, 5, 1, 1]][ri];
        const count = generation.attributeCount ?? next(counts[0], counts[1]);
        const pool = [...generation.attributes];
        item.attributes = {};
        for (let i = 0; i < count; i++) {
            const range = pool.splice(next(0, pool.length - 1), 1)[0];
            const key = range.attribute[0].toLowerCase() + range.attribute.slice(1);
            item.attributes[key as keyof CharacterStats] = next(range.minimum, range.maximum);
        }
        item.effects = [];
        if (generation.generateEffect && generation.effectIds.length && next(counts[2], counts[3]) > 0) {
            const effect = structuredClone(rules.diveLoot.effects.find(e => e.id === generation.effectIds[next(0, generation.effectIds.length - 1)])!);
            if (effect.trigger === 'DoubleAttack')
                effect.chance = next(generation.doubleAttackMinimumChance, generation.doubleAttackMaximumChance);
            item.effects = [{ ...effect, activity: effect.activity as Activity | null, description: effectText(effect) }];
        }
        const [power, stamina] = [[1, 1], [2, 2], [3, 4], [5, 7], [8, 10]][ri];
        item.attributes.power = power;
        item.attributes.stamina = stamina;
    }
    return item;
}


