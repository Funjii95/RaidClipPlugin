# Neue Minigames

## OBS, Event-Steuerung und eigene Texte

Unter **Event-Steuerung** lassen sich Boss und Rennen starten oder abbrechen. Der Status zeigt Teilnehmer, verbleibende Zeit und Boss-Lebenspunkte. Ein Abbruch erzeugt keine Gewinne oder Einsatzabbuchungen.

In OBS eine **Browserquelle** mit URL `http://127.0.0.1:18769/`, Breite 600 und Höhe 500 anlegen. Das Plugin muss mit laufendem Minigame-Modul geöffnet bleiben. Der Hintergrund ist transparent; ohne Events wird nichts angezeigt. Die Adresse bindet nur an den eigenen Rechner. Bei belegtem Port wird der Fehler protokolliert; die Spiele laufen unabhängig weiter. Das Plugin ändert keine OBS-Szenen.

Eigene Chattexte sind in den vier Spiel-Reitern einstellbar. Leere Texte unterdrücken die jeweilige Meldung. Je nach Meldung stehen `{user}`, `{outcome}`, `{stake}`, `{payout}`, `{currency}`, `{balance}`, `{seconds}`, `{count}`, `{hp}`, `{damage}`, `{reward}`, `{pot}` und `{reason}` zur Verfügung. Die Event-Steuerung zeigt die Platzhalter ebenfalls an. Ausgaben werden auf die sichere Chatlänge begrenzt.

## Fischsammlung und Statistiken

Erfolgreiche Fänge sammeln Rotfedern, Hechte und seltene Goldene Kois, Mondaale oder Kraken. Ein Stiefel zählt nicht als Fisch. Die Gewichte sind zufällig, die seltenen Fänge wiegen 5–20 kg. `!sammlung` zeigt die Arten und Häufigkeiten sowie den größten Fang. `!profil` zeigt zusätzlich Rennsiege, besiegte Bosse und die Anzahl gesammelter Arten. Abgelehnte Spiele erhalten keine Sammlungseinträge. Die Daten werden mit den Zuschauerpunkten dauerhaft gespeichert; bestehende Profile starten mit leeren Statistiken.

Die Einstellungen stehen unter **Punkte & Minigames** in eigenen Reitern. Punkte und Minigames müssen aktiviert sein. Alle Befehle erscheinen bei aktivem Modul in `!command`; bestehende Rollen- und Deaktivierungseinstellungen gelten weiterhin.

Boss- und Heist-Start sind Mods/Broadcastern vorbehalten, auch bei direktem `!boss` oder `!heist`. Ein Rennen kann jeder Zuschauer mit ausreichenden Punkten starten. Gemeinsame Startbefehle für Mods/Broadcaster: `!start boss`, `!start race <Einsatz>` und `!start heist`.

| Spiel | Start | Teilnahme |
| --- | --- | --- |
| Fischen | `!fish <Betrag\|all>` | Sofortiges Solospiel |
| Schatzsuche | `!dive <Betrag\|all>` | Sofortiges Solospiel |
| Rennen | `!start race <Betrag\|all>` | `!joinrace` |
| Bosskampf | `!boss` oder automatisch | `!attack` (angreifen) |
| Bestehender Heist | `!heist` | `!joinheist` |

## Fischen und Schatzsuche

Einsatzgrenzen und Nutzer-Cooldown sind pro Spiel einstellbar. `all` setzt das verfügbare Guthaben bis zum eingestellten maximalen Einsatz. Auszahlungen enthalten den Einsatz; 1x bedeutet deshalb weder Gewinn noch Verlust.

- Fischen: 50% 0x, 30% 1x, 17% 2x, 3% 8x.
- Schatzsuche: 50% 0x, 25% 1x, 20% 2x, 5% 6x.

Diese Werte sind die Standardwerte. In den Reitern Fischen und Schatzsuche sind alle vier Chancen und Auszahlungsfaktoren getrennt einstellbar. Chancen: je 0–100 %, zusammen genau 100 %. Faktoren: 0–100, auch Dezimalwerte wie 1,5. Die Auszahlung enthält den Einsatz und wird auf ganze Punkte abgerundet. Ungültige Einstellungen werden vor dem Speichern abgelehnt. Die Seltenheit eines Fischs richtet sich nach der Ergebnisstufe, nicht nach dem Auszahlungsfaktor.

Die Spiele nutzen die vorhandenen Tageslimits und das Kontolimit. Ergebnisse werden im Verlauf gespeichert.

## Rennen

Der erste Nutzer legt den gemeinsamen Einsatz fest und tritt selbst bei. Weitere Nutzer treten mit `!joinrace` zum gleichen Einsatz bei. Nach der einstellbaren Beitrittszeit gewinnt ein zufällig gewählter Teilnehmer den vollständigen Topf. Alle haben dieselbe Chance.

Es werden keine Einsätze vorab reserviert. Bei der Auswertung werden Guthaben und Tages-/Kontolimits aller Teilnehmer erneut geprüft und die ganze Runde gemeinsam verrechnet. Reicht ein Guthaben oder Limit nicht mehr aus, wird die Runde für alle ohne Abbuchung abgebrochen. Auch bei weniger als zwei Teilnehmern, deaktiviertem Spiel oder Modul-/Programmende gibt es keine Abbuchung. Ein Nutzer kann nur einmal pro Runde teilnehmen.

## Bosskampf

`!boss` startet einen Boss oder zeigt den bereits aktiven Boss an. `!attack` verursacht 10–30 Schaden; weitere Angriffe sind nach dem eingestellten Nutzer-Cooldown möglich. Es gibt keinen Einsatz. Bei einem Sieg wird der Belohnungstopf auf die unterschiedlichen Angreifer gleichmäßig aufgeteilt, abgerundet auf ganze Punkte. Kontolimits können die tatsächlich gutgeschriebene Belohnung begrenzen. Bei Zeitablauf gibt es keine Belohnung.

Unter **Bosskampf** lassen sich **Automatisch starten** und **Auto-Start alle X Minuten** einstellen. Standard: deaktiviert, 15 Minuten. Der erste automatische Start erfolgt nach dem Intervall ab Modulstart bzw. Speichern der Einstellungen. Jeder Bossstart setzt das nächste Intervall neu. Aktive Kämpfe werden nicht überlagert; die Rundenpause kann einen Auto-Start verzögern. Der Zeitplan besteht nur bei laufendem Plugin/Modul und wird nicht über Neustarts gespeichert.

Der alte Heist-Standard `!join` wird beim Laden auf `!joinheist` umgestellt. Andere selbst konfigurierte Heist-Befehle bleiben erhalten. Der Giveaway-Alias `!join` wird nicht verändert.
