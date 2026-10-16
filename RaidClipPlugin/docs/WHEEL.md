# Eigenständiges Glücksrad

Unter **Giveaways → Glücksrad** stehen eigene Text-/Aktionsfelder zur Verfügung. Diese sind unabhängig von Giveaway-Teilnehmern, Gewinnern und Zuschauerpunkten. Jeder Eintrag ist ein gleich wahrscheinliches Feld. Eine Aktion wird nur als Ergebnis angezeigt, nicht ausgeführt.

1. Pro Zeile ein Feld eintragen (2–100 Felder, je maximal 80 Zeichen).
2. Drehdauer von 3 bis 30 Sekunden einstellen.
3. Optional über **Design auswählen** eine lokale PNG-/JPG-Datei wählen, maximal 10 MB und 8192 × 8192 Pixel. Das Bild wird mittig kreisförmig beschnitten und dreht sich mit. Feldgrenzen und Texte werden darüber dargestellt. **Standarddesign** entfernt die Auswahl.
4. **Glücksrad speichern** speichert Felder, Dauer und Bildpfad ohne Änderung des Giveaways. Die Bilddatei muss am ausgewählten Ort erhalten bleiben.
5. **Browserquelle starten** oder **Glücksrad drehen** startet die lokale Quelle. Twitch-Verbindung und aktiviertes Giveaway sind nicht erforderlich.
6. In OBS eine Browserquelle mit `http://127.0.0.1:18770/` und Größe **740 × 850** anlegen. Der Hintergrund ist transparent.

**Glücksrad drehen** verwendet die aktuellen, auch ungespeicherten Felder und das ausgewählte Design. Das Plugin ermittelt das Ergebnis; die Animation landet auf diesem Feld. Ein bereits drehendes Rad kann nicht erneut gestartet oder gelöscht werden. Nach dem Ende bleibt das Ergebnis sichtbar, bis **Anzeige leeren** oder eine neue Drehung gewählt wird.

Die Quelle ist ausschließlich auf dem eigenen Rechner erreichbar und nur lesend. Sie kann keine Drehung auslösen. Das Bild wird lokal aus dem beim Drehen geladenen Speicher ausgeliefert, nicht zu einem externen Dienst hochgeladen. Sein ursprünglicher Dateipfad wird nicht im Browserstatus angezeigt. Während einer laufenden Drehung vorgenommene Einstellungen gelten erst beim nächsten Drehen. Programmende beendet die Quelle; das letzte Ergebnis wird nicht dauerhaft gespeichert.
