# Contributing

[日本語](CONTRIBUTING.ja.md)

Drag'n Wash Localization lets you contribute a translation by **editing CSV files only**, with no code. This guide is for translators. Build and release steps for the plugin itself are in [docs/RELEASING.md](docs/RELEASING.md).

## What you need

- Drag'n Wash (Steam) with this mod installed (BepInEx)
- Any editor that can handle CSV (Excel, LibreOffice, VS Code, ...)

You do not need to know Unity's internal keys or how to program. While working you write the **exact English text shown in the game** in a `source_en` column, and convert it to a hash before committing (see "Hash before committing" below).

### Language display name (`name.txt`)

Whatever you put in `Translations/<locale>/name.txt` is shown on the language buttons in the in-game F1 menu and in the installer (for example `ja/name.txt` → `日本語`, `zh-Hans/name.txt` → `中文`). One line, UTF-8. Without the file the folder name is shown. Adding a language is just a folder, a `strings.csv` and a `name.txt`.

## Basic flow

1. Fork this repository.
2. Add or fix translations in `Translations/<locale>/strings.csv`.
3. Commit and open a pull request.

## File format

`Translations/<locale>/strings.csv` is the published file. Rows are ordered **the way the game plays them** and grouped under `#` headers. While working, rows with the English text may sit **in the same file**.

```csv
key,section,node,order,speaker,translation

# ===== Level 1: Ryan (Sunny) | sets level_1 | ends level_1_complete =====
# --- intro: Ryan_1_intro ---
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,Hi there. Is this the cleaning place?
# --- phone: Ryan_1_PhoneTutorial | if $has_talked_to_ryan ---
…
# ===== UI and other text (not part of the dialogue script) =====
bc1b88907d3b748a,UI,,,UI,Options
```

- `section` … level number and dragon such as `L01 Ryan`, or `Cutscene` / `Reaction` / `Unused` / `UI`
- `node` / `order` … the Yarn conversation node and the line's position in it. Branch nodes come right after their parent
- `#` lines … headers. They are ignored when loading, so leave them in. `| if $variable` is a hint about the branch condition
- The order comes from `data/script_order.csv` (node names, line ids, hashes and speakers only, no English) and is regenerated in-game with **Export game flow** (the maintainer does this when the game updates)

```csv
source_en,translation
Options,Optionen
```

