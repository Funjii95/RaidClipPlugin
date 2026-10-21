# Perks und Mimic

Perks werden mit Perlen gekauft, unabhängig von Talentpunkten. Der Standardkatalog enthält 31 Perks mit maximal fünf Rängen. Die sieben bisherigen IDs, gekauften Ränge, individuellen Preise und Attributboni bleiben erhalten; der Konfigurationslader ergänzt fehlende Standard-Perks. Höhere historische Ränge werden nicht aus den Spielständen gelöscht, wirken aber höchstens wie Rang fünf. Es gibt keine automatische Rückerstattung.

## Spielregeln

- Kampf: Schaden, Kritchance, Kritschaden, Bossschaden, prozentuale Basis-MaxHP, kürzerer Boss-Angriffscooldown, Lebensraub, Boss-Siegperlen und Otterzorn (60 Sekunden nach einem Kampfsieg).
- Angeln: bisheriges Angelglück, weniger Fehlfänge, zusätzlicher Fisch, höhere relative Fanggewichte für seltene/epische/legendäre Fische, zusätzliche Zutatenfische, kürzerer Cooldown und positive Netto-Perlenbelohnung.
- Tauchen: bisheriges Tauchglück, zusätzlicher Item-Lootwurf, relative Sammlerschatzchance, relative Chance auf seltene Sammlerschätze, kürzerer Cooldown, vermiedene Unterwasser-Gegnertreffer (keine Bosse), höhere Item-Dropchance, positive Netto-Perlenbelohnung und Mimic-Fundchance.
- Support: bisheriges allgemeines Glück, stärkere persönliche Sushi-Boni bei der Aktivierung, zusätzliche passive HP-Regeneration und persönliche Boss-Schadensreduktion.

Teamgeist, Perlenspender, Motivationsruf und Händlerfreund werden nicht als kaufbare Platzhalter angeboten. Die dafür nötigen Gruppen- beziehungsweise Rabattregeln sind nicht implementiert. „Schatzsucher“ und „Reliktjäger“ verwenden ausdrücklich vorhandene Sammlerschätze; es werden keine neuen Truhen- oder Reliktinventare vorgetäuscht. Perk-Symbole verwenden keine Ausrüstungsbilder.

Talente und Perks tragen gemeinsam zu vorhandenen Regeln bei. Einsatz- und Seltenheitsgrenzen sowie bestehende Effektgrenzen bleiben wirksam. Doppelfang und Zutatenbonus benötigen einen erfolgreichen Fischfang. Mehr Beute bedeutet zusätzliche beziehungsweise bessere Würfe und keine garantierten Gegenstände. Lebensraub basiert auf tatsächlichem Schaden ohne Overkill und belebt nicht wieder.

## Mimic und Legendary-Beute

Die Mimic hat standardmäßig 0,5 % Grundchance je erfolgreich gespeichertem Tauchgang. Abgrundsinn ergänzt 0,1 Prozentpunkte pro Rang: 0,6 / 0,7 / 0,8 / 0,9 / 1,0 % insgesamt. Ungültige, doppelte oder wegen Cooldown abgelehnte Aktionen starten keine Mimic. Während eines aktiven Bosses oder der Rundenpause wird kein weiterer Boss gestartet. Chat und Extension verwenden dieselbe gemeinsame Bossrunde.

Nach einem Sieg erhält jeder berechtigte überlebende Teilnehmer einen separaten Legendary-Wurf: normaler Boss 5 %, Mimic 50 %. Der Wurf ist unabhängig vom normalen, maximal epischen Boss-Lootwurf und wird nicht durch Hot Time erhöht. Gewählt wird nach den vorhandenen Dropgewichten aus legendären, für Bossbeute freigegebenen Gegenständen. Ohne passenden Pool gibt es kein Ersatzitem. Die bisherigen HP-/Teilnehmerbedingungen für legendäre reguläre Beute begrenzen diesen neuen separaten Wurf nicht. Niederlage, Flucht und Abbruch geben kein Legendary. Belohnungen und Items werden gemeinsam mit der vorhandenen idempotenten Belohnungstransaktion gespeichert.

Im Plugin unter **RPG → Mimic & Bossbeute** sind Aktivierung, Grundchance, Name, HP, Zeitlimit, Angriffscooldown, Perlenpool sowie beide Legendary-Chancen einstellbar. Neue Kampfwerte gelten ab der nächsten Mimic; Lootchancen verwenden die aktuelle Einstellung. Globale Boss-Aktivierung, HP-System und Spielbereitschaft gelten weiterhin. Das bereitgestellte Mimic-Bild wird nur für Mimic-Runden verwendet.

## Bereitstellung und Prüfung

Die Änderungen benötigen das neue Windows-Plugin und das neue Extension-Paket. Ein alleiniger Frontend-Upload aktiviert die serverseitigen Perks und Mimic-Regeln nicht. Das vorhandene buyPerk-Protokoll sowie die bestehenden Bossstatus-/Angriffsaktionen werden weiterverwendet; es wird kein separater Mock-Boss gestartet. Die laufende Plugin-Installation und Cloud-Dienste werden durch die Testpakete nicht ersetzt.

Geprüft: vollständige Plugin-Testsuite, zusätzliche Tests für 5/5-Kaufsperre, Neustart, Perkboni, Sushi, Fischgewichte, Fund- und Legendary-Grenzwerte, Einstellungen, einmalige gemeinsame Bossrunde und Belohnungen; Frontend-Tests sowie Browsertests für Bild, Rangbeschreibung, Kaufbestätigung, Bosszeit und schmale Ansichten.
