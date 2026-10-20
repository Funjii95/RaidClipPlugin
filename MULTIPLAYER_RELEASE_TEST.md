# Funjii Otter RPG – Multiplayer- und Release-Test

Stand: 19.10.2026. **Empfehlung: NICHT RELEASE-BEREIT.**

Die automatisierte Prüfung ist bestanden. Die verlangte echte Abnahme mit zwei unterschiedlichen Twitch-Anmeldungen ist nicht erfolgt. Ein laufender Prozess und ein verbundenes Panel ersetzen diesen Nachweis nicht.

## 1. Geprüfter Stand und Grenzen

- Arbeitsstand: Repository unter outputs/RaidClipPlugin-FinalRelease, einschließlich bereits vorhandener Integrationsänderungen; kein sauberer Git-Release-Tag. Vorher vorhandene Änderungen wurden nicht verworfen.
- Hosted-Test-Paket 0.0.2: Funjii-Otter-RPG-0.0.2-Boss-Refresh.zip; MD5 8b97f615688d6842a1e8ffbe0eb06693. Genau dieser Hash wurde im echten Twitch-Panel-Frame beobachtet.
- Bestehende rc1-Dienste: Backend 00010-7m5, Publisher 00017-ndc laut letztem Aktivierungsnachweis. Keine neue Cloud-Bereitstellung in diesem Auftrag.
- Zunächst kein Plugin-Prozess; während des Auftrags startete der Nutzer das installierte Plugin. Prozess und Pfad unter AppData/Local/Programs/RaidClipPlugin wurden danach festgestellt. Version und Prozessdaten stehen in LIVE_PLUGIN_READONLY.json.
- Echte Twitch-Beobachtung danach: Kanal offline; angemeldetes Profil Funjii sichtbar; Anzeige „Plugin online · RPG spielbereit“, Perlen, HP und effektive Charakterwerte geladen. Das belegt Verbindung und Lesbarkeit, nicht die erfolgreiche Ausführung einer Spielaktion.
- Ein zweites unabhängig angemeldetes Twitch-Konto stand für die Abnahme nicht zur Verfügung. Keine fremde Anmeldung simuliert, keine Zuschauer-JWTs hergestellt oder ausgelesen.
- Die Navigation zur Bossansicht scheiterte in der Browsersteuerung an der Eingabe in den verschachtelten Twitch-Frame. Daraus wird weder ein Gameplay-Defekt noch eine bestandene Bossabnahme abgeleitet.
- Keine Käufe, Angriffe, Minispiel-Einsätze, Kontoresets oder Spielstandrestores auf echten Konten durchgeführt. Plugin nicht eigenmächtig beendet.
- Keine neuen Features, UI-Neugestaltung, Wirtschaftsänderung, GitHub-Veröffentlichung oder öffentliche Twitch-Einreichung.

## 2. Tatsächlich verwendete Konten

| Konto | Umgebung | Tatsächliche Verwendung |
|---|---|---|
| 1001 / SYNTHETIC | Isolierte automatische Tests, temporäre Dateien und lokale Testserver | Bossangriffe, Inventar, Perks, Minispiele, Relay-/Snapshot-Prüfungen |
| 1002 / TARGET bzw. SYNTHETIC-B | Dieselbe isolierte Testumgebung | Zweiter Boss-Spieler, fremde Items, parallele Perks/Minispiele, Duell/Heist |
| Weitere Fixture-IDs, z. B. a/b | Bestehende lokale Regeltests | Boss-Belohnungen, Rennen und andere ursprüngliche Regelprüfungen |
| Funjii | Echter Twitch Hosted Test | Nur Lesen von Profil, Verbindungsanzeige und ausgeliefertem Paket |
| Coner123 und übrige freigegebene Tester | Echter Twitch Hosted Test | In diesem Auftrag NICHT für Spielaktionen verwendet |

Die numerischen synthetischen IDs sind lokale Testfixtures. Sie wurden nicht in die reale Cloud importiert, freigeschaltet oder aus Datenschutz-Tombstones wiederhergestellt.

## 3. Ergebnismatrix

„BESTANDEN (automatisch)“ bezieht sich auf isolierte Tests, nicht auf Twitch-Mehrspieler-Abnahme.

