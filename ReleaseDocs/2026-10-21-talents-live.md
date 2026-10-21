# Live-Bereitstellung: Perks und Talente

Am 21.10.2026 wurden Backend und Publisher an den bestehenden rc1-Adressen aktualisiert.

- Backend-Revision: funjii-otter-rpg-backend-test-talents-bd6310a
- Backend-Image: sha256:ac2903d6bbcf9718c0f3153b5e56253a03b51774776f19c39247699c4dcddb0e
- Publisher-Revision: funjii-otter-rpg-publisher-test-talents-bd6310a
- Quellstand: bd6310a (TalentRanks in der Backend-Profilantwort erhalten)
- Publisher unterstützt jetzt Datenversionen 2, 3 und 4.
- Vollständige Konfigurationsprüfung vor Revisionen und Umschaltungen bestanden. Einstellungen und Laufzeitidentitäten übernommen; Rückkehrkonfiguration lokal DPAPI-geschützt.
- 247 Backend-/Publisher-Tests bestanden.
- Authentifizierte Live-Abfrage bestätigt: Level 4, 3 verfügbare Talentpunkte.
- Nach vollständigem Plugin-Neustart bestätigt: frischer Katalog, 31 Perks mit jeweils maximal 5 Rängen, 21 Talente, learnTalent freigegeben, Verbindungsstatus ready.
- Keine Testkäufe oder Änderungen am Spielerfortschritt ausgeführt.

Beim Wechsel von einem nicht unterstützten Publisher-Vertrag muss das gesamte Plugin neu starten; ein Modulneustart startet den bereits angehaltenen Cloud-Sender nicht neu. Die Twitch-Kanalseite anschließend neu laden, falls noch ein alter Katalog sichtbar ist.
