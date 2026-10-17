import { describe, it, expect } from "vitest";
import {
  MockRpgDataSource,
  remaining,
  hotActive,
  diveInfo,
  characterStats,
  activityStats,
} from "../src/mock";
import { catalog, recipes } from "../src/data";
describe("isolierte Mockaktionen", () => {
  it("snapshots cannot mutate state; no persistence between instances", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    const s = a.getSnapshot();
    s.player.pearls = 0;
    expect(a.getSnapshot().player.pearls).toBe(25000);
    await a.dispatch({ type: "perk", id: "damage" });
    expect(
      new MockRpgDataSource(
        () => 1000,
        () => 0.99,
      ).getSnapshot().perks[0].level,
    ).toBe(0);
  });
  it("uses 13 source items, seven perks and no invented special effects", () => {
    expect(catalog).toHaveLength(13);
    expect(
      new MockRpgDataSource(
        () => 1000,
        () => 0.99,
      ).getSnapshot().perks,
    ).toHaveLength(7);
    expect(catalog.every((i) => !i.effects.length)).toBe(true);
  });
  it("dive deducts stake, grants loot, enforces cooldown and available rarities", async () => {
    let now = 1000;
    const a = new MockRpgDataSource(
      () => now,
      () => 0,
    );
    const s = await a.dispatch({ type: "dive", stake: 250 });
    expect(s.player.pearls).toBe(25050);
    expect(s.inventory).toHaveLength(14);
    await expect(a.dispatch({ type: "dive", stake: 250 })).rejects.toThrow(
      "Cooldown",
    );
    now += 5000;
    await expect(a.dispatch({ type: "dive", stake: -1 })).rejects.toThrow(
      "Ungültiger",
    );
    expect(diveInfo(s, 0, now).rarities).not.toContain("Legendary");
    expect(diveInfo(s, 10000, now).rarities).toContain("Legendary");
  });
  it("fishing reward doubles only while hot; expired countdown clamps to zero", async () => {
    let now = 0;
    const a = new MockRpgDataSource(
      () => now,
      () => 0.99,
    );
    expect((await a.dispatch({ type: "fishing" })).result?.pearls).toBe(164);
    now = 300001;
    expect(hotActive(a.getSnapshot(), now)).toBe(false);
    expect(remaining(300000, now)).toBe(0);
    expect((await a.dispatch({ type: "fishing" })).result?.pearls).toBe(82);
  });
  it("set bonuses follow equipped items and both rings support either ring slot", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    const original = characterStats(a.getSnapshot());
    let s = await a.dispatch({ type: "unequip", slot: "Helmet" });
    expect(characterStats(s).damage).toBe(original.damage - 6);
    s = await a.dispatch({
      type: "equip",
      id: "demo-deep-ring",
      slot: "Ring2",
    });
    expect(s.equipment.Ring2).toBe("demo-deep-ring");
    await expect(
      a.dispatch({ type: "equip", id: "demo-deep-ring", slot: "Boots" }),
    ).rejects.toThrow();
  });
  it("protects favorites and equipment from dismantling", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    await expect(
      a.dispatch({ type: "dismantle", id: "demo-trident" }),
    ).rejects.toThrow();
    await a.dispatch({ type: "favorite", id: "demo-shell" });
    await expect(
      a.dispatch({ type: "dismantle", id: "demo-shell" }),
    ).rejects.toThrow();
    await a.dispatch({ type: "favorite", id: "demo-shell" });
    const s = await a.dispatch({ type: "dismantle", id: "demo-shell" });
    expect(s.inventory).toHaveLength(12);
    expect(s.player.pearls).toBe(25025);
  });
  it("perk cost grows and max level is enforced", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    let s = a.getSnapshot();
    for (let i = 0; i < 5; i++)
      s = await a.dispatch({ type: "perk", id: "damage" });
    expect(s.perks[0].level).toBe(5);
    expect(s.player.pearls).toBe(9500);
    await expect(a.dispatch({ type: "perk", id: "damage" })).rejects.toThrow(
      "Maximallevel",
    );
  });
  it("boss consumes buffs, reaches waiting state and restarts after countdown", async () => {
    let now = 0;
    const a = new MockRpgDataSource(
      () => now,
      () => 0.99,
    );
    for (let i = 0; i < 5; i++)
      await a.dispatch({ type: "perk", id: "damage" });
    for (let i = 0; i < 4; i++) await a.dispatch({ type: "perk", id: "boss" });
    let s = a.getSnapshot();
    for (let i = 0; i < 50 && s.boss.status === "active"; i++)
      s = await a.dispatch({ type: "boss" });
    expect(s.boss.hp).toBe(0);
    expect(s.player.buffs[0].remainingUses).toBe(0);
    await expect(a.dispatch({ type: "boss" })).rejects.toThrow();
    now = 60001;
    s = await a.dispatch({ type: "boss" });
    expect(s.boss.status).toBe("active");
    expect(s.boss.hp).toBeGreaterThan(0);
  });
});

