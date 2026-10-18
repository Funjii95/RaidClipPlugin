# Grafische Backend-Anbindung und Plugin-Freigabe

Stand: 18.10.2026. Separate Arbeitskopie von RaidClipPlugin 2.1.22, Branch `codex/graphical-rpg-backend`, Basis `0844c6e`. Keine Änderung der ursprünglichen Installation oder Spielstände, kein Deployment und kein Twitch-Upload.

## Verbindliche Regel

Twitch live ist vollständig als Spielvoraussetzung aufgehoben. Chat und Extension dürfen dieselbe vorhandene RPG-Logik benutzen, solange das Plugin läuft, authentifiziert verbunden und spielbereit ist, das Minigame-/Perlenmodul und RPG aktiviert sind und der lokale Heartbeat höchstens 20 Sekunden alt ist. Für Remote-Aktionen meldet der geschützte ausgehende Aktionskanal diese Bereitschaft alle vier Sekunden; das Backend akzeptiert nur aktuelle, streng aufsteigende Heartbeats derselben Sitzung (Lease höchstens 15 Sekunden, Zeitabweichung höchstens fünf Sekunden). Öffnen der Sitzung allein erlaubt kein Spiel.

Helix Get Streams wird von der Spielfreigabe nicht aufgerufen; Helix-Client-ID und -Token sind hierfür nicht mehr erforderlich. Streamstart, Streamende und Offline-Stream ändern die Freigabe nicht. Der historische Typname StreamAdmission und das reservierte StreamId-Protokollfeld bleiben aus Kompatibilitätsgründen bestehen; sie binden keine Aktionen an einen Livestream. Andere vorhandene Plugin-Funktionen wie Clips, Giveaway oder Streaminformationen wurden nicht umgebaut.

JWT-Signatur, Ablaufzeit, Kanalbindung und verknüpfte Nutzeridentität bleiben serverseitig geprüft. Plugin-DPAPI-Zugang, getrennte Schlüsselbereiche, Sitzungsepoch, Browser-Sitzungsnonce, idempotente ActionId, dauerhafte Warteschlange und Belege bleiben erhalten. Vor dem lokalen Commit werden Bereitschaft, Sitzung und zeitlich begrenztes Ausführungsbudget erneut geprüft. Bereits angenommene Aktionen werden bestätigt oder verworfen; bei unklarem Ausgang erfolgt keine automatische neue Buchung. Unverbrauchte Warteschlangenaufträge laufen ab. Bereits reservierte Einsätze behalten die technische Rückabwicklung. Cloud-Ausfälle beenden das normale Plugin nicht.

## Oberfläche und echte Daten

`GraphicalApp.tsx` ist die vorhandene grafische App mit injizierter Datenquelle. CSS und Spielgrafiken bleiben unverändert. Die normale Ansicht lädt ausschließlich BackendRpgDataSource; die Mock-Engine ist nur im gesonderten Vite-Modus demo importiert. Ein Fehler aktiviert keinen Demo-Ersatz.

Der Adapter liest `/me` und `/play/state`, übersetzt die echten gespeicherten Felder in vorhandene UI-Typen und nutzt den bestehenden LiveRpgClient für signierte Anfragen. Perlen werden auch oberhalb der JavaScript-Ganzzahlgrenze exakt als Text dargestellt. Inventarinstanzen, Ausrüstung, gespeicherte Perks, Fische, Sushi, Buffs und Cooldowns stammen aus dem Snapshot. Fehlende abgeleitete Werte werden nicht nachgerechnet oder erfunden. Der optionale vorhandene Status-Aufruf kann tatsächliche Plugin-Stats ergänzen, sofern sie zum aktuellen Snapshot passen.

Doppelklicks werden vor dem ersten asynchronen Aufruf gesperrt. Ausstehende Aktionen bleiben nach Neuladen unter derselben ID prüfbar. Bestätigte Aktionen laden den Serverstand neu; verzögerte Snapshot-Ankunft wird angezeigt. Profile können bei Plugin-Ausfall lesbar bleiben. Fehlende Authentifizierung und Identitätsfreigabe werden erklärt; Geheimnisse werden nicht gespeichert.

Verbunden sind die vorhandenen sicheren Wege für Tauchen, Angeln, Anlegen/Ablegen, Perks kaufen, Sushi herstellen und benutzen. Charakter, Inventar und Itemsets bleiben lesbar. Bossanzeige und alle Navigationselemente bleiben erhalten. Bossangriff, Zerlegen, Item-Lock/Favoriten und Perlen-Minispiele besitzen noch keinen ausreichend sicheren Backend-Aufrufweg; entsprechende Aktionen bleiben gesperrt statt Mock-Ergebnisse oder neue Regeln auszuführen. Nicht übermittelte Boss-/Hot-Time-/Katalogwerte bleiben als unbekannt gekennzeichnet. Die Funktionen sind weiterhin im getrennten Demo-Build prüfbar; sie wurden nicht als spielbar mit echtem Backend ausgegeben.

## Aktuell betroffene Dateien und Tests

