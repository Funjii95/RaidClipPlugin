# Windows 0.3.1 / Plugin 2.3.1

Die Erweiterung aus 0.3.0 bleibt vollständig enthalten: 54 neue Ausrüstungsteile, elf Materialien, 81 zugeordnete Einzelbilder, sechs neue Sets, garantierte Aufwertungen bis +9, Endveredelung und Heal all.

## Korrekturen

Beim Speichern prüft das Plugin auch die bestehende Einstellungsdatei, bevor es sie durch die neue ersetzt. Die erste Erweiterung bewertete dabei eine gültige ältere Datei ohne die drei neuen Seltenheiten als ungültig. Dadurch blieb das Programm geöffnet, aber Start/Verbinden brach beim Speichern ab. Die Prüfung führt jetzt zuerst dieselbe additive Ergänzung wie der normale Ladevorgang aus. Vorhandene ursprüngliche Werte werden nicht ersetzt; fehlerhafte ursprüngliche Zerlegewerte bleiben ein Fehler.

Die lokal betroffene Datei wurde vor der Reparatur gesichert. Ergänzt wurden ausschließlich die fehlenden Zerlegewerte für True, Cursed und Abyss. Danach wurde wieder ein erfolgreicher Speichervorgang beim Start und ein frischer Cloud-Heartbeat mit elf Materialien, sechs neuen Sets sowie Upgrade-, Veredelungs- und Heal-all-Freigaben festgestellt.

Die Funktion zum Aktivieren zufälliger Itemwerte schließt jetzt auch die fest entworfenen neuen Fluch- und Schmuckdefinitionen aus. Ihre Vorteile und Nachteile werden nicht durch generische Zufallswerte ersetzt.

## Adminpanel

Der autoritative Adminkatalog enthält beide Erweiterungen einschließlich aller 54 neuen Ausrüstungsteile. Die Liste ist nach Seltenheit absteigend und danach nach Name sortiert. Windows ergänzt Bildvorschau, Suche nach Name oder ID, Seltenheitsfilter und eine ausdrückliche Katalog-Aktualisierung. Eine noch vorhandene Auswahl bleibt beim Aktualisieren erhalten. Eine durch Filter ausgeblendete Auswahl kann nicht versehentlich vergeben werden.

## Prüfung

1.013 Plugin-Tests, 268 Backend-/Publisher-Tests, 116 gemeinsame Oberflächentests und sieben Updater-Tests bestanden. Die beiden zusätzlichen Einstellungsdatei-Tests speichern tatsächlich über eine synthetische alte Datei, prüfen die erhaltenen benutzerdefinierten Werte und Sicherung und verlangen, dass beschädigte alte Werte weiterhin unverändert abgelehnt werden. Alle Tests verwenden isolierte Datenpfade.

Die Cloud-Projektion 6 aus dem vorherigen Release bleibt kompatibel; für diese Korrekturen ist keine weitere Cloud-Umstellung nötig. Produktive Itemvergaben, Bossstarts und Heal-all-Aktionen wurden zur Prüfung nicht ausgeführt.
