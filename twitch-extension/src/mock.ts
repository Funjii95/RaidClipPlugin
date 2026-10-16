import { catalog, initialState, sets } from "./data";
import type {
  Action,
  CharacterStats,
  DemoState,
  InventoryItem,
  RpgDataSource,
} from "./types";
export const remaining = (until: number, now: number) =>
  Math.max(0, Math.ceil((until - now) / 1000));
export const hotActive = (s: DemoState, now: number) =>
  s.hotTime.active && s.hotTime.endsAt > now;
export function setCount(s: DemoState, id: string) {
  return s.inventory.filter(
    (i) => i.setId === id && Object.values(s.equipment).includes(i.instanceId),
  ).length;
}
export function characterStats(s: DemoState) {
  const stats = { ...s.player.baseStats };
  const add = (a: Partial<CharacterStats>) => {
    for (const k of Object.keys(a) as (keyof CharacterStats)[])
      stats[k] += a[k] ?? 0;
  };
  s.inventory
    .filter((i) => Object.values(s.equipment).includes(i.instanceId))
    .forEach((i) => add(i.attributes));
  s.perks.forEach((p) =>
    add(
      Object.fromEntries(
        Object.entries(p.bonus).map(([k, v]) => [k, v * p.level]),
      ),
    ),
  );
  sets.forEach((set) =>
    set.bonuses
      .filter((b) => setCount(s, set.id) >= b.pieces)
      .forEach((b) => add(b.attributes)),
  );
  return stats;
}
export function diveInfo(s: DemoState, stake: number, now: number) {
  const tier =
    stake >= 10000
      ? 4
      : stake >= 5000
        ? 3
        : stake >= 1000
          ? 2
          : stake >= 250
            ? 1
            : 0;
  const chance = Math.min(
    60,
    25 + [0, 4, 8, 12, 15][tier] + Math.min(10, characterStats(s).luck * 0.1),
  );
  return {
    chance: Math.min(100, chance * (hotActive(s, now) ? 1.5 : 1)),
    rarities: ["Common", "Uncommon", "Rare", "Epic", "Legendary"].slice(
      0,
      [2, 3, 4, 4, 5][tier],
    ),
  };
}
export const perkCost = (level: number) => 500 * 2 ** level;
export class MockRpgDataSource implements RpgDataSource {
  readonly mode = "demo" as const;
  private state: DemoState;
  private serial = 0;
  constructor(
    private now = () => Date.now(),
    private random = () => Math.random(),
  ) {
    this.state = initialState(now());
  }
  getSnapshot() {
    return structuredClone(this.state);
  }
  async dispatch(action: Action) {
    const s = this.state,
      now = this.now();
    const find = (id: string) => {
      const i = s.inventory.find((x) => x.instanceId === id);
      if (!i) throw Error("Gegenstand nicht gefunden.");
      return i;
    };
    switch (action.type) {
      case "reset":
        this.state = initialState(now);
        break;
      case "favorite": {
        const i = find(action.id);
        i.favorite = !i.favorite;
        break;
      }
      case "equip": {
        const i = find(action.id);
        if (
          !i.slot ||
          !(
            i.slot === action.slot ||
            (i.slot.startsWith("Ring") && action.slot.startsWith("Ring"))
          )
        )
          throw Error("Unpassender Ausrüstungsplatz.");
        for (const slot of Object.keys(
          s.equipment,
        ) as (keyof typeof s.equipment)[])
          if (s.equipment[slot] === i.instanceId) delete s.equipment[slot];
        s.equipment[action.slot] = i.instanceId;
        break;
      }
      case "unequip":
        delete s.equipment[action.slot];
        break;
      case "dismantle": {
        const i = find(action.id);
        if (i.favorite || Object.values(s.equipment).includes(i.instanceId))
          throw Error("Favorisierte oder ausgerüstete Items sind geschützt.");
        s.inventory = s.inventory.filter((x) => x !== i);
        // A removed inventory instance must also disappear from the loot reveal.
        if (s.result?.kind === "dive") {
          const wasLoot = s.result.items.some(item => item.instanceId === i.instanceId);
          if (wasLoot) {
            s.result.items = s.result.items.filter(item => item.instanceId !== i.instanceId);
            if (s.result.items.length === 0) s.result = undefined;
          }
        }
        s.player.pearls += 25;
        break;
      }
      case "perk": {
        const p = s.perks.find((p) => p.id === action.id);
        if (!p || p.level >= p.maxLevel) throw Error("Maximallevel erreicht.");
        const cost = perkCost(p.level);
        if (s.player.pearls < cost) throw Error("Nicht genügend Demo-Perlen.");
        s.player.pearls -= cost;
        p.level++;
        break;
      }
      case "hot":
        if (![0, 30000, 300000].includes(action.duration))
          throw Error("Ungültige Demozeit.");
        s.hotTime = {
          active: action.duration > 0,
          endsAt: now + action.duration,
        };
        break;
      case "boss": {
        if (s.boss.status === "waiting") {
          if (now < s.boss.nextAt) throw Error("Der nächste Boss kommt bald.");
          s.boss = { ...s.boss, hp: 4000, status: "active", damage: 0 };
        }
        const stats = characterStats(s);
        const buff = s.player.buffs.find(
          (b) => b.activity === "Boss" && b.remainingUses > 0,
        );
        const damage = Math.round(
          (stats.damage + (buff?.attributes.damage ?? 0)) *
            (1 + stats.bossDamage / 100),
        );
        s.boss.hp = Math.max(0, s.boss.hp - damage);
        s.boss.damage += damage;
        if (buff) buff.remainingUses--;
        if (!s.boss.hp) {
          s.boss.status = "waiting";
          s.boss.nextAt = now + 60000;
        }
        break;
      }
      case "dive":
      case "fishing": {
        const kind = action.type;
        if (s.cooldowns[kind] > now)
          throw Error("Die Demo-Aktion hat noch Cooldown.");
        if (
          kind === "dive" &&
          (![0, 250, 1000, 5000, 10000].includes(action.stake) ||
            action.stake > s.player.pearls)
        )
          throw Error("Ungültiger Einsatz oder zu wenige Demo-Perlen.");
        const pearls =
          (kind === "dive" ? 150 : 80) * (hotActive(s, now) ? 2 : 1);
        s.player.pearls += pearls - (kind === "dive" ? action.stake : 0);
        s.cooldowns[kind] = now + 5000;
        if (kind === "dive") {
          const info = diveInfo(s, action.stake, now);
          const allowed = catalog.filter((i) =>
            info.rarities.includes(i.rarity),
          );
          const weights = {
            Common: 6000,
            Uncommon: 2900,
            Rare: 850,
            Epic: 200,
            Legendary: 50,
          };
          const total = info.rarities.reduce(
            (n, r) => n + weights[r as keyof typeof weights],
            0,
          );
          let roll = this.random() * total;
          let rarity = info.rarities[0];
          for (const r of info.rarities) {
            roll -= weights[r as keyof typeof weights];
            if (roll < 0) {
              rarity = r;
              break;
            }
          }
          const pool = allowed.filter((i) => i.rarity === rarity);
          const items: InventoryItem[] =
            this.random() * 100 < info.chance
              ? [
                  {
                    ...structuredClone(
                      pool[Math.floor(this.random() * pool.length)],
                    ),
                    instanceId: `loot-${++this.serial}`,
                  },
                ]
              : [];
          s.inventory.push(...items);
          s.result = {
            kind: "dive",
            pearls,
            items,
            message: items.length
              ? "Ein Schatz aus der Tiefe!"
              : "Nur Perlen gefunden – der Ozean birgt weitere Geheimnisse.",
          };
        } else {
          s.result = {
            kind: "fishing",
            pearls,
            fish: { name: "Kristallkarpfen", rarity: "Rare", icon: "🐟" },
            message: "Ein schimmernder Fang!",
          };
        }
        break;
      }
    }
    return this.getSnapshot();
  }
}