it("dismantling dive loot dismisses its reveal and cannot reward twice", async () => {
  const source = new MockRpgDataSource(
    () => 1000,
    () => 0,
  );
  const dive = await source.dispatch({ type: "dive", stake: 250 });
  if (dive.result?.kind !== "dive") throw Error("Expected dive");
  const id = dive.result.items[0].instanceId;
  // Dismantling another item must not dismiss the current loot.
  const unrelated = await source.dispatch({
    type: "dismantle",
    id: "demo-shell",
  });
  expect(unrelated.result).toBeDefined();
  const after = await source.dispatch({ type: "dismantle", id });
  expect(after.result).toBeUndefined();
  expect(after.inventory.some((item) => item.instanceId === id)).toBe(false);
  expect(after.player.pearls).toBe(unrelated.player.pearls + 25);
  await expect(source.dispatch({ type: "dismantle", id })).rejects.toThrow();
  expect(source.getSnapshot().player.pearls).toBe(after.player.pearls);
});

describe("Otter HP and enemy counterattacks", () => {
  it("surviving bosses counter once per turn and unrelated actions cannot hurt or heal", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    const s = await a.dispatch({ type: "boss" });
    expect(s.player.hp).toBe(965);
    expect(s.boss.lastExchange?.damageTaken).toBe(35);
    expect(s.boss.lastExchange?.damageDealt).toBeGreaterThan(0);
    expect(s.boss.hp).toBe(s.boss.maxHp - s.boss.lastExchange!.damageDealt);
    await a.dispatch({ type: "fishing" });
    await a.dispatch({ type: "favorite", id: "demo-shell" });
    expect(a.getSnapshot().player.hp).toBe(965);
    await expect(a.dispatch({ type: "restartFight" })).rejects.toThrow();
  });
  it("defeat clamps HP to zero, blocks more attacks and restarts only the encounter", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    await a.dispatch({ type: "favorite", id: "demo-shell" });
    let s = a.getSnapshot();
    for (let i = 0; i < 29; i++) s = await a.dispatch({ type: "boss" });
    expect(s.player.hp).toBe(0);
    expect(s.boss.hp).toBeGreaterThan(0);
    expect(s.boss.lastExchange?.outcome).toBe("defeat");
    expect(s.boss.lastExchange?.damageTaken).toBe(20);
    const before = a.getSnapshot();
    await expect(a.dispatch({ type: "boss" })).rejects.toThrow("besiegt");
    expect(a.getSnapshot()).toEqual(before);
    const reset = await a.dispatch({ type: "restartFight" });
    expect(reset.player.hp).toBe(reset.player.maxHp);
    expect(reset.boss.hp).toBe(reset.boss.maxHp);
    expect(reset.boss.lastExchange).toBeUndefined();
    expect(reset.boss.damage).toBe(0);
    expect(reset.player.pearls).toBe(before.player.pearls);
    expect(reset.inventory).toEqual(before.inventory);
    expect(reset.perks).toEqual(before.perks);
    expect(reset.player.buffs).toEqual(before.player.buffs);
    expect(reset.equipment).toEqual(before.equipment);
    expect((await a.dispatch({ type: "boss" })).player.hp).toBe(965);
  });
  it("a finishing blow wins without a counterattack and full demo reset restores HP", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    for (let i = 0; i < 5; i++)
      await a.dispatch({ type: "perk", id: "damage" });
    for (let i = 0; i < 4; i++) await a.dispatch({ type: "perk", id: "boss" });
    let previous = a.getSnapshot();
    while (previous.boss.hp > 0) {
      const next = await a.dispatch({ type: "boss" });
      if (next.boss.hp === 0) {
        expect(next.player.hp).toBe(previous.player.hp);
        expect(next.boss.lastExchange?.damageTaken).toBe(0);
        expect(next.boss.lastExchange?.outcome).toBe("victory");
        expect(next.boss.damage).toBe(next.boss.maxHp);
        break;
      }
      previous = next;
    }
    const reset = await a.dispatch({ type: "reset" });
    expect(reset.player.hp).toBe(1000);
    expect(reset.boss.lastExchange).toBeUndefined();
  });
});

