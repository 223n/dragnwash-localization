# Drag'n Wash Localization

[日本語](README.ja.md)

An unofficial BepInEx-based localization mod for [Drag'n Wash](https://store.steampowered.com/).

The project aims to make it possible to add Japanese, Simplified Chinese, and other languages in the future without writing code.

See [docs/PLAN.md](docs/PLAN.md) for the technical research and implementation plan.

## For translators

You can add a translation by editing `Translations/<locale>/strings.csv`. While translating, use two columns: `source_en` (the exact English text displayed in the game) and `translation` (your translation). Before committing, convert the source text into hashes in the `key` column.

The game's English script is intentionally not included in this repository. This ensures that **only people who own the full game can create translations**. You do not need to know Unity's internal keys or write any code. See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed instructions.

If the source text contains formatting tags such as `<size=70%>`, preserve the tag structure and translate only the text inside it.

### Exporting all dialogue for context

After installing the plugin, load a save and press **F6** in the game. The plugin exports all dialogue to:

`BepInEx/plugins/DragNWashLocalization/Translations/_discovered/dialogue_lines.csv`

The export has been verified with 1,839 lines on an actual game installation.

Lines appear in the order in which they are played in the game. The `node` column identifies each conversation and uses names such as `Alexander_2_intro`, following the pattern "character name_occurrence_scene." The `order` column gives the line's position within that conversation. The `kind` column distinguishes character dialogue (`line`) from player choices (`option`). This context makes it easier to understand who is speaking and what each response refers to. The three dragons in the game are Conrad, Ryan, and Alexander.

Copy the lines you want to translate into `Translations/<locale>/strings.csv`, fill in the `translation` column, and submit a pull request. Extra columns such as `node` and `key` may be left in place; the plugin will still load the file correctly.

Already translated lines are exported with their translations filled in, so exporting again will not discard your work.

### Previewing edits without restarting the game

When you save `Translations/<current-language>/strings.csv` while the game is running, the plugin reloads it automatically after about two seconds and immediately updates text that is currently visible.

This lets you edit a translation and check it in the game repeatedly without restarting. You can disable this behavior with `[Debug] HotReloadTranslations`. Successful reloads appear as `[reload]` entries in the F1 Activity log.

### Exporting all UI text

Press **F7** to export all loaded UI text to:

`Translations/_discovered/ui_texts.csv`

The export includes **hidden menus**, so you can collect every UI string in the current scene without opening the pause menu or confirmation dialogs. Its columns are `source_en`, `translation` (the existing translation, if any), and `object_path` (where the text appears in the UI).

Press F7 once on the title screen and once during gameplay to collect nearly all UI text.

Untranslated UI text is also recorded automatically during gameplay in `Translations/_discovered/strings.csv`. This file is cleaned up each time the game starts: translated entries and duplicates are removed, leaving an up-to-date list of remaining work.

### Strings that do not need translation

Slider values, resolutions such as `1920 x 1080 @ 164.995Hz`, build numbers, and similar strings are excluded from discovery by default.

You can add exclusion patterns to [Translations/ignore.txt](Translations/ignore.txt). The file uses regular expressions and includes examples.

Exclusions affect discovery only. Translation lookup happens first, so any entry present in `strings.csv` will always be translated even if it matches an exclusion pattern.

### In-game debug menu

Press **F1** to toggle the debug window. The key is configurable. Drag the title bar to move the window, and drag the lower-right corner to resize it.

- **Activity log:** Displays translation results and processing logs. `Follow: ON/OFF` controls automatic scrolling to the latest entry; scrolling manually disables following. `Clear log` clears the display and resets duplicate suppression. The log keeps the 100 most recent entries.
- **Tools:** Select any installed language to switch the on-screen translation without restarting. You can also export dialogue (`Export loaded dialogue`) and run the layout check here.

The **Check translation layout** button exports strings at risk of overflowing their layout to `Translations/_discovered/layout_risks.csv`. Configure the threshold with `BepInEx/config/.../LayoutOverflowThreshold`; the default is `1.0`, meaning an exact fit.

### Restoring the previous save for translation testing

Whenever the game writes a save, the plugin stores a versioned copy in:

`BepInEx/plugins/DragNWashLocalization/SaveHistory/<slot>/`

It keeps 30 versions per slot by default. You can change this with `[Debug] SaveHistoryKeep`.

Open **F1 → Saves**, select a slot, and click **Restore** on the version you want. Then return to the title screen and load that slot for the restored save to take effect. Saving again during gameplay will overwrite the active save as usual.

The plugin automatically preserves the state from immediately before a restore, so you can recover if you go back too far.

Use this feature to revisit the same scene while comparing revisions of a dialogue translation. It replaces the game's own save file without directly editing flags or variables.

## Crash when opening Options on Windows

A crash in `D3D12ScratchAllocator::DestroyScratch` has been observed when opening Options with Unity 6000.3.14f1 and DirectX 12. Unity has an official issue report with the same stack trace: [UUM-140564](https://issuetracker.unity.com/issues/10698). Because this is a native rendering bug, it cannot be prevented by catching exceptions in the translation hook.

The bug is triggered when textures are allocated or uploaded at runtime. The plugin generates Japanese glyphs together at startup to avoid atlas updates during gameplay, and this has been verified on an actual system to prevent the crash while continuing to use Direct3D 12. No configuration is required.

If the game still crashes, open Steam and go to **Drag'n Wash → Properties → General → Launch Options**, add `-force-d3d11`, and restart the game. This bypasses the issue by switching the graphics API. The option is part of [Unity's standard command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html), and it does not modify the game's DLLs or save data.

The plugin startup entry in `BepInEx/LogOutput.log` reports the graphics API currently in use as `graphics=...`.

Lowering `[Font] AtlasPointSize` in `BepInEx/config/com.tomxv.dragnwash.localization.cfg` reduces the number of font atlases. Raising it produces sharper text. The default is 80.

## Current status

Phase 4 is complete. The BepInEx plugin skeleton, Japanese and Chinese replacement of UI and dialogue text, CJK font rendering, bulk dialogue and UI export, in-game debug menu, layout overflow detection, translator documentation, and release workflow have all been implemented and tested in the game.

See [docs/PLAN.md](docs/PLAN.md) for details.

## Contributing translations

No code is required. Edit `Translations/<locale>/strings.csv` to contribute a translation.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, file format, and instructions for finding untranslated strings.

## Distribution and releases

See [docs/RELEASING.md](docs/RELEASING.md) for instructions on building and distributing the release ZIP.

Because the game-derived reference assemblies cannot be committed, releases are built locally and uploaded to GitHub Releases.

## License

See [LICENSE](LICENSE) for the plugin's code license. This repository does not include assets or code from the game. Translations are treated as contributions from their respective translators.
