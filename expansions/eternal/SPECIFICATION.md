# CODEX MASTERPROMPT – ETERNAL & DIVINE
## Funjii Otter RPG – Neue Endgame-Ausrüstung

**Repository:** https://github.com/Funjii95/RaidClipPlugin

**Ziel:** Implementiere drei erheblich verstärkte Eternal-Sets und ein göttliches Admin-Only-Set für das bestehende Funjii Otter RPG.

Plattformen:
- RaidClipPlugin – Windows-Anwendung (C#/.NET)
- Twitch Extension – Panel und Mobil
- Bestehendes Cloud Run Backend
- Firestore-Persistenz

Die vorhandene Architektur, Itemregistry, Equipment-Slots, Kampflogik, Lootsysteme und Cloud-Synchronisation wiederverwenden.

**Wichtig:** Die in diesem Prompt festgelegten Eternal-Werte und Seteffekte ersetzen die bisherigen Entwürfe vollständig. Keine alten und neuen Versionen der Setboni gleichzeitig registrieren.

---

# 1. Neue Seltenheiten

## Eternal

- Enum-/Rarity-ID: `Eternal`
- Name: Eternal
- Farbe: `#FFF2A8` (Hellgelb)
- Seltenheitsrang: oberhalb von Abyss
- Normal droppbar: Ja
- Dropgebiet: ausschließlich `primordial_throne` (Thron der Urtiefe)
- Gegnerlevel: 100
- Aufwertbar: Ja, von +0 bis +9
- Endveredelung: Ja
- Handel und Zerlegung: vorhandene Regeln zunächst beibehalten

## Divine

- Enum-/Rarity-ID: `Divine`
- Name: Göttlich
- Farbe: `#FFD700` (strahlendes Gold)
- Seltenheitsrang: oberhalb von Eternal
- Normal droppbar: Nein
- Admin Only: Ja
- Aufwertbar: Nein
- Endveredelung: Nein
- Handel: Nicht erlaubt
- Zerlegung: Nicht erlaubt
- Weitergabe: Nicht erlaubt

Divine-Gegenstände dürfen ausschließlich durch autorisierte administrative Funktionen vergeben werden.

Alle Berechtigungen serverseitig überprüfen.

---

# 2. Vorhandene Equipment-Slots

Die Sets verwenden exakt sechs Slots:

1. Helmet
2. Chest
3. Gloves
4. Boots
5. Ring
6. Amulet

Die Windows-Anwendung besitzt zusätzlich einen separaten Waffenslot. Diesen nicht entfernen oder verändern.

Eternal- und Divine-Sets benötigen selbst keinen Waffenslot.

Zwei Ringslots bleiben erhalten.

Jedes Setitem darf beim Setfortschritt maximal einmal zählen, auch wenn zwei Exemplare desselben Rings getragen werden.

**Jedes Set besitzt sechs kumulative Boni bei 1, 2, 3, 4, 5 und 6 Teilen.**

---

# 3. ETERNAL SET 1
## Sternenjäger des Urmeers

**Set-ID:** `eternal_star_hunter`

**Spielstil:** Angeln, Tauchen, Luck, Loot und starker Kampfschaden.

Dieses Set soll für Spieler ideal sein, die möglichst effizient Materialien, Fische und Schätze farmen möchten, aber trotzdem in hochstufigen Kämpfen enormen Schaden verursachen.

### Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_star_hunter_helmet | Krone des Sternenjägers | Helmet | +230 HP, +95 Rüstung, +30 % Luck |
| eternal_star_hunter_chest | Brustplatte des Urmeers | Chest | +480 HP, +170 Rüstung, +35 % Perlenfund |
| eternal_star_hunter_gloves | Griffe der Tiefenjagd | Gloves | +175 Angriff, +28 % Krit-Chance, +20 % Fischfund |
| eternal_star_hunter_boots | Stiefel der Strömungsspur | Boots | +220 HP, +28 % Ausweichen, +25 % Tauchfund |
| eternal_star_hunter_ring | Ring des Glücksstroms | Ring | +130 Angriff, +40 % Luck, +25 % Materialfund |
| eternal_star_hunter_amulet | Amulett des ewigen Fangs | Amulet | +300 HP, +35 % Boss-Schaden, +35 % Fischfund |

Dies sind feste Referenzwerte für Eternal-Gegenstände. Bereits vorhandene Itemroll-Systeme nur verwenden, wenn ausdrücklich konfiguriert. Die hier genannten Werte nicht zusätzlich durch eine pauschale Seltenheitsskalierung vervielfachen.

### Sechs Seteffekte

**1 Teil – Sternenglück**
- +35 % Luck
- +20 % Perlenfund

**2 Teile – Meister der Tiefenjagd**
- +35 % Chance auf zusätzliche normale Tauchbeute
- +30 % Chance auf einen zusätzlichen Fisch
- +30 % Materialfundchance relativ zu den bestehenden Basischancen

**3 Teile – Schatzsegen**
- +40 % normale Perlenbelohnung
- +30 % Boss-Schaden
- +25 % Angriff

**4 Teile – Jäger der Urtiefe**
- +35 % Krit-Chance
- +40 % Krit-Schaden
- +20 % Doppelangriff

**5 Teile – Unendliche Ausbeute**
- Jeder erfolgreiche Angel- oder Tauchvorgang hat 25 % Chance auf einen zusätzlichen normalen Beuteroll.
- +50 % erhaltene EXP bei berechtigten Begegnungen.
- Zusätzliche Lootwürfe dürfen keine Kampfbelohnungen erzeugen, für die kein Gegner besiegt wurde.

**6 Teile – Ewiger Sternenjäger**
- +75 % Gesamtschaden
- +50 % Luck
- +50 % normale Materialfundchance relativ zu den vorhandenen Wahrscheinlichkeiten
- +35 % Boss-Schaden
- Bei erfolgreichem Angeln oder Tauchen besteht zusätzlich eine 30-%-Chance auf einen weiteren normalen Material- beziehungsweise Fischroll.

Bonuswürfe dürfen sich nicht selbst erneut auslösen.

Eternal-, Divine- und kampfgebundene Setdrops dürfen niemals durch passive Bonuswürfe erzeugt werden.

---

# 4. ETERNAL SET 2
## Gezeitensegen der Ewigkeit

**Set-ID:** `eternal_tide_blessing`

**Spielstil:** Extreme Selbstheilung, Lebensraub, Regeneration und hoher Schaden.

Dieses Set soll kein klassisches Tankset sein.

Stattdessen soll es durch aggressive Kampfführung, Heilung und Schadensboni besonders mächtig werden.

### Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_tide_blessing_helmet | Diadem des Gezeitensegens | Helmet | +320 HP, +100 Rüstung, +35 % Heilungswirkung |
| eternal_tide_blessing_chest | Gewand der ewigen Heilflut | Chest | +620 HP, +190 Rüstung, +25 % Schadensreduktion |
| eternal_tide_blessing_gloves | Hände der sanften Strömung | Gloves | +145 Angriff, +28 % Lebensraub, +25 % Angriffstempo |
| eternal_tide_blessing_boots | Stiefel des lebenden Wassers | Boots | +300 HP, +25 % Ausweichen, +30 % Heilungswirkung |
| eternal_tide_blessing_ring | Ring des Heilbrunnens | Ring | +120 Angriff, +25 % Krit-Chance, +25 % Lebensraub |
| eternal_tide_blessing_amulet | Amulett des Ozeanherzens | Amulet | +420 HP, +40 % Heilungswirkung, +35 % Boss-Schaden |

### Sechs Seteffekte

**1 Teil – Ewige Regeneration**
- +35 % Heilungswirkung
- Regeneriert alle 5 Sekunden 5 % maximale HP.

**2 Teile – Lebendige Gezeiten**
- +35 % maximale HP
- Direkte erfolgreiche Treffer heilen den Spieler um 12 % des tatsächlich verursachten Schadens.

**3 Teile – Lebensstrom**
- Kritische Treffer regenerieren 8 % der maximalen HP.
- Cooldown: 3 Sekunden.
- +25 % Krit-Chance.

**4 Teile – Unzerbrechlicher Lebenswille**
- Fallen die HP unter 40 %, regeneriert der Spieler sofort 35 % seiner maximalen HP.
- Cooldown: 20 Sekunden.
- +35 % Gesamtschaden.

**5 Teile – Macht der Heilflut**
- Jede tatsächlich erfolgte Eigenheilung erhöht den Schaden 10 Sekunden lang um 5 %.
- Maximal fünf Stapel.
- Neue Heilungen erneuern die Dauer.
- +30 % Angriffstempo.

**6 Teile – Unsterblicher Gezeitensegen**
- +75 % Gesamtschaden
- +50 % Heilungswirkung
- Alle 8 Sekunden 20 % maximale HP regenerieren.
- Ein tödlicher Treffer heilt den Spieler stattdessen einmal pro Kampf auf 50 % seiner maximalen HP.
- 10 Sekunden lang +50 % Boss-Schaden nach Aktivierung dieses Rettungseffekts.

Keine unbegrenzte Stapelung von Heilungs- oder Schadenseffekten.

Normale Heilungen überschreiten nicht das maximale Leben.

Bei der Berechnung bestehende Heilungsobergrenzen, Trigger und Cooldowns berücksichtigen.

---

# 5. ETERNAL SET 3
## Zorn der Ewigen Urtiefe

**Set-ID:** `eternal_primordial_wrath`

**Spielstil:** Maximaler direkter Schaden, Krit, Doppelangriffe und Bossvernichtung.

Dieses Set soll das stärkste regulär erhältliche reine Schadensset des gesamten Spiels sein.

### Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_primordial_wrath_helmet | Helm des ewigen Zorns | Helmet | +210 HP, +100 Rüstung, +35 % Krit-Chance |
| eternal_primordial_wrath_chest | Panzer der Urtiefe | Chest | +440 HP, +160 Rüstung, +50 % Boss-Schaden |
| eternal_primordial_wrath_gloves | Klauen des Vernichters | Gloves | +240 Angriff, +45 % Krit-Schaden, +25 % Doppelangriff |
| eternal_primordial_wrath_boots | Schritte des Untergangs | Boots | +240 HP, +35 % Angriffstempo, +30 % Krit-Schaden |
| eternal_primordial_wrath_ring | Ring der letzten Flut | Ring | +190 Angriff, +35 % Krit-Chance, +35 % Boss-Schaden |
| eternal_primordial_wrath_amulet | Amulett des Weltenbruchs | Amulet | +220 Angriff, +55 % Krit-Schaden, +50 % Boss-Schaden |

### Sechs Seteffekte

**1 Teil – Zorn der Urtiefe**
- +50 % Angriff
- +35 % Krit-Schaden

**2 Teile – Meister der Vernichtung**
- +35 % Krit-Chance
- +75 % Krit-Schaden

**3 Teile – Bossschlächter**
- +75 % Boss-Schaden
- +25 % Angriffstempo

**4 Teile – Ewiger Doppelangriff**
- +35 % Doppelangriff
- Erfolgreiche Doppelangriffe verursachen zusätzlich 150 % des ursprünglichen Angriffsschadens.
- Der Zusatzeffekt darf keine weitere Doppelangriffskette auslösen.

**5 Teile – Unaufhaltsamer Zorn**
- Kritische Treffer erhöhen den Gesamtschaden für 10 Sekunden um 8 %.
- Maximal zehn Stapel.
- Bei maximalen Stapeln zusätzlich +30 % Angriffstempo.

**6 Teile – Weltenbrecher**
- +125 % Gesamtschaden
- +100 % Boss-Schaden
- Alle 6 Sekunden verursacht der nächste erfolgreiche direkte Treffer eine Ewige Zerreißung mit 450 % Angriffsschaden.
- Kritische Treffer ignorieren 30 % der gegnerischen Schadensreduktion.

Proc-Schaden darf keine weiteren Item-Procs rekursiv auslösen.

Alle Boni sind absichtlich extrem stark.

---

# 6. ETERNAL DROPS

Eternal-Items dürfen ausschließlich im Gebiet:

`primordial_throne`

droppen.

Feste Gegnerstufe: Level 100.

Maximales Spielerlevel bleibt 99.

**Eternal-Ausrüstung darf ausschließlich nach erfolgreich besiegten Gegnern entstehen.**

### Dropchancen

| Gegnerart | Eternal-Itemchance pro Sieg |
|---|---:|
| Normaler Gegner | 0,03 % |
| Elitegegner | 0,12 % |
| Mimic | 0,20 % |
| Besonderer Endgamegegner | 0,35 % |

Nach einem erfolgreichen Eternal-Drop:

1. Eines der drei Sets gleichgewichtet auswählen.
2. Eines der sechs Teile gleichgewichtet auswählen.
3. Eine individuelle Iteminstanz erzeugen.
4. Einmalig vorhandene zulässige Itemrolls erzeugen, sofern das Itemsystem dies vorsieht.
5. Werte und Iteminstanz dauerhaft speichern.

Keine Eternal-Gegenstände aus normalen Truhen, Angeln, passiven Tauchbelohnungen oder bloßer Gebietsauswahl vergeben.

Die genannten Dropraten werden nicht durch allgemeine Luck-, Tauchfund-, Fischfund- oder Bonusloot-Effekte erhöht, sofern nicht ausdrücklich ein separater Eternal-Dropmodifier konfiguriert wurde.

So bleibt Eternal unabhängig von den hohen Lootboni des Sternenjäger-Sets außergewöhnlich selten.

---

# 7. DIVINE SET
## Erhabenheit der Unendlichkeit

**Set-ID:** `divine_infinity`

**Seltenheit:** Divine

**Farbe:** Gold `#FFD700`

**ADMIN ONLY – NIEMALS REGULÄR DROPPBAR**

Dieses Set ist bewusst absurd übermächtig.

Es dient der Administrationsfigur und soll jede normale Spielaktivität dominieren können.

### Gegenstände

| Item-ID | Name | Slot | Werte |
|---|---|---|---|
| divine_infinity_helmet | Krone der Unendlichkeit | Helmet | +5.000 HP, +500 Rüstung, +100 % Luck |
| divine_infinity_chest | Rüstung des göttlichen Lichts | Chest | +15.000 HP, +2.000 Rüstung, +95 % Schadensreduktion |
| divine_infinity_gloves | Handschuhe der Schöpfung | Gloves | +10.000 Angriff, +100 % Krit-Chance, +100 % Doppelangriff |
| divine_infinity_boots | Stiefel der Transzendenz | Boots | +5.000 HP, +100 % Ausweichen, +200 % Angriffstempo |
| divine_infinity_ring | Ring der ewigen Welten | Ring | +5.000 Angriff, +500 % Krit-Schaden, +100 % Luck |
| divine_infinity_amulet | Amulett der Ursprungsquelle | Amulet | +5.000 HP, +500 % Fischfund, +500 % Tauchbeute, +500 % Perlenfund |

Die Werte sollen nicht zufällig gerollt werden.

Alle Divine-Items besitzen exakt definierte Werte.

Keine normalen Aufwertungen oder Endveredelungen möglich.

### Sechs göttliche Seteffekte

**1 Teil – Göttliche Macht**
- +1.000 % Angriff
- +1.000 % maximales Leben
- +1.000 % Rüstung

**2 Teile – Allwissender Schatzsucher**
- +500 % Luck
- +500 % Fischfund
- +500 % Tauchbeute
- +500 % normale Perlenbelohnungen

**3 Teile – Schöpfung ohne Grenzen**
- Garantierte normale Materialbelohnung bei berechtigten Lootereignissen.
- Dreifache normale Lootmengen.
- +500 % normale Materialfundchance, soweit die vorhandene Materiallogik zusätzliche Würfe unterstützt.

**4 Teile – Unberührbarkeit**
- 100 % Ausweichen
- Immunität gegen gewöhnlichen Kampfschaden
- Keine Todesfolge durch reguläre Gegnerangriffe

**5 Teile – Unsterblichkeit**
- Nach jedem eigenen erfolgreichen Angriff vollständige Heilung.
- Tödlicher Kampfschaden wird verhindert.
- +500 % Boss-Schaden

**6 Teile – Urteil der Götter**
- Ein göttlicher Angriff besiegt normale Gegner, Elitegegner, Mimics und Bosse garantiert.
- Dieser Spezialangriff ignoriert HP, Rüstung und Schadensreduktion.
- +10.000 % göttlicher Schaden für die Anzeige und für Effekte, die numerischen Schaden benötigen.
- Keine zufällige Erfolgschance.

## Göttlicher Schaden

Implementiere den Sonderangriff als explizit berechtigten Divine-Effekt.

Keine normalen Schadensformeln durch extrem große Zahlen überlaufen lassen.

Falls ein Gegner durch das Göttliche Urteil besiegt wird, muss der normale autoritative Kampfabschluss erfolgen.

### WICHTIG: NORMALE BELOHNUNGEN BLEIBEN AKTIV

Göttliche Sofortsiege dürfen Belohnungen nicht unterdrücken.

Der Spieler erhält bei berechtigtem Sieg weiterhin:

- Normale EXP
- Normale Perlenbelohnungen
- Materialdrops
- Ausrüstungsdrops
- Setdrops
- Eternal-Drops, sofern die normale Gebiets- und Quellenprüfung erfüllt ist
- Mimic-Belohnungen
- Sammelgegenstände
- Eventbelohnungen

Keine künstliche Loot-Sperre für Divine-Spieler implementieren.

Alle regulären Dropbedingungen, Gebietstabellen und bestehenden Belohnungsregeln bleiben gültig.

Eternal-Items dürfen dadurch weiterhin nur von besiegten Level-100-Gegnern im Thron der Urtiefe stammen.

---

# 8. ADMIN-VERGABE UND SICHERHEIT

Divine-Items dürfen niemals im normalen Lootpool enthalten sein.

Implementiere folgende Funktionen in der vorhandenen Windows-Adminoberfläche:

- Göttliches Set vollständig vergeben
- Einzelnes Divine-Item vergeben
- Divine-Items entfernen
- Divine-Items eines Spielers anzeigen

Vergabe nur für autorisierte Adminidentitäten.

Die Berechtigung muss serverseitig beziehungsweise in der autoritativen Pluginlogik geprüft werden.

Keine Freigabe allein aufgrund eines Frontend-Flags oder lokal manipulierbaren Benutzernamens.

Direkte API-Aufrufe dürfen diese Prüfung nicht umgehen.

Divine-Items dürfen nicht:

- gehandelt werden,
- zerlegt werden,
- an andere Spieler übertragen werden,
- regulär droppen,
- aufgewertet werden,
- endveredelt werden.

Bereits rechtmäßig ausgerüstete Divine-Items dürfen nach Neustart und Cloud-Synchronisation erhalten bleiben.

Eine erteilte Adminberechtigung darf nicht automatisch allen Testspielern oder Zuschauern Zugriff geben.

---

# 9. ITEMSET-SYSTEM

Das vorhandene Setbonus-System erweitern.

Für Eternal und Divine gelten sechs kumulative Stufen:

`1 / 2 / 3 / 4 / 5 / 6`

Die bestehenden älteren Sets behalten ihre jeweiligen 2/3/4- oder 2/4/6-Boni.

Keine vorhandenen Setdefinitionen überschreiben, außer den ausdrücklich hier neu definierten Eternal-Sets.

Beim Ausrüsten und Ablegen:

- Setfortschritt neu berechnen.
- Buffs korrekt aktivieren und deaktivieren.
- Doppelte Iteminstanzen desselben Setstücks nicht mehrfach zählen.
- Zwei unterschiedliche Ringslots weiterhin unterstützen.
- Beim Itemwechsel keine alten Buffs aktiv lassen.
- Prozente, Heilung und Buffstapel konsistent verrechnen.

Bei hohen Eternal-Werten sicherstellen, dass bestehende Prozentlimits die neuen Boni nicht unabsichtlich wirkungslos machen.

Für Divine erforderliche Immunitäten und garantierte Effekte als ausdrücklich gekennzeichnete Sondermechaniken implementieren, anstatt globale Spielregeln für alle Items zu verändern.

---

# 10. UPGRADE-SYSTEM

Eternal-Gegenstände dürfen das vorhandene garantierte Aufwertungssystem +0 bis +9 verwenden.

Da Eternal oberhalb von Abyss liegt, erhält Eternal eine eigene Materialkostengruppe.

**Vorgeschlagene zusätzliche Eternal-Kosten ab +7:**

| Zielstufe | Zusätzliche Materialien |
|---|---|
| +7 | 2× Abyss-Scherbe + 1× Kern der Ewigen Tiefe |
| +8 | 3× Abyss-Scherbe + 2× Kern der Ewigen Tiefe |
| +9 | 5× Abyss-Scherbe + 3× Kern der Ewigen Tiefe |

Diese Kosten kommen zu den bestehenden normalen Perlen, Verstärkungskernen und farbigen Perlen hinzu.

Die vorhandenen Upgrade-Boni bleiben:

+1: 3 %
+2: 6 %
+3: 9 %
+4: 12 %
+5: 17 %
+6: 22 %
+7: 28 %
+8: 35 %
+9: 45 %

Eternal-Endveredelung mit dem bisherigen System ermöglichen, solange keine gesonderten Eternal-Veredelungskosten definiert sind.

**Jede Aufwertung ist garantiert erfolgreich. Keine Fehlschläge, keine RNG-Erfolgsmechanik und keine Itemzerstörung.**

Divine-Gegenstände müssen sämtliche Upgradeoperationen ablehnen, ohne Materialien oder normale Perlen zu verbrauchen.

---

# 11. WINDOWS-ANWENDUNG

Integriere die neuen Seltenheiten und Sets vollständig in das vorhandene beziehungsweise geplante Deep-Sea-Codex-Interface.

Anforderungen:

- Eternal in Hellgelb darstellen.
- Divine in Gold mit besonders aufwendiger Verzierung darstellen.
- Alle 18 Eternal-Items registrieren.
- Alle sechs Divine-Items registrieren.
- Neue 1/2/3/4/5/6-Setboni anzeigen.
- Eternal-Aufwertung ermöglichen.
- Divine-Aufwertung deaktivieren und „Nicht aufwertbar“ anzeigen.
- Itemtooltips mit allen Werten und Effekten.
- Seltenheitsfilter.
- Setfortschritt.
- Charakterstatistiken korrekt aktualisieren.
- Vorhandenen Waffenslot unverändert beibehalten.

Adminfunktionen nur in der Windows-Verwaltungsoberfläche und mit vorhandenen Berechtigungen anzeigen.

---

# 12. TWITCH EXTENSION

Die neuen Sets müssen auch im Twitch-Panel und in der Mobilansicht korrekt erscheinen.

Funktionen:

- Eternal- und Divine-Gegenstände anzeigen.
- Korrekte Seltenheitsfarben.
- Alle Setboni darstellen.
- Richtige Charakterwerte anzeigen.
- Itemwechsel und Buffstatus synchronisieren.
- Eternal-Upgrades unterstützen.
- Divine als nicht aufwertbar kennzeichnen.

Keine Admin-Vergabefunktion in der normalen Zuschaueroberfläche.

---

# 13. GRAFIKEN

Verwende die zuvor erstellten drei Eternal-Itemtafeln und die separate Divine-Itemtafel als visuelle Referenzen.

Pro Item eine eigene Grafik registrieren.

Empfohlen:

- 256×256 oder 512×512 Pixel
- Quadratische Item-Icons
- Klare Unterscheidbarkeit auf dunklem Hintergrund
- Hellgelbe Eternal-Rahmen
- Goldene Divine-Rahmen

Die Itemtafeln dürfen nicht direkt als einzelne Item-Icons missbraucht werden.

Falls Einzel-Icons fehlen, dokumentiere diese und verwende vorübergehend Platzhalter.

---

# 14. AUTOMATISIERTE TESTS

Teste mindestens:

1. Registrieren aller 18 Eternal-Items.
2. Registrieren aller sechs Divine-Items.
3. Eindeutigkeit sämtlicher IDs.
4. Gültige Equipment-Slots.
5. Eternal-Drops ausschließlich aus erlaubten Level-100-Kämpfen.
6. Divine-Dropchance immer 0 %.
7. Serverprüfung für Adminvergaben.
8. Verbotene Divine-Weitergabe.
9. Divine nicht aufwertbar.
10. Eternal-Aufwertung +0 bis +9.
11. Alle sechs Eternal-Setboni pro Set.
12. Alle sechs Divine-Setboni.
13. Korrekte Berechnung von Krit, Doppelangriff und Luck.
14. Kein rekursiver Proc-Schaden.
15. Korrekte Heilung und Bufflaufzeiten.
16. Keine doppelt gezählten Ringe.
17. Göttliche Immunitäten.
18. Göttliche garantierte Sofortsiege.
19. Normale EXP nach Divine-Sieg.
20. Normale Ausrüstungs- und Materialdrops nach Divine-Sieg.
21. Eternal-Loot weiterhin an Level-100-Kampfquellen gebunden.
22. Persistenz sämtlicher Iteminstanzen.
23. Windows- und Twitch-Synchronisation.
24. Rückwärtskompatibilität alter Spielstände.
25. Keine Duplizierung durch wiederholte Cloud-Events.

---

# 15. ABSCHLUSS

Nach der Implementierung dokumentieren:

- Alle neuen Dateien
- Alle registrierten Itemdefinitionen
- Die neuen Rarity-Stufen
- Alle Seteffekte
- Die Eternal-Droplogik
- Die Divine-Adminvergabe
- Die Upgrade-Integration
- Änderungen im Windows-Client
- Änderungen in der Twitch Extension
- Tatsächlich ausgeführte Tests
- Noch fehlende Grafiken oder Backend-Funktionen

Keine Testresultate erfinden.

Kein automatisches Deployment und keine produktive Veröffentlichung ohne gesonderten Auftrag.

## VERBINDLICHE PRIORITÄTEN

1. Bestehende Systeme erhalten.
2. Eternal deutlich stärker als Abyss implementieren.
3. Die drei Eternal-Spielstile beibehalten.
4. Divine als kompromisslos übermächtiges Admin-Set implementieren.
5. Göttliche Kämpfe dürfen normale EXP und Lootbelohnungen nicht verhindern.
6. Kein RNG bei Aufwertungen.
7. Sichere Adminberechtigungen.
8. Konsistente Implementierung in Windows, Backend und Twitch.