describe("Sushi crafting from existing C# recipe defaults", () => {
  it("all four recipes consume exactly their ingredient and pearl cost", async () => {
    for (const recipe of recipes) {
      const a = new MockRpgDataSource(
        () => 1000,
        () => 0.99,
      );
      const before = a.getSnapshot();
      const after = await a.dispatch({ type: "cook", recipeId: recipe.id });
      expect(after.fish[recipe.fishId]).toBe(before.fish[recipe.fishId] - 1);
      expect(after.player.pearls).toBe(before.player.pearls - recipe.pearlCost);
      expect(after.sushi[recipe.id]).toBe(1);
      expect(after.player.buffs).toEqual(before.player.buffs);
    }
  });
  it("rejects missing fish, unknown recipes and insufficient pearls atomically", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    for (let i = 0; i < 3; i++)
      await a.dispatch({ type: "cook", recipeId: "fishing" });
    const empty = a.getSnapshot();
    await expect(
      a.dispatch({ type: "cook", recipeId: "fishing" }),
    ).rejects.toThrow();
    await expect(
      a.dispatch({ type: "cook", recipeId: "bad" }),
    ).rejects.toThrow();
    await expect(
      a.dispatch({ type: "activateSushi", recipeId: "bad" }),
    ).rejects.toThrow();
    expect(a.getSnapshot()).toEqual(empty);
    const poor = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    for (let i = 0; i < 5; i++)
      await poor.dispatch({ type: "perk", id: "damage" });
    for (let i = 0; i < 4; i++)
      await poor.dispatch({ type: "perk", id: "crit" });
    await poor.dispatch({ type: "cook", recipeId: "critical" });
    const before = poor.getSnapshot();
    expect(before.player.pearls).toBeLessThan(1500);
    await expect(
      poor.dispatch({ type: "cook", recipeId: "dive" }),
    ).rejects.toThrow();
    expect(poor.getSnapshot()).toEqual(before);
  });
  it("activation uses one dish, prevents duplicates and caps active buffs at three", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    await expect(
      a.dispatch({ type: "activateSushi", recipeId: "fishing" }),
    ).rejects.toThrow();
    for (const id of ["fishing", "critical", "dive", "fighter"])
      await a.dispatch({ type: "cook", recipeId: id });
    await a.dispatch({ type: "activateSushi", recipeId: "fishing" });
    await a.dispatch({ type: "activateSushi", recipeId: "critical" });
    const full = a.getSnapshot();
    await expect(
      a.dispatch({ type: "activateSushi", recipeId: "fighter" }),
    ).rejects.toThrow("bereits aktiv");
    await expect(
      a.dispatch({ type: "activateSushi", recipeId: "dive" }),
    ).rejects.toThrow("Maximal drei");
    expect(a.getSnapshot()).toEqual(full);
    expect(full.sushi.fishing).toBe(0);
    expect(full.player.buffs.filter((b) => b.remainingUses > 0)).toHaveLength(
      3,
    );
  });
  it("fishing collects recipe fish and consumes only matching buffs", async () => {
    const a = new MockRpgDataSource(
      () => 1000,
      () => 0.99,
    );
    await a.dispatch({ type: "cook", recipeId: "fishing" });
    await a.dispatch({ type: "activateSushi", recipeId: "fishing" });
    const before = a.getSnapshot();
    const after = await a.dispatch({ type: "fishing" });
    expect(after.fish["crystal-carp"]).toBe(before.fish["crystal-carp"] + 1);
    expect(after.result?.pearls).toBe(172);
    expect(
      after.player.buffs.find((b) => b.recipeId === "fishing")?.remainingUses,
    ).toBe(2);
    expect(
      after.player.buffs.find((b) => b.recipeId === "fighter")?.remainingUses,
    ).toBe(3);
    await expect(a.dispatch({ type: "fishing" })).rejects.toThrow("Cooldown");
    expect(a.getSnapshot()).toEqual(after);
  });
  it("dive buff raises chance and expires after exactly three successful dives", async () => {
    let now = 1000;
    const a = new MockRpgDataSource(
      () => now,
      () => 0.99,
    );
    const base = diveInfo(a.getSnapshot(), 250, now).chance;
    await a.dispatch({ type: "cook", recipeId: "dive" });
    await a.dispatch({ type: "activateSushi", recipeId: "dive" });
    expect(diveInfo(a.getSnapshot(), 250, now).chance).toBeGreaterThan(base);
    for (let i = 0; i < 3; i++) {
      await a.dispatch({ type: "dive", stake: 0 });
      now += 5000;
    }
    expect(
      a.getSnapshot().player.buffs.find((b) => b.recipeId === "dive")
        ?.remainingUses,
    ).toBe(0);
    expect(diveInfo(a.getSnapshot(), 250, now).chance).toBe(base);
  });
  it("critical sushi affects only boss stats and produces a critical hit in its extra chance range", async () => {
    const plain = new MockRpgDataSource(
      () => 1000,
      () => 0.075,
    );
    const buffed = new MockRpgDataSource(
      () => 1000,
      () => 0.075,
    );
    await buffed.dispatch({ type: "cook", recipeId: "critical" });
    await buffed.dispatch({ type: "activateSushi", recipeId: "critical" });
    const s = buffed.getSnapshot();
    expect(activityStats(s, "Boss").critChance).toBe(
      characterStats(s).critChance + 5,
    );
    expect(activityStats(s, "Fishing").critChance).toBe(
      characterStats(s).critChance,
    );
    const normal = await plain.dispatch({ type: "boss" });
    const critical = await buffed.dispatch({ type: "boss" });
    expect(critical.boss.lastExchange!.damageDealt).toBeGreaterThan(
      normal.boss.lastExchange!.damageDealt,
    );
  });
});
