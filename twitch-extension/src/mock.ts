import {learnTalent,talentBonus,underwaterDodgeChance,avoidsUnderwaterHit} from './talents';
import { initialState, sets, recipes, rarities, fishSpecies, rules, reference, emptyStats, generateItem, newBoss } from './data';
import type { Action, CharacterStats, CollectorStats, DemoState, InventoryItem, RpgDataSource, Activity, ActiveBuff, Rarity } from './types';
export const remaining = (until: number, now: number) => Math.max(0, Math.ceil((until - now) / 1000));
export const hotActive = (s: DemoState, now: number) => s.hotTime.active && s.hotTime.endsAt > now;
export const buffActive = (b: ActiveBuff, now: number) => b.expiresAt !== undefined ? b.expiresAt > now : b.remainingUses > 0;
export function setCount(s: DemoState, id: string) { const set = sets.find(t => t.id === id); return new Set(s.inventory.filter(i => i.setId === id && set?.itemIds.includes(i.itemId) && Object.values(s.equipment).includes(i.instanceId)).map(i => i.itemId)).size; }
const add = (a: CharacterStats, b: Partial<CharacterStats>) => { for (const k of Object.keys(b) as (keyof CharacterStats)[])
    a[k] += b[k] ?? 0; };
export function characterStats(s: DemoState, activity: Activity = 'Boss', now = Date.now(), buffs = false, effects = true, stake = 0): CharacterStats {
    const stats = emptyStats();
    add(stats, talentBonus(s.talents??{},activity));
    const gear = s.inventory.filter(i => Object.values(s.equipment).includes(i.instanceId));
    gear.forEach(i => add(stats, i.attributes));
    s.perks.forEach(p => add(stats, Object.fromEntries(Object.entries(p.bonus).map(([k, v]) => [k, v * p.level]))));
    sets.forEach(set => set.bonuses.filter(b => setCount(s, set.id) >= b.pieces).forEach(b => add(stats, b.attributes)));
    if (buffs)
        s.player.buffs.filter(b => b.activity === activity && buffActive(b, now)).forEach(b => add(stats, b.attributes));
    if (effects)
        gear.forEach(i => i.effects.filter(e => !e.requiresCritical && (!e.activity || e.activity === activity) && stake >= e.minimumStake && (s.effectCooldowns[i.instanceId + ':' + e.id] ?? 0) <= now).forEach(e => { const k = ({ DoubleAttack: 'doubleAttackChance', TripleAttack: 'tripleAttackChance', ExtraLoot: 'extraLootChance', SushiPreservation: 'sushiPreservationChance' } as const)[e.trigger as 'DoubleAttack']; if (k)
            stats[k] += e.chance; }));
    stats.maximumHp = Math.min(rules.health.equipmentHpCap, Math.max(0, stats.maximumHp + stats.stamina * rules.health.hpPerStamina));
    stats.damage = Math.min(rules.damageBonusCap, stats.damage + stats.power * rules.powerDamagePerPoint);
    const caps: Partial<CharacterStats> = { critChance: rules.critChanceCap, critDamage: rules.critDamageCap, luck: rules.luckCap, fishingLuck: rules.luckCap, diveLuck: rules.luckCap, bossDamage: rules.damageBonusCap, pearlBonus: rules.pearlBonusCap, doubleAttackChance: rules.doubleAttackChanceCap, tripleAttackChance: rules.tripleAttackChanceCap, extraLootChance: rules.extraLootChanceCap, sushiPreservationChance: rules.sushiPreservationChanceCap, bossDamageReduction: 80, bossDodgeChance: 100, criticalHealing: 100, rareEquipmentLuck: 100 };
    if (buffs && activity === 'Boss')
        stats.bossDamageReduction += s.player.buffs.filter(b => b.category === 'Defensive' && buffActive(b, now)).reduce((n, b) => Math.max(n, b.bossDamageReduction), 0);
    for (const [k, v] of Object.entries(caps))
        stats[k as keyof CharacterStats] = Math.min(v, Math.max(0, stats[k as keyof CharacterStats]??0));
    return stats;
}
export const activityStats = (s: DemoState, activity: Activity, now = Date.now()) => characterStats(s, activity, now, true);
export function collectors(s: DemoState): CollectorStats { const a: CollectorStats = { fishingPearls: 0, divePearls: 0, bossPearls: 0, bossExperience: 0, treasureChance: 0, rareTreasureChance: 0 }; const seen = new Set<string>(); for (const i of s.inventory) {
    if (!i.collector || seen.has(i.itemId))
        continue;
    seen.add(i.itemId);
    for (const k of Object.keys(a) as (keyof CollectorStats)[])
        a[k] += i.collector[k] ?? 0;
} for (const k of Object.keys(a) as (keyof CollectorStats)[])
    a[k] = Math.min(a[k], k === 'bossExperience' ? rules.diveLoot.collectorExperienceCap : k.includes('Chance') ? rules.diveLoot.collectorChanceCap : rules.diveLoot.collectorPearlCap); return a; }
