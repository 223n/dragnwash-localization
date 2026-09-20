# Roadmap

[日本語](ROADMAP.ja.md)

Where Drag'n Wash Localization is going. Plans change; dates are given only when they are close. Updated 2026-09-20.

## Released: v1.3.0 (2026-09-19)

- Runs on Drag'n Wash ModFramework 1.3.0: fewer crashes on Direct3D 12, and a crash report window when the game does crash.

## Released: v1.2.1 (2026-09-19)

- The Saves tab finds saves made after the game update of 2026-09-14 again (Drag'n Wash ModFramework 1.2.1).
- The working copy fills the English of screens that were not open when it was exported ([#31](https://github.com/TomXV/dragnwash-localization/issues/31)).

## Released: v1.2.0 (2026-09-17)

- Ukrainian, Thai and Vietnamese (provisional), for 16 languages.
- Korean proofread by a native speaker (thanks, Hotcake).
- Translations that survive a game update editing a line.
- A logo by Mister ERIO.
- Runs on Drag'n Wash ModFramework 1.2.0.

## Built, in the next release

### Translations for other mods ([#28](https://github.com/TomXV/dragnwash-localization/issues/28))

Mods that add their own text (new mechanics, UI, dialogue) should be translatable too.

- **What works now:** text a mod shows with TextMeshPro already goes through the same lookup as the game's, and untranslated lines show up in the exports. Text drawn with IMGUI or legacy uGUI `Text` does not.
- **How it works:** each mod ships its own translations in `<mod folder>/Translations/<locale>/strings.csv`, and this mod loads them after its own packs. A mod's file never overrides the game's lines; where two mods translate the same line, neither is used and both are named in the log. The exports carry a mod column, so a line's owner is visible.
- It is an **experimental beta feature, off by default**: how it behaves with other mods is uncharted territory, so you turn it on knowing that.
- Machine translation is not built in.
- Other mods are tested when their authors ask.
- This repository does not carry other mods' translations.
- Design: [docs/MOD_TRANSLATIONS.md](MOD_TRANSLATIONS.md). Status: **built and tested in the game with a test mod; in the mod from the next release.**

### Translated pictures ([#4](https://github.com/TomXV/dragnwash-localization/issues/4))

Menu buttons, the loading screen's door sign and the signs on the walls are pictures. Under the framework's content policy, this repository carries pictures drawn by hand or changed from the game's, per language, with the artists credited. They go in `Translations/<locale>/textures/`, with a `fallback.txt` naming the languages to fall back to. A **Translate pictures** setting turns them off; on Direct3D 12 a language change takes effect after a restart. The framework's Assets library 1.2.0 carries the language-specific texture replacements underneath.

- Design: [docs/TRANSLATED_TEXTURES.md](TRANSLATED_TEXTURES.md). Status: **built and tested in the game (Direct3D 12); in the mod from the next release. No pictures are drawn yet** - the machinery is there, and any language can start adding them.

## Planned

### Native-speaker reviews

German, French, Spanish, Brazilian Portuguese, Russian, Polish, Hebrew, Ukrainian, Thai, Vietnamese and Traditional Chinese are still provisional. Corrections are welcome as pull requests at any time; each reviewer is credited ([CONTRIBUTING.md](../CONTRIBUTING.md#credits-for-contributors)).

### Checks on other systems

- Steam Deck: typing in the F1 window with the on-screen keyboard.
- macOS: waiting on BepInEx's Doorstop, which cannot hook Unity 6.3 yet ([UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)).

## Not planned

- Machine translation inside the mod.
- Shipping the game's files or its English script unchanged ([content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)).
- Translations of other mods kept in this repository.
