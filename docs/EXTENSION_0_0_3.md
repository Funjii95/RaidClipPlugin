# Funjii Otter RPG 0.0.3 – lokale Entwicklung

Status: private Twitch-Testversion 0.0.3; Frontend hochgeladen, neue Server-/Plugin-Funktionen noch nicht bereitgestellt.
Basis: main / Plugin 2.1.29, Commit a9043ee32ee3e474fab19e67ed69d1ffef9d1cd1.
0.0.2 bleibt unverändert in der Twitch-Prüfung.

## Geplante Erweiterung: Ausrüstungs-Upgrades
Autoritative Umsetzung im vorhandenen Plugin, gemeinsame Spielstände für Chat und Extension. Keine zweite Spielökonomie in Frontend oder Cloud.

## Verbindliche Regeln
- Maximalstufe +9.
- Pro Stufe +2,5 % additiv auf den ursprünglichen Attributwert; kein Zinseszins.
- Faktor: 1 + Stufe * 0,025. Bei +9: 1,225 (+22,5 %).
- Beispiel: Basiswert 100 ergibt bei +1 102,5, bei +2 105 und bei +9 122,5.
- Upgrades gelingen immer; kein Zufallswurf.
- +7 benötigt 1 Weiße Perle, +8 benötigt 1 Blaue Perle, +9 benötigt 1 Rote Perle. +1 bis +6 benötigen kein Upgrade-Material.
- Perlenkosten je EINZELNEM Schritt auf die Zielstufe: +1 10.000; +2 25.000; +3 50.000; +4 100.000; +5 200.000; +6 350.000; +7 500.000; +8 750.000; +9 1.000.000. Kein Sammelpreis für alle Stufen. Keine Stufensprünge.
- Materialperlen stammen aus Muscheln. Konkrete Auswahl aufwertbarer Attribute/etwaige Rundung sind noch offen. Keine Dropchancen oder zusätzlichen Kosten erfinden.
- Die reine Dezimalberechnung ist lokal als RpgEquipmentUpgradeRules vorbereitet und getestet. Noch keine Einbindung in gespeicherte Gegenstände, Aktionen oder Anzeige.

Bis Materialverwaltung, sichere Transaktion und Datenprojektion integriert und geprüft sind: keine Upgrade-Aktion aktivieren, keine Materialien oder Perlen abbuchen, keine Items verändern oder migrieren. Vorhandene Grundwerte bleiben unverändert; spätere Berechnung muss immer von unveränderten ursprünglichen Werten ausgehen.

## Bestehende Integrationspunkte
- RaidClipPlugin/Models/RpgProfile.cs: RpgItem mit InstanceId, Attributes und CoreStatsVersion.
- RaidClipPlugin/Services/RpgProgressionService.cs: bestehende atomare RPG-Transaktionen über ViewerPointStore.ApplyRpgAsync.
- RaidClipPlugin/Services/RpgCoreStatService.cs: vorhandene Basismigration für Kraft/Ausdauer; ist kein kaufbares Upgrade-System und wird dafür nicht zweckentfremdet.
- RaidClipPlugin/Services/RemoteRpgExecutor.cs: autoritative Aktionsprüfung und persistente Idempotenz.
- Sync/src/SnapshotProtocol: Aktionsvertrag und vollständige Snapshot-Projektion.
- twitch-extension/src/types.ts und remote/BackendRpgDataSource.ts: bestehende UI-Verträge und authentifizierte Aktionen.
- twitch-extension/src/GraphicalApp.tsx: vorhandene Inventar-/Ausrüstungsansicht erweitern, keine neue Oberfläche bauen.

