# Roadmap

[日本語](ROADMAP.ja.md)

Where Drag'n Wash Localization is going. Plans change; dates are given only when they are close. Updated 2026-09-26.

## Released: v1.5.0 (2026-09-23)

- The F1 window's Activity log, Translation, Saves and About tabs, gone through one by one.
- Every language pack translates the new Mods screen of Drag'n Wash ModFramework 1.5.0.
- The zip no longer carries the framework: the installers fetch ModFramework 1.5.0 from its own release and check it first.

## Released: v1.4.0 (2026-09-20)

- Translations shipped by other mods ([#28](https://github.com/TomXV/dragnwash-localization/issues/28)), an experimental beta feature that is off by default.
- Translated pictures per language ([#4](https://github.com/TomXV/dragnwash-localization/issues/4)): the machinery, with no pictures drawn yet.
- Runs on Drag'n Wash ModFramework 1.4.0.

The two entries below say what each of them does.

### Translations for other mods ([#28](https://github.com/TomXV/dragnwash-localization/issues/28))

Mods that add their own text (new mechanics, UI, dialogue) should be translatable too.

- **What works now:** text a mod shows with TextMeshPro already goes through the same lookup as the game's, and untranslated lines show up in the exports. Text drawn with IMGUI or legacy uGUI `Text` does not.
- **How it works:** each mod ships its own translations in `<mod folder>/Translations/<locale>/strings.csv`, and this mod loads them after its own packs. A mod's file never overrides the game's lines; where two packs translate the same line differently, the one read first is kept (this mod's own pack, then the others in GUID order, the same order every time), the other is recorded as a conflict, and the log and F1 → Translation name both and say whose is used. The exports carry a mod column, so a line's owner is visible.
- It is an **experimental beta feature, off by default**: how it behaves with other mods is uncharted territory, so you turn it on knowing that.
- Machine translation is not built in.
- Other mods are tested when their authors ask.
- This repository does not carry other mods' translations.
- Design: [docs/MOD_TRANSLATIONS.md](MOD_TRANSLATIONS.md). Status: **built and tested in the game with a test mod; in the mod from the next release.**

### Translated pictures ([#4](https://github.com/TomXV/dragnwash-localization/issues/4))

Menu buttons, the loading screen's door sign and the signs on the walls are pictures. Under the framework's content policy, this repository carries pictures drawn by hand or changed from the game's, per language, with the artists credited. They go in `Translations/<locale>/textures/`, with a `fallback.txt` naming the languages to fall back to. A **Translate pictures** setting turns them off; on Direct3D 12 a language change takes effect after a restart. The framework's Assets library 1.2.0 carries the language-specific texture replacements underneath.

- Design: [docs/TRANSLATED_TEXTURES.md](TRANSLATED_TEXTURES.md). Status: **built and tested in the game (Direct3D 12); in the mod from the next release. No pictures are drawn yet** - the machinery is there, and any language can start adding them.

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

## Planned

### Native-speaker reviews

German, French, Spanish, Brazilian Portuguese, Russian, Polish, Hebrew, Ukrainian, Thai, Vietnamese and Traditional Chinese are still provisional. Corrections are welcome as pull requests at any time; each reviewer is credited ([CONTRIBUTING.md](../CONTRIBUTING.md#credits-for-contributors)).

### Checks on other systems

- Steam Deck: typing in the F1 window with the on-screen keyboard.
- macOS: the Doorstop in BepInEx 5.4.23.5 cannot hook Unity 6.3 ([NeighTools/UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)). For now, an experimental install script ([`installer/experimental/install-macos.sh`](../installer/experimental/install-macos.sh)) puts in the Doorstop from UnityDoorstop's `ci` pre-release, pinned to its 4.6.0 build, which was tested with the game, runs the game as x86_64 under Rosetta, and hands Steam's overlay on to it; progress is followed in [TomXV/dragnwash-modframework#85](https://github.com/TomXV/dragnwash-modframework/issues/85). The overlay hand-over was merged upstream on 2026-09-25 ([NeighTools/UnityDoorstop#121](https://github.com/NeighTools/UnityDoorstop/pull/121), in 4.6.0); with Doorstop 4.6.0 or later, including the pinned build, the script's edit is not needed and is skipped. As `ci` is rebuilt under the same URL (the 4.5.0 build pinned before can no longer be downloaded), the script downloads the 4.6.0 build from an unchanged copy kept as a pre-release in 223n's fork ([223n/UnityDoorstop `ci-4.6.0-97293a28`](https://github.com/223n/UnityDoorstop/releases/tag/ci-4.6.0-97293a28)). Next, the script pins the stable UnityDoorstop 4.6.0 in place of the copy, once it is released and tested. It is expected to carry both UnityDoorstop fixes ([NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117) and [NeighTools/UnityDoorstop#114](https://github.com/NeighTools/UnityDoorstop/pull/114)), as both are already in the pinned build. After that, the script only waits for a BepInEx release with BepInEx's arm64 fixes ([BepInEx/BepInEx#1288](https://github.com/BepInEx/BepInEx/pull/1288) and [BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402)), and moves to it. With it, the game is expected to run natively on arm64, without Rosetta (not tested yet).

## Not planned

- Machine translation inside the mod.
- Shipping the game's files or its English script unchanged ([content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)).
- Translations of other mods kept in this repository.
