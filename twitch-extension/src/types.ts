export type Rarity = "Common" | "Uncommon" | "Rare" | "Epic" | "Legendary";
export const slots = [
  "Weapon",
  "Helmet",
  "Chest",
  "Gloves",
  "Boots",
  "Ring1",
  "Ring2",
  "Amulet",
] as const;
export type Slot = (typeof slots)[number];
export interface CharacterStats {
  damage: number;
  critChance: number;
  critDamage: number;
  luck: number;
  bossDamage: number;
  fishingLuck: number;
  diveLuck: number;
  pearlBonus: number;
}
export interface ItemEffect {
  description: string;
  chance?: number;
  damageMultiplier?: number;
}
export interface InventoryItem {
  instanceId: string;
  itemId: string;
  name: string;
  description: string;
  icon: string;
  rarity: Rarity;
  slot?: Slot;
  attributes: Partial<CharacterStats>;
  effects: ItemEffect[];
  favorite: boolean;
  setId?: string;
}
export type Equipment = Partial<Record<Slot, string>>;
export interface ItemSet {
  id: string;
  name: string;
  demoOnly: boolean;
  bonuses: {
    pieces: number;
    description: string;
    attributes: Partial<CharacterStats>;
  }[];
}
export interface ActiveBuff {
  recipeId: string;
  name: string;
  remainingUses: number;
  activity: "Boss" | "Dive" | "Fishing";
  attributes: Partial<CharacterStats>;
}
export interface PlayerProfile {
  id: string;
  username: string;
  avatarUrl: string;
  pearls: number;
  hp: number;
  maxHp: number;
  baseStats: CharacterStats;
  buffs: ActiveBuff[];
}
export interface DiveResult {
  kind: "dive";
  pearls: number;
  items: InventoryItem[];
  message: string;
}
export interface FishingResult {
  kind: "fishing";
  pearls: number;
  fish: { id: string; name: string; rarity: Rarity; icon: string };
  message: string;
}
export interface CombatExchange {
  turn: number;
  damageDealt: number;
  damageTaken: number;
  outcome: "hit" | "victory" | "defeat";
}
export interface BossState {
  name: string;
  hp: number;
  maxHp: number;
  status: "active" | "waiting";
  damage: number;
  nextAt: number;
  lastExchange?: CombatExchange;
}
export interface HotTimeState {
  endsAt: number;
  active: boolean;
}
export interface Perk {
  id: string;
  name: string;
  level: number;
  maxLevel: number;
  bonus: Partial<CharacterStats>;
}
export interface SushiRecipe {
  id: string;
  name: string;
  fishId: string;
  fishName: string;
  fishQuantity: number;
  pearlCost: number;
  activity: ActiveBuff["activity"];
  attributes: Partial<CharacterStats>;
}
export interface DemoState {
  player: PlayerProfile;
  fish: Record<string, number>;
  sushi: Record<string, number>;
  inventory: InventoryItem[];
  equipment: Equipment;
  perks: Perk[];
  boss: BossState;
  hotTime: HotTimeState;
  cooldowns: { dive: number; fishing: number };
  result?: DiveResult | FishingResult;
}
export type Action =
  | { type: "dive"; stake: number }
  | { type: "fishing" }
  | { type: "equip"; id: string; slot: Slot }
  | { type: "unequip"; slot: Slot }
  | { type: "favorite" | "dismantle"; id: string }
  | { type: "perk"; id: string }
  | { type: "cook" | "activateSushi"; recipeId: string }
  | { type: "boss" }
  | { type: "restartFight" }
  | { type: "hot"; duration: number }
  | { type: "reset" };
// A future server adapter must validate JWT, channel, role, expiry and every command.
// Clients send intent only, never authoritative balances, rewards or trusted usernames.
export interface RpgDataSource {
  readonly mode: "demo" | "remote";
  getSnapshot(): DemoState;
  dispatch(action: Action): Promise<DemoState>;
}
