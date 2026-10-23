# Gezeiten-Erweiterung: Windows 0.2.0 / Plugin 2.2.0

Lokaler Updatekandidat. Kein automatisches Deployment, keine GitHub-Veröffentlichung, keine Änderung der laufenden Installation.

## Vor der Installation

Plugin 2.2.0 benötigt einen Snapshot-Publisher, der RPG-Projektion **5** bestätigt. Projektion 5 erhält die fünf neuen Attributfelder und die Schildwerte. Der alte Publisher unterstützt nur bis Version 4; mit ihm stoppt das neue Plugin den Cloud-Sender absichtlich, damit keine Werte verloren gehen. Das bestehende Plugin 2.1.47 bleibt installiert und verbunden.

Die lokale Ausgabe `artifacts/tides-update/Cloud` enthält passende Linux-Builds für Backend und Publisher. Erst diese Cloud-Versionen prüfen und gemeinsam freigeben, dann Plugin und Windows-App verteilen. Vor dem Ersetzen des Plugins regulär beenden und die übliche Sicherung der lokalen Daten erstellen. Die Pakete dieses Kandidaten wurden noch nicht gegen den laufenden Cloud-Dienst installiert oder mit einem echten Spieler verwendet.

## Inhalte und Daten

Alle 66 IDs, Namen, Seltenheiten und Slots: [ITEMS.md](../expansions/tides/ITEMS.md). Maschinenlesbare Definitionen einschließlich Statbereichen und sechs echten Sets: [catalog.json](../expansions/tides/catalog.json).

Neue Attribute: `Armor` (Rüstungspunkte), `ArmorPercent`, `MaximumHpPercent`, `AttackSpeed`, `HealingPower` (Prozentpunkte). Rüstung reduziert erhaltenen Schaden mit `100 / (100 + effektive Rüstung)`. Angriffstempo verkürzt den autoritativen Boss-Angriffscooldown durch Division durch `1 + Tempo/100`, mindestens eine Sekunde. Das bestehende Attribut `Damage` bleibt der vorhandene prozentuale Schadensbonus; es wird kein zweites Angriffssystem eingeführt. Rare-Basiswerte liegen zwischen Uncommon und Epic. Die angegebenen Slotbereiche sind die finalen Würfelbereiche; kein zusätzlicher globaler Seltenheitsmultiplikator verändert alte Items.

`RpgItemGeneration.BaseAttributes` enthält garantierte Basiswürfe, `MinimumAttributes` / `MaximumAttributes` begrenzen die Anzahl zusätzlich ausgewürfelter Attribute. Starke Zusatzattribute (Doppelangriff und Boss-Schadensreduktion) haben eigene, kleinere Bereiche. Gewicht und Bereiche sind im vorhandenen Plugin-Looteditor je Definition editierbar. Die übergeordneten Loot- und Seltenheitswahrscheinlichkeiten bleiben erhalten; die Chance auf ein einzelnes altes Item innerhalb seiner Seltenheit verteilt sich durch die größere Auswahl entsprechend neu.

Ein Fund nutzt weiterhin eine neue `InstanceId`, speichert seine endgültigen `Attributes` und entfernt `Generation`. Laden, Cloud-Import und Neustart würfeln nicht neu. `CoreStatsVersion=1` verhindert zusätzliche automatische Basiswerte auf neuen Items. Vorhandene Instanzen werden nicht durch neue Definitionen ersetzt. Zerlegen nutzt das bestehende Perlen-System.

Nur 24 Gegenstände besitzen `SetId`: die 18 legendären Setteile und die sechs Schmuckstücke. Die übrigen 42 haben keine Seteffekte. Beide Ringslots verwenden weiterhin die bestehende Ring1/Ring2-Zuordnung; Setfortschritt zählt unterschiedliche Item-IDs.

## Kampf und Darstellung

Gezeitenwelle und Schattenflosse werden ausschließlich vom Plugin nach dem primären Angriff berechnet. Sie erzeugen keine weiteren Krit-, Doppelangriffs- oder Proc-Ketten. Cooldowns und zeitlich begrenzte Boni liegen im bereits persistenten `Cooldowns`-Feld. `Health.SetShieldHp` und `Health.SetShieldSource` speichern einen gemeinsamen, nicht addierten Schild. Ablegen entfernt zugehörige Zustände und Boni. Heilungswirkung gilt für Setheilung, passive Heilung, Chatheilung und Boss-Lebensraub. Die bestehenden Schutzgrenzen und Todessperren bleiben bestehen.

