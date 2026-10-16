# Prüfung der RPG-Kaufbefehle

## Befund

Vor dieser Änderung war ausschließlich `!perk <ID>` als Kaufbefehl in der CommandRegistry und im RpgChatCommand vorhanden. `!buyperk` fehlte in beiden Stellen, wurde deshalb weder als Spielbefehl noch als RPG-Befehl erkannt und im Minigame-Dispatcher als unbekannt ignoriert. Das erklärt die stumme Antwort, ohne einen Fehler in der eigentlichen Perlenabbuchung vorauszusetzen.

`!perk damage` führt bereits über den vorhandenen `RpgProgressionService.BuyPerkAsync` zur gesperrten, idempotenten Kontotransaktion. Bei aktivierten Minigames, Punkten und RPG kauft es das nächste Damage-Level für den jeweils gespeicherten Preis. Global deaktivierte Commands und vorgelagerte Berechtigungsregeln bleiben weiterhin verbindlich.

## Implementierung

- `!perk damage` bleibt der dokumentierte Hauptbefehl. `!buyperk damage` ist nun ein Alias desselben Registry-Eintrags `rpg.perk`, kein zweiter Kaufhandler.
- `RpgCommandCatalog` ist die gemeinsame Quelle für Primärbefehle, Aliase und Syntax. CommandRegistry und RPG-Erkennung verwenden denselben Katalog. Die Aliasverarbeitung normalisiert nur den Befehlsnamen; Perk-IDs und Preise werden nicht neu definiert.
- Das Abschalten von `rpg.perk` schaltet den Alias mit ab. Bestehende Rollenüberschreibungen gelten für denselben Registry-Eintrag. Der Alias führt nicht an der normalen Commandverwaltung vorbei.
- Falsche Parameter erzeugen einen konkreten Nutzungshinweis, bevor eine Transaktion ausgeführt wird. Beispiele: `!perk`, `!perk damage extra`, `!equip <ID> invalidslot`, ungültige Seitenzahlen.
- Unbekannte Perk-IDs nennen die vorhandenen konfigurierten IDs und den richtigen Kaufaufruf.
- `!rpg` bzw. `!rpg help`/`!rpg hilfe` zeigt kurze Hilfe; unbekannte Unterbefehle wie `!rpg foo` nennen die richtige Syntax. Völlig fremde `!foo`-Befehle werden weiterhin anderen Modulen/Custom-Commands überlassen, statt einen zweiten globalen Fehlerhandler einzuführen.
- Im bestehenden Zwei-Sekunden-RPG-Cooldown erfolgt ein Wartehinweis statt einer stillen Rückkehr. Die Dauer wurde nicht verändert.
- Kaufdetails enthalten zusätzlich den bereits gespeicherten neuen Kontostand, nächsten konfigurierten Upgrade-Preis und Maximallevel. Chat liest diese Fakten aus dem erfolgreichen Ergebnis und ruft den Kauf nicht erneut auf.

## Geprüftes Beispiel ohne Änderungen an Standardpreisen

Bei 10.000 Startperlen und den Economy-1.2-Standardpreisen:

1. `!perk damage`: Level 1, +2 % Schaden aus diesem Perk, Kosten 500, Stand 9.500, nächstes Upgrade 1.000.
2. Nach dem normalen Cooldown `!buyperk damage`: Level 2, +4 % insgesamt aus diesem Perk, Kosten 1.000, Stand 8.500, nächstes Upgrade 2.000.
3. Wiederholte Verarbeitung derselben Twitch-Nachrichten-ID: kein weiterer Abzug und kein weiteres Level.
4. Am Maximallevel: eindeutiger Hinweis statt weiterer Abbuchung; keine nächste Kostenstufe wird behauptet.

Gespeicherte abweichende Preise bleiben gültig: Ein Test mit Basiskosten 250 und Wachstum 3 ergibt exakt 250 Abbuchung und 750 als nächste Kosten. Kein Preset wird durch den Alias oder die Registrierung angewendet. Inventare und alte Perk-Level werden nicht migriert oder zurückgesetzt.

## Tests und Grenzen

Neue Tests decken Hauptbefehl, Alias und Großschreibung des Befehls, Registrierung aller Katalogbefehle, identische Deaktivierung, unbekannte IDs, falsche Syntax, Hilfe, tatsächliche Abzüge/Level/Folgekosten, konfigurierte Altpreise, Maximallevel, fehlende Perlen, wiederholte Aktions-IDs, Berechtigungsablehnung und tatsächliche Adventure-Routenweiterleitung samt Cooldown ab. Keine echte Twitch-Nachricht und keine produktive Kontobuchung im Test.

Abschluss: **449 Tests bestanden**, Release-Build erfolgreich (0 Fehler; bekannte NU1510-/WindowsBase-WebView2-Warnungen). Keine Veröffentlichung, keine neue Releaseversion, keine Balanceänderung.
