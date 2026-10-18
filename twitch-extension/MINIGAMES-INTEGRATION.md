# Perlen-Minispiele: Extension und Backend-Vertrag

## Umfang

Neunter Hauptreiter **Perlen-Minispiele** mit Otter-Heist, Gamble, Münzwurf, Slots, Roulette, Otter-Rennen und Duell. Charakter, Inventar, Ausrüstung, Sets, Perks, Boss, Kochen, Tauchen/Angeln und Hot Time bleiben erhalten. Darstellung bei 320 und 390 Pixel Breite geprüft. Der Heist verwendet selbst erstellte austauschbare SVG-Otter, Schatzkammer, Blasen und Perleneffekte. Reduzierte Bewegung wird respektiert.

Die aktuelle Fassung startet ausdrücklich mit `MockMinigameClient`. Der Demo-Ausgang bleibt geskriptet; es gibt keine zweite produktive Spiellogik und keine zufällig ermittelten Gewinner. Der Adapter skaliert seine Auszahlungsantworten jetzt mit Einsatz und den exportierten Plugin-Standardmultiplikatoren. React zeigt ausschließlich diese Antwortfelder an.

Münzwurf: 2×; Gamble: 0×/0,5×/1×/2×; Slots: zwei gleiche 1,5×, drei gleiche 5×, drei Siebenen 10×; Roulette: einfache Wetten 2×, Einzelzahl 36×. Die Auszahlung enthält den Einsatz. Ein 1.000er-Einsatz bei 2× ergibt 2.000 Auszahlung und +1.000 netto. Teilrückzahlungen erscheinen mit tatsächlichem Nettoverlust, eine 1×-Rückzahlung als „Einsatz zurück“. Der Heist hat keinen persönlichen Einsatz und zeigt im Demo-Erfolg einen Anteil seines Jackpot-Fixtures; Rennen verwenden den gemeinsamen Topf, Duelle beide Einsätze. Die Teststeuerung bietet zusätzlich Teilrückzahlung, Einsatzrückgabe, Slot-Paar und drei Siebenen.

Die Werte sind unveränderte Release-Defaults, keine eingelesene Benutzerkonfiguration. Hot-Time-/Jackpot-Beiträge und Kontolimits werden im Minigame-Testadapter nicht zusätzlich nachgebildet. Solche produktiven Regeln muss später das Backend ausführen. Die gemeinsame flüchtige Demo-Wallet rechnet jede Antwort serialisiert und idempotent ab. Neuladen setzt die Seite zurück.

## Tatsächlicher Quellenabgleich

Referenz v2.1.21, Commit `48ac7ad1445285a1b560257ac33e2aa5f9d7ce77`.

- `HeistService`/`HeistConfig`: persönlicher Einsatz **0**; gemeinsamer Jackpot; Start nur Moderator/Broadcaster; Viewer dürfen beitreten. Kein erfundener Heist-Einsatzregler. Ein Startbutton erscheint nur bei entsprechender Adapter-Berechtigung.
- `ChatMinigameService`/`MinigameConfig`: Gamble, Coinflip, Slots und Roulette. Auswahlen werden als Intent übertragen, kein Würfeln im React-Frontend. Normale Einsatzgrenzen und Cooldowns aus den Release-Defaults.
- `AdventureGamesConfig`: Rennen mit gemeinsamem Einsatz und Abrechnung beim Rennstart; mindestens zwei Teilnehmer. Die Extension startet keine eigene Rennlogik. Die Demo bietet eine ausdrücklich markierte Renn-Anmeldung als Fixture.
- `DuelConfig`/`DuelService`: Herausforderung an Twitch-Login, Einladung annehmen/ablehnen; serverseitige Identitäts- und Besitzprüfung bleiben erforderlich.
- `scripts/export-minigame-defaults.py` reproduziert die skalaren Regeln aus dem gepinnten Git-Commit nach `src/minigames/defaults.json`. Die JSON enthält Defaults, keine Benutzerkonfiguration oder Secrets. Casino, Heist und Duell sind produktiv teilweise standardmäßig deaktiviert; **nur der Demo-Adapter** schaltet ihre Oberflächen zum Testen frei. Im Livebetrieb gelten ausschließlich die Backend-Capabilities.

## Dateien

