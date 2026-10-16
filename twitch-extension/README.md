# Funjii Otter RPG · Phase 1 (0.0.1)

Eigenständiges React/TypeScript-Frontend für das **C#/.NET-Projekt RaidClipPlugin**. Kein Streamer.bot-Modul. Nur eine flüchtige Demo, kein Backend, keine Verbindung zu Spielständen. Ein Neuladen setzt den jeweiligen Tab zurück. Panel, Mobil und Konfiguration haben getrennte Demo-Sitzungen.

## Voraussetzungen unter Windows

1. [Node.js](https://nodejs.org/) installieren: aktuelle unterstützte LTS-Version, mindestens 22.12; Node und npm bei der Installation auswählen. Danach ein neues Terminal öffnen.
2. Repository herunterladen (GitHub → Code → Download ZIP und entpacken) oder mit Git klonen. Für diese Änderung den PR-Branch `codex/funjii-otter-extension-phase1` auswählen.
3. Im Explorer den Ordner `twitch-extension` öffnen. In die Adresszeile `powershell` eingeben und Enter drücken. Alle folgenden Befehle werden in diesem Ordner ausgeführt.
4. Paketverwaltung einmal installieren: `npm.cmd install --global pnpm@11.25.0`.
5. Prüfen: `node --version` und `pnpm.cmd --version`. Bei „nicht gefunden“ Terminal neu öffnen und Node-Installation prüfen.

## Installation und Start

```powershell
pnpm.cmd install --frozen-lockfile
pnpm.cmd start
```

`start` prüft TypeScript, baut die drei Seiten und startet den ausschließlich an localhost gebundenen HTTPS-Server auf Port 8080. Terminal geöffnet lassen. Stoppen mit Strg+C. Nach Änderungen stoppen und erneut starten. `pnpm.cmd dev` ist nur eine alternative schnelle Vite-Vorschau über HTTP; für Twitch immer `start` verwenden.

Aufrufen:

- https://localhost:8080/panel.html
- https://localhost:8080/mobile.html
- https://localhost:8080/config.html

`https://localhost:8080/` öffnet ebenfalls das Panel. Port 8080 muss frei sein. Bei EADDRINUSE den anderen lokalen Server beenden. Keine Firewallfreigabe, kein Tunnel und kein öffentliches Hosting nötig.

### Lokales Zertifikat vertrauen

Beim ersten Start erzeugt der Server `.certs/localhost.pem` und `.certs/localhost-key.pem` mit localhost/127.0.0.1 als Namen. Zertifikate und private Schlüssel werden nicht eingecheckt. Ein Browser muss dem Zertifikat vertrauen, bevor Twitch es in einem iframe laden kann. Nur für dieses selbst erzeugte lokale Zertifikat:

1. Nach dem ersten Start in einem zweiten PowerShell-Fenster im selben Ordner ausführen:

```powershell
certutil.exe -user -addstore Root .certs\localhost.pem
```

2. Browser vollständig schließen und neu öffnen. Erst alle drei URLs direkt öffnen. Sie müssen ohne Zertifikatsfehler laden. Das bloße Wegklicken einer Warnung reicht für iframe-Tests unter Umständen nicht aus. Firefox nutzt abhängig von seinen Einstellungen einen eigenen Zertifikatsspeicher; gegebenenfalls das Zertifikat dort manuell importieren oder Edge/Chrome verwenden.
3. Das Zertifikat läuft nach 365 Tagen ab. Dann Server stoppen, die beiden Dateien in `.certs` entfernen, neu starten und das neue Zertifikat wieder vertrauen.
4. Zum späteren Entfernen `certmgr.msc` öffnen → Vertrauenswürdige Stammzertifizierungsstellen → Zertifikate. Das lokale, selbst erzeugte Zertifikat mit Aussteller/Name `localhost` anhand seiner Details identifizieren und nur dieses entfernen. Private Schlüssel niemals teilen.

## Twitch Local Test auf dem eigenen Kanal

Die folgenden Schritte sind manuell; dieser Auftrag verändert keine Developer-Console-Einstellungen und veröffentlicht nichts.

1. [Developer Console](https://dev.twitch.tv/console/extensions) öffnen → **Funjii Otter RPG**, Version **0.0.1**. Status **Local Test** beibehalten.
2. Unter Asset Hosting / Test-Einstellungen die Testing Base URI auf `https://localhost:8080/` setzen (abschließenden Schrägstrich beachten).
3. Panel Viewer Path `panel.html`, Mobile Viewer Path `mobile.html`, Config Path `config.html`. Panelhöhe **500**. Panel- und Mobile-Unterstützung aktivieren.
4. Keine Secrets in Dateien eintragen. Die Demo benötigt weder Identitätsfreigabe noch Bits, Chat-Schreibrechte, externe APIs oder den Twitch Configuration Service. Keine erforderliche Konfiguration festlegen; config.html enthält nur sitzungslokale Demoregler.
5. Das eigene Twitch-Konto in den Test-Accounts zulassen, falls die Console dies verlangt. „View on Twitch and Install“ verwenden. Im Creator Dashboard unter Extensions → My Extensions das Panel installieren/aktivieren und einem Panelplatz zuweisen. Die Bezeichnungen können je nach Console-Version abweichen.
6. Lokalen Server laufen lassen und die Kanalseite auf **demselben Windows-PC** öffnen. Ggf. Browsercache neu laden. Das sichtbare DEMO-Schild muss erscheinen. Unter dem Stream zum Panel scrollen.
7. Alle acht Bereiche durchklicken. Tauchen und Angeln starten, fünf Sekunden Cooldown abwarten. Favorit schützen, Ausrüstung ablegen, Item zerlegen, Perk kaufen, Boss angreifen. Unter Hot Time die letzten 30 Sekunden ausprobieren. Neuladen muss alles zurücksetzen.
8. Nicht auf Hosted Test, Submit for Review oder Release wechseln. Keine Assets hochladen. Ein ZIP/Deployment ist hier nicht vorgesehen.

**Mobil:** `mobile.html` auf dem PC in der Geräteansicht des Browsers testen. Auf einem echten Telefon bedeutet `localhost` das Telefon, nicht diesen PC. Mit der ausdrücklich lokalen Adresse und dem nur lokal gebundenen Server ist daher kein echter Twitch-App-Test auf einem separaten Telefon möglich. Dafür wäre später ein separat autorisierter, erreichbar gehosteter HTTPS-Test mit passendem vertrauenswürdigem Zertifikat nötig. Die echten Twitch-Apps auf iOS/Android und das echte Twitch-iframe wurden hier nicht getestet.

## Funktionen und Struktur

```text
twitch-extension/
  panel.html / mobile.html / config.html  Twitch-Helper jeweils erstes Skript
  src/types.ts                            sichere DTOs und austauschbarer Datenvertrag
  src/data.ts                             Snapshot der C#-Defaults, deutsche Beschreibungen
  src/mock.ts                             In-Memory-Zustand, Aktionen und Berechnungen
  src/main.tsx                            React-Ansichten und Benutzerinteraktion
  src/style.css                           Fantasy-Meer, Seltenheiten, reduzierte Bewegung
  public/otter.svg                         lokaler Demo-Avatar, keine externe Bildabfrage
  scripts/serve.mjs                        statischer HTTPS-Server, kein Spiel-API
  tests/                                  Logik- und Browserprüfungen
  pnpm-lock.yaml                          reproduzierbare Abhängigkeiten
```

Charakterwerte, acht Slots, Inventarkarten mit Attributen und Effekten, Favorisieren, Aus-/Ablegen einschließlich Ring2, geschütztes Zerlegen, zwei Setkonzepte mit 2/3/4-Teile-Fortschritt, sieben Perks mit Kosten und Maximallevel, Tauch-Einsatzstufen, Beute-Enthüllung, Fanganimation, Boss-HP/Schaden/Respawn und Hot Time. Graue/grüne/blaue/lila/goldene Raritäten; Legendary mit zusätzlichem Goldschein. Animationen per Checkbox und Betriebssystemeinstellung abschaltbar. Die Navigation bleibt beim Scrollen erreichbar; Inhalt scrollt innerhalb des 500-Pixel-iframes.

## Quellenabgleich und bewusste Mockannahmen

Referenz: `main` bei `5f8a92305dc8c48b3849bb97b58c9bc0bcaf12d6`.

- `RaidClipPlugin/Config/RpgLootConfig.cs`: alle 13 Item-IDs, Namen, Raritäten, Slots und Attribute übernommen. Die Ringe tragen im C#-Default zwar keinen expliziten Equipment-Kind, aber einen Slot; wie im vorhandenen Equip-Service ist der Slot entscheidend. Keine erfundenen Spezialeffekte: vorhandene Defaults besitzen keine. Die UI unterstützt strukturierte ItemEffect-Daten für spätere Definitionen.
- `RaidClipPlugin/Models/RpgProfile.cs`: acht Slots und Attributnamen als Grundlage.
- `RaidClipPlugin/Config/RpgConfig.cs`: sieben Perk-IDs mit denselben Boni, fünf Leveln, Kosten 500 × 2^Level. Deutsche Anzeigenamen sind UI-Texte.
- **Wächter der Tiefsee und Schatzjäger des Ozeans existieren in diesem Stand nicht als C#-Setdefinitionen.** Zuordnung und 2/3/4-Boni sind gekennzeichnete Demokonzepte. Keine Rückschreibung ins Plugin.
- Fischfang ist bewusst ein fester Kristallkarpfen. Anfangsprofil, Avatar, Boss, Fünfsekunden-Cooldowns, 25-Perlen-Zerlegewert und Belohnungen (150 Tauchen / 80 Angeln vor Hot Time) sind UI-Mockwerte, keine neue persistente Perlenökonomie.
- Tauchen nutzt Einsatzschwellen und Seltenheitsgewichte aus den Defaults. Demo-Lootchance berücksichtigt Basis 25 %, Einsatzbonus und Luck (gedeckelt); Hot Time multipliziert relativ mit 1,5. Das ist keine vollständige Nachbildung aller produktiven Dienste/Regeln. Hot Time verdoppelt nur Tauch-/Angelbelohnungen, keine Einsätze/Zerlegewerte. Der Boss vergibt in der Demo keine Perlen.

## Sicherheitsgrenze für Phase 2

Keine fetch-, WebSocket-, Storage- oder Dateioperationen im Spieladapter. Der Server liefert nur statische Dateien und erstellt sein lokales TLS-Zertifikat. Das C#-Projekt wird weder referenziert noch verändert. Strenge lokale CSP: connect-src 'none'; Skripte nur lokal und vom offiziellen Twitch-Helper; keine Inline-Skripte, kein eval, keine externen Fonts.

`RpgDataSource` trennt Intent-Aktionen und Snapshots von der Darstellung. Phase 2 benötigt einen eigenen serverseitigen Adapter: aktuellstes Helper-JWT verwenden; Signatur, Ablauf, Kanal, Rolle und Identität **serverseitig** prüfen. Ein Frontend-Benutzername ist keine vertrauenswürdige Identität. Einsätze, Besitz, Inventar, Kosten, Cooldowns, Belohnungen und idempotente Transaktionen muss der vorhandene RPG-Server autorisieren. Secrets ausschließlich serverseitig. Die Demo liest/verwendet/speichert kein Helper-Token. Ein Helper-Callback zeigt lediglich den Integrationsstatus; er gibt keine Spielrechte und ersetzt kein Profil. Der Demo-Otter ist explizit ein Platzhalter für den später serverseitig gelieferten Twitch-Avatar.

## Aktuell geprüfte Twitch-Anforderungen (16.10.2026)

- [Building Extensions](https://dev.twitch.tv/docs/extensions/building/): Helper in allen HTML-Ansichten, getrennte mobile Einstiegspfade, JWT-Verifikation im späteren EBS, Tests auf echten mobilen Geräten erforderlich.
- [Guidelines & Policies](https://dev.twitch.tv/docs/extensions/guidelines-and-policies/): Helper als erstes Skript. Lokale Icons statt externer Bilddienste.
- [Life Cycle / CSP / Local Test](https://dev.twitch.tv/docs/extensions/life-cycle/): Test-Accounts und iframe/CSP beachten. Der gebaute statische Server vermeidet die Inline-Skripte und WebSockets des Vite-Entwicklungsclients. Twitch kann zusätzliche iframe-Richtlinien anwenden; lokale CSP-Tests ersetzen den echten Kanaltest nicht.

## Tests erneut ausführen

```powershell
pnpm.cmd build
pnpm.cmd test
pnpm.cmd exec playwright install chromium
pnpm.cmd test:e2e
```

Vor Browserprüfungen einen manuell gestarteten Server mit Strg+C stoppen: Die Suite startet ihren eigenen Server auf 8080. Sie ignoriert das lokale Zertifikat ausschließlich in ihrem Testbrowser und ersetzt den Helper durch einen Stub. Kein Twitch-Konto wird kontaktiert. Screenshots landen in `test-results/` (nicht eingecheckt). Ergebnisse und Grenzen siehe [TEST-RESULTS.md](TEST-RESULTS.md).
