# Drag'n Wash Localization Mod Plan

[日本語](PLAN.ja.md)

## Objective

Add BepInEx support to Drag'n Wash and enable localization into Japanese, Simplified Chinese, and additional languages in the future. Translators should be able to contribute without writing code by placing translation files in the prescribed format under `Translations/<locale>/`.

## Research findings (Steam version, as of 2026-09-11)

| Item | Finding |
|---|---|
| Engine | Unity **6000.3.14f1** (Unity 6.3) |
| Build type | **Mono**, not IL2CPP; x64 |
| UI strings | Unity Localization (`Unity.Localization.dll`) and StringTables delivered through Addressables (`StreamingAssets/aa/StandaloneWindows64/localization-locales_assets_all.bundle`) |
| Dialogue | **Yarn Spinner** (`YarnSpinner.dll`, `YarnSpinner.Unity.dll`, and `Yarn.CsvHelper.dll`), which includes CSV-based localization support |
| Fonts | TextMeshPro in Unity 6.3 supports Dynamic OS Font Fallback. CJK text can likely fall back to fonts bundled with Windows, such as Yu Gothic and Microsoft YaHei. |

### Conclusion

Japanese and Chinese localization is practical without modifying the game's DLLs or assets by replacing text at runtime with **BepInEx and Harmony**. Testing in Phase 1 showed that the initially planned Unity Localization hook was not suitable. The implementation instead hooks TextMeshPro text assignment itself, as described below.

## Findings from Phase 1 testing (2026-09-11)

- Although `Unity.Localization.dll` is bundled with the game, Harmony call-detection patches confirmed that the main menu, Options screen, and actual dialogue never call `LocalizeStringEvent`, `LocalizedString.RefreshString`, or `LocalizedStringDatabase.GenerateLocalizedString`. The hit count remained zero while navigating menus and displaying dialogue. Unity Localization is effectively an unused dependency in the current game.
- UI buttons, settings, and dialogue all reach the screen through the `TMPro.TMP_Text.text` property setter. This engine-level route is independent of whether Unity Localization is used, so hooking it captures and replaces nearly all in-game text.
- The translation format changed from the proposed table-name-and-key scheme to a simple mapping from **the exact English text shown on screen to its translation**. Translators do not need Unity's internal keys; they can copy text visible in the game into a CSV file.
- The game's TMP font assets do not contain Japanese or Chinese glyphs, so injected translations initially appeared as tofu boxes. This was fixed with Unity 6.3's `TMP_FontAsset.CreateFontAsset(familyName, styleName)` API in `AtlasPopulationMode.DynamicOS`. Dynamic TMP assets are created from Yu Gothic, Meiryo, or MS Gothic for Japanese and Microsoft YaHei for Chinese, then registered in `TMP_Settings.fallbackFontAssets`. No game font asset is modified. Japanese rendering was verified in the game.

## Final architecture

1. Install **BepInEx 5.4.23.5 (x64, Mono)** in the game directory. Installation and startup have been verified.
2. Load the custom BepInEx plugin `DragNWashLocalization`.
   - Harmony-patch the `TMPro.TMP_Text.text` setter and replace its `ref string value` in a Prefix when an external translation matches the source text.
   - Record unmatched source strings in `Translations/_discovered/strings.csv` so translators can see what remains.
   - Inject CJK fallback fonts at startup with `FontFallback.EnsureCjkFallback()`.
   - Provide a BepInEx `TargetLocale` setting for `ja`, `zh-Hans`, or a manually supplied locale.
3. Use a contributor-friendly translation format.
   - `Translations/<locale>/strings.csv` originally used the two columns `source_en,translation`; the public format later changed to hashed keys, as documented below.
   - UI and dialogue share one file because they use the same hook.
   - Translators use the locally generated `Translations/_discovered/strings.csv`, fill the corresponding rows, and submit a pull request.

## Repository layout

```text
dragnwash-localization/
  README.md                    Project overview and translator instructions
  LICENSE
  docs/PLAN.md                 This document
  src/DragNWashLocalization/   BepInEx plugin C# project
    Plugin.cs                  Entry point, Harmony setup, and configuration
    TmpTextPatches.cs          Harmony patches for TMP_Text.text
    TranslationStore.cs        Translation loading, lookup, and discovery output
    CsvReader.cs               Small dependency-free CSV parser
    FontFallback.cs            Dynamic CJK fallback creation and registration
  Translations/
    ja/strings.csv
    zh-Hans/strings.csv
  tools/                       Helper scripts, including export tools
  .gitignore
```

The repository contains no game assets or code. Only the plugin source and translation files are distributed.

## Phase 1.5: Bulk dialogue export and debug UI (2026-09-11)

Two features were added to improve the translator workflow and verified in the game.