| Prüfung | Automatisch | Echter Hosted Test |
|---|---|---|
| Zwei Spieler: gleiche Boss-ID und HP nach ihren Angriffen | BESTANDEN | OFFEN |
| Schaden dem richtigen Spieler zugeordnet, Summe stimmt exakt mit HP-Abzug überein | BESTANDEN | OFFEN |
| Wiederholung derselben Boss-ActionId, auch mit neuem Executor | BESTANDEN | OFFEN |
| Boss-Tod, Teilnehmerbelohnung und keine zweite Auszahlung | BESTANDEN in vorhandenen ursprünglichen Boss-Regeltests | OFFEN |
| Nächster Boss: neue ID, volle HP, persönliche Schadensanzeige 0, alte bestätigte ActionId ohne erneuten Schaden | BESTANDEN | OFFEN |
| Boss-Polling trotz HTTP-500 der Charakterstatus-Abfrage bzw. ohne status-Unterstützung | BESTANDEN | OFFEN am aktiven Boss |
| An-/Ablegen für zwei Konten, fremde Item-ID abgelehnt | BESTANDEN | OFFEN |
| Gleichzeitiger Perkkauf beider Konten plus doppelte Anfrage eines Kontos | BESTANDEN | OFFEN |
| Perlen und Perklevel nach neuem Store-Objekt erhalten | BESTANDEN | OFFEN nach Browser-Neuladen |
| Zu wenig Perlen, fehlendes Item, Cooldown | BESTANDEN | OFFEN |
| Wiederholter regulärer Perkkauf / Wiederaufnahme nach verlorener Bestätigung | BESTANDEN | OFFEN |
| Gamble, Coinflip, Slots, Roulette: parallele Anfragen, kontogenaue Belege, Wiederholungen | BESTANDEN | OFFEN |
| Rennen: bestehende Runde, veraltete Event-ID abgelehnt; Gewinnerregeln | BESTANDEN in vorhandenen Tests | OFFEN |
| Duell: Gegenpartei, Einsatzreservierung, Annahme/Ablehnung, einmalige Abrechnung/Erstattung | BESTANDEN in vorhandenen Tests | OFFEN |
| Heist: Teilnahme, Auszahlung, Absturz-/Commit-Wiederherstellung | BESTANDEN in vorhandenen Tests | OFFEN |
| Offline-Stream + frischer spielbereiter Plugin-Heartbeat | BESTANDEN | Verbindung/Profil BESTANDEN; echte Aktion OFFEN |
| Live-Stream + spielbereites Plugin | BESTANDEN auf Relay-/Regeltestebene | OFFEN |
| Plugin getrennt / Heartbeat veraltet / RPG nicht bereit | BESTANDEN | OFFEN, laufendes Plugin nicht gestoppt |
| Neustart, alte Sitzung, Timeout mit unklarem Ergebnis, keine blinde Wiederholung | BESTANDEN | OFFEN |
| Automatischer Snapshot nach echter autoritativer Testaktion | BESTANDEN über lokale HTTP-Testserver und Firestore-Transportdouble | OFFEN über echte Twitch-Konten/Cloud |
| Anonyme echte Cloud-Endpunkte verweigern Profildaten/Aktionen | BESTANDEN: HTTP 401 | BESTANDEN als Live-HTTP-Prüfung |
| Backend-Liveness | BESTANDEN: HTTP 200 | BESTANDEN als Live-HTTP-Prüfung |

Nicht behauptet: gleichzeitige Echtzeitkonsistenz zweier Twitch-Ansichten, erfolgreiche echte Boss-Auszahlung oder realer Cloud-Ausfall unter Last. Wegen Polling können Ansichten kurz zeitversetzt sein; sie müssen nach abgeschlossener Aktion und Aktualisierung auf denselben autoritativen Stand kommen. Die tatsächliche Verzögerung ist noch zu messen.

## 4. Ausgeführte Tests und Nachweise

| Suite | Ergebnis |
|---|---|
| RaidClipPlugin.Tests, Release, vollständiger finaler Lauf | 731 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Backend.Tests, Release | 226 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Frontend Vitest | 88 bestanden |
| Playwright über playwright.actions.config.ts | 30 bestanden |
| **Gesamt, ohne gezielte Wiederholungsläufe doppelt zu zählen** | **1.075 bestanden** |
| Frontend build:hosted-test | BESTANDEN |

Neue Tests in diesem Auftrag:
- RemoteExtendedTests.Two_remote_players_share_boss_hp_with_exact_damage_and_fresh_next_round
- RemoteExtendedTests.Two_players_equip_and_buy_perks_without_cross_account_or_duplicate_booking
- RemoteMinigameTests.Two_players_parallel_casino_receipts_preserve_individual_balances_on_retry: vier Varianten für gamble/coinflip/slots/roulette

