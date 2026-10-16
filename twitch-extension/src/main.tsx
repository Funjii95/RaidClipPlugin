import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { labels, sets, statsText } from "./data";
import {
  MockRpgDataSource,
  characterStats,
  diveInfo,
  hotActive,
  perkCost,
  remaining,
  setCount,
} from "./mock";
import { slots } from "./types";
import type { Action, InventoryItem, RpgDataSource } from "./types";
import "./style.css";
const source: RpgDataSource = new MockRpgDataSource();
const tabs = [
  "Charakter",
  "Abenteuer",
  "Inventar",
  "Ausrüstung",
  "Itemsets",
  "Perks",
  "Boss",
  "Hot Time",
];
const symbols = ["🦦", "🧭", "🎒", "⚔", "🪸", "✧", "🐙", "🔥"];
const time = (n: number) =>
  `${Math.floor(n / 60)}:${String(n % 60).padStart(2, "0")}`;
function App() {
  const [state, setState] = useState(source.getSnapshot()),
    [tab, setTab] = useState("Charakter"),
    [now, setNow] = useState(Date.now()),
    [motion, setMotion] = useState(true),
    [stake, setStake] = useState(250),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState(""),
    [revealing, setRevealing] = useState(false),
    [helper, setHelper] = useState("Lokale Vorschau");
  const config = location.pathname.endsWith("config.html");
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(timer);
  }, []);
  useEffect(() => {
    const w = window as typeof window & {
      Twitch?: { ext?: { onAuthorized: (fn: () => void) => void } };
    };
    w.Twitch?.ext?.onAuthorized(() => setHelper("Twitch Helper bereit · Demo"));
  }, []);
  const run = async (action: Action) => {
    if (busy) return;
    setBusy(true);
    setMessage("");
    try {
      const next = await source.dispatch(action);
      setState(next);
      if (action.type === "dive" || action.type === "fishing") {
        setRevealing(true);
        setTimeout(() => setRevealing(false), motion ? 850 : 0);
      } else
        setMessage(
          action.type === "reset"
            ? "Demo zurückgesetzt."
            : "Demo aktualisiert.",
        );
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Aktion fehlgeschlagen.");
    } finally {
      setBusy(false);
    }
  };
  const hot = hotActive(state, now),
    seconds = remaining(state.hotTime.endsAt, now),
    stats = characterStats(state),
    info = diveInfo(state, stake, now);
  const equipped = (i: InventoryItem) =>
    Object.values(state.equipment).includes(i.instanceId);
  function itemCard(i: InventoryItem) {
    return (
      <article
        data-instance-id={i.instanceId}
        className={`item ${i.rarity}`}
        key={i.instanceId}
      >
        <div className="item-top">
          <span className="item-icon" aria-hidden="true">
            {i.icon}
          </span>
          <small>
            {i.rarity} · {i.slot ?? "Fundstück"}
          </small>
        </div>
        <h3>{i.name}</h3>
        <p>{i.description}</p>
        <ul className="attributes">
          {statsText(i.attributes).map((t) => (
            <li key={t}>{t}</li>
          ))}
        </ul>
        <p>
          {i.effects.length
            ? i.effects.map((e) => e.description).join(" ")
            : "Kein zusätzlicher Spezialeffekt."}
        </p>
        {i.setId && (
          <small>Demo-Set: {sets.find((s) => s.id === i.setId)?.name}</small>
        )}
        <div className="actions">
          {i.slot &&
            (equipped(i) ? (
              <button
                onClick={() => {
                  const slot = slots.find(
                    (slot) => state.equipment[slot] === i.instanceId,
                  );
                  if (slot) void run({ type: "unequip", slot });
                }}
              >
                Ablegen
              </button>
            ) : (
              <>
                <button
                  onClick={() =>
                    void run({ type: "equip", id: i.instanceId, slot: i.slot! })
                  }
                >
                  Ausrüsten{i.slot.startsWith("Ring") ? " · Ring1" : ""}
                </button>
                {i.slot.startsWith("Ring") && (
                  <button
                    onClick={() =>
                      void run({
                        type: "equip",
                        id: i.instanceId,
                        slot: "Ring2",
                      })
                    }
                  >
                    Ring2
                  </button>
                )}
              </>
            ))}
          <button
            aria-pressed={i.favorite}
            onClick={() => void run({ type: "favorite", id: i.instanceId })}
          >
            {i.favorite ? "★ Favorisiert" : "☆ Favorisieren"}
          </button>
          <button
            disabled={i.favorite || equipped(i)}
            onClick={() => void run({ type: "dismantle", id: i.instanceId })}
          >
            Zerlegen · +25 ◉
          </button>
        </div>
      </article>
    );
  }
  const hotControls = (
    <div className="actions">
      <button onClick={() => void run({ type: "hot", duration: 300000 })}>
        5 Minuten starten
      </button>
      <button onClick={() => void run({ type: "hot", duration: 30000 })}>
        Letzte 30 Sekunden
      </button>
      <button onClick={() => void run({ type: "hot", duration: 0 })}>
        Beenden
      </button>
    </div>
  );
  return (
    <div className={motion ? "app" : "app no-motion"}>
      <header>
        <img
          src="./otter.svg"
          alt="Demo-Avatar anstelle des späteren Twitch-Avatars"
        />
        <div>
          <small>FUNJII’S UNTERWASSERWELT</small>
          <h1>
            Otter RPG <span>DEMO</span>
          </h1>
          <p>Eine kleine Pfote. Ein großes Abenteuer.</p>
        </div>
      </header>
      <div className="statusbar">
        <span>◉ {state.player.pearls.toLocaleString("de-DE")} Demo-Perlen</span>
        <small>{helper}</small>
      </div>
      <div
        className={`hot-strip ${hot ? "burning" : ""} ${hot && seconds <= 30 ? "urgent" : ""}`}
      >
        🔥{" "}
        {hot
          ? `Hot Time · ${time(seconds)} · +50 % Itemchance`
          : "Die Tiefsee ruht · Hot Time inaktiv"}
      </div>
      {!config && (
        <nav aria-label="Hauptnavigation">
          {tabs.map((name, n) => (
            <button
              key={name}
              aria-current={tab === name ? "page" : undefined}
              onClick={() => {
                setTab(name);
                setMessage("");
              }}
            >
              <span aria-hidden="true">{symbols[n]}</span>
              {name}
            </button>
          ))}
        </nav>
      )}
      <main>
        <div className="section-title">
          <div>
            <small>
              {config ? "DEIN LOKALES TESTLABOR" : "ENTDECKE DIE TIEFE"}
            </small>
            <h2>{config ? "Demo-Konfiguration" : tab}</h2>
          </div>
          <span aria-hidden="true">✦</span>
        </div>
        {config ? (
          <>
            <article>
              <h3>Alles bleibt in dieser Vorschau</h3>
              <p>
                Keine Anbindung an das RaidClipPlugin. Einstellungen gelten nur
                für diese Seite bis zum Neuladen.
              </p>
              <p>
                Panel und Mobil verwenden denselben responsiven Aufbau. Keine
                Zugangsdaten erforderlich.
              </p>
            </article>
            {hotControls}
            <button onClick={() => void run({ type: "reset" })}>
              Demo zurücksetzen
            </button>
          </>
        ) : (
          <>
            {tab === "Charakter" && (
              <>
                <article className="hero">
                  <img
                    src={state.player.avatarUrl}
                    alt="Otter als Demo-Twitch-Avatar"
                  />
                  <div>
                    <small>HÜTER DER PERLE</small>
                    <h3>{state.player.username}</h3>
                    <p>Dein nächster Schatz wartet schon.</p>
                  </div>
                </article>
                <div className="stat-grid">
                  {Object.entries(stats).map(([key, value]) => (
                    <div className="stat" key={key}>
                      <strong>
                        {value}
                        {key === "damage" ? "" : " %"}
                      </strong>
                      <span>{labels[key as keyof typeof labels]}</span>
                    </div>
                  ))}
                </div>
                <article>
                  <h3>🍣 Aktive Sushi-Buffs</h3>
                  {state.player.buffs
                    .filter((b) => b.remainingUses > 0)
                    .map((b) => (
                      <p key={b.name}>
                        {b.name} · {b.remainingUses} Bossangriffe ·{" "}
                        {statsText(b.attributes).join(", ")}
                      </p>
                    ))}
                  {!state.player.buffs.some((b) => b.remainingUses > 0) && (
                    <p>Keine aktiven Buffs.</p>
                  )}
                  <h3>🪸 Aktive Setboni</h3>
                  {sets.map((set) => (
                    <p key={set.id}>
                      {set.name}:{" "}
                      {set.bonuses
                        .filter((b) => setCount(state, set.id) >= b.pieces)
                        .map((b) => b.description)
                        .join(", ") || "Noch kein Bonus"}
                    </p>
                  ))}
                  <small>
                    Buffs gelten nur für ihre Aktivität; Werte oben ohne
                    temporäre Buffs. Sets: Demokonzept.
                  </small>
                </article>
              </>
            )}
            {tab === "Abenteuer" && (
              <>
                <article className="adventure">
                  <div className="sea-art" aria-hidden="true">
                    🦦<span>🐚</span>🫧
                  </div>
                  <h3>Was verbirgt sich unter den Wellen?</h3>
                  <label>
                    Einsatz in Demo-Perlen
                    <select
                      value={stake}
                      onChange={(e) => setStake(Number(e.target.value))}
                    >
                      {[0, 250, 1000, 5000, 10000].map((n) => (
                        <option key={n} value={n}>
                          {n.toLocaleString("de-DE")}
                        </option>
                      ))}
                    </select>
                  </label>
                  <p>
                    Itemchance: {info.chance.toFixed(1)} %
                    {hot ? " · Hot Time eingerechnet" : ""}
                  </p>
                  <p className="rarities">
                    {info.rarities.map((r) => (
                      <span className={r} key={r}>
                        {r}{" "}
                      </span>
                    ))}
                  </p>
                  <button
                    className="primary"
                    disabled={
                      busy ||
                      revealing ||
                      remaining(state.cooldowns.dive, now) > 0 ||
                      state.player.pearls < stake
                    }
                    onClick={() => void run({ type: "dive", stake })}
                  >
                    🤿{" "}
                    {remaining(state.cooldowns.dive, now) > 0
                      ? `Tauchen · ${time(remaining(state.cooldowns.dive, now))}`
                      : "Tauchgang starten"}
                  </button>
                  <button
                    className="primary secondary"
                    disabled={
                      busy ||
                      revealing ||
                      remaining(state.cooldowns.fishing, now) > 0
                    }
                    onClick={() => void run({ type: "fishing" })}
                  >
                    🎣{" "}
                    {remaining(state.cooldowns.fishing, now) > 0
                      ? `Angeln · ${time(remaining(state.cooldowns.fishing, now))}`
                      : "Angel auswerfen"}
                  </button>
                  <small>Simulierte Belohnungen · je 5 Sekunden Cooldown</small>
                </article>
                {state.result && (
                  <article
                    className={revealing ? "reveal animating" : "reveal"}
                    aria-live="polite"
                  >
                    {revealing ? (
                      <p>🫧 Die Tiefe gibt ihren Schatz frei …</p>
                    ) : (
                      <>
                        <h3>{state.result.message}</h3>
                        <p>
                          +{state.result.pearls} Demo-Perlen{" "}
                          {state.result.kind === "dive"
                            ? `(Einsatz separat abgezogen)`
                            : ""}
                        </p>
                        {state.result.kind === "dive" ? (
                          state.result.items.map(itemCard)
                        ) : (
                          <p className={state.result.fish.rarity}>
                            <span className="item-icon">
                              {state.result.fish.icon}
                            </span>{" "}
                            {state.result.fish.name} ·{" "}
                            {state.result.fish.rarity}
                          </p>
                        )}
                      </>
                    )}
                  </article>
                )}
              </>
            )}
            {tab === "Inventar" && (
              <>
                <p className="muted">
                  {state.inventory.length} Schätze · Favoriten und ausgerüstete
                  Items sind vor Zerlegen geschützt.
                </p>
                <div className="cards">{state.inventory.map(itemCard)}</div>
              </>
            )}
            {tab === "Ausrüstung" &&
              slots.map((slot) => {
                const i = state.inventory.find(
                  (i) => i.instanceId === state.equipment[slot],
                );
                return (
                  <section key={slot} className="equipment">
                    <h3>{slot}</h3>
                    {i ? (
                      itemCard(i)
                    ) : (
                      <article className="empty">✧ Freier Platz</article>
                    )}
                  </section>
                );
              })}
            {tab === "Itemsets" && (
              <>
                <p className="muted">
                  Demokonzept: Setzuordnungen und Boni sind noch keine
                  Definitionen des C#-Projekts.
                </p>
                {sets.map((set) => (
                  <article key={set.id}>
                    <h3>🪸 {set.name}</h3>
                    <p>{setCount(state, set.id)} / 4 Teile ausgerüstet</p>
                    <progress max={4} value={setCount(state, set.id)} />
                    {set.bonuses.map((b) => (
                      <p
                        className={
                          setCount(state, set.id) >= b.pieces
                            ? "active-bonus"
                            : "muted"
                        }
                        key={b.pieces}
                      >
                        {setCount(state, set.id) >= b.pieces
                          ? "✓ Aktiv"
                          : "○ Inaktiv"}{" "}
                        · {b.pieces} Teile · {b.description}
                      </p>
                    ))}
                  </article>
                ))}
              </>
            )}
            {tab === "Perks" &&
              state.perks.map((p) => (
                <article key={p.id}>
                  <div className="item-top">
                    <h3>✧ {p.name}</h3>
                    <span>
                      {p.level}/{p.maxLevel}
                    </span>
                  </div>
                  <p>Pro Level: {statsText(p.bonus).join(", ")}</p>
                  <progress value={p.level} max={p.maxLevel} />
                  <button
                    disabled={
                      busy ||
                      p.level >= p.maxLevel ||
                      state.player.pearls < perkCost(p.level)
                    }
                    onClick={() => void run({ type: "perk", id: p.id })}
                  >
                    {p.level >= p.maxLevel
                      ? "Maximallevel"
                      : `Kaufen · ${perkCost(p.level).toLocaleString("de-DE")} Demo-Perlen`}
                  </button>
                </article>
              ))}
            {tab === "Boss" && (
              <article className="boss">
                <div className="boss-art" aria-hidden="true">
                  🐙
                </div>
                <small>BOSS DER TIEFSEE</small>
                <h3>{state.boss.name}</h3>
                <p>
                  {state.boss.status === "active"
                    ? "Der Kraken wartet auf dich!"
                    : "Besiegt · Nächster Boss in " +
                      time(remaining(state.boss.nextAt, now))}
                </p>
                <progress
                  aria-label="Boss HP"
                  value={state.boss.hp}
                  max={state.boss.maxHp}
                />
                <p>
                  {state.boss.hp} / {state.boss.maxHp} HP
                </p>
                <button
                  className="primary"
                  disabled={
                    busy ||
                    (state.boss.status === "waiting" && now < state.boss.nextAt)
                  }
                  onClick={() => void run({ type: "boss" })}
                >
                  ⚔ Teilnehmen · Demo-Angriff
                </button>
                <p>Dein Schaden: {state.boss.damage}</p>
              </article>
            )}
            {tab === "Hot Time" && (
              <article className={hot ? "event burning" : "event"}>
                <div className="boss-art" aria-hidden="true">
                  🔥
                </div>
                <h3>{hot ? "Die Tiefe glüht!" : "Die Glut ist erloschen"}</h3>
                <strong
                  className={seconds <= 30 ? "urgent countdown" : "countdown"}
                >
                  {hot ? time(seconds) : "0:00"}
                </strong>
                <p>+50 % relative Item-Dropchance</p>
                <p>+100 % zulässige Perlenbelohnungen</p>
                <p className="muted">
                  Demo: 30 % werden zu 45 %. Keine Verdopplung von Einsätzen
                  oder Zerlegewerten.
                </p>
                {hotControls}
              </article>
            )}
          </>
        )}
        <p role="status" className="feedback">
          {message}
        </p>
      </main>
      <footer>
        <label>
          <input
            type="checkbox"
            checked={!motion}
            onChange={(e) => setMotion(!e.target.checked)}
          />{" "}
          Animationen aus
        </label>
        {!config && (
          <button onClick={() => void run({ type: "reset" })}>
            Demo zurücksetzen
          </button>
        )}
        <p>Nur Mockdaten · keine echten Spielstände · v0.0.1</p>
      </footer>
    </div>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
