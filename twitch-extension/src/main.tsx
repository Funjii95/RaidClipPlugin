import { useEffect, useState, useRef } from "react";
import { createRoot } from "react-dom/client";
import { labels, sets, statsText, recipes, numberStat, recipeEffects, collectorText } from "./data";
import { MockRpgDataSource, characterStats, diveInfo, hotActive, perkCost, remaining, setCount, buffActive, collectors, } from "./mock";
import { slots } from "./types";
import type { Action, InventoryItem, RpgDataSource } from "./types";
import "./style.css";
import "./live/live.css";
import {LivePanel} from "./live/LivePanel";
import { Icon } from "./icons";
import {ItemArtwork} from "./item-art";
import {PearlMinigames} from "./minigames/PerlMinigames";
import {MockMinigameClient} from "./minigames/mock-client";
const demoSource = new MockRpgDataSource();
const source:RpgDataSource=demoSource;
const minigameClient = new MockMinigameClient({balance:()=>source.getSnapshot().player.pearls,settle:(id,stake,payout)=>demoSource.settleMinigameFixture(id,stake,payout)});
const tabs = [
    "Charakter",
    "Abenteuer",
    "Inventar",
    "Ausrüstung",
    "Itemsets",
    "Perks",
    "Boss",
    "Kochen",
    "Perlen-Minispiele",
];
const symbols = [
    "otter",
    "compass",
    "bag",
    "swords",
    "sets",
    "perk",
    "boss",
    "cook",
    "pearl",
] as const;
const time = (n: number) => `${Math.floor(n / 60)}:${String(n % 60).padStart(2, "0")}`;
function App() {
    const [state, setState] = useState(source.getSnapshot()), [tab, setTab] = useState("Charakter"), [now, setNow] = useState(Date.now()), [motion, setMotion] = useState(true), [stake, setStake] = useState(250), [fishStake, setFishStake] = useState(0), [search, setSearch] = useState(""), [busy, setBusy] = useState(false), [message, setMessage] = useState(""), [revealing, setRevealing] = useState(false), [helper, setHelper] = useState("Lokale Vorschau");
    const dialog = useRef<HTMLDialogElement>(null);
    useEffect(() => { if (state.salvageQuote)
        dialog.current?.showModal();
    else
        dialog.current?.close(); }, [state.salvageQuote]);
    useEffect(() => { const t = setInterval(() => void source.dispatch({ type: "refresh" }).then(setState), 1000); return () => clearInterval(t); }, []);
    const config = location.pathname.endsWith("config.html");
    useEffect(() => {
        const timer = setInterval(() => setNow(Date.now()), 250);
        return () => clearInterval(timer);
    }, []);
    useEffect(() => {
        const w = window as typeof window & {
            Twitch?: {
                ext?: {
                    onAuthorized: (fn: () => void) => void;
                };
            };
        };
        w.Twitch?.ext?.onAuthorized(() => setHelper("Twitch Helper bereit · Demo"));
    }, []);
    const run = async (action: Action) => {
        if (busy)
            return;
        setBusy(true);
        setMessage("");
        try {
            const next = await source.dispatch(action);
            setState(next);
            if (action.type === "dive" || action.type === "fishing") {
                setRevealing(true);
                setTimeout(() => setRevealing(false), motion ? 850 : 0);
            }
            else
                setMessage(action.type === "reset"
                    ? "Demo zurückgesetzt."
                    : "Demo aktualisiert.");
        }
        catch (e) {
            setMessage(e instanceof Error ? e.message : "Aktion fehlgeschlagen.");
        }
        finally {
            setBusy(false);
        }
    };
    const hot = hotActive(state, now), seconds = remaining(state.hotTime.endsAt, now), stats = characterStats(state), info = diveInfo(state, stake, now);
    const equipped = (i: InventoryItem) => Object.values(state.equipment).includes(i.instanceId);
    function itemCard(i: InventoryItem) {
        return (<article data-instance-id={i.instanceId} className={`item ${i.rarity}`} key={i.instanceId}>
        <div className="item-top">
          <ItemArtwork id={i.itemId} name={i.name} fallback={i.icon}/>
          <small>
            {i.rarity} · {i.slot ?? "Fundstück"}
          </small>
        </div>
        <h3>{i.name}</h3>
        <p>{i.description}</p>
        <ul className="attributes">
          {statsText(i.attributes).map((t) => (<li key={t}>{t}</li>))}
        </ul>
        <p>
          {i.effects.length
                ? i.effects.map((e) => e.description).join(" ")
                : "Kein zusätzlicher Spezialeffekt."}
        </p>
        {i.setId && (<small>Set: {sets.find((s) => s.id === i.setId)?.name}</small>)}
        {i.collector && <p>{collectorText(i.collector).join(" · ")}</p>}
        <div className="actions">
          <button aria-pressed={i.locked} onClick={() => void run({ type: "lock", id: i.instanceId })}>{i.locked ? "Entsperren" : "Sperren"}</button>
          {i.slot &&
                (equipped(i) ? (<button onClick={() => {
                        const slot = slots.find((slot) => state.equipment[slot] === i.instanceId);
                        if (slot)
                            void run({ type: "unequip", slot });
                    }}>
                Ablegen
              </button>) : (<>
                <button onClick={() => void run({ type: "equip", id: i.instanceId, slot: i.slot! })}>
                  Ausrüsten{i.slot.startsWith("Ring") ? " · Ring1" : ""}
                </button>
                {i.slot.startsWith("Ring") && (<button onClick={() => void run({
                            type: "equip",
                            id: i.instanceId,
                            slot: "Ring2",
                        })}>
                    Ring2
                  </button>)}
              </>))}
          <button aria-pressed={i.favorite} onClick={() => void run({ type: "favorite", id: i.instanceId })}>
            {i.favorite ? "★ Favorisiert" : "☆ Favorisieren"}
          </button>
          <button disabled={i.favorite || i.locked || equipped(i)} onClick={() => void run({ type: "dismantle", id: i.instanceId })}>
            Zerlegen prüfen
          </button>
        </div>
      </article>);
    }
    const hotControls = (<div className="actions">
      <button onClick={() => void run({ type: "hot", duration: 300000 })}>
        5 Minuten starten
      </button>
      <button onClick={() => void run({ type: "hot", duration: 30000 })}>
        Letzte 30 Sekunden
      </button>
      <button onClick={() => void run({ type: "hot", duration: 0 })}>
        Beenden
      </button>
    </div>);
    return (<div className={motion ? "app" : "app no-motion"}>
      <div className={`hot-hud ${hot ? "burning" : ""} ${hot && seconds <= 30 ? "urgent" : ""}`} title="Hot Time: +50 % relative Itemchance, +100 % zulässige Perlenbelohnungen">
        <progress aria-label="Hot-Time-Restzeit" max={300} value={hot ? Math.min(300, seconds) : 0} aria-valuetext={hot ? `${time(seconds)} verbleibend` : "Hot Time inaktiv"}/>
        <div className="hot-hud-label">
          <span>🔥 {hot ? "HOT TIME" : "Hot Time inaktiv"}</span>
          <strong className="hot-timer">{hot ? time(seconds) : "0:00"}</strong>
          {hot && <small>+50 % Drops · +100 % Perlen</small>}
        </div>
      </div>
      <header>
        <img src="./otter-mage.png" alt="Demo-Avatar anstelle des späteren Twitch-Avatars"/>
        <div>
          <small>FUNJII · LEUCHTENDE TIEFE</small>
          <h1>
            Otter RPG <span>DEMO</span>
          </h1>
          <p>Dein Abenteuer unter den Wellen.</p>
        </div>
      </header>
      <div className="statusbar">
        <span>
          <Icon name="pearl"/> {state.player.pearls.toLocaleString("de-DE")}{" "}
          Demo-Perlen
        </span>
        <small>{helper}</small>
      </div>
      {!config && (<section className={`otter-health ${state.player.hp <= state.player.maxHp * 0.25 ? "health-low" : ""} ${state.boss.lastExchange?.damageTaken ? "health-hit" : ""}`} key={`health-${state.boss.lastExchange?.turn ?? 0}-${state.player.hp}`} aria-label="Lebenspunkte deines Otters">
          <div className="health-label">
            <span>♥ Otter-HP</span>
            <strong>
              {state.player.hp} / {state.player.maxHp} HP
            </strong>
          </div>
          <progress aria-label="Otter HP" value={state.player.hp} max={state.player.maxHp}/>
          {state.player.hp === 0 ? (<div className="defeat-notice">
              <p role="status">Dein Otter ist besiegt.</p>
              <button onClick={() => void run({ type: "healChat" })}>
                Chat-Heilung testen
              </button>
              <small>
                10 HP alle 30 Sekunden. Die Todessperre bleibt für diesen Boss bestehen.
              </small>
            </div>) : state.player.hp <= state.player.maxHp * 0.25 ? (<small>
              Wenig Lebenspunkte – der nächste Gegentreffer kann dich besiegen.
            </small>) : null}
        </section>)}
      {!config && (<nav aria-label="Hauptnavigation">
          {tabs.map((name, n) => (<button key={name} aria-current={tab === name ? "page" : undefined} onClick={() => {
                    setTab(name);
                    setMessage("");
                }}>
              <Icon name={symbols[n]}/>
              {name}
            </button>))}
        </nav>)}
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
        {config ? (<>
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
          </>) : (<>
            {tab === "Perlen-Minispiele" && <PearlMinigames client={minigameClient}/>}
            {tab === "Charakter" && (<>
                <article className="hero">
                  <img src={state.player.avatarUrl} alt="Otter als Demo-Twitch-Avatar"/>
                  <div>
                    <small>HÜTER DER PERLE</small>
                    <h3>{state.player.username}</h3>
                    <p>Dein nächster Schatz wartet schon.</p>
                  </div>
                </article>
                <article><h3>Level {state.player.level}</h3><p>{state.player.experience} EXP · nächstes Level: {1000 * state.player.level * (state.player.level + 1) / 2} EXP</p><button onClick={() => void run({ type: "healChat" })}>Chat-Heilung testen</button><button onClick={() => void run({ type: "demoWound" })}>Verletzung simulieren</button></article><div className="stat-grid">
                  {Object.entries(stats).map(([key, value]) => (<div className="stat" key={key}>
                      <strong>
                        {numberStat(key, value)}
                      </strong>
                      <span>{labels[key as keyof typeof labels]}</span>
                    </div>))}
                </div>
                <article>
                  <h3>🍣 Aktive Sushi-Buffs</h3>
                  {state.player.buffs
                    .filter((b) => buffActive(b, now))
                    .map((b) => (<p key={b.name}>
                        {b.name} · {b.remainingUses} neue Bosse ·{" "}
                        {statsText(b.attributes).join(", ")}
                      </p>))}
                  {!state.player.buffs.some((b) => buffActive(b, now)) && (<p>Keine aktiven Buffs.</p>)}
                  <h3>🪸 Aktive Setboni</h3>
                  {sets.map((set) => (<p key={set.id}>
                      {set.name}:{" "}
                      {set.bonuses
                        .filter((b) => setCount(state, set.id) >= b.pieces)
                        .map((b) => b.description)
                        .join(", ") || "Noch kein Bonus"}
                    </p>))}
                  <small>
                    Buffs gelten nur für ihre Aktivität; Werte oben ohne
                    temporäre Buffs. Sets: v2.1.21.
                  </small>
                </article>
              </>)}
            {tab === "Abenteuer" && (<>
                <article className="adventure">
                  <div className="sea-art">
                    <img src="./luminous-depths.png" alt="Otter-Magier zwischen leuchtenden Quallen und einem Perlenschatz in versunkenen Ruinen" width="1536" height="1024"/>
                    <div className="sea-caption">
                      <span>LEUCHTENDE TIEFE</span>
                      <h3>Ein Meer voller Geheimnisse.</h3>
                    </div>
                  </div>
                  <div className="adventure-controls">
                    <label>
                      Einsatz in Demo-Perlen
                      <select value={stake} onChange={(e) => setStake(Number(e.target.value))}>
                        {[0, 250, 1000, 5000, 10000].map((n) => (<option key={n} value={n}>
                            {n.toLocaleString("de-DE")}
                          </option>))}
                      </select>
                    </label>
                    <div className="loot-chance">
                      <span>Lootchance</span>
                      <strong>{info.chance.toFixed(1)} %</strong>
                      <small>
                        {hot ? "Hot Time eingerechnet" : "Aktuelle Itemchance"}
                      </small>
                    </div>
                    <label>Angel-Einsatz<select value={fishStake} onChange={e => setFishStake(Number(e.target.value))}>{[0, 10, 250, 1000].map(n => <option key={n} value={n}>{n} Perlen</option>)}</select></label><div className="adventure-buttons">
                      <button className="primary" disabled={busy || state.player.hp === 0 ||
                    revealing ||
                    remaining(state.cooldowns.dive, now) > 0 ||
                    state.player.pearls < stake} onClick={() => void run({ type: "dive", stake })}>
                        <Icon name="dive"/>
                        <span>
                          {remaining(state.cooldowns.dive, now) > 0
                    ? `Tauchen · ${time(remaining(state.cooldowns.dive, now))}`
                    : "Tauchgang starten"}
                        </span>
                      </button>
                      <button className="primary secondary" disabled={busy ||
                    revealing ||
                    remaining(state.cooldowns.fishing, now) > 0} onClick={() => void run({ type: "fishing", stake: fishStake })}>
                        <Icon name="fish"/>
                        <span>
                          {remaining(state.cooldowns.fishing, now) > 0
                    ? `Angeln · ${time(remaining(state.cooldowns.fishing, now))}`
                    : "Angel auswerfen"}
                        </span>
                      </button>
                    </div>
                    <p className="loot-label">Mögliche Seltenheiten</p>
                    <p className="rarities">
                      {info.rarities.map((r) => (<span className={r} key={r}>
                          {r}{" "}
                        </span>))}
                    </p>
                    <small>
                      Simulierte Belohnungen · je 30 Sekunden Cooldown
                    </small>
                  </div>
                </article>
                {state.result && (<article className={revealing ? "reveal animating" : "reveal"} aria-live="polite">
                    {revealing ? (<p>🫧 Die Tiefe gibt ihren Schatz frei …</p>) : (<>
                        <h3>{state.result.message}</h3>
                        <p>
                          +{state.result.pearls} Demo-Perlen{" "}
                          {state.result.kind === "dive"
                            ? `(Einsatz separat abgezogen)`
                            : ""}
                        </p>
                        {state.result.kind === "dive" ? (state.result.items.map(itemCard)) : state.result.fish ? (<p className={state.result.fish.rarity}>
                            <span className="item-icon">
                              <ItemArtwork id={`fish-${state.result.fish.id}`} name={state.result.fish.name} fallback={state.result.fish.icon}/>
                            </span>{" "}
                            {state.result.fish.name} ·{" "}
                            {state.result.fish.rarity}
                          </p>) : <p>Kein Fang.</p>}
                        {state.result.kind === "fishing" && state.result.items.map(itemCard)}
                        {state.result.kind === "dive" && state.result.enemy && <p>{state.result.enemy.name}: {state.result.enemy.victory ? "Besiegt" : "Nicht besiegt"} · −{state.result.enemy.damageTaken} HP · +{state.result.enemy.experience} EXP</p>}
                      </>)}
                  </article>)}
              </>)}
            {tab === "Inventar" && (<>
                <p className="muted">
                  {state.inventory.length} Schätze · Favoriten und ausgerüstete
                  Items sind vor Zerlegen geschützt.
                </p>
                <article><h3>Sammlerboni</h3><p>{collectorText(collectors(state)).join(" · ")}</p><button onClick={() => void run({ type: "salvageBatch", selection: "common" })}>Common-Stapel prüfen</button><button onClick={() => void run({ type: "salvageBatch", selection: "duplicates" })}>Duplikate prüfen</button></article><label>Inventar durchsuchen<input value={search} onChange={e => setSearch(e.target.value)}/></label><div className="cards">{state.inventory.filter(i => (i.name + " " + i.rarity).toLowerCase().includes(search.toLowerCase())).map(itemCard)}</div>
              </>)}
            {tab === "Ausrüstung" &&
                slots.map((slot) => {
                    const i = state.inventory.find((i) => i.instanceId === state.equipment[slot]);
                    return (<section key={slot} className="equipment">
                    <h3>{slot}</h3>
                    {i ? (itemCard(i)) : (<article className="empty">✧ Freier Platz</article>)}
                  </section>);
                })}
            {tab === "Itemsets" && (<>
                <p className="muted">
                  Vier originale Sets aus v2.1.21. Es zählen unterschiedliche ausgerüstete Setteile.
                </p>
                {sets.map((set) => (<article key={set.id}>
                    <h3>🪸 {set.name}</h3>
                    <p>{setCount(state, set.id)} / 4 Teile ausgerüstet</p>
                    <progress max={4} value={setCount(state, set.id)}/>
                    {set.bonuses.map((b) => (<p className={setCount(state, set.id) >= b.pieces
                            ? "active-bonus"
                            : "muted"} key={b.pieces}>
                        {setCount(state, set.id) >= b.pieces
                            ? "✓ Aktiv"
                            : "○ Inaktiv"}{" "}
                        · {b.pieces} Teile · {b.description}
                      </p>))}
                  </article>))}
              </>)}
            {tab === "Perks" &&
                state.perks.map((p) => (<article key={p.id}>
                  <div className="item-top">
                    <h3>✧ {p.name}</h3>
                    <span>
                      {p.level}/{p.maxLevel}
                    </span>
                  </div>
                  <p>Pro Level: {statsText(p.bonus).join(", ")}</p>
                  <progress value={p.level} max={p.maxLevel}/>
                  <button disabled={busy ||
                        p.level >= p.maxLevel ||
                        state.player.pearls < perkCost(p.level)} onClick={() => void run({ type: "perk", id: p.id })}>
                    {p.level >= p.maxLevel
                        ? "Maximallevel"
                        : `Kaufen · ${perkCost(p.level).toLocaleString("de-DE")} Demo-Perlen`}
                  </button>
                </article>))}
            {tab === "Boss" && (<article className="boss">
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
                <progress aria-label="Boss HP" value={state.boss.hp} max={state.boss.maxHp}/>
                <p>
                  {state.boss.hp} / {state.boss.maxHp} HP
                </p>
                <button className="primary" disabled={busy ||
                    state.player.hp === 0 || state.player.defeatedBossId === state.boss.id || remaining(state.cooldowns.boss, now) > 0 ||
                    (state.boss.status === "waiting" && now < state.boss.nextAt)} onClick={() => void run({ type: "boss" })}>
                  ⚔ Demo-Angriff {remaining(state.cooldowns.boss, now) > 0 ? `· ${remaining(state.cooldowns.boss, now)} s` : ""}
                </button>
                <p>Dein Schaden: {state.boss.damage}</p>
                {state.boss.lastExchange && (<div className="combat-exchange" role="status">
                    <p>
                      Dein Treffer: {state.boss.lastExchange.damageDealt}{" "}
                      Schaden
                    </p>
                    <p className="damage-taken">
                      Gegentreffer: −{state.boss.lastExchange.damageTaken} HP
                    </p>
                    {state.boss.lastExchange.outcome === "victory" && (<strong>Boss besiegt! Kein Gegentreffer.</strong>)}
                    {state.boss.lastExchange.outcome === "defeat" && (<strong>
                        Niederlage · Für diesen Boss gesperrt, auch nach Heilung.
                      </strong>)}
                  </div>)}
                <small>
                  Ein Angriff alle 10 Sekunden. Normaler Boss: 10–20 Gegenschaden. Passive Heilung: 2 HP pro Minute.
                </small><p>{state.boss.reward?.message} {state.boss.reward && `+${state.boss.reward.pearls} Perlen · +${state.boss.reward.experience} EXP`}</p><button onClick={() => void run({ type: "newBoss" })}>Neuen Demo-Boss starten</button><button onClick={() => void run({ type: "healChat" })}>Chat-Heilung testen</button>
              </article>)}
            {tab === "Kochen" && (<>
                <article className="kitchen-intro">
                  <span className="kitchen-icon" aria-hidden="true">
                    🍣
                  </span>
                  <div>
                    <h3>Die Otter-Küche</h3>
                    <p>Aus deinem Fang wird Sushi mit magischen Kräften.</p>
                  </div>
                  <small>
                    Acht Rezepte. Je ein offensiver und defensiver Slot. Sushi gilt für drei unterschiedliche Bosse und bis zum Ende des dritten Bosses.
                  </small>
                </article>
                <div className="recipe-list">
                  {recipes.map((recipe) => {
                    const activeBuff = state.player.buffs.find((b) => b.recipeId === recipe.id && buffActive(b, now));
                    const canCook = recipe.ingredients.every(i => (state.fish[i.fishId] ?? 0) >= i.quantity) &&
                        state.player.pearls >= recipe.pearlCost;
                    const atBuffLimit = state.player.buffs.some(b => b.category === recipe.category && buffActive(b, now));
                    return (<article className="recipe" data-recipe-id={recipe.id} key={recipe.id}>
                        <div className="recipe-title">
                          <ItemArtwork id={`sushi-${recipe.id}`} name={recipe.name} fallback="🍣"/>
                          <div>
                            <small>
                              {recipe.activity === "Boss"
                            ? "BOSSKAMPF"
                            : recipe.activity === "Dive"
                                ? "TAUCHEN"
                                : "ANGELN"}
                            </small>
                            <h3>{recipe.name}</h3>
                          </div>
                        </div>
                        <p className="recipe-effect">
                          {recipeEffects(recipe)} · {recipe.category === "Offensive" ? "Offensiv" : "Defensiv"} · 3 Bosse
                        </p>
                        <div className="recipe-ingredients">
                          {recipe.ingredients.map(i => <p key={i.fishId}><ItemArtwork id={`fish-${i.fishId}`} name={i.name} fallback="🐟" small/> {i.name} <strong>{state.fish[i.fishId] ?? 0} / {i.quantity}</strong></p>)}
                          <p>
                            ◉ Kosten{" "}
                            <strong>
                              {recipe.pearlCost.toLocaleString("de-DE")}{" "}
                              Demo-Perlen
                            </strong>
                          </p>
                        </div>
                        <p>
                          Vorrat:{" "}
                          <strong>{state.sushi[recipe.id] ?? 0} Sushi</strong>
                        </p>
                        {activeBuff && (<p className="active-bonus">
                            ✓ Aktiv · {activeBuff.remainingUses} neue Bosse übrig
                          </p>)}
                        <div className="recipe-actions">
                          <button className="primary" disabled={busy || !canCook} onClick={() => void run({ type: "cook", recipeId: recipe.id })}>
                            Sushi herstellen
                          </button>
                          <button className="primary secondary" disabled={busy ||
                            (state.sushi[recipe.id] ?? 0) < 1 ||
                            !!activeBuff ||
                            atBuffLimit} onClick={() => void run({
                            type: "activateSushi",
                            recipeId: recipe.id,
                        })}>
                            {activeBuff ? "Buff aktiv" : "Sushi essen"}
                          </button>
                        </div>
                        {!canCook && (<small>
                            Zum Kochen fehlen Fische oder Demo-Perlen.
                          </small>)}
                        {!activeBuff && atBuffLimit && (<small>
                            Dieser Sushi-Slot ist bereits belegt.
                          </small>)}
                      </article>);
                })}
                </div>
                <p className="muted">
                  Demo-Startervorrat: je sechs Fische von zwölf Arten. Neuladen setzt die Demo zurück.
                </p>
              </>)}
          </>)}
        <p role="status" className="feedback">
          {message}
        </p>
      </main>
      <dialog ref={dialog} onCancel={() => void run({ type: "cancelSalvage" })}><h2>Zerlegen bestätigen</h2>{state.salvageQuote && <><p>Gültig: {remaining(state.salvageQuote.expiresAt, now)} Sekunden</p><ul>{state.salvageQuote.items.map(i => <li key={i.id}>{i.name} · {i.value} Perlen {i.warning ? "· Set / Legendär" : ""}</li>)}</ul><strong>Gesamt: {state.salvageQuote.total} Perlen</strong><div className="actions"><button onClick={() => void run({ type: "confirmSalvage", token: state.salvageQuote!.token })}>Jetzt zerlegen</button><button onClick={() => void run({ type: "cancelSalvage" })}>Abbrechen</button></div></>}</dialog>
      <footer>
        <label>
          <input type="checkbox" checked={!motion} onChange={(e) => setMotion(!e.target.checked)}/>{" "}
          Animationen aus
        </label>
        {!config && (<button onClick={() => void run({ type: "reset" })}>
            Demo zurücksetzen
          </button>)}
        <p>Nur Mockdaten · RPG v2.1.21 · keine echten Spielstände</p>
      </footer>
    </div>);
}
function Entry(){const [demo,setDemo]=useState(new URLSearchParams(location.search).get('demo')==='1'||location.pathname.endsWith('config.html'));return demo?<><button className="demo-return" onClick={()=>setDemo(false)}>Zur echten Plugin-Ansicht</button><App/></>:<LivePanel demo={()=>setDemo(true)}/>;}
createRoot(document.getElementById("root")!).render(<Entry />);