Relevante bestehende Tests:
- AdventureGameTests.BossSharesRewardAcrossUniqueAttackersAndEnforcesAttackCooldown
- RemoteExtendedTests.Boss_uses_running_engine_and_shared_chat_cooldown_and_durable_receipt
- RemoteExtendedTests.Persisted_boss_action_cannot_damage_again_even_after_cooldown_and_without_outer_receipt
- RemoteActionTests.Offline_Twitch_does_not_block_actual_perk_and_duplicates_after_restart
- RemoteActionTests.Crash_after_intent_fsync_is_unknown_and_never_replayed
- RemoteActionTests.Full_HTTP_queue_plugin_sync_firestore_and_viewer_snapshot_roundtrip
- RemoteMinigameTests.Jwt_HTTP_outgoing_plugin_original_engine_automatic_sync_and_Firestore_reader_roundtrip
- ActionRelayTests: JWT-/Identitäts-/Kanalprüfung, Offline-Stream, Heartbeat, alte Sitzung, unbekanntes Ergebnis und dauerhafte Warteschlange
- boss-polling.test.ts und perk-recovery.test.ts

Die lokalen HTTP-Hosts und Firestore-Transportdouble sind keine Tests gegen eine echte Firestore-Emulator- oder Produktivinstanz. Playwright verwendet synthetische Antworten und ersetzt keinen echten Twitch-Test.

Erste gezielte Testentwürfe hatten falsche Erwartungen: beide parallelen Casinoanfragen sollten erfolgreich sein, und ein Boss sollte unmittelbar nach Abbruch ohne Rundenpause starten. Tatsächlich greifen der bestehende globale Cooldown und mindestens eine Sekunde Rundenpause korrekt. Die Tests wurden an diese vorhandenen Regeln angepasst; Spielregeln nicht verändert. Alle finalen Läufe bestehen. Vorhandene Build-/Analyzer-Warnungen bleiben sichtbar in Logs.

Nachweisordner neben dem Repository:
outputs/Multiplayer-Release-Test-2026-10-19/
- plugin-final.trx (finale vollständige Suite)
- backend.trx
- multiplayer-targeted.trx / inventory-targeted.trx (zusätzliche gezielte Läufe; nicht zur Gesamtsumme addieren)
- multiplayer-frontend.log / multiplayer-browser.log / multiplayer-build.log
- LIVE_READONLY_CHECKS.json
- LIVE_PLUGIN_READONLY.json

Wiederholen:
~~~powershell
dotnet test RaidClipPlugin/Tests/RaidClipPlugin.Tests.csproj -c Release
dotnet test Sync/tests/Backend.Tests/Backend.Tests.csproj -c Release
# Im Unterordner twitch-extension:
pnpm test
pnpm build:hosted-test
pnpm test:actions
~~~

## 5. Behobene Fehler und geänderte Dateien

In diesem Auftrag wurde kein zusätzlicher Produktions-Gameplay-Fehler reproduziert. Deshalb keine spekulative Reparatur und kein Deployment.

Die bereits hochgeladene Bosskorrektur bleibt bestehen: Bossstatus wird nicht mehr wegen eines Fehlers der Charakterstatus-Abfrage übersprungen. Die zugehörigen Regressionstests bestehen erneut; echte Boss-Abnahme bleibt offen.

Nur zwei Quelldateien für zusätzliche synthetische Testabdeckung geändert:
- RaidClipPlugin/Tests/RemoteExtendedTests.cs
- RaidClipPlugin/Tests/RemoteMinigameTests.cs

Zusätzlich dieser Bericht und lokale Testnachweise. Bereits vorhandene, anderweitige Änderungen im Arbeitsverzeichnis gehören nicht zu diesem Auftrag.

## 6. Verbindliche manuelle Prüfanleitung – alle folgenden Prüfpunkte zunächst OFFEN

### Vorbereitung

