# Funjii Otter RPG – lokaler Test auf Basis von v2.1.21

Der lokale Extension-Adapter verwendet jetzt die RPG-Defaults von **v2.1.21**, Commit `48ac7ad1445285a1b560257ac33e2aa5f9d7ce77`. `main` stand beim Abgleich weiterhin auf `5f8a92305dc8c48b3849bb97b58c9bc0bcaf12d6`; deshalb wurde wie vereinbart der Release-Tag verwendet. Die Twitch-Extension-Version bleibt 0.0.1.

**Dies ist weiterhin eine flüchtige Demo.** Es werden keine echten Profile gelesen, keine Plugin-Dateien verändert und keine Aktionen im laufenden Plugin ausgeführt. Die sichere Verbindung für echte Daten und Aktionen ist noch nicht vorhanden. Neuladen setzt den Teststand zurück. Panel, Mobilansicht und Konfiguration sind getrennte Sitzungen.

## Enthalten

- 83 Itemdefinitionen mit Attributen, Effekten und Sammlerboni; vier originale Sets, acht Ausrüstungsslots und sieben Perks.
- Kraft/Ausdauer, HP aus Level und Ausrüstung, passive Heilung und Chat-Heilung. Ausrüsten heilt nicht. Eine Niederlage sperrt denselben Boss auch nach Heilung.
- Bossangriffe mit zehn Sekunden Cooldown, Gegenschaden nach Bossgröße, Krits, Zusatzangriffen, Boss-Verteidigung, Lebensraub, EXP und Leveln.
- Tauchen mit Einsatzstufen, relativer Hot-Time-Chance, Seltenheitsgrenzen und kleinen Unterwassergegnern. Angeln mit zwölf Fischarten, möglichen Fehlfängen und 30 Sekunden Cooldown.
- Acht Sushi-Rezepte einschließlich Kappa, Tempura, Kugelfisch und Vampir-Maki. Je ein offensiver und defensiver Slot; Verbrauch einmal je unterschiedlichem Boss, Laufzeit bis zum Ende des dritten Bosses.
- Zerlegen mit stabilen Werten nach Seltenheit, Vorschau und ausdrücklicher Bestätigung. Favorisierte, gesperrte und ausgerüstete Items sind geschützt. Stapelzerlegen überspringt Sets und Legendäre. Zerlegte Tauchbeute verschwindet aus der Lootanzeige.
- Gestaltung „Leuchtende Tiefe“, Kochen-Reiter und brennender Hot-Time-Balken am oberen Rand bleiben enthalten.

## Lokal starten

Im Ordner `twitch-extension`, mit Node ab 22.12 und pnpm:

```powershell
pnpm.cmd install --frozen-lockfile
pnpm.cmd start
```

Anschließend `https://localhost:8080/panel.html` öffnen. Weitere Ansichten: `mobile.html` und `config.html`. Der Server bindet ausschließlich localhost. Er erzeugt sein selbstsigniertes Testzertifikat unter `.certs/`; Browser müssen diesem für iframe-Tests vertrauen. Kein öffentliches Hosting oder Tunnel ist für die direkte Vorschau nötig. Browser können trotz Zertifikatsfreigabe den lokalen Netzwerkzugriff aus einem Twitch-iframe blockieren; dafür ist der bereits verwendete geschlossene Hosted Test geeignet.

Zum Testen starten wir absichtlich mit 25.000 Demo-Perlen, allen 83 Musteritems, sechs Fischen je Art, einem ausgerüsteten Wächter-Set und fünf Minuten Hot Time. Das sind Testvorräte, keine neuen Produktions-Startwerte. Die Muster-Rolls sind reproduzierbar; neue Beute wird gewürfelt. Unter Charakter können Verletzung und Chat-Heilung simuliert werden. Unter Boss kann ein neuer Demo-Boss ohne Gratisheilung gestartet werden. Hot-Time-Regler befinden sich auf `config.html`.

