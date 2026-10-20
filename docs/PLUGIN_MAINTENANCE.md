# Wartung und getrennte Twitch-Konten

## Geprüfte Architektur

Die Desktop-Anwendung verwendet .NET 10 / Windows Forms. `Program.cs` hält eine exklusive
Instanzdatei im Nutzerdatenordner und öffnet bei fehlenden Zugangsdaten den Einrichtungsassistenten.
`ConfigurationService` verbindet die Programmvorlage mit `settings.json`.
`TwitchCredentialStore` und `AuthenticationService` speichern Client ID/Secret und OAuth-Tokens
mit Windows-DPAPI für das jeweilige Windows-Konto. Ein Twitch-Kontowechsel legt keinen neuen
lokalen RPG-Datenordner an.

Nutzerdaten liegen unter `%LOCALAPPDATA%/RaidClipPlugin`, Programmdateien beim Installer unter
`%LOCALAPPDATA%/Programs/RaidClipPlugin`. RPG-Daten, Sicherungen, Datenschutzsperren,
Cloud-Outbox und Aktionsbelege dürfen bei Programmwartung nicht pauschal gelöscht werden.
Cloud-Sync und ausstehende Datenschutz-Löschungen laufen unabhängig vom gestarteten Spielbetrieb.
Ein Stop des Plugins beendet diese Hintergrunddienste deshalb nicht.

Der Installer kopiert Programmdateien ohne Löschanweisung für den Nutzerdatenordner.
Der Auto-Updater verwendet HTTPS, ein ZIP-Paket und dessen SHA256 aus dem Manifest.
Bisher überschrieb er Dateien ohne vollständiges Rollback. Die gemeinsame Kopierlogik wurde
auf eine vorherige Sicherung, eingeschränkte Programmdateien und Rollback bei Kopierfehlern umgestellt.

## Neue Bedienung

Unter **Einstellungen → Wartung**:

- **Neu initialisieren** lädt die gespeicherten Einstellungen und setzt die Verbindungsanzeige zurück.
  Nicht gespeicherte Eingaben werden nach Bestätigung verworfen. Der Spielbetrieb bleibt gestoppt.
- **Twitch Client ID / Secret neu einrichten** öffnet den Assistenten mit leeren Feldern und Link
  zur Twitch Developer Console. Abbrechen verändert nichts. Speichern legt zuerst eine
  DPAPI-verschlüsselte Sicherung der bisherigen Twitch-Dateien unter `maintenance-backups` an,
  entfernt beide alten Tokenformate und speichert neue Zugangsdaten. Bei einem Fehler wird
  die bisherige Dateibelegung wiederhergestellt. Die Sicherung ist nur unter demselben
  Windows-Konto entschlüsselbar und wird nicht automatisch wieder eingespielt.
- **Programmdateien reparieren / neu installieren** verwendet das feste offizielle Update-Manifest.
  Die gleiche oder eine neuere Version ist zulässig, ein Downgrade wird abgelehnt.
  Vor dem Download werden Zielversion und Neustart bestätigt. SHA256-Prüfung bleibt erforderlich.
  Zulässig sind die Programm-EXE, DLLs/PDBs, die zwei Laufzeitmanifeste und die Konfigurationsvorlage.
  Pakete mit Nutzerdaten werden vollständig abgelehnt. Verknüpfte oder überlappende Daten-/Programm-
  und Paketpfade werden abgelehnt. Bei Kopierfehlern werden ersetzte Dateien restauriert und neu
  angelegte Programmdateien entfernt. Bei unvollständigem Rollback erfolgt kein automatischer Start.

Wartung ist bei laufendem Plugin, unvollständig beendeten Hintergrundaufgaben, Verbindungsprüfung,
Datenverwaltung oder Update gesperrt. Cloud-Konfiguration, Cloud-Schlüssel, RPG-Daten und andere
Service-Zugänge werden bei diesen Aktionen nicht verändert. Eine allgemeine Datenlöschung wurde
bewusst nicht ergänzt; dafür bleiben die gezielten vorhandenen Verwaltungsfunktionen zuständig.

