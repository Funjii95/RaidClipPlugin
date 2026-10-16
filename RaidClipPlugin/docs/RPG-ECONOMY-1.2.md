# RPG Economy & Balancing 1.2 – lokaler Prüfbericht

Stand: 16.10.2026. **Veröffentlichung nach ausdrücklicher Freigabe als Version 2.1.12.**

## Umsetzung und Schutz bestehender Einstellungen

Neue Konfigurationen verwenden die gewünschten Werte:

| Bereich | Neue Standardwerte |
|---|---|
| Dive-Chance | Basis 25 %, Cap 60 %, Luck 0,1 Prozentpunkte/Punkt, Luck-Cap 10 Prozentpunkte, Event 0/Cap 10 |
| Legendary-Mindesteinsatz | 10.000 |
| Einsatzstufen (Minimum / Maximum-Rarity / Dropbonus) | 0/Uncommon/0; 250/Rare/4; 1.000/Epic/8; 5.000/Epic/12; 10.000/Legendary/15 |
| Rarity-Gewichte | Common 6000, Uncommon 2900, Rare 850, Epic 200, Legendary 50 |
| Dive-Maximaleinsatz | 10.000; andere Spiele behalten ihre bisherigen Grenzen |
| Alle sieben Standardperks | 5 Level, Basiskosten 500, Wachstum 2: 500/1.000/2.000/4.000/8.000; insgesamt 15.500 je Perk, 108.500 für alle sieben |
| Sushi-Kosten | fishing/sardine 250; fighter/perch 500; critical/crystal-carp 1.000; dive/eel 1.500 |

Perk-Boni pro Level, Itemattribute, Crit-/Luck-/Damage-Caps, Fischgewichte, Boss-Schaden, Bossbelohnung und Boss-Rhythmus wurden nicht geändert. Legendäre Drops bleiben vor der Seltenheitsauswahl an den tatsächlich abgebuchten Einsatz gebunden. Vorhandene Idempotenz, Kontosperre, gemeinsame Perlenabrechnung und persistente Cooldowns bleiben bestehen.

**Keine stille Migration:** Beim Laden bleiben explizit gespeicherte alte und eigene Werte erhalten, auch ein bisheriger Dive-Maximalbetrag unter 10.000. Unter **Punkte & Minigames → RPG → Einstellungen → Balancing 1.2 übernehmen** kann das Preset nach Bestätigung in den Editor übernommen werden. Es ersetzt die oben genannten Balancingfelder und die Kosten/Maximallevel der sieben Standardperks, nicht eigene Itemdefinitionen, Attribute, Fischarten oder weitere eigene Perks/Rezept-IDs. Den Dive-Maximalbetrag hebt es gezielt auf mindestens 10.000 an. Erst **Speichern** macht die Übernahme dauerhaft. Höhere eigene Dive-Grenzen bleiben erhalten. Kein automatischer Aufruf des Presets beim Laden.

Bereits gekaufte Perk-Level und Inventare werden nicht migriert oder neu bepreist. Es gibt weder nachträgliche Abbuchungen noch Erstattungen; nur zukünftige Käufe verwenden die bewusst übernommenen Kosten. Die Einnahmewerte aus dem Auftrag wurden **nicht** in AppConfig, Vorlage oder Benutzerdateien geändert.

## Fische und Equipment löschen

Unter **RPG → Spielerverwaltung**:

1. **Spieler laden**, das Konto anhand Name und eindeutiger Twitch-ID wählen.
2. **Fische löschen:** Fischart wählen und Anzahl eingeben. Zu große/negative Mengen werden abgelehnt; bei null Restbestand verschwindet die Art aus dem verbrauchbaren Vorrat.
3. **Equipment löschen:** eine konkrete Iteminstanz auswählen. Nur dieses Stück wird gelöscht und gegebenenfalls aus seinem Ausrüstungsslot entfernt. Andere Instanzen desselben Gegenstands bleiben erhalten.

Beide Aktionen verlangen eine Sicherheitsabfrage, erstatten keine Perlen und verändern weder Perks noch Buffs oder Kontostand. Historische Fangzahlen bleiben erhalten; das Importkennzeichen verhindert eine erneute Wiederherstellung gelöschter Vorräte aus diesen Statistiken. Keine neuen Lösch-Chatbefehle für Zuschauer.

