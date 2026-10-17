import { describe, it, expect } from "vitest";
import {
  MockRpgDataSource,
  remaining,
  hotActive,
  diveInfo,
  characterStats,
} from "../src/mock";
import { catalog } from "../src/data";
describe("isolierte Mockaktionen", () => {
  it("snapshots cannot mutate state; no persistence between instances", async () => {
    const a = new MockRpgDataSource();
    const s = a.getSnapshot();
    s.player.pearls = 0;
    expect(a.getSnapshot().player.pearls).toBe(25000);
    await a.dispatch({ type: "perk", id: "damage" });
    expect(new MockRpgDataSource().getSnapshot().perks[0].level).toBe(0);
  });
  it("uses 13 source items, seven perks and no invented special effects", () => {
    expect(catalog).toHaveLength(13);
    expect(new MockRpgDataSource().getSnapshot().perks).toHaveLength(7);
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
    const a = new MockRpgDataSource(() => now);
    expect((await a.dispatch({ type: "fishing" })).result?.pearls).toBe(160);
    now = 300001;
    expect(hotActive(a.getSnapshot(), now)).toBe(false);
    expect(remaining(300000, now)).toBe(0);
    expect((await a.dispatch({ type: "fishing" })).result?.pearls).toBe(80);
  });
  it("set bonuses follow equipped items and both rings support either ring slot", async () => {
    const a = new MockRpgDataSource();
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
    const a = new MockRpgDataSource();
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
    const a = new MockRpgDataSource();
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
    const a = new MockRpgDataSource(() => now);
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
    const a = new MockRpgDataSource();
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
    const a = new MockRpgDataSource();
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
    const a = new MockRpgDataSource();
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