- `key` … the first 16 hex digits of the SHA-256 of the source text. **Only this form is committed.** The repository never carries the game's English script, so nobody without the game can read the script or translate without the source in front of them.
- `speaker` … who says the line (Conrad / Ryan / Alexander / Kobold = the player's choice / Phone / UI). Filled in automatically from the script structure.
- `source_en` … the exact English text shown in the game (matched exactly). **Use this while working**: saving the file hot-reloads it into the running game.
- `translation` … your text.

The plugin hashes the English text it is about to show and looks it up, so both row forms behave the same. The F6 / F7 exports carry both `key` and `source_en`, which is how you can map one to the other.

### Starting a new language

1. Create `Translations/<locale>/` (for example `ko`) and write the display name into `name.txt` (for example `한국어`).
2. Start the game and pick the new language under F1 → Tools (the screen stays English, there are no translations yet).
3. Press **F1 → Tools → Export working copy**. Even without a `strings.csv` you get an empty working copy, `_discovered/<locale>.working.csv`, listing every line the game has loaded with its English text.
4. Continue as in "Working with the English beside each line". Lines show up in the game as you translate them.

### Working with the English beside each line (recommended)

**F1 → Tools → Export working copy** expands the published `strings.csv` into `Translations/_discovered/<locale>.working.csv`:

```csv
key,section,node,order,speaker,source_en,translation
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,Hey. This the cleaning place?,…
```

Conversations are in **play order** and the `speaker` column says **who is talking** (Conrad / Ryan / Alexander, Kobold for the player's choices, Phone for calls from head office, UI for interface text), which helps keep each character's voice consistent. `source_en` is filled from the script and UI the game currently has loaded (load a save first so all dialogue is present). Edit and save this file and hot reload shows the result immediately. It lives under `_discovered/`, so it never goes into the repository.

Running inside the game is the ownership check; there is no separate login.

### Hash before committing

Rebuild the published `strings.csv` before opening a pull request. It is generated from the working copy (`_discovered/<locale>.working.csv`) when one exists, otherwise from the `source_en` rows in `strings.csv` itself. Two ways:

- In the game: **F1 → Tools → Hash for commit** (rewrites the current language's file)
- `tools/hash-strings.ps1` (all languages with no arguments, one file with `-Path`)

**A `strings.csv` that still contains English is not accepted.** Every pull request is checked automatically, and when the format is wrong a comment explains why in English. Push a fix and the same comment is updated.

Wrap fields containing commas, quotes or line breaks in `"` (escape quotes as `""`), per [RFC 4180](https://datatracker.ietf.org/doc/html/rfc4180).

### Formatting tags

When the source contains TextMeshPro tags such as `<size=70%>`, `<gradient="gold">` or `<i>`, **keep the tag structure and translate only the text inside**. Broken tags break the display.

```csv
"<gradient=""gold""><b> ...English... </b></gradient><size=70%> (hint)","<gradient=""gold""><b> ...translation... </b></gradient><size=70%> (translated hint)"
```

## Finding untranslated text

Three ways to collect source text while playing. All write CSV files under `Translations/_discovered/`.

| How | File | Content |
|---|---|---|
| **F6** | `_discovered/dialogue_lines.csv` | Every dialogue line in **play order** (`yarn_project,node,order,kind,speaker,line_id,key,source_en,translation,tags`). Press it after loading a save |
| **F7** | `_discovered/ui_texts.csv` | Every UI text, hidden menus included (`key,source_en,translation,object_path`) |
| automatic | `_discovered/strings.csv` | Untranslated text seen while playing (`source_en,translation`) |

`_discovered/` contains the game's own copyrighted text, so **never commit it** (it is in `.gitignore`). Everyone generates it locally.

- Copy the rows you want from `dialogue_lines.csv` into `strings.csv` and fill in `translation`; extra columns such as `node` and `line_id` are fine.
- `node` is "character_visit_scene" (for example `Conrad_1_intro`), `order` is the position in that conversation, and `kind` is `line` (dialogue) or `option` (the player's choice). Translating one conversation at a time keeps tone and context consistent. The dragons are Conrad, Ryan and Alexander; suffixed names such as `RyanMuddy` are the same character in a different state.
- Lines already translated are exported with their translation filled in, so re-exporting never loses work.
- `object_path` in `ui_texts.csv` tells you where on screen a string lives. F7 once on the title screen and once in a level covers nearly all UI.

## Text that does not need translating

Slider values, resolutions (`1920 x 1080 @ 164.995Hz`), build numbers and the like are excluded from discovery from the start. Add more exclusions as regular expressions in `Translations/ignore.txt` (examples inside).

Exclusions only affect discovery. Lookup happens first, so a row in `strings.csv` is **always translated** even when it matches an exclusion pattern.

## Adding a locale

Add a folder under `Translations/<locale>/` with a `strings.csv` (and a `name.txt`). Use a BCP 47 style name like the existing ones (`ja`, `zh-Hans`, `zh-Hant`, `pt-BR`, `ko`). The plugin detects folders automatically.

Fonts are chosen from the characters in your file, so most scripts need nothing extra: Japanese, Chinese (Simplified and Traditional), Korean, Cyrillic, accented Latin and Hebrew all have a font on Windows and the Steam Deck. Right-to-left languages (`he`, `ar`, `fa`, `ur`, `yi`) are drawn right to left automatically; keep the file in normal typing order and avoid Latin words or digits inside a line, because those come out reversed. For a script the plugin has no font for, put a `.ttf`/`.otf` in a `fonts/` folder next to the plugin DLL.

## Improving a provisional language

Every pack except Japanese and Simplified Chinese is provisional: complete, but not reviewed by a native speaker. If you speak one of them, a review is the most valuable contribution there is. Fix lines in a pull request; once a whole pack has been read through by a native speaker, update the comment at the top of its `strings.csv` and its row in the README's language table in the same pull request.

## Checking your work

- Open the debug window with **F1** in the game and switch languages on the **Tools** tab; the screen updates without a restart.
- **Check translation layout** on the Tools tab writes strings at risk of overflowing to `_discovered/layout_risks.csv` (`source_en,translation,axis,required_px,available_px,ratio,object_path`). A larger `ratio` means more overflow; shorten the translation or rephrase.

## Before opening a pull request

- `source_en` matches the on-screen English **exactly** (case, surrounding spaces, formatting tags).
- **Hashed**: `strings.csv` has the columns `key,section,node,order,speaker,translation` and no `source_en` rows remain.
- Tag structure matches the source.
- No duplicate rows, no rows with an empty `translation`.
- One language and a coherent scope per pull request.

## Rules

- Never commit the game's assets or code (copyright).
- Never commit `Translations/_discovered/`.
- Translations are credited to their translators (see [LICENSE](LICENSE)).