Vor jeder erfolgreichen Löschtransaktion entsteht eine vollständige Sicherung als `viewer-points.json.rpg-delete-<ID>.bak` im lokalen `RaidClipPlugin/minigame`-Datenordner. Speicherfehler rollen Inventar-/Slotänderungen zurück. Sicherungen werden nicht automatisch gelöscht. Eine Wiederherstellung muss bei beendetem Plugin bewusst aus der passenden Sicherung erfolgen; sie betrifft die vollständige Kontodatei, nicht nur das gelöschte Item.

Es wurden ausschließlich automatisch erzeugte Testkonten gelöscht, keine produktiven Spielerbestände.

## Überprüfung der tatsächlichen Einnahmelogik

`ChatMinigameService.OnChatMessageAsync` markiert jeden nicht ausgeschlossenen Nutzer mit empfangener Chatnachricht als aktiv. Beim Intervallaufruf wird diese Aktivitätsliste geleert. Im aktuellen Intervall explizit lurkende Nutzer oder Nutzer ohne Nachricht bekommen den Lurker-Satz, sofern `LurkersReceiveNormalAttendancePoints` nicht aktiviert ist. Bei aktiviertem Haken bekommen auch diese Nutzer den normalen Satz.

Damit sind mit den **vom Auftrag vorgegebenen** Einnahmewerten bei vollständiger Vergütung möglich: Lurker 1.000/h, in jedem Dreiminutenintervall aktive Nutzer 2.000/h, maximal vergüteter Chat zusätzlich 600/h. Eine Nachricht alle drei Minuten genügt für den vollen Aktivsatz und bringt außerdem ungefähr 200 Chatperlen/h. Die Annahme „aktiv, aber kein Chat“ wäre daher nicht konsistent.

Einschränkungen: Chatter-API-Fehler fallen auf die aktive Chatterliste zurück; stille Nutzer können dann fehlen. Angefangene Intervalle und Modulneustarts reduzieren reale Einnahmen gegenüber einem kontinuierlichen Stundenmodell. Der Vergütungsloop selbst hat **keine Live-Stream-Prüfung**; läuft das Modul außerhalb eines Streams weiter und liefert Twitch Chatters, sind zusätzliche Offline-Vergütungen möglich. Das wurde dokumentiert, nicht ungefragt geändert.

## Reproduzierbare Simulation

Tool: `Tools/RpgEconomySimulation/RaidClipPlugin.EconomySimulation.csproj`. Aufruf aus dem Repository:

```powershell
dotnet run --project RaidClipPlugin/Tools/RpgEconomySimulation/RaidClipPlugin.EconomySimulation.csproj -c Release -- <absoluter-Ausgabepfad.json>
```

Seed **20261016**, .NET 10, **100.000 gültige Dives pro Zelle**: fünf Einsatzstufen × fünf Konfigurationen = **2.500.000 Tauchgänge**. Die Produktionsservices für Solo-Ergebnis, Loot, Charakterwerte und Buff-Verbrauch werden verwendet; nur der kryptografische RNG wird durch einen festen Test-RNG ersetzt. Keine Zugriffe auf Benutzerkonten oder laufende Chat-/OBS-Verbindungen. Zweiter Lauf erzeugte byte-identische Ergebnisdaten, SHA256 `AC84F0758AA29F79C42AB0FA758CBE9330040A3D1C7B82941BBFBE5EEA905727`.

[Vollständige Rohdaten](RPG-ECONOMY-1.2-results.json) enthalten theoretische Wahrscheinlichkeiten, beobachtete Rarity-Zahlen, Auszahlungen, Gewinne/Verluste und Sushi-Kosten pro Zelle.

