# RPG: Lebenspunkte und Überleben

## Bestand und Integration

Erweitert das bestehende `RpgProfile`, `ViewerPointStore.ApplyRpgAsync`, `AdventureGameService`, `RpgChatCommand` und die normale Chat-Verarbeitung. Keine neue Datenbank, kein zweiter Bossmanager, keine Streamer.bot-Abhängigkeit. Vorhandene Waffen, Ringe, Perks und Sushi werden unverändert durch `RpgPassiveService.BossAttack` berücksichtigt. Es wurden weder Lootchancen noch bestehende Einsatz-/Fischbelohnungen verändert.

Im ursprünglich untersuchten Stand gab es **kein Spielerlevel-/EXP-System und keine kleinen Unterwassergegner**. Level/EXP wurden deshalb additiv im vorhandenen Profil ergänzt. Inzwischen ist `RpgHealthService.ResolveEnemy` an automatische Tauchbegegnungen angebunden; siehe [Unterwassergegner](RPG-UNDERWATER-ENEMIES.md).

## HP und Level

- Standard: `100 + (Level - 1) * 2` MaxHP, Level 1–100. Untergrenze immer 0.
- Alte Profile ohne HP beginnen bei ihren berechneten MaxHP. Nullable HP unterscheiden fehlende Daten zuverlässig von gespeicherten 0 HP.
- MaxHP werden abgeleitet; aktuelle HP, letzte angewandte MaxHP, Heilungszeitpunkte, Level und gesamte EXP werden gespeichert. Kein Auffüllen bei Spawn/Neustart.
- Bei höherem MaxHP wird nur dessen Zuwachs geheilt (abschaltbar), keine vollständige Heilung.
- Mangels bestehender EXP-Kurve gilt: nächste Stufe kostet `ExperiencePerLevel * aktuelles Level`, standardmäßig 1.000, 2.000, 3.000 … EXP. EXP sind kumulativ; beim Levelaufstieg werden keine EXP entfernt. Die Basis und Maximalstufe sind einstellbar. Level gewährt HP, keinen ungefragten zusätzlichen Schadensmultiplikator.

## Bosskämpfe

Pro gültigem Angriff läuft genau die vorhandene Spieler-Schadensberechnung. Überlebt der Boss, folgt genau ein Gegenangriff. Der tödliche Treffer auf den Boss erzeugt keinen Gegenangriff. Aktuelle HP und tatsächlicher Boss-Schadensbeitrag (Overkill zählt nicht) werden gemeinsam mit der verarbeiteten Twitch-Message-ID atomar gespeichert. Fehlgeschlagene Speicherung verändert weder Spieler- noch Boss-HP.

Da bisher keine Boss-Seltenheitsklassen existieren, wird der Schwierigkeitsgrad aus den **anfänglichen**, nicht verbleibenden Boss-HP abgeleitet:

| Typ | Ab Boss-HP | Gegenschaden |
|---|---:|---:|
| Gewöhnlich | 10 | 10–20 |
| Stark | 1.000 | 15–30 |
| Elite | 5.000 | 25–40 |
| Legendär | 20.000 | 35–55 |

Alle Grenzen und Schadenbereiche sind einstellbar; Gegenangriffe können deaktiviert werden. Die vorhandenen Spawnintervalle bleiben unverändert.

0 HP sperrt weitere Angriffe. Wer in diesem Boss besiegt wurde, bleibt zusätzlich durch Boss-ID und persistentes Todesflag gesperrt – auch nach Chat-/passiver Heilung. Der nächste Boss hat eine neue ID. Bereits verursachter Schaden bleibt am Boss bestehen.

## Belohnungen und Neustarts

- Sieg: überlebende Teilnehmer teilen den vorhandenen Perlenpool, erhalten jeweils 1.000 EXP und mögliche bisher konfigurierte Bossbeute.
- Der Überlebende mit höchstem tatsächlichem Schaden erhält zusätzlich 200 EXP. Gleichstand: deterministisch nach Twitch-ID.
- Entkommen: überlebende Teilnehmer erhalten 100 EXP, keine Siegperlen oder Siegbeute.
- Besiegte: keine Perlen, EXP oder Items. Aufhören zu attackieren entfernt keinen lebenden Teilnehmer.
- Pro Spieler werden Perlen, EXP und Beute in **einer** vorhandenen Profiltransaktion mit `bosssurvival:<Boss-ID>` gebucht. Wiederholte Abrechnung kann nichts doppelt gutschreiben. Kontolimits entfernen keine vorhandenen Perlen.
- `rpg-boss-round.json` hält atomar Boss-ID, Zeitlimit und ursprüngliche Konfiguration im selben Speicherordner. Gespeicherte Profile halten Beiträge und Boss-Rest-HP nach ihrem letzten Angriff. Nach Neustart rekonstruiert der bestehende Bossmanager Teilnehmer und minimale Rest-HP. So geht auch ein Angriff nicht verloren, wenn der Prozess direkt nach dem Profil-Commit endet. Zeitlimits laufen offline weiter.
- Bei einem Abbruch während der Auszahlung werden noch offene Buchungen erneut versucht; erfolgreiche bleiben durch ihre Aktions-ID geschützt. Ein manuell abgebrochener/deaktivierter Boss verteilt keine Belohnung.
- Die Oberfläche veröffentlicht den rekonstruierten Bosszustand nach dem ersten Tick. Todessperren im HP-Text beziehen sich ausdrücklich auf den zuletzt angegriffenen Boss.

