# Release-Prüfung – Desktop 0.0.1-rc1

18.10.2026. Ergebnis: Desktop-Kandidat für einen eingeschränkten geschlossenen Test vorbereitet. Kein vollständiger Hosted-Test-Erfolg und keine öffentliche Release-Freigabe nachgewiesen.

## Bestandene Prüfungen

| Bereich | Nachweis |
|---|---|
| Release-Build | Zwei Desktop-Builds bytegleich, SHA256 je Datei im reproduzierbaren Build-Manifest; Node 24.22.0, pnpm 11.25.0, eingefrorene pnpm-lock.yaml |
| Paket | panel.html und config.html direkt im ZIP, relative Assets, kein mobile.html; ZIP-Zeitstempel und Dateireihenfolge deterministisch |
| Codeprüfung | Anwendungscode lesbar ohne Minifizierung, React in eigenem Vendor-Chunk; Twitch-Helper zuerst, keine Inline-Skripte, keine eval-basierte Einbindung |
| Assets | Bestehende 102 Itembilder und beide Illustrationen erhalten; Design und CSS-Struktur nicht neu erstellt; normaler Titel ohne Demo, Plattform-Werbebadge entfernt |
| Frontend | 72 Unit-/Hostingtests bestanden; 21 bestehende Browserfälle bestanden, zusätzlich 2 Tests am tatsächlichen Desktop-Release mit 320x500 und 1024x768 |
| Plugin | 683 Tests bestanden, darunter 8 neue Fälle für exakt erlaubte Staging-URLs, falsche Hosts, HTTP, Query und falschen Kanal; ursprüngliche RPG-/Idempotenz-/Snapshot-Tests weiterhin grün |
| Backend | Unveränderter Bestandsnachweis: 196 Tests bestanden; beide Linux-Container aktuell gebaut und sämtliche OCI-Blob-Hashes geprüft; keine laufende Containerinstanz lokal getestet |
| Windows | Eigenständiger win-x64-Build 2.1.22-cloudtest.3 erfolgreich; separater UI-Starttest ohne Twitch-Verbindung |
| Abhängigkeiten | pnpm Produktionsaudit: keine gemeldeten Advisories; NuGet-Backendaudit inkl. transitiver Pakete: keine gemeldeten verwundbaren Pakete. Kein vollständiger OS-CVE-Scan des Basisimages durchgeführt |
| Reale Lese-Prüfung | Backend health/live und health/ready 200; /me ohne oder mit falschem JWT 401; exakte CORS-Origin erlaubt, fremde Origin ohne Freigabe; Antworten no-store; Firestore anonym 403 |
| Publisher-Bestand | Geschützte Metadatenabfragen funktionieren, anonym 401 und fremder Nutzer 403; keine Spielstandänderung durch die Prüfung |
| Sicherheit | JWT-Signatur/Ablauf/Kanal/Identität, eigene Nutzerzuordnung, Session-Nonce, Heartbeat-Lease, persistente ActionId/Ergebnisbelege und Commit-Fencing bleiben erhalten. Kein Twitch-Livestatus als Spielbedingung |
| Originaldaten | Ursprüngliche lokale viewer-points.json SHA256 unverändert: E178D117F6D78C9ABB914B9DC0281BD12AD57963C234AB48EEFD92103A23430E |

Die Summe der obigen automatisierten Bestands-/Kandidatenprüfungen beträgt 974 bestandene Fälle (683 + 196 + 72 + 21 + 2). Der Backend-Bestandsnachweis wurde übernommen und ist nicht als neuer realer Cloud-Lauf ausgegeben. Einzelne Prüfungen sind synthetisch und ersetzen keine echte Twitch-/Cloud-Abnahme.

## Fehlgeschlagen oder ausstehend

- Reale Cloud: /api/v1/play/state liefert 404. Der aktuelle Backend-Container enthält den Aktionskanal noch nicht.
- Reale Cloud: Publisher /api/v1/snapshots/capabilities liefert ebenfalls 404. Die neue integrierte Synchronisierung verlangt Projektionsversion 2 und mindestens 262144 Byte Nachrichtenlimit. Daher ist auch ein Publisher-Update erforderlich.
- Vor der Scope-Änderung: mobiler Startgrößentest fehlgeschlagen, 6555305 Byte statt höchstens 1000000 Byte. Vier andere zusätzliche Ansichtsprüfungen bestanden. Mobil ist auf ausdrücklichen Nutzerwunsch aus der ersten Einreichung ausgenommen, nicht nachträglich als bestanden markiert. Die großen Startbilder bleiben auch am Desktop eine Leistungsoptimierung für später.
- Echte Twitch-Helper-Authentifizierung, echte Testaktion, tatsächlicher Plugin-Heartbeat und Wiederverbindung mit den neuen Cloud-Revisionen: noch nicht durchgeführt, da Deployment-Freigabe fehlt.
- Bossangriff, Zerlegen, Lock/Favoriten und Perlen-Minispiele besitzen noch keinen sicheren echten Aufrufweg. Ihre Bereiche bleiben erhalten, Aktionen sind gesperrt. Das ist ein Funktionsblocker für das ursprüngliche Ziel „vollständig spielbar“.
- Konsole: Datenschutz-URL fehlt, öffentliche Prüfung ist gesperrt. Kategorie und Einreichungsbilder müssen vervollständigt/verifiziert werden. Vorhandene Beschreibung verspricht Bosskämpfe und muss vor Einreichung korrigiert oder die Funktion fertiggestellt werden.
- Aktuelle Server-/Publisherkonfiguration ist absichtlich auf Kanal/Testnutzer begrenzt. Keine öffentliche Mehrkanal-/Alle-Nutzer-Bereitschaft behauptet.

## Datenschutz und Geheimnisse

Das Extension-ZIP enthält keine DPAPI-Dateien, lokalen Einstellungen, OAuth-Token, privaten Schlüssel, Profile oder Testfixtures. Die Browserdatenquelle hat keinen Mock-Fallback; Authentifizierung verbleibt im Speicher. Session Storage enthält ausschließlich die eigene offene Aktions-ID, Parameter und Sitzung zur sicheren Wiederaufnahme. Der eigentliche Schlüssel liegt nur DPAPI-geschützt lokal; der Server erhält nur dessen Hash. Separate Einreichungs-/Testberichte können ausdrücklich als synthetisch markierte UI-Screenshots enthalten und sind nicht Bestandteil des Hosting-ZIPs.

Die Profil-Collection enthielt beim rein lesenden Baseline-Check 7 Dokumente, darunter das bekannte synthetische Profil 1001. Staging darf ausschließlich explizit freigegebene synthetische IDs verändern; die übrigen sechs Profile bleiben geschützt. Keine automatische Löschung oder neue Spielökonomie.

Belege: evidence/, CONTAINER_IMAGES.json, RELEASE_MANIFEST.json und STAGING_PLAN.json. Offizielle Vorgaben und Einreichungsentwürfe: TWITCH_EINREICHUNG.md.