Szenarien: **Base** ohne Boni; **MaxPerks** alle sieben Perks auf Level 5; **Equipment** Ring des Tiefenmeeres, Amulett der Gezeiten und Flossenschuhe; **Sushi** Dive-Sushi; **Combined** alle genannten Quellen. In Sushi-Stresszellen werden exakt drei gültige Aktionen pro Sushi verwendet und dann neu aktiviert. Fischverfügbarkeit wird dort bewusst als unbegrenzt angenommen, die tatsächlichen Rezeptkosten werden alle drei Aktionen abgezogen. Diese Zellen sind kein realistischer Crafting-Nachschubplan, sondern die obere Nutzungsintensität.

### Dive-Ergebnisse ohne Boni

| Einsatz | Theoretische Dropchance | Legendary je Dive | Theoretischer Perlen-Nettoeffekt | Simulierter Nettoeffekt |
|---:|---:|---:|---:|---:|
| 0 | 25 % | 0 % | 0 | 0 |
| 250 | 29 % | 0 % | −12,50 | −12,45 |
| 1.000 | 33 % | 0 % | −50 | −50,23 |
| 5.000 | 37 % | 0 % | −250 | −242,95 |
| 10.000 | 40 % | 0,20 % | −500 | −539,30 |

Die bestehende Dive-Verteilung ergibt 0,50×0 + 0,25×1 + 0,20×2 + 0,05×6 = **0,95-fache Auszahlung**, also im Durchschnitt 5 % Verlust. Ein höherer Einsatz allein macht den Dive nicht profitabel. Itemfunde sind nicht als verkaufbare Perlen verbucht, weil aktuell kein Itemverkauf existiert.

Bei 10.000 Einsatz lieferte Base **211 legendäre Items** statt theoretisch 200 je 100.000 Dives; diese Zufallsabweichung ist erwartbar. Ohne Freischaltung gab es in allen simulierten Zellen **keine** Legendarys.

| 10.000 Einsatz, Szenario | Dropchance | Legendary theoretisch | Legendary beobachtet / 100.000 | Netto simuliert ohne Crafting | Netto mit Crafting |
|---|---:|---:|---:|---:|---:|
| Base | 40 % | 0,2000 % | 211 | −539,30 | −539,30 |
| MaxPerks | 42 % | 0,2100 % | 208 | −511,90 | −511,90 |
| Equipment | 41,2 % | 0,2060 % | 200 | +43,67 | +43,67 |
| Sushi | 40,5 % | 0,2025 % | 169 | −517,70 | −1.017,71 |
| Combined | 43,7 % | 0,2185 % | 213 | −15,235 | −515,245 |

Das positive Stichprobenergebnis der Equipment-Zelle ist **kein positiver theoretischer Erwartungswert**: mit dem 5-%-Perlenamulett liegt dieser bei −25 Perlen/Dive, also nur −0,25 %. Der Stichprobenfehler ist im Verhältnis zu diesem kleinen Abstand groß.

## Zuschauerprofile und Progression

Annahmen: 0 Startperlen; Vergütung nur während der angegebenen beobachteten Stunden; A vollständig still, B eine Nachricht je Dreiminutenintervall, C eine vergütete Nachricht je Minute. Keine angenommenen Follow-/Sub-/Raid-/Reward-Ereignisse, da deren Häufigkeit unbekannt ist. Diese Einnahmen bleiben unverändert und kommen bei realen Ereignissen hinzu.

Boss-Sensitivität: A keine Teilnahme; B zwei, C vier Bossversuche je Stunde, je zehn Teilnehmer und 80 % Siegquote. Bestehender Topf 500, also 50 Perlen pro Teilnehmer und Sieg. Vier Versuche/h setzen laufende Auto-Starts oder manuelle Starts voraus; Auto-Start ist standardmäßig ausgeschaltet. Andere Teilnehmerzahlen/Siegquoten ändern den Anteil unmittelbar.

**Theoretische Sparzeiten** sind alternative Ziele ohne gleichzeitige Käufe, Spielverluste oder Crafting, inklusive des erwarteten Bossanteils. Sie sind Durchschnittsraten, keine auf die Minute garantierten Freischaltungen.

