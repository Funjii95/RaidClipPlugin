# Kleine Unterwassergegner

## Ablauf

Jeder gültige RPG-Tauchgang (`!dive`, auch kostenlos) würfelt einmal auf eine Begegnung, standardmäßig **15 %**. Die Chance ist zwischen **10 und 20 %** einstellbar. Ungültige Einsätze, doppelte Anfragen, Cooldowns oder fehlende HP erzeugen keinen Kampf. Angeln erzeugt keine Begegnung.

Der automatische Kampf ist ein **zusätzliches Ereignis**, unabhängig davon, ob der ursprüngliche Tauchgang Perlen oder Beute gebracht hat. Die originale Einsatzabrechnung und Tauchauszahlung bleiben exakt bestehen. Eine Gegnerniederlage zieht keine weiteren Perlen ab und entfernt keine normalen Tauchfunde oder vorhandenen Gegenstände. Dadurch kann ein Tauchgang Beute liefern, obwohl der anschließende Gegnerkampf verloren geht; beide Ergebnisse werden ausdrücklich getrennt dargestellt.

## Stufen

| Mindesteinsatz | Gegner | Gegner-HP | Gegenschaden pro Runde | Sieg-EXP |
|---|---|---:|---:|---:|
| 0 | Schnappkrabbe | 25 | 3–6 | 30 |
| 250 | Korallenräuber | 40 | 5–9 | 60 |
| 1.000 | Tiefsee-Muräne | 60 | 7–12 | 100 |
| 5.000 | Abgrundwächter | 85 | 10–16 | 180 |
| 10.000 | Leviathan-Jungtier | 110 | 12–20 | 300 |

Die höchste passende Stufe wird anhand des tatsächlich validierten Einsatzes gewählt. Alle Gegnernamen, Grenzen, HP-, Schadens- und EXP-Werte sind konfigurierbar.

Pro Runde verursacht der Spieler 10–30 Grundschaden, berechnet mit der vorhandenen zentralen Schadens-/Crit-Formel und seinen für Tauchen geltenden Ausrüstungs-/Perk-/Buffwerten. Überlebt der Gegner, schlägt er zurück. Nach maximal sechs Runden (einstellbar 1–20) ohne Sieg endet der Kampf verloren. 0 HP beendet den Kampf ebenfalls. Es gibt weder EXP-Verlust noch Zerstörung permanenter Items.

Boss-only Sushi wirkt nicht auf diese Kämpfe und wird nicht verbraucht. Bereits für den Tauchgang aktive Tauchbuffs folgen weiterhin ihrer normalen Verbrauchsregel. Eine Niederlage mit 0 HP setzt gegebenenfalls auch die gespeicherte Todessperre für den zuletzt angegriffenen Boss; Heilung hebt diese nicht auf.

## Bessere Gegnerbeute

Bei einem Sieg wird **ein zusätzlicher Gegenstand** aus dem besseren von **zwei vollständigen normalen Tauch-Lootwürfen** vergeben. Höhere Seltenheit gewinnt; Gleichstand behält den ersten Fund. Es gibt keine zwei Gegneritems und keinen rekursiven Extra-Loot-Proc.

Bei einer normalen Fundchance `p` beträgt die Chance auf einen Gegnerbonusfund `1 - (1-p)²`: beispielsweise werden aus 25 % insgesamt 43,75 %, aus 40 % werden 64 %. Die Verteilung verschiebt sich ebenfalls zu selteneren Funden. Das ist eine bessere **Chance**, keine Garantie, dass jeder einzelne Gegnerfund den konkreten Tauchfund übertrifft.

Die beiden Würfe verwenden denselben bestehenden Pool, dieselbe Itemgenerierung, Luck-/Tauchbuff-/Ausrüstungs-/Hot-Time-Boni und dieselben einsatzabhängigen Seltenheitsgrenzen. Höherer Einsatz verbessert die Beute somit entsprechend den vorhandenen Tauch-Lootstufen. Die konfigurierte Legendary-Mindestgrenze (im aktuellen Standard 10.000) wird niemals umgangen. Auch ein siegreicher Kampf kann ohne besonderen Fund enden.

Gegner-EXP gibt es nur bei Sieg und überlebendem Spieler; bei Niederlage keine Gegner-EXP oder Gegnerbeute. Normale Tauchbeute bleibt erhalten.

## Oberfläche und Datensicherheit

Punkte & Minigames → RPG → **Unterwassergegner** enthält Aktivierung, Begegnungschance, Rundengrenze und die editierbare Stufenliste (`Tiers`, Schaltfläche `…`). Speichern verwendet die reguläre Konfigurationsvalidierung. Benötigt aktiviertes RPG- und HP-System.

`RpgEnemyService` nutzt den vorhandenen `RpgHealthService.ResolveEnemy` und `RpgLootService.RollEnemy`, keine zweite Spielerdatenbank. Kampfberechnung, HP, EXP, Bonusbeute, Buffverbrauch und Einsatz werden innerhalb desselben bestehenden `ApplyCasinoAsync`-Commits gespeichert. Twitch-Message-ID, persistenter Cooldown und bestehende Sperre schützen gegen Wiederholungen/parallele Anfragen. Speicherfehler rollen alle Änderungen zurück.

Der Chat zeigt Gegnername, Sieg/Niederlage, erhaltene EXP, erlittenen Schaden und verbleibende HP im bestehenden Tauchresultat. Bonusfunde werden in dessen Extra-Funden aufgeführt. Keine zusätzlichen Chatbefehle nötig. Bestehende `!hp`, `!stats` und `!inv` zeigen die gespeicherten Ergebnisse.

17 neue automatisierte Fälle decken Begegnungsgrenzen, fünf Einsatzstufen, bessere Beute, Legendary-Mindestgrenze, Niederlage/Tod, gemeinsame Boss-Sperre, atomare Buchung, Wiederholungsschutz/Persistenz, Speicherfehler, Deaktivierung und unberührtes Boss-Sushi ab. Keine automatische Veröffentlichung.
