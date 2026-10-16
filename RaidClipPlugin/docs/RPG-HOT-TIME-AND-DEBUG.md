# Hot Time und RPG Debug Center (lokaler Entwicklungsstand)

## 1. Hot Time

Ein tatsächlicher Boss-Sieg über den Community-Chat startet bei aktiviertem RPG und aktivierter Automatik eine globale Hot Time für den vorhandenen lokalen Kanal-/Spielstand. Normale Angriffe, Abbruch und Ablauf starten kein Event. `ExecuteBossAttackAsync` gilt hier ausdrücklich als administrativer/testweiser Einstieg und startet keine Community-Hot-Time. Die Debug-Siegesimulation startet ebenfalls keine Hot Time.

Standard: 5 Minuten, relativer Basis-Dropbonus 50 %, zusätzliche zulässige Perlenbelohnungen 100 %. Ein aktives Event kann nicht verlängert oder gestapelt werden. Nach Ablauf startet erst ein neuer echter Sieg ein neues Event. Der auslösende Boss wird über die stabile Begegnungs-ID gespeichert.

`RpgHotTimeService` speichert Event-ID, Boss-ID, UTC-Start/Ende, Aktivstatus und Meldungs-Flags in `minigame/hot-time.json`. Ein Neustart verwendet dieselbe Endzeit. Ein bereits während der Offlinezeit abgelaufenes Event wird nicht erneut abgespielt: keine nachträglichen Würfe, Gutschriften oder Chatmeldungen. `Status` liefert das Event, aktuellen Aktivstatus, Restsekunden und gegebenenfalls Ladefehler strukturiert für spätere Extension-Adapter. Keine öffentliche API.

`PollMessages` aktualisiert den Laufzeitstatus und beansprucht Start-/Endmeldungen persistent vor dem Versand über den bestehenden Chatweg. Das verhindert doppelte Meldungen einschließlich Neustarts. Bewusste Grenze: Bei einem Netzwerk-/Prozessfehler genau zwischen Anspruch und Versand kann eine Meldung fehlen; ein erneuter Versand mit möglicher Doppelmeldung wird vermieden. Bei beschädigter Statusdatei bleiben Boni aus und der Fehler wird angezeigt, statt eine neue Laufzeit zu erfinden.

## 2. Berechnung und Belohnungsquellen

Loot: `Basis × (1 + Hot-Time-Dropbonus / 100) + Einsatzbonus + begrenzte Luck-/Sushi-/Ausrüstungsboni + bestehender Eventbonus`, anschließend gemeinsamer Dropcap und höchstens 100 %. Eine alleinige Basis von 25 % wird so zu 37,5 %. Legendary-Mindest-Einsatz und RiskTier-Maximum werden weiterhin vor der Kandidatenauswahl geprüft, auch bei zusätzlichem Loot. Separate Boss-/Angel-Ausrüstungswürfe verwenden ihren eigenen Basiswert relativ erhöht und ebenfalls den gemeinsamen Cap. Es wird kein zusätzlicher Einsatz ausgelöst.

Perlen: Der zentrale `RpgRewardSource` unterscheidet Quellen. `RpgReward` trägt zusätzlich `HotTimeApplied`; ein bereits markierter Betrag wird nicht erneut verstärkt.

| Quelle | Hot-Time-Perlenbonus |
|---|---|
| Anwesenheit, Watchtime, Chat-/Follow-/Sub-/Raid-/Kanalbelohnungen, täglicher Bonus | ja, auf den ausgewiesenen Belohnungsbetrag |
| Angeln und Dive | nur auf den positiven Nettogewinn nach Abzug des Einsatzes und vorhandener zulässiger Ausrüstungsboni |
| Casino-/Gamble-/Coinflip-/Slots-/Roulette-Nettogewinne | ja, nur positiver Gewinnanteil; Tages-/Kontolimits rechnen mit dem verstärkten Betrag |
| Boss-Sieg | ja, sofern bereits vorher Hot Time aktiv war; der erste auslösende Sieg wird nicht rückwirkend verdoppelt |
| Admin-Gutschrift, Transfers, Einsatzrückzahlung, Perk-Erstattung, Salvage | niemals |
| Umverteilte Duel-/Race-/Heist-/Jackpot-Pots | unverändert, keine zusätzliche Verstärkung von fremden Einsätzen/Potanteilen |

