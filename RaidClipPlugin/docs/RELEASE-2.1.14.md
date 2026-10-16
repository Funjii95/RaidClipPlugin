# RaidClipPlugin 2.1.14 – Item-Salvage

- Unbenötigte Items gegen Perlen zerlegen: `!salvage <Nummer>`, `!salvage duplicates`, `!salvage all common`.
- Jeder Befehl zeigt zuerst eine Vorschau mit exakten Items und Perlenbeträgen. Innerhalb von 60 Sekunden denselben Befehl mit `confirm` senden, etwa `!salvage 2 confirm`.
- Sets und Legendarys erfordern eine Einzelvorschau mit Warnhinweis und bleiben beim Sammelzerlegen immer erhalten. Unterschiedliche Rolls sind keine Duplikate.
- Angelegte, gesperrte und favorisierte Items sind geschützt. Schutz setzen mit `!itemlock 2` oder `!favorite 2`, aufheben durch Zusatz `off`.
- Konfigurierbare Zerlegewerte: Common 25–50, Uncommon 75–150, Rare 200–400, Epic 600–1200, Legendary 2000–4000 Perlen. Itemdetails zeigen den tatsächlichen Wert.
- Atomare Item-/Perlentransaktion, Kontolimitprüfung, Backups und Schutz vor parallelen Doppelgutschriften. Bestehende Items und Spielstände bleiben erhalten.

Nach dem Zerlegen mit `!inv` die aktualisierten Inventarnummern anzeigen. Zerlegen ist bei aktiviertem RPG standardmäßig verfügbar; Werte und Frist unter RPG-Einstellungen → JSON → Salvage anpassbar.