export function diveInfo(s: DemoState, stake: number, now: number) { const tier = [...rules.diveLoot.riskTiers].reverse().find(t => stake >= t.minimumStake)!; const stats = activityStats(s, 'Dive', now); const luck = Math.min(rules.diveLoot.maximumLuckBonus, (stats.luck + stats.diveLuck) * rules.diveLoot.luckChancePerPoint); return { chance: Math.min(rules.diveLoot.maximumDropChance, rules.diveLoot.baseDropChance * (hotActive(s, now) ? 1 + rules.hotTime.dropBonusPercent / 100 : 1) + tier.dropBonus + luck + rules.diveLoot.eventBonus), rarities: rarities.slice(0, rarities.indexOf(tier.maximumRarity as Rarity) + 1) }; }
export const perkCost = (level: number) => 500 * 2 ** level;
const fingerprint = (s: DemoState) => JSON.stringify([s.inventory, s.equipment]);
export class MockRpgDataSource implements RpgDataSource {
    readonly mode = 'demo' as const;
    private state: DemoState;
    private serial = 0;
    private queue: Promise<unknown> = Promise.resolve();
    constructor(private now = () => Date.now(), private random = () => Math.random(), seed?: DemoState) { this.state = structuredClone(seed ?? initialState(now())); this.serial=Math.max(0,...this.state.inventory.map(i=>Number(/^loot-(\d+)$/.exec(i.instanceId)?.[1]??0))); }
    private fixtureClaims=new Map<string,string>();
    // Only the local test transport can settle canned response fixtures. Not a live API intent.
    settleMinigameFixture(id:string,stake:number,payout:number):Promise<void> {
        const task=this.queue.then(()=>{
            const signature=JSON.stringify([stake,payout]);const previous=this.fixtureClaims.get(id);
            if(previous){if(previous!==signature)throw Error('Testanfrage-ID bereits verwendet.');return;}
            if(!Number.isSafeInteger(stake)||!Number.isSafeInteger(payout)||stake<0||payout<0||stake>this.state.player.pearls)throw Error('Nicht genügend Demo-Perlen.');
            this.state.player.pearls+=payout-stake;this.fixtureClaims.set(id,signature);
        });this.queue=task.catch(()=>undefined);return task;
    }
    getSnapshot() { return structuredClone(this.state); }
    private int(a: number, b: number) { return a + Math.floor(this.random() * (b - a + 1)); }
    private roll(chance: number) { return chance > 0 && this.random() * 100 < chance; }
    private weighted<T>(items: T[], weight: (i: T) => number): T { let n = this.random() * items.reduce((sum, i) => sum + weight(i), 0); for (const item of items) {
        n -= weight(item);
        if (n < 0)
            return item;
    } return items[items.length - 1]; }
    private maximum(s: DemoState) { return rules.health.baseHp + (s.player.level - 1) * rules.health.hpPerLevel + characterStats(s).maximumHp; }
    private refresh(s: DemoState, now: number) {
        const p = s.player;
        p.maxHp = this.maximum(s);
        p.hp = Math.min(p.hp, p.maxHp);
        const intervals = Math.max(0, Math.floor((now - p.lastPassiveRegen) / (rules.health.passiveSeconds * 1000)));
        if (intervals) {
            const end = p.lastPassiveRegen + intervals * rules.health.passiveSeconds * 1000;
            let extra = p.regenRemainder;
            for (const b of p.buffs.filter(b => b.passiveHealing > 0)) {
                const start = Math.max(b.activatedAt, p.lastPassiveRegen);
                const until = Math.min(end, b.expiresAt ?? end);
                extra += Math.max(0, until - start) / 60000 * b.passiveHealing;
            }
            p.hp = Math.min(p.maxHp, p.hp + intervals * rules.health.passiveHp + Math.floor(extra));
            p.regenRemainder = p.hp >= p.maxHp ? 0 : extra - Math.floor(extra);
            p.lastPassiveRegen = end;
        }
        if (s.salvageQuote && now >= s.salvageQuote.expiresAt)
            s.salvageQuote = undefined;
        if (s.boss.status === 'active' && now >= s.boss.endsAt)
            this.closeBoss(s, now, false);
        if (s.boss.status === 'waiting' && now >= s.boss.nextAt) {
            s.round++;
            s.boss = newBoss(now, s.round);
            s.cooldowns.boss = 0;
        }
    }
    private experience(s: DemoState, amount: number) { const p = s.player; const before = p.level; p.experience += Math.max(0, amount); while (p.level < rules.health.maximumLevel && p.experience >= rules.health.experiencePerLevel * p.level * (p.level + 1) / 2)
        p.level++; p.maxHp = this.maximum(s); if (rules.health.healLevelGain)
        p.hp = Math.min(p.maxHp, p.hp + (p.level - before) * rules.health.hpPerLevel); }
    private reward(s: DemoState, base: number, stake: number, activity: 'Dive' | 'Fishing' | 'Boss', now: number) { let net = Math.max(0, base - stake); const stats = activityStats(s, activity, now); net += Math.floor(net * stats.pearlBonus / 100); const c = collectors(s); net += Math.floor(net * (activity === 'Dive' ? c.divePearls : activity === 'Fishing' ? c.fishingPearls : c.bossPearls) / 100); if (hotActive(s, now))
        net += Math.floor(net * rules.hotTime.pearlBonusPercent / 100); return Math.min(base, stake) + net; }
    private closeBoss(s: DemoState, now: number, victory: boolean) {
        const eligible = s.boss.damage > 0 && s.player.hp > 0 && s.player.defeatedBossId !== s.boss.id;
        const exp = eligible ? Math.floor((victory ? rules.health.victoryExperience + rules.health.topExperience : rules.health.escapeExperience) * (1 + collectors(s).bossExperience / 100)) : 0;
        const pearls = eligible && victory ? this.reward(s, reference.adventures.boss.rewardPool, 0, 'Boss', now) : 0;
        s.player.pearls += pearls;
        this.experience(s, exp);
        s.boss.reward = { pearls, experience: exp, message: eligible ? victory ? 'Boss besiegt – du hast überlebt!' : 'Boss entkommen – Überlebens-EXP.' : 'Keine Belohnung: nicht teilgenommen oder besiegt.' };
        s.boss.status = 'waiting';
        s.boss.nextAt = now + reference.adventures.boss.roundCooldownSeconds * 1000;
        for (const b of s.player.buffs)
            if (b.lastBossId === s.boss.id && b.remainingUses === 0)
                b.expiresAt = Math.min(b.expiresAt ?? now, now);
        if (victory && rules.hotTime.autoAfterBoss && !hotActive(s, now))
            s.hotTime = { active: true, endsAt: now + rules.hotTime.durationMinutes * 60000 };
    }
    private proc(s: DemoState, activity: Activity, trigger: string, now: number, critical = false, stake = 0): number { const stats = characterStats(s, activity, now, true, false); const key = ({ DoubleAttack: 'doubleAttackChance', TripleAttack: 'tripleAttackChance', ExtraLoot: 'extraLootChance', SushiPreservation: 'sushiPreservationChance' } as const)[trigger as 'DoubleAttack']; const cap = ({ DoubleAttack: rules.doubleAttackChanceCap, TripleAttack: rules.tripleAttackChanceCap, ExtraLoot: rules.extraLootChanceCap, SushiPreservation: rules.sushiPreservationChanceCap } as Record<string, number>)[trigger]; const candidates: {
        key: string;
        chance: number;
        strength: number;
        cooldown: number;
    }[] = []; if (stats[key] > 0)
        candidates.push({ key: '', chance: stats[key], strength: .5, cooldown: 0 }); for (const i of s.inventory.filter(i => Object.values(s.equipment).includes(i.instanceId)))
        for (const e of i.effects)
            if (e.trigger === trigger && (!e.activity || e.activity === activity) && (!e.requiresCritical || critical) && stake >= e.minimumStake && (s.effectCooldowns[i.instanceId + ':' + e.id] ?? 0) <= now)
                candidates.push({ key: i.instanceId + ':' + e.id, chance: e.chance, strength: e.strength, cooldown: e.cooldownSeconds }); if (!candidates.length || !this.roll(Math.min(cap, candidates.reduce((n, c) => n + c.chance, 0))))
        return 0; const chosen = this.weighted(candidates, c => c.chance); if (chosen.key && chosen.cooldown)
        s.effectCooldowns[chosen.key] = now + chosen.cooldown * 1000; return chosen.strength; }
    private loot(s: DemoState, stake: number, now: number, activity: Activity = 'Dive', chance?: number, max?: Rarity): InventoryItem | undefined {
        const info = diveInfo(s, stake, now);
        const available = rarities.slice(0, max ? rarities.indexOf(max) + 1 : info.rarities.length);
        const pool = rules.diveLoot.items.filter(i => i.dropActivities.includes(activity) && available.includes(i.rarity as Rarity));
        if (!pool.length || !this.roll(chance ?? info.chance))
            return;
        const rarity = this.weighted(available.filter(r => pool.some(i => i.rarity === r)), r => rules.diveLoot.rarityWeights[r.toLowerCase() as keyof typeof rules.diveLoot.rarityWeights]);
        let candidates = pool.filter(i => i.rarity === rarity);
        const treasures = candidates.filter(i => i.collector);
        const regular = candidates.filter(i => !i.collector);
        if (treasures.length && regular.length) {
            const c = collectors(s);
            const bonus = Math.min(rules.diveLoot.collectorChanceCap, c.treasureChance + (rarities.indexOf(rarity) >= 2 ? c.rareTreasureChance : 0));
            candidates = this.roll(rules.diveLoot.collectorPoolChance * (1 + bonus / 100)) ? treasures : regular;
        }
        if (rarities.indexOf(rarity) >= 2 && !candidates.some(i => i.collector)) {
            const gear = candidates.filter(i => i.slot);
            const other = candidates.filter(i => !i.slot);
            if (gear.length && other.length) {
                const rate = gear.reduce((n, i) => n + i.dropWeight, 0) / candidates.reduce((n, i) => n + i.dropWeight, 0);
                candidates = this.roll(Math.min(100, rate * 100 * (1 + activityStats(s, activity, now).rareEquipmentLuck / 100))) ? gear : other;
            }
        }
        const chosen = this.weighted(candidates, i => i.dropWeight);
        return generateItem(chosen.itemId, `loot-${++this.serial}`, () => this.random());
    }
    private storeLoot(s: DemoState, item: InventoryItem) { if (item.kind === 'Consumable') {
        const definition = rules.diveLoot.items.find(i => i.itemId === item.itemId)!;
        s.sushi[definition.consumableRecipeId] = (s.sushi[definition.consumableRecipeId] ?? 0) + 1;
    }
    else
        s.inventory.push(item); }
    dispatch(action: Action): Promise<DemoState> { const pending = this.queue.then(() => this.execute(action)); this.queue = pending.catch(() => undefined); return pending; }
    private async execute(action: Action) {
        const now = this.now();
        if (action.type === 'reset') {
            this.state = initialState(now);
            return this.getSnapshot();
        }
        const s = structuredClone(this.state);
        this.refresh(s, now);
        const find = (id: string) => { const item = s.inventory.find(i => i.instanceId === id); if (!item)
            throw Error('Gegenstand nicht gefunden.'); return item; };
        switch (action.type) {
            case 'learnTalent':
                s.talents=learnTalent(action.id,s.player.level,s.talents??{});
                this.refresh(s,now);
                break;
            case 'refresh': break;
            case 'demoWound':
                s.player.hp = Math.min(s.player.hp, Math.max(1, Math.floor(s.player.maxHp * .2)));
                break;
            case 'healChat': {
                if (now - s.player.lastChatHeal < rules.health.chatCooldownSeconds * 1000)
                    throw Error('Chat-Heilung hat noch Cooldown.');
                const bonus = s.player.buffs.filter(b => b.category === 'Defensive' && buffActive(b, now)).reduce((n, b) => Math.max(n, b.chatHealing), 0);
                s.player.hp = Math.min(s.player.maxHp, s.player.hp + rules.health.chatHp + bonus);
                s.player.lastChatHeal = now;
                break;
            }
            case 'newBoss': {
                for (const b of s.player.buffs)
                    if (b.lastBossId === s.boss.id && b.remainingUses === 0)
                        b.expiresAt = now;
                s.round++;
                s.boss = newBoss(now, s.round);
                s.cooldowns.boss = 0;
                break;
            }
            case 'favorite': {
                const i = find(action.id);
                i.favorite = !i.favorite;
                break;
            }
            case 'lock': {
                const i = find(action.id);
                i.locked = !i.locked;
                break;
            }
            case 'equip': {
                const i = find(action.id);
                if (!i.slot || !(i.slot === action.slot || (i.slot.startsWith('Ring') && action.slot.startsWith('Ring'))))
                    throw Error('Unpassender Ausrüstungsplatz.');
                for (const slot of Object.keys(s.equipment) as (keyof typeof s.equipment)[])
                    if (s.equipment[slot] === i.instanceId)
                        delete s.equipment[slot];
                s.equipment[action.slot] = i.instanceId;
                this.refresh(s, now);
                break;
            }
            case 'unequip':
                delete s.equipment[action.slot];
                this.refresh(s, now);
                break;
            case 'dismantle':
            case 'salvageBatch': {
                let items: InventoryItem[];
                const blocked = (i: InventoryItem) => i.favorite || i.locked || Object.values(s.equipment).includes(i.instanceId);
                if (action.type === 'dismantle') {
                    items = [find(action.id)];
                    if (blocked(items[0]))
                        throw Error('Favorisierte, gesperrte oder ausgerüstete Items sind geschützt.');
                }
                else {
                    const seen = new Set<string>();
                    items = [];
                    const inventory = [...s.inventory].sort((a, b) => Number(blocked(b)) - Number(blocked(a)));
                    for (const i of inventory) {
                        const signature = JSON.stringify([i.itemId, i.name, i.description, i.rarity, i.slot, i.kind, i.setId, i.attributes, i.collector, [...i.effects].sort((a, b) => a.id.localeCompare(b.id))]);
                        const duplicate = seen.has(signature);
                        seen.add(signature);
                        if (!blocked(i) && i.rarity !== 'Legendary' && !i.setId && (action.selection === 'common' ? i.rarity === 'Common' : duplicate))
                            items.push(i);
                    }
                }
                if (!items.length)
                    throw Error('Keine passenden ungeschützten Items.');
                if (items.length > rules.salvage.maximumBatchSize)
                    throw Error('Zu viele Items für eine Vorschau.');
                const rows = [];
                for (const i of items) {
                    const range = rules.salvage.values[i.rarity.toLowerCase() as keyof typeof rules.salvage.values];
                    let value = i.salvageValue;
                    if (!value) {
                        const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(`salvage-v1:${i.instanceId}:${rarities.indexOf(i.rarity)}`));
                        value = range.minimum + Number(new DataView(hash).getBigUint64(0, true) % BigInt(range.maximum - range.minimum + 1));
                    }
                    rows.push({ id: i.instanceId, name: i.name, value, warning: i.rarity === 'Legendary' || !!i.setId });
                }
                s.salvageQuote = { token: `quote-${++this.serial}`, expiresAt: now + rules.salvage.confirmationSeconds * 1000, items: rows, total: rows.reduce((n, i) => n + i.value, 0), fingerprint: fingerprint(s) };
                break;
            }
            case 'cancelSalvage':
                s.salvageQuote = undefined;
                break;
            case 'confirmSalvage': {
                const q = s.salvageQuote;
                if (!q || q.token !== action.token || q.expiresAt <= now || q.fingerprint !== fingerprint(s))
                    throw Error('Vorschau abgelaufen oder Inventar verändert. Bitte neu prüfen.');
                const ids = q.items.map(i => i.id);
                s.inventory = s.inventory.filter(i => !ids.includes(i.instanceId));
                s.player.pearls += q.total;
                s.salvageQuote = undefined;
                if (s.result) {
                    s.result.items = s.result.items.filter(i => !ids.includes(i.instanceId));
                    if (s.result.kind === 'dive' && !s.result.items.length)
                        s.result = undefined;
                }
                break;
            }
            case 'perk': {
                const p = s.perks.find(p => p.id === action.id);
                if (!p || p.level >= p.maxLevel)
                    throw Error('Maximallevel erreicht.');
                const cost = perkCost(p.level);
                if (s.player.pearls < cost)
                    throw Error('Nicht genügend Demo-Perlen.');
                s.player.pearls -= cost;
                p.level++;
                break;
            }
            case 'cook': {
                const r = recipes.find(r => r.id === action.recipeId);
                if (!r)
                    throw Error('Unbekanntes Sushi-Rezept.');
                if (r.ingredients.some(i => (s.fish[i.fishId] ?? 0) < i.quantity) || s.player.pearls < r.pearlCost)
                    throw Error('Nicht genügend Zutaten oder Perlen.');
                for (const i of r.ingredients)
                    s.fish[i.fishId] -= i.quantity;
                s.player.pearls -= r.pearlCost;
                s.sushi[r.id] = (s.sushi[r.id] ?? 0) + 1;
                break;
            }
            case 'activateSushi': {
                const r = recipes.find(r => r.id === action.recipeId);
                if (!r || (s.sushi[r.id] ?? 0) < 1)
                    throw Error('Kein passendes Sushi vorhanden.');
                if (s.player.buffs.some(b => b.category === r.category && buffActive(b, now)))
                    throw Error('Dieser Sushi-Slot ist bereits belegt.');
                s.sushi[r.id]--;
                s.player.buffs.push({ recipeId: r.id, name: r.name, category: r.category, activity: r.activity, remainingUses: r.bossUses, attributes: structuredClone(r.attributes), activatedAt: now, passiveHealing: r.passiveHealing, chatHealing: r.chatHealing, bossDamageReduction: r.bossDamageReduction, bossLifeleech: r.bossLifeleech });
                break;
            }
            case 'hot':
                if (![0, 30000, 300000].includes(action.duration))
                    throw Error('Ungültige Demozeit.');
                if (action.duration === 0)
                    s.hotTime = { active: false, endsAt: now };
                else if (!hotActive(s, now))
                    s.hotTime = { active: true, endsAt: now + action.duration };
                else
                    throw Error('Aktive Hot Time kann nicht verlängert werden. Zuerst beenden.');
                break;
            case 'boss': {
                if (s.boss.status !== 'active')
                    throw Error('Der nächste Boss kommt bald.');
                if (s.player.hp <= 0 || s.player.defeatedBossId === s.boss.id)
                    throw Error('Für diesen Boss besiegt. Heilung hebt die Todessperre nicht auf.');
                if (s.cooldowns.boss > now)
                    throw Error('Bossangriff hat noch Cooldown.');
                for (const b of s.player.buffs.filter(b => buffActive(b, now) && b.lastBossId !== s.boss.id)) {
                    b.lastBossId = s.boss.id;
                    b.remainingUses--;
                    if (b.remainingUses === 0)
                        b.expiresAt = s.boss.endsAt;
                }
                const stats = activityStats(s, 'Boss', now);
                const critical = this.roll(stats.critChance);
                const regular = Math.floor(this.int(10, 30) * (1 + stats.damage / 100) * (1 + stats.bossDamage / 100) * (critical ? rules.baseCritMultiplier + stats.critDamage / 100 : 1));
                let strength = this.proc(s, 'Boss', 'TripleAttack', now, critical);
                let extraHits = strength > 0 ? 2 : 0;
                if (!extraHits) {
                    strength = this.proc(s, 'Boss', 'DoubleAttack', now, critical);
                    if (strength)
                        extraHits = 1;
                }
                const actual = Math.min(s.boss.hp, regular + extraHits * Math.floor(regular * strength));
                s.boss.hp -= actual;
                s.boss.damage += actual;
                const leech = s.player.buffs.filter(b => b.category === 'Defensive' && buffActive(b, now)).reduce((n, b) => Math.max(n, b.bossLifeleech), 0);
                const healing = Math.min(s.player.maxHp - s.player.hp, Math.floor(actual * (leech + (critical ? stats.criticalHealing : 0)) / 100));
                s.player.hp += healing;
                const health = rules.health;
                const range = s.boss.maxHp >= health.legendaryThreshold ? [health.legendaryMinimum, health.legendaryMaximum] : s.boss.maxHp >= health.eliteThreshold ? [health.eliteMinimum, health.eliteMaximum] : s.boss.maxHp >= health.strongThreshold ? [health.strongMinimum, health.strongMaximum] : [health.commonMinimum, health.commonMaximum];
                const dodged = s.boss.hp > 0 && this.roll(stats.bossDodgeChance);
                const damageTaken = s.boss.hp === 0 || dodged ? 0 : Math.min(s.player.hp, Math.ceil(this.int(range[0], range[1]) * (1 - stats.bossDamageReduction / 100)));
                s.player.hp -= damageTaken;
                if (s.player.hp === 0)
                    s.player.defeatedBossId = s.boss.id;
                s.boss.lastExchange = { turn: (s.boss.lastExchange?.turn ?? 0) + 1, damageDealt: actual, damageTaken, healing, critical, extraHits, dodged, outcome: s.boss.hp === 0 ? 'victory' : s.player.hp === 0 ? 'defeat' : 'hit' };
                s.cooldowns.boss = now + reference.adventures.boss.attackCooldownSeconds * 1000;
                if (s.boss.hp === 0) {
                    if (this.roll(rules.bossLootChance * (hotActive(s, now) ? 1.5 : 1))) {
                        const found = this.loot(s, 0, now, 'Boss', 100, 'Epic');
                        if (found)
                            this.storeLoot(s, found);
                    }
                    this.closeBoss(s, now, true);
                }
                break;
            }
            case 'dive':
            case 'fishing': {
                const activity: Activity = action.type === 'dive' ? 'Dive' : 'Fishing';
                const stake = action.type === 'dive' ? action.stake : action.stake ?? 0;
                const game = activity === 'Dive' ? reference.adventures.treasure : reference.adventures.fishing;
                if (s.cooldowns[action.type] > now)
                    throw Error('Die Aktion hat noch Cooldown.');
                if (!Number.isSafeInteger(stake) || stake < 0 || stake > game.maximumBet || (stake > 0 && stake < game.minimumBet) || stake > s.player.pearls)
                    throw Error('Ungültiger Einsatz oder zu wenige Perlen.');
                if (activity === 'Dive' && s.player.hp <= 0)
                    throw Error('Vor dem Tauchen erst heilen. Kein Einsatz abgebucht.');
                const roll = this.int(1, 100);
                const multiplier = roll <= game.lossChance ? game.lossMultiplier : roll <= game.lossChance + game.smallChance ? game.smallMultiplier : roll <= game.lossChance + game.smallChance + game.largeChance ? game.largeMultiplier : game.rareMultiplier;
                let payout = Math.floor(stake * multiplier);
                const items: InventoryItem[] = [];
                let fish: typeof fishSpecies[number] | undefined;
                if (activity === 'Fishing' && roll > game.lossChance) {
                    const stats = activityStats(s, activity, now);
                    fish = this.weighted(fishSpecies.filter(f => f.minimumStake <= stake), f => Math.min(1000000, Math.floor(f.weight * (rarities.indexOf(f.rarity as Rarity) >= 2 ? 1 + Math.min(100, stats.luck + stats.fishingLuck) / 100 : 1))));
                    payout = fish.pearlReward !== null ? stake + fish.pearlReward : stake === 0 ? rules.freeFishingReward : payout;
                }
                payout = this.reward(s, payout, stake, activity, now);
                s.player.pearls += payout - stake;
                s.cooldowns[action.type] = now + game.cooldownSeconds * 1000;
                if (activity === 'Dive') {
                    const found = this.loot(s, stake, now);
                    if (found) {
                        items.push(found);
                        this.storeLoot(s, found);
                    }
                    if (this.proc(s, 'Dive', 'ExtraLoot', now, false, stake) > 0) {
                        const extra = this.loot(s, stake, now);
                        if (extra) {
                            items.push(extra);
                            this.storeLoot(s, extra);
                        }
                    }
                    let enemy: DemoState['result'] extends never ? never : import('./types').EnemyResult | undefined;
                    if (this.roll(rules.enemies.encounterChance)) {
                        const tier = [...rules.enemies.tiers].reverse().find(e => e.minimumStake <= stake)!;
                        let hp = tier.hitPoints, rounds = 0, taken = 0;
                        const stats = activityStats(s, 'Dive', now);
                        while (hp > 0 && s.player.hp > 0 && rounds < rules.enemies.maximumRounds) {
                            rounds++;
                            hp = Math.max(0, hp - Math.max(1, Math.floor(this.int(10, 30) * (1 + stats.damage / 100) * (1 + stats.bossDamage / 100) * (this.roll(stats.critChance) ? rules.baseCritMultiplier + stats.critDamage / 100 : 1))));
                            if (hp > 0 && !(underwaterDodgeChance(s.talents??{})>0 && avoidsUnderwaterHit(s.talents??{},this.random()*100))) {
                                const hit = Math.min(s.player.hp, this.int(tier.minimumDamage, tier.maximumDamage));
                                s.player.hp -= hit;
                                taken += hit;
                            }
                        }
                        const victory = hp === 0 && s.player.hp > 0;
                        if (s.player.hp === 0 && s.boss.damage > 0)
                            s.player.defeatedBossId = s.boss.id;
                        if (victory) {
                            this.experience(s, tier.experience);
                            const first = this.loot(s, stake, now), second = this.loot(s, stake, now);
                            const best = !first ? second : second && rarities.indexOf(second.rarity) > rarities.indexOf(first.rarity) ? second : first;
                            if (best) {
                                items.push(best);
                                this.storeLoot(s, best);
                            }
                        }
                        enemy = { name: tier.name, victory, rounds, damageTaken: taken, experience: victory ? tier.experience : 0 };
                    }
                    s.result = { kind: 'dive', stake, pearls: payout, items, enemy, message: items.length ? 'Ein Schatz aus der Tiefe!' : 'Tauchgang abgeschlossen.' };
                }
                else {
                    if (fish) {
                        s.fish[fish.id] = (s.fish[fish.id] ?? 0) + 1;
                        const gear = this.loot(s, 0, now, 'Fishing', rules.fishingEquipmentChance * (hotActive(s, now) ? 1.5 : 1), 'Epic');
                        if (gear) {
                            items.push(gear);
                            this.storeLoot(s, gear);
                        }
                    }
                    s.result = { kind: 'fishing', stake, pearls: payout, items, fish: fish ? { id: fish.id, name: fish.name, rarity: fish.rarity as Rarity, icon: '🐟' } : undefined, message: fish ? 'Ein schimmernder Fang!' : 'Heute kein Fang. Der Cooldown gilt auch bei Fehlfang.' };
                }
                break;
            }
        }
        this.state = s;
        return this.getSnapshot();
    }
}
