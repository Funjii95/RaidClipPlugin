# RaidClipPlugin 1.2.6

RaidClipPlugin erkennt eingehende Twitch-Raids und spielt automatisch einen zufälligen Clip des raidenden Kanals in einer OBS-Browserquelle ab.

## Erster Start

Beim ersten Start erscheint automatisch der Einrichtungsassistent:

1. **Twitch-Anwendung öffnen** anklicken.
2. In der Twitch Developer Console eine eigene Anwendung anlegen.
3. Client ID und Client Secret in den Assistenten kopieren.
4. **Speichern und fortfahren** anklicken.

Der Assistent erscheint danach nicht erneut. Falls die verschlüsselte Datei fehlt oder nicht mehr gelesen werden kann, wird die Einrichtung automatisch erneut angeboten.

## Sicherheit

- Im Projekt und in der EXE sind keine Twitch-Zugangsdaten enthalten.
- Client ID und Client Secret werden mit Windows DPAPI verschlüsselt und an das aktuelle Windows-Benutzerkonto gebunden.
- Twitch Access Token und Refresh Token werden ebenfalls verschlüsselt gespeichert.
- Eine vorhandene alte Klartext-Token-Datei wird automatisch verschlüsselt übernommen und anschließend gelöscht.
- Im Repository befindet sich nur `Config/config.template.json` mit leeren Platzhaltern.
- `.gitignore` verhindert, dass eine lokale `config.json` versehentlich veröffentlicht wird.

Die geschützten Daten liegen im lokalen Anwendungsdatenordner des Windows-Benutzers und können nur von diesem Benutzer auf diesem Windows-System entschlüsselt werden.

## OBS einrichten

1. In OBS unter **Werkzeuge → WebSocket-Servereinstellungen** den Server aktivieren.
2. Host, Port und Passwort in der RaidClip-GUI eintragen.
3. **OBS-Quelle erstellen** anklicken. Die Browserquelle `RaidClip` wird automatisch in der aktiven Szene erstellt oder aktualisiert.
4. **Plugin starten** anklicken.

## Einstellungen

Twitch-Kanal, OBS-Verbindung, Clip-Zeitraum, Retries, Clipdauer, Lautstärke, Raid-Cooldown, Blacklist und Chataktionen werden direkt in der GUI verwaltet.

Technische Standardwerte stehen in `Config/config.template.json`. Die Datei enthält absichtlich keine echten Client-Zugangsdaten.

## Chat-Moderation

Das optionale Modul läuft unabhängig von Raid-Erkennung und Clip-Wiedergabe. Im Tab **Chat-Moderation** erscheinen neue Nachrichten mit direkten Schaltflächen für Timeout, Ban und Löschen.

Der Wortfilter ist standardmäßig deaktiviert. Bei Aktivierung löscht er nur die betroffene Nachricht; automatische Timeouts oder Bans werden bewusst nicht ausgeführt. Streamer, Moderatoren und VIPs können vom Filter ausgenommen werden. Beim ersten Start nach dem Update fordert Twitch die zusätzlichen Chat- und Moderationsrechte an.

## Chat-Minigame

Das optionale Modul läuft unabhängig von Raid-Clips, OBS und Moderation. Seine sieben Reiter trennen Übersicht, Punktequellen, Commands, Casino-Spiele, Rangliste, Historie und Limits.

- Gemeinsame, dauerhaft gespeicherte Punkte für alle Spiele
- Passive Punkte durch Watchtime, Chat, Follow, Sub, Raid und Channel Rewards
- Commands `!punkte`, `!daily`, `!top`, `!rang` und `!profil`
- Casino-Spiele `!gamble`, `!coinflip` und `!slots`, jeweils separat abschaltbar
- Optionaler, dauerhaft gespeicherter Jackpot
- Konfigurierbare Konto-, Spiele-, Verlust- und Gewinnlimits
- Live-Rangliste, Spielhistorie sowie JSON-Export und sicherer Import mit Backup
- Admin-Commands `!punkte add/remove/set <user> <betrag>` für Broadcaster und Mods

