RAID CLIP PLUGIN 1.2.3
=======================

ERSTER START
1. RaidClipPlugin starten.
2. Im Assistenten „Twitch-Anwendung öffnen“ anklicken.
3. Eigene Client ID und eigenen Client Secret eintragen.
4. „Speichern und fortfahren“ anklicken.

SICHERHEIT
- Keine Twitch-Zugangsdaten in config.json oder der EXE.
- Client ID, Client Secret, Access Token und Refresh Token werden mit
  Windows DPAPI verschlüsselt im Benutzerprofil gespeichert.
- Im Projekt liegt nur Config\config.template.json mit leeren Platzhaltern.
- Config\config.json wird durch .gitignore ausgeschlossen.

BEDIENUNG
- „OBS-Quelle erstellen“ richtet RaidClip in der aktiven Szene ein.
- „Verbindungen testen“ prüft Player, OBS, Twitch und EventSub.
- „Plugin starten“ aktiviert Raid-Erkennung, Clips und Chataktionen.
- „Clip testen“ spielt einen Clip des eingegebenen Kanals ab.

INSTALLER
BUILD_INSTALLER.bat erzeugt installer-output\RaidClipPlugin-Setup-1.2.3.exe.

AUTO-UPDATE
- Beim Start wird update.json aus dem GitHub Release geprüft.
- Changelog anzeigen, Update installieren oder Version überspringen.
- Der separate Updater ersetzt Dateien erst nach dem Beenden der App.
- In den Einstellungen kann Auto-Update abgeschaltet werden.
