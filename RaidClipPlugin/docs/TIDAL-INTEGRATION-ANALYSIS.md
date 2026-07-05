# TIDAL-Integrationsanalyse für RaidClipPlugin 1.7.0

Stand: 5. Juli 2026

## Entscheidung

**Empfehlung B – TIDAL ist teilweise integrierbar.**

Eine sichere erste TIDAL-Integration kann offizielle Katalogsuche, Metadaten, Track-Links, Embeds und die bestehende lokale RaidClip-Warteschlange anbieten. Eine mit Spotify vergleichbare Steuerung des TIDAL-Desktopplayers, einer aktiven TIDAL-Queue oder von TIDAL-Connect-Geräten ist für ein allgemeines .NET-Windows-Tool derzeit nicht offiziell verfügbar. Diese Funktionen werden deshalb ausdrücklich nicht implementiert.

Verwendete offizielle Quellen:

- [TIDAL API & SDK Overview](https://developer.tidal.com/documentation/api-sdk/api-sdk-overview)
- [TIDAL Quick Start](https://developer.tidal.com/documentation/api-sdk/api-sdk-quick-start)
- [TIDAL App-Verwaltung](https://developer.tidal.com/documentation/api-sdk/api-sdk-manage-apps)
- [TIDAL Embeds](https://developer.tidal.com/documentation/embeds/embeds-overview)
- [TIDAL Connect](https://developer.tidal.com/documentation/connect)
- [TIDAL Design Guidelines](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines)

## 1. Gefundener Projektstand

### Spotify

Spotify ist bereits vollständig eingebaut, aber noch nicht über eine allgemeine Musikdienst-Schnittstelle abstrahiert.

- `Services/SpotifyService.cs`: OAuth Authorization Code mit PKCE, Callback über lokalen `HttpListener`, automatische Token-Erneuerung, Track-Suche, Track-Auflösung, aktueller Titel, Geräte, Queue, Sofortwiedergabe, Skip, Pause, Fortsetzen und Gerätewechsel.
- `Services/MusicRequestService.cs`: Twitch-Chat- und Kanalpunkt-Wünsche, Validierung, Blacklists, Cooldowns, Duplikate, Queue-Limits, Spotify-Linkauflösung, Playback und Chatantworten.
- `Services/MusicRequestEventSubService.cs`: Kanalpunkt-Einlösungen über Twitch EventSub.
- `Services/MusicRequestStore.cs`: dauerhafte lokale Historie und Queue-Zustände.
- `Services/MusicRequestRules.cs`: reine Prüfregeln für Nutzer, Titel, Dauer, Explicit-Inhalte und Limits.
- `MainForm.MusicRequests.cs`: Spotify-Login, Gerätewahl, Belohnung, Commands, Regeln, Queue und Moderationsaktionen.
- `Config/MusicRequestConfig.cs`: Spotify Client ID, Redirect URI, Wiedergabemodus und Songrequest-Regeln.

### OAuth und Sicherheit

- Spotify verwendet PKCE und benötigt in der Desktop-App kein Client Secret.
- Access- und Refresh-Token werden unter `%LocalAppData%/RaidClipPlugin/spotify-token.dat` gespeichert.
- Die Datei wird über `WindowsProtectedStore` und damit Windows DPAPI geschützt.
- Abgelaufene Access Tokens werden mit dem Refresh Token erneuert.
- Tokens und Secrets werden nicht geloggt.

### Aktuelle Kopplung

`MusicRequestService` hängt direkt von `SpotifyService` und `SpotifyTrack` ab. Provider-Auswahl, providerneutrale Suchergebnisse und Capability-Prüfungen fehlten bisher. 1.7.0 ergänzt hierfür die sichere, noch nicht aktiv umgeschaltete Grundlage, ohne den stabilen Spotify-Ablauf zu verändern.

## 2. Offizielle TIDAL-Möglichkeiten

- TIDAL stellt eine öffentliche REST-API für Katalogsuche und Metadaten bereit.
- Eigene Apps erhalten im Developer Dashboard Client ID und Client Secret.
- Offizielle Authentifizierung ist über TIDAL Auth beziehungsweise OAuth-Client-Credentials möglich.
- Tracks, Alben, Künstler und Playlists können offiziell abgefragt werden; die konkreten Felder und Länder-Verfügbarkeit hängen vom API-Endpunkt und `countryCode` ab.
- Trackseiten und TIDAL-Links können ausgegeben werden.
- TIDAL bietet offizielle Embeds und oEmbed für unterstützte TIDAL-Seiten.
- Der Player des offiziellen SDK ist laut TIDAL der einzige zulässige Weg für Playback in Drittanbieter-Apps. Öffentlich dokumentiert sind Web-, Android- und iOS-SDKs, kein natives C#/.NET-Windows-SDK.
- TIDAL Connect wird laut offizieller Dokumentation nur für Gerätepartner unterstützt.
- Eine öffentliche API zum Steuern der Queue oder des bestehenden TIDAL-Desktopclients ist nicht dokumentiert.
- Inoffizielle Endpunkte, fremde Client-Tokens, Scraping und simulierte Tastatureingaben werden nicht verwendet.

## 3. Funktionsmatrix

| Funktion | Spotify möglich? | TIDAL offiziell möglich? | TIDAL eingeschränkt? | Risiko/Einschränkung | Empfehlung |
|---|---:|---:|---:|---|---|
| Song per Text suchen | Ja | Ja | Nein | Country Code und Katalogverfügbarkeit beachten | Phase 3 |
| Track-Metadaten | Ja | Ja | Nein | TIDAL-Attribution erforderlich | Phase 3 |
| Künstler/Album | Ja | Ja | Nein | API-Felder providerneutral abbilden | Phase 3 |
| Playlist abrufen | Ja | Ja | Teilweise | Nur benötigte öffentliche Daten/Scopes verwenden | Später |
| Song-Link erzeugen | Ja | Ja | Nein | Link muss zu TIDAL zurückführen | Phase 3 |
| Embed-Link erzeugen | Ja | Ja | Teilweise | Unterstützte Seiten und Embed-Bedingungen beachten | Phase 3 |
| Interne Plugin-Queue | Ja | Ja | Nein | RaidClip verwaltet nur Metadaten/Status | Phase 4 |
| Song automatisch abspielen | Ja | Nur über offizielles SDK/Embed | Ja | Kein natives .NET-SDK; keine Desktop-Remote-API | Nicht in Phase 1–4 |
| Aktiver Queue hinzufügen | Ja | Nein | Nein | Keine öffentliche Desktop-/Queue-Steuerung | Nicht implementieren |
| Pause/Fortsetzen | Ja | Nicht für bestehenden Desktopclient | Ja | Nur innerhalb eines erlaubten SDK-Players | Nicht implementieren |
| Aktuelles Gerät erkennen | Ja | Nein | Nein | Keine öffentliche Geräte-API | Nicht implementieren |
| TIDAL Connect steuern | Nein/Nicht relevant | Nur Gerätepartner | Ja | Partnerstatus erforderlich | Nicht implementieren |
| OAuth speichern/refreshen | Ja | Ja | Nein | Secret und Tokens DPAPI-geschützt speichern | Phase 2 |
| Windows Desktop Tool | Ja | API ja | Playback eingeschränkt | REST funktioniert; offizieller Player nur Web/iOS/Android | Katalog + Links |
| Ohne TIDAL-Partnerstatus | Ja | Katalog/Embeds ja | Ja | Connect und vergleichbare Gerätesteuerung fehlen | Minimalintegration |

## 4. Architektur für die nächste Phase

1. `IMusicProvider` definiert Providername, Authentifizierung, Suche und das Hinzufügen eines Wunsches.
2. `MusicProviderType` unterscheidet `Spotify`, `Tidal`, `InternalQueue` und `Disabled`.
3. `MusicProviderCapabilities` verhindert, dass die GUI oder der Command-Service nicht unterstützte Funktionen anbietet.
4. `SongSearchResult` ist das gemeinsame Track-Modell.
5. Ein späterer `SpotifyMusicProvider` adaptiert den bestehenden `SpotifyService`, ohne dessen funktionierenden OAuth- und Playback-Code neu zu schreiben.
6. Ein späterer `TidalMusicProvider` darf ausschließlich offizielle TIDAL-Endpunkte verwenden und zunächst nur Suche, Metadaten, Links, Embeds und lokale Queue liefern.
7. Ein `SongRequestManager` wählt den Provider anhand der Einstellung und prüft vor jeder Aktion dessen Capabilities.

## 5. Sichere TIDAL-Minimalintegration

- Eigene TIDAL-App im Developer Dashboard.
- Client ID und Client Secret nie in `config.json` oder Logs.
- Secret und Token über den bereits vorhandenen DPAPI-Speicher im Benutzerprofil sichern.
- Offizielle Katalogsuche mit Land/`countryCode`.
- Providerneutrales Suchergebnis und TIDAL-Attribution.
- Track-Link und optional offizielles Embed.
- Speicherung in der lokalen RaidClip-Queue.
- Button zum manuellen Öffnen des TIDAL-Links.
- Keine Behauptung, dass der Titel automatisch in TIDAL eingereiht oder abgespielt wurde.

## 6. Geplante GUI

Auf der Musikwunsch-Seite sollte eine Auswahl `Spotify / TIDAL / Nur interne Queue / Deaktiviert` ergänzt werden. TIDAL erhält Client ID, geschütztes Client Secret, Redirect URI, Country Code, Verbinden/Trennen, Tokenstatus und Testsuche. Queue-, Geräte- und Playback-Steuerungen müssen abhängig von `MusicProviderCapabilities` ausgeblendet oder deaktiviert werden. Für TIDAL wird zunächst „Nur Links und interne Queue“ deutlich angezeigt.

Bestehende Command-, Kanalpunkt-, Queue-, Blacklist-, Cooldown- und Moderator-Einstellungen bleiben providerunabhängig erhalten.

## 7. Implementierungsphasen

1. **1.7.0 – Analyse und Grundlage:** Provider-Interface, gemeinsame Modelle, Capability-Katalog, Tests und diese Dokumentation. Bestehendes Spotify-Verhalten unverändert.
2. **Phase 2 – Sichere TIDAL-Authentifizierung:** eigene App-Daten, DPAPI-Speicher, Connect/Disconnect, Tokenstatus und Fehlertexte.
3. **Phase 3 – Katalog:** Suche, Metadaten, Links, Embeds, Country Code und Attribution.
4. **Phase 4 – Providerneutrale Queue:** `SongRequestManager`, bestehende Twitch-Commands/Kanalpunkte, lokale Queue und manuelles Öffnen.
5. **Phase 5 – Playback nur bei offizieller Grundlage:** ausschließlich ein unverändertes offizielles TIDAL SDK oder Embed; keine Desktop-Automatisierung und kein TIDAL-Connect-Hack.

## 8. Änderungen in 1.7.0

- `Models/MusicProviderModels.cs`
- `Services/IMusicProvider.cs`
- `Services/MusicProviderCatalog.cs`
- `Tests/MusicProviderCatalogTests.cs`
- `docs/TIDAL-INTEGRATION-ANALYSIS.md`
- Versionsanhebung in `RaidClipPlugin.csproj`

Diese Version aktiviert noch keinen TIDAL-Login und verändert den produktiven Spotify-/Songrequest-Ablauf nicht. Das ist beabsichtigt: Die nächste Implementierungsphase benötigt eigene TIDAL-App-Zugangsdaten und eine bewusste Freigabe für den eingeschränkten Funktionsumfang.
