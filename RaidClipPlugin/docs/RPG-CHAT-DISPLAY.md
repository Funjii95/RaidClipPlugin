# RPG-Chatdarstellung – lokale Korrekturen

## Gefundene Ursachen

Die veröffentlichte Version 2.1.12 verwendet für alle Progressionsaktionen nur `Success` und `Balance`. Der Commandhandler konnte daraus weder Zutaten noch Level oder Buff-Aufladungen ablesen und antwortete deshalb pauschal mit „erledigt“. Die zuerst angeforderte Chatkorrektur war noch nicht veröffentlicht; der erneute Live-Test konnte sie daher noch nicht enthalten.

Die damalige Inventaransicht erzeugte immer eine Überschrift und verknüpfte anschließend die gefundenen Einträge. Bei leerem Inventar oder einer angefragten Seite hinter dem letzten Item blieb nur „Inventar 1/1:“ beziehungsweise eine andere leere Seitenüberschrift übrig. Dies allein belegt keinen Itemverlust.

Zusätzlich wurde beim Profilabruf nach dem redundanten `UserId`-Feld im Kontodatensatz gesucht, während die Perlenabfrage den eigentlichen Twitch-ID-Schlüssel im Dictionary verwendet. Fehlte dieses zusätzliche Feld in alten/importierten Daten, konnte ein vorhandenes Profil als neues leeres Profil erscheinen. Der Profilabruf verwendet jetzt ebenfalls den eindeutigen Kontoschlüssel, ohne die gespeicherte Datei umzuschreiben.

Weitere Schutzlücken: Explizite null-Listen, unvollständige Items und fehlende Namen hatten keinen eigenen Anzeigeweg. Bei fehlender gespeicherter Item-ID hätte die Modellinitialisierung sogar eine neue ID beim Deserialisieren erzeugen können, die nicht zuverlässig zur gespeicherten Instanz passte. Solche fehlenden IDs werden jetzt vor dem Deserialisieren erkannt und nicht automatisch ersetzt.

## Anzeige und Befehle

- `!inv [Seite]`, `!inventar` und `!inventory`: globale Inventarnummer, Name, Seltenheit, Attribute und Ausrüstungsstatus statt langer IDs. Legendarys erhalten 💎. Details zur sicheren Nummerierung: [RPG-NUMBERED-INVENTORY.md](RPG-NUMBERED-INVENTORY.md).
- `!item <Nummer|vollständige Item-ID> [Seite]`: eigene Iteminstanz mit vollständigen Werten, Beschreibung, Typ, Slot, interner ID und `!equip`-Syntax. Kein Zugriff auf andere Konten.
- `!gear [Seite]` und `!equipment`: ausgerüstete Items mit Slot und Werten, ohne lange IDs. Leere Ausrüstung wird ausdrücklich erklärt.
- `!perks [Seite]`: Name/ID, Level und Fortschritt, Gesamtbonus dieses Perks sowie Kosten der nächsten Stufe beziehungsweise MAX.
- `!stats`: effektive, begrenzte Bossboni, tatsächlicher Crit-Multiplikator und aktivitätsspezifische Dive-/Fishing-Luck-Werte. Kein Einfluss auf die Berechnung selbst.
- `!sushi` und `!sushi status <Seite>`: Vorrat und aktive Buffs mit Name, Wirkung, Aktivität und echten Restaufladungen. Identische numerische Rezept-IDs bleiben mit `!sushi <Rezept>` weiterhin aktivierbar.
- `!craft <Rezept>`: hergestelltes Sushi, tatsächlich verbrauchter Fisch und Menge, gezahlte Perlen, erhaltene Menge sowie Vorschau des Buffs nach separater Aktivierung. Crafting aktiviert keinen Buff.
- `!sushi <Rezept>`: tatsächlicher Buff, Werte und verbleibende gültige Aktionen statt Kontostandsmeldung.
- `!perk <ID>` und `!equip`: konkrete Erfolgsergebnisse mit Kosten/Level/Perkbonus beziehungsweise Item, Slot und Itemattributen.

Emoji-Gruppen: 🦦 allgemein/Inventar/Fehler, 🎣 Fischfang und Fischvorrat, 🍣 Sushi, 💍 Equipment und Itemdetails, ⭐ Perks, 💎 legendäre Funde, ⚔️ Bossangriffe und Siege. Bestehende eigene Spiel-Chatvorlagen bleiben maßgeblich. Boss-Crits ergänzen denselben Angriffstext statt eine zusätzliche Nachricht zu senden. Leere Vorlagen bleiben stumm.

## Pagination ohne stilles Abschneiden