- **`DialogueDumper` (F6):** Reads the internal `entries` table in `YarnProject.baseLocalization` through reflection and writes every loaded dialogue line to `Translations/_discovered/dialogue_lines.csv`. A test installation exported **1,839 lines** at once. Translators do not need to trigger every conversation; the relevant scene only needs to have loaded the YarnProject into memory.
  - Lines containing TMP formatting such as `<gradient="gold"><b>...</b></gradient><size=70%>...` are exported intact. No additional CSV columns are needed: translators preserve the tags and translate only the enclosed text.
  - Because the export contains copyrighted game dialogue, it is excluded by `.gitignore`. Each translator regenerates it locally by pressing F6 after installing the plugin.
- **Debug menu (F1):** A small `OnGUI` overlay discovers locale directories under `Translations/`, presents them as buttons, and switches languages immediately without restarting. It can also trigger dialogue export. The game's Options screen is untouched, reducing coupling to game updates.

## Phase 2: Replacement in real dialogue (2026-09-11)

### Discovery: `TMP_Text.SetText()` bypasses the `text` property

Translations failed to appear in some NPC speech bubbles. Yarn Spinner's standard `LetterTypewriter` and `WordTypewriter` use `Text.text = ...`, but the game's custom bubble component calls `TMP_Text.SetText(string)` directly. Decompilation confirmed that `SetText` writes the internal `m_text` field without invoking the property setter.

The shared rewrite logic, `TmpTextHook.Rewrite`, was therefore patched into both `TMP_Text.SetText(string)` and `SetText(string, bool)`. This fixed the issue in the game.

The lesson is that TMP exposes several text-assignment APIs. Another unhandled route may still exist. If likely text never appears as an untranslated `[--]` entry in the debug log, suspect a separate assignment path.

### Debugging improvements

Based on user feedback, the following were added:

- An in-game log viewer under F1 that shows `[OK] "source" -> "translation"` and `[--] "untranslated source"` in real time.
- Automatic scrolling and a dark theme because Unity's default white `OnGUI` skin was difficult to read.
- A fix for **Clear log** so it resets both visible entries and internal duplicate suppression. Previously, cleared strings could never appear again.

## Phase 3: Full translations and layout validation (2026-09-11)

Phase 3 aimed to add substantial Japanese and Simplified Chinese translations and verify that CJK text does not break the layout. Translation and tooling were completed first, with visual testing initially left as QA.

### Translation additions

- Added `Translations/zh-Hans/strings.csv` with Simplified Chinese versions of all 95 entries then present in the Japanese file, in the same key order. Microsoft YaHei was selected as the default Chinese font; actual rendering remained to be tested at that stage.
- Added common Unity UI strings to `Translations/ja/strings.csv`, including Play, Continue, Pause, Settings, Language, Volume, Apply, and Confirm. These were best-effort additions before checking which strings the game actually displays. Nonmatching entries are harmless; actual unmatched text is still recorded in `_discovered/strings.csv`.
- Because matching is exact, each source must reproduce the displayed English verbatim. Contributors generate their own ignored discovery file and use it to expand the translations.

### Layout overflow detection

CJK glyphs can be approximately twice as wide as Latin characters, so translated text can overflow a control even when it contains fewer characters. `LayoutChecker` was added to identify likely risks before visual inspection.

- The first heuristic assigned width 2 to CJK and other full-width glyphs and 1 to all others. It did not inspect font metrics, wrapping, or container width and was intended only for triage.
- It ran at startup and from **Check layout** in the debug menu. `[Debug] LayoutRiskThreshold` defaulted to `1.4`; entries were reported when estimated translation width exceeded source width by that factor.
- Results were written to `Translations/_discovered/layout_risks.csv` with `source_en,translation,source_width,translation_width,ratio`.

### Work remaining at that point

- Switch between `ja` and `zh-Hans` in the game and visually inspect strings reported in `layout_risks.csv`, especially on the Options screen and in dialogue bubbles.
- Verify actual Chinese rendering with Microsoft YaHei.
- Continue expanding translations from `_discovered/strings.csv`.

## Missing static UI text (2026-09-11)

`Pause Menu`, `Resume`, `Options`, and `Quit` remained in English and appeared in neither the log nor the discovery file because the hook was never reached.

TMP text configured on a prefab is deserialized directly into `m_text` and may never be assigned at runtime. Neither the property setter nor `SetText` is called. This was the third assignment route found, and unlike the `SetText` bypass, it involved no call to intercept. The same cause affected `New Game`, `Credits`, `Quit`, `Back`, and `Options` on the main menu.

The fix had two parts:

- Add Postfix patches to `TextMeshProUGUI.OnEnable` and `TextMeshPro.OnEnable`. When a component becomes active, translate its current text. Components already seen through a setter are skipped to prevent duplicate work, and `SuppressRewrite` prevents re-entry while assigning the translation.
- Add `UiTextDumper`. `Resources.FindObjectsOfTypeAll<TMP_Text>()` includes inactive objects, so F7 can export every UI string in the loaded scene to `_discovered/ui_texts.csv` without opening every menu. If a component already contains a translation, `TmpTextHook.TryGetTrackedSource` recovers its English source.

### Incorrect conclusion during investigation

A length-prefixed asset search found `Resume` in the animation bone name `Resume.Bone.004`, which led to the incorrect conclusion that it was not display text. The bone name and the pause-menu label were separate occurrences. Asset search is useful for showing that something exists, but weak evidence that a string is not UI text. Future decisions should rely on the actual output of `ui_texts.csv`.

Buttons whose labels are designed as images, including `Back`, `Save`, `Set Default`, and the `OPTIONS` heading, are intentionally outside the translation scope.

## IMGUI input leaking into the game (2026-09-11)

Clicks on the debug window also activated the game UI behind it. IMGUI does not stop the game from reading input. uGUI reads through `EventSystem`, while the game reads through the Input System; neither observes IMGUI.

`InputBlocker` was added to disable `EventSystem` and call `PlayerInput.DeactivateInput()` only while the pointer is over the debug window. Restricting the block to pointer hover preserves the ability to play while leaving the log visible. While a mouse button is held, the block remains active even if the pointer leaves the window, preventing input from returning to the game mid-drag when moving or resizing the window beyond a screen edge. `OnDestroy` always restores input.

## LayoutChecker changed to measured dimensions (2026-09-11)

The first implementation compared character widths using full-width = 2 and half-width = 1, but ignored the container. It reported comfortable labels such as `Audio → オーディオ` and `Options → オプション` as 2.0× risks even though screenshots showed ample space. False positives prevented it from serving as useful triage.

The checker now asks TMP directly. `GetPreferredValues` measures the translated text with the component's font, size, and spacing. Comparing the result to `rectTransform.rect` reveals whether it actually fits. For text without wrapping, the checker compares width. With wrapping, it compares the required height under the `rect.width` constraint. Output now includes `axis`, `required_px`, `available_px`, and `object_path`.

The setting was renamed from `LayoutRiskThreshold` to `LayoutOverflowThreshold`, with a default of `1.0`. Its meaning changed from source-text ratio to container ratio. Leaving an old value of `1.4` in an existing config would hide real overflow.

## Complete UI string inventory (2026-09-11)

Running F7 in the game collected all 40 loaded TMP text objects. Including `object_path` made each string identifiable without guessing.

- 35 were already translated.
- `Option A` belongs to the Unity dropdown **Template**, and `Option Text` is the Yarn choice prefab placeholder. Both are replaced at runtime and never displayed as written. A zero-width space is inserted by TMP into an empty input field. All three were added to `ignore.txt`.
- The only meaningful untranslated entry was the input placeholder `Enter text...`.

Two bugs were found:

- `UiTextDumper` called `Trim()` before looking up translations. The version label is actually `"Version\n"`, so it appeared as the nonexistent untranslated key `"Version"`. Adding `Version` would never match. Trimming is now used only to test whether a string is empty.
- `LayoutChecker.Report` was called from `Awake`, but after switching to actual measurements, the scene contained no targets at that point and returned `No translated text is on screen yet`. The startup call was removed; checks run only from the debug-menu button.

This completed the UI translation inventory. Visual layout review and dialogue volume remained.

## Cascading failure caused by the legacy input API (2026-09-11)

