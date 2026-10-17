# Animiertes Heist-Overlay und Community-Anzeige

Release 2.1.22, Veröffentlichung separat vom Benutzer beauftragt. Keine zweite Heist-Ökonomie, keine Streamer.bot-Abhängigkeit und keine öffentliche Extension-API.

## Tatsächliche Spielregeln

Der bestehende Heist verteilt **echte Punkte aus dem Jackpot**. Es gibt bisher keinen individuellen Heist-Einsatz. Ein erfolgreicher Heist verteilt den gesamten Jackpot auf die tatsächlichen Teilnehmer einschließlich der bestehenden Restpunktverteilung; die Anzeige verwendet die bestätigten Auszahlungen des vorhandenen `ViewerPointStore`. Bei Fehlschlag bleibt der Jackpot erhalten. Individueller Verlust ist deshalb korrekt **0**, nicht ein erfundener Einsatzverlust. Der bestehende konfigurierte Jackpot-Reset bleibt erhalten.

Die Beitrittsdauer, Teilnehmer, Berechtigungen, Erfolgschance und Chattexte stammen weiterhin aus `HeistConfig`/`HeistService`. Es wird kein zweiter Zufallswurf für die Animation verwendet. Die Auswertung und Punktevergabe warten nicht auf Browser, OBS oder Animationen. Ein fehlerhafter Overlay-Abonnent wird isoliert und kann keine zweite Auszahlung auslösen. Das feste Beitrittsende wird serverseitig geprüft; verspätete Beitritte werden nicht mehr während eines verzögerten Countdown-Ticks angenommen.

## Neue Darstellung

- Transparente OBS-Seite mit dunkelblauer/türkiser Unterwasserwelt, goldener Schatzkammer und selbst erstellten, austauschbaren SVG-Otter-Piraten.
- Beitrittsphase: Titel, echter Countdown, Teilnehmerzahl, Live-Namen, Jackpot und Hinweis auf einsatzfreie Teilnahme. Neue Otter erscheinen animiert; maximal sechs Figuren, unabhängig vom vollständigen tatsächlichen Teilnehmerbestand.
- Ergebnispräsentation: Annäherung und Spannungsanimation, dann bestätigter Erfolg mit Truhenöffnung/Perleneffekten und tatsächlichen Auszahlungen oder Fehlschlag mit unverändertem Jackpot/Verlust 0. Abbruch hat eine eigene Meldung.
- Sanftes Ausblenden nach der konfigurierten Dauer. Reduzierte Bewegung wird über die Browserpräferenz unterstützt. Keine externen Bilder, Schriftarten oder Frameworks.
- Namen werden als Text eingefügt, nicht als HTML. Die Browser-Daten enthalten Anzeigenamen und Beträge, keine Twitch-IDs oder Kontostände. Namen können abgeschaltet/begrenzt werden.

## OBS einrichten

1. Module starten. Der vorhandene Event-Server läuft weiterhin nur lokal unter `http://127.0.0.1:18769/`.
2. Unter **Punkte & Minigames → Heist → OBS-Overlay** eine Browserquelle benennen oder mit **OBS-Szenen & Browserquellen laden** eine Quelle auswählen. Optional Szene auswählen; leer bedeutet die aktuelle Programmszene. Es gibt keine fest vorausgesetzten Szenennamen.
3. **Browserquelle in OBS einrichten** anklicken. Dafür muss die vorhandene OBS-Verbindung aktiv sein. Die Clip-Player-Quelle ist vor Überschreiben geschützt; nicht-Browserquellen werden abgelehnt.
4. Alternativ in OBS manuell eine Browserquelle mit dieser URL, **900 × 600**, 30 FPS und transparentem Hintergrund anlegen. Die bestehende Quelle mit 600 × 500 funktioniert durch responsives Layout weiterhin; 900 × 600 bietet mehr Platz für Namen.
5. Einstellungen speichern. Dieselbe Quelle zeigt Boss, Rennen und Heist priorisiert an. Nicht zusätzlich eine zweite Kopie der alten Boss-/Rennquelle sichtbar lassen, sonst erscheinen identische Anzeigen doppelt.

