# Drag'n Wash Localization

[日本語](README.ja.md) | [한국어](README.ko.md)

An unofficial BepInEx-based multilingual localization mod for [Drag'n Wash](https://store.steampowered.com/app/4739660/).

It lets you play the game in lots of languages (see [Language packs](#language-packs)), and anyone can add a language or improve one just by editing CSV files. You don't have to write any code.

The technical research and the implementation plan are in [docs/PLAN.md](docs/PLAN.md).

> [!WARNING]
> ## ⚠️ SPOILER WARNING ⚠️
> **The CSV files under `Translations/` contain every conversation in the game, in story order.**
> Opening them will spoil the story. Play through the game a few times first!

## Installation

### Quick install (recommended)

Installing is really easy.

1. Download the zip from the [Releases page](https://github.com/TomXV/dragnwash-localization/releases) and extract it anywhere.
2. Double-click **`Install.exe`**.
3. Pick a language and **click Install** (it says **Update** if the mod is already installed).

> [!TIP]
> The same steps are on Steam as a guide too: [English](https://steamcommunity.com/sharedfiles/filedetails/?id=3801420947) / [日本語](https://steamcommunity.com/sharedfiles/filedetails/?id=3801418794). Drag'n Wash has no Steam Workshop, so you download the mod itself from GitHub Releases.

## See? EASY. ( ･´ｰ･｀) HEH! YIP!

The installer finds the game through Steam by itself, or you can point it at the folder. If BepInEx isn't installed yet, it downloads the official 5.4.23.5 release, checks its SHA-256 and unpacks it for you. After that, just start the game from Steam.

You get 日本語, 简体中文 and English (no translation), Korean proofread by a native speaker, provisional packs for Traditional Chinese, German, French, Spanish, Brazilian Portuguese, Russian, Polish, Hebrew, Ukrainian, Thai and Vietnamese, and Esperanto and Toki Pona for fun (see [Language packs](#language-packs)). The same window has an **Uninstall** button. It keeps your save-history snapshots unless you say otherwise, and it only removes BepInEx along with the mod if you ask and no other mod uses it. You can uninstall from inside the game as well: **Options → Mods → Drag'n Wash Localization → Uninstall**, and the mod is gone the next time the game starts.

If you'd rather do it by hand, the manual steps are below.


> [!NOTE]
> **If nothing happens when you run `Install.exe`, or Windows says "Windows protected your PC"**
> `Install.exe` is a small unsigned program, so Windows SmartScreen may stop it the first time you run it.
> - If you see the warning, click **More info → Run anyway**.
> - If no window shows up at all, right-click `Install.exe` → Properties → tick **Unblock** → OK, and double-click it again.

> [!WARNING]
> **If Windows Security (Microsoft Defender) detects `Install.exe` as "Trojan:Script/Wacatac.B!ml", or `Install.exe` is missing from the folder you extracted**
> This is a false positive. The `!ml` at the end means a machine-learning model guessed the file looks suspicious. It didn't match any known malware. Installers up to v1.0.0 started a PowerShell script without a console window, and that looks like something malware does. From v1.1.0, `Install.exe` is Drag'n Wash ModFramework's shared installer, a plain unsigned program that runs no scripts, but a new unsigned file can still get flagged. Its source is public: [`installer/`](https://github.com/TomXV/dragnwash-modframework/tree/main/installer) in the framework's repository.
>
> When the file is quarantined automatically, you don't see a threat name. `Install.exe` just seems to be missing from the extracted folder. Windows Security → **Protection history** shows what was removed.
> - First make sure the zip you downloaded is the real one. In PowerShell, run `(Get-FileHash "<path to the zip>").Hash -eq ("<the sha256 from Releases>" -replace '^sha256:')`. If it prints `True`, the file is the one published here. If it prints `False`, delete the file and don't use it.
> - You'll find the `sha256` under `DragNWashLocalization-<version>.zip` on the [Releases](https://github.com/TomXV/dragnwash-localization/releases) page. The two "Source code" rows GitHub adds automatically have no hash, so don't use those.
> - A match tells you the file is the one published here. On its own it doesn't prove the file is safe; that's what the source linked above is for.
> - If it matches, open the detection in Windows Security → **Protection history** and choose **Actions → Allow on device**. Only allow that one file. You don't need a folder exclusion, and you don't need to turn Windows Security off.
> - If you'd rather not allow anything, use the manual installation steps below instead.
> - Don't use copies from anywhere other than this repository's Releases page.

### Windows on ARM (verified)

On ARM Windows PCs such as Snapdragon X laptops, install with the same `Install.exe` steps and the mod works. The game itself runs as x64 under emulation.

> [!IMPORTANT]
> **The game doesn't render correctly on DirectX 12 there, so add `-force-d3d11` to its Steam launch options.** It happens without the mod too, so it's a problem in the game and this mod can't fix it.
> - With an older GPU driver the game crashes right after the splash screen.
> - With the latest driver it doesn't crash any more, but no 3D gets drawn.
>
> In Steam, right-click the game → **Properties** → **Launch Options** and enter `-force-d3d11`. The game then starts on DirectX 11 and plays normally.
> Verified on an ASUS ProArt PZ13 (Snapdragon X Plus / Adreno X1-45).

### Steam Deck / Linux (verified)

> [!IMPORTANT]
> Steam Deck support needs **v0.3.0 or later**. Earlier versions do run on the Deck, but you can't use the F1 menu there.

It works with the game's native Linux build and the Linux build of BepInEx. `Install.exe` is for Windows, so on the Deck you use the install script.

**Install script (recommended)**, in Desktop Mode:

1. Download the zip from the [Releases page](https://github.com/TomXV/dragnwash-localization/releases) and extract it (right-click → Extract).
2. Open the extracted folder, right-click an empty spot and choose **Open Terminal Here**.
3. Type this and press Enter:

   ```bash
   bash install-steamdeck.sh
   ```

4. Choose **Install / Update**, then pick a language. Steam has to close for a moment so the launch option can be set. The script asks you first and starts Steam again afterwards.
5. Go back to Gaming Mode and start the game. You can change the language later in **Options → Language (Mod)**.

The script finds the game in your Steam libraries (an SD card counts too), downloads the official Linux BepInEx 5.4.23.5 and checks its SHA-256, and sets `executable_name="DragNWash"` in `run_bepinex.sh`. Then it copies the mod and adds `./run_bepinex.sh %command%` to the game's launch options, keeping whatever options you already had. To update or remove the mod, run the same command again and choose **Install / Update** or **Uninstall**. Uninstalling keeps your save history and takes `./run_bepinex.sh` back out of the launch options when no other BepInEx mod needs it. It also offers to remove BepInEx. `--install` and `--uninstall` skip the question.

Steam rewrites launch options while it's running, so when the launch option has to change, the script closes Steam, edits it and starts Steam again. It asks first; `--close-steam` skips that question. If a step couldn't be done, the last dialog says so and tells you what to change by hand. Every run is logged to `~/.local/state/dragnwash-installer/installer.log`.

<details>
<summary>Manual installation on the Deck</summary>

1. Extract [BepInEx_linux_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x64_5.4.23.5.zip) into the game folder (`~/.local/share/Steam/steamapps/common/Drag'n Wash/`).
2. Merge this mod's `BepInEx/` folder into the same place.
3. Open `run_bepinex.sh`, set `executable_name="DragNWash"`, save, and run `chmod +x run_bepinex.sh`.
4. In Steam, game properties → Launch options: `./run_bepinex.sh %command%`
5. Start the game. Change the language in **Options → Language (Mod)**.

</details>

You don't need to set up any fonts. The game text uses SteamOS's Noto Sans CJK straight from the font file (the Japanese, Chinese and Korean faces). The F1 menu draws with a Noto Sans JP that comes with the mod (`dragnwash-menufont.bundle`), because Steam's Linux runtime doesn't give Unity's menu system any CJK font. That menu font has no Hangul or Hebrew, so on the Deck the F1 language buttons show the locale code for those two. The game itself shows them fine.

To change the language on the Deck, use **Options → Language (Mod)** with the controller. You don't need the F1 key for that. If you want the translator tools in the F1 menu (Gaming Mode):

- Bind **F1** to a button with Steam Input to open it.
- Point with the right trackpad or the touchscreen. **A**, **R2** or a trackpad click presses the button under the pointer. (Steam Input sends the trackpad click as a stick press instead of a mouse click, so the mod handles that itself.)
- Hold one of those buttons on the title bar to move the window, or on the bottom-right corner to resize it.
- The sticks and the d-pad scroll whichever list the pointer is over.

> [!WARNING]
> **macOS doesn't work right now.** Drag'n Wash is built with Unity 6.3, and the Doorstop loader that BepInEx 5.4.23.5 uses on macOS can't hook Unity 6.3 games yet ([NeighTools/UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)). Doorstop does load into the game, but BepInEx never starts: no `BepInEx/LogOutput.log` and no `BepInEx/config` show up, and the game runs in English. We checked this on an Apple M3 Pro with macOS 26.6, both natively and under Rosetta. The problem is on the BepInEx side, so there's nothing this mod can do to work around it. Once a BepInEx release has the fix, macOS will be tested again.
>
> For that day, the repository keeps an **experimental** macOS install script: [`installer/experimental/install-macos.sh`](installer/experimental/install-macos.sh). It does the same job as the Steam Deck script (BepInEx for macOS, the mod, the language and the Steam launch option), and it has a **Check** mode that tells you whether the mod loaded after you start the game. Nobody has run it on a Mac yet. It warns about the issue above before installing, and it isn't in the release zip.

### Manual installation

### What you need

- The Windows Steam version of Drag'n Wash
- [BepInEx 5 for 64-bit Windows (Mono)](https://github.com/BepInEx/BepInEx/releases)
- The latest `DragNWashLocalization-<version>.zip` from this repository's [Releases page](https://github.com/TomXV/dragnwash-localization/releases)

> [!IMPORTANT]
> Download the file called `DragNWashLocalization-<version>.zip` from the release assets. The **Source code** archives GitHub generates automatically aren't mod packages you can install. If the Releases page doesn't have a mod ZIP yet, no installable build has been published.

### 1. Open the game folder

In Steam, right-click **Drag'n Wash** and choose **Manage → Browse local files**. That opens the game root, the folder with the game's `.exe` in it.

### 2. Install BepInEx

Download the BepInEx 5 archive for **Windows x64 (Mono)** and extract it straight into the game root.

After that, `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder should sit next to the game's executable. If they ended up inside another folder, move them up to the game root.

Start the game once, wait for the title screen, and close it. BepInEx creates its config and log files on that first run. Check that `BepInEx/LogOutput.log` is there before you go on.

### 3. Install Drag'n Wash Localization

Download `DragNWashLocalization-<version>.zip` from [Releases](https://github.com/TomXV/dragnwash-localization/releases) and extract it into the **same game root**. Let your archive tool merge the `BepInEx` folder that's inside.

The plugin DLL should end up here:

```text
<Drag'n Wash folder>/BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
```

The zip also brings Drag'n Wash ModFramework, which the mod runs on, as one folder per plugin next to it: `BepInEx/plugins/DragNWash.ModFramework`, `DragNWash.ModFramework.Text`, `.Dialogue`, `.ToolWindow`, `.Assets` and `.Saves`, plus `BepInEx/patchers/DragNWash.ModFramework.Preloader.dll`. Keep all of them. If another mod already installed a newer ModFramework, keep the newer files.

Don't leave the ZIP itself, or an extra `DragNWashLocalization-<version>` folder, between `plugins` and the DLL.

### 4. Launch and verify

Start Drag'n Wash. A manual install starts in Japanese. (The installer uses whichever language you picked.)

To change the language, open **Options** and use **Language (Mod)** at the end of the Gameplay section. Picking a language switches the game to it right away. Press **Save** to keep it, or **Back** to go back to the saved language. It works with a mouse or a gamepad, on the Steam Deck too. You can also switch language from the **Translation** tab in the **F1** window, and that saves your choice straight away.

When the install worked, `BepInEx/LogOutput.log` also gets a `DragNWashLocalization` startup entry.

To change the default language by hand, close the game and edit:

```text
BepInEx/config/com.tomxv.dragnwash.localization.cfg
```

Set `TargetLocale` under `[General]` to an installed locale such as `ja` or `zh-Hans`, then start the game again. `en` keeps the game's original English text: the mod stays installed but doesn't translate anything. The installer, Options → Language (Mod) and the F1 menu all offer the same choice.

### If the mod does not load

- Check that both BepInEx and the mod were extracted into the folder with the game executable in it.
- Check the DLL is at exactly the path shown above.
- Open `BepInEx/LogOutput.log`. If there's no such file, BepInEx itself isn't loading. If it's there, search it for `DragNWashLocalization` and look at the error around it.
- If opening Options makes the game crash in Direct3D 12, use the Windows workaround in [Crash when opening Options on Windows](#crash-when-opening-options-on-windows).
- If you installed a translation before that overwrites the game's files (for example files copied into `DragNWash_Data`), the game's English is gone and this mod has nothing left to translate. Put the original files back first: right-click the game in Steam → **Properties** → **Installed Files** → **Verify integrity of game files**, then install this mod again. Translations like that also stop working, or break, when the game updates. This mod doesn't change any game files.

## Language packs

TomXV wrote the translation files, and they come in the same zip. Contributors who improve a pack get credited in its row below (see [Credits for contributors](CONTRIBUTING.md#credits-for-contributors)). The installer and the F1 menu list every folder under `Translations/`.

| Locale | Language | Status |
| --- | --- | --- |
| `ja` | 日本語 | Supervised by the author |
| `zh-Hans` | 简体中文 | Supervised by the author |
| `zh-Hant` | 繁體中文 | Provisional, converted from the supervised Simplified Chinese with Taiwan wording |
| `de` | Deutsch | Provisional |
| `fr` | Français | Provisional |
| `es` | Español | Provisional |
| `pt-BR` | Português (Brasil) | Provisional |
| `ko` | 한국어 | Proofread by a native speaker, Hotcake (lines the game doesn't use were left as they were) |
| `ru` | Русский | Provisional |
| `pl` | Polski | Provisional |
| `he` | עברית | Provisional, drawn right to left |
| `uk` | Українська | Provisional |
| `th` | ไทย | Provisional (a zero-width space between words lets lines break) |
| `vi` | Tiếng Việt | Provisional |
| `eo` | Esperanto | Provisional, just for fun |
| `tok` | toki pona | Provisional, just for fun (it's a 137-word language, so expect it to be loose) |
| `en` | English | The game's original text (no translation) |

> [!NOTE]
> Nobody who speaks the language natively has reviewed the **Provisional** packs yet. They're complete and you can play with them, but some lines may sound odd or miss a joke. Only Japanese and Simplified Chinese were supervised by the author. If you're a native speaker, corrections are very welcome as pull requests (see [CONTRIBUTING.md](CONTRIBUTING.md)). Every `strings.csv` starts with a comment saying the same thing.

## For translators

You add a translation by editing `Translations/<locale>/strings.csv`. The published file has the columns `key,section,node,order,speaker,translation`. `key` is a hash of the English line, `section`/`node`/`order` say where in the game it plays (the level and the conversation, in play order), and `speaker` is who says it. Lines starting with `#` are section headers like `# ===== Level 1: Ryan (Sunny) =====`, so the file reads top to bottom like a script. This is the way we recommend working:

1. In the game, open **F1 → Translation → Export working copy**. That writes `Translations/_discovered/<locale>.working.csv` with the English next to every line (`key,section,node,order,speaker,source_en,translation`), in the order the lines play, with the same section headers.
2. Edit the `translation` column. When you save the file, the running game hot-reloads it.
3. Before you commit, press **F1 → Translation → Hash for commit** (or run `tools/hash-strings.ps1`). That rebuilds `strings.csv` with no English text in it.

Each language folder also has a one-line `name.txt` with the language's display name (for example `日本語`). The installer and the in-game menu show that name.

The game's English script is left out of this repository on purpose, so **only people who own the full game can make translations**. You don't need to know Unity's internal keys or write any code. [CONTRIBUTING.md](CONTRIBUTING.md) has the detailed steps.

If the source text has formatting tags such as `<size=70%>`, keep the tags as they are and only translate the text inside.

### Exporting all dialogue for context

These are developer tools, so first turn on **Options → Mods → Drag'n Wash ModFramework → Developer tools**. It's off for players, so nothing below runs for them. Then load a save and press **F6** in the game. The plugin exports all the dialogue to:

`BepInEx/plugins/DragNWashLocalization/Translations/_discovered/dialogue_lines.csv`

The export has been checked on a real game install, and it came out with 1,839 lines.

Lines are listed in the order they play in the game. The `node` column tells you which conversation a line belongs to, with names like `Alexander_2_intro` that follow the pattern "character name_occurrence_scene". The `order` column is the line's position in that conversation. The `kind` column tells character dialogue (`line`) apart from player choices (`option`). With that context it's easier to see who's talking and what each reply is about. The game's three dragons are Conrad, Ryan and Alexander.

Copy the lines you want to translate into `Translations/<locale>/strings.csv`, fill in the `translation` column, and check them in the game. You can leave extra columns like `node` and `key` in place; the plugin still loads the file fine.

**Before you open a pull request, rebuild the published file** with the in-game **Hash for commit** button (or `tools/hash-strings.ps1`). The automatic check only accepts the published headers: `key,section,node,order,speaker,translation`, which is what *Hash for commit* writes, or the shorter `key,speaker,translation` and `key,translation`. A file that still has `source_en` and the other export columns gets rejected. A pull request carrying the English script is the one thing this repository is set up to avoid. See [CONTRIBUTING.md](CONTRIBUTING.md#hash-before-committing).

Lines you've already translated are exported with your translations filled in, so exporting again won't throw away your work.

### Previewing edits without restarting the game

If you save `Translations/<current-language>/strings.csv`, or the working copy `_discovered/<locale>.working.csv`, while the game is running, the plugin reloads it by itself after about two seconds and updates any text that's on screen right away. Every changed line is listed in the F1 Activity log.

So you can edit a translation and check it in the game over and over without restarting. You can turn this off with `[Debug] HotReloadTranslations`. Each reload that works shows up as a `[reload]` entry in the F1 Activity log.

### Exporting all UI text

Press **F7** to export all the UI text that's loaded to:

`Translations/_discovered/ui_texts.csv`

The export includes **hidden menus**, so you get every UI string in the current scene without having to open the pause menu or the confirmation dialogs. Its columns are `key`, `source_en`, `translation` (the existing translation, if there is one) and `object_path` (where the text appears in the UI).

Press F7 once on the title screen and once during gameplay and you'll have nearly all the UI text.

UI text that isn't translated yet is also recorded automatically while you play, in `Translations/_discovered/strings.csv`. That file gets cleaned up every time the game starts: translated entries and duplicates are removed, so what's left is an up-to-date list of the work still to do.

### Strings that do not need translation

Slider values, resolutions such as `1920 x 1080 @ 164.995Hz`, build numbers and strings like that are left out of discovery by default.

You can add your own exclusion patterns to [Translations/ignore.txt](Translations/ignore.txt). The file uses regular expressions and has examples in it.

Exclusions only affect discovery. The translation lookup happens first, so anything that's in `strings.csv` always gets translated, even if it matches an exclusion pattern.

### In-game debug menu

Press **F1** to open or close the tool window. It's shared with other mods built on Drag'n Wash ModFramework. The key is `[General] ToggleKey` in `BepInEx/config/com.tomxv.dragnwash.modframework.toolwindow.cfg`. Drag the title bar to move the window, and drag the bottom-right corner to resize it. This mod adds four tabs:

- **Activity log** shows translation results and processing logs. `Follow: ON/OFF` decides whether it scrolls to the newest entry by itself; scrolling by hand turns following off. `Clear log` clears the display and resets duplicate suppression. The log keeps the 100 most recent entries.
- **Translation** switches the language without a restart (the buttons show each language's name from `name.txt`, and **English** turns translation off). From here you can also export dialogue (`Export loaded dialogue`), UI text (`Export UI text`) or the working copy with the English next to each line (`Export working copy`), rebuild the published file (`Hash for commit`), and run the layout check.
- **Saves** lets you restore an earlier save, step the level index or toggle save flags. More on that below.
- **About** shows the version and build of the mod you're running, who made it, the license, and what this session loaded. It's handy to paste into a bug report.

The **Check translation layout** button exports strings that might overflow their layout to `Translations/_discovered/layout_risks.csv`. You set the threshold with `BepInEx/config/.../LayoutOverflowThreshold`. The default is `1.0`, which means an exact fit.

### Restoring the previous save for translation testing

Every time the game writes a save, the plugin keeps a versioned copy in:

`BepInEx/SaveHistory/<slot>/`

By default it keeps 30 versions per slot. You can change that with `[History] Keep` in `BepInEx/config/com.tomxv.dragnwash.modframework.saves.cfg`, or from the Mods screen. Copies that older versions of the mod kept in `BepInEx/plugins/DragNWashLocalization/SaveHistory` get moved there the first time the game starts.

Open **F1 → Saves**, pick a slot, and click **Restore** on the version you want. Then go back to the title screen and load that slot so the restored save takes effect. If you save again while playing, it overwrites the active save as usual.

The plugin also keeps the state from right before a restore, so you can get back if you went too far.

This is useful for going back to the same scene while you compare different versions of a dialogue translation. Restore just swaps in the game's own save file. It doesn't edit flags or variables.

The same tab also has a **PROGRESS** editor. Step the level index back or forward with **-** / **+** and press **Apply**. Going forward asks you to confirm first, because it can spoil things you haven't seen yet. **Flags...** lists every event flag the game is known to use, grouped (level flow, story, romance, scene triggers, scene watched, wash session, items, debug) with a short description, whether or not the save has set it yet. Click a value to cycle unset → true → false, type in the search box to filter, and use **Reset all to false...** to wipe every flag (the level index stays as it is). The list comes from `FlagCatalog.csv` next to the plugin DLL, so you can add rows for flags people find later. Every edit takes a snapshot of the save first.

## Crash when opening Options on Windows

With Unity 6000.3.14f1 and DirectX 12, the game has been seen crashing in `D3D12ScratchAllocator::DestroyScratch` when you open Options. Unity has an official issue report with the same stack trace: [UUM-140564](https://issuetracker.unity.com/issues/10698). It's a native rendering bug, so catching exceptions in the translation hook can't stop it.

The bug is set off when textures are allocated or uploaded at runtime. So on Direct3D 12 the plugin prepares the fonts for every installed language at startup, and after that neither playing nor switching language from Options or the F1 menu adds anything to a font atlas. Each character is only rasterized into the one font its language uses, which keeps that startup work small. This has been checked on a real machine, switching languages over and over as well. You don't need to configure anything.

Other graphics APIs (Direct3D 11, and Vulkan on the Steam Deck) cope with runtime uploads, so there only the language you're using is prepared, and the others load when you pick them. Set `[Font] PreloadAllLocales = true` if you want everything prepared at startup there too.

If the game still crashes, open Steam, go to **Drag'n Wash → Properties → General → Launch Options**, add `-force-d3d11` and restart the game. That switches the graphics API and gets around the problem. The option is one of [Unity's standard command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html), and it doesn't touch the game's DLLs or your save data.

The plugin's startup entry in `BepInEx/LogOutput.log` shows which graphics API is in use as `graphics=...`.

Lowering `[Fonts] AtlasPointSize` in `BepInEx/config/com.tomxv.dragnwash.modframework.assets.cfg` cuts down the number of font atlases. Raising it makes the text sharper. The default is 80.

## Freeze after switching windows in Exclusive fullscreen (Windows)

If **Window Mode** is set to **Exclusive** on DirectX 12, switching to another window (Alt+Tab, or clicking another window) and coming back can freeze the game, and then it crashes. The crash reports show Unity's DirectX 12 swap chain getting stuck while Windows takes the game out of exclusive fullscreen or puts it back in (`D3D12SwapChain::Present` fails with `887a0001`, often after `D3D12Fence::Wait ... May cause crash` in the log). No mod code is running at that point. The problem is in the game's graphics code, and this mod doesn't cause it.

To avoid it, do one of these:

- Set **Window Mode** to **Fullscreen** instead of **Exclusive**. Only exclusive fullscreen changes the display mode when you switch windows. We checked this on September 20, 2026: on DirectX 12, with the Steam overlay on, switching windows again and again in this mode didn't freeze the game, and there wasn't a single swap chain error in the log.
- Add `-force-d3d11` to the launch options: in Steam, **Drag'n Wash → Properties → General → Launch Options**. We checked this too, and with it switching windows in Exclusive fullscreen doesn't freeze any more.

> [!NOTE]
> On September 20, 2026 the freeze didn't happen in **Exclusive** either, on the same machine it was first found on, with the same graphics driver and the Steam overlay running. We don't know what changed. The framework's DirectX 12 fix in 1.3.0 got rid of a different crash, and this one wasn't it. So this section stays: if the game freezes when you switch windows, either of the two fixes above will get you out of it.

## Current status

v1.4.0 is the latest release. Text from other mods can be translated (experimental and off by default, [#28](https://github.com/TomXV/dragnwash-localization/issues/28)), and pictures can be translated per language ([#4](https://github.com/TomXV/dragnwash-localization/issues/4); that's the machinery, and no pictures have been drawn yet). It runs on Drag'n Wash ModFramework 1.4.0, which brings mods with no code, the operations registry and the Bridge.

Earlier releases, newest first:

- v1.3.0 brought fewer crashes on Direct3D 12 and a window that tells you what happened when the game does crash (with Drag'n Wash ModFramework 1.3.0).
- v1.2.1 made the Saves tab find saves made after the game update of September 14, 2026 again, and gave translators' working copies the English of screens that weren't open.
- v1.2.0 added Ukrainian, Thai and Vietnamese (16 languages), Korean proofread by a native speaker, translations that survive a game update, a logo, and Drag'n Wash ModFramework 1.2.0.
- v1.1.2 shipped ModFramework 1.1.2 with its hand-made Mods screen icon.
- v1.1.1 shipped ModFramework 1.1.1 with the framework's first icon.
- v1.1.0 made the Mods screen and the title screen tell you when a newer release of this mod is out (with ModFramework 1.1.0).
- v1.0.0 moved the mod onto [Drag'n Wash ModFramework](https://github.com/TomXV/dragnwash-modframework) and added the Mods screen.
- v0.6.2 stopped "Hash for commit" from dropping rows when the working copy is from before a game update, and translated "Really Delete Save?".
- v0.6.1 fixed names and other untranslated text showing backwards in Hebrew.
- v0.6.0 added per-line translations, so English that several characters say can be translated differently for each of them (checked with the game update of September 14, 2026).
- v0.5.0 added changing the language from the game's own Options screen.
- v0.4.0 brought thirteen languages, per-language fonts, an About tab and an installer in English, Japanese and Chinese.
- v0.3.0 added Steam Deck support.

Windows on ARM has been checked too (the game itself needs `-force-d3d11` there). macOS doesn't work right now because of a known issue on the BepInEx side (see the note under [Steam Deck / Linux](#steam-deck--linux-verified)). The BepInEx plugin skeleton, Japanese and Chinese replacement of UI and dialogue text, CJK font rendering, bulk dialogue and UI export, the in-game debug menu, layout overflow detection, the translator docs and the release workflow are all built and tested in the game.

[docs/PLAN.md](docs/PLAN.md) has the details, and [docs/ROADMAP.md](docs/ROADMAP.md) has what's coming next (translations for other mods too).

## Drag'n Wash ModFramework

From v1.0.0 this mod runs on **Drag'n Wash ModFramework**, a mod this one needs, and one that other Drag'n Wash mods can build on as well.

A lot of what this mod did to hook into the game is useful to other mods too, so that part now lives in the framework. That's the **Mods** screen (Options → Mods), which lists every installed mod with its settings and an on/off switch, the language row in the game's Options screen, rewriting text before it's shown, dialogue and choice events, the shared F1 tool window, fonts that are safe on Direct3D 12, and save history.

The point is that when the game updates, only the framework has to catch up, and the mods built on it keep working. The update of September 14, 2026 is the kind of change it deals with in one place.

For players, the release zip and the installers include the framework. From v1.1.0 the installers are the framework's shared ones, which any Drag'n Wash mod can ship. If you uninstall this mod while another mod is installed, the framework stays.

From v1.1.0 the title screen says **1 update available in Mods** when there's a newer release of this mod or the framework, and **Options → Mods** has a button that takes you to its release page. Once a day the framework asks GitHub for the latest release. It sends nothing about you or your game, and it doesn't download anything. You can switch it off in **Mods → Drag'n Wash ModFramework → Settings → Check for updates**.

For translators nothing changes: the CSV format and the translation tools are the same, and existing packs and contributions carry over.

The framework's docs live in its [wiki](https://github.com/TomXV/dragnwash-modframework/wiki). It covers the Mods screen, crash reports, the developer tools behind F1, and the [Inspector](https://github.com/TomXV/dragnwash-modframework/wiki/Inspector), which this mod doesn't ship (the wiki tells you how to add it).

If you make mods for Drag'n Wash and have ideas for what the framework should offer, please open an issue.

## Contributing translations

You don't need any code. To contribute a translation, edit `Translations/<locale>/strings.csv`.

[CONTRIBUTING.md](CONTRIBUTING.md) explains the workflow, the file format and how to find strings that aren't translated yet.

Everyone who takes part follows the [code of conduct](CODE_OF_CONDUCT.md). If you find a security problem, please report it privately instead of in an issue: [SECURITY.md](SECURITY.md). And if you'd like to and are able to, there's [GitHub Sponsors](https://github.com/sponsors/TomXV). The mod is free and stays free either way, and a translation is worth more.

## Distribution and releases

[docs/RELEASING.md](docs/RELEASING.md) explains how the release ZIP gets built and put out.

The reference assemblies that come from the game can't be committed here, so the ZIP is built by the **Build** workflow on GitHub Actions, which reads them from a private repository. Pushing a `v*` tag makes a draft release with the ZIP attached, and then a person writes the notes and publishes it.

## A note to the developers

This is an unofficial fan project and isn't affiliated with Gator Dragon Games. It follows Drag'n Wash ModFramework's [content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md) and doesn't contain any of the game's assets or script text as they are. English lines are only stored as SHA-256 hashes, and the game's files are never changed (BepInEx loads the plugin at runtime). If you're on the development team and have any concerns, please open an issue on this repository or contact the maintainer, and the project will be changed or taken down, whichever you prefer.

## Credits

- The mod's **logo** (its icon on the Mods screen, `icon.png`) was drawn by **Mister ERIO** ([@mistererio](https://github.com/mistererio)) and is used with permission.
- The **Mods button** on the Options screen, which ships with Drag'n Wash ModFramework, is Mister ERIO's work too.
- Drag'n Wash ModFramework's **logo and icon** (the icon ships in this zip) were drawn by **NotaGames** ([@NotaGames](https://github.com/NotaGames)).
- The Korean pack was proofread by **Hotcake**.
- Translators who improved a language pack are credited in the [Language packs](#language-packs) table.

## License

The plugin's code and the translations are under the MIT license ([LICENSE](LICENSE)). Each translation is still the work of the people who translated it, and they're named in the language table and under [Credits](#credits). The artwork listed under Credits belongs to its artists and isn't covered by the license. This repository doesn't include any assets or code from the game.
