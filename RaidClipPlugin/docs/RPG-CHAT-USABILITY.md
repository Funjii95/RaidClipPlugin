# RPG-Chat: kurze Übersicht, vollständige Details

- `!inv [Seite]`: Nummer, Slot-Symbol, Itemname und Seltenheit; angelegte Items zusätzlich mit ✓ und Slot. Schätze/Verbrauchsgegenstände werden als solche gekennzeichnet. Keine Attribute, IDs oder langen Effekttexte in der Übersicht.
- `!item <Nummer> [Seite]`: Name, Seltenheit, deutscher Gegenstandstyp und Slot, Ausrüstungsstatus, alle vorhandenen Attribute, dynamisch erzeugte Effektbeschreibungen, Setzugehörigkeit und aktuelle Bonuszustände sowie Beschreibung. Die erste Seite ist eine kompakte Zusammenfassung, weitere Seiten werden ausschließlich auf Anforderung gesendet. Keine abgeschnittenen Textteile und keine automatische Nachrichtenserie.
- `!equip <Nummer> [ring1|ring2]`: vorhandene automatische Slotwahl bleibt erhalten. Die Antwort zeigt die tatsächlich gespeicherten Attributboni und vollständigen Spezialeffekte samt Chance, Stärke und Bedingungen. Bei Überlänge greift die bestehende sichere Navigation über `!rpgtext N`, ohne die Aktion zu wiederholen.
- `!gear [Seite]`: kurze Ausrüstung mit aktuellen Setboni auf Folgeseiten. `!sets` bleibt die vollständige Setübersicht.

Alle Attributanzeigen verwenden zentrale deutsche Namen: Kritische Trefferchance, Kritischer Schaden, Glück, Angelglück, Tauchglück, Perlenbonus, Doppelangriffschance, Dreifachangriffschance, Zusätzlicher Lootwurf und Sushi-Erhaltung. Effekttexte entstehen aus den gespeicherten Effektwerten einschließlich Voraussetzungen/Cooldowns. Extra-Loot beschreibt korrekt einen zusätzlichen Wurf, der auch leer ausgehen kann.

Interne Instanz- und Effekt-IDs werden nicht im normalen Chat gezeigt. Die bisherige Eingabe vollständiger IDs bleibt kompatibel; die Modelle und Dienste speichern sie unverändert. `RpgActionDetails` enthält für Equip nun zusätzlich abgetrennte Effekt-Snapshots und den Setnamen. Keine separate Datenbank oder neue Effektberechnung. `RpgItem` behält IconId, Seltenheit, Attribute, Effekte und SetId für eine spätere grafische Extension.

Die bisherige ID-Ausgabe in `!item` und ausführliche Werte in `!inv` aus älteren Dokumentationsständen sind damit abgelöst. Keine Änderungen an Spielbalance oder gespeicherten Inventaren. Keine Veröffentlichung.
