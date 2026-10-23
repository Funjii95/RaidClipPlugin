export type Rarity = 'Common' | 'Uncommon' | 'Rare' | 'Epic' | 'Legendary';
export const slots = ['Weapon', 'Helmet', 'Chest', 'Gloves', 'Boots', 'Ring1', 'Ring2', 'Amulet'] as const;
export type Slot = typeof slots[number];
export type Activity = 'Dive' | 'Fishing' | 'Boss';
export type SushiCategory = 'Offensive' | 'Defensive';
export interface CharacterStats {
    armor?: number;
    armorPercent?: number;
    maximumHpPercent?: number;
    attackSpeed?: number;
    healingPower?: number;
    damage: number;
    critChance: number;
    critDamage: number;
    luck: number;
    bossDamage: number;
    fishingLuck: number;
    diveLuck: number;
    pearlBonus: number;
    power: number;
    stamina: number;
    maximumHp: number;
    doubleAttackChance: number;
    tripleAttackChance: number;
    extraLootChance: number;
    sushiPreservationChance: number;
    bossDamageReduction: number;
    bossDodgeChance: number;
    criticalHealing: number;
    rareEquipmentLuck: number;
}
export interface CollectorStats {
    fishingPearls: number;
    divePearls: number;
    bossPearls: number;
    bossExperience: number;
    treasureChance: number;
    rareTreasureChance: number;
}
export interface ItemEffect {
    id: string;
    description: string;
    trigger: string;
    chance: number;
    strength: number;
    requiresCritical: boolean;
    minimumStake: number;
    cooldownSeconds: number;
    activity?: Activity | null;
}
export interface InventoryItem {
    upgradeLevel?: number;
    instanceId: string;
    itemId: string;
    name: string;
    description: string;
    icon: string;
    rarity: Rarity;
    slot?: Slot;
    kind: string;
    attributes: Partial<CharacterStats>;
    effects: ItemEffect[];
    favorite: boolean;
    locked: boolean;
    salvageValue: number;
    setId?: string;
    collector?: CollectorStats;
    dropWeight: number;
    activities: Activity[];
}
export type Equipment = Partial<Record<Slot, string>>;
export interface ItemSet {
    id: string;
    name: string;
    description: string;
    demoOnly: boolean;
    itemIds: string[];
    bonuses: {
        pieces: number;
        description: string;
        attributes: Partial<CharacterStats>;
    }[];
}
export interface ActiveBuff {
    recipeId: string;
    name: string;
    activity: Activity;
    category: SushiCategory;
    remainingUses: number;
    attributes: Partial<CharacterStats>;
    activatedAt: number;
    expiresAt?: number;
    lastBossId?: string;
    passiveHealing: number;
    chatHealing: number;
    bossDamageReduction: number;
    bossLifeleech: number;
}
export interface PlayerProfile {
    id: string;
    username: string;
    avatarUrl: string;
    pearls: number;
    hp: number;
    maxHp: number;
    level: number;
    experience: number;
    lastPassiveRegen: number;
    regenRemainder: number;
    lastChatHeal: number;
    defeatedBossId?: string;
    shieldHp?: number;
    baseStats: CharacterStats;
    buffs: ActiveBuff[];
}
export interface EnemyResult {
    name: string;
    victory: boolean;
    rounds: number;
    damageTaken: number;
    experience: number;
}
export interface DiveResult {
    kind: 'dive';
    pearls: number;
    stake: number;
    items: InventoryItem[];
    message: string;
    enemy?: EnemyResult;
}
export interface FishingResult {
    kind: 'fishing';
    pearls: number;
    stake: number;
    fish?: {
        id: string;
        name: string;
        rarity: Rarity;
        icon: string;
    };
    items: InventoryItem[];
    message: string;
}
export interface CombatExchange {
    turn: number;
    damageDealt: number;
    damageTaken: number;
    healing: number;
    critical: boolean;
    extraHits: number;
    dodged: boolean;
    outcome: 'hit' | 'victory' | 'defeat';
}
export interface BossState {
    isMimic?:boolean;
    legendaryChance?:number;
    id: string;
    name: string;
    hp: number;
    maxHp: number;
    status: 'active' | 'waiting';
    damage: number;
    nextAt: number;
    endsAt: number;
    lastExchange?: CombatExchange;
    reward?: {
        pearls: number;
        experience: number;
        message: string;
    };
}
export interface HotTimeState {
    endsAt: number;
    active: boolean;
}
export interface Perk {
    presentation?: {category:string;symbol:string;value:number;label:string};
    id: string;
    name: string;
    level: number;
    maxLevel: number;
    bonus: Partial<CharacterStats>;
}
export interface SushiRecipe {
    id: string;
    name: string;
    ingredients: {
        fishId: string;
        name: string;
        quantity: number;
    }[];
    pearlCost: number;
    activity: Activity;
    category: SushiCategory;
    bossUses: number;
    attributes: Partial<CharacterStats>;
    passiveHealing: number;
    chatHealing: number;
    bossDamageReduction: number;
    bossLifeleech: number;
}
export interface SalvageQuote {
    token: string;
    expiresAt: number;
    items: {
        id: string;
        name: string;
        value: number;
        warning: boolean;
    }[];
    total: number;
    fingerprint: string;
}
export interface MaterialStock { shells:number; whitePearls:number; bluePearls:number; redPearls:number; stoneChips:number; }
export interface DemoState {
    talents?: import("./talents").TalentRanks;
    materials?: MaterialStock;
    remote?: import("./remote/model").RemoteView;
    player: PlayerProfile;
    fish: Record<string, number>;
    sushi: Record<string, number>;
    inventory: InventoryItem[];
    equipment: Equipment;
    perks: Perk[];
    boss: BossState;
    round: number;
    hotTime: HotTimeState;
    cooldowns: {
        dive: number;
        fishing: number;
        boss: number;
    };
    result?: DiveResult | FishingResult;
    salvageQuote?: SalvageQuote;
    effectCooldowns: Record<string, number>;
}
export type Action = {type:'upgrade';id:string;expectedLevel:number;} | {type:'learnTalent';id:string;} | {type:'openShells';count:number;} | {
    type: 'dive';
    stake: number;
} | {
    type: 'fishing';
    stake?: number;
} | {
    type: 'equip';
    id: string;
    slot: Slot;
} | {
    type: 'unequip';
    slot: Slot;
} | {
    type: 'favorite';
    id: string;
} | {
    type: 'lock';
    id: string;
} | {
    type: 'dismantle';
    id: string;
} | {
    type: 'salvageBatch';
    selection: 'common' | 'duplicates';
} | {
    type: 'confirmSalvage';
    token: string;
} | {
    type: 'cancelSalvage';
} | {
    type: 'perk';
    id: string;
    maximum?: boolean;
} | {
    type: 'cook' | 'activateSushi';
    recipeId: string;
} | {
    type: 'boss';
} | {
    type: 'newBoss' | 'healChat' | 'refresh' | 'reset';
} | {
    type: 'demoWound';
} | {
    type: 'hot';
    duration: number;
};
// Remote adapters must verify JWT/channel/identity and authorize intents server-side.
export interface RpgDataSource {
    selfDeletionStatus?():Promise<{request:import('./live/client').SelfDeletionJob|null}>;
    requestSelfDeletion?(requestId:string):Promise<import('./live/client').SelfDeletionJob>;
    readonly minigames?:import('./minigames/contracts').MinigameClient;
    previewAdventures?(stake:number,fishingStake:number):Promise<void>;
    readonly mode: 'demo' | 'remote';
    getSnapshot(): DemoState;
    canDispatch?(type: Action["type"]): boolean;
    subscribe?(listener:()=>void):()=>void;
    retryPending?():Promise<DemoState>;
    requestIdentity?():void;
    dispatch(action: Action): Promise<DemoState>;
}