Die zentrale Seiteneinteilung verwendet einen konservativen Inhaltsrahmen von 300 UTF-16-Codeeinheiten und höchstens drei Einträge pro Seite, damit Kopf und Navigation innerhalb des vorhandenen 480er-Chatlimits bleiben. Lange Namen, Beschreibungen oder mehrere Attribute werden in gekennzeichnete Fortsetzungen aufgeteilt; Surrogatpaare von Emojis werden nicht getrennt. Zu große Seitennummern werden auf die letzte vorhandene Seite begrenzt. Es werden keine automatischen Nachrichtenserien erzeugt.

Für ungewöhnlich lange **Aktionsantworten** oder andere Einzelantworten gibt es `!rpgtext [Seite]`: Die vollständige bereits erzeugte Antwort wird je Store und bestätigter Twitch-ID für 15 Minuten zwischengespeichert. Höchstens 256 Antworten werden gehalten. Folgeseiten lesen nur diesen Text; sie führen Crafting, Käufe oder Ausrüsten nicht erneut aus. Cache ist nicht kontenübergreifend und nicht persistent. Für langfristig erneut abrufbare Itemdaten wird `!item` verwendet. Es werden keine neuen Geld- oder Itemtransaktionen für Seitennavigation eingeführt.

## Fehlerfälle und Erhalt der Daten

- Wirklich leer: verständliche Meldung und Hinweis auf `!dive`.
- Fehlender Anzeigename bei sonst gültigem Item: lesbarer Rückfallname; kein Löschen und kein Überschreiben des Items.
- Fehlende/null Inventarliste oder gespeicherte fehlende Instanz-ID: Laden wird mit Protokollhinweis abgebrochen, statt leere Daten oder zufällige Ersatz-IDs zu verwenden.
- Vorhandene Liste mit ungültigen Attributen, doppelten IDs oder ungültigen Ausrüstungsreferenzen: explizite Datenfehlermeldung, nicht „Inventar leer“.
- Beschädigtes JSON/Lesefehler: eigene Meldung „RPG-Daten konnten nicht geladen werden“.
- Fehler bei der Nachrichtenerstellung: eigene Meldung mit Status-/Protokollhinweis; keine irreführende Erfolgsmeldung oder leere Überschrift.

Konten aus der Zeit vor RPG, die noch gar kein RPG-Feld besitzen, bleiben gültig und erscheinen mit einem leeren RPG-Inventar. Explizit beschädigte vorhandene RPG-Daten werden dagegen nicht als Legacy-Leerstand interpretiert. Der vorhandene Store lädt die Kontodatei gemeinsam: ein erkannter beschädigter gespeicherter Datensatz kann deshalb den Ladevorgang der Datei stoppen; er muss gezielt geprüft werden und wird nicht automatisch „repariert“ oder überschrieben.

Die zusätzlichen Aktionsdetails werden innerhalb der erfolgreichen bestehenden Transaktion ermittelt und erst nach erfolgreicher Speicherung zurückgegeben. Sie enthalten abgetrennte Attribut-Snapshots, nicht bloß nachträglich ausgelesene Kontostände. Keine neuen Abbuchungsaufrufe, Belohnungen, Lootchancen, Kostenänderungen oder Itemmigrationen. Keine produktiven Spielerdateien verändert. Kein Release/Deployment.

## Prüfung

Tests decken leeres Inventar und beide Aliasbefehle, einzelne und mehrere Items, Seiten und ungültige Seitenzahlen, sehr lange Namen/Beschreibungen, alle acht Attribute, Nachkommastellen, vollständige IDs, Ausrüstungsstatus, eigene/fremde Twitch-IDs, defekte/fehlende Daten, Legacy-Profile, Darstellungsfehler und unveränderte Dateien ab. Die bereits vorhandenen neuen Crafting-/Aktivierungs-/Perk-/Equipmenttests sichern weiterhin die tatsächlichen Zahlen und Idempotenz. Lange Crafting-Antworten werden ohne erneutes Crafting weitergeblättert.

Abschluss: **431 Tests bestanden**, keine fehlgeschlagen; Release-Build erfolgreich. Bekannte bestehende Warnungen NU1510 und WindowsBase/WebView2 bleiben unverändert. Kein Live-Twitch-Test und keine Veröffentlichung im Rahmen dieser Korrektur. Geänderte Bereiche: RpgChatCommand/Presentation/Pages, Progression-Ergebnisdetails, lesender Profilabruf und Validierung gespeicherter Inventar-IDs, CommandRegistry, Patchnotes sowie die Chat-/Inventar-Testklassen.