After adding `InputBlocker`, both layout checking and UI export stopped working because of one exception:

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input
class, but you have switched active Input handling to Input System package.
```

The game is configured for the new Input System only, so legacy `UnityEngine.Input` throws. Using `Input.mousePosition` was incorrect. BepInEx's `KeyboardShortcut` internally selects between input systems, which made the F1 hotkey work and led to the false assumption that the legacy API was available.

The exception affected unrelated features because `UpdateInputBlocking()` ran near the beginning of `Update()`. It threw every frame and prevented later dump and layout code from running. One fault disabled two separate features.

The fix:

- Read the pointer from `Mouse.current`.
- Move `UpdateInputBlocking()` to the **end** of `Update()`, wrap it in a try/catch, and permanently disable it after the first failure to avoid throwing every frame. Both ordering and isolation matter; either fix alone leaves room for a similar cascade.
- Remove the now-unused `UnityEngine.InputLegacyModule` reference and DLL so this project cannot accidentally use an invalid API.
- Log whether an `EventSystem` exists and how many `PlayerInput` instances were suspended, because games that drive InputActions directly may not be blocked.

BepInEx utilities working does not prove that a Unity API is safe to call directly. The order of work inside `Update()` determines which features an exception can take down.

## Wrong input-blocking API (2026-09-11)

The implementation used `PlayerInput.DeactivateInput()`, but the game has no `PlayerInput` component. Inspection of `Assembly-CSharp` showed:

```text
PlayerInput            0
InputActionAsset       1
InputActionReference   1
InputActionMap         1
```

Because `PlayerInput.all` was empty, the block silently did nothing. The implementation now enumerates `InputActionAsset` objects with `Resources.FindObjectsOfTypeAll` and disables only enabled `InputActionMap` instances. On restoration, it re-enables only maps that were originally enabled. Calling `InputActionAsset.Enable()` would enable every map and change the previous state.

If nothing is stopped, the plugin states this explicitly: `No enabled action maps were found, so gameplay input is NOT blocked.` A no-op must not be reported as success.

## Process retrospective (2026-09-11)

A user reported that neither fix worked. Investigation found that the corrected DLL had not been deployed. Deployment was waiting for the game to exit, so testing while the game remained open used the old DLL.

Changes that could not be exercised locally had also been added in succession, causing defects to accumulate before being discovered. Future work must follow these rules:

- Before asking someone to test, verify and report that the deployed DLL hash matches the built artifact.
- Do not ask for testing based only on an expectation. Perform all available checks first; for example, inspecting the game assemblies could have shown that `PlayerInput` was absent.

## Phase 4: Contributor documentation and distribution (2026-09-11)

The contributor and maintainer paths were completed.

- **`CONTRIBUTING.md`:** Explains the two-column `source_en,translation` format used at that point, exact matching, formatting tags, F6/F7 discovery, automatic recording, `ignore.txt`, adding locales, pre-PR checks, and the rule against committing game assets or `_discovered/`.
- **`docs/RELEASING.md`:** Documents local release builds. Game-derived assemblies in `libs/` cannot be committed, so releases cannot be built in CI and are uploaded from a local environment.
- **`tools/pack.ps1`:** Automates `dotnet build` and creation of `release/DragNWashLocalization-<version>.zip`. The archive contains `BepInEx/plugins/DragNWashLocalization/{dll, Translations/}` and `README.md` in a structure that can be extracted at the game root. Runtime-generated `_discovered/` content is excluded.
- Added contribution and release links to the README, and ignored `release/` and `*.zip`.

## Dialogue export ordered by execution (2026-09-12)

`dialogue_lines.csv` was originally sorted by `line_id`, but IDs are content hashes and have no relation to narrative order. The export did not reveal who replied to whom, which response belonged to a question, or where conversations began, making meaningful translation impractical.

The compiled `Yarn.Program` retains execution order. Each node represents a conversation, and its `Instructions` run from top to bottom. Walking `RunLine` and `AddOption` restores script order and distinguishes dialogue from choices. This is equivalent to the traversal behind `Program.LineIDsForNode`, with custom handling to retain the entry type.

The columns are now `yarn_project,node,order,kind,line_id,source_en,translation,tags`. Existing translations are included so another export does not lose work. Lines not referenced by any node are appended with `(not reached from any node)` so translators can distinguish unreachable content from an incomplete export.

Testing found all 1,839 lines in one of 195 nodes and no unreferenced lines. Node names follow patterns such as `Alexander_2_intro`, representing character, occurrence, and scene. The three main characters account for Conrad 547 lines, Ryan 507, and Alexander 472. `RyanMuddy`, `ConradBeatup`, and `RyanDate` are alternate states of the same characters.

YarnProject is not loaded on the title screen. Press F6 after loading a save.

## Bulk-translation reconciliation and cleanup (2026-09-12)

After the Japanese CSV grew to 1,660 entries, it was compared with the execution-ordered export:

- 1,566 of 1,601 unique dialogue sources matched, or 97.8%.
- Of 35 apparently untranslated entries, 33 were development strings such as `Test line N`, `title: X_Done`, and `==`, all excluded by `ignore.txt`. The only real omissions were `Yes!` and `no`.
- Another 72 source strings matched no dump. Twenty-one were valid UI elements such as Options-screen text not loaded during F7. The rest were **partial dialogue keys**. Exact matching meant they could never apply.

Partial keys were matched against actual lines using substring checks followed by `difflib` similarity. Forty-six redundant partials were removed when the real line was already translated; no translation needed to be moved. `Yes!` became `はい！`, and `no` became `いいえ。`, consistent with existing variants.

Five unmatched strings were retained because they might come from another build or be transcription errors. They are harmless because they simply never match.

The result was 1,616 entries with no blanks or duplicates. Of 1,601 dialogue sources, 1,568 were translated and the remaining 33 were excluded development text: dialogue was effectively complete. Prewarming all 978 characters from the 1,616 entries succeeded in the game without exceptions. Simplified Chinese still contained 95 entries at this stage.

## Complete rewrite of the Japanese translation (2026-09-12)

The first bulk translation misunderstood slang and character voices. Conrad's Australian slang was translated literally, sometimes reversing the meaning: `Yeah, nah` means no, while his speech also includes `G'day mate!` and `Good as!`. Alexander was supposed to use the grandiose `〜でございます` style but instead spoke casually, losing his character.