1. Testfenster mit zwei Personen vereinbaren. Vorschlag: Funjii als A und Coner123 als B; beide sind bereits freigegeben. Alternativ zwei andere bereits freigegebene Konten. Zwei Tabs derselben Anmeldung zählen nicht als zwei Konten.
2. Beide melden sich selbst über Twitch an, geben ihre Identität frei und öffnen das Desktop-Panel. Keine Passwörter oder Tokens teilen.
3. Vor echten Änderungen ausdrücklich die konkreten Testkonten, erlaubten Aktionen und das Einsatzbudget freigeben. Ohne diese Freigabe nur lesen. Benötigte Testitems müssen aus vorhandenen freigegebenen Beständen stammen; keine neuen Gegenstände oder Perlen heimlich vergeben.
4. Laufendes Plugin und tatsächliche Version notieren. Beide müssen „Plugin online · RPG spielbereit“ sehen. Aktuelles Paket über MD5/Framepfad bestätigen. Datum, Uhrzeit, Streamstatus, Anfangsperlen, HP, Perklevel, Gegenstands-IDs und relevante Cooldowns notieren.
5. Falls die Ausstattung oder Freigabe fehlt: den betreffenden Test OFFEN lassen. Keinen Reset und keine alte Sicherung über aktuelle Bestände spielen.

### A. Boss, mindestens zwei Runden

1. Moderator/Broadcaster startet über den bestehenden Weg einen Boss. Beide öffnen Boss. Boss-ID soweit Diagnose verfügbar, Max-HP, aktuelle HP und Zeitpunkt erfassen. Beide Ansichten müssen nach Aktualisierung denselben Zustand zeigen.
2. A greift einmal an. Ergebnis/Schaden und endgültigen Beleg erfassen. B wartet auf Aktualisierung. HP-Abzug muss genau zu den autoritativen Treffern passen.
3. B greift an; anschließend beide möglichst gleichzeitig. Individuelle Schadenswerte, Gegenangriffe und Cooldowns prüfen. Jede bestätigte Aktion darf genau einmal wirken.
4. Während Cooldown erneut versuchen: Ablehnung ohne neuen Schaden/Abbuchung. Bei offener Bestätigung nur denselben Auftrag prüfen.
5. Boss gemeinsam besiegen. Beide müssen den beendeten Kampf anzeigen. Belohnung pro berechtigtem Teilnehmer aus der tatsächlich aktiven Plugin-Konfiguration und den bestehenden Regeln ableiten, einschließlich Überlebens-/Kontolimitregeln. Anfangs-/Endstände dokumentieren; keine pauschale gleiche Auszahlung unterstellen.
6. Beide Browser neu laden: kein wieder aktiver alter Boss, keine erneute Belohnung.
7. Bestehende Rundenpause abwarten und nächsten Boss starten. Neue Runde, volle Start-HP, kein persönlicher Schaden aus der alten Runde. Alte abgeschlossene Belege dürfen keine neue Wirkung haben.

### B. Inventar und Perks

1. Jeder legt ein eigenes freigegebenes Testitem an und wieder ab. Anzeige, tatsächlicher Slot und Gegenstandsanzahl nach Neuladen vergleichen. Kein Kopieren oder Verschwinden.
2. A und B kaufen parallel je einen erlaubten Perk. Kosten aus dem Plugin notieren. Mehrfachklick bei A: höchstens eine laufende Kaufaktion. Nach endgültigem Abschluss muss ein weiterer legitimer Kauf wieder möglich sein.
3. Anzeigen beider Spieler müssen nur ihre eigenen Abbuchungen/Level zeigen. Neuladen bestätigt denselben Stand.
4. Mit einem bereits dafür geeigneten Konto einen zu teuren Kauf versuchen; nicht erst fremde Perlen entfernen. Keine Abbuchung.
5. Bereits maximale Perks nicht weiter kaufbar; bereits gekaufte, noch ausbaubare Perks dürfen regulär zum richtigen nächsten Preis gekauft werden.

### C. Tatsächlich implementierte Perlen-Minispiele

Für Gamble, Coinflip, Slots und Roulette jeweils:
1. Aktive Regeln und zulässigen kleinen Einsatz festhalten; echte Aktivierung nur mit vereinbartem Budget.
2. A/B parallel spielen. Auch eine Ablehnung durch den vorhandenen globalen Cooldown kann korrekt sein; dann nach Ablauf testen. Nicht zum Test die Cooldowns ändern.
3. Ergebnis, Einsatz, Endstand und Beleg prüfen; Gewinne/Verluste nach ursprünglichen Plugin-Regeln, kein UI-Zufall.
4. Gleichen offenen Auftrag nach Timeout prüfen. Keine neue ActionId zum „Nachholen“ erzeugen. Beleg darf nicht zweimal abbuchen/auszahlen.
5. Gewinner und Verlierer tatsächlich dokumentieren. Wenn zufällig nur eine Ergebnisart auftritt, andere Variante OFFEN lassen; kein hohes Einsatzrisiko und keine Regeln manipulieren, um sie zu erzwingen.
6. „Max.“ nur als Autofill prüfen; nicht versehentlich den gesamten Stand einsetzen.