| Profil | Stunden/Woche | Anwesenheit + Chat/Woche | Boss extra/Woche | Gesamt vor Spielen | erster Perk (500) | ein Perk Level 5 (15.500) | erster 10k-Dive |
|---|---:|---:|---:|---:|---:|---:|---:|
| A Gelegenheit/Lurker | 2 | 2.000 | 0 | 2.000 | 0,50 h | 15,50 h / 7,75 Wochen | 10,00 h / 5 Wochen |
| B Regelmäßig | 8 | 17.600 | 640 | 18.240 | 0,22 h | 6,80 h / 0,85 Wochen | 4,39 h / 0,55 Wochen |
| C Sehr aktiv | 20 | 52.000 | 3.200 | 55.200 | 0,18 h | 5,62 h / 0,28 Wochen | 3,62 h / 0,18 Wochen |

Alle sieben Perks kosten theoretisch A **54,25**, B **5,95**, C **1,97 Wochen** reiner Sparzeit. Earlygame ist damit schnell sichtbar, vollständiger Perkausbau für sehr aktive Nutzer aber eher ein Zweiwochenziel als ein monatelanges Endgame.

### Tatsächlich simulierte Zuschauerverläufe

Zusätzlich je Profil 100.000 fortlaufende Wochen mit übertragenem Kontostand, Bosszufall, **einem 100-Perlen-Fish je Stunde** und einem Brutto-Divebudget von **25 % der Anwesenheits-/Chateinnahmen**, maximal ein 10k-Dive pro zwei beobachtete Stunden. Kein negativer Kontostand; ein Dive erfolgt erst, wenn Konto und angespartes Divebudget reichen. Keine automatischen Perkkäufe; die Profile messen Spieleinnahmen, nicht eine vorgeschriebene Kaufreihenfolge. Diese langen Verläufe sind statistische Versuche, keine reale Langzeitbeobachtung.

| Profil | Simulierter Nettozuwachs/Woche | Fish-Netto/Woche | 10k-Dives/Woche | Legendarys/Woche beobachtet | Legendarys/Woche theoretisch bei dieser Divezahl |
|---|---:|---:|---:|---:|---:|
| A | 1.938,588 | −24,712 | 0,05 | 0,00011 | 0,00010 |
| B | 17.929,896 | −93,839 | 0,44 | 0,00090 | 0,00088 |
| C | 54.313,359 | −245,252 | 1,30 | 0,00257 | 0,00260 |

Die bisherige Fish-Verteilung zahlt im Mittel 0,88×Einsatz: −12 Perlen pro 100er Versuch, plus zusätzliche nicht monetarisierte Fische. Das ist im Modell berücksichtigt. Andere Casino-/Heist-/Rennen-Nutzung wird ohne bekannte Teilnahmehäufigkeit nicht erfunden; hier wird sie als null modelliert. Ebenso sind Daily und eventabhängige Boni nicht pauschal unterstellt.

Bei einem vollständigen Ausgeben des Wochenbudgets für Dives statt einer 25-%-Bruttobudgetregel ändern sich die Zahlen stark, weil Auszahlungen erneut eingesetzt werden können. Daher sind die niedrigen Legendary-Raten **keine universellen Prognosen**. Base braucht durchschnittlich 500 freigeschaltete Dives je Legendary, nicht zwingend 5 Millionen neue Perlen: 5 Millionen ist der mittlere Einsatzumsatz, der erwartete Nettoverlust ohne Boni ungefähr 250.000. Nach etwa 347 Dives beträgt die Fundwahrscheinlichkeit mindestens 50 %, nach etwa 1.497 Dives ungefähr 95 %; garantiert ist weiterhin nichts.

## Erkannte Probleme und spätere Empfehlungen

