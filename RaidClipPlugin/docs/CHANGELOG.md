## 2.1.34 – Unterwassergegner: 0–100 %

- Begegnungschance unter RPG → Unterwassergegner von 0 bis 100 % einstellbar, einschließlich Nachkommastellen. 0 % bedeutet keine Begegnung; 100 % bedeutet eine Begegnung bei jedem gültigen Tauchgang, sofern Gegner- und HP-System aktiv sind.
- Oberfläche, Laden und Validierung verwenden denselben Bereich. Bisherige Einstellungen und Standardwert 15 % bleiben erhalten; Gegnerwerte und Loot unverändert.

# Patchnotes

## 2.1.33 – OBS-Eventquellen automatisch aktualisieren

- Boss, Heist und Rennen laden ihre OBS-Browserquelle beim Start einmal einschließlich Cache neu.
- Hintergrundverarbeitung und Wiederholungsschutz: OBS-Fehler stoppen weder Spiel noch Auszahlung.
- Auf aktuellem Stand 2.1.32 aufgebaut; Twitch-Anmeldung und OBS-Schnellaktion bleiben erhalten.
- Fehlende Patchnotes 2.1.23–2.1.32 aus den veröffentlichten Versionshinweisen nachgetragen.

## 2.1.32

- **OBS-Quelle erstellen** ist wieder unter **Dashboard → Schnellaktionen** erreichbar.
- Der vorhandene Ablauf erstellt bzw. aktualisiert die Browserquelle in der aktiven OBS-Szene.
  OBS muss geöffnet sein; Host, Port und Passwort werden weiterhin aus den Einstellungen verwendet.
- Keine Änderung an Twitch-Zugangsdaten, RPG-Spielständen oder Cloud-Zuordnungen.


## 2.1.31

- Neuer direkter Anmeldeweg: oben auf **Twitch anmelden** klicken oder unter
  **Einstellungen → Allgemein → Bei Twitch anmelden**. Die Anmeldung ist unabhängig von OBS,
  gestarteten Plugin-Diensten und der Cloud-Zuordnung möglich.
- Ein eigenes Fenster zeigt den Twitch-Gerätecode und einen anklickbaren Browserlink.
  Auch wenn Windows den Browser nicht automatisch öffnet, bleibt die Anmeldung erreichbar.
- Fehler bei der Anmeldung werden im Fenster angezeigt. Abbrechen beendet das Warten.
- Nach validierter Anmeldung erscheinen Kontoname und Begrüßung. Ein leerer Zielkanal wird
  mit dem angemeldeten Login vorbelegt; vorhandene Kanaleinstellungen bleiben erhalten.
- Spielstände und Cloud-Zuordnung werden durch die direkte Anmeldung nicht verändert.

Version 2.1.30 bot die erneute Einrichtung der App-Zugangsdaten. Die eigentliche Twitch-Anmeldung
erfolgte beim normalen Plugin-Start jedoch erst nach dem OBS-Verbindungsaufbau. Der neue Knopf
macht diesen Schritt unabhängig und sichtbar.

Die Anmeldung wurde automatisiert mit simulierten Twitch-Antworten einschließlich fehlendem
Browser, Abbruch, abgelehnten Zugangsdaten und dem Windows-Anmeldefenster geprüft. Eine
erfolgreiche Anmeldung mit Dennis' echtem Konto wurde nicht vorweggenommen.


## 2.1.30

- Neue Wartungsseite unter **Einstellungen → Wartung**: gespeicherte Einstellungen neu laden,
  Twitch Client ID und Secret austauschen sowie die aktuelle Programmversion erneut installieren.
- Twitch-Zugangsdaten werden vor dem Austausch verschlüsselt gesichert. Die alte Twitch-Anmeldung
  wird entfernt; RPG-Spielstände, Programmeinstellungen und Cloud-Zuordnung bleiben erhalten.
- Obere Leiste und Begrüßung zeigen nach validierter Anmeldung das tatsächliche Twitch-Konto.
  Der Anzeigename ist nicht mehr fest auf „Funjii“ gesetzt. Neue Nutzer wählen ihren Kanal selbst.
- Programmreparatur und Updates sichern ersetzte Dateien vorab und stellen sie bei Kopierfehlern
  wieder her. Nutzerdaten in Update-Paketen werden abgelehnt.
- Schutz vor versehentlich gemeinsam verwendeten Cloud-Zuordnungen: Zielkanal und Cloud-Kanal
  müssen zusammenpassen. RPG-Verwaltung mit Cloud-Sync verlangt die Anmeldung des Kanalinhabers.
- „Punktedaten zurücksetzen“ setzt nur noch Perlen zurück und erhält RPG-Level und Inventar.

Für getrennte Nutzer sind eigene Twitch-Anwendungen und eigene Cloud-Zugänge erforderlich.
Eine neue Twitch Client ID trennt bestehende Cloud-Daten nicht automatisch. Weitergegebene
Cloud-Schlüssel müssen separat ersetzt werden; die Desktop-Prüfung ersetzt keine Serverberechtigung.

