# Roadmap

[日本語](ROADMAP.ja.md)

Where Drag'n Wash Localization is going. Plans change; dates are given only when they are close. Updated 2026-09-17.

## Released: v1.2.0 (2026-09-17)

- Ukrainian, Thai and Vietnamese (provisional), for 16 languages.
- Korean proofread by a native speaker (thanks, Hotcake).
- Translations that survive a game update editing a line.
- A logo by Mister ERIO.
- Runs on Drag'n Wash ModFramework 1.2.0.

## Planned

### Translations for other mods ([#28](https://github.com/TomXV/dragnwash-localization/issues/28))

Mods that add their own text (new mechanics, UI, dialogue) should be translatable too.

- **What works now:** text a mod shows with TextMeshPro already goes through the same lookup as the game's, and untranslated lines show up in the exports. Text drawn with IMGUI or legacy uGUI `Text` does not.
- **The plan:** each mod ships its own translations in `<mod folder>/Translations/<locale>/strings.csv`, and this mod loads them after its own packs. A mod's file never overrides the game's lines. This repository does not carry other mods' translations.
- It will come as an **experimental beta feature, off by default**: how it behaves with other mods is uncharted territory, so you turn it on knowing that.
- Machine translation will not be built in.
- Other mods are tested when their authors ask.
- Design: [docs/MOD_TRANSLATIONS.md](MOD_TRANSLATIONS.md). Status: **designed, not built yet.**

### Native-speaker reviews

German, French, Spanish, Brazilian Portuguese, Russian, Polish, Hebrew, Ukrainian, Thai, Vietnamese and Traditional Chinese are still provisional. Corrections are welcome as pull requests at any time; each reviewer is credited ([CONTRIBUTING.md](../CONTRIBUTING.md#credits-for-contributors)).

### Checks on other systems

- Steam Deck: typing in the F1 window with the on-screen keyboard.
- macOS: waiting on BepInEx's Doorstop, which cannot hook Unity 6.3 yet ([UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)).

## Open questions

- **Translated textures** ([#4](https://github.com/TomXV/dragnwash-localization/issues/4)): menu buttons, the loading screen and the wall signs are pictures. The framework can already replace textures with PNGs. Translated versions follow the framework's [content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md): art drawn by hand or changed from the game's is fine, the game's own images unchanged are not.

## Not planned

- Machine translation inside the mod.
- Shipping the game's files or its English script unchanged ([content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)).
- Translations of other mods kept in this repository.