Following `TRANSLATION_STYLE.md`, 1,561 unique dialogue lines were retranslated by character and in execution order.

- **Conrad:** Rough endings such as `〜だぜ` and `〜だろ`. `Yeah, nah` means 「いや」, `Grouse as / Good as` becomes 「最高じゃねえか」, `Damn right` becomes 「あったりめぇだ」, and `'aight I'm down` becomes 「いいぜ、乗った。」. ConradBeatup remains masculine but subdued.
- **Ryan:** Affectionate endings such as `〜だよ` and `〜なんだ`. Lines spoken with a basket in his mouth use deliberately muffled spelling. Calls from franchise headquarters use separate, businesslike polite language.
- **Alexander:** Grandiose forms such as `〜でございます`, `〜でありまして`, and `貴殿`, with his fixation on his crest, university, and reputation. Long speeches with progressively shrinking text preserve every tag position.
- **Kobold choices (491 lines):** Primarily polite `です・ます`, with occasional friendly `〜だね`. `Yip!` is 「イップ！」 and `Yip! (Yes)` is 「イップ！（うん）」.
- **Ryan and Conrad scenes:** Speaker prefixes use full-width colons, and each speaker retains his own voice.

The execution-ordered export was split by character and translated in batches of roughly 140–150 lines, stored as JSON, and reassembled with mechanical checks for exact source keys, duplicates, blanks, matching TMP tag counts, and complete coverage of non-ignored lines. Two detected errors added `</i>` where the source left `<i>` open; both were corrected to match the source exactly.

The final set contained 48 UI entries and 1,561 dialogue entries, for 1,609 total. In-game startup prewarmed all 1,007 characters without exceptions. Yarn sample content from the `Start` node and development error messages were added to `ignore.txt`.

## Higher font resolution (2026-09-12)

Sharper text was requested. Clarity is controlled by `[Font] AtlasPointSize`, the SDF sampling point size. It had been reduced to 40 while minimizing atlas count during the Direct3D 12 crash investigation. Once all glyph creation was moved to startup, runtime atlas expansion was no longer the risk; increasing the value only adds startup atlas-generation cost.

The default was raised from 40 to 80 in both code and configuration. A real-game test prewarmed all 1,007 characters without exceptions, and magnified Japanese text on the Options screen had visibly sharper contours. The README was updated, including replacement of the obsolete `LayoutRiskThreshold` name with `LayoutOverflowThreshold`.

## Complete Simplified Chinese translation and neutral reused lines (2026-09-12)

The same workflow produced Simplified Chinese translations for 1,561 dialogue lines, for 1,604 entries including 43 UI strings. Conrad uses a rough masculine voice while avoiding excessive 「老兄」 and 「操」; Ryan uses softer particles such as 「呢」「呀」「啦」; Alexander uses literary honorifics such as 「在下」「阁下」「乃是」「甚是」; and kobold choices use 「您」 and 「请」. Exact-key, duplicate, blank, TMP-tag-count, and non-ignored-coverage checks all passed.

A player then pointed out that `What's up?` should mean 「どうしました？」 in its scene. This exposed a structural constraint: exact matching gives the **same English string one translation in every scene**, while short stock phrases can occur in multiple nodes and be spoken by different characters. Translating only within individual batches had produced context-specific results for reused strings such as `Whats up?`, `I'm excited.`, `I'm glad.`, `Hey there.`, `Me too.`, `Thanks.`, `Aw, thanks.`, and `I am too.`.

All English strings of 22 characters or fewer that appeared in multiple nodes—76 entries—were listed with the preceding line for every occurrence. Mixed-speaker entries were changed to neutral translations that work in every scene: ten Japanese and seven Chinese entries changed.

The lesson is to inspect every occurrence and speaker before translating a reused stock phrase. This occurrence-listing script should be used for future additions. User feedback confirmed that overall character consistency was otherwise sound.

## Translation hot reload (2026-09-12)

The existing language-switch path, `TranslationStore.Load → TmpTextHook.RefreshAll`, already reloads data and reapplies it to visible text. Hot reload only needed to detect file changes and call that path.

`HotReload` polls once per second from `Update` rather than using `FileSystemWatcher`. Watchers run on a thread-pool thread, often emit several events for one save, and may fire while an editor still holds the file. Reload begins only after the modification timestamp remains stable for two consecutive polls, avoiding partially written CSV data.

