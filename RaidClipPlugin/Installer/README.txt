RAID CLIP PLUGIN – RELEASE 1.2.7

1. Inno Setup 6 installieren:
   https://jrsoftware.org/isdl.php

2. BUILD_INSTALLER.bat doppelt anklicken.

Danach liegen unter installer-output:
- RaidClipPlugin-Setup-1.2.7.exe
- RaidClipPlugin-Update-1.2.7.zip

GITHUB AUTO-UPDATE

1. Ein GitHub Release anlegen und das Update-ZIP hochladen.
2. UPDATE_DATEI_ERSTELLEN.cmd ausführen und die GitHub-Adresse
   des ZIPs sowie den Changelog eingeben.
3. Die erzeugte update.json ebenfalls zum Release hochladen.
4. In Config/config.template.json als ManifestUrl eintragen:
   https://github.com/Funjii95/RaidClipPlugin/releases/latest/download/update.json

Die App prüft die SHA256-Prüfsumme, bevor der separate Updater
Dateien ersetzt. Auto-Update kann in der GUI abgeschaltet werden.