Die Quelle bleibt technisch aktiviert; der HTML-Inhalt wird bei Inaktivität vollständig transparent. Das vermeidet einen zweiten, verbindungsabhängigen Sichtbarkeitstimer und ermöglicht Quell-Neuladen oder OBS-Neuverbindung ohne Zurücksetzen der Spielzeit. Die Einrichtung per WebSocket ist eine ausdrückliche Bedienaktion, keine Voraussetzung für Punktevergabe. Kein Live-OBS wurde während der Implementierung verändert.

## Prioritäten und Warteschlange

`CommunityOverlayEventManager` koordiniert die Darstellung, nicht die Spiellogik. Standardprioritäten: Boss 100, Rennen 80, aktiver Heist 60, Heist-Ergebnis 20. Es ist jeweils nur ein großes Event sichtbar. Individuell einstellbare Prioritäten können diese Reihenfolge ändern; bei Gleichstand entscheidet Startzeit/ID.

Bis zu 16 Heist-Anzeigen werden nach Event-ID geführt. Aktualisierungen ersetzen denselben Eintrag. Ein unterbrochener aktiver Heist erscheint später mit seiner **aktuellen** Deadline, nicht mit einem neu begonnenen Countdown. Ein inzwischen fertiger Heist kann sein Ergebnis nach dem höher priorisierten Event zeigen. Noch nicht angezeigte Ergebnisse verfallen standardmäßig nach 120 Sekunden; veraltete Beitrittsphasen werden verworfen. Überfüllung entfernt den ältesten Anzeigeeintrag, niemals das Spiel oder Punkte.

Die Ergebnisanimation beginnt einmal zentral, wenn sie tatsächlich ausgewählt wird. Reload und mehrere Browseransichten verwenden denselben Präsentationszeitpunkt. Eine erneute, verspätete Zustellung kann einen fertigen Status nicht zurück in die Beitrittsphase versetzen. Manuelles Ausblenden betrifft nur die Präsentation. Nach Anzeigeende wird die ID zeitlich begrenzt gegen Wiederholung geschützt. Eine bereits laufende Ergebnispräsentation kann bei höherer Priorität verdeckt werden; ihr Anzeigetimer läuft weiter, statt später veraltete Bilder wieder abzuspielen.

## Hot Time

Das bestehende `RpgHotTimeService` liefert aktiv/inaktiv und die persistierte Endzeit. Das Banner zeigt die **konfigurierten tatsächlichen** relativen Drop- und Perlenboni sowie die Restzeit; die letzten 30 Sekunden werden hervorgehoben. Während einer Bossanzeige bleibt das Banner verborgen. Keine neue Hot-Time-Auslösung, keine neuen Belohnungsregeln und kein Timerreset bei Szenenwechsel/Reload.

## Einstellungen ohne JSON

Der neue Heist-Reiter stellt Checkboxen, Zahlenfelder, Text-/Auswahllisten und Speichern bereit:

| Einstellung | Standard / Grenzen |
|---|---|
| Heist-Overlay | an; nur Heist-Darstellung, bestehende Boss-/Rennspiele bleiben aktiv |
| Titel | Überfall auf die Perlenschatzkammer; maximal 120 Zeichen |
| Browserquelle / Szene | frei auswählbar; Szene leer = aktuelle |
| Ergebnisdauer | 10 Sekunden, 2–60 |
| Überfallanimation | 6 Sekunden, 2–20; steuert die Darstellung, nicht das Spiel |
| Teilnehmer anzeigen | an |
| Maximal sichtbare Namen | 12, 1–50; vollständige Teilnehmerzahl bleibt korrekt |
| Erfolgs-/Niederlageeffekte | an |
| Hot-Time-Banner | an, nicht über Boss |
| Prioritäten Boss / Rennen / Heist / Ergebnis | 100 / 80 / 60 / 20, jeweils 0–1000 |
| Maximales wartendes Ergebnisalter | 120 Sekunden, 10–600 |
| Startmeldung senden | an; zusätzliche Checkbox |

