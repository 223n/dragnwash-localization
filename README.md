# Drag'n Wash Localization

[日本語](README.ja.md)

An unofficial BepInEx-based localization mod for [Drag'n Wash](https://store.steampowered.com/).

The project aims to make it possible to add Japanese, Simplified Chinese, and other languages in the future without writing code.

See [docs/PLAN.md](docs/PLAN.md) for the technical research and implementation plan.

## Installation

### Quick install (recommended)

1. Download `DragNWashLocalization-<version>.zip` from the [Releases page](https://github.com/TomXV/dragnwash-localization/releases) and extract it anywhere.
2. Double-click **`Install.exe`**. The installer finds the game through Steam (or lets you pick the folder).
3. Choose the language (日本語 / 简体中文 / English) and press **Install / Update**. If BepInEx is not installed yet, the installer downloads the official 5.4.23.5 release, verifies its SHA-256, and unpacks it for you.
4. Start the game from Steam.

The same window has an **Uninstall** button. Save-history snapshots are kept by default, and BepInEx is removed together with the mod only when the installer put it there and no other plugin uses it.

If you prefer to do it by hand, follow the manual steps below.

### Manual installation

### What you need

- The Windows Steam version of Drag'n Wash
- [BepInEx 5 for 64-bit Windows (Mono)](https://github.com/BepInEx/BepInEx/releases)
- The latest `DragNWashLocalization-<version>.zip` from this repository's [Releases page](https://github.com/TomXV/dragnwash-localization/releases)

> [!IMPORTANT]
> Download the file named `DragNWashLocalization-<version>.zip` from the release assets. GitHub's automatically generated **Source code** archives are not installable mod packages. If the Releases page does not contain a mod ZIP yet, an installable build has not been published.

### 1. Open the game folder

In Steam, right-click **Drag'n Wash**, then select **Manage → Browse local files**. This opens the game root: the folder containing the game's `.exe`.

### 2. Install BepInEx

Download the BepInEx 5 archive for **Windows x64 (Mono)** and extract it directly into the game root.

After extraction, `winhttp.dll`, `doorstop_config.ini`, and the `BepInEx` folder should be next to the game's executable. If they are inside another nested folder, move them up to the game root.

Launch the game once, wait until the title screen appears, and close it. BepInEx will create its configuration and log files. Confirm that `BepInEx/LogOutput.log` now exists before continuing.

### 3. Install Drag'n Wash Localization

Download `DragNWashLocalization-<version>.zip` from [Releases](https://github.com/TomXV/dragnwash-localization/releases) and extract it into the **same game root**. Allow your archive tool to merge the included `BepInEx` folder.

The plugin DLL should end up at:

```text
<Drag'n Wash folder>/BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
```

Do not leave the ZIP itself or an extra `DragNWashLocalization-<version>` directory between `plugins` and the DLL.

### 4. Launch and verify

Start Drag'n Wash. Japanese is selected by default. Press **F1** to open the localization menu; under **Tools**, you can switch between the installed languages without restarting.

A successful installation also produces a `DragNWashLocalization` startup entry in `BepInEx/LogOutput.log`.

To change the default language manually, close the game and edit:

```text
BepInEx/config/com.tomxv.dragnwash.localization.cfg
```

Set `TargetLocale` under `[General]` to an installed locale such as `ja` or `zh-Hans`, then start the game again. `en` keeps the game's original English text (the mod stays installed but translates nothing); the installer and the in-game F1 menu offer the same choice.

### If the mod does not load

- Confirm that both BepInEx and the mod were extracted into the folder containing the game executable.
- Confirm the exact DLL path shown above.
- Open `BepInEx/LogOutput.log`. If the file does not exist, BepInEx itself is not loading. If it exists, search it for `DragNWashLocalization` and review the nearby error.
- If opening Options causes a Direct3D 12 crash, use the Windows workaround described in [Crash when opening Options on Windows](#crash-when-opening-options-on-windows).

## For translators

You can add a translation by editing `Translations/<locale>/strings.csv`. The published file has three columns, `key,speaker,translation`, where `key` is a hash of the English line and `speaker` says who says it. The recommended way to work is:

1. In the game, open **F1 → Tools → Export working copy**. This writes `Translations/_discovered/<locale>.working.csv` with the English text beside every line (`key,speaker,source_en,translation`), in the order the lines are played.
2. Edit the `translation` column. Saving the file hot-reloads it into the running game.
3. Before committing, press **F1 → Tools → Hash for commit** (or run `tools/hash-strings.ps1`). This regenerates `strings.csv` without any English text.

Each language folder also holds a one-line `name.txt` with the language's display name (for example `日本語`), shown in the installer and the in-game menu.

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

When you save `Translations/<current-language>/strings.csv` or the working copy `_discovered/<locale>.working.csv` while the game is running, the plugin reloads it automatically after about two seconds and immediately updates text that is currently visible. Each changed line is listed in the F1 Activity log.

This lets you edit a translation and check it in the game repeatedly without restarting. You can disable this behavior with `[Debug] HotReloadTranslations`. Successful reloads appear as `[reload]` entries in the F1 Activity log.

### Exporting all UI text

Press **F7** to export all loaded UI text to:

`Translations/_discovered/ui_texts.csv`

The export includes **hidden menus**, so you can collect every UI string in the current scene without opening the pause menu or confirmation dialogs. Its columns are `key`, `source_en`, `translation` (the existing translation, if any), and `object_path` (where the text appears in the UI).

Press F7 once on the title screen and once during gameplay to collect nearly all UI text.

Untranslated UI text is also recorded automatically during gameplay in `Translations/_discovered/strings.csv`. This file is cleaned up each time the game starts: translated entries and duplicates are removed, leaving an up-to-date list of remaining work.

### Strings that do not need translation

Slider values, resolutions such as `1920 x 1080 @ 164.995Hz`, build numbers, and similar strings are excluded from discovery by default.

You can add exclusion patterns to [Translations/ignore.txt](Translations/ignore.txt). The file uses regular expressions and includes examples.

Exclusions affect discovery only. Translation lookup happens first, so any entry present in `strings.csv` will always be translated even if it matches an exclusion pattern.

### In-game debug menu

Press **F1** to toggle the debug window. The key is configurable. Drag the title bar to move the window, and drag the lower-right corner to resize it.

- **Activity log:** Displays translation results and processing logs. `Follow: ON/OFF` controls automatic scrolling to the latest entry; scrolling manually disables following. `Clear log` clears the display and resets duplicate suppression. The log keeps the 100 most recent entries.
- **Tools:** Switch the language without restarting (the buttons show each language's name from `name.txt`; **English** turns translation off). Export dialogue (`Export loaded dialogue`), UI text (`Export UI text`), or the working copy with English beside each line (`Export working copy`), rebuild the published file (`Hash for commit`), and run the layout check.
- **Saves:** Restore an earlier save, step the level index, or toggle save flags. See below.

The **Check translation layout** button exports strings at risk of overflowing their layout to `Translations/_discovered/layout_risks.csv`. Configure the threshold with `BepInEx/config/.../LayoutOverflowThreshold`; the default is `1.0`, meaning an exact fit.

### Restoring the previous save for translation testing

Whenever the game writes a save, the plugin stores a versioned copy in:

`BepInEx/plugins/DragNWashLocalization/SaveHistory/<slot>/`

It keeps 30 versions per slot by default. You can change this with `[Debug] SaveHistoryKeep`.

Open **F1 → Saves**, select a slot, and click **Restore** on the version you want. Then return to the title screen and load that slot for the restored save to take effect. Saving again during gameplay will overwrite the active save as usual.

The plugin automatically preserves the state from immediately before a restore, so you can recover if you go back too far.

Use this feature to revisit the same scene while comparing revisions of a dialogue translation. Restore replaces the game's own save file without editing flags or variables.

The same tab also has a **PROGRESS** editor: step the level index back or forward with **-** / **+** and press **Apply**. Moving forward asks for confirmation because it can spoil content you have not seen. **Flags...** lists every event flag the game is known to use, grouped (level flow, story, romance, scene triggers, scene watched, items) with a short description, whether or not the save has set it yet. Click a value to cycle unset → true → false, type in the search box to filter, and use **Reset all to false...** to wipe every flag (the level index is kept). The list comes from `FlagCatalog.csv` next to the plugin DLL, so you can add rows for flags found later. Every edit snapshots the save first.

## Crash when opening Options on Windows

A crash in `D3D12ScratchAllocator::DestroyScratch` has been observed when opening Options with Unity 6000.3.14f1 and DirectX 12. Unity has an official issue report with the same stack trace: [UUM-140564](https://issuetracker.unity.com/issues/10698). Because this is a native rendering bug, it cannot be prevented by catching exceptions in the translation hook.

The bug is triggered when textures are allocated or uploaded at runtime. The plugin generates Japanese glyphs together at startup to avoid atlas updates during gameplay, and this has been verified on an actual system to prevent the crash while continuing to use Direct3D 12. No configuration is required.

If the game still crashes, open Steam and go to **Drag'n Wash → Properties → General → Launch Options**, add `-force-d3d11`, and restart the game. This bypasses the issue by switching the graphics API. The option is part of [Unity's standard command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html), and it does not modify the game's DLLs or save data.

The plugin startup entry in `BepInEx/LogOutput.log` reports the graphics API currently in use as `graphics=...`.

Lowering `[Font] AtlasPointSize` in `BepInEx/config/com.tomxv.dragnwash.localization.cfg` reduces the number of font atlases. Raising it produces sharper text. The default is 80.

## Current status

Released as v0.1.1 (one-click installer, English pass-through, save progress editor). The BepInEx plugin skeleton, Japanese and Chinese replacement of UI and dialogue text, CJK font rendering, bulk dialogue and UI export, in-game debug menu, layout overflow detection, translator documentation, and release workflow have all been implemented and tested in the game.

See [docs/PLAN.md](docs/PLAN.md) for details.

## Contributing translations

No code is required. Edit `Translations/<locale>/strings.csv` to contribute a translation.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, file format, and instructions for finding untranslated strings.

## Distribution and releases

See [docs/RELEASING.md](docs/RELEASING.md) for instructions on building and distributing the release ZIP.

Because the game-derived reference assemblies cannot be committed, releases are built locally and uploaded to GitHub Releases.

## License

See [LICENSE](LICENSE) for the plugin's code license. This repository does not include assets or code from the game. Translations are treated as contributions from their respective translators.