## Umsetzung nach Regelfestlegung
1. Rückwärtskompatible Speicherung der Upgrade-Stufe pro Item planen; Altgegenstände behalten ihre bisherigen Werte.
2. Upgrade innerhalb der bestehenden RPG-Transaktion: Eigentum, Ausrüstungstyp, Stufe und Kosten prüfen; Kosten und Ergebnis gemeinsam speichern.
3. Sichere Vorschau/Bestätigung und idempotente Aktion über den bestehenden Aktionskanal ergänzen.
4. Snapshot-Projektion und UI-Mapping um tatsächliche Upgrade-Daten erweitern; alte Extension-Version weiterhin unterstützen.
5. Bestehende Inventar-/Ausrüstungsansicht um Vorschau, Kosten und Bestätigung ergänzen.
6. Synthetische Tests: Doppelklick, Wiederholung, zu wenig Perlen, fremdes Item, Maximalstufe, gleichzeitige Chat-/Extension-Aktionen, Neustart, Altgegenstände und Synchronisation.

Kein Push, GitHub-Release, Cloud-Deployment oder Twitch-Upload ohne weiteren Auftrag.

## Neues Minispiel: Muscheln öffnen
Verbindliche Vorgaben:
- Eine Öffnung verbraucht genau eine Muschel.
- Mengen von 1 bis maximal 100 je Aktion; 10 Öffnungen kosten 10 Muscheln.
- Bei zu wenig Muscheln keine Teilöffnung oder Abbuchung.
- Muscheln kommen aus dem bisherigen Nieten-Ausgang beim Angeln (alter Stiefel); vorgesehen ist eine Muschel pro solchem Ausgang. Keine zusätzliche Dropchance erfinden.
- Bestehende Nietenchance, Einsätze, Cooldowns und Perlenauszahlungen bleiben erhalten.

Fundstellen:
- Services/AdventureGameService.cs: SoloOutcome liefert Tier 0 mit Text "Ein alter Stiefel – kein Fang!".
- Services/RpgAdventureService.cs: autoritative Fang-/Spielstandtransaktion über ApplyCasinoAsync; Muschelgewinn muss nach bestehender Aktions-/Cooldown-Prüfung in derselben Transaktion verbucht werden.
- Die gemeinsame Textfunktion darf nicht bloß "Muschel" behaupten, ohne dass tatsächlich eine Muschel gespeichert wurde. Der Legacy-Modus ohne RPG braucht eine ausdrückliche Behandlung.

Genehmigte Belohnungen pro Muschel: 3 % eine weiße Perle, 2 % eine blaue Perle, 1 % eine rote Perle, 47 % gleichverteilt 10–50 gewöhnliche Perlen (einschließlich Grenzen), 47 % ein Steinstückchen.

Implementiert: atomarer Muschelverbrauch und Belohnung in ViewerPointStore, persistente ActionId, Chatbefehl !muscheln [1–100], authentifizierte openShells-Aktion, vollständige Materialprojektion und vorhandene Minispielansicht mit Mengenwahl. Angeln vergibt eine Muschel bei Tier 0 innerhalb der bestehenden Transaktion. Legacy-Angeln ohne RPG bleibt unverändert.

Speicherung: RpgProfile.Materials mit fünf nichtnegativen Zählern. Alte Profile ohne Materials starten mit leeren Materialbeständen. Vor erster Speicherung entsteht eine .pre-materials-v1.bak. Unvollständige oder ungültige Materialobjekte werden abgelehnt. Keine Änderung bestehender Gegenstände oder Perlen beim bloßen Synchronisieren.

Kompatibilität: Snapshot-Projektion 3 erforderlich. Der derzeitige Publisher unterstützt Version 2; neue Plugin-Snapshots scheitern dort sicher. Vor realem Test passenden privaten Publisher und Backend sowie isolierte Plugin-Testversion bereitstellen. Alte Plugin-Versionen können unbekannte Materialfelder beim Speichern verlieren; nach Einführung nicht ohne geplante Wiederherstellung auf 2.1.29 zurückwechseln. Keine produktiven Spielstände dafür verwenden.

Das hochgeladene Frontend bleibt am bisherigen Backend angeschlossen. Ohne Materialdaten und openShells-Freigabe ist die neue Aktion gesperrt. Ausrüstungs-Upgrades bleiben reine Regelvorbereitung, noch kein Kaufweg.