## Geschlossenen Twitch-Test aktualisieren

Das ZIP enthält die **Inhalte** von `dist` direkt im Archiv, ohne übergeordneten Ordner. In der bestehenden Extension-Version: zum lokalen Test wechseln, unter Dateien das neue ZIP hochladen und wieder zum gehosteten Test wechseln. Pfade `panel.html`, `mobile.html`, `config.html` und Panelhöhe 500 beibehalten. Danach den Kanal neu laden. Dieses Update wurde nicht automatisch hochgeladen, zur Prüfung eingereicht oder veröffentlicht.

## Quellen und Grenzen

`scripts/export-reference.py` exportiert die reinen Config-/Katalogklassen und deterministische Muster-Rolls aus dem gepinnten Git-Commit mit .NET 10 nach `src/reference/v2.1.21.json`. Es liest keine Spielstände. Für das normale Frontend genügt die eingecheckte JSON; .NET ist nur zum erneuten Export notwendig. Quellen sind insbesondere RpgConfig, RpgLootConfig, Health/Enemy/HotTime/Salvage-Config, Equipment/HealthEquipment/LootExpansion-Kataloge und Sushi-Defaults.

`src/mock.ts` bildet diese Regeln für eine einzelne lokale Sitzung in TypeScript nach. Dies ist keine Ausführung der produktiven C#-Dienste: keine Mehrspieler-Bossverteilung, Chat-Events oder persistente Transaktionen. Die Daten sind unveränderte Release-Defaults; benutzerspezifische Plugin-Konfigurationen werden nicht geladen. Der angezeigte Chat-Heilungsbutton simuliert eine Nachricht.

Der statische HTTPS-Server bietet keine Spiel-API. Die CSP erlaubt keine API-Verbindungen (`connect-src 'none'`). Der Twitch-Helper lädt zuerst, aber der Adapter liest oder speichert kein JWT. Eine spätere echte Anbindung muss JWT, Kanal und Identität serverseitig prüfen und vorhandene Plugin-Dienste für autorisierte Aktionen verwenden; niemals Spielstände direkt aus dem Browser bearbeiten.

## Prüfen

```powershell
pnpm.cmd build
pnpm.cmd test
pnpm.cmd test:e2e
```

Die Browser-Suite benötigt Playwright Chromium und startet ihren eigenen Server auf 8080. Einen manuell gestarteten Vorschau-Server vorher schließen. Ergebnisse und tatsächliche Grenzen stehen in `TEST-RESULTS.md`.

## Neu: Perlen-Minispiele

Heist, Gamble, Münzwurf, Slots, Roulette, Rennen und Duell sind im neuen Hauptreiter zusammengefasst. Die bestehenden RPG-Ansichten bleiben erhalten. Der Heist entspricht der vorhandenen Form ohne persönlichen Einsatz; ein Startbutton wird nur mit Moderator-/Broadcaster-Berechtigung angeboten. Lokale Teststeuerung und feste Testantworten sind deutlich als Demo gekennzeichnet.

Verträge, Realtime-/Reconnect-Verhalten, fehlende Backend-Funktionen und lokale Testanleitung: [MINIGAMES-INTEGRATION.md](MINIGAMES-INTEGRATION.md). Der neue Reiter nutzt dasselbe flüchtige Demo-Perlenkonto, aber keine produktiven Plugin-Dienste. Es gibt weiterhin keinen öffentlich erreichbaren Live-Server.

## Eigene Itemgrafiken

Die 102 vom Nutzer gelieferten Motive aus dem Bildpaket 2.1.22 sind über stabile Item-IDs zugeordnet: Inventar, Ausrüstung, Lootanzeige, gefangene Fische, Sushi und Rezeptzutaten. Transparenz bleibt erhalten; alle Bilder werden lokal im Extension-Paket ausgeliefert. Neue oder unbekannte IDs zeigen weiter ein Ersatzsymbol. Details und reproduzierbarer Import stehen in `ART-SOURCES.md`.
