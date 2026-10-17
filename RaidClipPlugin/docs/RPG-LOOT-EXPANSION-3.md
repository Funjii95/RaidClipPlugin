# Loot Expansion 3.0

Implementiert auf `feature/rpg-loot-expansion`, Release 2.1.21. Keine Streamer.bot-Abhängigkeit.

## Umfang und Kompatibilität

52 neue Definitionen: 34 reguläre Ausrüstungsteile, 8 Setteile, 10 Sammlerschätze. Die vorhandene **Krone des Abgrunds** wird nicht dupliziert oder überschrieben: Sie behält ihre bisherige ID `deepsea-crown`, Zufallswerte und Setzugehörigkeit. Die gewünschte neue defensive Wächtervariante heißt **Tiefseewächter**, damit das bereits existierende offensive Set **Wächter der Tiefsee** unverändert erhalten bleibt.

Alle acht Slots existierten bereits; keine Slotmigration. Ringe passen in beide Ringplätze. Nummerierte Inventare, Besitzprüfung, Snapshotprüfung bei Inventaränderungen, eindeutige Instanz-IDs und atomare Ausrüstungsaktionen bleiben bestehen. Keine automatische Ersetzung von Builds: Im vorhandenen Produktivcode existierte kein Auto-Equip bei Lootfunden; manuelles `!equip` bleibt bewusst bestehen.

Gespeicherte Instanzen, Perlen, Buffs und Bestände werden nicht neu gewürfelt oder zurückgesetzt. Neue Definitionen und Sets werden beim Laden fehlender IDs ergänzt. Neue Felder haben sichere Standardwerte (`DropWeight=1`, `Collector=null`, neue Kampfwerte=0). Keine neue Datenbank und keine Persistenz abgeleiteter Sammler-/Setwerte. Die bestehende atomare Speicherung und ihre Backups werden weiterverwendet. Änderungen an Definitionen wirken auf künftige Funde; vorhandene Instanzen behalten ihre Snapshots.

## Ausrüstung

Alle Werte sind zusätzlich zu den bestehenden garantierten Kraft-/Ausdauer-Hauptwerten. HP-Spalte bedeutet direkte MaxHP; Ausdauer liefert zusätzlich die konfigurierte Menge HP. Prozentangaben für Crit sind Prozentpunkte. Alle Definitionen sind konfigurierbar und permanent, ohne Haltbarkeit oder Verlust bei Tod.

| Slot | Common | Uncommon | Rare | Epic | Legendary |
|---|---|---|---|---|---|
| Waffe | Korallendolch: Boss +3 % | Perlensäbel: Boss +6 % | Tiefseeklinge: Boss +12 % | Sturmharpune: Boss +22 % | Zorn des Leviathans: Boss +30 %, Doppelangriff 5 % |
| Helm | Muschelhelm: HP +5 | Korallenkrone: HP +10 | Helm der Tiefen: HP +20 | Krone des Abgrunds: **vorhandene Werte erhalten** | Leviathans Blick: HP +45, Crit +3 % |
| Brust | Algenpanzer: HP +10 | Schuppenrüstung: HP +20 | Korallenharnisch: HP +35 | Rüstung der Gezeiten: HP +50, Reduktion 5 % | Panzer des Abgrunds: HP +60, Reduktion 12 % |
| Handschuhe | Taucherhandschuhe: Crit +1 % | Korallengriff: Crit +2 % | Handschuhe der Tiefen: Crit +4 % | Sturmgreifer: Crit +6 % | Leviathans Klauen: Crit +8 %, Doppelangriff 15 % |
| Schuhe | Sandläufer: HP +5 | Korallenstiefel: HP +10 | Tiefseeschritte: HP +15 | Schuhe der Strömung: HP +20, Ausweichen 5 % | Gezeitenläufer: HP +25, Ausweichen 10 % |
| Amulett | Muschelanhänger: HP +5 | Glücksperle: seltene Ausrüstung relativ +2 % | Tiefsee-Talisman: Boss +5 % | Amulett der Strömung: Boss +8 % | Neptuns Herz: Boss +10 %, Crit-Heilung 5 % |
| Ring | Ring der Muscheln: HP +5 | Gezeitenring: HP +10 | Ring des Jägers: Crit +3 % | Blutrubin der Tiefen: Crit +5 %, HP +10 | Leviathans Schwur: Crit +8 %, HP +30 |

### Neue Sets

- **Tiefseewächter (Epic, ID `depth-warden`)**: Helm des Tiefseewächters (HP 35), Brustplatte des Tiefseewächters (HP 50, Reduktion 5 %), Handschuhe des Tiefseewächters (Crit 6 %), Stiefel des Tiefseewächters (HP 20, Ausweichen 5 %). 2 Teile: HP +25. 4 Teile zusätzlich: Boss-Reduktion 10 %, Boss-Schaden +3 %.
- **Zorn des Ozeans (Legendary, ID `ocean-wrath`)**: Krone des Ozeanzorns (HP 45, Crit 3 %), Panzer des Ozeanzorns (HP 60, Reduktion 12 %), Klauen des Ozeanzorns (Crit 8 %, Doppelangriff 15 %), Schritte des Ozeanzorns (HP 25, Ausweichen 10 %). 2 Teile: Crit +5 %. 4 Teile zusätzlich: Boss-Schaden +10 %, Doppelangriff 5 %.