The required order is reload, **prewarm newly introduced characters**, then reapply. Otherwise, the first render of a new glyph would expand the atlas at runtime and re-enter the Direct3D 12 crash path. `FontFallback.Prewarm` therefore generates only the delta before the UI refresh. This performs a small texture update during gameplay, but it is safer than leaving an inevitable uncontrolled expansion.

Only the active locale's `strings.csv` is watched. `ignore.txt` remains load-once and requires a restart after editing.

## Removing the English script from public translation files (2026-09-12)

Publishing `strings.csv` with 1,561 lines of the game's English dialogue as keys would expose the same copyrighted material that caused `_discovered/` to be ignored. The goal was to require ownership of the full game for convenient translation.

Source text keys were replaced with the **first 16 hexadecimal characters of SHA-256**, represented by `TranslationKey`.

- The plugin hashes English text as it appears and looks up the result through `TranslationStore.KeyFor`, with a per-string cache.
- Both `key` and `source_en` rows are accepted in one file. Translators can work with source text and hot reload, then convert it before committing.
- Conversion is available through **F1 → Tools → Hash strings.csv for commit** and `tools/hash-strings.ps1`. Both paths produced identical output for all 1,609 Japanese rows.
- F6 and F7 exports include a `key` column so owners can see the source-to-key mapping.
- Rows with conflicting `key` and `source_en` values, or keys that are not 16 hexadecimal characters, are treated as invalid, skipped, and counted in the log. They do not silently create unusable translations.
- Source-bearing working copies belong under `_discovered/<locale>.working.csv`.

A request to authenticate through Steam and expand hashes back into source text did not require the Steam API. The plugin running inside the installed game already establishes access to the game data. `WorkingCopy` therefore compares the public hashed file against the loaded Yarn script, all TMP text in the scene, and discovered strings. It expands them in execution order into `strings.local.csv` with `key,source_en,translation`. `TranslationStore` overlays this working file on the public file, and hot reload watches both. **Hash for commit** regenerates the public file from the working copy when it exists. The workflow becomes expand, edit with source context, see changes immediately, hash, and submit a PR.

Because earlier Git history contained many full-source CSV commits, hashing the latest files was not enough. Before publication, history needed to be squashed to one commit while retaining development history in this plan. Dialogue excerpts in documentation were reduced to short fragments.

## Save rollback (2026-09-12)

Translation testing required returning to a previous save. Game state consists of a static `Flags` registry plus Yarn variables. `SaveManager` writes JSON to `savegame.dgn` under `<persistentDataPath>/<SteamID>_slot<N>/` and keeps only one backup.

A flag editor was considered, but restoring correctly would require knowing flag semantics and synchronizing Yarn state through `WalkNWashSceneState._SetFlag`. Versioning and replacing the game's own save file is more reliable and requires no interpretation.

`SaveHistory` polls each slot's `savegame.dgn` timestamp every two seconds. When content changes, it copies the file to `SaveHistory/<slot>/<timestamp>.dgn`, keeping 30 versions by default. The F1 **Saves** tab lists slots and versions and offers **Restore**. The state immediately before restoration is also archived. Restoration changes only the file; the player must return to the title screen and load the slot because in-memory flags are untouched. Saving during gameplay naturally overwrites the active save again. The list extracts `levelIndex` from JSON for identification.

## Public release and installer, v0.1.1 (2026-09-12)