Nach dem Update Plugin neu starten. Keine Spielstandmigration und keine automatische Zurücksetzung.
Details und Grenzen: [Wartungsdokumentation](https://github.com/Funjii95/RaidClipPlugin/blob/v2.1.30/docs/PLUGIN_MAINTENANCE.md).

Validierung: 761 Desktop-Tests, 232 Backend-/Publisher-Tests und isolierter Windows-Starttest.
Echte Twitch-Anmeldungen und produktive Cloud-Löschungen wurden nicht ausgeführt.


## 2.1.29

- Automatische Cloud-Aufnahme bestehender Spieler mit Perlen, wenn der Betreiber den neuen Modus aktiviert hat.
- Einmal aufgenommene Spieler bleiben auch bei 0 Perlen synchronisiert.
- Neue Cloud-Profile entstehen aus den vollständig gespeicherten Plugin-Spielständen. Keine manuelle Datenbankanlage pro Spieler erforderlich.
- Bestehende dauerhafte Warteschlange, Wiederholungsversuche und Revisionsprüfung bleiben erhalten. Alte Snapshots überschreiben keine neueren; gelöschte Konten werden nicht automatisch wiederhergestellt.
- SoundAlerts und konfigurierte Perlen-Sperrlisten werden bei der automatischen Aufnahme ausgeschlossen.
- Twitch-Identität, Kanalbindung, Plugin-Verbindung und vorhandene RPG-Spielregeln bleiben maßgeblich. Der Stream muss nicht live sein.

## Installation

Das Plugin vor dem Update regulär beenden und anschließend Version 2.1.29 starten. Vorhandene Spielstände und geschützte Zugänge behalten.

Für Funjiis vorhandene Installation sind die beiden lokalen Synchronisationsschalter bereits vorbereitet und die dazugehörigen Serverrevisionen aktiviert. Die automatische Aufnahme startet mit dem neuen Plugin. Ein Twitch-Konto ohne vorhandenes Plugin-Profil bzw. ohne bisherige Perlen wird erst nach einem normalen Chat-/Plugin-Vorgang mit Perlen aufgenommen.

Für andere Betreiber wird Cloud-Synchronisation dadurch nicht automatisch eingerichtet. Benötigt werden die passende geschützte Betreiberkonfiguration und ein entsprechend konfigurierter Server.

## Prüfung

Lokal 748 Plugin-Tests und 232 Backend-/Publisher-Tests bestanden. Synthetischer Cloud-Test für Erstanlage, Duplikate, Nullsaldo, veraltete Snapshots, JWT-/Kanal-/Identitätsprüfung und Aktionssperre bei getrenntem Plugin bestanden.

Die tatsächliche automatische Aufnahme der lokalen Spieler nach Start sowie die vollständige Twitch-Mehrspielerabnahme bleiben separat zu prüfen. Dies ist keine öffentliche Freigabe der Twitch Extension.

Rollback: Version 2.1.28 bleibt verfügbar. Spielstände, Outbox und Aktionsjournale beim Rollback erhalten; die Betreiberkonfiguration und Serverrevisionen können auf den gesicherten Stand zurückgestellt werden.


## 2.1.28

- Erfolgreiche Perk-Käufe liefern den tatsächlich gespeicherten Spielerstand und die vom Plugin berechneten Statuswerte direkt mit der bestätigten Antwort.
- Das aktualisierte Twitch-Panel übernimmt Perk-Level, Preise und Perlen nach der Kaufbestätigung, während der Cloud-Abgleich im Hintergrund läuft. Ein langsamer Snapshot-GET hält den Kaufknopf nicht mehr gesperrt.
- Ältere Cloud-Snapshots dürfen den bestätigten Spielerstand nicht zurücksetzen. Fremde Spielerprofile werden abgewiesen. ActionId, Speicherung, Kostenprüfung und Doppelbuchungsschutz bleiben erhalten.
- Ältere Plugin-Versionen funktionieren weiter mit dem bisherigen Nachladeweg; die Beschleunigung setzt das neue Plugin und das aktualisierte Hosted-Test-Paket voraus.

Validierung: 744 Plugin-Tests, 94 Frontend-Tests, drei Browserprüfungen am konkreten Hosted-Test-Build; der Release-Build prüft zusätzlich die Backend-/Publisher-Tests. Synthetischer Nachweis: Der nächste Kaufknopf wird freigegeben, obwohl der Cloud-GET absichtlich unbeantwortet bleibt. Ein älterer nachgelieferter Snapshot setzt weder Level noch Perlen zurück. Kein echter Kauf für die Tests ausgeführt; die genaue Zeitersparnis über Twitch muss nach dem Update geprüft werden.

Update: Plugin über den Auto-Updater aktualisieren und neu starten, Twitch-Kanalseite neu laden.
Rollback: Plugin schließen und den erhaltenen Installer von v2.1.27 erneut installieren. Spielstände und Einstellungen nicht löschen. Die neue Extension unterstützt weiterhin die alte Antwort ohne Profil. Keine Cloud-Bereitstellung oder Änderung der Spielökonomie.


## 2.1.27

- Der geschützte Aktionskanal ruft wartende Aufträge im Leerlauf alle 500 statt 1000 Millisekunden ab.
- Die Cloud-Synchronisierung prüft bis zu zwei unabhängige Konten gleichzeitig. Pro Konto bleiben Warteschlange, Reihenfolge, Hash- und Revisionsschutz erhalten.
- Twitch-Extension: frühere erste Antwortprüfung nach 250 Millisekunden; separate Aktualisierung des Hosted-Test-Pakets erforderlich. Enthält die Charakterwerte-Korrektur und den Boss-Countdown.

Validierung vor Veröffentlichung: 744 Plugin-Tests, 227 Backend-/Publisher-Tests und 92 Frontend-Tests; zwei Browserprüfungen gegen das konkrete Hosted-Test-Paket. Synthetische Konten; keine Spielstände zurückgesetzt oder Spielregeln geändert. Ein vollständiger echter Mehrspielertest dieser Optimierung ist noch offen.

Hinweis: Der schnellere Aktionsabruf erzeugt im Leerlauf etwa 120 statt 60 Claims pro Minute. Vorhandene Serverlimits und Sicherheitsprüfungen bleiben unverändert. Keine neue Cloud-Bereitstellung nötig.

Update: Über den Plugin-Auto-Updater aktualisieren und das Plugin neu starten. Laufende Aktionen vorher abschließen lassen.
Rollback: Plugin schließen und den unverändert verfügbaren Installer von v2.1.26 erneut installieren. Spielstände und Konfiguration nicht löschen. Für die Extension kann das vorherige Hosted-Test-Paket mit korrekter rc1-Adresse erneut hochgeladen werden.


## 2.1.26

- Boss-Einstellungen: HP-Modus als Dropdown „Dynamisch“ oder „Feste HP“. Dynamisch startet mit 100 HP und ergänzt je weiterem Teilnehmer 100 HP; bestehender Schaden bleibt erhalten. Ein Moduswechsel gilt erst ab dem nächsten Boss. Vorhandene Einstellungen bleiben auf feste HP.
- Moderator/Broadcaster: !heal @Spieler und !heal all füllen die HP vorhandener Konten auf. Boss-Ausschluss und Cooldowns bleiben bestehen.
- Moderator/Broadcaster: !kill boss beendet einen aktiven Kampf mit Teilnehmern über die reguläre Sieg-Abrechnung einschließlich Belohnungen und Hot Time.
- Boss-Sieg zeigt die tatsächlich gutgeschriebenen Perlen pro belohntem Spieler an, einschließlich Kontolimit.
- Hot-Time-Chatmeldung enthält keine Zeitmessungs-Nachkommastellen mehr.
- Publisher: Getrennte Limits für Statusabfragen und Snapshot-Schreibzugriffe; kein zusätzliches Cloud-Deployment durch dieses Plugin-Release.

Validierung: 743 Plugin-Tests und 227 Backend-/Publisher-Tests bestanden. Tests mit synthetischen Konten; kein vollständiger manueller Twitch-Mehrspielertest dieser Version durchgeführt.

Update: Laufenden Boss vorher beenden, Plugin aktualisieren und neu starten. Unter Boss-Einstellungen den gewünschten HP-Modus auswählen.
Rollback: Plugin schließen, Windows-Installer von v2.1.25 erneut installieren. Nicht während eines aktiven dynamischen Bosskampfs zurückwechseln. Keine Spielstände löschen oder zurücksetzen; die bisherigen festen HP und das Spieler-Speicherformat bleiben erhalten. v2.1.25 bleibt als Release verfügbar.


## 2.1.25

- `!boss` zeigt die verbleibende Boss-Pause in Minuten und Sekunden. Während eines Kampfes zeigt es den aktiven Bossstatus. Der Befehl startet keinen Boss mehr.
- Der einzige manuelle Chat-Befehl zum Bossstart ist `!start boss`, weiterhin nur für Moderatoren und Broadcaster.
- `!attack` / `!joinboss`, Bossregeln, Belohnungen und bestehender automatischer Bossstart bleiben unverändert.
- Befehlsübersicht und Berechtigungen wurden passend getrennt. `!boss` ist standardmäßig für Zuschauer sichtbar; eigene Rollenüberschreibungen werden berücksichtigt.

733 Plugin-Tests bestanden, einschließlich neuer Status-/Cooldown-/Berechtigungstests. Keine Spielstandmigration oder Zurücksetzung.

Nach dem Update das Plugin und seine Module neu starten. Twitch bleibt im geschlossenen Hosted Test; dieses Plugin-Release ist keine öffentliche Twitch-Veröffentlichung.

## 2.1.24

Die reguläre Windows-Version enthält jetzt die bereits getestete erweiterte RPG-Anbindung für die Twitch Extension. Damit meldet das laufende Plugin auch Bossstatus, Perlen-Minispiele und die weiteren integrierten Aktionen an das Backend.

- Gemeinsame autoritative Spielstände und Cooldowns für Chat und Extension; keine zweite Spielökonomie.
- Geschützte ausgehende Aktionsverbindung und automatische vollständige Cloud-Snapshots mit Wiederholungs-/Revisionsschutz.
- Bestehende RPG-, Inventar-, Perk-, Boss- und Minispielregeln bleiben maßgeblich.
- Testerlisten bleiben ausdrücklich konfiguriert; der Publisher unterstützt bis zu 100 freigegebene Konten.
- Spielen benötigt ein verbundenes, spielbereites Plugin; ein Livestream ist keine Voraussetzung.
- Selbstlöschung durch Zuschauer bleibt standardmäßig deaktiviert. Keine Cloud-Ressourcen oder Serverflags werden durch dieses Update verändert.

Bestehende Spielstände und geschützte lokale Zugänge werden nicht mit dem Updatepaket ausgeliefert oder zurückgesetzt. Nach dem Update das Plugin und seine Module starten und die Twitch-Seite neu laden.

Die Twitch Extension bleibt im geschlossenen Hosted Test. Die vollständige echte Abnahme mit zwei Twitch-Konten ist noch offen; dieses Plugin-Release ist keine öffentliche Twitch-Genehmigung.

## 2.1.23

RaidClipPlugin 2.1.23 integriert die geschützte RPG-Cloud-Synchronisierung und den ausgehenden Aktionskanal zur Twitch Extension. Die vorhandene Plugin-Spiellogik bleibt autoritativ. Spielaktionen benötigen ein verbundenes, spielbereites Plugin; der Twitch-Stream darf offline sein.

- Startschutz korrigiert: Das Plugin zählt seinen eigenen Prozess nicht mehr als zweite Instanz.
- Vorhandenes Spielstandformat und normale lokale Speicherung bleiben erhalten. Cloud-Anbindung benötigt die separate geschützte Konfiguration; Zugangsdaten sind nicht im Download enthalten.
- Geprüfter Windows-Build: 693 Plugin- und 196 Backend-/Publisher-Tests erfolgreich. Zusätzlich synthetische End-to-End-Prüfungen und echter Twitch-Hosted-Test mit reinen Statusaktionen.
- Weitere Extension-Aktionen und manuelle Geräte-/Upgrade-Abnahmen bleiben in Arbeit. Die Twitch Extension befindet sich im geschlossenen Hosted Test und ist nicht öffentlich von Twitch freigegeben.

Für bestehende Installationen das Update-ZIP bzw. den integrierten Auto-Updater verwenden; für Neuinstallationen den Setup-Installer. Kein Spielstand-Reset enthalten.

**Full Changelog**: https://github.com/Funjii95/RaidClipPlugin/compare/v2.1.22...v2.1.23

## 2.1.22 – Heist-OBS-Overlay und Gegner-Einstellungen

- Gemeinsame Event-Browserquelle um animierte Unterwasser-Heists mit eigenen SVG-Otter-Piraten, Countdown, Crew, Schatzkammer und tatsächlichen Jackpot-Auszahlungen erweitert. Keine neue Heist-Ökonomie; echte Punkteverteilung bleibt im vorhandenen Service.
- Zentraler Präsentationsmanager priorisiert Boss, Rennen, Heist und Ergebnisse, mit begrenzter Warteschlange, Verfallszeit, Wiederholungsschutz und gemeinsamem Zeitstempel bei Reload. Hot-Time-Banner verwendet bestehende Boni und Endzeiten, ohne Bossanzeigen zu überdecken.
- Neuer Heist-Reiter „OBS-Overlay“ mit Formularfeldern, auswählbarer OBS-Szene/Browserquelle und sicherer lokaler Mockvorschau; zusätzlicher Einstieg im RPG Debug Center. Read-only Loopback-Daten und interne Event-Snapshots für spätere Extension-Adapter; keine öffentliche API.
- Startdiagnose: Der letzte Fehler am 17.10.2026 um 17:10:40 betraf ungültige Unterwassergegner-Einstellungen; nach dem App-Neustart waren die gespeicherten Werte wieder gültig. Der genaue damalige Wert ist im alten Protokoll nicht enthalten. Neuer Tabelleneditor ersetzt den fehleranfälligen generischen Collection-Editor, begrenzt Chance/Runden und erzeugt eindeutige neue Einsatzstufen. Präzise Validierungsfehler und Wiederherstellung des zuletzt geladenen Gegnerstands ohne App-Neustart; keine stillen Balanceänderungen oder Kontenänderungen.
- 646 Tests bestanden; Einrichtung, Funktionen und bekannte Grenzen: `docs/HEIST-OBS-OVERLAY.md`.

## 2.1.21 – Loot Expansion 3.0

- 52 zusätzliche Definitionen: 34 Ausrüstungsteile, acht Teile für zwei neue Sets und zehn passive Sammlerschätze. Alte Instanzen und die vorhandene Krone des Abgrunds bleiben erhalten.
- Große Bosskämpfe: zentrale Verteidigungsrechnung mit Ausweichen und einmaliger Schadensreduktion, kritische Heilung aus tatsächlichem Schaden ohne festes Heilungscap. Set-HP werden ohne Gratisheilung berücksichtigt.
- Separate, duplikatsichere Sammlerboni für passende positive Belohnungsquellen. Keine Kampfboni durch Besitz. Konfigurierbare Sammlercaps und gewichtete Pools nach dem unveränderten Seltenheitswurf.
- `!treasures`, erweiterte Item-/Werteansichten, fokussierter Loot-/Set-Konfigurationsreiter und ergänzte isolierte Debugaktionen. Details und vollständiger Katalog: `docs/RPG-LOOT-EXPANSION-3.md`.

## 2.1.20 – Vampir-Maki ohne Lifeleech-Cap

- Fangbarer Vampirtintenfisch (`vampire_squid`, Rare, Tiefsee, Standard 300 Perlen) und zuvor fehlender Thunfisch (`tuna`, Uncommon, Standard 150 Perlen). Gewichte 8/25 und Belohnungen sind im vorhandenen Fangpool-Editor einstellbar. Keine zusätzliche Einsatzpflicht.
- `!cook vampir` / `!eat vampir`: 2 Vampirtintenfische +1 Thunfisch +750 Perlen. Defensiver Sushi-Slot, 5 % Lifeleech, drei Bosskampfteilnahmen. Vorhandene Bestände und Rezepte bleiben erhalten.
- Heilung `floor(min(Angriffsschaden, verbleibende Boss-HP) × 0,05)`, unmittelbar vor dem großen Boss-Gegenangriff. Kein festes HP-Heilungscap; nur MaxHP begrenzen. Kritische Treffer und Zusatztreffer zählen über ihren tatsächlichen Schaden, Overkill nicht. Keine Wiederbelebung, kein Umgehen von Todessperren und kein Lifeleech gegen kleine Gegner.
- Konfigurierbarer Lifeleech-Prozentwert im Rezepteditor, persistenter Effektsnapshot, Anzeige im Sushi-Status und Kampftext sowie isolierte Debugaktion „Boss-Lifeleech“. 610 Tests bestanden; Details: `docs/RPG-VAMPIR-MAKI.md`.

## 2.1.19 – Sushi 2.1 und neue Meeresbewohner

- Tatsächlich fangbar: Seegurke, Garnele, Riesengarnele, Kugelfisch und Goldener Kugelfisch, mit relativen Gewichten 50/25/12/10/3 und eigenen Fangbelohnungen 40/100/250/350/800 Perlen. Bestehende Arten bleiben erhalten; bezahlte bisherige Fänge behalten ihre Auszahlung. `!fish` erlaubt jetzt auch kostenlose Versuche; `!fishbag` zeigt Zutatenbestände.
- Defensive Rezepte: Kappa-Maki (2 Seegurken +300 Perlen, +3 passive HP/Minute), Tempura-Rolle (2 Garnelen +1 Riesengarnele +450 Perlen, +5 Chat-HP), Kugelfisch-Sashimi (2 Kugelfische +700 Perlen, 15 % weniger großer Boss-Gegenschaden). Multi-Zutaten-Crafting und eigene Perlenbelohnungen werden in vorhandenen Transaktionen atomar gespeichert.
- Ein offensiver und ein defensiver Slot, ohne Überschreiben. Aufladungen zählen je Boss-ID statt je Angriff; der dritte Boss erhält den vollen Bonus bis zum Ende. Alte Buffs und Bestände werden ohne Rücksetzen übernommen; vorhandene Mehrfach-Offensivbuffs bleiben bis zum Verbrauch erhalten. Regeneration rechnet vergangene Intervalle mit den damals gültigen Buffzeiten ab.
- `!cook`/`!eat` als Aliase, Rezeptübersicht mit `!sushi`, Statusseiten und Fischvorrat. Tabelleneditor unter RPG → Fischfang & Sushi, ergänzte lokale Debugaktionen und zusätzliche Spielstandsicherung `.pre-sushi-2.bak`. Details: `docs/RPG-SUSHI-2.md`.

## 2.1.18 – Kraft, Ausdauer und HP-Ausrüstung

- Garantierte Hauptwerte auf jedem Ausrüstungsteil: Kraft/Ausdauer nach Seltenheit Common 1/1, Uncommon 2/2, Rare 3/4, Epic 5/7, Legendary 8/10. Beide kommen zusätzlich zur bisherigen Anzahl zufälliger Attribute und Spezialeffekte. Standardumrechnung: 1 Kraft = +1 % Schaden innerhalb des bestehenden Schadenscaps; 1 Ausdauer = +5 MaxHP innerhalb des Ausrüstungs-HP-Caps.
- Alte Ausrüstungsinstanzen werden deterministisch und versioniert ergänzt, ohne IDs, Sekundärwerte, Effekte oder Slots zu verändern. Positive eigene Kraft-/Ausdauerwerte bleiben erhalten. Lesende Inventarabfragen zeigen die ergänzten Werte auf einer Kopie; RPG-Transaktionen speichern sie atomar. Zusätzliche `.pre-core-stats.bak`-Sicherung vor dem ersten Schreibvorgang. Schätze und Verbrauchsgegenstände erhalten keine Kampfwerte.
- Ausrüstung kann maximale Lebenspunkte erhöhen: Seetangstiefel (Common, +5), Korallenhelm (Uncommon, +10), Muschelpanzer (Rare, +20), Abgrundrüstung (Epic, +35), Leviathans Herz (Legendary, +50). Neue Definitionen ergänzen den bestehenden Loot-Pool beim Laden; vorhandene Definitionen und gespeicherte Items werden nicht verändert.
- Nur angelegte, eindeutige Instanzen zählen. Ausrüstungsbonus standardmäßig insgesamt auf 1.000 HP begrenzt, unter HP & Überleben einstellbar. Bonusanzeige in Itemdetails, Ausrüstungsantworten und Charakterwerten; normale Seltenheits-/Einsatzgrenzen gelten weiter.
- Kein Heilen durch An-/Ablegen, kein Wiederbeleben oder Entfernen einer Todessperre. Ablegen begrenzt aktuelle HP auf die neue Obergrenze. Levelaufstiege heilen weiterhin ausschließlich den hinzugewonnenen Level-HP-Betrag. Basis-HP-Items bleiben beim Aktivieren dynamischer Drops unverändert feste Items.

## 2.1.17 – Heist-Startkollision

- Ein gespeicherter Heist-Startbefehl `!start heist` wird nicht mehr zusätzlich als eigener Event-Shortcut registriert. Beseitigt die falsche Heist-gegen-Heist-Kollision beim Modulstart; echte Konflikte und Deaktivierungsschalter bleiben erhalten.

## 2.1.16 – Modulstart trotz fehlender Tip-Anbieter

- Fehlende Tip-Anbieter oder Zugangsdaten pausieren nur den betroffenen Tip-Empfang und verhindern weder Modulstart noch Speichern. Warnhinweise erscheinen im Tip-Status und Botlog. Andere gültige Anbieter bleiben aktiv; gespeicherte Anbieter-Einstellungen werden nicht automatisch überschrieben.
- Eigener Speichern-Knopf unter Timer → Event-Trigger → Tip-Anbieter.

## 2.1.15 – RPG-Überleben, Unterwassergegner und Hot Time

- Kleine Unterwassergegner bei `!dive`: standardmäßig 15 % Begegnungschance (10–20 % einstellbar), fünf einsatzabhängige Gegnerstufen, automatische HP-Kämpfe und EXP bei Sieg. Gegnerbonusbeute verwendet den besseren von zwei normalen Lootwürfen mit unveränderten Legendary-Grenzen. Kampf, HP, EXP und Beute werden mit dem Tauchgang atomar gespeichert; keine zusätzlichen Perlenverluste. Einstellungen im RPG-Bereich unter „Unterwassergegner“. Details: `docs/RPG-UNDERWATER-ENEMIES.md`.
- RPG-HP und Überleben: 100 Basis-HP, +2 MaxHP pro Level; Boss-Gegenangriffe, persistente Todessperre und atomare Belohnungen nur für Überlebende. `!hp`, erweiterte `!stats`, persönliche Chat-Heilung (10 HP/30 s), zeitbasierte Regeneration (2 HP/Minute), konfigurierbare HP-/EXP-Regeln und isolierte Debugaktionen. Laufende HP-Bosse werden nach Neustart aus Metadaten und gespeicherten Angriffen rekonstruiert. Details: `docs/RPG-HEALTH-AND-SURVIVAL.md`.
- Neustartfeste fünfminütige Hot Time nach echten Community-Boss-Siegen, ohne Stapeln oder Auslösen durch administrative Tests. Relativer Basis-Dropbonus und klassifizierte Perlenbelohnungen mit unveränderten Legendary-/Einsatzgrenzen und Caps; keine Boni auf Einsatzrückzahlungen, Admin-/Transfer- oder Salvage-Beträge.
- Grafischer Hot-Time-Bereich mit Haken/Zahlenfeldern, Nachrichten, Restlaufzeit und Beenden. Standardwerte: +50 % relative Basis-Dropchance und +100 % geeignete Perlenbelohnung.
- Isoliertes, standardmäßig deaktiviertes RPG Debug Center für lokale Administratoren: Drop-Editor, Item-Generator, Kampftests, Fortschritt, Sets, Test-Hot-Time und seedbasierte Simulationen. Eigener Testdatenspeicher und Testkonfiguration, keine produktiven Zuschaueränderungen.
- Validierte atomare Konfigurationsspeicherung, Backups und grafische Wiederherstellung. Details und Einschränkungen: docs/RPG-HOT-TIME-AND-DEBUG.md.

## 2.1.14 – Items sicher gegen Perlen zerlegen

- !salvage <Nummer>, !salvage duplicates und !salvage all common zeigen genaue Vorschau und Perlenbetrag; identischer Befehl mit confirm bestätigt binnen 60 Sekunden. Sammelbefehle überspringen Sets, Legendarys, angelegte und geschützte Items; unterschiedliche Rolls sind keine Duplikate.
- Konfigurierbare, deterministische Zerlegewerte nach Seltenheit; Itemdetails zeigen Preis und Warnhinweise. !itemlock <Nummer> [on|off] und !favorite <Nummer> [on|off] schützen Items dauerhaft.
- Entfernung und volle Perlengutschrift erfolgen atomar mit Kontolimitprüfung, Backup und Wiederholungsschutz. Veränderte Inventare/Items/Werte verwerfen die Vorschau; fehlgeschlagene Speicherung erhält Items und Perlen. Wiederverwendbarer lokaler Service für die spätere Extension, keine öffentliche API.

## 2.1.13 – Item-System 2.0, Sets und verständlicher RPG-Chat

- RPG-Chat übersichtlicher: !inv und !gear zeigen kurze nummerierte Einträge mit Slot-Symbol und Ausrüstungsmarkierung. !item <Nummer> [Seite] bündelt alle Werte, deutschen Slot/Typ, Spezialeffekte, Setboni und Status ohne interne IDs.
- Deutsche Attributnamen für Glück, Krit, Mehrfachangriffe und Abenteuerboni. Ausrüstungsbestätigungen enthalten echte Spezialeffekte samt Chance/Stärke und gegebenenfalls den Setnamen aus dem erfolgreichen Transaktionsergebnis. Vollständige Details bleiben auf gezielt abrufbaren Folgeseiten erhalten, keine automatische Nachrichtenserie.

- Zwei vierteilige epische Sets „Wächter der Tiefsee“ und „Schatzjäger des Ozeans“ mit gewürfelten Attributen und kumulativen 2-/3-/4-Teile-Boni. Nur tatsächlich angelegte unterschiedliche Definitionen zählen; alle Boni unterliegen bestehenden Caps.
- Fünf eigenständige Legendarys mit festen Werten und Spezialeffekten, einschließlich Crit-gebundenem Siegel und Dreifachangriff zu je 35 % Zusatzschaden. Neue !sets-Übersicht und !unequip <Slot>; Setfortschritt in Itemdetails und Ausrüstung.
- Quellenbezogener Loot: hochwertige Dives, 2 % Bossloot je Teilnehmer (Legendarys nur mit expliziter Freigabe und Mindest-LP/-Teilnehmerzahl), 0,5 % Ausrüstungsfund bei erfolgreichen Angelaktionen. Keine Änderung der Fisch-/Perlen-Basisauszahlungen. Button „Sets & Legenden vorbereiten“ ergänzt bestehende Konfigurationen. Dokumentation: docs/RPG-SETS-AND-LEGENDARIES.md.

- Item-System 2.0: optionale zufällige Instanzattribute und persistente passive Effekte, zentrale Effektdefinitionen und slotbezogene Vorlagen. Bestehende Items bleiben unverändert; Button „Dynamische Items vorbereiten“ für künftige Funde. Details: docs/RPG-ITEM-SYSTEM-2.md.
- Doppel-/Dreifachangriffe mit klarer Priorität ohne zusätzliche Aktionen, Extra-Itemwurf mit identischem Legendary-Gating und finite Sushi-Erhaltung. Gemeinsame Effektcaps; Perlenbonus maximal 15 % ausschließlich auf Nettogewinne, nicht auf Einsatzrückzahlungen. Automatische datenbasierte Effektbeschreibungen und zusätzliche Sicherung vor erster RPG-Schreiboperation.

- Nummeriertes Inventar: !inv, !item <Nummer>, !equip <Nummer> [Slot] und !gear. Automatische Slotwahl, freie Ringplätze und explizites Ersetzen per ring1/ring2. Veraltete Inventarnummern werden sicher abgelehnt; vollständige IDs und API bleiben erhalten. Details: docs/RPG-NUMBERED-INVENTORY.md.

- !perk <ID> bleibt der Kaufbefehl; !buyperk <ID> ist jetzt ein gleichwertiger Alias mit identischen Rechten/Deaktivierung. Registrierung, Alias-Erkennung und Syntax stammen aus einem gemeinsamen RPG-Befehlskatalog.
- Perk-Erfolg zeigt tatsächlichen Kontostand und nächste Upgrade-Kosten beziehungsweise Maximallevel. Falsche Syntax, unbekannte Perk-IDs und der RPG-Cooldown erhalten konkrete Hinweise. Neue !rpg-Hilfe für RPG-Aufrufe; andere unbekannte Bot-/Custom-Befehle werden nicht abgefangen. Keine Änderung an Preisen oder Balance.

- Inventar zeigt Attribute, kurze Inventarnummer und Ausrüstungsstatus; !item <Nummer|ID> [Seite] ergänzt vollständige Details. Profilabruf nutzt den tatsächlichen Twitch-ID-Kontoschlüssel, damit ein fehlendes redundantes UserId-Feld vorhandene Items nicht versteckt.
- Zentrale vollständige Seitenausgabe statt Kürzen langer Inhalte. !rpgtext [Seite] liest längere gespeicherte Antworten ohne erneute Aktion (15 Minuten, pro Konto). Getrennte Meldungen für Leerstand, Lade-, Daten- und Darstellungsfehler; keine automatischen Reparaturen oder Ersatz-IDs für defekte Items.
- Einheitliche RPG-Emojis, vollständige Zahlenpräzision und erweiterte Status-/Itemtests. Ursachen und Testumfang: docs/RPG-CHAT-DISPLAY.md.

- Konkrete Erfolgstexte für Sushi-Herstellung, Sushi-Aktivierung, Perk-Käufe und Ausrüsten statt „erledigt“. Zutaten, Kosten, erhaltene Menge, Level, Boni, Aktivität und Aufladungen stammen aus dem erfolgreichen Transaktionsergebnis.
- Optionale DisplayName-Felder für eigene Rezept-/Perknamen im RPG-Editor; bestehende Konfigurationen verwenden lesbare Standardnamen beziehungsweise ihre eigene ID als Rückfall.
- Verständliche Leerzustände, Fischvorräte mit Namen statt nur IDs und kompakte Seiten für Inventar, Equipment, Perks und Sushi-Status. Weitere Sushi-Seiten mit !sushi status <Seite>; keine automatische Nachrichtenserie.
- RPG-Angelergebnisse nennen den tatsächlich ausgewählten Fisch und dessen Seltenheit, statt die monetäre Auszahlungsstufe irreführend als legendären Fang zu bezeichnen. Boss-Crits sind im bestehenden Angriffstext erkennbar; optionaler Platzhalter {critical}, leere Meldungen bleiben stumm.
- Keine Balanceänderungen, zusätzlichen Abbuchungen oder Belohnungen. Bestehende Daten, Idempotenz und Cooldowns bleiben erhalten. Keine automatische Veröffentlichung.

## 2.1.12 – Economy 1.2, RPG-Verwaltung und Glücksrad

- Oberflächentests mit gemeinsamen WinForms-/Prozesszuständen isoliert, um konkurrierende Fensterinitialisierungen im Build zu vermeiden. Version 2.1.11 wurde wegen fehlgeschlagener CI-Tests nicht als Download veröffentlicht.

- Glücksrad-Oberfläche vom Giveaway getrennt: Giveaway-Buttons, Teilnehmerliste und Giveaway-Status werden im Glücksrad-Reiter ausgeblendet. Mehr Platz, sichtbare native Vorschau samt Drehanimation und direkt erreichbare PNG-/JPG-Bildauswahl; Bildänderungen erscheinen sofort in der Vorschau. Giveaway-Ansicht wird beim Zurückwechseln wiederhergestellt.

- Neue Standardwerte für Dive-Loot, Einsatzstufen bis 10.000, Rarity-Gewichte, Perk-Kosten und Sushi-Rezepte. Andere Spiele und Einnahmen bleiben unverändert.
- Bestehende Einstellungen werden nicht still überschrieben. Bestätigte Übernahme über „Balancing 1.2 übernehmen“ im RPG-Editor; erst Speichern macht Änderungen dauerhaft. Gekaufte Perks und Inventare bleiben erhalten.
- RPG-Spielerverwaltung: Fischart und Menge gezielt löschen, einzelne Equipmentinstanzen löschen und gegebenenfalls ablegen. Sicherheitsabfrage, frische Kontosicherung vor Löschung, keine Perlenänderungen oder neuen Zuschauer-Löschbefehle.
- Reproduzierbare Simulation mit 2,5 Millionen Dives und drei Zuschauerprofilen, inklusive Bericht zu Progressionszeiten, Bossanteilen und Balancing-Risiken: docs/RPG-ECONOMY-1.2.md. Veröffentlichung nach ausdrücklicher Freigabe.

## 2.1.10 – RPG-Teststand

- Optional aktivierbare RPG-Erweiterung unter Punkte & Minigames → RPG. Inventar, Ausrüstung, Perks, zusätzliche Fischvorräte, Sushi und aktive Buffs werden beim vorhandenen Perlenkonto nach Twitch-ID gespeichert.
- Dive-Loot mit zentralen Einsatzstufen und Legendary-Mindestbetrag, unabhängigen Seltenheitsgewichten, ausrüstbaren Gegenständen, Schätzen und Verbrauchsgegenständen. Kostenloses !dive bei aktiviertem RPG; bisherige bezahlte Perlen-Auszahlungen bleiben Grundlage.
- !inventory/!inventar [Seite], !equipment/!ausrüstung, !equip <Item-ID> <Slot>, !perks, !perk <ID>, !craft <Rezept>, !sushi [Rezept], !stats. In die bestehende Befehlsliste integriert.
- Zentral berechnete, begrenzte Charakterboni wirken bei Loot und Bossangriffen; Perlenbonus erhöht die konfigurierte Auszahlung. Permanente Perks kosten progressiv mehr. Sushi wird hergestellt und separat aktiviert, wirkt standardmäßig drei gültige Aktionen und stapelt identische aktive Buffs nicht.
- Alte Fischsammlungen werden einmalig zusätzlich in den verbrauchbaren RPG-Vorrat übernommen; historische Fangzahlen bleiben erhalten. Unveränderte Sicherung vor der ersten RPG-Speicherung, Abbruch statt Kontenreset bei beschädigter Punktedatei.
- Atomare gemeinsame Speicherung von Solo-Perlen, Loot, Cooldown und Buff-Verbrauch; persistente Aktions-IDs verhindern wiederholte Belohnungen. Kein öffentliches Backend und keine Twitch Extension.
- RPG ist standardmäßig deaktiviert. Zum Testen unter Punkte & Minigames → RPG aktivieren und speichern. Balancing-Editor zunächst JSON-basiert. Noch kein Live-/Langzeit-Balancing; bei Verbindungsabbrüchen ist die Nachzustellung besonderer Chatmeldungen nicht garantiert. Details und Testanleitung: docs/RPG-IMPLEMENTATION.md.

## 2.1.9

- Eigenständiges Glücksrad unter Giveaways → Glücksrad, unabhängig von Teilnehmern und Gewinnern. 2–100 frei bearbeitbare Text-/Aktionsfelder, gleiche Chancen je Feld, einstellbare Drehdauer, Drehen und Anzeige leeren. Keine automatischen Aktionen, Chatnachrichten oder Punkteänderungen.
- Eigene transparente OBS-Browserquelle http://127.0.0.1:18770/ (740 × 850), lokal und ausschließlich lesend. Start über Browserquelle starten oder Drehen; Twitch-/Giveaway-Verbindung nicht erforderlich. Ergebnis wird im Plugin bestimmt und von der Animation dargestellt.
- Optional eigenes PNG-/JPG-Design bis 10 MB und 8192 × 8192 Pixel. Bild wird mittig kreisförmig beschnitten und mitgedreht; Texte bleiben darüber. Lokale Auslieferung ohne Upload; Dateipfad wird nicht im Browserstatus veröffentlicht. Felder, Drehdauer und Bildpfad separat speicherbar.

## 2.1.8

- Timer-Reiter: „Auswahl testen (Chat)“ sendet den markierten Timertext sofort mit der eingestellten Farbe. Nutzt auch ungespeicherte Änderungen und funktioniert unabhängig von Timer-Aktivierung, Intervall und Mindestzuschauern. Keine Änderung am automatischen Zeitplan; Twitch-Verbindung erforderlich. Test sendet in den echten Chat.

## 2.1.7

- Aktiviert-/Auto-Start-Schalter in Fischen, Schatzsuche, Rennen und Bosskampf als echte Checkboxen statt True/False-Zeilen. Werte werden beim Laden und Speichern übernommen; Zahlen und Texte bleiben in der Tabelle editierbar. Checkboxen folgen dem gewählten Theme.

## 2.1.6

- Fischen/Schatzsuche: Live-Gesamtübersicht der vergebenen Chancen, fehlenden/überschüssigen Punkte und des verbleibenden Budgets für die Niete. Einzelne Chancen wieder als einfache Zahlen dargestellt; keine automatische Änderung der Niete.

## 2.1.5

- Einstellbare Chancen für !fish und !dive werden als x/100 angezeigt (z. B. 33/100). Eingabe als Zahl oder x/100 möglich; Summe muss 100/100 ergeben.

- !fish und !dive: vier Ergebnis-Chancen (je 0–100 %, Summe genau 100 %) und Auszahlungsfaktoren (0–100, auch Dezimalwerte) pro Spiel einstellbar. Auszahlung enthält den Einsatz; Bruchteile werden abgerundet. Bisherige Wahrscheinlichkeiten bleiben Standardwerte, auch bei alten gespeicherten Einstellungen. Seltene Fischarten richten sich nach der Ergebnisstufe, nicht dem Auszahlungsfaktor.

- Spielbefehle heißen jetzt !fish <Betrag|all> (Fischen) und !dive <Betrag|all> (Schatzsuche). Befehlsliste, Cooldown-Hinweise, Oberfläche und Dokumentation aktualisiert; Spielregeln und gespeicherte Sammlungen unverändert.

- Boss-Angriffe während des Cooldowns melden die verbleibenden Sekunden. Der Text ist im Bosskampf-Reiter über „Angriffs-Cooldown-Text“ mit {user} und {remaining} anpassbar. !attack und !joinboss teilen die Sperre; blockierte Versuche verursachen keinen Schaden und verlängern sie nicht.

- Boss-Angriff auf !attack umgestellt, !joinboss bleibt Alias. Beide teilen denselben Cooldown. Befehlsliste, OBS-Hinweis und Oberfläche angepasst; bisheriger Standard-Starttext wird aktualisiert, eigene Texte bleiben unverändert.

## 2.1.4

- Wiederholte !fischen- und !schatzsuche-Aufrufe geben während des Cooldowns die verbleibenden Sekunden aus. Der Hinweis ist in den Spiel-Einstellungen anpassbar ({user}, {command}, {remaining}); erneute Versuche verlängern die Sperre nicht und buchen keine Punkte ab.

- Rennstart heißt !start race <Betrag|all> und bleibt für Zuschauer mit genügend Punkten verfügbar. Boss-/Heist-Starts bleiben Mods/Broadcastern vorbehalten. Beitritt bleibt !joinrace; Oberfläche, Befehlsliste und Hinweise entsprechend aktualisiert.

- Rennstart über die Event-Steuerung verwendet den tatsächlichen Twitch-Anzeigenamen und Login des verbundenen Broadcasters statt „Streamer“. Nutzer-ID bleibt unverändert; bei fehlender oder unpassender Kontozuordnung wird kein Rennen gestartet.

- Einstellungen für Fischen, Schatzsuche, Rennen und Bosskampf übernehmen jetzt die Theme-Farben auch in Tabelle, Hilfebereich, Auswahl und Rahmen. Helle Standardflächen und unlesbarer Hilfetext im dunklen Design beseitigt; Theme-Wechsel aktualisieren die Farben.

## 2.1.3

- Lokale transparente OBS-Browserquelle unter http://127.0.0.1:18769/ für Boss-Lebenspunkte, Rennteilnehmer und Countdowns. Nur bei laufendem Minigame-Modul aktiv, ausschließlich lesend und ohne Änderungen an OBS selbst.
- Neuer Reiter „Event-Steuerung“ mit Live-Status, Start-/Abbruchbuttons und kopierbarer OBS-Adresse. Manuelle Rennabbrüche buchen keine Einsätze ab; Bossabbrüche zahlen keine Belohnungen aus.
- Chattexte für Fischen, Schatzsuche, Rennen und Bosskampf direkt in den Spiel-Einstellungen anpassbar. Platzhalter je Nachricht; leere Texte werden nicht gesendet.
- Persistente Fischsammlung mit fünf Arten, seltenen Fängen und größtem Fang. !sammlung zeigt die eigene Sammlung; !profil enthält Rennsiege, Boss-Siege, Artenzahl und größten Fisch. Statistiken werden gemeinsam mit der erfolgreichen Punkte-Abrechnung gespeichert.

- Boss und Heist können nur Mods/Broadcaster starten (auch über die direkten Befehle). Rennen kann jeder berechtigte Zuschauer mit ausreichendem Guthaben starten. !joinboss und !joinrace stehen Zuschauern offen; beim Heist gelten die bestehenden Teilnahmerechte. Niedrigere Rollen-Overrides umgehen die Startsperre nicht.

- Gemeinsamer Event-Start für Mods/Broadcaster: !start boss, !start rennen <Betrag|all> (Alias !start race) und !start heist. Bestehende Aktivierungen, Sperren und Event-Cooldowns bleiben wirksam; Beitritte unverändert mit !joinboss, !joinrace und !joinheist.

- Vier neue Minigames mit eigenen Einstellungsreitern: !fischen und !schatzsuche mit Einsatz, Cooldown und Zufallsbeute; !rennen <Einsatz> mit gemeinsamer Beitrittsphase und !joinrace; !boss und !joinboss für einen kooperativen, einsatzfreien Bosskampf mit geteilter Belohnung.
- Bosskampf optional automatisch alle X Minuten bei laufendem Modul starten. Aktive Kämpfe werden nicht überlagert; Rundenpause und Zeitlimit gelten. Auto-Start ist standardmäßig deaktiviert, Intervall 15 Minuten.
- Rennen: gleicher Einsatz für alle; gemeinsame Abrechnung erst nach Beitrittsphase. Bei zu wenig Teilnehmern, Abbruch oder nicht mehr erfüllten Guthaben-/Tageslimits werden keine Einsätze abgebucht. Gewinne/Verluste werden im Verlauf gespeichert. Offene Runden werden bei Modul-/Programmende verworfen, ohne Einsätze zu verlieren.
- Heist-Beitritt heißt standardmäßig !joinheist statt !join. Bestehende Einstellungen mit !join werden migriert; eigene andere Befehle bleiben erhalten. !joinrace und !joinboss sind unabhängig; bestehende Giveaway-Aliase bleiben unverändert.
- Neue Befehle erscheinen rollenabhängig in !command und unterstützen die Modul-Navigation und Deaktivierung. Einsatzspiele berücksichtigen Tages- und Kontolimits, der kostenlose Bosskampf das Kontolimit.

## 2.1.2

- !command und !commands antworten auch dann auf manuelle Eingaben, wenn der Broadcaster selbst als Bot-Sendekonto verwendet wird. Das interne IsBot-Merkmal hatte diese Eingaben fälschlich unterdrückt. Berechtigungen und Cooldown bleiben aktiv; eigene Listenantworten lösen keine neue Ausgabe aus.

## 2.1.1

- Alte und neue Befehlslisten-Registrierung zusammengeführt: !command und !commands verwenden genau einen aktiven Handler und einen Registry-Eintrag. Ein zuvor konfigurierter Listenbefehl bleibt als Alias erhalten; Deaktivierung wird berücksichtigt.

- !command (Alias !commands) zeigt eine dynamische Liste aktivierter, sichtbarer Befehle passend zur Rolle des Aufrufers, inklusive Custom Commands und Follower-Prüfung. !sr wird Zuschauern nur mit aktiver !srpermit-Freigabe angezeigt; Musik-Verwaltung bleibt Mods/Broadcaster vorbehalten. Lange Listen sind über !command <Seite> abrufbar; ein globaler 5-Sekunden-Cooldown verhindert Spam.

## 2.1.0

- Rückkehr auf FUNJII live geprüft: „Narcoleptic“ bei 2:57 vorgemerkt, „Holz“ sofort gestartet und nach dessen Testende zu „Narcoleptic“ zurückgekehrt; Wiedergabe von Spotify bestätigt.

- Vor einem Sofortstart wird der laufende Song mit Position, Gerät und Playlist-/Album-Kontext im Arbeitsspeicher vorgemerkt. Nach dem erkannten natürlichen Ende des Wunschs wird der unterbrochene Song an dieser Position fortgesetzt. Weitere Sofortstarts behalten den ursprünglichen Rückkehrpunkt. Manuelle Kontext-/Gerätewechsel brechen die Rückkehr ab; Pause löst keine Rückkehr aus. Neustart oder Modulstopp verwirft den Rückkehrpunkt. Die vollständige Spotify-Warteschlange wird nicht rekonstruiert.

## 2.0.32

- !srnow und „Nochmal abspielen“ starten den gewünschten Song direkt über seinen Album-Kontext und den exakten Track als Startpunkt. Dies umgeht das beobachtete Desktop-Verhalten, bei dem Tracklisten-Startbefehle trotz HTTP 204 die Wiedergabe leeren. Live-Test mit „Holz“ auf FUNJII erfolgreich bestätigt. Kein Einreihen/Überspringen; nach dem Song kann Spotify im Album fortfahren.

## 2.0.31

- Zuschauer ohne aktive !srpermit-Freigabe erhalten bei !sr und dessen Aliasen einen Hinweis auf die fünfminütige Moderator-Freigabe und Musikwünsche über Kanalpunkte.

- „Erneut in die Warteschlange hinzufügen“ nutzt nun wie reguläre Warteschlangen-Wünsche den Spotify-Einreihaufruf und erstellt einen neuen Eintrag „In Warteschlange“, ohne den laufenden Song zu unterbrechen. „Nochmal abspielen“ und !srnow bleiben Sofortstarts.

- Spotify-Sofortstart und erneute Wiedergabe senden den JSON-Startbefehl gepuffert mit bekannter Inhaltslänge. !srnow meldet „Jetzt läuft“ erst nach bestätigter Wiedergabe; ausbleibende Bestätigungen werden als fehlgeschlagener Start protokolliert.

- !srnow umgeht für Moderatoren und Broadcaster die Duplikatsperre und den Explicit-Filter. Blacklists und andere Grenzen bleiben aktiv. Normale !sr-Wünsche einschließlich !srpermit-Freigaben behalten den Explicit-Filter und die Duplikatprüfung.

## 2.0.30

- !srnow <Songname oder Spotify-Link> startet Musikwünsche für Moderatoren und Broadcaster im Modus „Sofort“. Der globale Wiedergabemodus bleibt unverändert; !srpermit gewährt keinen Zugriff auf !srnow.

- Rechtsklick → „Erneut in die Warteschlange hinzufügen“ legt nach bestätigtem Spotify-Start einen neuen Musikwunsch-Eintrag mit „Sofort“ an. Der ursprüngliche Verlauf und die globale Wiedergabeeinstellung bleiben erhalten; es wird keine Kanalpunkte-Einlösung erneut verarbeitet.

## 2.0.29

- Moderatoren und Broadcaster können mit !srpermit @name einem Nutzer fünf Minuten lang Musikwünsche per !sr und dessen Aliasen erlauben. Verwaltungsbefehle bleiben gesperrt; erneute Freigabe setzt die Frist auf fünf Minuten. Freigaben enden beim Modulneustart.

## 2.0.28

- „Nochmal abspielen“ startet ausdrücklich bei Position 0 und prüft den vollständigen Spotify-Playerstatus. Bei ausbleibender Bestätigung werden Konto, Gerät und Wiedergabestatus protokolliert.

- Das Rechtsklick-Menü der Musikwunsch-Warteschlange enthält „Zur Blacklist hinzufügen“ mit Song, Künstler und Nutzer. Die ausgewählte Sperre wird gespeichert.

## 2.0.27

- „Songlink kopieren“ im Rechtsklick-Menü der Musikwunsch-Warteschlange kopiert den Spotify-Link des ausgewählten Songs zur Fehlersuche.

- Erneute Spotify-Wiedergabe wartet auf die tatsächliche Aktivierung des Zielgeräts und berücksichtigt marktbedingte Track-Ersatzversionen. Fehlermeldungen nennen das Zielgerät und den aktuell gemeldeten Song.

## 2.0.26

- „Nochmal abspielen“ ermittelt das Spotify-Gerät neu, aktiviert es bei Bedarf und prüft den tatsächlichen Wiedergabestart. Erst nach Bestätigung wird der Song als laufend markiert; Fehler werden direkt angezeigt.

- Abgelehnte, fehlgeschlagene und übersprungene Musikwünsche blockieren einen erneuten Versuch nicht mehr als vermeintliche Warteschlangen-Duplikate. Tatsächlich eingereihte, laufende und kürzlich abgeschlossene Songs bleiben geschützt.

## 2.0.25

- Die gespeicherten Explicit-Freigaben sind als bearbeitbare Ausnahmeliste unter Musikwünsche → Filter & Limits sichtbar. Track-IDs können entfernt werden, um einzelne Freigaben zu widerrufen.

- Rechtsklick unter Musikwünsche → Warteschlange: „Nochmal abspielen“ startet den ausgewählten Song erneut.
- „Zukünftig erlauben“ speichert für explizite Songs eine Ausnahme anhand der Spotify-Track-ID. Der allgemeine Explicit-Filter und andere Sperren bleiben aktiv.

## 2.0.24

- Alle Musikwunsch-Chatbefehle einschließlich !sr, !songrequest und Aliasen sind auf Moderatoren und Broadcaster begrenzt. Niedrigere Rollen in der Command-Übersicht können diese Begrenzung nicht aufheben. Kanalpunkte-Belohnungen bleiben unverändert verfügbar.

- Wünsche über !sr und andere konfigurierte Musikwunsch-Chatbefehle besitzen eigene Erfolgstexte für Warteschlange und sofortige Wiedergabe, getrennt von Kanalpunkte-Belohnungen.

- Musikwunsch-Chattexte und Skip-Nachrichten unterstützen {role}: Broadcaster, Moderator, VIP oder Zuschauer. Rollen von Kanalpunkte-Einlösern werden über Twitch abgefragt; dafür ist eine erneute Autorisierung nötig. Fehlgeschlagene Rollenabfragen erscheinen als „Unbekannt“.

- Unter Musikwünsche → Chattexte ist die Nachricht „Song übersprungen“ mit dem Platzhalter {moderator} einstellbar; sie wird nach erfolgreichem Skip per Moderator-Command oder Plugin-Button gesendet, wenn das Musikwunsch-Modul läuft.

- Ein dauerhaft sichtbarer Button „Aktuellen Song überspringen“ im Kopfbereich von Musikwünsche überspringt den laufenden Spotify-Song unabhängig vom geöffneten Unterreiter.
- Die Skip-Aktion verhindert Mehrfachklicks und zeigt fehlende Spotify-Verbindungen sowie Fehler direkt an.

## 2.0.23

- Rechtsklick auf einen Command in der Übersicht öffnet ein Menü mit „Modul“ zum passenden Einstellungsbereich und „Deaktivieren“ zum gespeicherten Abschalten des Commands und seines zugehörigen Moduls.

- Custom Commands und Chat-Timer bieten pro Eintrag die Auswahl zwischen normalem Chat und Ankündigungen in Blau, Grün, Orange, Lila oder der Twitch-Kanalfarbe.
- Farbige Ankündigungen benötigen Moderatorrechte und eine erneute Twitch-Autorisierung für die zusätzliche Berechtigung.
- Die Dashboard-Schnellaktionen enthalten wieder ein Clip-Kanalfeld: leer für den eigenen Kanal oder ein anderer Kanalname zum Testen.
- „Clip abspielen“ ruft die Wiedergabe direkt auf und weist bei gestoppten Modulen darauf hin, diese zuerst zu starten.
- „Lokaler Player“ wird bei wenig Platz auf zwei Zeilen angezeigt, damit die Beschriftung vollständig lesbar bleibt.

## 2.0.22

- Das Dashboard bietet jetzt direkte Aktionen zum Starten, Stoppen und vollständigen Neustarten aller Module.
- Der Modulneustart beendet die laufenden Dienste kontrolliert und startet das Plugin anschließend erneut.

## 2.0.21

- „Chat testen“ im Dashboard ruft die Diagnose-Testnachricht nun direkt auf und funktioniert auch bei geschlossenem Diagnose-Reiter.
- Die Schnellaktion „Event-Trigger“ wurde durch „Minigames“ ersetzt und öffnet unmittelbar den Bereich Punkte & Minigames.

## 2.0.20

- Twitch, OBS Studio, EventSub und Player erhalten im Dashboard wieder ihre jeweils richtige Grafik und Bezeichnung.
- Die doppelte Erzeugung der Service-Karten wurde entfernt und abgeschnittene Statistikwerte wurden korrigiert.

## 2.0.19

- Das Dashboard orientiert sich mit kompakter Navigation, großzügigeren Karten und eigenen Grafiken für Twitch, OBS, EventSub und Player stärker am neuen Mockup.
- Versionsanzeige und Update-Suche stehen übersichtlich im mittleren Kopfbereich.
- Clips und Auto Clip sind unter dem gemeinsamen Hauptreiter „Clips“ zusammengeführt.
- Die Dashboard-Aktion „Chat testen“ verwendet jetzt direkt die Diagnose-Testnachricht.
- Für jeden Event-Trigger kann eine eigene lokale WAV-Datei ausgewählt, angehört und dauerhaft gespeichert werden.

## 2.0.18

- Das Dashboard wurde vollständig überarbeitet und zeigt Systemstatus, Schnellaktionen, Tagesaktivität und letzte Ereignisse übersichtlich an.
- Die Akzentfarbe lässt sich unter „Allgemeine Einstellungen“ frei per RGB-Werten, Hex-Code oder Windows-Farbauswahl festlegen und dauerhaft speichern.
- Die Seitenleiste kommt ohne die zusätzliche Kategorie „Weitere Module“ aus.

## 2.0.17

- Jeder Event-Trigger besitzt jetzt eine eigene lokale Soundauswahl für den Streamer.
- Enthalten sind die Sounds Hinweis, Erfolg, Glocke, Achtung und Frage sowie die Option „Kein Sound“.
- Jeder Sound kann direkt in den Einstellungen über „Anhören“ getestet werden.

## 2.0.16

- Neue Event-Trigger mit frei einstellbaren Chatantworten für Follows, Tips, Abonnements, Geschenkabos, Cheers/Bits und Werbepausen.
- Tip-Anbindungen für StreamElements, Streamlabs, Ko-fi und TipeeeStream.
- Unterstützt Mindestbeträge, Providerstatus und individuelle Platzhalter je Ereignis.
- StreamElements wird über den Astro-WebSocket angebunden; Ko-fi und TipeeeStream unterstützen abgesicherte Webhook-Relays.

## 2.0.15

- Twitch-Werbepausen werden automatisch über EventSub erkannt.
- Für Werbepausen kann ein frei konfigurierbarer Text in den Chat gesendet werden.
- Der Streamer kann lokal per Windows-Benachrichtigung und optionalem Ton informiert werden; es wird kein Whisper versendet.
- Texte unterstützen Platzhalter für Dauer, Typ, Uhrzeit und Auslöser der Werbung.

## 2.0.14

- Neuer Hauptreiter „Timer“ für automatische Chatnachrichten mit individuellen Intervallen.
- Jeder Timer kann erst ab einer frei wählbaren Mindestzuschauerzahl senden.
- Timer lassen sich einzeln sowie als gesamtes Modul aktivieren und dauerhaft speichern.
- Der Custom-Commands-Bereich besitzt einen gut sichtbaren Löschen-Button und mehr Platz ohne Scrollleiste.

## 2.0.13

- Neue Option, mit der stille Zuschauer und Nutzer im `!lurk`-Modus den normalen Anwesenheitssatz erhalten.
- Der separate Lurker-Satz bleibt optional für bestehende Konfigurationen verfügbar.

## 2.0.12

- Neuer eigener Reiter „Daily“ unter „Punkte & Minigames“.
- Punkte pro `!daily` sowie Erfolgs- und Cooldown-Text sind dort frei einstellbar.
- Unterstützte Textplatzhalter werden direkt in der Oberfläche angezeigt.

## 2.0.11

- Durch den Explicit-Filter abgelehnte Twitch-Musikwünsche werden automatisch storniert.
- Twitch erstattet dadurch die eingesetzten Kanalpunkte, auch wenn die allgemeine Auto-Stornierung deaktiviert ist.

## 1.6.8

- Zentraler Healthcheck für Twitch Chat, Command-Dispatcher, Punktesystem, Minigame und Gamble.
- Automatischer Minigame-Neustart ohne doppelte Chat-Eventhandler, begrenzt durch Restart-Cooldown und Neustartfenster.
- Neue GUI-Statusanzeige sowie „Module prüfen“ und „Module neu starten“.
- Healthcheck-Intervall, Auto-Restart, maximale Neustarts, Restart-Cooldown und Gamble-Prüfung sind konfigurierbar.
- `!gambel` wird als Alias für `!gamble` akzeptiert.
- Unbekannte Commands werden nicht mehr fälschlich als deaktiviertes Modul gemeldet.
- Gamble-Fehler werden mit Stacktrace protokolliert und freundlich im Chat beantwortet, ohne das Modul abzuschalten.
- Minigame-Heartbeat, Task-Ausfallerkennung und sichere manuelle Wiederherstellung ergänzt.

## 1.6.7

- Behebt den Überlauf bei `!gamble all` und großen Punkteständen.
- Punktestände verwenden jetzt 64-Bit-Werte (praktisch unbegrenzt bis 9.223.372.036.854.775.807).
- `!gamble all` setzt pro Runde höchstens 10.000.000 Punkte.
- Jackpot-, Tageslimit- und Statistikberechnungen sind gegen Überläufe abgesichert.
- Neuer Broadcaster-Befehl `!dumpjackpot` leert den Jackpot auf den konfigurierten Startwert.
- `!addpoints all <Betrag>` vergibt als Admin Punkte an alle gespeicherten Zuschauer.
- `!addjackpot <Betrag>` (Alias `!addjockpot`) erhöht den Jackpot überlaufsicher.
- Heist-Cooldown-Antworten zeigen die verbleibenden Minuten und Sekunden.

# Changelog

## 1.6.6

- Absturz beim automatischen Scrollen der Moderations-Chat-Tabelle behoben.
- Auto-Scroll wird nur ausgeführt, wenn die Tabelle sichtbar ist und tatsächlich Platz für mindestens eine Zeile besitzt.
- Kurzzeitige WinForms-Layoutzustände beim Tabwechsel oder Verkleinern werden defensiv abgefangen.
- Moderations-Chat nutzt feste Zeilenhöhen und deaktivierten Textumbruch.
- Regressionstest verarbeitet 300 Nachrichten bei unsichtbarer Tabelle und bestätigt das 250-Zeilen-Limit.


## 1.6.5

- Texteingabe und Senden-Button sind im offiziellen Twitch-Chat und im separaten Popout wieder dauerhaft vorhanden.
- Neuer Custom-Command-Import für JSON, CSV und eingefügten Text mit flexiblen StreamElements-ähnlichen Feldnamen.
- Vorschautabelle mit Quelle, Status, Konfliktmeldung und den Aktionen Überspringen, Überschreiben oder Umbenennen.
- Case-insensitive Kollisionsprüfung gegen System-, Punkte-, Minigame-, Musik-, Clip-, Giveaway- und bestehende Custom Commands einschließlich Aliase.
- Eingebaute Commands können nicht überschrieben werden; sichere Ein-Klick-Option importiert nur konfliktfreie Einträge.
- Importzusammenfassung und Fehler werden im bestehenden Log protokolliert.
- Vorschauzeilen besitzen feste Höhen; lange oder mehrzeilige Antworten und Konflikttexte werden sicher gekürzt, während der vollständige Originaltext erhalten bleibt.


## 1.6.4

- Offizielle 7TV- und BetterTTV-Browser-Erweiterungen lassen sich im WebView2-Twitch-Chat aktivieren.
- Erweiterungen werden beim ersten Verbinden aus den offiziellen GitHub-Releases geladen, lokal zwischengespeichert und im gemeinsamen Chatprofil verwendet.
- Animierte 7TV-/BTTV-Emotes bleiben als GIF/WebP animiert und werden nicht in statische Vorschaubilder umgewandelt.
- Eigene GUI-Schalter für 7TV und BTTV; Fehler einer Erweiterung blockieren den Twitch-Chat nicht.
- BetterTTV-Pakete werden vor der Installation mit SHA256 geprüft.


## 1.6.3

- Neuer eigener Livechat-Bereich in der Sidebar mit bisherigem Plugin-Chat und offiziellem Twitch-Popout-Chat.
- WebView2 lädt den Chat des konfigurierten Twitch-Kanals und zeigt verständliche Verbindungs- und Fehlerzustände.
- Separates, einzelnes Popout-Fenster mit Direktfokus, TopMost sowie dauerhaft gespeicherter Größe und Position.
- Ungültige oder nicht mehr sichtbare Popout-Positionen werden automatisch zentriert.
- Vorbereitete IChatProvider-Architektur und Emote-Modelle für einen späteren nativen Twitch-/BTTV-/7TV-/FFZ-Chat.




## 1.6.2

- Custom Commands mit frei wählbarem Befehl, Aliasen, Chatantwort, Rollen und Cooldowns.
- Deaktiviertes !raid-Beispiel mit der Funjii-Otter-Chatantwort ist vorbereitet.
- Berechtigungen bestehender Commands lassen sich pro Befehl zwischen Zuschauer und Broadcaster überschreiben.
- Livechat erhält eine Eingabezeile zum direkten Schreiben in den Twitch-Chat.
- Doppelte Subscriber- und erhöhte VIP-Gewinnchancen wurden aus Giveaways entfernt.

## 1.6.1

- Livechat-Funktionsleisten passen sich an Fensterbreite und Windows-Skalierung an; alle Einstellungsleisten bleiben scrollbar erreichbar.
- 7TV nutzt Windows-kompatible GIF-Dateien inklusive statischem Fallback; BTTV erkennt animierte Emotes zuverlässig.
- Discord-Clip-Embeds verwenden jetzt Titel, Link und Vorschaubild des tatsächlich veröffentlichten Twitch-Clips.
- Leere Discord-Embed-Felder werden verhindert und durch einen sicheren Platzhalter ersetzt.

## 1.6.0

- Neuer read-only Livechat-Reiter neben Log und Clip-Historie auf Basis der bestehenden EventSub-Chatverbindung.
- Thread-sichere, strikt begrenzte Chat-Historie mit Suche, Pause, Auto-Scroll sowie Command-, Bot- und Systemfiltern.
- Rollen, Badges, Twitch-Benutzerfarben und Twitch-Emote-Fragmente werden aus vorhandenen Chatdaten übernommen.
- Vorbereitete und optional aktivierbare BTTV- und 7TV-Anbieter mit Cache, Fehler-Fallback und ohne API-Aufrufe im deaktivierten Zustand.
- Gebündelte UI-Aktualisierung für stabilen Betrieb bei hohem Chat-Aufkommen.
- Neue Tests für Filter, Limits, Pause, Konfigurationskorrektur, externe Emotes und hohe Nachrichtenmengen.

## 1.5.9

- Neues Twitch-Minispiel Duel mit `!duel <user> <punkte|all>`, `!accept` und `!deny`.
- Thread-sichere Einsatzreservierung, Rückerstattung bei Ablehnung, Timeout und Bot-Stopp sowie atomare Pot-Auszahlung.
- Fairer 50/50-Modus oder konfigurierbare Gewinnchance für Herausforderer.
- Neuer Duel-Reiter mit Commands, Limits, Rollen, Chatvorlagen, Live-Status, Abbruch und Testmodus.
- Duel-Commands in zentraler CommandRegistry, Commands-Seite, Kollisionsprüfung und `!commands` integriert.
- Umfangreiche Unit- und Integrationstests für Punkteerhalt, Race Conditions, All-In und Command-Kollisionen.

## 1.5.8

- `!heist` reagiert wieder zuverlässig im Twitch-Chat.
- Broadcaster mit identischem Botkonto werden nicht länger fälschlich blockiert.
- Moderatoren dürfen einen Heist starten.
- Deaktivierte oder nicht erlaubte Heist-Aufrufe liefern eine verständliche Chatantwort.
- Der Test-Heist sendet drei klar markierte Testmeldungen in den Chat, ohne Punkte oder Jackpot zu verändern.

## 1.5.7

- Startabsturz der neuen Commands-Tabelle behoben.
- Rekursive Aktualisierung des Commands-Modulfilters verhindert.
- Automatisches Startfehler-Log unter `%LOCALAPPDATA%\RaidClipPlugin\logs` ergänzt.
- GUI-Starttest ergänzt, damit Fehler beim Fensteraufbau vor Releases erkannt werden.

## 1.5.6

- Neues gemeinschaftliches Heist-Minispiel mit konfigurierbaren Commands, Beitrittsphase, Berechtigungen und Cooldowns
- Einstellbare Erfolgschance von 0 bis 100 Prozent mit kryptografisch sicherem W100
- Vollständige, atomare Jackpot-Verteilung ohne verlorene Restpunkte
- Eigener Heist-Reiter mit Chattexten, Live-Status, Testmodus und Abbruchfunktion
- Zentrale CommandRegistry, neuer Commands-Reiter, Kollisionsprüfung sowie TXT-/JSON-Export
- Neuer frei benennbarer Chatbefehl !commands mit Seiten, Modulfiltern, Rollenprüfung und Spam-Schutz
- Neue Unit- und Integrationstests für Heist-Regeln, Jackpot-Auszahlung, Rollen und Command-Kollisionen

## 1.4.2

- Minigame-Chat startet wieder zuverlässig bei gleichzeitig aktiven Musikwünschen
- Musikwünsche und passive Minigame-Ereignisse teilen sich eine EventSub-Verbindung
- Chat-Verbindung wird beim Start priorisiert

## 1.4.1

- Neuer vollständiger Stream-Start-Check mit 18 unabhängigen Prüfungen
- Konfigurierbares Check-Profil, Wiederholung, sichere Diagnose und Ergebnisexport
- Bestätigter Streamstart mit optionaler Startszene und OBS-Streaming
- Der Jackpot wird ausschließlich bei einer gewürfelten 100 in `!gamble` ausgeschüttet
- Die zufällige Jackpot-Gewinnchance wurde aus Logik und GUI entfernt
- Der Broadcaster erhält nun Chat-, Anwesenheits- und Lurkerpunkte
- Europäisches Roulette mit Farben, Gerade/Ungerade, 1–18, 19–36 und exakten Zahlen
- Konfigurierbare Roulette-Auszahlungen, Einsatzgrenzen und eigener Cooldown

## 1.4.0

- Schaltflächen zeichnen Beschriftungen zuverlässig und vollständig, auch im dunklen Theme
- Größere Navigation, Tabs und Aktionsbereiche verhindern abgeschnittene Texte
- Fenster und Einstellungsbereiche skalieren sauberer bei langen deutschen Beschriftungen
- Neues optionales Modul „Musikwünsche“ mit Spotify PKCE und sicherer Tokenablage
- Twitch-Kanalpunkte-EventSub, persistente Deduplizierung und Statusupdates
- Spotify-Suche, Queue/Sofortwiedergabe, Geräteauswahl, Filter und Blacklists
- Konfigurierbare Chattexte und Moderator-Commands
- Warteschlangen-GUI mit Wiederholen, Abspielen, Überspringen und Sperraktionen
- Automatisierte Tests für Links, Filter, Limits, Cooldowns und Deduplizierung

## 1.3.0

- Checkboxen im dunklen GUI-Theme wieder klar sichtbar und bedienbar
- Konfigurierbare Punkte-Währung mit Einzahl, Mehrzahl und Live-Vorschau
- Auswahl zwischen !punkte, !points, !perlen und eigenem Abfrage-Command
- Konfigurierbare Punkte-Blacklist mit sicheren Standard-Botkonten
- Zentrale Blacklist-Prüfung für Anwesenheit, Chat, Events, Casino und Vergaben

- Anwesenheitspunkte für stille Zuschauer und Lurker
- Neue Commands `!lurk`, `!unlurk` und `!give all <punkte>`
- Garantierte vollständige Jackpot-Auszahlung bei einer gewürfelten 100
- GUI-Einstellungen bleiben während der Plugin-Verbindung bedienbar
- Neues RaidClip-Anwendungslogo für Fenster, EXE und Installer
- Vollständig überarbeitete dunkle GUI mit roter RaidClip-Sidebar, Statuskarten und dunklen Tabs

## 1.2.7

- `!gamble all` setzt alle verfügbaren Punkte auf einen Wurf
- `!give @name <punkte>` überträgt Punkte sicher zwischen Zuschauern
- `!addpoints @name <punkte>` erzeugt als Broadcaster oder Mod neue Punkte

## 1.2.6

- Modulares Minigame-System mit gemeinsamer Punkte-Datenbank
- Passive Punkte für Watchtime, Chat, Follow, Sub, Raid und Channel Rewards
- Neue Commands !daily, !top, !rang und !profil
- Optionale Casino-Spiele Coinflip und Slots
- Dauerhafter Jackpot mit konfigurierbarer Chance und Einzahlung
- Kontolimit sowie tägliche Spiele-, Verlust- und Gewinnlimits
- Sieben übersichtliche Minigame-Reiter mit Rangliste und Historie
- Sicherer JSON-Export und -Import mit automatischem Backup

## 1.2.5

- Neues optionales Modul „Chat-Minigame“ mit eigener Sidebar-Seite
- Dauerhaftes Punktesystem für aktive Chat-Zuschauer
- !punkte mit User-Cooldown
- !gamble mit W100, Einsatzgrenzen und konfigurierbaren Auszahlungen
- Admin-Commands add/remove/set für Broadcaster und Mods
- Globaler Command-Cooldown und thread-sichere lokale Speicherung
- Punktedaten können in der GUI zurückgesetzt werden

## 1.2.4

- Neue linke Kachelnavigation
- Raid Clip und Chat-Moderation sind vollständig getrennte GUI-Seiten
- Eigene Einstellungen und Statusanzeige pro Modul
- Übersichtlicheres Layout für den Live-Betrieb

## 1.2.3

- Eigenständiges, optionales Modul „Chat-Moderation“
- Twitch-Chat über EventSub mitlesen und in einem eigenen GUI-Tab anzeigen
- Timeout, Ban und Nachrichtenlöschung direkt pro Chatzeile
- Optionaler Wortfilter, der Treffer sicherheitshalber nur löscht
- Optionale Whitelist für Streamer, Moderatoren und VIPs
- Moderationsfehler werden isoliert geloggt und blockieren keine Raid-Clips
- Neue Twitch-Berechtigungen für Chat und Moderation

## 1.2.2

- Sicherer Einrichtungsassistent für Twitch-Zugangsdaten
- OBS-Browserquelle auf Knopfdruck erstellen
- GitHub-Auto-Update mit SHA256-Prüfung

