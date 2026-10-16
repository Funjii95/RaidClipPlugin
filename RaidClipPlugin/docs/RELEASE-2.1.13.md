# RaidClipPlugin 2.1.13 – Item-System 2.0 und RPG-Komfort

- Kurzes Inventar mit `!inv`, nummerierten Items und Slot-Symbolen. Automatische Slotwahl mit `!equip <Nummer>`, Ringwahl über `ring1`/`ring2`, Ausrüstung über `!gear`, Ablegen mit `!unequip <Slot>`. Veraltete Inventarnummern werden sicher abgelehnt.
- Vollständige deutsche Itemdetails mit `!item <Nummer> [Seite]`: Attribute, Spezialeffekte, Chancen, Stärken und Setstatus. Lange Antworten bleiben ohne erneute Aktion über Folgeseiten abrufbar.
- Dynamische Attribute für künftige Funde, Doppel-/Dreifachangriffe, Extra-Loot und endliche Sushi-Erhaltung. Gemeinsame Caps; Perlenbonus nur auf tatsächliche Nettogewinne, nicht auf Einsatzrückzahlungen.
- Zwei vierteilige Sets: Wächter der Tiefsee und Schatzjäger des Ozeans, dazu fünf besondere Legendarys. `!sets` zeigt Fortschritt und aktive Boni. Eigene Boss-Lootregeln; Legendary-Bossloot standardmäßig gesperrt.
- Konkrete Crafting-, Sushi-, Perk- und Ausrüstungstexte statt generischer Kontostandsmeldungen. `!buyperk` als Alias für `!perk`; einheitliche Registrierung und verständliche Syntaxhinweise.
- Bestehende Inventare, Item-IDs, Perlen, Fische, Perks und Sushi bleiben erhalten. Zusätzliche Sicherung vor erster RPG-Schreiboperation.

## Nach dem Update

Unter **Punkte & Minigames → RPG → Einstellungen** bei Bedarf **Dynamische Items vorbereiten** und **Sets & Legenden vorbereiten** wählen, danach **RPG-Einstellungen speichern**. Eigene vorhandene Definitionen werden nicht überschrieben; bereits gefundene Items nicht neu gewürfelt.

Das Update-ZIP und `update.json` sind für den eingebauten Auto-Updater bestimmt. Alternativ den Windows-Installer herunterladen.
