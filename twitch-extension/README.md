# Grafische Twitch Extension mit Backend

Stand 18.10.2026. Die ursprüngliche Oberfläche, CSS, Navigation, Animationen und 102 Itemgrafiken bleiben erhalten. `src/main.tsx` startet in normalen Builds ausschließlich `BackendRpgDataSource` mit dem Twitch-Helper. Keine Demo-Perlen, keine lokale Spielengine und kein Mock-Fallback bei Fehlern.

Die aktuelle Spielregel lautet: Plugin gestartet, authentifiziert verbunden, RPG aktiviert und spielbereit, Heartbeat frisch. Der Twitch-Stream darf offline sein. Ob Twitch das Panel bzw. die mobile Extension außerhalb eines Streams anzeigt, bestimmt Twitch.

## Bauen und prüfen

```powershell
pnpm install --frozen-lockfile
pnpm build
pnpm test
pnpm build:demo
pnpm exec playwright install chromium
pnpm test:e2e
```

`dist` ist das normale Hosting-Paket mit `panel.html`, `mobile.html`, `config.html` und Assets. Ausschließlich den Inhalt dieses Ordners für Hosted Test zippen. `dist-demo` ist ein ausdrücklich getrennter Entwicklungsbuild. Der lokale HTTPS-Server zeigt ihn unter `/dev-demo/panel.html`; URL-Parameter wie `?demo=1` aktivieren im normalen Paket niemals Mockdaten.

## Verbindung und Daten

Die öffentliche Backend-URL steht im bestehenden `src/live/client.ts`. Sie ist kein Secret. Twitch liefert Token und Kanal über `Twitch.ext.onAuthorized`; das Backend ermittelt den Benutzer ausschließlich aus dem signierten JWT. Keine privaten Zugangsdaten im Frontend, keine Tokens in Browser-Persistenz. Session Storage hält ausschließlich die eigene offene Aktions-ID samt Intent zur Wiederaufnahme derselben Anfrage.

`GET /api/v1/me` liefert das persistierte Profil; `GET /api/v1/play/state` liefert die Plugin-Freigabe und den tatsächlichen Katalog. Aktionen werden als eindeutig identifizierte Requests an `/api/v1/play/actions` gesendet. Kosten, Besitz, Zufall und Cooldowns prüft ausschließlich das normale Plugin. Nach Bestätigung lädt der Adapter den tatsächlichen Snapshot nach, ohne selbst Guthaben zu buchen. Nicht übermittelte Werte erscheinen als unbekannt statt als erfundene Zahlen.

Bei Plugin-Trennung bleiben Profile optional lesbar, Aktionen werden gesperrt. Eine unklare Aktion bleibt gesperrt und wird nur mit derselben ID geprüft. Die CSP des Testservers erlaubt ausschließlich die bestehende Backend-Domain als API-Ziel; der Twitch-Helper wird als erstes Skript geladen. Alle Spielbilder liegen im Paket.

Vollständige Anleitung, Grenzen, aktuelle Cloud-Voraussetzungen und betroffene Dateien: `../GRAPHICAL_BACKEND_INTEGRATION.md`. Aktuelle Testergebnisse: `../TESTERGEBNISSE_GRAPHICAL.md`. Ältere Demo-/Minigame-Berichte beschreiben nur den getrennten Entwicklungsmodus.

## Aktueller Twitch-Test 0.0.3

Das Plugin verbindet sich mit **rc1**. Für diesen gehosteten Test ausschließlich `pnpm build:hosted-test` verwenden und den Inhalt von `dist-hosted-test` hochladen. Der Build prüft die Backend-Adresse und enthält Panel, Mobilansicht und Konfiguration. `dist` und `dist-desktop` verwenden den normalen Backend-Endpunkt und gehören nicht in diesen Test. Sonst fehlen der vollständige Perk-Katalog, Sushi-Rezepte und Itemsets trotz sichtbarem Spielstand.
