# Contributing

[日本語](CONTRIBUTING.ja.md)

With Drag'n Wash Localization you can contribute a translation by **editing CSV files only**. There's no code involved. This guide is for translators. How to build and release the plugin itself is in [docs/RELEASING.md](docs/RELEASING.md).

Everyone who takes part follows the [code of conduct](CODE_OF_CONDUCT.md). If you find a security problem, send it through the private form in [SECURITY.md](SECURITY.md), and please keep it out of pull requests and public issues.

## What you need

- Drag'n Wash (Steam) with this mod installed (BepInEx)
- Any editor that can handle CSV (Excel, LibreOffice, VS Code, ...)

You don't need to know Unity's internal keys, and you don't need to know how to program. While you work, you write the **exact English text shown in the game** in a `source_en` column, and before you commit you turn it into a hash (see "Hash before committing" below).

### Language display name (`name.txt`)

Whatever you put in `Translations/<locale>/name.txt` is shown as the language's name:

- in the game's **Options → Language (Mod)** list
- on the language buttons in the F1 menu
- in the installers (Windows and Steam Deck)

For example `ja/name.txt` → `日本語`, `zh-Hans/name.txt` → `简体中文`. It's one line, in UTF-8. If the file isn't there, the folder name is shown instead. So adding a language is just a folder, a `strings.csv` and a `name.txt`.

### Status and reviewers (`credits.txt`)

The language table on the F1 menu's **About** tab reads `Translations/<locale>/credits.txt`. The first line is the pack's status: `supervised`, `native` (translated by a native speaker), `proofread`, `converted`, `provisional` or `fun`. Each line after it is the name of someone who checked the pack. A pack without the file shows as provisional. You don't need to touch it in your pull request; the maintainer updates it along with the other credits.

## Basic flow

