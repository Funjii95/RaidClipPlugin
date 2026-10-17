# Items gegen Perlen zerlegen (2.1.14)

## Verwendung

1. `!inv` anzeigen; es gelten die bekannten sicheren Inventarnummern.
2. `!salvage 2` zeigt den genauen Gegenstand, Roll/Effekte und den Zerlegebetrag.
3. `!salvage 2 confirm` innerhalb von 60 Sekunden entfernt genau die vorgestellte Instanz und schreibt den Betrag gut. Auch normale Items benötigen diese Vorschau/Bestätigung; es gibt kein versehentliches Sofortzerlegen.

Sammelbefehle: `!salvage duplicates` oder `!salvage all common`, danach derselbe Befehl mit `confirm`, also `!salvage duplicates confirm` / `!salvage all common confirm`. Kein Sammelbefehl entfernt Legendarys, Setteile, angelegte, gesperrte oder favorisierte Items. Nach einer erfolgreichen Entfernung erneut `!inv` aufrufen, da sich Nummern geändert haben.

`!itemlock 2` bzw. `!favorite 2` aktiviert den jeweiligen Schutz. `!itemlock 2 off` / `!favorite 2 off` hebt ihn auf. Beide Flags werden gespeichert und in Inventar/Details markiert. Ein Item mit beiden Flags bleibt geschützt, bis beide aufgehoben sind. Diese Flags sperren Zerlegen, nicht Ausrüsten.

Legendäre Items und alle Items mit SetId können ausschließlich explizit ausgewählt und nach Warnhinweis bestätigt werden. Eine Bestätigung allein ohne aktuelle passende Vorschau führt niemals eine Entfernung aus.

## Werte und Konfiguration

`Rpg.Salvage`: `Enabled` (Standard true), `ConfirmationSeconds` (60; zulässig 10–300), `MaximumBatchSize` (100; 1–100) und `Values` nach Seltenheit mit Minimum/Maximum:

| Seltenheit | Standardwerte |
|---|---|
| Common | 25–50 |
| Uncommon | 75–150 |
| Rare | 200–400 |
| Epic | 600–1200 |
| Legendary | 2000–4000 |

Wertebereiche sind inklusive; konfigurierte Werte müssen 0 ≤ Minimum ≤ Maximum ≤ 1.000.000.000 erfüllen. Falls eine Instanz einen gespeicherten nichtnegativen `SalvageValue` besitzt, ist dieser maßgeblich. Andernfalls wird reproduzierbar SHA-256 aus der versionierten InstanceId/Seltenheit gebildet und in den konfigurierten Bereich abgebildet. Derselbe Gegenstand hat mit denselben Konfigurationswerten immer denselben Preis, auch nach Neustart. Keine Neuwürfelung alter Items, kein schreibender Migrationslauf. Eine Konfigurationsänderung kann Fallback-Preise verändern; eine laufende Bestätigung wird bei abweichendem Preis abgelehnt.

Itemdetails zeigen den tatsächlichen Preis und Schutz-/Bestätigungswarnungen. PearlBonus, Sushi, Perks und Setboni multiplizieren Zerlegegutschriften nicht. Das eingestellte Konto-Maximum gilt; bei Überschreitung wird die komplette Transaktion abgelehnt, statt ein Item gegen eine gekürzte Auszahlung zu vernichten.

## Duplikate und Vorschauen

Gleiche Definition ist nicht gleicher Roll. Duplikate müssen ItemId, Name/Beschreibung/Bild, Seltenheit, Typ/Slot, SetId, Verbrauchsrezept, alle Attributwerte und vollständige Effekte gemeinsam haben. Die Effektreihenfolge ist egal. InstanceId, Fundzeit und Zerlegepreis unterscheiden sich zwischen Exemplaren und werden nicht als Gameplay-Roll behandelt. Items ohne definierte ItemId werden vorsichtshalber nicht automatisch als Duplikate gruppiert.

Pro exakter Gruppe bleibt mindestens ein Item erhalten, bevorzugt ein bereits angelegtes oder geschütztes. Andere Rolls desselben Itemtyps bleiben vollständig erhalten. `all common` ist dagegen die ausdrückliche Auswahl aller ungeschützten, nicht angelegten Common-Items ohne Setzugehörigkeit, unabhängig von Duplikaten.

Eine serverseitige Vorschau enthält konkrete IDs, Fingerprints und Einzelpreise. Sie bindet an bestätigte Twitch-ID, Auswahl, gesamte Inventarreihenfolge und Ablaufzeit. Neue Vorschau ersetzt die vorherige. Im Chat werden IDs nicht benötigt oder ausgegeben. Nach Neustart sind alle Vorschauen ungültig; gespeicherte Items/Flags bleiben bestehen. Lange Listen werden über den vorhandenen `!rpgtext N`-Mechanismus gelesen, nicht automatisch als Nachrichtenserie verschickt. Die Bestätigungsfrist läuft unabhängig vom Textcache weiter. Maximal 100 Items pro Vorschau; größere Auswahlen werden mit Hinweis abgelehnt, nicht teilweise still ausgeführt.

## Transaktion und Extension

`RpgSalvageService` bietet `PreviewAsync` (nummerierter Chat), `PreviewInstancesAsync` (künftige grafische Auswahl vollständiger IDs), `PendingQuote`, `ConfirmAsync`, `SetProtectionAsync` und den gemeinsamen deterministischen Preisrechner `Value`. Alle aufrufenden Adapter müssen eine verifizierte Twitch-ID liefern. Es gibt keine öffentliche API.

Vorschau liest nur bestehende Dienste. Bestätigung löst die gespeicherten IDs im autoritativen ViewerPointStore erneut auf und prüft Ablauf, Eigentum, Inventarstand, Fingerprint, Schutz und Preis innerhalb derselben Kontosperre. Erst danach werden alle ausgewählten Items entfernt und alle Perlen gutgeschrieben. Die bestehenden Aktions-IDs verhindern wiederholte Verarbeitung; parallele Anfragen können entfernte IDs nicht erneut vergüten. Erfolgreich verwendete Tickets werden verworfen. Vor Entfernung erzeugt der bestehende Store eine datierte/GUID-basierte `.rpg-delete-*.bak`-Sicherung. Fehler beim Speichern rollen den In-Memory-Kontostand zurück; die ursprüngliche Datei bleibt durch die vorhandene atomare Speicherung erhalten.

Kein zweites Inventar, keine neue Effektberechnung, keine Änderung an Loot- oder Kampfchancen. Die neuen optionalen Itemfelder haben Legacy-kompatible Standardwerte: SalvageValue=null, IsLocked=false, IsFavorite=false.
