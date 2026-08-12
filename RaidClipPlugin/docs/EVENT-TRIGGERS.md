# Event-Trigger

Die Event-Trigger befinden sich unter `Timer > Event-Trigger` und senden bei
aktivierten Ereignissen eine frei konfigurierbare Nachricht in den Twitch-Chat.
Für jeden Trigger kann zusätzlich ein lokaler Streamer-Sound ausgewählt und über
`Anhören` getestet werden. Zur Auswahl stehen kein Sound, Hinweis, Erfolg,
Glocke, Achtung und Frage. Der Sound wird nur auf dem RaidClip-PC abgespielt.

## Twitch

Follows, Abonnements, Geschenkabos, Resubs, Cheers/Bits und Werbepausen werden
direkt über Twitch EventSub empfangen. Nach dem Update muss Twitch einmal neu
verbunden werden, da für Cheers zusätzlich der Scope `bits:read` benötigt wird.

## StreamElements

Benötigt werden die Channel-ID und ein JWT, Overlay-Token oder OAuth2-Token.
Der Token-Typ muss passend ausgewählt werden. RaidClip abonniert anschließend
den offiziellen Astro-WebSocket-Topic `channel.tips`.

## Streamlabs

Benötigt wird ein OAuth Access Token mit `donations.read`. RaidClip fragt nur
verifizierte Donations ab und sendet beim ersten Abruf keine alten Tips erneut.

## Ko-fi und TipeeeStream

RaidClip nimmt Webhooks lokal auf Port `17892` entgegen:

- Ko-fi: `http://127.0.0.1:17892/kofi`
- TipeeeStream: `http://127.0.0.1:17892/tipeeestream`

Da externe Plattformen keine lokale Adresse erreichen können, muss davor ein
öffentlich erreichbares HTTPS-Relay oder ein sicherer Tunnel eingerichtet werden.
Ko-fi wird über dessen `verification_token` geprüft. Das TipeeeStream-Relay muss
den eingestellten Prüftoken im HTTP-Header `X-RaidClip-Token` mitsenden.

## Platzhalter

`{user}`, `{amount}`, `{currency}`, `{message}`, `{months}`, `{quantity}`,
`{gift}`, `{provider}`, `{duration}` und `{type}`.