1. Fork this repository.
2. Add or fix translations in `Translations/<locale>/strings.csv`.
3. Commit and open a pull request (for the title and description, see [Writing the pull request](#writing-the-pull-request)).

## File format

> [!IMPORTANT]
> **The CSV notation changed in v0.6.0.** Files from earlier versions still load, but keep these differences in mind when you work on a current file:
> - Besides the 16-digit hash, the `key` column can now hold a Yarn line ID such as `line:6046bedf`. A row like that translates just that one line (see [Lines said by more than one character](#lines-said-by-more-than-one-character)).
> - When a line is said by more than one character, its `speaker` column lists all of them, separated by `/` (`Ryan/Alexander`).
> - The working copy has extra line-ID rows with an empty translation wherever a line is shared. Leave them empty unless you want that line to be different.
> - `order` now counts every line in a conversation, so the numbers don't match files made before v0.6.0. The column is only there for reading, so there's nothing to fix.
>
> For translation work, use the v0.6.0 plugin or later and the tools in this repository. The v0.5.0 tools don't know about line-ID rows and turn them into broken hash rows. Players on older versions aren't affected; their plugin just ignores line-ID rows.

`Translations/<locale>/strings.csv` is the published file. Its rows are in **the order the game plays them**, grouped under `#` headers. While you're working, rows with the English text can sit **in the same file**.

```csv
key,section,node,order,speaker,translation

# ===== Level 1: Ryan (Sunny) | sets level_1 | ends level_1_complete =====
# --- intro: Ryan_1_intro ---
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,Hi there. Is this the cleaning place?
# --- phone: Ryan_1_PhoneTutorial | if $has_talked_to_ryan ---
…
# ===== UI and other text (not part of the dialogue script) =====
d0db8b5e364b6989,UI,,,UI,Options
```

- `section` is the level number and dragon, such as `L01 Ryan`, or `Cutscene` / `Reaction` / `Unused` / `UI`.
- `node` / `order` are the Yarn conversation node and the line's position in it. Branch nodes come right after their parent.
- `#` lines are headers. Loading skips them, so just leave them in. `| if $variable` is a hint about the branch condition.
- The order comes from `data/script_order.csv` (node names, line ids, hashes and speakers only, no English). It's regenerated in the game with **Export game flow**, which the maintainer does when the game updates.

```csv
source_en,translation
Options,Optionen
```

- `key` is the first 16 hex digits of the SHA-256 of the source text. **This is the only form that gets committed.** The repository never carries the game's English script, so nobody without the game can read the script, and nobody can translate without the source in front of them.
- `speaker` is who says the line (Conrad / Ryan / Alexander / Kobold = the player's choice / Phone / UI). It's filled in automatically from the script structure. English that several characters say lists all of them (`Ryan/Alexander`).
- A `key` can also be a Yarn line ID such as `line:6046bedf`. A row like that translates just that one line; see [Lines said by more than one character](#lines-said-by-more-than-one-character).
- `source_en` is the exact English text shown in the game, and it has to match exactly. **Use this while you work**: when you save the file, the running game hot-reloads it.
- `translation` is your text.

The plugin hashes the English text it's about to show and looks that up, so both kinds of row behave the same. The F6 / F7 exports have both `key` and `source_en`, and that's how you can match one to the other.

### Starting a new language

1. Create `Translations/<locale>/` (for example `ko`) and write the display name into `name.txt` (for example `한국어`).
2. Turn on the developer tools in **Options → Mods → Drag'n Wash ModFramework → Developer tools**. They're off by default, so players never see the F1 window, the exports or the `_discovered` folder. Start the game and pick the new language in **Options → Language (Mod)** or under F1 → Translation. The screen stays in English, because there are no translations yet.
3. Press **F1 → Translation → Export working copy**. Even without a `strings.csv` you get an empty working copy, `_discovered/<locale>.working.csv`, listing every line the game has loaded along with its English text.
4. From here, carry on as in "Working with the English beside each line". Lines show up in the game as you translate them.
5. Translate the label of the Options row, `Language (Mod)`, as well (key `e3becbaee46cc0df`). Keep "(Mod)", or whatever it is in your language, so players can tell it's this mod's setting and not the game's. It shows up in the working copy and in the F7 export once you've opened Options.

### Working with the English beside each line (recommended)

**F1 → Translation → Export working copy** expands the published `strings.csv` into `Translations/_discovered/<locale>.working.csv`:

```csv
key,section,node,order,speaker,source_en,translation
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,Hey. This the cleaning place?,…
```

- Conversations are in **play order**, and the `speaker` column tells you **who is talking** (Conrad / Ryan / Alexander, Kobold for the player's choices, Phone for calls from head office, UI for interface text). That helps you keep each character's voice consistent.
- `source_en` is filled from the script and UI the game has loaded right now, so load a save first to get all the dialogue in.
- Edit this file and save it, and hot reload shows the result straight away.
- It lives under `_discovered/`, so it never goes into the repository.

The fact that it runs inside the game is the ownership check. There's no separate login.

### Lines said by more than one character

A row keyed by hash translates **every** line with that English. A few short lines are said by different characters: `Wonderful!` is Ryan's in level 1 and Alexander's in level 5. A row like that lists every speaker, for example `Ryan/Alexander`, and then one translation has to work for all of them.

When it can't, give the line its own translation. Wherever a shared line is spoken, the working copy puts an empty row keyed by the Yarn **line ID**:

```csv
key,section,node,order,speaker,source_en,translation
84f325bca745e504,L01 Ryan,Ryan_1_intro,9,Ryan/Alexander,Wonderful!,Wonderful translation for everyone
line:6046bedf,L01 Ryan,Ryan_1_intro,9,Ryan,Wonderful!,Ryan's own translation
line:ab423ac7,L15 Alexander,Alexander_5_required,19,Alexander,Wonderful!,
```

- Only fill in the line rows you want to be different. A line row with a translation wins for that one line, and everywhere else keeps using the hash row.
- It's fine to leave line rows empty. *Hash for commit* only publishes the ones you filled in, at their place in the script.
- Line IDs come from the game's script. If a game update changes one, that line falls back to the hash row. If an update changes a line's English instead, the line ID still finds the row (this is experimental and not in a release yet). The translation keeps showing, and the line is listed in the log and the F1 window so someone can review it, because the English it was written for has changed.
- Line rows work for dialogue and options. They don't work for UI text.

### Hash before committing

Rebuild the published `strings.csv` before you open a pull request. It's generated from the working copy (`_discovered/<locale>.working.csv`) if there is one, and otherwise from the `source_en` rows in `strings.csv` itself. You can do it in any of these ways:

- In the game: **F1 → Translation → Hash for commit** (rewrites the file for the current language)
- `tools/hash-strings.ps1` with no arguments (all languages)
- With Docker, on any system and without installing anything yourself: `docker compose run --rm hash ja` (one language) or `docker compose run --rm hash` (all of them). See [docs/DOCKER.md](docs/DOCKER.md).

`-Path` works differently. It converts exactly the files you give it, in place, and does **not** look for a working copy. Use it on a published `strings.csv`. If you pass it a working copy, it overwrites it with the published form, and you lose its `source_en` column and every row that isn't translated yet.

**A `strings.csv` that still has English in it won't be accepted.** Every pull request is checked automatically, and if the format is wrong, a comment in English explains why. When you push a fix, that same comment gets updated. To run those checks yourself before you push, with Docker: `docker compose run --rm checks`.

If a field has commas, quotes or line breaks in it, wrap it in `"` (and write quotes as `""`), as [RFC 4180](https://datatracker.ietf.org/doc/html/rfc4180) says.

### Formatting tags

If the source has TextMeshPro tags such as `<size=70%>`, `<gradient="gold">` or `<i>`, **keep the tags as they are and only translate the text inside**. Broken tags break the display.

```csv
"<gradient=""gold""><b> ...English... </b></gradient><size=70%> (hint)","<gradient=""gold""><b> ...translation... </b></gradient><size=70%> (translated hint)"
```

## Finding untranslated text

There are three ways to collect source text while you play. They all write CSV files under `Translations/_discovered/`.

| How | File | Content |
|---|---|---|
| **F6** | `_discovered/dialogue_lines.csv` | Every dialogue line in **play order** (`yarn_project,node,order,kind,speaker,line_id,key,source_en,translation,tags`). Press it after loading a save |
| **F7** | `_discovered/ui_texts.csv` | Every UI text, hidden menus included (`key,source_en,translation,object_path`) |
| automatic | `_discovered/strings.csv` | Untranslated text seen while playing (`source_en,translation`) |

`_discovered/` has the game's own copyrighted text in it, so **never commit it** (it's in `.gitignore`). Everyone makes their own copy locally.

- Copy the rows you want from `dialogue_lines.csv` into `strings.csv` and fill in `translation`. Extra columns such as `node` and `line_id` are fine to leave.
- `node` is "character_visit_scene" (for example `Conrad_1_intro`), `order` is the position in that conversation, and `kind` is `line` (dialogue) or `option` (the player's choice). If you translate one conversation at a time, the tone and context stay consistent. The dragons are Conrad, Ryan and Alexander. Names with something added, like `RyanMuddy`, are the same character in a different state.
- Lines you've already translated are exported with the translation filled in, so exporting again never loses your work.
- `object_path` in `ui_texts.csv` tells you where on screen a string is. Pressing F7 once on the title screen and once in a level gets you nearly all the UI.

## Text that does not need translating

Slider values, resolutions (`1920 x 1080 @ 164.995Hz`), build numbers and things like that are left out of discovery from the start. You can add more exclusions as regular expressions in `Translations/ignore.txt` (there are examples inside).

Exclusions only affect discovery. The lookup happens first, so a row in `strings.csv` **always gets translated**, even when it matches an exclusion pattern.

## Adding a locale

Add a folder under `Translations/<locale>/` with a `strings.csv` (and a `name.txt`). Name it in BCP 47 style like the existing ones (`ja`, `zh-Hans`, `zh-Hant`, `pt-BR`, `ko`). The plugin finds new folders by itself.

Fonts are picked from the characters in your file, so most scripts don't need anything extra.

- Japanese, Chinese (Simplified and Traditional), Korean, Cyrillic, accented Latin and Hebrew already have a font on Windows and the Steam Deck.
- Right-to-left languages (`he`, `ar`, `fa`, `ur`, `yi`) are drawn right to left automatically. Keep the file in normal typing order, and avoid Latin words or digits inside a line, because those come out reversed.
- For a script the plugin has no font for, put a `.ttf`/`.otf` in a `fonts/` folder next to the plugin DLL.

## Improving a provisional language

Every pack except Japanese and Simplified Chinese is provisional. They're complete, but no native speaker has reviewed them. If you speak one of those languages, a review is the most valuable thing you can contribute.

- To fix lines, send them in a pull request.
- Once a native speaker has read through a whole pack, update the comment at the top of its `strings.csv` and its row in the README's language table in the same pull request. If that feels like too much, just say in the pull request that the whole pack was reviewed, and the maintainer will update both.
- If you only change the `translation` column of the published `strings.csv` and leave the other columns alone, the file stays hashed and doesn't need *Hash for commit*.
- If you want to see the English next to each line while you review, use the working copy described in [Working with the English beside each line](#working-with-the-english-beside-each-line-recommended).

## Checking your work

- Switch languages in **Options → Language (Mod)** (Save keeps your choice, Back goes back to the saved language) or on the **Translation** tab of the **F1** debug window. The screen updates without a restart.
- **Check translation layout** on the Translation tab writes strings that might overflow to `_discovered/layout_risks.csv` (`source_en,translation,axis,required_px,available_px,ratio,object_path`). A bigger `ratio` means more overflow, so shorten or reword the translation.

## Before opening a pull request

- `source_en` matches the English on screen **exactly** (case, spaces around it, formatting tags).
- **It's hashed**: `strings.csv` has the columns `key,section,node,order,speaker,translation` and there are no `source_en` rows left.
- The tags are laid out the same as in the source.
- There are no duplicate rows and no rows with an empty `translation`.
- Each pull request covers one language and one sensible chunk of work.
- Your commits are made with an email that's on your GitHub account, or with your GitHub no-reply address (both are on [github.com/settings/emails](https://github.com/settings/emails)). Otherwise GitHub can't tell the commits are yours, and the **Commit checker** fails.

## Writing the pull request

- For the title, start with the locale code in brackets and then say what changed. For example `[ko] Fix the Korean translation`, `[ko] Native review of levels 1-3`, or `[de] Translate the Options row`. English, Japanese or your own language are all fine.
- The pull request template fills in the description for you. Under *What*, write the language and which part you changed (levels, scenes such as `Conrad_1_intro`, or UI), then tick the checklist items that apply and leave the rest unticked. Under *Credit*, say whether you'd like to be credited and, if so, the name to show (and a link if you want one).
- Once it's open, the automatic translation check runs. If it fails, a comment in English lists the reasons with file and line numbers, and it updates itself when you push a fix. After that the maintainer reviews the pull request. Feel free to ask questions in the pull request, in English or Japanese. If the check fails, see [If the automatic check fails](#if-the-automatic-check-fails).

## If the automatic check fails

Every pull request runs `tools/check-translations.py`. When it finds a problem, the pull request gets a comment in English explaining why, with a **Full report** that lists each problem as `file:line: message`. The line number is the line in the file as you see it in an editor. Push a fix to the same branch and the check runs again. The existing comment gets updated, so you won't end up with a second one.

You can run the same check yourself before you push (Python 3.9 or later):

```bash
python tools/check-translations.py
```

It prints `translations OK` when everything passes.

| Message in the report | What it means | How to fix it |
|---|---|---|
| `header is [...]; the published file must be ...` | The file is still a working copy (it has a `source_en` column) | Run **F1 → Translation → Hash for commit** or `tools/hash-strings.ps1`, then commit the rebuilt `strings.csv` |
| `key is not 16 lowercase hex digits or a line ID` | A key is neither a hash nor a line ID. English text went into the key column, or the key was edited | Rebuild with *Hash for commit*. Never edit the `key` column by hand |
| `must not be committed (contains source text)` | A file from `Translations/_discovered/` or a `strings.local.csv` is in the pull request | Take it out of the pull request with `git rm --cached <file>` and commit. You can keep the file locally if you still need it |
| `duplicate key (see line N)` | The same line is in there twice | Keep one row per key and delete the other |
| `empty translation` | A row has an empty `translation` | Fill it in, or delete the row so the game shows the English |
| `expected 6 fields, got N` | The row has the wrong number of columns | Put values that contain `,`, a line break or `"` in double quotes, and write `"` inside them as `""` |
| `section does not look like an identifier` / `node does not look like an identifier` | These columns were edited, or the columns got shifted | Put back the values from `main` and only change `translation` |
| `no strings.csv` | A language folder has no `strings.csv` | Add the file, or remove the empty folder |
| `empty file` | `strings.csv` has no header line | Start the file with `key,section,node,order,speaker,translation` |

The check doesn't compare formatting tags with the source. Reviewers look at those.

**Spreadsheet apps can break the file when they save it.** Excel may turn keys that look like numbers (for example `12345e6789012345`) into scientific notation, change the encoding, or change the quoting. A text editor such as VS Code is safer, or LibreOffice with every column set to *Text*. Save as UTF-8 CSV.

## Translated pictures

Some text is inside pictures (menu buttons, signs). A translated picture goes in `Translations/<locale>/textures/<game texture name>.png`, with a row in `textures/credits.csv` (`file,author,note`). Draw it by hand or change the game's picture, but never commit the game's picture unchanged ([content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)). The steps are in [docs/TRANSLATED_TEXTURES.md](docs/TRANSLATED_TEXTURES.md#for-translators).

## Rules

- Never commit the game's assets or code unchanged ([content policy](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)). Pictures you drew or changed are welcome (see above).
- Never commit `Translations/_discovered/`.
- Translations are credited to the people who translated them (see [LICENSE](LICENSE)).

## Credits for contributors

In the pull request's *Credit* section, say whether you want to be credited and under what name. If you do, the maintainer adds the credit in a separate commit after merging, so you don't have to change anything for it. The credit shows up in:

- the language's row in the README's language pack table
- the comment at the top of the pack's `strings.csv`
- the notes of the release that includes your change
- the pack's `credits.txt`, which the in-game F1 menu's About tab shows (from the next release on)
- the About screen of the installer (from the next release on)

How the pack's status is worded (for example whether a partial review changes "provisional") is decided per pull request. If you'd rather not be credited, nothing gets added, though your commits still show in the repository history.

## Issues

- New issues get labelled automatically (kind, area, severity). If a bug report is missing a version, steps or a log, it gets one comment asking for them.
- To do that, some of the issue is sent to TypeSafe AI's classification model ([`tools/issue-triage.py`](https://github.com/TomXV/dragnwash-modframework/blob/main/tools/issue-triage.py) in the framework's repository): the title and the body's own words, without code blocks, tables, translation rows or images, plus the error lines of a pasted log, with user names taken out of paths. No game text and no translation file is sent.
- **A person reads every issue.**
