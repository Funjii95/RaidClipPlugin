# Bildquellen: Leuchtende Tiefe

Erzeugt mit dem integrierten ImageGen-Werkzeug, keine externen Bilddienste im Frontend.
Referenz: vom Nutzer gewähltes Mockup 3 „Leuchtende Tiefe“ aus diesem Chat. Die UI selbst ist React/CSS; Illustrationen enthalten keine Bedienelemente oder Texte. Keine Änderung an bestehenden Iconsystem-Abhängigkeiten: Navigationsicons sind lokale, mit dem Projekt erstellte SVG-Komponenten.

## public/luminous-depths.png

Finaler Prompt (Referenzbild: Mockup 3):

Use case: stylized-concept. The reference is a UI concept, use ONLY its illustrated world and otter mage character as visual reference. Generate a standalone landscape 3:2 game illustration, NOT a UI mockup. A lovable realistic-stylized brown otter mage swimming through mystical deep ocean ruins, violet embroidered cape and a staff with a luminous pearl, friendly face looking toward a glowing treasure chest of pearls at lower right. Otter at middle-left, expressive face fully visible, underwater columns, cyan rays from above, bioluminescent violet coral and luminous jellyfish around edges. Premium detailed fantasy game art, midnight navy, electric cyan, violet and soft pearl highlights. Keep central character readable at small size, central focus safe for crop. Edge scenery can serve as dark atmosphere. Absolutely NO text, lettering, logos, numbers, borders, buttons, interface or UI panels. Full-bleed finished illustration.

## public/otter-mage.png

Finaler Prompt (Referenzbild: die oben erzeugte Illustration):

Use case: stylized-concept. Create a tightly framed SQUARE fantasy game character portrait of the same friendly brown OTTER MAGE in the reference illustration, violet robe collar with cyan rune embroidery, large expressive dark eyes, soft whiskers, little round ears. Face and upper shoulders fill 85 percent of frame, face central with generous circular crop safety, looking slightly right but both eyes visible. Rich softly blurred dark midnight navy underwater background with restrained cyan and violet bioluminescence, premium painterly detailed game art matching reference. Recognizably an otter, not a cat or bear. No landscape composition, no chest, no text, no numbers, no logo, no frame or UI. This will be a small 48px avatar, prioritize strong face readability.

## Zugeordnete Itemgrafiken (2.1.22)

Vom Nutzer bereitgestellt: `RaidClipPlugin-2.1.22-Alle-Items.zip`, 102 transparente PNG-Motive. Die Zuordnung erfolgt ausschließlich nach `Item-ID` und verifizierter SHA256 aus `Item-Zuordnung.csv`, nicht nach der Reihenfolge oder ähnlich klingenden Namen. Dateinamen mit abweichender Umlautkodierung werden durch den Inhalts-Hash eindeutig aufgelöst.

Für die Extension wurden die gelieferten Motive ohne gestalterische Änderungen mit erhaltenem Alphakanal auf maximal 384×384 verkleinert und als WebP gespeichert. Die Original-ZIP bleibt unverändert auf dem Desktop. Dateien: `public/items/<item-id>.webp`. Herkunft, Original-/Asset-Prüfsummen und Maße: `src/item-art.json`. Reproduzierbarer Import: `python scripts/import-item-art.py <ZIP>` (Pillow erforderlich, nur zur Vorbereitung).

Alle vorhandenen Inventory-Definitionen sind abgedeckt, außerdem zwölf Fische und acht Sushi-Sorten. Fighter-Sushi verwendet dasselbe Bild für das Loot-Item und das Rezept. Das Bildpaket trägt 2.1.22; die RPG-Spielregeln wurden durch den Grafikimport nicht auf eine andere Version umgestellt.

## public/boss-kraken.png
Vom Nutzer am 20.10.2026 bereitgestelltes Bild 'Krakens Erwachen in den versunkenen Ruinen.png', unverändert als lokale Boss-Illustration eingebunden. Keine externe Bild-URL.
