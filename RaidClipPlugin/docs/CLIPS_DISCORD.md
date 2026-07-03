# Clips & Discord

## Einrichtung

1. In der Anwendung den Bereich **Clips & Discord** öffnen.
2. Clip-Command aktivieren und Berechtigungen, Cooldowns und Limits festlegen.
3. Für Discord den Bot-Token, die Server-ID und mindestens einen Textkanal eintragen.
4. **Channels prüfen** verwenden. Der Bot benötigt **View Channel**, **Send Messages** und **Embed Links**.
5. Mit **Clips & Discord speichern** übernehmen.
6. Das Plugin neu starten, wenn das Modul gerade erst aktiviert wurde.

Discord-Bot-Token und Webhook-URLs werden per Windows-DPAPI im Benutzerprofil verschlüsselt gespeichert und nicht in **settings.json** geschrieben.

## Twitch-Anmeldung

Das Modul benötigt den Twitch-Scope **clips:edit**. Nach dem Update muss Twitch deshalb möglicherweise einmal neu verbunden werden. Die bestehende Tokenverwaltung speichert Access- und Refresh-Token verschlüsselt und erneuert sie automatisch.

## Hinweis zur Twitch-API

Der offizielle Live-Endpunkt **POST /helix/clips** akzeptiert derzeit nur **broadcaster_id** und optional **has_delay**. Twitch vergibt beim direkten Live-Clip zunächst seinen Standardtitel und seine Standardlänge. Der konfigurierte Command-Titel wird zuverlässig für Chat, Log und Discord verwendet. Twitch stellt die zurückgegebene **edit_url** für eine nachträgliche manuelle Bearbeitung bereit.