Nur unterschiedliche angelegte Definitionen zählen. Inventarduplikate zählen nicht; Ablegen entfernt Boni sofort. Set-HP zählen in derselben HP-Berechnung wie Ausrüstungs-HP und Ausdauer, einschließlich bestehendem Ausrüstungs-HP-Cap. Anlegen heilt nicht und entfernt keine Todessperre.

## Kampfrechnung

- Bestehende zentrale Schadens-, Crit- und Proc-Berechnung bleibt erhalten. Alle Doppelangriffquellen teilen das bestehende konfigurierte Cap (Standard 20 %). Ein erfolgreicher Doppelangriff liefert **einen** Bonus-Treffer mit der bisherigen Standardstärke von 50 %; Triple hat weiterhin Vorrang. Keine rekursiven Procs und nur ein Gegenangriff je Befehl.
- Schaden in Kampfresultat und Beitragskonto wird auf verbleibende Boss-HP begrenzt. Keine zusätzliche EXP-/Beutetransaktion pro Bonus-Treffer.
- Große Boss-Gegenangriffe: ein Ausweichwurf; bei Erfolg 0 Schaden. Danach Ausrüstungs-/Setreduktion und aktives defensives Sushi addieren, insgesamt maximal bestehende 80 %, **einmal** anwenden und aufrunden. Kein solcher Bonus gegen kleine Tauchgegner.
- Neptuns Herz heilt ausschließlich bei kritischem großen Bossangriff. `floor(min(Schaden, Rest-Boss-HP) × Heilprozent / 100)`. Aktiver Sushi-Lifeleech wird in derselben Heilungsfunktion berücksichtigt. Vor dem Gegenangriff, nur bis MaxHP, kein festes HP-Heilungscap, keine Wiederbelebung oder Umgehung der Todessperre.

## Passive Sammlerschätze

Separate strukturierte `RpgCollectorBonuses` statt Kampfattribute. Besitz genügt. Jede Itemdefinition zählt einmal, auch bei drei oder mehr Instanzen. Unterschiedliche Schätze addieren sich; nach Entfernen des letzten Exemplars entfällt der Bonus beim nächsten Berechnen. Keine Kampfwerte, Heilung, Regeneration oder Proc-Chancen durch Inventarbesitz.

| Schatz | Seltenheit | Wirkung |
|---|---|---|
| Kleine Glücksmuschel | Common | Fischfang-Perlen +1 % |
| Perlenkompass | Uncommon | Fischfang-Perlen +2 % |
| Schatzkarte der Tiefsee | Rare | seltene Sammlerschatzchance relativ +2 % |
| Muschelsammlersiegel | Rare | Fischfang-Perlen +3 % |
| Perle des Reichtums | Epic | Boss-Siegperlen +5 % |
| Alte Kapitänskarte | Epic | Sammlerschatzchance relativ +3 % |
| Kompass des Glücks | Legendary | seltene Sammlerschatzchance relativ +5 % |
| Sanduhr der Gezeiten | Legendary | EXP bei überlebten großen Bosskämpfen +5 % |
| Krone des Perlensammlers | Legendary | positive reguläre Dive-Netto-Belohnung +5 % |
| Goldene Muschel | Legendary | seltene Sammlerschatzchance relativ +5 % |

„Besondere Sammelfunde“ und „seltene Sammelschätze“ bezeichnen hier dieselbe Kategorie **Rare/Epic/Legendary innerhalb des reinen Sammlerpools**, nicht Ausrüstung, Sushi-Zutaten oder Fischarten.

Standard-Caps ausschließlich für Sammler: je Perlenquelle 15 %, relative Sammlerchance insgesamt 10 %, Boss-EXP 10 %. Unterschiedliche Ausrüstungs-/Perk-Caps bleiben unverändert. Positive Belohnungen werden abgerundet. Dive-Einsätze, Verluste, technische Erstattungen, administrative Vergaben, Handel, Perkkäufe und Zerlegen erhalten keinen Sammleraufschlag. Sammlerbonus nutzt den Bestand vor der Belohnung, nicht den gerade gefundenen Schatz.

## Lootpools und Gewichte