| Datei | Aufgabe |
|---|---|
| `src/minigames/contracts.ts` | Status, Regeln, Teilnehmer, Ergebnisse, Join-/Start-Anfragen, Live-Ereignisse und Adaptervertrag |
| `src/minigames/PerlMinigames.tsx` | Spielauswahl, Einsatz/Auswahl, Bestätigung, Countdown, Animationen, Ergebnis und Demo-Steuerung |
| `src/minigames/controller.ts` | Snapshot/Subscription, Wiederverbindung, Versionsprüfung, Sperre paralleler Aktionen, Wiederholung derselben Anfrage |
| `src/minigames/mock-client.ts` | Ausschließlich lokale, geskriptete Integrations-Fixtures |
| `src/minigames/backend-client.ts` | Austauschbare Schnittstelle zu einem künftig authentifizierten Backend, ohne erfundene HTTP-Endpunkte |
| `src/minigames/rules.ts`, `defaults.json` | Abgeglichene Default-Regeln und gekennzeichnete Demo-Freischaltung |
| `src/main.tsx`, `src/mock.ts`, `src/style.css` | Navigation, gemeinsames Demo-Konto und Darstellung |
| `tests/minigames.test.ts`, `tests/minigames.spec.ts` | Adapter-/Controller- und Browserprüfungen |

## Benötigte Backend-Funktionen für das separate Plugin-Projekt

Es existiert derzeit **kein öffentliches Extension-Backend**. Der neue `BackendMinigameClient` ist ein Transportadapter, kein laufender Server und keine öffentliche API. Er wird noch nicht durch die Anwendung aktiviert. Die bestehende CSP `connect-src 'none'` bleibt unverändert. Keine Verbindung zu localhost, Plugin-Dateien oder OBS aus dem Zuschauerbrowser.

`AuthenticatedMinigameTransport` benötigt folgende Operationen; Namen sind Schnittstellenmethoden und **keine vorhandenen URL-Pfade**:

1. `rules(token)`: aktivierte Spiele, zulässige Aktionen/Berechtigungen, aktuelle Einsatzgrenzen, maximale Teilnehmer, Commands und Cooldowns liefern.
2. `snapshot(token)`: aktuellen Kanalstatus einschließlich persönlicher Teilnahme und Ergebnis sowie Kontostand liefern.
3. `execute(token, request)`: Start, Join, Play, Challenge, Accept oder Deny autoritativ über vorhandene Plugin-Dienste ausführen.
4. `subscribe(currentToken, onEvent, onDisconnect)`: Live-Ereignisse liefern; nach Wiederverbindung vollständigen aktuellen Snapshot abrufen.

Für die konkret angefragten Heist-Operationen stehen `HeistStatus`, `HeistParticipant`, `HeistRules`, `HeistJoinRequest`, `HeistJoinResult`, `HeistStartRequest`, `HeistStartResult`, `HeistResult` und `HeistEvent` bereit. Der aktuelle Zustand, die Regeln, die persönliche Teilnahme und Ergebnisse werden aus dem Snapshot gelesen. Aktionen dürfen nie aus einer UI-Animation abgeleitet werden.

### Identität und Transaktionen

Das Backend muss das aktuelle Twitch-Helper-JWT prüfen: Signatur, Ablauf, Kanal, Rolle und freigegebene Nutzeridentität. Es darf weder einen eingegebenen Login noch eine Frontend-ID als handelnden Nutzer akzeptieren. Der Request enthält bewusst keine vertrauenswürdige User-ID. Der Duell-Login bezeichnet nur den Gegner und muss serverseitig aufgelöst werden. Ein vorhandenes Token im Frontend ist kein Identitätsnachweis; `BackendMinigameClient` prüft nur dessen Anwesenheit und übergibt es unverändert an den Transport.

Einsatzgrenzen, Eigentum, Guthaben, Cooldowns, Rollen, aktive Runde, Beitrittsende und Doppelteilnahme müssen nochmals serverseitig geprüft werden. Anfrage-ID und Payload-Fingerprint dauerhaft bzw. für ein ausreichend langes Retry-Fenster speichern. Wiederholungen liefern dieselbe Antwort und dürfen keine erneute Transaktion auslösen. Abrechnung, Ergebnis und Event-Publikation müssen zusammen konsistent sein. Die Demo-ID-Claims sind kein Ersatz für diese produktive Transaktionssicherheit.

### Ereignisse, OBS und Versionen