Beispiel: Einsatz 100, reguläre Auszahlung 200 → Nettogewinn 100 → während Standard-Hot-Time Auszahlung 300 (100 Einsatz + 200 Gewinn), nicht 400. Ein Ergebnis von 100 bei Einsatz 100 bleibt 100. Ausrüstungs-Perlenbonus und dessen gemeinsamer 15-%-Cap bleiben vor dem separat klassifizierten Hot-Time-Bonus erhalten. Die bestehenden Chat-/Historienausgaben verwenden die tatsächliche Casino-/Daily-Auszahlung.

## 3. Grafische Hot-Time-Einstellungen

Punkte & Minigames → RPG → **Hot Time**. Ohne JSON einstellbar: Aktivierung, Boss-Automatik, Dauer, relativer Dropbonus, Perlenbonus, Start-/Endtext. Haken für boolesche Optionen, Zahlenfelder für Dauer/Boni, Live-Restlaufzeit und sofortiges Beenden. Speichern läuft über den bestehenden validierten Einstellungsweg. Zulässige Dauer 1–60 Minuten, Boni 0–200 %, Nachrichtenvorlagen höchstens 450 Zeichen. Platzhalter: `{minutes}`, `{drop}`, `{pearls}`, `{reward}`; `{reward}` ergibt bei 100 % „doppelte Perlenbelohnungen“.

Manueller Start ist bewusst ausschließlich im isolierten Debug Center möglich. Es gibt keinen Testknopf, der produktive Zuschauerboni startet. Der lokale Spielstand repräsentiert wie das bestehende Punktesystem den einen konfigurierten Kanal; parallele Mehrkanal-Betriebsführung wurde nicht neu eingeführt.

## 4. RPG Debug Center: Isolation und Bedienung

Punkte & Minigames → RPG → **RPG Debug Center**. Standardmäßig deaktiviert. Zum Aktivieren muss das lokale Windows-Konto Administrator beziehungsweise Mitglied der lokalen Administratorengruppe sein; die Prüfung erfolgt vor Aktivierung und jeder Testaktion. Es gibt keinen Twitch-Chat-Einstieg und keinen öffentlichen Autorisierungs-Bypass. Tests können intern eine Autorisierung injizieren.

Der Debugdienst erzeugt ausschließlich einen eigenen ViewerPointStore unter `%LOCALAPPDATA%/RaidClipPlugin/rpg-debug/players`, mit fester TEST-Spieler-ID. Produktive Stores werden ihm nie übergeben. Die Konfiguration ist eine JSON-deep-copy der reinen Minigame-Konfiguration, nicht des AppConfig mit Twitch-Zugangsdaten. Testkonfiguration und RNG-/Editoroptionen werden ausschließlich in `rpg-debug/debug-settings.json` gespeichert. Deaktivieren verhindert weitere Testaktionen und verändert keine Produktivregeln.

Bereiche:

