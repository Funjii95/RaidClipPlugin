# RaidClipPlugin 2.8.4

- Doppelt so großes Spielstandbudget (512 KiB) und größere Aktionsantworten (768 KiB).
- Begrenztes Long-Polling für Aufträge und Ergebnisse; bestehende Wiederholschutzbelege und Spielstände bleiben erhalten.
- Alte Muschel auch aus gespeicherten Beutekatalogen entfernt. Bereits besessene Exemplare und die Muschelöffnung bleiben erhalten.
- Individuelle Namen für alle 80 Questset-Gegenstände; IDs, Werte und Aufwertungen unverändert.

Zuerst Backend und Snapshot-Publisher aktualisieren. Der vorhandene Capability-Check verhindert die Synchronisierung mit einem Publisher mit zu kleiner Kapazität.
