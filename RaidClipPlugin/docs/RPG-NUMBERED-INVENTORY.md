# Kurzes Inventar und automatische Ausrüstung

## Chatbefehle

- `!inv [Seite]` = `!inventory` = `!inventar`: nummeriertes Inventar mit Name, Seltenheit, tatsächlichen Attributen und Ausrüstungsstatus.
- `!equip 1`: Gegenstand 1 ausrüsten, Slot automatisch erkennen.
- `!equip 2 ring1` / `!equip 2 ring2`: einen Ringplatz gezielt ersetzen.
- `!item 1 [Seite]`: vollständige Details einschließlich interner ID für Debugging/API, Beschreibung und Werte.
- `!gear [Seite]` = `!equipment` = `!ausrüstung`: aktuell ausgerüstete Gegenstände.
- Alte vollständige IDs bleiben für `!item <ID>` und `!equip <ID> <Slot>` verwendbar. Auch automatische Slotwahl per ID ist möglich.

## Nummerierung und Sicherheit

Nummern beginnen bei 1 und folgen der gespeicherten Inventarliste. Neue Funde werden wie bisher angehängt. Es gibt keine Sortierung nach Namen, Seltenheit oder zufälligen GUIDs. Nummern gelten über alle Seiten hinweg; mehrere identische Gegenstände bleiben getrennte Instanzen.

Vor Verwendung einer Nummer muss der Zuschauer einmal `!inv` aufrufen. Jede Inventaranzeige merkt sich serverseitig die vollständige ID-Reihenfolge dieses Kontos. `!item <Nummer>` prüft diese Ansicht ebenfalls. Ändert sich die Reihenfolge, etwa durch Fund oder Löschung, wird die Nummer abgelehnt: erst erneut `!inv` anzeigen. Ein Ausrüstungswechsel allein verändert die Nummern nicht.

Beim Ausrüsten werden Ansicht, Nummer, tatsächliche Instanz und Slot innerhalb derselben bestehenden Kontotransaktion geprüft. Die Zuordnung kommt nie aus Clientdaten. Parallel laufende Ausrüstungsaktionen werden durch die vorhandene Store-Sperre serialisiert. Eine zwischenzeitliche Inventaränderung führt entweder zu einem bereits korrekt abgeschlossenen Ausrüsten der ursprünglichen Instanz oder zur Ablehnung, niemals zum Ausrüsten der nachgerückten Instanz.

Die Ansicht liegt nur im Arbeitsspeicher des jeweiligen Stores, getrennt nach bestätigter Twitch-ID. Nach Neustart ist erneut `!inv` erforderlich. Inventarliste, vollständige InstanceIds und gespeicherte Ausrüstung bleiben unverändert erhalten. Dienste/API können weiterhin vollständige IDs verwenden; der bestehende `EquipAsync`-Aufruf bleibt verfügbar.

## Slotwahl

Der bereits aus der Itemdefinition gespeicherte `Slot` ist maßgeblich – auch für ältere Items ohne Kind-Metadaten. Handschuhe, Waffe, Helm, Rüstung, Schuhe und Amulett brauchen keinen Zusatz. Items ohne Slot werden als Schatz beziehungsweise Verbrauchsgegenstand gekennzeichnet und abgelehnt.

Ringe belegen zuerst Ring 1, dann Ring 2. Ein bereits ausgerüsteter Ring behält bei automatischer Wahl seinen Platz. Sind beide Plätze durch andere Ringe belegt, nennt der Bot die beiden expliziten Ersetzungsbefehle. Explizites Verschieben entfernt die Instanz zuerst aus allen bisherigen Slots. Falsche Slots werden ohne Änderung abgelehnt.

## Chatlänge und Kompatibilität

Normale Inventar-/Ausrüstungsseiten zeigen keine langen IDs. Seiten enthalten höchstens drei Einträge mit konservativem Inhaltslimit; lange Texte werden als Fortsetzung ausgegeben, nicht still abgeschnitten. `Weiter: !inv N` führt zur nächsten Seite. Bei `!item` ist die interne ID bewusst als Detailinformation abrufbar.

Keine Schemaänderung, Migration, Itemlöschung, Kosten-, Perlen- oder Balanceänderung. Kein zusätzlicher Belohnungsaufruf. Tests verwenden ausschließlich temporäre Konten; kein Test im Live-Chat. Keine automatische Veröffentlichung.

## Prüfung am 16.10.2026

- Vollständige Testsuite: 462 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
- Release-Build: erfolgreich, 0 Fehler; 10 vorhandene Paket-/WindowsBase-/Nullable-Warnungen.
- Neue Tests: leere/fehlende Ansicht, globale Nummern und Seiten, identische Ringe, alle Einzelslots, explizite Ringwahl, volle Ringplätze, unveränderte IDs nach Neustart, veraltete Ansichten nach Löschung, konkurrierende Inventar-/Ausrüstungsaktionen, Schätze, falsche Slots und alte ID-Syntax.
