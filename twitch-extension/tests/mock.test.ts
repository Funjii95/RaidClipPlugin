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
  const source = new MockRpgDataSource(() => 1000, () => 0);
  const dive = await source.dispatch({type: "dive", stake: 250});
  if (dive.result?.kind !== "dive") throw Error("Expected dive");
  const id = dive.result.items[0].instanceId;
  // Dismantling another item must not dismiss the current loot.
  const unrelated = await source.dispatch({type: "dismantle", id: "demo-shell"});
  expect(unrelated.result).toBeDefined();
  const after = await source.dispatch({type: "dismantle", id});
  expect(after.result).toBeUndefined();
  expect(after.inventory.some(item => item.instanceId === id)).toBe(false);
  expect(after.player.pearls).toBe(unrelated.player.pearls + 25);
  await expect(source.dispatch({type: "dismantle", id})).rejects.toThrow();
  expect(source.getSnapshot().player.pearls).toBe(after.player.pearls);
});
