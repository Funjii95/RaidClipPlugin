# Funjii Otter RPG 0.0.3 – lokale Entwicklung

Status: lokale Entwicklung, nicht hochgeladen oder veröffentlicht.
Basis: main / Plugin 2.1.29, Commit a9043ee32ee3e474fab19e67ed69d1ffef9d1cd1.
0.0.2 bleibt unverändert in der Twitch-Prüfung.

## Geplante Erweiterung: Ausrüstungs-Upgrades
Autoritative Umsetzung im vorhandenen Plugin, gemeinsame Spielstände für Chat und Extension. Keine zweite Spielökonomie in Frontend oder Cloud.

Vor Umsetzung festzulegen:
- maximale Upgrade-Stufe;
- Kosten je Stufe und mögliche Abhängigkeit von Seltenheit;
- welche Attribute wie stark steigen;
- garantierter Erfolg oder Fehlschlag, einschließlich Folgen eines Fehlschlags.

Bis diese Regeln feststehen, keine Upgrade-Aktion aktivieren, keine Perlen abbuchen und keine gespeicherten Items migrieren.

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
