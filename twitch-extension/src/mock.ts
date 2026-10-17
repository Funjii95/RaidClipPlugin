import { catalog, initialState, sets, recipes } from "./data";
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
export function activityStats(
  s: DemoState,
  activity: "Boss" | "Dive" | "Fishing",
) {
  const stats = characterStats(s);
  for (const buff of s.player.buffs.filter(
    (b) => b.activity === activity && b.remainingUses > 0,
  )) {
    for (const key of Object.keys(buff.attributes) as (keyof CharacterStats)[])
      stats[key] += buff.attributes[key] ?? 0;
  }
  return stats;
}
function consumeBuffs(s: DemoState, activity: "Boss" | "Dive" | "Fishing") {
  for (const buff of s.player.buffs)
    if (buff.activity === activity && buff.remainingUses > 0)
      buff.remainingUses--;
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
    25 +
      [0, 4, 8, 12, 15][tier] +
      Math.min(
        10,
        (activityStats(s, "Dive").luck + activityStats(s, "Dive").diveLuck) *
          0.1,
      ),
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
          const wasLoot = s.result.items.some(
            (item) => item.instanceId === i.instanceId,
          );
          if (wasLoot) {
            s.result.items = s.result.items.filter(
              (item) => item.instanceId !== i.instanceId,
            );
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
      case "cook": {
        const recipe = recipes.find((r) => r.id === action.recipeId);
        if (!recipe) throw Error("Unbekanntes Sushi-Rezept.");
        if (
          (s.fish[recipe.fishId] ?? 0) < recipe.fishQuantity ||
          s.player.pearls < recipe.pearlCost
        )
          throw Error("Nicht genügend Fische oder Demo-Perlen.");
        s.fish[recipe.fishId] -= recipe.fishQuantity;
        s.player.pearls -= recipe.pearlCost;
        s.sushi[recipe.id] = (s.sushi[recipe.id] ?? 0) + 1;
        break;
      }
      case "activateSushi": {
        const recipe = recipes.find((r) => r.id === action.recipeId);
        if (!recipe || (s.sushi[recipe.id] ?? 0) < 1)
          throw Error("Kein passendes Sushi vorhanden.");
        const active = s.player.buffs.filter((b) => b.remainingUses > 0);
        if (active.some((b) => b.recipeId === recipe.id))
          throw Error("Dieser Sushi-Buff ist bereits aktiv.");
        if (active.length >= 3)
          throw Error("Maximal drei verschiedene Sushi-Buffs sind erlaubt.");
        s.sushi[recipe.id]--;
        s.player.buffs = [
          ...active,
          {
            recipeId: recipe.id,
            name: recipe.name,
            activity: recipe.activity,
            remainingUses: 3,
            attributes: structuredClone(recipe.attributes),
          },
        ];
        break;
      }
      case "restartFight": {
        if (s.player.hp > 0)
          throw Error("Neustart ist erst nach einer Niederlage möglich.");
        // Restart only this demo encounter; inventory, perks and pearls are retained.
        s.player.hp = s.player.maxHp;
        s.boss = { ...initialState(now).boss };
        break;
      }
      case "boss": {
        if (s.player.hp <= 0)
          throw Error("Dein Otter ist besiegt. Starte den Demokampf neu.");
        if (s.boss.status === "waiting") {
          if (now < s.boss.nextAt) throw Error("Der nächste Boss kommt bald.");
          s.boss = { ...initialState(now).boss };
        }
        const stats = activityStats(s, "Boss");
        const critical = this.random() * 100 < Math.min(50, stats.critChance);
        const damage = Math.round(
          stats.damage *
            (1 + stats.bossDamage / 100) *
            (critical ? 1.5 + Math.min(300, stats.critDamage) / 100 : 1),
        );
        const damageDealt = Math.min(s.boss.hp, damage);
        s.boss.hp -= damageDealt;
        s.boss.damage += damageDealt;
        // Explicit demo turn rules: surviving bosses counter once for 35 HP.
        // A killing blow wins immediately, so a defeated boss never retaliates.
        const damageTaken = s.boss.hp > 0 ? Math.min(35, s.player.hp) : 0;
        s.player.hp -= damageTaken;
        s.boss.lastExchange = {
          turn: (s.boss.lastExchange?.turn ?? 0) + 1,
          damageDealt,
          damageTaken,
          outcome:
            s.boss.hp === 0 ? "victory" : s.player.hp === 0 ? "defeat" : "hit",
        };
        consumeBuffs(s, "Boss");
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
          (kind === "dive"
            ? 150
            : Math.round(
                80 * (1 + activityStats(s, "Fishing").fishingLuck / 100),
              )) * (hotActive(s, now) ? 2 : 1);
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
          s.fish["crystal-carp"] = (s.fish["crystal-carp"] ?? 0) + 1;
          s.result = {
            kind: "fishing",
            pearls,
            fish: {
              id: "crystal-carp",
              name: "Kristallkarpfen",
              rarity: "Rare",
              icon: "🐟",
            },
            message: "Ein schimmernder Fang!",
          };
        }
        consumeBuffs(s, kind === "dive" ? "Dive" : "Fishing");
        break;
      }
    }
    return this.getSnapshot();
  }
}