- **Drop-Editor:** Basis/Maximalchance, fünf Rarity-Gewichte, gewünschte einzelne Seltenheit, Luck, statischer Eventbonus, 0–2 explizite Test-Zusatzwürfe, Legendary-Mindest-Einsatz, Testeinsatz und Seed. Normal, garantiert oder kein Loot. „Garantiert“ erhöht nur im Test die Basis/den Cap auf 100 %, umgeht aber nicht stillschweigend die Einsatzgrenzen.
- **Legendary-Drop erzwingen:** stellt ausschließlich die Testkonfiguration auf Legendary/garantiert, passenden Einsatz und passenden Test-RiskTier um; der vorhandene Lootservice erzeugt das Item. Einfachster gezielter Legendary-Test.
- **Test-Balancing:** grafischer PropertyGrid für die getrennte RPG-Testkonfiguration und deren numerische Caps; kein Bearbeiten produktiver JSON-Dateien.
- **Item-Generator:** kombinierbare Filter nach Name/ID, Seltenheit, Slot, Set und vorhandenen/erlaubten Spezialeffekten. Zufall, Minimum, Durchschnitt, Maximum oder benutzerdefinierte gültige Werte in einer Attributtabelle. Feste Items bleiben fest; benutzerdefinierte Rollwerte außerhalb der tatsächlichen Definitionsgrenzen werden abgelehnt. Ein gewählter Effekt wird im Test gezielt über den vorhandenen Generator ausgewählt, aber nur, wenn Definition und Seltenheit ihn bereits erlauben. Es werden keine erfundenen Effekte injiziert.
- **Kampf & Fortschritt:** echter zentraler Bossschadens-/Proc-Rechner, Krit/Double/Triple-Erzwingen über Testoptionen, Boss-HP, Siegesimulation, effektive Werte, Perlenänderung, Aktivitätsbelohnung, Perk-Level, Fischbestände, Crafting, Sushi-Aktivierung, Sushi-Verbrauch für gewählte Aktivität, Inventar-/Cooldown-Reset, Ausrüstung anlegen und vollständiges Set testen. Ausrüsten/Setwerte verwenden die bestehenden Modelle und Statservices.
- **Hot Time testen / beenden:** nur der eigene Debug-Store. Die Test-Hot-Time lässt sich beispielsweise mit einer Test-Aktivitätsbelohnung gegen eine Admin-Perlenänderung vergleichen. Das automatische Produktivereignis bleibt unangetastet.

Erzwungene Kampfergebnisse werden durch reproduzierbare Ablehnungsstichproben des echten Kampfrechners auf abgetrennten Testprofilen gefunden. Falls erforderlich trägt nur dieses Profil einen temporären Test-Buff mit Chancen innerhalb der vorhandenen Caps. Er wird nie gespeichert. Krit/Triple und Krit/Double sind kombinierbar, Double und Triple nicht gleichzeitig; ein passender Cap von 0 führt zu einem erklärten Fehler. Höchstens 100.000 Versuche, keine Endlosschleife. Normale Simulationen benutzen dagegen die tatsächlichen Wahrscheinlichkeiten, nicht die Einzeltest-Erzwingungshaken.

Die Boss-Siegesimulation markiert den isolierten Testboss als besiegt und prüft den regulären Angriffs-/Buffpfad; sie ist ausdrücklich kein End-to-End-Twitch-Bosslauf und vergibt keinen produktiven Sieg/Hot-Time-Start. Die automatische Community-Auslösung wird separat durch Integrationstests des vorhandenen AdventureGameService geprüft.

## 5. Simulationen und Diagnose

100, 1.000, 10.000 oder 100.000 Durchläufe. Rückgabe: Dropanzahl, Anzahl/Prozentsatz je Seltenheit bezogen auf alle Lootwürfe, Legendary-Anzahl, Crit-/Double-/Triple-Anzahl und Quote, Extra-Loot-Procs, durchschnittlicher Schaden und durchschnittliche Perlenänderung. Test-Luck, ausgerüstete Sets/Items, passende Sushi-Boni und tatsächliche Proc-/Sushi-Verbrauchsregeln werden berücksichtigt. Loot-/Kampf-/Nettogewinnberechnung verwendet dieselben zentralen Dienste wie das Spiel.

Simulationen laufen im Hintergrund und ausschließlich auf einem abgetrennten Profil, ohne Spielerstände zu speichern. Sie verwenden einen festen Seed, einen 30-Sekunden-Aktionsschritt und einen eingefrorenen Hot-Time-Aktivitätszustand des Teststarts; das erlaubt reproduzierbare Vergleiche. Der Simulationsmodus ist ein kombinierter Loot-/Kampf-Benchmark, keine vollständige Nachbildung eines Streams mit Kontolimits, Chatlatenzen, Bossrunden und allen Cooldown-Interaktionen. Diese Einschränkung ist absichtlich sichtbar, statt Ergebnisse als echte Live-Prognose auszugeben.

