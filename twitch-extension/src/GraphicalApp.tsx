import {BossGallery} from './BossGallery';
import {isCollectionItem} from './collectionInventory';
import {CodexCharacter} from './CodexCharacter';
import {ItemComparison} from './ItemComparison';
import {ZonePanel} from './ZonePanel';
import {MaterialInventory,EquipmentUpgrade,materialNames} from './UpgradePanel';
import {PerkPanel} from './PerkPanel';
import {TalentTree} from './TalentTree';
import { useEffect, useState, useRef, type ReactNode } from "react";
import { createRoot } from "react-dom/client";
import { labels, statsText, numberStat, recipeEffects, collectorText } from "./presentation";
import { slots } from "./types";
import type { Action, InventoryItem, RpgDataSource, CharacterStats, ItemSet, SushiRecipe, Perk } from "./types";
import "./style.css";
import { Icon } from "./icons";
import {ItemArtwork} from "./item-art";
import {PearlMinigames} from "./minigames/PerlMinigames";
import {SelfDeletion} from './remote/SelfDeletion';
import type {MinigameClient} from "./minigames/contracts";
import type {RecipeView} from "./remote/model";
export interface DemoPresentation {
 sets:ItemSet[];recipes:SushiRecipe[];characterStats:(s:any)=>CharacterStats;collectors:(s:any)=>any;setCount:(s:any,id:string)=>number;
 diveInfo:(s:any,stake:number,now:number)=>{chance:number;rarities:string[]};perkCost:(level:number)=>number;hotActive:(s:any,now:number)=>boolean;
}
const format=(v:number)=>Number.isFinite(v)?v.toLocaleString('de-DE'):'–';
const remaining=(until:number,now:number)=>Number.isFinite(until)?Math.max(0,Math.ceil((until-now)/1000)):0;
const buffActive=(b:any,now:number)=>b.expiresAt!==undefined?b.expiresAt>now:b.remainingUses>0;
const tabs = [
    "Charakter",
    "Abenteuer",
    "Inventar",
    "Ausrüstung",
    "Itemsets",
    "Talente",
    "Perks",
    "Boss",
    "Kochen",
    "Perlen-Minispiele",
    "Aufwertungs-Materialien",
];
const symbols = [
    "otter",
    "compass",
    "bag",
    "swords",
    "sets",
    "perk",
    "perk",
    "boss",
    "cook",
    "pearl",
    "bag",
] as const;
const time = (n: number) => `${Math.floor(n / 60)}:${String(n % 60).padStart(2, "0")}`;
export function App({source,demo,minigameClient,desktop=false,desktopAdmin}:{source:RpgDataSource;demo?:DemoPresentation;minigameClient?:MinigameClient;desktop?:boolean;desktopAdmin?:ReactNode}) {
    const desktopTabs=["Abenteuer","Charakter","Waffen","Ausrüstung","Inventar","Aufwertung","Materialien","Sammlungen","Itemsets","Boss","Sushi-Küche","Talente & Perks","Minispiele","Werte & Statistiken","RPG-Verwaltung"];
    const isDemo=source.mode==="demo";
    const allowed=(type:Action["type"])=>isDemo || source.canDispatch?.(type)===true;
    const clickLock=useRef(false);
    const [state, setState] = useState(source.getSnapshot()), [tab, setTab] = useState(desktop?"Abenteuer":"Charakter"), [now, setNow] = useState(Date.now()), [motion, setMotion] = useState(true), [stake, setStake] = useState(250), [fishStake, setFishStake] = useState(0), [search, setSearch] = useState(""), [busy, setBusy] = useState(false), [message, setMessage] = useState(""), [revealing, setRevealing] = useState(false), [helper, setHelper] = useState("Lokale Vorschau");
    const [focusedItem,setFocusedItem]=useState<string>();
    const sets=isDemo?demo!.sets:state.remote?.sets??[];
    const recipes:RecipeView[]=isDemo?demo!.recipes:state.remote?.recipes??[];
    const loaded=isDemo||state.remote?.loaded===true;
    const affordable=(n:number)=>Number.isSafeInteger(n)&&n>=0&&(isDemo?state.player.pearls>=n:!!state.remote?.pointsExact&&BigInt(state.remote.pointsExact)>=BigInt(n));
    const perkPrice=(p:Perk)=>isDemo?demo!.perkCost(p.level):state.remote?.perkPrices[p.id]??Number.NaN;
    const setCount=(s:typeof state,id:string)=>isDemo?demo!.setCount(s,id):new Set(s.inventory.filter(i=>i.setId===id&&sets.find(v=>v.id===id)?.itemIds.includes(i.itemId)&&Object.values(s.equipment).includes(i.instanceId)).map(i=>i.itemId)).size;
    const collectors=(s:typeof state)=>isDemo?demo!.collectors(s):s.remote?.collectors??{};
    const dialog = useRef<HTMLDialogElement>(null);
    const upgradeDialog=useRef<HTMLDialogElement>(null);
    const [upgradeItem,setUpgradeItem]=useState<string|null>(null);
    const upgrading=state.inventory.find(i=>i.instanceId===upgradeItem);
    useEffect(()=>{if(upgradeItem){upgradeDialog.current?.showModal();upgradeDialog.current?.querySelector<HTMLElement>("h2")?.focus();}else upgradeDialog.current?.close();},[upgradeItem]);
    useEffect(() => { if (state.salvageQuote)
        dialog.current?.showModal();
    else
        dialog.current?.close(); }, [state.salvageQuote]);
    useEffect(()=>{
        const unsubscribe=source.subscribe?.(()=>setState(source.getSnapshot()));
        const update=()=>void source.dispatch({type:"refresh"}).then(setState).catch(e=>setMessage(e instanceof Error?e.message:"Spielstand nicht erreichbar."));
        update();const timer=setInterval(update,1000);return()=>{unsubscribe?.();clearInterval(timer);};
    },[source]);
    const [shellCount,setShellCount]=useState(1);
    const canOpenShells=(count:number)=>!isDemo && allowed("openShells") && !busy && Number.isInteger(count) && count>=1 && count<=100 && state.materials!==undefined && state.materials.shells>=count;
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
        if(isDemo)w.Twitch?.ext?.onAuthorized(() => setHelper("Twitch Helper bereit · Demo"));
    }, []);
    const run = async (action: Action) => {
        if (clickLock.current || !allowed(action.type))
            return;
        clickLock.current=true;
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
                    : isDemo ? "Demo aktualisiert." : next.remote?.message??"Gespeicherten Spielstand geladen.");
        }
        catch (e) {
            setMessage(e instanceof Error ? e.message : "Aktion fehlgeschlagen.");
        }
        finally {
            clickLock.current=false;
            setBusy(false);
        }
    };
    const hot = isDemo?demo!.hotActive(state,now):state.remote?.hotKnown===true&&state.hotTime.active&&state.hotTime.endsAt>now;
    const seconds=remaining(state.hotTime.endsAt,now);
    const stats=isDemo?demo!.characterStats(state):Object.fromEntries(Object.keys(labels).map(k=>[k,state.remote?.stats?.[k as keyof CharacterStats]??Number.NaN])) as unknown as CharacterStats;
    const preview=state.remote?.adventurePreview;
    const info=isDemo?demo!.diveInfo(state,stake,now):preview?.dive.stake===String(stake)?preview.dive:{chance:Number.NaN,rarities:[] as string[]};
    useEffect(()=>{
        if(isDemo||tab!=="Abenteuer"||!source.previewAdventures)return;
        const timer=setTimeout(()=>void source.previewAdventures!(stake,fishStake).catch(()=>{}),300);
        return()=>clearTimeout(timer);
    },[source,isDemo,tab,stake,fishStake,state.remote?.updatedAt,state.remote?.connection,state.remote?.pending]);
    const equipped = (i: InventoryItem) => Object.values(state.equipment).includes(i.instanceId);
    function itemCard(i: InventoryItem) {
        return (<article data-instance-id={i.instanceId} className={`item ${i.rarity}`} key={i.instanceId}>
        <div className="item-top">
          <ItemArtwork id={i.itemId} name={i.name} fallback={i.icon}/>
          <small>
            {i.rarity} · {i.slot ?? "Fundstück"}
          </small>
        </div>
        <h3>{i.name}{(i.upgradeLevel??0)>0?` +${i.upgradeLevel}`:""}{i.isRefined?" · Veredelt":""}</h3>
        <p>{i.description}</p>
        <ul className="attributes">
          {statsText(i.attributes).map((t) => (<li key={t}>{t}</li>))}
        </ul>
        <p>
          {i.effects.length
                ? i.effects.map((e) => e.description).join(" ")
                : "Kein zusätzlicher Spezialeffekt."}
        </p>
        <ItemComparison source={source} item={i}/>{i.setId && (<small>Set: {sets.find((s) => s.id === i.setId)?.name}</small>)}
        {i.collector && <p>{collectorText(i.collector).join(" · ")}</p>}
        <div className="actions">
          {i.slot && (i.rarity==="Divine"?<span>ADMIN ONLY · Nicht aufwertbar</span>:<button onClick={()=>setUpgradeItem(i.instanceId)} disabled={busy}>Aufwerten</button>)}
          <button aria-pressed={i.locked} onClick={() => void run({ type: "lock", id: i.instanceId })} disabled={!allowed("lock") || busy}>{i.locked ? "Entsperren" : "Sperren"}</button>
          {i.slot &&
                (equipped(i) ? (<button onClick={() => {
                        const slot = slots.find((slot) => state.equipment[slot] === i.instanceId);
                        if (slot)
                            void run({ type: "unequip", slot });
                    }} disabled={!allowed("unequip") || busy}>
                Ablegen
              </button>) : (<>
                <button onClick={() => void run({ type: "equip", id: i.instanceId, slot: i.slot! })} disabled={!allowed("equip") || busy}>
                  Ausrüsten{i.slot.startsWith("Ring") ? " · Ring1" : ""}
                </button>
                {i.slot.startsWith("Ring") && (<button onClick={() => void run({
                            type: "equip",
                            id: i.instanceId,
                            slot: "Ring2",
                        })} disabled={!allowed("equip") || busy}>
                    Ring2
                  </button>)}
              </>))}
          <button aria-pressed={i.favorite} onClick={() => void run({ type: "favorite", id: i.instanceId })} disabled={!allowed("favorite") || busy}>
            {i.favorite ? "★ Favorisiert" : "☆ Favorisieren"}
          </button>
          <button disabled={i.rarity==="Divine" || !allowed("dismantle") || busy || (i.favorite || i.locked || equipped(i))} onClick={() => void run({ type: "dismantle", id: i.instanceId })}>
            Zerlegen prüfen
          </button>
        </div>
      </article>);
    }
    const hotControls = (<div className="actions">
      <button onClick={() => void run({ type: "hot", duration: 300000 })} disabled={!allowed("hot") || busy}>
        5 Minuten starten
      </button>
      <button onClick={() => void run({ type: "hot", duration: 30000 })} disabled={!allowed("hot") || busy}>
        Letzte 30 Sekunden
      </button>
      <button onClick={() => void run({ type: "hot", duration: 0 })} disabled={!allowed("hot") || busy}>
        Beenden
      </button>
    </div>);
    return (<div className={(motion ? "app" : "app no-motion")+(desktop?" codex-app":"")}>
      <div className={`hot-hud ${hot ? "burning" : ""} ${hot && seconds <= 30 ? "urgent" : ""}`} title={isDemo?"Hot Time: +50 % relative Itemchance, +100 % zulässige Perlenbelohnungen":"Nur bestätigte Spielwerte werden angezeigt"}>
        <progress aria-label="Hot-Time-Restzeit" max={300} value={hot ? Math.min(300, seconds) : 0} aria-valuetext={hot ? `${time(seconds)} verbleibend` : "Hot Time inaktiv"}/>
        <div className="hot-hud-label">
          <span>🔥 {hot ? "HOT TIME" : isDemo?"Hot Time inaktiv":"Hot-Time-Status nicht übermittelt"}</span>
          <strong className="hot-timer">{hot ? time(seconds) : isDemo ? "0:00" : "–"}</strong>
          {hot && <small>+50 % Drops · +100 % Perlen</small>}
        </div>
      </div>
      <header>
        <picture><source media="(min-width: 901px)" srcSet="./otter-mage.png"/><img src="./otter-mage-compact.webp" alt="Otter-Magier"/></picture>
        <div>
          <small>FUNJII · LEUCHTENDE TIEFE</small>
          <h1>
            {desktop?"Funjii Otter RPG – Deep Sea Codex":"Otter RPG"} <span>{isDemo?"DEMO":"RPG"}</span>
          </h1>
          <p>{desktop?state.player.username+" · Level "+format(state.player.level):"Dein Abenteuer unter den Wellen."}</p>{desktop&&<small>{state.remote?.experienceText??format(state.player.experience)} EXP</small>}
        </div>
      </header>
      <div className="statusbar">
        <span>
          <Icon name="pearl"/> {isDemo?state.player.pearls.toLocaleString("de-DE"):state.remote?.pointsText??"–"}{" "}
          {isDemo?"Demo-Perlen":"Perlen"}
        </span>
        <small role="status">{isDemo?helper:state.remote?.message}</small>{!isDemo&&state.remote?.connection==="unauthorized"&&<button onClick={()=>source.requestIdentity?.()}>Twitch-Identität freigeben</button>}
      </div>
      {!isDemo&&<p className="feedback" role="status">{state.remote?.updatedAt?"Gespeichert: "+new Date(state.remote.updatedAt).toLocaleString("de-DE"):"Noch kein gespeicherter Spielstand geladen."}{state.remote?.syncing?" · Aktualisierung wird erwartet.":""}{state.remote?.pending&&<> Eine Aktion ist noch offen. <button disabled={busy} onClick={()=>{if(clickLock.current)return;clickLock.current=true;setBusy(true);void source.retryPending?.().then(setState).catch(e=>setMessage(e.message)).finally(()=>{clickLock.current=false;setBusy(false);});}}>Dieselbe Aktion prüfen</button></>}</p>}
      {!config && (<section className={`otter-health ${state.player.hp <= state.player.maxHp * 0.25 ? "health-low" : ""} ${state.boss.lastExchange?.damageTaken ? "health-hit" : ""}`} key={`health-${state.boss.lastExchange?.turn ?? 0}-${state.player.hp}`} aria-label="Lebenspunkte deines Otters">
          <div className="health-label">
            <span>♥ Otter-HP</span>
            <strong>
              {format(state.player.hp)} / {format(state.player.maxHp)} HP
            </strong>
          </div>
          <progress aria-label="Otter HP" value={Number.isFinite(state.player.hp)?state.player.hp:undefined} max={Number.isFinite(state.player.maxHp)&&state.player.maxHp>0?state.player.maxHp:undefined}/>
          {(state.player.shieldHp??0)>0 && <small aria-label="Aktiver Schild">🛡 {format(state.player.shieldHp!)} Schildpunkte</small>}
          {state.player.hp === 0 ? (<div className="defeat-notice">
              <p role="status">Dein Otter ist besiegt.</p>
              {isDemo&&(<button onClick={() => void run({ type: "healChat" })} disabled={!allowed("healChat") || busy}>
                Chat-Heilung testen
              </button>)}
              <small>
                10 HP alle 30 Sekunden. Die Todessperre bleibt für diesen Boss bestehen.
              </small>
            </div>) : state.player.hp <= state.player.maxHp * 0.25 ? (<small>
              Wenig Lebenspunkte – der nächste Gegentreffer kann dich besiegen.
            </small>) : null}
        </section>)}
      {!config && (<nav aria-label="Hauptnavigation">
          {(desktop?desktopTabs:tabs).map((name, n) => (<button key={name} aria-current={tab === name ? "page" : undefined} disabled={desktop&&name==="RPG-Verwaltung"&&!desktopAdmin} onClick={() => {
                    setTab(name);
                    setMessage("");
                }}>
              <Icon name={desktop?(["compass","otter","swords","swords","bag","sets","pearl","bag","sets","boss","cook","perk","pearl","sets","perk"] as const)[n]:symbols[n]}/>
              {name}
            </button>))}

          {desktop&&<button onClick={()=>setTab("Einstellungen")} aria-current={tab==="Einstellungen"?"page":undefined}>⚙ Einstellungen</button>}
        </nav>)}
      {focusedItem&&<div className="codex-item-dialog" role="dialog" aria-label="Itemdetails"><button onClick={()=>setFocusedItem(undefined)}>Itemdetails schließen</button>{state.inventory.filter(i=>i.instanceId===focusedItem).map(itemCard)}</div>}
      <main>
        <div className="section-title">
          <div>
            <small>
              {config ? isDemo?"DEIN LOKALES TESTLABOR":"VERBINDUNG" : "ENTDECKE DIE TIEFE"}
            </small>
            <h2>{config ? isDemo?"Demo-Konfiguration":"Extension-Konfiguration" : tab}</h2>
          </div>
          <span aria-hidden="true">✦</span>
        </div>
        {!loaded&&!config?<article><h3>Dein Spielstand</h3><p>{state.remote?.message}</p></article>:config&&!isDemo?<article><h3>Backend-Verbindung</h3><p>{state.remote?.message}</p><p>Die Verbindung wird durch Twitch hergestellt. Hier werden keine Schlüssel oder Zugangsdaten gespeichert.</p></article>:config ? (<>
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
            <button onClick={() => void run({ type: "reset" })} disabled={!allowed("reset") || busy}>
              Demo zurücksetzen
            </button>
          </>) : (<>
            {(tab === "Perlen-Minispiele"||tab==="Minispiele") && <>
              <article><h3>Muscheln öffnen</h3>
                <p>Eine Öffnung verbraucht eine Muschel. Muscheln findest du statt des alten Stiefels beim Angeln.</p>
                {state.materials ? <><p>Muscheln: {format(state.materials.shells)}</p>
                  <p>Upgrade-Perlen: {format(state.materials.whitePearls)} weiß · {format(state.materials.bluePearls)} blau · {format(state.materials.redPearls)} rot</p>
                  <p>Steinstückchen: {format(state.materials.stoneChips)}</p></>
                  : <p>Materialbestand nicht übermittelt. Öffnen ist noch nicht verfügbar.</p>}
                <p className="muted">3 % weiße, 2 % blaue, 1 % rote Upgrade-Perle; 47 % ergeben 10–50 Perlen, 47 % ein Steinstückchen.</p>
                <div className="actions">{[1,10,100].map(count=><button key={count} disabled={!canOpenShells(count)} onClick={()=>void run({type:"openShells",count})}>{count} öffnen</button>)}</div>
                <label>Anzahl (1–100)<input type="number" min="1" max="100" step="1" value={Number.isNaN(shellCount)?"":shellCount} onChange={event=>setShellCount(event.target.value===""?Number.NaN:Number(event.target.value))}/></label>
                <button disabled={!canOpenShells(shellCount)} onClick={()=>void run({type:"openShells",count:shellCount})}>Auswahl öffnen</button>
              </article>
              {(isDemo?minigameClient:source.minigames)?<PearlMinigames client={(isDemo?minigameClient:source.minigames)!}/>:<article><h3>Die Perlen-Spielhalle</h3><p>Für diese Spiele ist noch kein sicherer Backend-Aufruf freigegeben. Dein gespeicherter Perlenstand bleibt unverändert.</p></article>}
            </>}
            {(tab === "Aufwertungs-Materialien"||tab==="Materialien"||tab==="Aufwertung") && <>{state.materialFinds&&<p role="status">Materialien erhalten: {Object.entries(state.materialFinds).map(([id,n])=>`${n} × ${materialNames[id]??id}`).join(', ')}</p>}{tab!=="Aufwertung"&&<MaterialInventory stock={state.materials} catalog={state.remote?.materialCatalog}/>}<h3>Ausrüstung aufwerten</h3><div className="cards">{state.inventory.filter(i=>i.slot).map(itemCard)}</div></>}
            {tab === "Charakter" && (<>
                {desktop&&<CodexCharacter state={state} stats={stats} onItem={i=>setFocusedItem(i.instanceId)}/>}
                <article className="hero" hidden={desktop}>
                  <img src={state.player.avatarUrl} alt="Otter-Magier als Spielillustration"/>
                  <div>
                    <small>HÜTER DER PERLE</small>
                    <h3>{state.player.username}</h3>
                    <p>Dein nächster Schatz wartet schon.</p>
                  </div>
                </article>
                <article hidden={desktop}><h3>Level {format(state.player.level)}</h3><p>{state.remote?.experienceText??format(state.player.experience)} EXP{isDemo?` · nächstes Level: ${1000 * state.player.level * (state.player.level + 1) / 2} EXP`:""}</p>{isDemo&&(<button onClick={() => void run({ type: "healChat" })} disabled={!allowed("healChat") || busy}>Chat-Heilung testen</button>)}{isDemo&&(<button onClick={() => void run({ type: "demoWound" })} disabled={!allowed("demoWound") || busy}>Verletzung simulieren</button>)}</article><div className="stat-grid" hidden={desktop}>
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
                        {b.name} · {b.expiresAt!==undefined?time(remaining(b.expiresAt,now))+" verbleibend":b.remainingUses+" neue Bosse"} ·{" "}
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
                    {isDemo?"Buffs gelten nur für ihre Aktivität; Werte oben ohne temporäre Buffs. Sets: v2.1.21.":"Effektive Werte werden ausschließlich aus dem verbundenen Spiel übernommen. Nicht übermittelte Werte stehen als –."}
                  </small>
                </article>
              </>)}
            {tab === "Abenteuer" && (<>
                {!isDemo&&state.remote?.zones&&source.readPlugin&&<ZonePanel zones={state.remote.zones} diving={state.remote.selectedDivingZone??"sunny_lagoon"} fishing={state.remote.selectedFishingZone??"sunny_lagoon"} busy={busy||state.remote.connection!=="ready"} cooldown={remaining(state.cooldowns.dive,now)} fishingCooldown={remaining(state.cooldowns.fishing,now)} onSelect={async(activity,zoneId)=>{setBusy(true);try{await source.dispatch({type:"selectZone",activity,zoneId});}finally{setBusy(false);}}} onStart={activity=>void run({type:activity==="dive"?"dive":"fishing",stake:0})}/>}
                <article className="adventure">
                  <div className="sea-art">
                    <picture><source media="(min-width: 901px)" srcSet="./luminous-depths.png"/><img src="./luminous-depths-compact.webp" alt="Otter-Magier zwischen leuchtenden Quallen und einem Perlenschatz in versunkenen Ruinen" width="1536" height="1024"/></picture>
                    <div className="sea-caption">
                      <span>LEUCHTENDE TIEFE</span>
                      <h3>Ein Meer voller Geheimnisse.</h3>
                    </div>
                  </div>
                  <div className="adventure-controls" hidden={!isDemo&&!!state.remote?.zones}>
                    <label>
                      Einsatz in {isDemo?"Demo-Perlen":"Perlen"}
                      <select value={stake} onChange={(e) => setStake(Number(e.target.value))}>
                        {[0, 250, 1000, 5000, 10000].map((n) => (<option key={n} value={n}>
                            {n.toLocaleString("de-DE")}
                          </option>))}
                      </select>
                    </label>
                    <div className="loot-chance">
                      <span>Lootchance · Tauchen</span>
                      <strong>{Number.isFinite(info.chance)?info.chance.toFixed(1)+" %":"Nicht übermittelt"}</strong>
                      <small>
                        {isDemo?(hot ? "Hot Time eingerechnet" : "Aktuelle Itemchance"):"Primärer Ausrüstungsfund · vom Plugin berechnet"}
                      </small>
                    </div>
                    <label>Angel-Einsatz<select value={fishStake} onChange={e => setFishStake(Number(e.target.value))}>{[0, 10, 250, 1000].map(n => <option key={n} value={n}>{n} Perlen</option>)}</select></label>{!isDemo&&<small>Ausrüstungs-Lootchance beim Angeln: {preview?.fish.stake===String(fishStake)?preview.fish.chance.toFixed(2)+" %":"Wird vom Plugin geladen …"}</small>}<div className="adventure-buttons">
                      <button className="primary" disabled={!allowed("dive") || busy || (busy || state.player.hp === 0 ||
                    revealing ||
                    remaining(state.cooldowns.dive, now) > 0 ||
                    !affordable(stake))} onClick={() => void run({ type: "dive", stake })}>
                        <Icon name="dive"/>
                        <span>
                          {remaining(state.cooldowns.dive, now) > 0
                    ? `Tauchen · ${time(remaining(state.cooldowns.dive, now))}`
                    : "Tauchgang starten"}
                        </span>
                      </button>
                      <button className="primary secondary" disabled={!allowed("fishing") || busy || (busy ||
                    revealing ||
                    remaining(state.cooldowns.fishing, now) > 0)} onClick={() => void run({ type: "fishing", stake: fishStake })}>
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
                      {isDemo?"Simulierte Belohnungen · je 30 Sekunden Cooldown":"Kosten, Belohnungen und Cooldowns prüft das verbundene Spiel."}
                    </small>
                  </div>
                </article>
                {state.result && (<article className={revealing ? "reveal animating" : "reveal"} aria-live="polite">
                    {revealing ? (<p>🫧 Die Tiefe gibt ihren Schatz frei …</p>) : (<>
                        <h3>{state.result.message}</h3>
                        <p>
                          +{format(state.result.pearls)} {isDemo?"Demo-Perlen":"Perlen Auszahlung"}{" "}
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
                          </p>) : <p>{isDemo?"Kein Fang.":"Der gespeicherte Fischvorrat wird aktualisiert."}</p>}
                        {state.result.kind === "fishing" && state.result.items.map(itemCard)}
                        {state.result.kind === "dive" && state.result.enemy && <p>{state.result.enemy.name}: {state.result.enemy.victory ? "Besiegt" : "Nicht besiegt"} · −{state.result.enemy.damageTaken} HP · +{state.result.enemy.experience} EXP</p>}
                      </>)}
                  </article>)}
              </>)}
            {desktop&&(tab==="Admin"||tab==="RPG-Verwaltung")&&desktopAdmin}
            {tab === "Einstellungen"&&<article><h3>Spiel &amp; Einstellungen</h3>{["Talente","Perks","Kochen","Perlen-Minispiele"].map(name=><button key={name} onClick={()=>setTab(name)}>{name}</button>)}<p>Animationen können am unteren Fensterrand ausgeschaltet werden. Anmeldung und Updates findest du im Windows-Fenster.</p></article>}
            {tab==="Sammlungen"&&<><h3>Sammelinventar</h3><p>Sammelobjekte werden hier aufbewahrt. Überzählige Exemplare ab dem sechsten werden automatisch zum Zerlegewert in Perlen umgewandelt. Gesperrte und favorisierte Stücke bleiben geschützt.</p><div className="cards">{(state.remote?.collectorItems??[]).map(def=>{const quantity=state.inventory.filter(i=>isCollectionItem(i)&&i.itemId===def.Id).length;return <article key={def.Id}><ItemArtwork id={def.Id} name={def.Name}/><h3>{def.Name}</h3><p>{quantity}/5</p><progress value={quantity} max={5}/><p>Fundort: Gebietstruhen und berechtigte Kämpfe</p></article>;})}</div><p>{(state.remote?.collectorItems??[]).reduce((n,d)=>n+Math.min(5,state.inventory.filter(i=>i.itemId===d.Id).length),0)} / {(state.remote?.collectorItems?.length??0)*5} gesammelt</p></>}
            {tab==="Werte & Statistiken"&&<><h3>Charakterwerte</h3><p>Aktuelle Werte aus deinem synchronisierten Spielstand.</p><div className="cards"><article>{statsText(state.remote?.stats??{}).map(text=><p key={text}>{text}</p>)}</article><article><h3>Sammlerboni</h3>{collectorText(collectors(state)).map(text=><p key={text}>{text}</p>)}<p>Weitere persönliche Kampfstatistiken: noch nicht erfasst.</p></article></div></>}
            {tab==="Waffen"&&<><h3>Waffenverwaltung</h3><label>Waffen suchen<input value={search} onChange={e=>setSearch(e.target.value)}/></label><div className="cards">{state.inventory.filter(i=>i.slot==="Weapon"&&(i.name+" "+i.rarity).toLowerCase().includes(search.toLowerCase())).map(itemCard)}</div>{!state.inventory.some(i=>i.slot==="Weapon")&&<p>Noch keine Waffe im Inventar.</p>}</>}
            {tab === "Inventar" && (<>
                <p className="muted">
                  {state.inventory.length} Schätze · Favoriten und ausgerüstete
                  Items sind vor Zerlegen geschützt.
                </p>
                <article><h3>Sammlerboni</h3><p>{collectorText(collectors(state)).join(" · ")}</p><button onClick={() => void run({ type: "salvageBatch", selection: "common" })} disabled={!allowed("salvageBatch") || busy}>Common-Stapel prüfen</button><button onClick={() => void run({ type: "salvageBatch", selection: "duplicates" })} disabled={!allowed("salvageBatch") || busy}>Duplikate prüfen</button></article><label>Inventar durchsuchen<input value={search} onChange={e => setSearch(e.target.value)}/></label><div className="cards">{state.inventory.filter(i => !isCollectionItem(i) && (i.name + " " + i.rarity).toLowerCase().includes(search.toLowerCase())).map(itemCard)}</div>
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
                  {isDemo?"Vier originale Sets aus v2.1.21. Es zählen unterschiedliche ausgerüstete Setteile.":sets.length?"Setdaten aus dem verbundenen Spiel. Es zählen unterschiedliche ausgerüstete Setteile.":"Der Setkatalog wurde noch nicht vom Spiel übermittelt."}
                </p>
                {sets.map((set) => (<article key={set.id}>
                    <h3>🪸 {set.name}</h3>
                    <div className="set-artwork">{set.itemIds.map(id=><ItemArtwork key={id} id={id} small/>)}</div>
                    <p>{setCount(state, set.id)} / {set.itemIds.length} Teile ausgerüstet</p>
                    <progress max={set.itemIds.length} value={setCount(state, set.id)}/>
                    {set.bonuses.map((b) => (<p className={setCount(state, set.id) >= b.pieces
                            ? "active-bonus"
                            : "muted"} key={b.pieces}>
                        {setCount(state, set.id) >= b.pieces
                            ? "✓ Aktiv"
                            : "○ Inaktiv"}{" "}
                        · {b.pieces} Teile · {b.description||statsText(b.attributes).join(", ")}
                      </p>))}
                  </article>))}
              </>)}
            {(tab === "Talente"||tab==="Talente & Perks") && <TalentTree state={state} demo={isDemo} busy={busy} canLearn={allowed("learnTalent")} onLearn={id=>void run({type:"learnTalent",id})}/>}
            {(tab === "Perks"||tab==="Talente & Perks") && <PerkPanel perks={state.perks} price={perkPrice} affordable={affordable} canBuy={allowed("perk")} busy={busy} demo={isDemo} pearls={isDemo?format(state.player.pearls):state.remote?.pointsExact?BigInt(state.remote.pointsExact).toLocaleString('de-DE'):'–'} canBuyMax={!isDemo&&state.remote?.perkMaxPurchase===true} onBuy={(id,maximum)=>void run({type:"perk",id,maximum})}/>}
            {tab === "Boss"&&state.remote?.areaBoss?.enabled&&!state.boss.isMimic&&<BossGallery view={state.remote.areaBoss} hp={state.player.hp} maxHp={state.player.maxHp} buffs={state.player.buffs.filter(b=>buffActive(b,now)).map(b=>b.recipeId)} busy={busy} canAttack={allowed("boss")} canSelect={allowed("selectBoss")} cooldown={remaining(state.cooldowns.boss,now)} onAttack={()=>void run({type:"boss"})} onSelect={id=>void run({type:"selectBoss",id})}/>}
            {tab === "Boss"&&(!state.remote?.areaBoss?.enabled||state.boss.isMimic) && (<article className="boss">
                <div className="boss-art">
                  <img src={state.boss.isMimic?"./boss-mimic.png":"./boss-kraken.png"} alt={state.boss.isMimic?"Mimic – lebendige Schatztruhe":"Kraken in versunkenen Ruinen"} width="1254" height="1254" loading="lazy"/>
                </div>
                <small>{state.boss.isMimic?"MIMIC · LEBENDE SCHATZTRUHE":"BOSS DER TIEFSEE"}</small>
                <h3>{state.boss.name}</h3>
                <p>
                  {!isDemo&&!state.remote?.bossKnown?"Aktueller Bossstatus nicht übermittelt. Bossangriffe sind noch nicht freigegeben.":state.boss.status === "active"
                    ? state.boss.isMimic?`Die Mimic schnappt nach dir! Legendary-Chance nach Sieg: ${state.boss.legendaryChance??50} % je berechtigtem Überlebenden.`:`Der Boss wartet auf dich!${state.boss.legendaryChance!==undefined?` Legendary-Chance nach Sieg: ${state.boss.legendaryChance} % je berechtigtem Überlebenden.`:""}`
                    : !isDemo?"Aktuell ist kein Boss aktiv. Start durch Mods/Broadcaster oder den Plugin-Autostart.":"Besiegt · Nächster Boss in " +
                        time(remaining(state.boss.nextAt, now))}
                </p>
                {(isDemo || state.remote?.bossKnown) && state.boss.status === "active" && (
                  <p aria-label="Boss-Restzeit">
                    {Number.isFinite(state.boss.endsAt) && state.boss.endsAt > 0
                      ? remaining(state.boss.endsAt, now) > 0
                        ? `Verbleibende Zeit: ${time(remaining(state.boss.endsAt, now))}`
                        : "Zeit abgelaufen · Bossstatus wird aktualisiert."
                      : "Verbleibende Zeit: nicht übermittelt"}
                  </p>
                )}
                <progress aria-label="Boss HP" value={Number.isFinite(state.boss.hp)?state.boss.hp:undefined} max={Number.isFinite(state.boss.maxHp)&&state.boss.maxHp>0?state.boss.maxHp:undefined}/>
                <p>
                  {format(state.boss.hp)} / {format(state.boss.maxHp)} HP
                </p>
                <button className="primary" disabled={!allowed("boss") || busy || (busy ||
                    state.player.hp === 0 || state.player.defeatedBossId === state.boss.id || remaining(state.cooldowns.boss, now) > 0 ||
                    (state.boss.status === "waiting" && now < state.boss.nextAt))} onClick={() => void run({ type: "boss" })}>
                  ⚔ {isDemo?"Demo-Angriff":"Boss angreifen"} {remaining(state.cooldowns.boss, now) > 0 ? `· ${remaining(state.cooldowns.boss, now)} s` : ""}
                </button>
                <p>Dein gespeicherter Schaden: {format(state.boss.damage)}</p>
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
                  {isDemo?"Ein Angriff alle 10 Sekunden. Normaler Boss: 10–20 Gegenschaden. Passive Heilung: 2 HP pro Minute.":"Es werden keine Bossregeln oder Bossgegner im Browser simuliert."}
                </small><p>{state.boss.reward?.message} {state.boss.reward && `+${state.boss.reward.pearls} Perlen · +${state.boss.reward.experience} EXP`}</p>{isDemo&&(<button onClick={() => void run({ type: "newBoss" })} disabled={!allowed("newBoss") || busy}>Neuen Demo-Boss starten</button>)}{isDemo&&(<button onClick={() => void run({ type: "healChat" })} disabled={!allowed("healChat") || busy}>Chat-Heilung testen</button>)}
              </article>)}
            {(tab === "Kochen"||tab==="Sushi-Küche") && (<>
                <article className="kitchen-intro">
                  <span className="kitchen-icon" aria-hidden="true">
                    🍣
                  </span>
                  <div>
                    <h3>Die Otter-Küche</h3>
                    <p>Aus deinem Fang wird Sushi mit magischen Kräften.</p>
                  </div>
                  <small>
                    {isDemo?"Acht Rezepte. Je ein offensiver und defensiver Slot. Sushi gilt für drei unterschiedliche Bosse und bis zum Ende des dritten Bosses.":recipes.length?"Rezepte und Zutaten aus dem verbundenen Spiel.":"Der Rezeptkatalog wurde noch nicht vom Spiel übermittelt."}
                  </small>
                </article>
                <div className="recipe-list">
                  {recipes.map((recipe) => {
                    const activeBuff = state.player.buffs.find((b) => b.recipeId === recipe.id && buffActive(b, now));
                    const canCook = recipe.ingredients.every(i => (state.fish[i.fishId] ?? 0) >= i.quantity) &&
                        affordable(recipe.pearlCost);
                    const atBuffLimit = state.player.buffs.some(b => recipe.category!==undefined && b.category === recipe.category && buffActive(b, now));
                    return (<article className="recipe" data-recipe-id={recipe.id} key={recipe.id}>
                        <div className="recipe-title">
                          <ItemArtwork id={`sushi-${recipe.id}`} name={recipe.name} fallback="🍣"/>
                          <div>
                            <small>
                              {recipe.activity === "Boss"
                            ? "BOSSKAMPF"
                            : recipe.activity === "Dive"
                                ? "TAUCHEN"
                                : recipe.activity==="Fishing"?"ANGELN":"AKTIVITÄT NICHT ÜBERMITTELT"}
                            </small>
                            <h3>{recipe.name}</h3>
                          </div>
                        </div>
                        <p className="recipe-effect">
                          {isDemo?recipeEffects(recipe as SushiRecipe):recipe.effectsText??"Effekte nicht übermittelt"} · {recipe.category === "Offensive" ? "Offensiv" : recipe.category==="Defensive"?"Defensiv":"Slot nicht übermittelt"} · {recipe.durationSeconds===3600?"1 Stunde":format(recipe.bossUses??Number.NaN)+" Bosse"}
                        </p>
                        <div className="recipe-ingredients">
                          {recipe.ingredients.map(i => <p key={i.fishId}><ItemArtwork id={`fish-${i.fishId}`} name={i.name} fallback="🐟" small/> {i.name} <strong>{state.fish[i.fishId] ?? 0} / {i.quantity}</strong></p>)}
                          <p>
                            ◉ Kosten{" "}
                            <strong>
                              {format(recipe.pearlCost)}{" "}
                              {isDemo?"Demo-Perlen":"Perlen"}
                            </strong>
                          </p>
                        </div>
                        <p>
                          Vorrat:{" "}
                          <strong>{state.sushi[recipe.id] ?? 0} Sushi</strong>
                        </p>
                        {activeBuff && (<p className="active-bonus">
                            ✓ Aktiv · {activeBuff.expiresAt!==undefined?time(remaining(activeBuff.expiresAt,now))+" verbleibend":activeBuff.remainingUses+" neue Bosse übrig"}
                          </p>)}
                        <div className="recipe-actions">
                          <button className="primary" disabled={!allowed("cook") || busy || (busy || !canCook)} onClick={() => void run({ type: "cook", recipeId: recipe.id })}>
                            Sushi herstellen
                          </button>
                          <button className="primary secondary" disabled={!allowed("activateSushi") || busy || (busy ||
                            (state.sushi[recipe.id] ?? 0) < 1 ||
                            !!activeBuff ||
                            atBuffLimit)} onClick={() => void run({
                            type: "activateSushi",
                            recipeId: recipe.id,
                        })}>
                            {activeBuff ? "Buff aktiv" : "Sushi essen"}
                          </button>
                        </div>
                        {!canCook && (<small>
                            Zum Kochen fehlen Fische oder {isDemo?"Demo-Perlen":"Perlen"}.
                          </small>)}
                        {!activeBuff && atBuffLimit && (<small>
                            Dieser Sushi-Slot ist bereits belegt.
                          </small>)}
                      </article>);
                })}
                </div>
                <p className="muted">
                  {isDemo?"Demo-Startervorrat: je sechs Fische von zwölf Arten. Neuladen setzt die Demo zurück.":"Fische und Sushi stammen aus deinem gespeicherten Profil. Neuladen setzt sie nicht zurück."}
                </p>
              </>)}
          </>)}
        <p role="status" className="feedback">
          {message}
        </p>
      </main>
      <dialog ref={upgradeDialog} aria-labelledby="upgrade-heading" onCancel={()=>setUpgradeItem(null)}>
        <h2 id="upgrade-heading" tabIndex={-1}>Ausrüstung aufwerten</h2>
        <EquipmentUpgrade item={upgrading} rules={state.remote?.itemUpgrade} stock={state.materials} points={state.remote?.pointsExact} busy={busy} allowed={allowed} onSubmit={a=>void run(a)}/>
        <p role="status">{message}</p>
        <button onClick={()=>setUpgradeItem(null)}>Schließen</button>
      </dialog>
      <dialog ref={dialog} onCancel={() => void run({ type: "cancelSalvage" })}><h2>Zerlegen bestätigen</h2>{state.salvageQuote && <><p>Gültig: {remaining(state.salvageQuote.expiresAt, now)} Sekunden</p><ul>{state.salvageQuote.items.map(i => <li key={i.id}>{i.name} · {i.value} Perlen {i.warning ? "· Set / Legendär" : ""}</li>)}</ul><strong>Gesamt: {state.salvageQuote.total} Perlen</strong><div className="actions"><button onClick={() => void run({ type: "confirmSalvage", token: state.salvageQuote!.token })} disabled={!allowed("confirmSalvage") || busy}>Jetzt zerlegen</button><button onClick={() => void run({ type: "cancelSalvage" })} disabled={!allowed("cancelSalvage") || busy}>Abbrechen</button></div></>}</dialog>
      <footer>
        {!isDemo&&state.remote?.selfDeletionEnabled===true&&<SelfDeletion source={source}/>}
        <label>
          <input type="checkbox" checked={!motion} onChange={(e) => setMotion(!e.target.checked)}/>{" "}
          Animationen aus
        </label>
        {isDemo&&!config && (<button onClick={() => void run({ type: "reset" })} disabled={!allowed("reset") || busy}>
            Demo zurücksetzen
          </button>)}
        <p>{isDemo?"Nur Mockdaten · RPG v2.1.21 · keine echten Spielstände":"Gespeicherte Spielstände · Aktionen werden im RaidClipPlugin geprüft"}</p>
      </footer>
    </div>);
}
