# Giveaways

Das Giveaway-Modul ist ein eigenständiger Bereich der RaidClip-Oberfläche. Es nutzt die bereits laufende Twitch-Chatverbindung und blockiert weder Raid-Clips noch Minigames.

## Bedienung

1. Unter **Giveaways** Titel, Gewinn, Command, Laufzeit und Teilnahmebedingungen einstellen.
2. **Giveaway speichern** klicken. Erst dieser Klick übernimmt die Änderungen dauerhaft.
3. Das Plugin starten und anschließend das Giveaway mit **Starten** öffnen.
4. Zuschauer nehmen mit `!giveaway` oder einem Alias teil. Bei aktivierten Zusatzlosen kann z. B. `!giveaway 3` verwendet werden.
5. Gewinner manuell oder nach Ablauf automatisch auslosen.

## Fairness und Sicherheit

- Gewinner werden mit `RandomNumberGenerator` kryptografisch zufällig gezogen.
- Doppelte Teilnahme, Bots, Broadcaster und frühere Gewinner lassen sich getrennt behandeln.
- Subscriber-, VIP- und Zusatzlos-Gewichtungen sind optional.
- Teilnahmegebühren werden atomar im vorhandenen Punktespeicher abgezogen und bei einem Abbruch optional zurückerstattet.

## Wiederherstellung

Der aktuelle Zustand liegt im Benutzerprofil unter `RaidClipPlugin/giveaways/current-giveaway.json`. Nach einem Neustart werden Laufzeit, Teilnehmer, Gewinner und Punktabzüge wiederhergestellt. Ein während der Pause abgelaufenes Giveaway wird entsprechend der Einstellung automatisch ausgelost oder beendet.

## Chatvorlagen

Format pro Zeile: `Name|1|Text`. `1` aktiviert und `0` deaktiviert die Nachricht. Verfügbar sind unter anderem `{username}`, `{title}`, `{prize}`, `{command}`, `{participantCount}`, `{winner}`, `{winners}` und `{remainingTime}`.
