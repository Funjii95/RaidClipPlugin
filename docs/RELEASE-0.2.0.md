# Veröffentlichung und Installation – 23.10.2026

Auf ausdrücklichen Nutzerauftrag veröffentlicht und installiert:

- [Windows 0.2.0](https://github.com/Funjii95/FunjiiOtterRPG-Desktop/releases/tag/v0.2.0): Installer, Portable-ZIP und automatischer Updatefeed öffentlich. GitHub-Release-Pipeline erfolgreich.
- [Plugin 2.2.0](https://github.com/Funjii95/RaidClipPlugin/releases/tag/v2.2.0): Installer, Update-ZIP, SHA256-Datei und `update.json` öffentlich. GitHub-Release-Pipeline erfolgreich.
- [Plugin-Quellcode, PR 49](https://github.com/Funjii95/RaidClipPlugin/pull/49): zusammengeführt; Quellcode auf `main` verfügbar.
- Die gemeinsame Twitch-Oberfläche wurde gebaut und als `Twitch-Extension-Items-66.zip` an der Plugin-Veröffentlichung angehängt. Die Aktivierung im Twitch-Entwicklerportal wurde nicht automatisch geändert.

Installierte Prozesse wurden als „Funjii Otter RPG · 0.2.0“ und „Raid Clip Plugin 2.2.0“ bestätigt. Das Plugin wurde vor dem Ersetzen regulär beendet. Programmdatei, Spielstände, Einstellungen, Outbox und Aktionsbelege wurden lokal unter `backups/release-2.2.0-*` gesichert. Alle zum Sicherungszeitpunkt vorhandenen Inventar-Instanz-IDs des Kanalinhabers sind nach der Installation noch vorhanden.

Publisher-Revision: `funjii-otter-rpg-publisher-test-tides-3e78236f6f`, Tag `rc1`. Unterstützt Projektion 5 sowie 2/3/4.

Backend-Revision einschließlich getrennter Status-/Mutationslimits: `funjii-otter-rpg-backend-test-desktop-fb9392a96e`, Tags `desktop` und `rc1`. Status- und Belegabfragen können den Mutationshaushalt nicht mehr ausschöpfen. 264 Backend-Tests bestanden, darunter zwei neue Grenztests. Die bisherigen Default-Traffic-Ziele und die übrigen Test-Tags bleiben erhalten. Die verpflichtende Konfigurationsprüfung wurde vor jedem Bereitstellen und Umschalten bestanden; geschützte Rollback-Sicherungen sind vorhanden.

Live geprüft: Publisher-Fähigkeiten mit dem vorhandenen geschützten Zugang, Desktop-Konfiguration und Bereitschaft, gültige Anmeldung von Twitch-ID `42812664`, frischer Plugin-Heartbeat, sechs neue echte Sets und drei Adminoperationen im veröffentlichten Katalog. In der installierten Windows-App sind „Plugin online · RPG spielbereit“ und die freigeschalteten Admin-Schaltflächen sichtbar. Zur Verifikation wurden keine Adminmutationen ausgelöst.

Gesamte lokale Prüfung: 960 Plugin-Tests, 264 Backend-Tests, 112 Oberflächentests und 7 Updater-Tests bestanden. Zusätzlich nativer Windows-Start und synthetische Bedienprüfung des Adminformulars. Einzelillustrationen bleiben vorerst beschriftete Platzhalter.