## Heilung

Normale Chatnachrichten heilen standardmäßig 10 HP, einmal pro Twitch-ID alle 30 Sekunden. Alle `!`-Nachrichten (das vorhandene Command-Prefix), auch unbekannte Befehle, sowie Bots und leere Nachrichten sind ausgeschlossen. Die existierende Chat-Ereignisverarbeitung und eine persistente Aktions-ID verhindern Mehrfachheilung durch erneut zugestellte Events. Heilung bleibt im Chat still.

Passive Regeneration: 2 HP pro vollständigen 60 Sekunden. Berechnung bei HP-Abfrage, Angriff, Tauchgang oder normaler Chataktivität statt separater Spielertimer. Bruchteile bleiben erhalten; Zeitstempel verhindern doppelte Minuten. Offline-Regeneration reicht standardmäßig bis MaxHP. Eine optionale Nachholgrenze in Minuten kann dies beschränken. Deaktivierte passive Heilung wird nicht nachträglich gutgeschrieben.

Ein Tauchgang mit 0 HP wird innerhalb der bestehenden Einsatztransaktion zurückgewiesen; es wird kein Einsatz abgebucht. Es gibt keine zusätzlichen Perlenverluste, Itemzerstörung oder EXP-Abzüge.

## Commands und Oberfläche

- `!hp`: aktuelle/maximale HP, Level/EXP, passive und Chat-Heilungswerte, gegebenenfalls Todessperre.
- `!stats`: bestehende Kampfwerte plus HP-/Levelinformationen; längere Antworten verwenden die bereits vorhandene `!rpgtext`-Seitendarstellung.
- Alle bisherigen Commands und Aliase bleiben erhalten. `!hp` wird über den gemeinsamen RPG-Katalog auch im CommandRegistry registriert.
- Punkte & Minigames → RPG → **HP & Überleben**: kategorisierte HP-, Heilungs-, EXP- und Gegenangriffseinstellungen, reguläres validiertes Speichern.
- RPG Debug Center → **HP & Überleben**: HP setzen/anzeigen, Gegenangriff nach Boss-Ausgangs-HP, Besiegtstatus, vergangene Regenerationsminuten und Chat-Heilung. Nur lokaler Administrator, aktivierter Testmodus und isoliertes Testkonto; keine Twitch-Debugbefehle.

## Daten und Grenzen

Schema bleibt additiv Version 1; Inventare, Perlen, IDs, Perks und existierende Buffs werden nicht migriert oder zurückgesetzt. Die vorhandenen `.pre-rpg.bak`-/`.pre-items-v2.bak`-Backups werden vor dem Schreiben weiterverwendet. Persistierte Todessperren werden durch Heilung nicht entfernt.

Keine Änderung an der bisherigen Sushi-Verbrauchslogik. Kleine Gegner verwenden inzwischen denselben HP-/EXP-Service (Details im separaten Gegnerdokument). Eine externe Twitch-/OBS-Liveprüfung und eine visuelle Bedienprüfung der neuen Windows-Oberfläche sind nicht Teil der automatisierten Tests. Änderungen bleiben lokal, ohne automatische Veröffentlichung.

## Verifikation

Vollständige automatisierte Testsuite: **544 bestanden**, darunter **19 neue HP-/Überlebensfälle**. Neue Fälle prüfen MaxHP je Level, sichere Initialisierung, Levelheilung, Regenerationsreste und Neustarts, Command-/Bot-Ausschluss, persönliche Chatcooldowns, parallele/duplizierte Events, Todessperre nach Heilung und Neustart, keine Gegenangriffe beim letzten Treffer, Belohnungsausschluss und Top-EXP, Speicher-Rollback, Timeout-EXP, den Gegner-Integrationspunkt und kostenlose Ablehnung eines Tauchgangs mit 0 HP.

Release-Build: **0 Fehler, 10 bestehende Warnungen** (unter anderem WindowsBase-Versionenkonflikt, Paket- und Nullable-Hinweise). Patchnotes sind unter „Noch nicht veröffentlicht“ ergänzt; keine Versionsanhebung, kein Push und kein Release.
