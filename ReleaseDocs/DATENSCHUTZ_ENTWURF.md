# Datenschutzhinweis Funjii Otter RPG – unveröffentlichter Entwurf

Dieser Entwurf beschreibt den aktuellen technischen Stand. Vor Veröffentlichung müssen Betreiber-/Verantwortlichenangaben, Zweck/Rechtsgrundlage, Aufbewahrungsfristen, Löschprozess und Vertragsgrundlagen geprüft und ergänzt werden. Er ist noch keine fertige öffentliche Datenschutzerklärung.

## Verantwortlicher und Kontakt

Anbieter/Verantwortlicher: [vollständiger bestätigter Name bzw. Organisation und erforderliche Kontaktdaten ergänzen]. Kontakt für Datenschutz und Support: funjii95@gmail.com.

## Welche Daten verarbeitet werden

Nach deiner Twitch-Identitätsfreigabe wird deine signierte Twitch-Nutzeridentität mit deinem eigenen RPG-Profil verknüpft. Verarbeitet werden Twitch-Nutzer-/Kanalzuordnung, Anzeigename und vorhandene Spieldaten, beispielsweise Perlen, Inventar, Ausrüstung, Perks, Fische, Sushi, Buffs und Cooldowns. Aktions-IDs, Parameter, Sitzungen, Revisionen und Ergebnisbelege dienen der sicheren Zustellung und dem Schutz vor doppelten Buchungen. Es werden keine Twitch-Passwörter benötigt.

Die ursprüngliche Spielverwaltung liegt beim RaidClipPlugin des Betreibers. Geschützte Dienste auf Google Cloud Run vermitteln Anfragen; Cloud Firestore speichert veröffentlichte RPG-Snapshots und Aktionsbelege. Der Browser darf keine fremden Konten auswählen. Aktionsausführung setzt ein verbundenes, spielbereites Plugin voraus. Ein laufender Livestream ist nicht erforderlich.

Im Browser bleiben Authentifizierungstoken nur im Arbeitsspeicher. Im sitzungsbezogenen Speicher wird eine eigene offene Aktion mit ID/Parametern/Sitzung zur Wiederaufnahme gehalten, kein Schlüssel. Technisch erforderliche Verbindungsdaten wie IP-Adresse, Zeitpunkt und Fehler-/Zugriffsinformationen können bei Twitch bzw. Google entstehen. Kein Analytics- oder Werbe-SDK wird von diesem Frontend eingebunden.

## Zwecke, Rechtsgrundlage und Freiwilligkeit

Zwecke sind die Zuordnung zum eigenen Spielkonto, Darstellung des Spielstands, Abwicklung gewünschter Spielaktionen, Synchronisierung und Missbrauchs-/Fehlervermeidung. [Passende Rechtsgrundlagen durch den tatsächlichen Verantwortlichen bestätigen und erläutern.] Ohne Identitätsfreigabe können persönliche RPG-Daten und Aktionen nicht sicher zugeordnet werden. Die Freigabe und die Nutzung des Spiels sind freiwillig.

## Empfänger und Speicherorte

Twitch stellt die Extension-Plattform und Identitätsfreigabe bereit. Google betreibt die vom Anbieter verwendeten Cloud-Dienste. Aktuell: Cloud Run europe-west3 und bestehende Firestore-Datenbank eur3. Die Verbindung und Anbieterzugriffe können weitere Infrastruktur betreffen. [Auftragsverarbeitungsverträge, Unterauftragnehmer, mögliche Drittlandübermittlungen und ggf. Garantien prüfen; keine pauschale Zusicherung ausschließlicher EU-Verarbeitung abgeben.]

## Aufbewahrung und Löschung

Es gibt derzeit keine abschließend konfigurierte automatische Löschfrist für RPG-Snapshots und Aktionsbelege. [Konkrete Fristen für Spielstände, Belege, lokale Backups und Betriebsprotokolle festlegen und technisch umsetzen.] Sicherungs-/Wiederholungsbelege dürfen nicht unkoordiniert entfernt werden, weil dies Schutz vor Wiederholungen und Datenverlust beeinflusst. Löschanfragen bitte an funjii95@gmail.com richten; ein bestätigter, konsistenter Prozess für Plugin, Cloud, Tombstones und Backups muss vor öffentlichem Betrieb festgelegt werden. Keine bereits funktionierende Selbstbedienungs-Löschfunktion behaupten.

## Rechte

Je nach anwendbarer Rechtsgrundlage bestehen insbesondere Rechte auf Auskunft, Berichtigung, Löschung, Einschränkung, Datenübertragbarkeit und Widerspruch sowie ggf. Widerruf einer Einwilligung. Es besteht ein Beschwerderecht bei einer zuständigen Datenschutzaufsichtsbehörde. [Zuständige Stelle und konkrete Ausübung ergänzen.]

Prüfgrundlage: [DSGVO, insbesondere Informationspflichten](https://eur-lex.europa.eu/eli/reg/2016/679/oj/eng) und [Twitch-Hinweis zu Identity Linking und Privacy Policy](https://discuss.dev.twitch.com/t/policy-updates-regarding-mobile-enabled-extensions-on-ios/59518). Erst nach bestätigter Ergänzung auf einer öffentlichen HTTPS-URL veröffentlichen und diese in der Developer Console hinterlegen.