- **Public repository.** The git history still carried the plain-English CSVs from before hashing, so it was squashed to a single commit (the full history is kept in a local branch) before the repository was made public. A ruleset protects `main`: pull requests only, no force-push or deletion, the translation check required, administrators may bypass.
- **Pull-request check.** `check-translations.py` validates the published files on every pull request. A second workflow (`workflow_run`, so it works for forks) posts one explanatory comment when the check fails and rewrites it to a pass message after a fix. The report lists line numbers only, never the rejected text, because the log is public.
- **Installer.** `Install.exe` (a console-less launcher compiled at pack time with the C# compiler shipped in .NET Framework 4) opens `installer/Installer.ps1`, a WinForms window that finds the game through Steam, downloads BepInEx 5.4.23.5 when missing (SHA-256 pinned), copies the mod, writes `TargetLocale`, and uninstalls. Save-history snapshots are kept by default; BepInEx is removed only when the installer put it there and no other plugin uses it. A `.cmd` launcher was tried first and rejected because it always shows a console window.
- **English pass-through.** `TargetLocale=en` loads nothing, so the mod can stay installed while the game shows its own text. Offered in the installer and the F1 menu.
- **Language names.** `Translations/<locale>/name.txt` supplies the label shown on the F1 buttons and in the installer, so adding a language needs no code change.
- **Save progress editor.** The Saves tab can step `levelIndex` (moving forward asks for confirmation because it can spoil content) and toggle the save's boolean flags, for example the `finished_watching_*` markers the game sets after a cutscene. Every edit snapshots the save first. Edits are regex replacements on the save text so the game's own layout is preserved.
- **Typewriter fix.** Replacing on-screen text on a locale switch or hot reload left `maxVisibleCharacters` at the old text's length, truncating longer text ("HEY! This is th"). The cap is lifted when the previous text was fully shown.
- **Versioning.** The csproj now emits assembly attributes (`Version`/`FileVersion`), and releases are built after the commit and tag so the informational version carries the tagged commit.

## Play-ordered translation files, v0.2.0 (2026-09-12)

- The game's `LevelFlow` asset (15 levels, all present regardless of progress) and the Yarn program are exported in-game (`FlowDumper`, `ScriptOrder.Generate`) into `data/script_order.csv`: node names, line ids, hashes and speakers, no English. `Hash for commit`, the working copy and `tools/hash-strings.ps1` order rows by it and print `#` section headers; `CsvReader` and the CI check skip comments and blank lines. The in-game and PowerShell outputs were verified byte-identical.
- Static audit of the flow found no unreachable references on the live path; 40 "untranslated" lines were developer debris (test node, Yarn separators, error text). The error/debug lines were translated in case a broken flag state shows them.
- Level 14's mount dialogues point at the `Conrad_4_Mount_*` nodes while `Conrad_5_Mount_*` exist unused; reported as a likely game data mistake, not touched.

## Phases

- **Phase 0:** Create the repository and finalize the plan — complete.
- **Phase 1:** Install BepInEx, create the plugin skeleton, replace UI text, and verify CJK fonts in the game — **complete**, after changing the original hook design.
- **Phase 1.5:** Add bulk dialogue export and the in-game debug UI — **complete**.
- **Phase 2:** Verify replacement in real dialogue — **complete**, after finding and fixing the `SetText` bypass.
- **Phase 3:** Add substantial Japanese and Chinese translations and check UI layouts — **complete**. UI inventory is complete, layout detection uses TMP measurements, and dialogue translation remains maintainable.
- **Phase 4:** Complete `CONTRIBUTING.md` and establish distribution through GitHub Releases — **complete**.
- **Phase 5:** Public repository, pull-request checks, installer, and the v0.1.1 release — **complete**.

## Risks and items to verify

- Exact matching can be awkward for dynamic strings containing values or placeholders. Consider partial matching or format strings if these occur often.
- Steam updates may change the versions or internals of TMP_Text or Yarn Spinner. Add version checks.
- Microsoft YaHei registration was confirmed, but actual Chinese display still required verification at the time this risk was recorded.
- `DialogueDumper` reads an internal `Localization` field through reflection and may break if Yarn Spinner changes its layout.
- TMP has text APIs beyond `SetText`, including `SetCharArray`. None have caused a problem yet, but another bypass may appear.

## Investigation of the Options crash (2026-09-11)

- All nine saved crash logs ended on the render thread in `D3D12ScratchAllocator::DestroyScratch → ReleaseExcessScratch → ReclaimMemory` under Unity 6000.3.14f1, Direct3D 12, and a GeForce RTX 3060.
- Unity issue [UUM-140564](https://issuetracker.unity.com/issues/10698) reports the same stack. These logs do not support earlier theories involving CSV writes or recursive string setters.
- The workaround is the Steam launch option `-force-d3d11`. The graphics API is selected before plugin initialization, so it must be supplied on restart and cannot be switched dynamically by the plugin.
- Diagnostic setter Postfixes were removed, restoring the regular translation Prefixes for the setter, `SetText(string)`, and `SetText(string, bool)`. Queueing of untranslated strings and duplicate-suppressed debug logging were also restored.
- Startup logging now reports the graphics API and warns about the affected Unity and Direct3D 12 combination. The dynamic-font setup was initially left unchanged.
- A real-game test with the corrected DLL and `-force-d3d11` opened, scrolled, and closed Options without a crash while displaying Japanese. Startup logged `graphics=Direct3D11`.

### Root fix without a launch option

The trigger itself was removed so the workaround would no longer be required.

`TMP_FontAsset.CreateFontAsset(familyName, styleName)` creates a `DynamicOS` asset with a 1024×1024 target atlas, but its texture begins as a dummy `new Texture2D(1, 1, ...)`. Decompilation showed this sequence:

1. Adding the first glyph calls `Reinitialize(1024, 1024)` and `ResetAtlasTexture`, reallocating a texture at runtime.
2. Every batch of new glyphs calls `UpdateAtlasTexture() → Apply()`, uploading to the GPU at runtime.
3. When an atlas fills, `SetupNewAtlasTexture() → new Texture2D(...)` performs another runtime allocation.

A frame displaying a character for the first time can therefore allocate and upload a texture. Options reliably triggered the crash because it introduces many previously hidden strings at once. Although the atlas width and height setters are internal, public `TryAddCharacters(string, out string, bool)` works for `DynamicOS` assets and can move glyph generation to startup.

The implementation changed as follows:

- Prewarm every non-ASCII character used by loaded translations with `FontFallback.Prewarm`, preventing atlas growth during normal gameplay.
- Reduce fallback fonts to one per language instead of six total, cutting the number of atlas textures.
- Create the debug-menu background `Texture2D` during `Awake`. Previously the first press of F1 called `Apply()`, which could crash when Options was already open.
- Move language switching and dialogue export from `OnGUI` to `Update`, keeping file I/O and glyph generation out of the rendering callback.
- Make atlas resolution configurable through `[Font] AtlasPointSize`, initially defaulting to 64.

Testing without `-force-d3d11` confirmed `graphics=Direct3D12`. Options could be opened and scrolled, and Japanese dialogue rendered correctly. Both fonts logged `Prewarmed 175/175 characters` with no missing glyphs. The F1 debug menu still triggered the crash, leading to the next investigation.

### F1 debug-menu crash in IMGUI

The captured stack was decisive:

```text
D3D12ScratchAllocator::DestroyScratch
D3D12ScratchAllocator::ReleaseExcessScratch
D3D12ScratchAllocator::ReclaimMemory
GfxDeviceD3D12::QueueExecute
GfxDeviceD3D12::QueuePresent
GfxDeviceD3D12::PresentFrame
GfxDeviceWorker::RunCommand
```

The `PresentFrame` path indicates a limit related to scratch-memory use within one frame, with the crash occurring when excess memory is reclaimed during presentation. Font prewarming eliminated the Options-specific route but not the underlying condition. F1 was an especially severe trigger because TMP and IMGUI load occurred in the same frame.

The IMGUI implementation was changed:

- Instead of rendering up to 200 individual `GUILayout.Label` calls each frame, join the full log into one label and rebuild it only when content changes. This greatly reduces meshes and draw calls.
- The log contains translated Japanese, so IMGUI's dynamic font was also expanding a texture at runtime. Create a dedicated menu font with `Font.CreateDynamicFontFromOSFont` and prewarm ASCII plus all translation characters through `RequestCharactersInTexture`.
- Reduce the maximum visible log from 200 entries to 100.

A separate bug was found at the same time. Change detection used `LogBuffer.Count`; when the buffer reached capacity, the count stopped changing and the display froze. It now uses a version number incremented on every update. `richText` is disabled so IMGUI does not interpret dialogue tags such as `<i>` or `<gradient>`.

A real-game test confirmed that F1 no longer crashes under Direct3D 12, including while Options is open.

### Ignored strings (`IgnoreRules`)

Because the TMP hook sees all visible strings, slider values, resolutions, and build numbers such as `9/9/2026_ee944596` overwhelmed the untranslated list.

Built-in regular expressions now exclude standalone numbers, resolutions, refresh rates, date/build identifiers, and times. Additional patterns can be added in `Translations/ignore.txt`.

Exclusions affect only discovery and logging, never lookup. `TryGetTranslation` runs first, so a row explicitly placed in `strings.csv` is translated even if an exclusion pattern matches it. A mistaken exclusion can therefore be overridden.

The initial numeric expression, `^[+-]?[\\d.,]+\\s*%?$`, also matched the dialogue line `...`. It was fixed to require at least one digit and validated against 34 real strings.

Validation included a clean Release build with no warnings or errors. A temporary test applied the production Harmony patches to TMP substitutes and passed 15 cases covering all three entry points, prevention of re-assignment, preservation of the Boolean argument, numeric formatting, null and empty strings, duplicate log suppression, delayed CSV output, embedded newlines and commas, configuration flags, and source preservation for an unavailable locale. This test did not exercise native rendering.

The built DLL was deployed to the game's BepInEx plugin directory, and its SHA-256 hash was verified against the build artifact.

## IMGUI debug-window restructuring (2026-09-11)

- Moved rendering into `Plugin.ImGui.cs` and organized the window into **Log** and **Tools** tabs.
- Added title-only movement, resizing from the lower-right corner, and bounds correction to keep the window on screen.
- Made automatic log following optional and stopped following when the user scrolls manually. Version-based updates continue after the buffer reaches capacity.
- Language switching, dialogue export, and layout checking continue to execute from `Update`.
- Every control uses the same prewarmed font and size. The log renders as one label, and its joined text and wrapped height are recalculated only after content changes. `OnGUI` performs no new `Texture2D` creation, `Apply`, or file I/O.
- The Release build succeeded. At the time of this entry, in-game appearance, interaction, and crash testing awaited user confirmation.
