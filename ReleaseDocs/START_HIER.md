# Funjii Otter RPG – Desktop-Panel 0.0.1-rc1

Stand 18.10.2026. Entscheidung des Nutzers: zuerst ausschließlich Desktop-Panel, Höhe 500 Pixel. Mobil bleibt im Quellcode erhalten und wird nicht eingereicht. Die Spielregel bleibt: authentifiziert verbundenes, aktiviertes und spielbereites Plugin mit frischem Heartbeat; der Stream darf offline sein.

## Aktueller Stand

Das Desktop-ZIP ist uploadfertig aufgebaut. Der neue Windows-Testbuild ist separat. Noch kein neuer Container wurde hochgeladen oder deployed, kein Twitch-Upload durchgeführt und keine öffentliche Freigabe erteilt. Der vorhandene Twitch-Status „Gehosteter Test“ bezieht sich auf den alten Upload, nicht auf diesen Kandidaten. Der tatsächliche Cloud-/Twitch-Gesamttest dieses Kandidaten steht aus.

## Die nächsten Schritte

1. PRUEFBERICHT.md und BLOCKER.md lesen. Die Kernfunktionen haben automatisierte Nachweise; unter anderem Bossaktionen und weitere sichere Aufrufwege fehlen weiterhin.
2. Den genau beschriebenen synthetischen Staging-Plan in DEPLOYMENT.md ausdrücklich freigeben. Er betrifft zwei neue getaggte Revisionen, neue begrenzte Berechtigungen und einen geschützten Zugang. Der bisherige Verkehr bleibt auf den alten Revisionen.
3. Nach Freigabe: Images hochladen, Rechte einrichten, beide Staging-Revisionen bereitstellen und den synthetischen Ablauf in E2E_PROTOKOLL.md durchführen. Keine vorhandenen Benutzerprofile zurücksetzen.
4. Erst nach erfolgreicher Staging-Prüfung gesondert die Konfiguration für einen echten freigegebenen Twitch-Testnutzer und die Verwendung der normalen Dienst-URLs freigeben. Diese spätere Umstellung ist nicht vom ersten Plan abgedeckt.
5. In der bestehenden Twitch-Version 0.0.1 zum lokalen Test wechseln. Panel aktiv lassen (panel.html, 500 px), Mobil deaktivieren, config.html beibehalten. Die bestehenden Dateien nicht löschen; vor Austausch den alten Upload/dessen Prüfsumme sichern. Erst nach ausdrücklicher Upload-Freigabe Desktop-HostedTest.zip hochladen und wieder in Hosted Test wechseln.
6. Im Erweiterungsmanager auf Funjii als Panel aktivieren. Nur erlaubte Testnutzer hinzufügen, Twitch-Identität freigeben, echte Profildaten vergleichen, Plugin an/aus und Stream offline prüfen, eine autorisierte Testaktion durchführen und nach Neuladen den synchronisierten Zustand vergleichen.
7. Erst nach dokumentiertem Erfolg die Einreichungsblocker schließen und eine separate Prüfungseinreichung freigeben. „Hosted Test bestanden“, „von Twitch genehmigt“ und „öffentlich veröffentlicht“ sind drei unterschiedliche Zustände.

Windows-Testbuild: Windows-Test.zip entpacken und FunjiiOtterRpgIntegratedTest.exe ausführen. Version 2.1.22-cloudtest.3. Niemals über die normale 2.1.22-Installation kopieren. Die Vorbereitung des synthetischen Datenordners liegt ausschließlich lokal unter work/release-stage-isolated und ist nicht im ZIP enthalten. Keine Schlüssel oder Anmeldedaten in den Chat kopieren.

Die Einreichungsunterlagen sind vorbereitet, aber Datenschutz-/Bedingungs-URLs und Betreiberangaben fehlen noch. Öffentliche Support-Adresse: funjii95@gmail.com.