Der vorhandene Allgemein-Reiter bleibt für den **einzigen** Beitrittscountdown zuständig. Vorhandene Checkboxen für individuelle Beitrittsmeldungen, Countdownmeldungen und Ergebnismeldungen bleiben maßgeblich. Wer nur kompakte Start-/Endmeldungen möchte, deaktiviert individuelle Beitritts- und Countdownmeldungen; eigene bestehende Einstellungen werden nicht still überschrieben. Teilnahmeweg bleibt der tatsächlich konfigurierte Chatbefehl, standardmäßig `!joinheist`, nicht eine noch unverfügbare Extension.

## Sicher lokal testen

Unter **Heist → OBS-Overlay → Sichere lokale Vorschau öffnen** wird ein statisches HTML-Dokument mit ausschließlich Mockdaten geöffnet. Das funktioniert auch ohne gestartete Module und ohne OBS/Twitch. Im **RPG Debug Center → Heist-Overlay** gibt es denselben Einstieg (nach Aktivieren des bestehenden isolierten Testmodus).

Die Vorschau hat Schaltflächen für Start, Teilnehmer hinzufügen, Zeit vorspulen, Erfolg/Niederlage einschließlich simulierten Punkten, Boss-/Rennkonflikt, Konflikt beenden und Ausblenden. Sie schreibt keinerlei echte Konten und verschickt keinen Chat. Grafik und Text sind ausdrücklich als Mockdaten/selbst erstellte SVG-Platzhalter gekennzeichnet. Die bestehenden Chat-Test-Heist-Schaltflächen sind davon getrennt.

Für Entwickler: `dotnet run --project RaidClipPlugin/Tools/OverlayPreview/OverlayPreview.csproj -c Release`, dann `http://127.0.0.1:18879/preview`. Dieser separate Loopback-Server erzeugt weder Kontenspeicher noch Twitch-/OBS-Verbindungen und belegt nicht den Live-Port. Strg+C beendet ihn.

## Extension-Vorbereitung und Dateien

- `Models/HeistEventSnapshot.cs`: autoritativer Snapshot mit Event-ID, Name, Status, Start/Beitrittsende/Abschluss, Grenzen, Teilnahmebefehl, Währung/Jackpot/Einsatz, persönlichen Ergebnissen und tatsächlichen Summen. Interne Twitch-IDs stehen zukünftigen vertrauenswürdigen Adaptern zur Verfügung, werden aber nicht an das Overlay weitergegeben.
- `Services/HeistService.cs`: erweitert bestehende Statusereignisse um sichere strukturierte Snapshots. Keine zweite Teilnehmerverwaltung oder Auszahlungslogik.
- `Services/CommunityOverlayEventManager.cs`: gemeinsame Anzeige-Metadaten, Prioritäten, Deduplizierung, begrenzte Warteschlange, Ausblendung und Hot-Time-Projektion; Typen für spätere Hot-Time-/Benachrichtigungsadapter vorbereitet.
- `Services/HeistOverlayPage.cs`: HTML/CSS/JavaScript mit wiederverwendbaren SVG-Symbolen und austauschbarer Szene.
- `Services/AdventureOverlayServer.cs`: bestehende read-only Loopback-Endpunkte `/` und `/state`, ergänzt um `/community` und `/preview`. Keine Schreibendpunkte, kein CORS und keine öffentliche API.
- `Services/AdventureGameService.cs`, `Services/ChatMinigameService.cs`: Zustandsanbieter und Verknüpfung ohne Abhängigkeit der Spiele vom Browser.
- `Config/HeistOverlayConfig.cs`, `Config/HeistConfig.cs`, `Services/ConfigurationService.cs`: additive, validierte Einstellungen; alte Konfigurationen erhalten Standardwerte.
- `MainForm.HeistOverlay.cs`, `MainForm.HeistCommands.cs`, `MainForm.RpgDebug.cs`, `Services/ObsService.cs`: vollständige Formularbedienung, Quellenauswahl/-einrichtung und sichere Vorschau.
- `Tools/OverlayPreview/*`, `Tests/HeistOverlayTests.cs`, `RaidClipPlugin.csproj`: isoliertes Vorschauwerkzeug, Tests und Ausschluss des Werkzeug-Einstiegspunkts aus der Hauptanwendung.