- Seltenheitsgewichte unverändert: Common 6000, Uncommon 2900, Rare 850, Epic 200, Legendary 50. Normalisierung nur über freigeschaltete, verfügbare Seltenheiten.
- Bestehende Risikostufen unverändert: 0 → maximal Uncommon; 250 → Rare; 1000/5000 → Epic; 10000 → Legendary. Bestehende Chancen und Stake-/Luck-/Hot-Time-Boni bleiben bestehen. Boss-/Fischbeute behält ihre separaten vorhandenen Gates.
- **Ein** vorhandener Seltenheitswurf. Danach innerhalb der gezogenen Seltenheit Sammlerpool gegen regulären Pool. Standard `CollectorPoolChance=15` Prozent, sofern beide Kategorien existieren. Nur vorhandene Kategorie wird direkt gewählt. 0 deaktiviert Sammlerfunde vollständig.
- Relativer Sammlerbonus verändert nur diese Kategoriechance: 15 % × 1,10 = 16,5 %, **nicht** 25 %. Allgemeine und seltene Schatzboni werden für Rare+ gemeinsam auf 10 % begrenzt. Keine Änderung der Legendary-Seltenheitschance, keine Erhöhung legendärer Ausrüstungsfunde durch Sammlerschätze.
- Danach gewichtete Auswahl des konkreten Items. `DropWeight=1` für alle neuen und bisherigen Definitionen; eigene Werte 1–1.000.000. Innerhalb eines gewählten Pools gleich wahrscheinlich bei gleichen Gewichten.
- Glücksperle wirkt **nur angelegt**, nach gezogenem Rare+-Seltenheitsgrad im regulären Pool: relative Erhöhung der Ausrüstungskategorie gegenüber sonstigen regulären Funden. Keine Veränderung der Sammlerquote oder des Seltenheitswurfs. Wenn die Kategorie bereits ausschließlich Ausrüstung enthält, kann der Bonus nichts weiter erhöhen.
- Neue reguläre Ausrüstung/Setteile erlauben Dive, vorhandene Bossbeute sowie Angel-Ausrüstungsfunde (dort weiterhin maximal Epic). Neue Sammlerschätze kommen ausschließlich aus Dive und dem bestehenden Einsatz-gesteuerten kleinen Gegnerloot. Die bestehende Gegnerregel „bester von zwei Würfen, nur ein Gegenstand“ bleibt erhalten.

## Bedienung und Debugging

- Neu: `!treasures [Seite]` zeigt Schatzbestand, einmalige Wirkungen und gedeckelte Summen. Registrierung über den bestehenden gemeinsamen Commandkatalog.
- Weiterhin: `!inv [Seite]`, `!item <Nummer> [Seite]`, `!equip <Nummer> [ring1|ring2]`, `!gear`, `!sets`, `!stats`. Itemdetails zeigen Sammlereffekte, neue Kampfwerte und Zerlegewert. `!stats` nennt aktive Sets, Verteidigung, Crit-Heilung, Sushi-Lifeleech und Doppelangriffchance; lange Antworten verwenden die bestehende sichere Seitenausgabe.
- **Punkte & Minigames → RPG → Loot & Sammlerschätze**: aus dem Haupteditor laden, Kategorie/Definition auswählen, Definition als strukturierten JSON-Abschnitt bearbeiten, übernehmen und speichern. Separate Auswahl für Sets und Pools/Caps/Risikostufen. IDs bestehender Definitionen sind geschützt. Keine Produktions-Itemmutation durch diesen Konfigurationseditor.
- Isoliertes Debug Center kann bestehend jede Definition gezielt erzeugen und Sets anlegen/simulieren. Neu: Item über Inventarnummer entfernen, alles ablegen, tatsächliche Sammlersummen und Setfortschritt anzeigen; Gegenangriffstest verwendet die zentrale Verteidigungsrechnung. Lootwurf/-simulation berücksichtigt Sammlerpools und angelegte Glücksperle. Debugdaten bleiben von Zuschauerdaten getrennt.

## Prüfungen / Einschränkungen

Zusätzliche Tests: Definitionen/Gewichte/Legacy-Krone, einmalige Sammlerwirkung und letzter Verlust, Caps und Kampffreiheit, Zweier-/Vierersets und HP ohne Heilung, DR/Dodge ohne kleine-Gegnerwirkung, Crit-Heilung bei 500/1000/5000/10000 Schaden, Overkill/Todessperre, relative Poolchance und Legendary-Gate, gewichtete Itemauswahl, Ring-/Slotkompatibilität und Neustart, Chatbefehl, echte Fischbelohnung versus Adminzahlung, Rollsignaturen und echte Boss-Transaktionen samt Dublettenabwehr/Überlebensbelohnungen.

Ausgeführt: vollständige Testsuite im Release-Modus, **628 bestanden, 0 fehlgeschlagen** (18 neue Testfälle). Release-Build erfolgreich mit **0 Fehlern**, zehn vorhandenen Projektwarnungen (WindowsBase, Paket-/Nullable-Hinweise). Veröffentlichung separat vom Benutzer beauftragt.

Bewusst keine neue Auto-Equip-Heuristik, keine grafische Itemkarte und keine öffentliche Extension-API. Der neue Konfigurationsreiter ist ein fokussierter Definitionseditor, noch kein vollständig formularbasierter Designer. Die OBS-/Twitch-Liveintegration wurde in diesem Auftrag nicht auf einem laufenden Stream bedient. Strukturierte Instanzdaten und wiederverwendbare Services bleiben für spätere Karten/Tooltips verfügbar.

Für spätere Erweiterungen: grafische Konfigurationsformulare, neue Wirtschaftsquellen explizit klassifizieren, weitere Kategorien erst mit eigenen Pooltests einführen und Itemgewichte statt globale Legendary-Chancen ändern.