`rpg-debug/debug-actions.jsonl` protokolliert Aktion, TEST-Spieler, RPG-Testkonfiguration, Optionen/Seed, Ergebnis, effektive Stats, Buffs, Cooldowns und Fehler. Loot-/Generatorergebnisse enthalten die erzeugten Items und berechneten Chancen. RNG-Trace enthält aus Platzgründen die ersten 200 Würfe plus Gesamtzahl/Seed, nicht Millionen Einzelzeilen. Keine Twitch-Tokens oder echten Zuschauerprofile werden an den Logger übergeben.

## 6. Validierung, Sicherung und Wiederherstellung

Produktive GUI-Speicherung validiert die gesamte GUI-Konfiguration sowie RPG, und prüft auch den serialisierten temporären Snapshot. Die Datei wird mit Flush geschrieben, vor dem Ersetzen nochmals gelesen und validiert und dann atomar über `File.Replace` beziehungsweise `File.Move` ausgetauscht. Eine existierende, beschädigte Konfiguration wird nicht als neue „gültige“ Sicherung übernommen. Der vorige gültige Stand bleibt in `settings.json.bak`.

**Letzte gültige Konfiguration wiederherstellen** im Hot-Time-Bereich validiert die Sicherung vor dem Ersetzen. Der vorherige aktuelle Stand wird separat als `.before-restore-<GUID>.bak` erhalten. Danach werden alle Einstellungsansichten neu geladen. Der Debugbereich hat zusätzlich getrennte Speichern-/Wiederherstellen-Schaltflächen. `RpgAtomicFile` verwendet für neue Debug-/Hot-Time-Dateien dieselben Validierungs-/Backup-/Austauschprinzipien. Fehlgeschlagene Validierung lässt die gültige Zieldatei unangetastet.

## 7. Dateien und Grenzen

Neu: `Config/RpgHotTimeConfig.cs`, `Services/RpgHotTimeService.cs`, `Services/RpgAtomicFile.cs`, `Services/RpgDebugSession.cs`, `MainForm.RpgDebug.cs`, `Tests/RpgHotTimeAndDebugTests.cs`.

Angepasst: `Config/RpgConfig.cs`, `MainForm.AdventureGames.cs`, `Models/ViewerPointEntry.cs`, `Services/ViewerPointStore.cs`, `AdventureGameService.cs`, `ChatMinigameService.cs`, `RpgAdventureService.cs`, `RpgBossLootService.cs`, `RpgLootService.cs`, `RpgItemGenerationService.cs`, `ConfigurationService.cs` und Patchnotes.

Prüfergebnis am 16.10.2026: vollständige Release-Testsuite **525 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Release-Build erfolgreich mit 0 Fehlern und 10 bereits vorhandenen Paket-/WindowsBase-/Nullable-Warnungen. Zusätzliche Tests decken Dauer, Meldungs-Deduplizierung, Offline-Neustart, fehlendes Stapeln, relativen Dropbonus und Gating, Quellen-/Wiederholschutz, tatsächlichen Casino-Netto-/Salvage-Bonus-Ausschluss, reale versus administrative Boss-Siege, Isolation, gezielte Legendary-Generierung, Sets/Progression, ungültige Eingaben, reproduzierbare Simulationen, echte Forced-Procs sowie atomare Sicherung/Wiederherstellung ab.

Die neuen Oberflächen werden von den vorhandenen STA-/WinForms-Aufbautests mit aufgebaut; keine Änderungen an realen OBS-/Twitch-Fenstern vorgenommen. Kein manueller Live-Stream-Test und keine produktiven Testgutschriften. Keine Veröffentlichung/kein Deployment dieses Entwicklungsstands. 2.1.14 bleibt die veröffentlichte Version.