„Aktiv“ bedeutet bei Watchtime-Punkten, dass der Nutzer im laufenden Intervall mindestens eine Chatnachricht geschrieben hat. Der Streamer selbst erhält keine automatischen Watchtime-Punkte. Beim ersten Start nach dem Update fordert Twitch die zusätzlichen Rechte für Follow-, Sub- und Channel-Reward-Ereignisse an.

## EXE erstellen

Den Ordner entpacken und `EXE_ERSTELLEN.cmd` doppelt anklicken. Die fertige App erscheint anschließend im Ordner `fertige-exe`.

## Windows-Installer erstellen

1. Einmalig [Inno Setup 6](https://jrsoftware.org/isdl.php) installieren.
2. `BUILD_INSTALLER.bat` doppelt anklicken.
3. Der fertige Installer liegt anschließend unter `installer-output/RaidClipPlugin-Setup-1.2.6.exe`.

Der Installer arbeitet ohne Administratorrechte, enthält die benötigte .NET-Laufzeit, legt einen Startmenü-Eintrag an und bietet optional eine Desktop-Verknüpfung.

## Auto-Update über GitHub Releases

Beim Programmstart lädt RaidClip die als GitHub-Release-Asset veröffentlichte `update.json`, sofern **Automatisch nach Updates suchen** aktiviert ist.

Ist eine neuere Version vorhanden, zeigt die GUI **Update verfügbar** mit:

- **Changelog anzeigen**
- **Update installieren**
- **Überspringen**

Beim Installieren wird das ZIP oder die EXE heruntergeladen und über SHA-256 geprüft. Anschließend startet RaidClip eine separate temporäre Updater-EXE und beendet sich. Erst der Updater ersetzt die alten Dateien und startet RaidClip neu. Fehler der Haupt-App stehen im normalen Log; Fehler beim Ersetzen oder Neustarten unter `%LocalAppData%\RaidClipPlugin\logs\updater-JJJJ-MM-TT.log`.

### GitHub Release veröffentlichen

1. `BUILD_INSTALLER.bat` ausführen. Dadurch entstehen der normale Installer und `RaidClipPlugin-Update-1.2.6.zip`.
2. Ein GitHub Release erstellen und das Update-ZIP hochladen.
3. `UPDATE_DATEI_ERSTELLEN.cmd` starten und die endgültige GitHub-Downloadadresse des ZIPs eingeben.
4. Die erzeugte `update.json` ebenfalls als Release-Asset hochladen.
5. Vor der Veröffentlichung in `Config/config.template.json` bei `Update.ManifestUrl` diese stabile Adresse eintragen:

   `https://github.com/Funjii95/RaidClipPlugin/releases/latest/download/update.json`

Die Struktur von `update.json` ist in `Installer/update.example.json` dokumentiert.

Persönliche Einstellungen, verschlüsselte Twitch-Zugangsdaten und Clip-Historie bleiben bei einem Update erhalten.
# RaidClipPlugin 1.2.3

RaidClipPlugin erkennt eingehende Twitch-Raids und spielt automatisch einen zufälligen Clip des raidenden Kanals in einer OBS-Browserquelle ab.

## Erster Start

Beim ersten Start erscheint automatisch der Einrichtungsassistent:

1. **Twitch-Anwendung öffnen** anklicken.
2. In der Twitch Developer Console eine eigene Anwendung anlegen.
3. Client ID und Client Secret in den Assistenten kopieren.
4. **Speichern und fortfahren** anklicken.

Der Assistent erscheint danach nicht erneut. Falls die verschlüsselte Datei fehlt oder nicht mehr gelesen werden kann, wird die Einrichtung automatisch erneut angeboten.

## Sicherheit

- Im Projekt und in der EXE sind keine Twitch-Zugangsdaten enthalten.
- Client ID und Client Secret werden mit Windows DPAPI verschlüsselt und an das aktuelle Windows-Benutzerkonto gebunden.
- Twitch Access Token und Refresh Token werden ebenfalls verschlüsselt gespeichert.
- Eine vorhandene alte Klartext-Token-Datei wird automatisch verschlüsselt übernommen und anschließend gelöscht.
- Im Repository befindet sich nur `Config/config.template.json` mit leeren Platzhaltern.
- `.gitignore` verhindert, dass eine lokale `config.json` versehentlich veröffentlicht wird.

Die geschützten Daten liegen im lokalen Anwendungsdatenordner des Windows-Benutzers und können nur von diesem Benutzer auf diesem Windows-System entschlüsselt werden.

## OBS einrichten

1. In OBS unter **Werkzeuge → WebSocket-Servereinstellungen** den Server aktivieren.
2. Host, Port und Passwort in der RaidClip-GUI eintragen.
3. **OBS-Quelle erstellen** anklicken. Die Browserquelle `RaidClip` wird automatisch in der aktiven Szene erstellt oder aktualisiert.
4. **Plugin starten** anklicken.

## Einstellungen

Twitch-Kanal, OBS-Verbindung, Clip-Zeitraum, Retries, Clipdauer, Lautstärke, Raid-Cooldown, Blacklist und Chataktionen werden direkt in der GUI verwaltet.

Technische Standardwerte stehen in `Config/config.template.json`. Die Datei enthält absichtlich keine echten Client-Zugangsdaten.

## Chat-Moderation

Das optionale Modul läuft unabhängig von Raid-Erkennung und Clip-Wiedergabe. Im Tab **Chat-Moderation** erscheinen neue Nachrichten mit direkten Schaltflächen für Timeout, Ban und Löschen.

Der Wortfilter ist standardmäßig deaktiviert. Bei Aktivierung löscht er nur die betroffene Nachricht; automatische Timeouts oder Bans werden bewusst nicht ausgeführt. Streamer, Moderatoren und VIPs können vom Filter ausgenommen werden. Beim ersten Start nach dem Update fordert Twitch die zusätzlichen Chat- und Moderationsrechte an.

## EXE erstellen

Den Ordner entpacken und `EXE_ERSTELLEN.cmd` doppelt anklicken. Die fertige App erscheint anschließend im Ordner `fertige-exe`.

## Windows-Installer erstellen

1. Einmalig [Inno Setup 6](https://jrsoftware.org/isdl.php) installieren.
2. `BUILD_INSTALLER.bat` doppelt anklicken.
3. Der fertige Installer liegt anschließend unter `installer-output/RaidClipPlugin-Setup-1.2.3.exe`.

Der Installer arbeitet ohne Administratorrechte, enthält die benötigte .NET-Laufzeit, legt einen Startmenü-Eintrag an und bietet optional eine Desktop-Verknüpfung.

## Auto-Update über GitHub Releases

Beim Programmstart lädt RaidClip die als GitHub-Release-Asset veröffentlichte `update.json`, sofern **Automatisch nach Updates suchen** aktiviert ist.

Ist eine neuere Version vorhanden, zeigt die GUI **Update verfügbar** mit:

- **Changelog anzeigen**
- **Update installieren**
- **Überspringen**

Beim Installieren wird das ZIP oder die EXE heruntergeladen und über SHA-256 geprüft. Anschließend startet RaidClip eine separate temporäre Updater-EXE und beendet sich. Erst der Updater ersetzt die alten Dateien und startet RaidClip neu. Fehler der Haupt-App stehen im normalen Log; Fehler beim Ersetzen oder Neustarten unter `%LocalAppData%\RaidClipPlugin\logs\updater-JJJJ-MM-TT.log`.

### GitHub Release veröffentlichen

1. `BUILD_INSTALLER.bat` ausführen. Dadurch entstehen der normale Installer und `RaidClipPlugin-Update-1.2.3.zip`.
2. Ein GitHub Release erstellen und das Update-ZIP hochladen.
3. `UPDATE_DATEI_ERSTELLEN.cmd` starten und die endgültige GitHub-Downloadadresse des ZIPs eingeben.
4. Die erzeugte `update.json` ebenfalls als Release-Asset hochladen.
5. Vor der Veröffentlichung in `Config/config.template.json` bei `Update.ManifestUrl` diese stabile Adresse eintragen:

   `https://github.com/Funjii95/RaidClipPlugin/releases/latest/download/update.json`

Die Struktur von `update.json` ist in `Installer/update.example.json` dokumentiert.

Persönliche Einstellungen, verschlüsselte Twitch-Zugangsdaten und Clip-Historie bleiben bei einem Update erhalten.