Zukünftige Extension-Beitritte müssen weiterhin durch dieselben Berechtigungs- und Teilnehmerprüfungen des autoritativen Heist-Service gehen. Kein Twitch-Frontend erhält in diesem Auftrag direkten Schreibzugriff auf das lokale Plugin. Hot Time und besondere Lootereignisse bleiben fachlich in ihren bestehenden Services; keine vollständige Neuimplementierung aller Minigames.

## Qualitätssicherung und Grenzen

646 Tests bestanden, einschließlich elf neuer Overlay-Fälle für Start/Teilnahme, echte Jackpot-Auszahlung, exakte Summen/Restverteilung, kein Doppelgewinn, fehlendes OBS/fehlerhafte Anzeigeabonnenten, Erfolg/Niederlage/Abbruch, Chatflags, Prioritäten, begrenzte Warteschlange, Countdown-Synchronisierung, Duplikatschutz, Ausblendung, Reload/erneute Abfragen, Datenschutz, Konfigurationskompatibilität und Hot-Time-Endzeiten sowie sieben Fälle für präzise Gegner-Validierung ohne stille Balanceänderung. Release-Build ohne Fehler.

Die interaktive Browser-Vorschau wurde mit mehreren Teilnehmern, Boss-Konflikt und anschließendem Heist-Erfolg (4 × 1.200 = 4.800 simulierte Punkte) geprüft. OBS-WebSocket-Einrichtung und eine physische OBS-Neuverbindung wurden **nicht live** bedient; die Wiederaufnahme der read-only Zustandsabfragen wurde automatisiert geprüft. Keine laufenden Streamspiele oder echten Zuschauerpunkte wurden für diese Prüfung verwendet.

Grenzen: Das bestehende Rennen hat weiterhin seine bisherige Countdown-/Kartenanzeige, keine neu erfundene Rennsimulation. Die Anzeige-Warteschlange ist flüchtig und wird bei Plugin-Neustart nicht nachgespielt; auch Heist-Sitzungen werden wie bisher nicht nach Neustart rekonstruiert. SVG-Figuren sind einfache eigene Platzhalter statt fertiger individueller Otter-Illustrationen. Die Vorschau testet ihre Mockdarstellung, während Prioritäten/Punkte zusätzlich gegen die echten Services automatisiert getestet werden. Veröffentlichung nach gesonderter Benutzerfreigabe.

## Startdiagnose vom 17.10.2026

Letzter protokollierter Fehlstart 17:10:40: generische Validierung ungültiger Unterwassergegner. Nach App-Neustart erfolgreicher Start um 17:11:14; aktuell gespeicherte Werte gültig. Der konkrete ungültige damalige Eingabewert ist nicht im alten Log enthalten; ein beschädigter gespeicherter Spielstand wurde nicht nachgewiesen. Deshalb kein automatisches Zurücksetzen produktiver Daten. Neuer Tabelleneditor statt generischem Collection-Dialog, begrenzte Chance/Runden, eindeutige neu angelegte Einsatzstufen, feld-/stufengenaue Fehler und Wiederherstellung des zuletzt geladenen Gegnerstands ohne App-Neustart. Frühere OAuth-401-Fehler vor dem Neustart waren ebenfalls protokolliert, sind aber nicht die dokumentierte Ursache dieses Fehlstarts.
