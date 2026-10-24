# Twitch-Hosting aktualisieren

Am 24.10.2026 sind auf Kanal 42812664 die RPG-Panels 0.0.2 und 0.0.3 gleichzeitig aktiv. Ältere gehostete Bundles erkennen neue Seltenheiten nicht.

Das neue ZIP Twitch-Extension-0.0.4-compatibility.zip enthält panel.html, mobile.html und config.html samt lokalen Assets und Twitch-Helper. Es benötigt einen Upload unter der neuen Extension-Version in der Twitch-Entwicklerkonsole. Anschließend Hosted Test prüfen und nur die gewünschte Panelversion aktivieren. Die bestehende Twitch-Client-ID und Identitätsfreigabe bleiben erhalten. Das Paket enthält keine Zugangsdaten oder Demo-Spielstände.

Reproduzierbares Paket: nach pnpm build das Skript twitch-extension/scripts/package-hosting.py mit Python und Pillow aufrufen. Das Paket verkleinert Illustrationen proportional auf maximal 1024 Pixel und kodiert sie als WebP; die ursprünglichen Bilder bleiben unverändert. Alle HTML-/CSS-/JS-Assetpfade werden angepasst und alle gepackten WebP-Dateien geprüft.