1. **Sehr aktive Spieler maximieren Perks schnell:** unter diesen Einnahmen etwa zwei Wochen für alle sieben. Gewünschte neue Preise umgesetzt, nicht ungefragt weiter erhöht. Später Prestige, zusätzliche langfristige Ziele oder bewusst höhere Endlevel-Kosten erwägen.
2. **Perlenamulett lässt wenig Hausvorteil:** 0,95×1,05 = 0,9975. Bereits insgesamt mehr als rund 5,263 % PearlBonus ergibt theoretisch positive Dive-Erwartung; der bestehende konfigurierbare Cap erlaubt deutlich mehr. Neue Kombinationsitems deshalb wirtschaftlich prüfen. Keine Änderung am vorhandenen Bonus oder Cap ohne Freigabe.
3. **Dive-Sushi ist teuer für den Effekt:** +5 DiveLuck bedeutet ohne Cap +0,5 Prozentpunkte Dropchance. Über drei Dives entstehen im Mittel nur 0,015 zusätzliche Items für 1.500 Perlen und einen seltenen Aal. Bei Legendary-fähigen Dives erhöht sich die Legendary-Chance nur um 0,0025 Prozentpunkte je Dive. Diese Kosten wirken als starke Perlenvernichtung, sind als Kaufanreiz schwach. Später Effekt/Kosten gezielt nachtesten statt jetzt andere Zielwerte einzusetzen.
4. **Fische bleiben selten:** ohne Bonus bei einem erfolgreichen Fang 65/25/8/1,9/0,1 %. Bei der normalen 50-%-Fangwahrscheinlichkeit pro Versuch entspricht der Aal etwa 0,95 %, der Leviathan 0,05 % pro Fish. Fishing-Sushi erhöht seltene Gewichte nur um 5 % (nicht um fünf Prozentpunkte der Fangchance) und ändert die Perlenvergabe nicht. Boss-Sushi verbessert Schaden/Crit, garantiert aber keine höhere Einzelbelohnung, weil der vorhandene Topf gleichmäßig geteilt wird.
5. **Bossinflation im Modell mäßig:** +640 bzw. +3.200 pro Woche entsprechen ungefähr 3,6 % bzw. 6,2 % der jeweiligen Anwesenheits-/Chateinnahmen. Bei wenigen Teilnehmern steigt der Einzelanteil, maximal theoretisch 2.000/h für einen allein siegenden Nutzer bei vier Runden/h. Topf und Rhythmus unverändert lassen, reale Teilnahme messen.
6. **Kostenlose Dives bleiben farmbar:** 25 % Chance auf Common/Uncommon ohne Währungsverlust. Kein direkter Perlenverkauf, aber Inventar- und Speicherwachstum möglich. Bestehende 30-Sekunden-Cooldowns schützen nicht vor dauerhaftem Farmen. Spätere Recycling-/Inventarregeln nur nach Freigabe.
7. **Offline-Vergütung und weitere Einnahmequellen** können die Sparzeiten weiter verkürzen. Vor finaler Wirtschaftsbewertung reales Modulverhalten und zusätzliche Daily-/Event-/Casino-Nutzung erfassen.
8. Bestehende Beschränkungen (keine persistente Chat-Outbox, langfristig wachsende Idempotenzliste, Store für eine Instanz) bleiben dokumentiert; sie wurden hier nicht durch einen Architekturumbau ersetzt.

## Tests, Build und Dateien

Vollständige Testsuite: **401 bestanden**, keine fehlgeschlagen. Neu abgedeckt sind die 10.000er-Grenzen, Gewichte, Perk-/Rezeptpreise, Erhalt gespeicherter Einstellungen, kombinierte Caps, gezieltes Löschen, Idempotenz, Entfernen belegter Slots, Sicherung und Rollback bei Schreibfehlern. Bestehende Tests der Angel-Auszahlung, Buff-Aufladungen und Boss-Crits bleiben grün. Keine produktiven Konten migriert oder verändert. Visuelle Liveprüfung der neuen Verwaltungsseite steht noch aus.

Release-Build separat geprüft; bekannte NU1510-/WindowsBase-WebView2-Warnungen bleiben vorhanden.

Geändert: `Config/RpgConfig.cs`, `RpgLootConfig.cs`, `AdventureGamesConfig.cs`; `MainForm.AdventureGames.cs`; `Services/ViewerPointStore.Rpg.cs`; bestehende RPG-Tests; csproj und AssemblyInfo zur getrennten Simulation. Neu: `RpgEconomyPreset`, `RpgAdministrationService`, `MainForm.RpgAdministration.cs`, Economy-/Löschtests, Simulationstool und diese Ergebnisdateien. Keine Änderungen an Einnahmewerten, Spielerdateien, Chatbefehlen oder Releaseversion.
