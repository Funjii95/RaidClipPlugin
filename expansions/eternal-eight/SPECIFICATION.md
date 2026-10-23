# CODEX MASTERPROMPT – ETERNAL 8/8 SETS
## Funjii Otter RPG – Drei ultimative Endgame-Sets

**Repository:** https://github.com/Funjii95/RaidClipPlugin

**Projekt:** Funjii Otter RPG / RaidClipPlugin

**Zielplattformen:**
- Windows-Anwendung (C#/.NET)
- Twitch Extension (Panel und Mobil)
- Bestehendes Backend auf Cloud Run
- Firestore-Spielstandspeicherung

## 1. Entwicklungsauftrag

Implementiere drei zusätzliche Eternal-Ausrüstungssets mit jeweils acht Gegenständen und acht kumulativen Seteffekten.

Insgesamt entstehen:

- 3 neue Eternal-Sets
- 24 neue Ausrüstungsgegenstände
- 24 individuelle Item-Icons
- 24 Seteffekte (acht pro Set)
- Neue Endgame-Dropzuordnungen
- Vollständige Integration in Equipment, Inventar, Setverwaltung und Aufwertung

**Wichtig:** Es handelt sich um neue Sets, nicht um Ersatz für die bisherigen Eternal-Sets.

Bereits vorhanden beziehungsweise zuvor spezifiziert:

- Sternenjäger des Urmeers
- Gezeitensegen der Ewigkeit
- Zorn der Ewigen Urtiefe

Diese drei 6er-Sets dürfen nicht verändert werden.

Das Divine-Adminset ebenfalls nicht verändern.

Vor Beginn das Repository analysieren und vorhandene Item-, Rarity-, Set-, Loot- und Kampfsysteme wiederverwenden.

---

# 2. Seltenheit Eternal

Alle 24 Items besitzen:

- Rarity: `Eternal`
- Anzeigename: Eternal
- Farbe: `#FFF2A8`
- Seltenheitsrang: oberhalb von Abyss, unterhalb von Divine
- Maximale Aufwertung: +9
- Garantierter Upgrade-Erfolg
- Vorhandene Endveredelung unterstützen
- Regulärer Fundort: ausschließlich Thron der Urtiefe

Keine neuen Seltenheitsklassen einführen.

Die bereits vorhandene Eternal-Definition wiederverwenden.

---

# 3. Ausrüstungsslots

Ein vollständiges 8er-Set besteht exakt aus:

1. Helmet
2. Chest
3. Gloves
4. Boots
5. Weapon
6. Ring
7. Ring
8. Amulet

Die beiden Ringe müssen unterschiedliche Item-IDs besitzen.

Beide Ringe können in den vorhandenen Ringslots ausgerüstet werden.

Ein Gegenstand darf beim Setfortschritt nur einmal zählen.

Es dürfen keine zusätzlichen Ausrüstungsslots eingeführt werden.

**Wichtig:** Der Waffenslot ist Bestandteil des vollständigen 8er-Sets.

## Setbonus-Regel

Jedes Set aktiviert kumulative Effekte bei:

`1/8, 2/8, 3/8, 4/8, 5/8, 6/8, 7/8, 8/8`

Bei acht ausgerüsteten Teilen müssen alle acht Effekte aktiv sein.

Beim Ablegen eines Teils werden nicht mehr erfüllte Schwellen sofort deaktiviert.

Die vorhandenen 6er- und 4er-Sets behalten ihre eigenen Setbonusschwellen.

---

# 4. SET 1 – STERNENSCHMIED DES URKOSMOS

**Set-ID:** `eternal_cosmic_forge`

**Spielstil:** Kritische Treffer, Sternenmagie, Doppelangriffe und extremer Boss-Schaden.

## Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_cosmic_forge_helmet | Krone des Sternenschmieds | Helmet | +280 HP, +38 % Krit-Chance |
| eternal_cosmic_forge_chest | Panzer des Urkosmos | Chest | +560 HP, +55 % Boss-Schaden |
| eternal_cosmic_forge_gloves | Fäuste der Sternenschöpfung | Gloves | +260 Angriff, +65 % Krit-Schaden |
| eternal_cosmic_forge_boots | Schritte der Galaxien | Boots | +280 HP, +40 % Angriffstempo |
| eternal_cosmic_forge_weapon | Sternenbrecher der Unendlichkeit | Weapon | +1.650 Angriff, +50 % Krit-Schaden |
| eternal_cosmic_forge_ring_1 | Ring der aufgehenden Sterne | Ring | +180 Angriff, +35 % Krit-Chance |
| eternal_cosmic_forge_ring_2 | Ring der fallenden Sterne | Ring | +190 Angriff, +45 % Krit-Schaden |
| eternal_cosmic_forge_amulet | Herz des Urkosmos | Amulet | +350 HP, +65 % Boss-Schaden |

Die Werte sind verbindliche Referenzwerte und dürfen nicht zusätzlich durch einen pauschalen Eternal-Stärkemultiplikator vervielfacht werden.

## Acht Seteffekte

**1/8 – Sternenerwachen**

+35 % Angriff.

**2/8 – Kosmische Präzision**

+25 % Krit-Chance und +50 % Krit-Schaden.

**3/8 – Sternenfunken**

Kritische Treffer erzeugen eine Sternenexplosion mit 180 % Angriffsschaden.

Cooldown: 5 Sekunden.

**4/8 – Himmlische Jagd**

+50 % Boss-Schaden und +20 % Angriffstempo.

**5/8 – Galaktische Resonanz**

Jeder erfolgreiche direkte Angriff gewährt +4 % Gesamtschaden für acht Sekunden.

Maximal zehn Stapel.

Neue Stapel erneuern die Dauer.

**6/8 – Zwillingssterne**

+35 % Doppelangriff.

Erfolgreiche Doppelangriffe verursachen einen zusätzlichen Treffer mit 150 % Angriffsschaden.

**7/8 – Kosmische Überladung**

Bei zehn Resonanzstapeln verursacht der nächste erfolgreiche direkte Treffer eine Supernova mit 500 % Angriffsschaden.

Cooldown: zehn Sekunden.

Der Effekt verbraucht die Resonanzstapel und verhindert eine unmittelbare erneute Auslösung derselben Supernova.

**8/8 – Schöpfung des Urkosmos**

+150 % Gesamtschaden.

+100 % Boss-Schaden.

Alle acht Sekunden löst der nächste erfolgreiche direkte Treffer einen Meteorschlag mit 800 % Angriffsschaden aus.

### Besondere technische Regeln

- Explosionsschaden darf nicht rekursiv neue Explosionen auslösen.
- Meteorschläge lösen keine weiteren Item-Procs aus.
- Doppelangriffe dürfen keine unbegrenzten Angriffsketten auslösen.
- Buffstapel müssen korrekt ablaufen.
- Schaden wird über das bestehende autoritative Kampfsystem berechnet.

---

# 5. SET 2 – EWIGER SEELENVERSCHLINGER

**Set-ID:** `eternal_soul_devourer`

**Spielstil:** Aggressiver Lebensraub, Selbstheilung und mit jedem Angriff zunehmender Schaden.

Kein klassisches Tankset.

## Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_soul_devourer_helmet | Maske des Seelenverschlingers | Helmet | +320 HP, +35 % Krit-Chance |
| eternal_soul_devourer_chest | Gewand der ewigen Seelen | Chest | +650 HP, +30 % Schadensreduktion |
| eternal_soul_devourer_gloves | Klauen des Seelenraubs | Gloves | +240 Angriff, +30 % Lebensraub |
| eternal_soul_devourer_boots | Schritte der Seelenlosen | Boots | +350 HP, +30 % Ausweichen |
| eternal_soul_devourer_weapon | Seelenernter der Urtiefe | Weapon | +1.550 Angriff, +35 % Lebensraub |
| eternal_soul_devourer_ring_1 | Siegel des Lebenshungers | Ring | +200 Angriff, +35 % Angriffstempo |
| eternal_soul_devourer_ring_2 | Siegel des Todeshungers | Ring | +220 Angriff, +50 % Krit-Schaden |
| eternal_soul_devourer_amulet | Herz der verschlungenen Seelen | Amulet | +450 HP, +45 % Heilungswirkung |

## Acht Seteffekte

**1/8 – Seelenhunger**

+20 Prozentpunkte Lebensraub aus tatsächlich verursachtem direktem Schaden.

**2/8 – Unstillbarer Durst**

+30 % Angriff und +30 % Heilungswirkung.

**3/8 – Seelenaufnahme**

Jeder fünfte erfolgreiche direkte Angriff heilt zusätzlich 15 % der maximalen HP.

**4/8 – Seelenrausch**

Eine tatsächlich erfolgte Eigenheilung gewährt acht Sekunden lang +30 % Gesamtschaden.

Nicht stapelbar.

**5/8 – Ernte der Verlorenen**

Jeder kritische direkte Treffer erzeugt einen Seelenstapel.

Ein Stapel gewährt +6 % Gesamtschaden.

Maximal acht Stapel.

Dauer: zehn Sekunden.

**6/8 – Blut der Unsterblichen**

Beim erstmaligen Unterschreiten von 30 % HP erfolgt eine sofortige Heilung um 50 % der maximalen HP.

Einmal pro Bosskampf.

**7/8 – Essenzraub**

Alle sieben Sekunden erzeugt der nächste erfolgreiche direkte Angriff einen Seelenschlag mit 400 % Angriffsschaden.

Der Effekt heilt 15 % des tatsächlich durch den Seelenschlag verursachten Schadens.

**8/8 – Der Ewige Verschlinger**

+120 % Gesamtschaden.

+60 % Boss-Schaden.

Während acht Seelenstapel aktiv sind, erhöht sich der direkte Lebensraub zusätzlich um 20 Prozentpunkte.

### Besondere technische Regeln

- Lebensraub basiert ausschließlich auf tatsächlich verursachtem Schaden.
- Kein Lebensraub durch verhinderte oder vollständig absorbierte Treffer.
- Buffs und Lebensraub dürfen keine rekursiven Triggerketten bilden.
- Heilungen übersteigen nicht die maximalen HP.
- Rettungseffekte werden nur einmal pro persönlichem Bosskampf aktiviert.
- Bestehende Selfheal- und Sushi-Systeme bleiben kompatibel.

---

# 6. SET 3 – SCHATZHERRSCHER DER ZEHN MEERE

**Set-ID:** `eternal_ten_seas_treasure`

**Spielstil:** Luck, Angeln, Tauchen, Materialfunde, Perlen und gleichzeitig erheblicher Kampfschaden.

## Gegenstände

| Item-ID | Name | Slot | Basiswerte |
|---|---|---|---|
| eternal_ten_seas_treasure_helmet | Krone der Zehn Meere | Helmet | +300 HP, +65 % Luck |
| eternal_ten_seas_treasure_chest | Gewand des Schatzherrschers | Chest | +550 HP, +60 % Perlenfund |
| eternal_ten_seas_treasure_gloves | Greifer des Ewigen Fischers | Gloves | +210 Angriff, +55 % Fischfund |
| eternal_ten_seas_treasure_boots | Pfade der Meeresschätze | Boots | +300 HP, +50 % Tauchfund |
| eternal_ten_seas_treasure_weapon | Dreizack des Schatzkönigs | Weapon | +1.450 Angriff, +45 % Boss-Schaden |
| eternal_ten_seas_treasure_ring_1 | Ring der Glücksflut | Ring | +160 Angriff, +60 % Luck |
| eternal_ten_seas_treasure_ring_2 | Ring des Perlenregens | Ring | +170 Angriff, +70 % Perlenfund |
| eternal_ten_seas_treasure_amulet | Ursprung der Zehn Meere | Amulet | +400 HP, +50 % Materialfund |

## Acht Seteffekte

**1/8 – Glück der Tiefe**

+50 % Luck.

**2/8 – Meisterfischer**

+50 % Chance auf einen zusätzlichen normalen Fischfang.

**3/8 – Unerschöpfliche Schätze**

+65 % Perlenfund.

+40 % Chance auf zusätzliche normale Tauchbeute.

**4/8 – Jäger der Meere**

+50 % Angriff.

+40 % Boss-Schaden.

**5/8 – Materialmagnet**

+60 % relative Chance auf normale Materialfunde.

Bei einem erfolgreichen Materialwurf zusätzlich 25 % Chance auf eine weitere Einheit desselben Materials.

**6/8 – Jagdinstinkt**

+40 % Krit-Chance.

+50 % Krit-Schaden.

**7/8 – Segen der Zehn Meere**

+75 % normale EXP-Belohnungen.

+50 % normale Perlenbelohnungen.

Nur tatsächlich berechtigte Belohnungen verstärken.

**8/8 – Herrscher aller Schätze**

+100 % Gesamtschaden.

+100 % Luck.

+100 % relative Chance auf normale Materialfunde.

35 % Chance auf einen zusätzlichen gewöhnlichen Fisch- oder Tauchbeutewurf.

### Wichtige Lootbeschränkungen

Zusätzliche normale Beutewürfe dürfen keine kampfgebundenen Items erzeugen.

Insbesondere ausgeschlossen:

- Eternal-Items
- Abyss-Setitems
- Wahre Setitems ohne gültigen Kampf
- Divine-Items
- Gegnerbezogene Bossbelohnungen ohne Kampf

Normale Material- und Fischboni dürfen die besonderen Eternal-Dropraten nicht erhöhen.

Luck erhöht Eternal-Drops nur dann, wenn dafür bereits ein ausdrücklich freigegebener Mechanismus existiert.

Bonusloot darf sich niemals selbst erneut auslösen.

---

# 7. ETERNAL-8ER-SET-DROPS

Die neuen Sets droppen ausschließlich im Gebiet:

`primordial_throne`

Name: Thron der Urtiefe.

Gegnerlevel: 100.

Keine Levelanforderung für den Gebietseintritt.

Ausrüstungsdrops nur nach erfolgreich besiegten Gegnern.

## Dropchancen

| Quelle | Chance auf ein neues Eternal-8er-Setitem |
|---|---:|
| Normaler Level-100-Gegner | 0,01 % |
| Elitegegner | 0,04 % |
| Erfolgreich besiegte Mimic | 0,08 % |
| Eonath im Boss-Event | 0,10 % |

Diese Wahrscheinlichkeiten gelten für die gesamte Gruppe der neuen Eternal-8er-Setitems, nicht für jedes einzelne Item.

Beim erfolgreichen Drop:

1. Eines der drei neuen Sets auswählen.
2. Eines der acht Teile auswählen.
3. Eine Iteminstanz erzeugen.
4. Werte nach vorhandenen Itemregeln speichern.
5. Belohnung dauerhaft und idempotent vergeben.

### Boss-Event

Die bestehende neue Boss-Event-Architektur berücksichtigen:

- Globaler gemeinsamer HP-Prozentwert
- Individuelle persönliche Bossauswahl
- Individueller Boss-Schaden
- Individueller Lootpool

Eternal-8er-Sets dürfen aus dem Boss-Event nur Spielern droppen, die:

- Eonath als persönlichen Boss ausgewählt haben,
- am Event gültig teilgenommen haben,
- die bestehenden Überlebensbedingungen erfüllen,
- beim globalen Sieg belohnungsberechtigt sind.

Ein Spieler, der Korallus oder einen anderen niedrigeren Boss ausgewählt hat, bekommt keine Eternal-Ausrüstung, auch wenn er zum gemeinsamen Eventabschluss beigetragen hat.

### Dropgewichtung

Die neuen 8er-Setdrops sind ein separater seltener Bonuswurf.

Die bisherigen Eternal-6er-Setdrops unverändert erhalten.

Es dürfen keine doppelten Itemdefinitionen oder unbeabsichtigten Rarity-Multiplikatoren entstehen.

Alle Dropwerte im Windows-Adminpanel konfigurierbar machen.

---

# 8. AUTOMATISCHE BILDERSTELLUNG

**WICHTIGER AUFTRAG: Erstelle für alle 24 neuen Gegenstände eigene Bilder.**

Prüfe zunächst, welche Bildgenerierungswerkzeuge oder vorhandenen Asset-Pipelines in der Entwicklungsumgebung verfügbar sind.

Wenn ein Bildgenerator verfügbar ist, verwende ihn zur tatsächlichen Erstellung der Grafiken.

Wenn keine Bildgenerierung verfügbar ist:

- Keine angeblich generierten Grafiken vortäuschen.
- Für jedes Item einen eigenständigen Bildgenerierungsprompt speichern.
- Vorübergehend passende Platzhalter erstellen.
- Die fehlenden Bilder im Abschlussbericht nennen.

## 8.1 Grafikstil

Alle Bilder sollen zum bestehenden Funjii Otter RPG passen.

Stil:

- Hochwertiges Fantasy-RPG-Itemicon
- Unterwasser-Fantasy
- Leuchtende magische Effekte
- Filigrane goldene Verzierungen
- Helle Eternal-Farbakzente
- Dreidimensionale, detaillierte Gegenstände
- Gute Erkennbarkeit in kleinen Inventarslots
- Keine Texte innerhalb der Itembilder
- Keine Charaktere oder Otter in den Itembildern

Jedes Icon zeigt ausschließlich den jeweiligen Gegenstand.

## 8.2 Auflösung

Bevorzugt:

- 512×512 Pixel
- Quadratisch
- Transparenter Hintergrund, sofern der Generator dies unterstützt
- PNG

Alternativ dunkler neutraler Hintergrund, wenn Transparenz nicht möglich ist.

## 8.3 Individuelle Setdesigns

### Sternenschmied des Urkosmos

Motiv:
- Weißgoldene Rüstung
- Kosmische Sterne
- Leuchtende Sternenkristalle
- Hellgelbe Lichtadern
- Türkisblaue Sternenenergie

Die acht Gegenstände müssen sichtbar zusammengehören.

Waffe: gewaltige sternengeschmiedete magische Klinge.

### Ewiger Seelenverschlinger

Motiv:
- Antikes Gold
- Dunkle Türkisenergie
- Geisterflammen
- Seelenkristalle
- Geheimnisvolle Schatten

Waffe: große, magische Seelensense.

Die Rüstung soll gefährlich und mystisch aussehen.

### Schatzherrscher der Zehn Meere

Motiv:
- Prunkvolle goldene Schatzsucherausrüstung
- Perlen
- Türkise Edelsteine
- Muscheln
- Tiefseeschätze
- Feine Korallenverzierungen

Waffe: goldener Dreizack des Schatzkönigs.

Die Ausrüstung soll königlich und besonders wertvoll wirken.

## 8.4 Benennung der Bilddateien

Verwende die jeweilige Item-ID als Dateinamen.

Beispiel:

`eternal_cosmic_forge_helmet.png`

Alle 24 Bilddateien müssen eindeutig benannt werden.

Die Bilder in der vorhandenen Asset-Struktur für Windows und Twitch Extension registrieren.

Keine vollständigen Itemübersichten als einzelne Itemicons verwenden.

## 8.5 Zusätzlich drei Setübersichten

Erstelle nach Möglichkeit drei gesonderte Übersichtsgrafiken.

Jede Übersicht zeigt:

- Exakt acht Items
- Einheitlichen Setstil
- Setnamen
- Passende Eternal-Farbgebung

Dateinamen:

- `eternal_cosmic_forge_overview.png`
- `eternal_soul_devourer_overview.png`
- `eternal_ten_seas_treasure_overview.png`

Diese Übersichten sind ausschließlich für die Setgalerie gedacht.

Die Einzel-Icons bleiben eigenständige Dateien.

---

# 9. INTEGRATION INS EQUIPMENT-SYSTEM

Alle 24 neuen Items müssen:

- Im Inventar erscheinen.
- In den richtigen Slots ausrüstbar sein.
- Korrekte Werte besitzen.
- Eigene Tooltips besitzen.
- Mit dem bestehenden Waffenslot funktionieren.
- Mit den zwei Ringslots funktionieren.
- Beim Ausrüsten die Charakterwerte aktualisieren.
- Beim Ablegen die Charakterwerte zurücksetzen.
- Den Setfortschritt korrekt beeinflussen.

Ein Gegenstand darf den Setfortschritt nur einmal erhöhen.

Die beiden unterschiedlichen Setringe zählen jeweils als eigenständiger Setgegenstand.

---

# 10. INTEGRATION IN WINDOWS

In der Windows-Anwendung:

- Alle 24 Items darstellen.
- Hellgelbe Eternal-Rahmen verwenden.
- Drei neue 8er-Sets im Itemset-Reiter anzeigen.
- 1/8 bis 8/8 Seteffekte auflisten.
- Aktive Effekte hervorheben.
- Fehlende Setteile anzeigen.
- Setgegenstände im Inventar auffindbar machen.
- Waffen im vorhandenen Waffenslot ausrüsten.
- Upgrade-Stufen anzeigen.
- Tooltip-Vergleich unterstützen.

Den bestehenden Deep-Sea-Codex-Stil verwenden.

---

# 11. INTEGRATION IN DIE TWITCH EXTENSION

Die neuen Eternal-Sets ebenfalls anzeigen.

Anforderungen:

- Alle 24 Gegenstände sichtbar
- Neue Icons eingebunden
- Setnamen korrekt
- Acht Setboni pro Set
- Korrekte Itemwerte
- Waffenslot berücksichtigt
- Beide Ringslots berücksichtigt
- Keine doppelten Boni
- Cloud-Synchronisation
- Mobile Darstellung

Die bestehende Extension-Architektur wiederverwenden.

---

# 12. UPGRADE-SYSTEM

Die neuen Items sind reguläre Eternal-Ausrüstung.

Deshalb:

- +0 bis +9 möglich
- 100 % Upgrade-Erfolg
- Keine Fehlschläge
- Keine Itemzerstörung
- Vorhandene Eternal-Materialkosten verwenden
- Vorhandene Endveredelung unterstützen
- Ursprüngliche Werte dauerhaft speichern
- Keine rekursive Statmultiplikation

Die Setboni dürfen durch das normale Aufwertungssystem nicht zusätzlich skaliert werden.

---

# 13. ADMINPANEL

Erweitere das bestehende Windows-Adminpanel.

Unter Item- und Lootverwaltung:

- Drei neue Eternal-Sets anzeigen.
- 24 Gegenstände registrieren.
- Itemwerte anzeigen.
- Dropchancen bearbeiten.
- Lootquellen anzeigen.
- Testdrops auslösen.
- Einzelne Items autorisiert vergeben.
- Set vollständig zu Testzwecken vergeben.
- Item-Icons prüfen.
- Setboni überprüfen.

Neue Einstellungen mit vorhandenen Konfigurationsservices speichern.

Debugaktionen müssen serverseitig autorisiert und protokolliert werden.

---

# 14. AUTOMATISIERTE TESTS

Teste mindestens:

1. Alle drei neuen Setdefinitionen.
2. Registrierung sämtlicher 24 Items.
3. Eindeutigkeit der Item-IDs.
4. Richtige Equipment-Slots.
5. Beide Setringe zählen separat.
6. Doppelte identische Ringe zählen nicht mehrfach.
7. Aktivierung sämtlicher 24 Seteffekte.
8. Kumulatives Verhalten von 1/8 bis 8/8.
9. Deaktivierung beim Ablegen.
10. Angriffs- und Kritberechnungen.
11. Doppelangriffe und Proc-Schutz.
12. Lebensraub und Eigenheilung.
13. Stapelgrenzen und Cooldowns.
14. Material- und Fischbonuswürfe.
15. Kein passiver Eternal-Drop.
16. Eternal-Drops ausschließlich aus gültigen Level-100-Kämpfen.
17. Eonath-Loot nur für berechtigte Teilnehmer.
18. Keine Divine-Drops.
19. Keine Veränderung alter Eternal-6er-Sets.
20. Keine Veränderung des Divine-Adminsets.
21. Aufwertung +0 bis +9.
22. Garantierter Upgrade-Erfolg.
23. Firestore-Persistenz.
24. Windows-Integration.
25. Twitch-Integration.
26. Bilddateien eindeutig zugeordnet.
27. Rückwärtskompatibilität bestehender Spielstände.

---

# 15. ARBEITSREIHENFOLGE

Arbeite in dieser Reihenfolge:

1. Repository und vorhandene Itemarchitektur untersuchen.
2. Vorhandene Eternal-Sets identifizieren.
3. Drei neue Setdefinitionen erstellen.
4. 24 Itemdefinitionen registrieren.
5. Setbonussystem für acht Schwellen erweitern.
6. Kampf- und Lootmechaniken integrieren.
7. Neue Endgame-Drops eintragen.
8. Bildgenerierung durchführen.
9. Einzel-Icons und Setübersichten einbinden.
10. Windows-UI ergänzen.
11. Twitch Extension ergänzen.
12. Adminpanel ergänzen.
13. Upgrade-Kompatibilität prüfen.
14. Tests durchführen.
15. Ergebnisse dokumentieren.

Keine produktive Veröffentlichung oder automatisches Deployment.

---

# 16. ABSCHLUSSBERICHT

Liefere nach Abschluss:

- Liste aller geänderten Dateien
- Übersicht der drei Eternal-Sets
- Liste aller 24 Items
- Implementierte Seteffekte
- Lootquellen und Dropchancen
- Generierte Bilddateien mit Pfaden
- Screenshots oder Previews, sofern technisch möglich
- Ergebnisse aller tatsächlich ausgeführten Tests
- Noch offene technische Einschränkungen

Keine erfundenen erfolgreichen Tests.

## VERBINDLICHE KERNREGELN

1. Drei neue Eternal-8er-Sets mit insgesamt 24 Gegenständen.
2. Jeder ausgerüstete Setgegenstand aktiviert die nächste kumulative Setstufe.
3. Exakt acht Effekte je Set.
4. Die neuen Sets bleiben zusätzlich zu den bisherigen Eternal-6er-Sets bestehen.
5. Die Waffe gehört verpflichtend zu jedem vollständigen 8er-Set.
6. Zwei verschiedene Ringe sind je Set erforderlich.
7. Eternal bleibt hellgelb.
8. Eternal-Drops nur aus besiegten Level-100-Gegnern im Thron der Urtiefe.
9. Alle 24 Einzelbilder nach Möglichkeit tatsächlich generieren.
10. Bestehende Spielerstände und Funktionen schützen.