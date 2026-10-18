# Testergebnisse – grafische Backend-Anbindung

18.10.2026, Windows x64, .NET SDK 10.0.400. Getrennte Testkopie, kein Deployment.

| Prüfung | Ergebnis |
|---|---|
| Plugin inkl. Original-RPG und Cloud-Sync | 675 bestanden, 0 fehlgeschlagen/übersprungen |
| Backend, Publisher, Protokoll, Firestore-Transport | 196 bestanden, 0 fehlgeschlagen/übersprungen |
| Frontend, Adapter, Hosting, ursprüngliche Demo | 72 bestanden |
| Browser: grafisches Panel/Mobil plus bisherige Demo-Suites | 21 bestanden |
| Summe automatisierte Tests | 964 bestanden |
| Windows Release, win-x64, self-contained | erfolgreich veröffentlicht, separate EXE |
| Start der vollständigen Windows-Form mit --ui-smoke | Exit 0, isoliertes leeres Testverzeichnis, keine Twitch-Verbindung |
| Ursprünglicher Spielstand | SHA256 unverändert: E178D117F6D78C9ABB914B9DC0281BD12AD57963C234AB48EEFD92103A23430E |

## Nachweise der neuen Plugin-Regel

- Der Backend-Testhost stellt Twitch ausdrücklich auf offline. Öffnen, spielbereiter Heartbeat und Aktion funktionieren trotzdem; der Live-Probe-Aufrufzähler bleibt null. Streamwechsel invalidiert keine Plugin-Sitzung.
- Noch nicht bereite, getrennte und abgelaufene Sitzungen sperren weiterhin. Ein Ready=false-Heartbeat entzieht sofort die Freigabe. Alte Sequenzen, Zeitstempel und Sitzungsnonces werden zurückgewiesen.
- Lokale Tests lassen Spielen ohne jede Twitch-Live-Bestätigung zu, sperren aber bei veraltetem Plugin-Heartbeat, fehlender Bereitschaft oder alter Sitzung. Deaktivierte RPG-, Perlen- und Modulkonfiguration verursachen keine Buchung.
- Bestehende Sicherheitsprüfungen bleiben grün: ungültiges JWT, falscher Kanal/Benutzer, Fremditems, zu wenig Perlen, Cooldown, doppelte ActionId, Neustart, Verbindungsabbruch, unklarer Ausgang, abgelaufene Ausführungsfenster und Snapshot-Rückweg.
- Der vollständige synthetische Durchlauf nutzt vorhandene RPG-Dienste und den automatischen Snapshot-Worker über echte lokale HTTP-Endpunkte und den Firestore-SDK mit kontrolliertem Transaktions-Transport. Keine zweite Engine.

## Frontend

- Originale grafische Oberfläche bei 320px Panel und 390px Mobile: tatsächliche synthetische Profilwerte, 1.234 Perlen, Inventory-Instanz und Eigenschaften, Ausrüstung, sichere Aktion genau einmal bei Doppelklick und unveränderter Serverstand nach Neuladen.
- In diesen Browserfällen ist der Twitch-Stream ausdrücklich offline und das Plugin bereit: Aktionen bleiben bedienbar. Offline-Plugin sperrt alle Mutationen, Profil und Inventar bleiben lesbar.
- Fehlender Aktionsendpunkt ergibt lesbaren Zustand statt Demo-Ersatz. Authfehler zeigen Identitätsfreigabe. Ein als mock markiertes Profil wird abgelehnt. URL-Parameter aktivieren keine Mockdaten.
- Drei relative HTML-Einstiege, Twitch-Helper als erstes Skript, keine Inline-Skripte; alle 102 Itembilder und ursprünglichen Illustrationen bytegleich im Paket. Mock-Engine und Demo-Startinventar fehlen im normalen Produktionsbundle. Ursprüngliche Demo-Suites laufen ausschließlich unter dem getrennten Entwicklungsbuild.

## Grenzen

Kein echter Twitch-Nutzer wurde als erfolgreich getestet ausgegeben. Die bereits vorhandene Cloud-Instanz liefert beim Aktionsstatus 404; sie wurde nicht verändert. Ein echter Hosted-Test inklusive OAuth-Scopes, IAM und Browseranzeige außerhalb eines Streams steht nach gesonderter Bereitstellung aus. Firestore-Tests verwenden einen kontrollierten gRPC-Transport, keine produktive Datenbank. Bestehende Compiler-/Analyzerwarnungen sind nicht als neue fehlerfreie Warnungsbilanz ausgegeben.

Bossaktionen, Zerlegen, Lock/Favoriten und Perlen-Minispiele besitzen noch keinen sicheren aktuellen Backend-Aufrufweg. Die grafischen Bereiche bleiben erhalten, entsprechende echte Aktionen gesperrt. Fehlende Stats/Katalogwerte werden als unbekannt angezeigt.

Reproduktion und manuelle Testschritte: GRAPHICAL_BACKEND_INTEGRATION.md. Die separaten Testberichte enthalten Build-/Testausgaben; keine Zugangsdaten sind beigefügt.
