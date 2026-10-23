# Talentfunktion und neue Talentbaum-Ansicht

Stand: 21.10.2026. Plugin-Implementierung plus kompatibler Cloud-/Extension-Vertrag; keine Änderung laufender Spielstände durch den Build.

## Regeln

- Ein Talentpunkt pro Charakter-Levelaufstieg; Level 1: 0, Level 30: 29, Level 100: 99 Punkte insgesamt.
- Rückwirkender Anspruch wird aus dem gespeicherten PlayerLevel abgeleitet. Keine wiederholten Login-Gutschriften und keine Perlenkosten.
- Drei Bereiche mit je 33 Punkten: sechs Talente mit fünf Rängen und ein Abschluss-Talent mit drei Rängen.
- Folgetalente benötigen Rang 3 des jeweiligen Vorgängers; Abschluss-Talente benötigen beide Vorgänger auf Rang 3.
- Der gemeinsame Katalog liegt in Sync/src/SnapshotProtocol/talents.json und wird von Plugin und Oberfläche verwendet. Boni und genaue Grenzen stehen dort.
- Unterwasserjäger: 5 % Schaden pro Rang, maximal 25 %, ausschließlich gegen normale Unterwassergegner.
- Unterwasser-Ausweichen: 4 Prozentpunkte pro Rang, maximal 20 % Chance je gegnerischem Treffer. Keine Bosswirkung, kein Schutz des Einsatzes und keine Beutegarantie.
- Kampfrausch verkürzt den Boss-Angriffscooldown. Abgrundsinn gibt einen zusätzlichen Gegner-Beutewurf statt einer nicht existierenden Bossfundchance. Seltene/hochwertige/legendäre Fische verwenden relative Fanggewichte; Einsatzgrenzen bleiben erhalten.
- Lebensraub bezieht sich auf tatsächlich verursachten Schaden ohne Overkill und kann nicht wiederbeleben. HP-Bonus multipliziert Basis- und Level-HP, nicht Ausrüstungs-HP.
- Dropchancen bleiben innerhalb bestehender Dropcaps. Perlenboni gelten nur für positive Netto-Belohnungen. Alte Perks bleiben ein getrenntes System.

## Plugin und Chat

RpgProfile.TalentRanks speichert die Ränge dauerhaft. Alte Profile ohne dieses Feld starten mit leeren Rängen und ihrem vollständigen Levelanspruch. Vor der ersten RPG-Speicherung wird eine .pre-talents-v1.bak angelegt. Ungültige, unbekannte oder überzogene Talentstände werden beim Laden abgewiesen.

- !talente oder !talents: Punkte und Übersicht
- !talente kämpfen / angeln / tauchen: Bereichsübersicht
- !talent <ID>: einen Rang lernen, zum Beispiel !talent combat-power
- !stats: zeigt zusätzlich verbleibende Talentpunkte

Vergaben verwenden ViewerPointStore.ApplyRpgAsync mit atomarer Prüfung/Speicherung und dem bestehenden persistenten CompletedActions-Beleg. Der Remote-Aufruf learnTalent verlangt talentId, expectedRank und catalogVersion; Identität und Kanal stammen aus dem bestehenden authentifizierten Aktionskanal. Ein veralteter erwarteter Rang wird abgewiesen. Ein bestätigter Aufruf liefert den bereits gespeicherten Talentstand direkt zurück.

## Darstellung

Sieben klickbare Talente pro Bereich, weiterhin drei umschaltbare Bäume. Goldene Medaillons und Rahmen liegen über der aus dem vorbereiteten Bild abgeleiteten Kampf-, Hafen- und Unterwasserkulisse. Keine eingebrannten Talenttexte im Hintergrund. Verbleibende Punkte bleiben oberhalb der Bereiche sichtbar. Details enthalten vollständige Wirkung und Voraussetzungen.

## Bereitstellung und Kompatibilität

Die Änderungen sind implementiert und als Plugin-Testpaket gebaut. Das laufende Plugin und seine Spielstände wurden nicht ersetzt.

Für die vollständige Twitch-Verbindung sind **beide Cloud-Dienste und das Plugin** zu aktualisieren:

1. Backend mit aktualisiertem SnapshotProtocol bereitstellen, damit learnTalent als gültige Aktion angenommen wird.
2. SnapshotPublisher mit Projektion 4 bereitstellen. Er unterstützt weiterhin Projektion 2 und 3 für alte Plugins. Vor jeder Cloud-Revision/Traffic-Umschaltung die verbindliche Prüfung aus Sync/DEPLOYMENT_SAFETY.md ausführen und die bestehende Konfiguration beibehalten.
3. Neues Plugin starten; der vorhandene Capability-Check verlangt Projektion 4 und verhindert, dass alte Publisher Talentränge stillschweigend verwerfen.
4. Aktualisiertes 0.0.3-Frontend verwenden. Ohne passenden Katalog, Talentränge und frische Plugin-Verbindung bleibt das Verteilen gesperrt. Bestehende Cloudprofile bekommen Talentränge mit dem nächsten vollständigen Plugin-Snapshot; keine manuelle Änderung von Perlen oder Leveln nötig.

Alte Plugin-Versionen kennen TalentRanks nicht. Nach ersten Talentvergaben nicht mit einem alten Plugin dieselbe Kontodatei überschreiben. Einen Rückwechsel nur mit passender vollständiger Sicherung durchführen. Im lokalen Demo-Modus dient der Katalog der Vergabe-/Darstellungsvorschau; die vollständige maßgebliche Boniberechnung ist im Plugin implementiert.

## Prüfung

- 867 Plugin-Tests erfolgreich, darunter 23 neue Tests für Talentfunktion und Remote-Aktion.
- 245 Backend-/Publisher-Tests erfolgreich.
- 104 Frontend-Tests erfolgreich.
- Vier Browserprüfungen erfolgreich: 320/390/1000 Pixel sowie bestätigte Vergabe, Punkteaktualisierung und erneutes Laden.
- Produktions-, Demo- und Hosted-Test-Build erfolgreich. Hosted-Test-Paket verwendet weiterhin ausschließlich das konfigurierte rc1-Backend.
