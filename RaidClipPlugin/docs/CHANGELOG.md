# Changelog

## 1.2.7

- `!gamble all` setzt alle verfügbaren Punkte auf einen Wurf
- `!give @name <punkte>` überträgt Punkte sicher zwischen Zuschauern
- `!addpoints @name <punkte>` erzeugt als Broadcaster oder Mod neue Punkte

## 1.2.6

- Modulares Minigame-System mit gemeinsamer Punkte-Datenbank
- Passive Punkte für Watchtime, Chat, Follow, Sub, Raid und Channel Rewards
- Neue Commands !daily, !top, !rang und !profil
- Optionale Casino-Spiele Coinflip und Slots
- Dauerhafter Jackpot mit konfigurierbarer Chance und Einzahlung
- Kontolimit sowie tägliche Spiele-, Verlust- und Gewinnlimits
- Sieben übersichtliche Minigame-Reiter mit Rangliste und Historie
- Sicherer JSON-Export und -Import mit automatischem Backup

## 1.2.5

- Neues optionales Modul „Chat-Minigame“ mit eigener Sidebar-Seite
- Dauerhaftes Punktesystem für aktive Chat-Zuschauer
- !punkte mit User-Cooldown
- !gamble mit W100, Einsatzgrenzen und konfigurierbaren Auszahlungen
- Admin-Commands add/remove/set für Broadcaster und Mods
- Globaler Command-Cooldown und thread-sichere lokale Speicherung
- Punktedaten können in der GUI zurückgesetzt werden

## 1.2.4

- Neue linke Kachelnavigation
- Raid Clip und Chat-Moderation sind vollständig getrennte GUI-Seiten
- Eigene Einstellungen und Statusanzeige pro Modul
- Übersichtlicheres Layout für den Live-Betrieb

## 1.2.3

- Eigenständiges, optionales Modul „Chat-Moderation“
- Twitch-Chat über EventSub mitlesen und in einem eigenen GUI-Tab anzeigen
- Timeout, Ban und Nachrichtenlöschung direkt pro Chatzeile
- Optionaler Wortfilter, der Treffer sicherheitshalber nur löscht
- Optionale Whitelist für Streamer, Moderatoren und VIPs
- Moderationsfehler werden isoliert geloggt und blockieren keine Raid-Clips
- Neue Twitch-Berechtigungen für Chat und Moderation

## 1.2.2

- Sicherer Einrichtungsassistent für Twitch-Zugangsdaten
- OBS-Browserquelle auf Knopfdruck erstellen
- GitHub-Auto-Update mit SHA256-Prüfung