Zusätzlich:
- Rennen: A startet, B tritt derselben Runde bei; Einsatz/Teilnehmer, Sieger und Abrechnung prüfen; veraltete Runde nicht erneut betreten.
- Duell: A fordert B, B nimmt an; zusätzlich Ablehnung/Erstattung prüfen. Keine fremde Person unaufgefordert herausfordern.
- Heist: über bestehenden Betreiberweg starten, beide beitreten; einmalige Auflösung/Belohnung, kein doppelter Beitritt.
- Nur tatsächlich aktivierte Spiele testen; deaktivierte Konfiguration als NICHT GETESTET kennzeichnen.

### D. Verbindung und Wiederaufnahme

1. Stream offline, Plugin bereit: je eine freigegebene kleine Aktion pro Konto ausführen und finalen Snapshot bestätigen.
2. Während eines ohnehin geplanten Livestreams denselben Test durchführen. Nicht ausschließlich für diesen Test eigenmächtig einen Stream starten.
3. Nach Rücksprache und ohne offene gemeinsame Runde Module/Plugin geordnet stoppen. Beide dürfen keine neuen Aktionen ausführen; gespeicherte Profile dürfen lesbar bleiben.
4. Plugin wieder starten und Bereitschaft abwarten. Alte Sitzungsbelege dürfen nicht als neue Aktion erneut ausgeführt werden.
5. Browserseitigen Verbindungsverlust kontrolliert testen, ohne das Netzwerk aller Zuschauer zu unterbrechen: nach genau einer freigegebenen Anfrage die Testansicht offline schalten, anschließend wieder online. Endbeleg derselben ActionId abfragen und mit genau einer Buchung vergleichen.
6. Ein echter Plugin-zu-Cloud-Verbindungsabbruch benötigt eine getrennte sichere Testumgebung bzw. abgestimmtes Wartungsfenster. Dies ist derzeit OFFEN; keine Cloud-Dienste/IAM/Firewall des laufenden Kanals zum Test abschalten.
7. Beide Browser neu laden, beide Stände mit dem Plugin vergleichen. Eine längere parallele Spielsitzung dokumentieren.

### Protokoll pro Prüffall

| ID | Konto A/B | Startzeit | Version/Streamstatus | Aktion + Beleg-ID | Vorher/Nachher | Ergebnis | Nachweis/Fehler |
|---|---|---|---|---|---|---|---|
| auszufüllen | | | | keine Tokens | | OFFEN | |

Belege dürfen keine Secrets, Twitch-JWTs oder DPAPI-Dateien enthalten. Bei unklarem Ergebnis Test stoppen und denselben Beleg prüfen; keine Bestände manuell passend machen.

## 7. Risiken und Entscheidung

- Kritischer fehlender Nachweis: zwei echte Twitch-Konten im selben Boss, Boss-Tod/Belohnung und nachfolgende Runde.
- Echte gleichzeitige Inventar-/Perk-/Minispielaktionen und reale Wiederverbindung noch nicht abgenommen.
- Ein frisch gestartetes installiertes Plugin ist nicht automatisch identisch mit dem kompletten lokal getesteten Entwicklungsstand. Prozessversion ist dokumentiert; Release-/Quellstand muss vor Veröffentlichung eindeutig zugeordnet werden.
- Polling und Warteschlange benötigen reale Messung bei mehreren aktiven Zuschauern. Die aktuelle Siebener-Freigabe ist kein öffentlicher Zugang für alle Nutzer.
- Öffentliche Datenschutz-URL, finale Reviewer-Unterlagen, kontrollierte öffentliche Zugriffskonfiguration und regulärer Plugin-Release bleiben separate Veröffentlichungsschritte.
- Selbstlöschung bleibt serverseitig deaktiviert; in diesem Auftrag weder aktiviert noch an echten Konten getestet.
- Keine öffentliche Twitch-Genehmigung vorhanden.

**NICHT RELEASE-BEREIT.** Voraussetzungen für die technische Freigabe: alle kritischen offenen manuellen Fälle mit zwei tatsächlich angemeldeten Testkonten durchführen, konkrete Ergebnisse protokollieren, etwaige reproduzierbare Fehler klein beheben und gezielt erneut testen. Automatische Tests allein reichen ausdrücklich nicht aus.