## Dennis und andere Nutzer

Neue Installationen erhalten keinen automatisch vorgegebenen Kanal „Funjii“ mehr.
Der gewünschte Kanal wird unter **Allgemein** eingetragen. Nach erfolgreicher Twitch-Anmeldung
zeigen obere Leiste und Begrüßung den Eigentümer des validierten Tokens, bevorzugt dessen
Twitch-Anzeigenamen. Ein eventuell anderer Zielkanal wird nicht als angemeldetes Konto ausgegeben.
Vor einer validierten Anmeldung bleibt die Anzeige neutral. Die Begrüßung verwendet die lokale Tageszeit.

RPG-Verwaltungsaktionen verändern zunächst den lokalen Datenbestand. Bei Cloud-Sync können
diese Änderungen anschließend synchronisiert werden. Eine eigene Twitch Client ID allein
trennt keine Cloud-Daten. Dennis benötigt eigene Cloud-Zuordnung und eigene Cloud-Schlüssel.
Für getrennte lokale Installationen auf demselben PC sind getrennte Windows-Konten erforderlich.

Neue Schutzprüfungen:

- Der gestartete Twitch-Zielkanal muss zu allen aktivierten Cloud-/Aktionskanal-IDs passen.
- RPG-Löschen, Datenschutz-Löschen, Perlen-Reset und Datenimport verlangen bei aktivierter Cloud
  eine erneut validierte Twitch-Sitzung des Cloud-Kanalinhabers. Eine abweichende ID wird abgelehnt.
  Die Änderungen sind nur bei vollständig gestopptem Spielbetrieb möglich.
- Der bisherige Punktedaten-Reset verwendet nun den gesicherten Perlen-Reset, der Level,
  Inventar und andere RPG-Felder erhält, statt die gesamte Spielerdatensammlung zu leeren.

Diese Prüfungen schützen die normale Desktop-Bedienung. Sie ersetzen keine serverseitige
Kanalberechtigung und machen weitergegebene Cloud-Schlüssel nicht unschädlich. Bereits verteilte
Versionen ändern sich dadurch nicht. Versehentlich gemeinsam verwendete Schlüssel müssen
serverseitig getrennt/ersetzt werden. Eine automatische Migration bestehender Cloud-Zuordnungen
oder eine Änderung von Cloud-Daten ist nicht Bestandteil dieser Umsetzung.

## Prüfung und Freigabe

Automatisiert: Desktop-Build, vollständige Desktop-Testreihe (760 Tests einschließlich Wartung),
isolierter `--ui-smoke`-Start. Neue Tests prüfen Kontobegrüßung, fremde Cloud-Kanalinhaber,
verschlüsselte Zugangssicherung und Tokeninvalidierung, Wiederherstellung bei Schreibsperre,
Ablehnung von Daten im Reparaturpaket sowie Rollback nach teilweisem Kopieren.

Vor Veröffentlichung noch mit Testkonten prüfen: echte Twitch-Anmeldung mit neuen App-Zugängen,
angezeigter Anzeigename bei abweichendem Zielkanal, Abbrechen des Assistenten und Neuinstallation
eines veröffentlichten Pakets in einer separaten Windows-Testinstallation. Dabei keine produktiven
Cloud-Schlüssel oder RPG-Daten verwenden. Keine echte Twitch-Anmeldung, Cloud-Löschung oder
Installation in die vorhandene Nutzerinstallation wurde bei der Entwicklung ausgeführt.

Die neue Version muss über den bestehenden Release-Prozess veröffentlicht werden, bevor die
Reparaturfunktion ein Paket dieser Version beziehen kann. Ein Pull Request veröffentlicht keine EXE.