- Der Backend-Stream besitzt `streamId` und streng monoton steigende `revision`.
- Jeder Spielstatus trägt dieselbe `eventId`, die das Plugin/OBS verwendet, und eine innerhalb desselben Streams pro Spiel monoton steigende `version` (auch über Rundenwechsel hinweg).
- Fremde Streams und ältere Revisionen/Spielversionen werden bei Live-Push ignoriert. Ein neuer Stream darf nur durch einen explizit neu geladenen Snapshot nach dem Handshake übernommen werden.
- `serverTime`, `startedAt`, `joinEndsAt`, `phaseEndsAt` und `cooldownEndsAt` sind absolute UTC-Zeitpunkte in Millisekunden. Die Ansicht korrigiert ihren Countdown anhand der Serverzeit; sie verlängert keine Anmeldung und entscheidet keine Ergebnisse.
- Ein Spielstatus mit vergangener Anmeldezeit darf keine aktive Teilnahme freigeben. Nach Neuverbindung aktuelle Phase liefern, keine alte Anmeldung erneut abspielen.
- Teilnehmer für die öffentliche Ansicht nur pseudonymisiert bzw. mit freigegebenem Anzeigenamen liefern. Private IDs/Ergebnisse nicht an andere Zuschauer verteilen. Das persönliche Ergebnis wird serverseitig anhand der verifizierten Sitzung ausgewählt.
- OBS und Extension beziehen ihre Daten vom selben autoritativen Heist. Die Extension greift nicht auf OBS-WebSocket zu und enthält keine OBS-Zugangsdaten.
- Chat-Ankündigungen bleiben ausschließlich im Plugin. Die Extension sendet keine eigenen Chatmeldungen.

### Verbindungsverlust

Die Ansicht sperrt neue Aktionen bei Offline-, Anmelde- oder Verbindungsfehlern. Es gibt keine optimistischen Perlenabbuchungen. Bei unklarem Ausgang bleibt dieselbe Anfrage-ID erhalten. Nach neuem Snapshot kann „Dieselbe Anfrage prüfen“ genau diese Anfrage erneut senden; es wird kein zweiter Beitritt erstellt. Verlorene Anfragen werden nicht automatisch als neue Aktionen wiederholt.

## Lokal testen

```powershell
pnpm.cmd build
pnpm.cmd start
```

`https://localhost:8080/panel.html` oder `mobile.html` öffnen → **Perlen-Minispiele**.

1. **Demo-Teststeuerung** aufklappen → **Heist-Anmeldung öffnen**. Als Zuschauer ist kein Startbutton vorhanden.
2. **Dem Heist beitreten** → Bestätigen. Persönlicher Einsatz bleibt 0; Konto wird nicht belastet.
3. Testteilnehmer hinzufügen. **Nächste Testphase** schaltet zur vorgegebenen Animation und anschließend zum vorgegebenen Ergebnis.
4. Vor der Ergebnisphase Erfolg oder Fehlschlag auswählen. Kein echtes Konto wird berührt.
5. **Moderator testen** schaltet die administrative Start-Capability frei. Ein laufender Heist kann nicht nochmals geöffnet werden.
6. Münzwurf, Slots, Roulette oder Gamble auswählen, Einsatz/Auswahl angeben und bestätigen. Der Adapter liefert nach drei Sekunden seinen geskripteten Ausgang mit einsatzabhängiger Auszahlung. Es werden keine echten Gewinnchancen simuliert.
7. Renn-Anmeldung oder Duell-Einladung simulieren. Rennen nutzen 100 Demo-Perlen je Teilnehmer; der Einsatz wird erst bei der Fixture-Abrechnung abgezogen.
8. Verbindung trennen und wiederherstellen; Teilnahme bleibt im Adapter erhalten. Offline- und Anmeldefehler separat testen.

Die neu gebaute ZIP ist für den geschlossenen Twitch-Hosted-Test vorbereitet. Bestehende Pfade bleiben `panel.html`, `mobile.html`, `config.html`; Höhe 500. Kein automatischer Upload, keine Einreichung und keine Veröffentlichung. Auch auf Twitch handelt es sich weiter um Mockdaten.

## Grenzen

Keine produktive Spiellogik, keine echten Perlen, keine echten Multiplayer-Events, keine persistente Sitzung über Seitenneuladen und kein fertiger öffentlicher EBS. Die bereits begonnene separate C#-/OBS-Arbeit ist nicht Bestandteil dieses Extension-Pakets. Für Livebetrieb fehlen Backend-Hosting, sichere Authentifizierung, autoritative Plugin-Befehle, Datenschutzfilter und ein gemeinsamer Ereignisstrom. Ein C#-Release-Build ist für diese ausschließlich auf die Extension begrenzte Änderung nicht erfolgt.