Zeitbasierte Setheilung und Schilde laufen im vorhandenen Sekundentakt des verbundenen Plugins, auch ohne geöffnete Windows-App. Ein gemeinsamer atomarer Speichervorgang übernimmt nur tatsächlich geänderte Spielstände; es wird kein dauerhaftes Aktionstoken pro Sekunde angesammelt. Die Oberfläche erzeugt selbst keine Heilungen. Gezeiten-Regeneration rechnet höchstens die tatsächlich abgelaufenen acht Sekunden nach. Orakelheilung und Mondflut gewähren nach einer Offlinephase höchstens einen fälligen Impuls und keine gestapelten Offline-Impulse. Wiederholte Verarbeitung desselben Zeitpunkts gewährt keinen weiteren Impuls.

Die Windows-Oberfläche zeigt neue Werte, gespeicherte Würfe, Vergleiche, echte Setbeschreibungen und Schildpunkte. Dieselben Änderungen sind für den gemeinsamen Twitch-Oberflächencode vorbereitet; das installierte Twitch-Paket wird nicht automatisch ersetzt.

Es fehlen endgültige Einzelillustrationen. Alle 66 Items haben eine eigene lokale, beschriftete SVG-Platzhalterdatei mit passender Seltenheitsfarbe und Slotform. Die gelieferte Übersicht wurde nicht als Sammelicon verwendet.

## Windows-Adminbereich

Der Adminbereich erscheint für den mit Twitch angemeldeten Kanalinhaber (auf dem vorhandenen Kanal Twitch-ID `42812664`). Benutzername, Moderatorrolle oder eine lokale Checkbox gewähren keine Rechte. Backend und Plugin vergleichen den authentifizierten Benutzer unabhängig mit der konfigurierten Kanal-ID. Die bestehenden Sitzungs-, Freigabe- und Verbindungsprüfungen gelten weiterhin.

Erlaubte Adminaufträge: `adminCatalog` (Ausrüstungsliste), `adminStart` (ausschließlich Boss, Mimic, Rennen, Heist, Hot-Time), `adminGrant` (vorhandene Ausrüstungsdefinition als neue Instanz erzeugen). Events nutzen die vorhandenen Spielhandler, Aktivierungen, Einsätze und Rundensperren; ein aktives Event wird nicht überschrieben. Heist benötigt den konfigurierten Heist-Dienst. Administrative Hot-Time wird als Adminstart protokolliert und kündigt keinen erfundenen Boss-Sieg an.

Items können zufällig, mit Minimal- oder Maximalwürfen oder mit eigenen Attributen erzeugt werden. Eigene Werte ersetzen die Attribute der neuen Instanz; nicht gesetzte Werte sind 0. Setzuordnung und Slot stammen weiterhin aus der gewählten Definition. Pro Auftrag sind 1–20 neue Instanzen erlaubt, Empfänger ist eine Twitch-ID mit bereits vorhandenem lokalem Spielstand. Perlen werden nicht verändert. Zahlenbereiche werden im Plugin geprüft; die bestehenden Kampf-Caps bleiben wirksam.

Adminmutationen verwenden denselben dauerhaften Aktionsbeleg wie andere Spielaktionen. Aktions-ID, ausführende Twitch-ID, Empfänger und erzeugte Instanz-IDs bleiben im lokalen Beleg. Verbindungsabbrüche führen zu einer Prüfung derselben Aktions-ID; ein unklarer Ausgang wird nicht blind als neuer Auftrag ausgeführt. Der Admin-Katalog ist ein reiner Leseauftrag. Keine Adminaktionen wurden auf dem laufenden Kanal ausgeführt.

## Build und Quellcode

`Build-ItemExpansion.ps1` exportiert Katalog und Platzhalter, baut den Windows-Installer samt Updatepaket, das eigenständige Plugin und beide Cloud-Komponenten. Kein Schritt lädt etwas hoch oder installiert etwas. Das lokale Plugin-Quellverzeichnis basiert auf der reparierten Version 2.1.47 und ist weiterhin vom Desktop-Git-Repository ausgeschlossen. Seine geänderten Quelldateien werden separat als Overlay im Erweiterungsordner mitgeführt; dazu gehört die gemeinsame `Sync`-Änderung im Desktop-Repository. `ItemExpansion.Tools` benötigt dieses lokale Plugin-Verzeichnis.

Tests und vollständige Änderungsliste werden im Abschlussbericht `expansions/tides/VALIDATION.md` dokumentiert. Dies ersetzt keinen Live-Test nach einer separat beauftragten Cloud-Freigabe.