| Bereich | Dateien | Prüfung |
|---|---|---|
| Backend-Freigabe | Sync/src/Backend/ActionRelay.cs, BackendSettings.cs, Program.cs | ActionRelayTests: offline Twitch erlaubt Spiel; fehlende/abgelaufene/nicht bereite Plugin-Sitzung sperrt; JWT, Kanal, Nutzer, Wiederholung und späte Aufträge bleiben abgesichert |
| Lokale Freigabe | RaidClipPlugin/Services/StreamAdmission.cs, MainForm.CloudIntegration.cs, MainForm.cs, Services/RemoteRpgExecutor.cs | StreamAdmissionTests: echte Verbindungsbereitschaft, Heartbeat-Frische, alte Kontexte und Commit-Sperre; RemoteActionTests: deaktiviertes RPG/Perlen/Modul, identische Buchung und synthetischer Gesamtdurchlauf |
| Grafische Datenquelle | twitch-extension/src/main.tsx, GraphicalApp.tsx, types.ts, presentation.ts, remote/model.ts, remote/BackendRpgDataSource.ts, live/client.ts | remote.test.ts: echte Felder, exakte Perlen, Authfehler, fehlende API, Plugin offline, Doppelklick und unklare Bestätigung |
| Entwicklungsmodus | src/demo-entry.tsx, package.json, scripts/serve.mjs | Demo ausschließlich gesonderter Build; normale URL kann keine Mockdaten einschalten |
| Hosting und Browser | tests/hosting.test.ts, live.spec.ts und bestehende UI-/Bild-/Minigame-Suites | drei HTML-Einstiege, Helper zuerst, unveränderte Bilder; Panel/Mobil inklusive Offline-Twitch bei bereitem Plugin |
| Dokumentation und Nachbau | ACTION_CHANNEL.md, CLOUD_INTEGRATION_TEST.md, twitch-extension/README.md, Build-ActionChannelTest.ps1 und diese Anleitung | aktuelle Regel dokumentiert; frühere Ergebnisberichte als historisch markiert |

Die vollständige Dateiliste steht in GEAENDERTE_DATEIEN_GRAPHICAL.md. Bestehende historische Quell-/Release-Berichte sind keine aktuelle Freigabeanweisung.

## Konfiguration und Testanleitung

1. Ausschließlich die getrennte Windows-Testversion verwenden. Die normale Installation nicht überschreiben. Das bestehende isolierte Testdatenverzeichnis und die vorhandenen Testkonten-/DPAPI-Konfigurationen gelten weiter; keine automatischen Kopien produktiver Spielstände.
2. Backend-Aktionskanal gemäß ACTION_CHANNEL.md mit explizitem Kanal, synthetischer Nutzerfreigabe, geschütztem Plugin-Schlüsselhash und dauerhaftem Firestore-Aktionsspeicher konfigurieren. Secrets ausschließlich im geschützten Server-/Plugin-Bereich. Für Aktionen kein Helix-Token nötig. Vorhandene cloud-sync.json und action-channel.json müssen denselben Kanal und kompatible Nutzerfreigaben verwenden. Remote-Freigabe verlangt zusätzlich funktionsfähige Snapshot-Synchronisierung.
3. Das normale Plugin starten und authentifiziert mit dem Chat verbinden, Minigames/Perlen/RPG aktivieren. Der Twitch-Stream kann ausgeschaltet bleiben. Lokales Testprofil verwenden. Verbindung und Cloud-Sync prüfen.
4. Das normale Hosting-ZIP enthält panel.html, mobile.html und config.html direkt an der Wurzel, dazu Assets und Bilder. Für Hosted Test dieselben Pfade in Twitch verwenden; im Extension-Netzwerkzugriff die vorhandene Backend-Domain erlauben. Keine ZIPs mit Quellcode oder dist-demo hochladen.
5. Mit einem erlaubten Twitch-Testnutzer öffnen und Identität freigeben. Profil, Perlen und Ausrüstung mit `/me` sowie lokalem Testprofil vergleichen. Ein vorhandenes Item einmal anlegen, Ergebnis abwarten, Seite neu laden und denselben gespeicherten Zustand prüfen. Danach Plugin stoppen: neue Aktionen müssen gesperrt sein, Profile dürfen lesbar bleiben. Wieder verbinden und Streamstart/Streamende prüfen: bei bereitem Plugin darf keines davon sperren. Wenn Twitch im Offline-Kontext keine Extension-Fläche zeigt, ist das keine Backend-Spielfreigabe und kann von dieser App nicht erzwungen werden.
6. Bei unklarem Ergebnis ausschließlich die bestehende Aktion prüfen, keine neue ID zur Wiederholung erzeugen. Sämtliche echten Testaktionen erst im ausdrücklich freigegebenen Testkonto durchführen.

## Bereitstellungsgrenze

Die vorhandene Cloud-URL war beim rein lesenden Check am 18.10.2026 erreichbar: health/live 200, /me ohne JWT 401, /play/state 404. Dort ist der neue Aktionskanal noch nicht verfügbar. Ein echter Twitch-Nutzer-/Cloud-Gesamttest kann deshalb mit diesem Paket allein noch nicht erfolgreich abgeschlossen werden. Kein Deployment wurde durchgeführt. Die automatischen Tests verwenden synthetische Nutzer, lokale ASP.NET-Hosts und kontrollierte Firestore-/Twitch-Transporte. Reale Twitch-OAuth-Scopes, IAM und Hosted-Test-Verfügbarkeit bleiben nach gesonderter Bereitstellung zu prüfen.

## Nachbau

.NET SDK 10, Node >=22.12, pnpm und Playwright Chromium benötigt. Build-ActionChannelTest.ps1 führt Backend-, Plugin-, Frontend- und Browsertests aus und veröffentlicht nur lokal eine separate Windows-Testversion. Für normale Frontend-Arbeit: pnpm build, pnpm test, pnpm build:demo, pnpm test:e2e. Keine automatische Veröffentlichung.
