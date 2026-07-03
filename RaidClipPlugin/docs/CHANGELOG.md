# Changelog

## 1.5.5

- Discord-Webhooks werden im laufenden Betrieb direkt an den Clip-Command angebunden
- Fehlgeschlagene Discord-Zustellungen erscheinen sichtbar als letzter Fehler
- Neuer echter Webhook-Routingtest verhindert stilles Überspringen
- Einstellbare Raid-Verzögerung von 0 bis 600 Sekunden in der GUI
- Chatnachricht, Shoutout und Clip-Wiedergabe starten nach der konfigurierten Verzögerung

## 1.5.4

- Neuer `!jackpot`-Command zeigt den aktuell gespeicherten Jackpot im Twitch-Chat
- Eigener Command-Cooldown verhindert Spam

## 1.5.3

- Punktesystem läuft unabhängig von den Chat-Minispielen
- Zuschauer sammeln weiterhin Anwesenheits-, Chat-, Follow-, Sub-, Raid- und Kanalpunkte, wenn Spiele deaktiviert sind
- Punktebefehle wie `!punkte`, `!daily`, `!give`, `!addpoints` und `!removepoints` bleiben verfügbar
- `!gamble`, Roulette, Slots und Coinflip werden separat über „Spiele aktivieren“ gesteuert
- Getrennte Validierung verhindert, dass deaktivierte Spiele das Punktesystem blockieren

## 1.4.2

- Minigame-Chat startet wieder zuverlässig bei gleichzeitig aktiven Musikwünschen
- Musikwünsche und passive Minigame-Ereignisse teilen sich eine EventSub-Verbindung
- Chat-Verbindung wird beim Start priorisiert

## 1.4.1

- Neuer vollständiger Stream-Start-Check mit 18 unabhängigen Prüfungen
- Konfigurierbares Check-Profil, Wiederholung, sichere Diagnose und Ergebnisexport
- Bestätigter Streamstart mit optionaler Startszene und OBS-Streaming
- Der Jackpot wird ausschließlich bei einer gewürfelten 100 in `!gamble` ausgeschüttet
- Die zufällige Jackpot-Gewinnchance wurde aus Logik und GUI entfernt
- Der Broadcaster erhält nun Chat-, Anwesenheits- und Lurkerpunkte
- Europäisches Roulette mit Farben, Gerade/Ungerade, 1–18, 19–36 und exakten Zahlen
- Konfigurierbare Roulette-Auszahlungen, Einsatzgrenzen und eigener Cooldown

## 1.4.0

- Schaltflächen zeichnen Beschriftungen zuverlässig und vollständig, auch im dunklen Theme
- Größere Navigation, Tabs und Aktionsbereiche verhindern abgeschnittene Texte
- Fenster und Einstellungsbereiche skalieren sauberer bei langen deutschen Beschriftungen
- Neues optionales Modul „Musikwünsche“ mit Spotify PKCE und sicherer Tokenablage
- Twitch-Kanalpunkte-EventSub, persistente Deduplizierung und Statusupdates
- Spotify-Suche, Queue/Sofortwiedergabe, Geräteauswahl, Filter und Blacklists
- Konfigurierbare Chattexte und Moderator-Commands
- Warteschlangen-GUI mit Wiederholen, Abspielen, Überspringen und Sperraktionen
- Automatisierte Tests für Links, Filter, Limits, Cooldowns und Deduplizierung

## 1.3.0

- Checkboxen im dunklen GUI-Theme wieder klar sichtbar und bedienbar
