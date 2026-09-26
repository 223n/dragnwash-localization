#!/bin/bash
# Drag'n Wash Localization - EXPERIMENTAL installer / uninstaller for macOS.
#
# !! EXPERIMENTAL, and not in the release zip. It installs what was checked by
# !! hand on one Mac (Apple A18 Pro, macOS 27.2, the game's 9/12/2026 build),
# !! following https://github.com/TomXV/dragnwash-modframework/issues/85.
# !! The script itself has been run once on that Mac's real game, over the
# !! setup made by hand there (so it downloaded nothing); a fresh install
# !! only against a test copy of a game folder.
#
# What it installs, and why:
#   - BepInEx 5.4.23.5 for macOS, with its Doorstop (libdoorstop.dylib and
#     run_bepinex.sh) swapped for the one Doorstop build this script pins
#     (DOORSTOP_URL and the lines around it). The Doorstop inside BepInEx
#     5.4.23.5 cannot hook Unity 6.3 games (NeighTools/UnityDoorstop#108); so
#     far only UnityDoorstop's "ci" pre-release has the fix
#     (NeighTools/UnityDoorstop#117). The pin is the ci 4.6.0 build that was
#     tried with the game, downloaded from an unchanged copy of it that is
#     kept as a pre-release in 223n's fork (223n/UnityDoorstop):
#     the ci pre-release itself is built again under the same URL on every
#     push to UnityDoorstop's master, which is how the ci 4.5.0 build pinned
#     before went away (HTTP 404) on 2026-09-25. Once the stable UnityDoorstop
#     4.6.0 is released and tried, the pin moves to it and the copy is no
#     longer used. Follow that in the issue above.
#   - The game is started as x86_64, under Rosetta (archpreference="x86_64" in
#     run_bepinex.sh). Running natively on Apple silicon, BepInEx 5.4.23.5
#     cannot apply Harmony patches, which its own preloader needs; the fix
#     (BepInEx/BepInEx#1402) is not in a release yet. Rosetta has to be
#     installed: the script checks, and says how when it is not.
#   - run_bepinex.sh hands Steam's overlay libraries on to the game
#     (STEAM_DYLD_INSERT_LIBRARIES), so Shift+Tab still opens the overlay.
#     UnityDoorstop merged the same change on 2026-09-25
#     (NeighTools/UnityDoorstop#121, in 4.6.0), so the pinned build's run.sh
#     already has it and is left as it is; the script only edits a
#     run_bepinex.sh from before that (the ci 4.5.0 build pinned earlier, or
#     one set up by hand).
#   - Drag'n Wash ModFramework, the release mod-install.json names, when the
#     game folder does not have it, then the mod itself.
#
# The plan: the copy of the ci 4.6.0 build stays pinned until the stable
# UnityDoorstop 4.6.0 is released and tried, and then that release is pinned
# (the steps are next to DOORSTOP_URL). UnityDoorstop's fixes
# (NeighTools/UnityDoorstop#117, and NeighTools/UnityDoorstop#114, which
# BepInEx/BepInEx#1402 needs) are both in the pinned build, so the stable
# 4.6.0 is expected to carry both. After it, the script only waits for a
# BepInEx release with BepInEx's arm64 fixes (BepInEx/BepInEx#1288 and
# BepInEx/BepInEx#1402), and moves to it; the game is then expected to run
# natively without Rosetta - not yet tested.
#
# After installing:
#   - Start the game from Steam. Started from Terminal it has no Steam API, so
#     it does not find your saves.
#   - While the game starts, it can look frozen before the title screen when
#     its window is not in front; clicking the window lets it go on (seen on
#     the Mac above).
#
# Run it in Terminal. The mod's files come from the extracted release zip:
#   bash install-macos.sh --payload <folder>              asks what to do
#   bash install-macos.sh --payload <folder> --install    install or update
#   bash install-macos.sh --uninstall                     remove the mod
#                         (ModFramework too, only if this script installed it)
#   bash install-macos.sh --check                         tell whether it loaded
# (--payload is not needed when the script sits in that folder, or when you
# run it from there.)
#
# What --install does:
#   1. finds DragNWash.app in your Steam libraries, and checks for Rosetta
#   2. works out what is missing: BepInEx, the pinned Doorstop, ModFramework
#   3. downloads those from GitHub and checks each SHA-256 (and ModFramework's
#      size) against this script and mod-install.json; until all of them pass,
#      the game folder is not changed
#   4. puts BepInEx and the pinned Doorstop next to DragNWash.app and sets
#      executable_name, archpreference and target_assembly in run_bepinex.sh,
#      plus the overlay hand-over when run_bepinex.sh does not have it yet
#      (Doorstop before 4.6.0); a file it replaces is kept as
#      <name>.dragnwash-backup
#   5. copies ModFramework (never over a copy that is as new or newer, and
#      never next to a part switched off in the Mods screen) and the mod, and
#      sets the language you pick; the mod itself is switched back on if the
#      Mods screen switched it off or set it to be uninstalled
#   6. sets the Steam launch option "<game folder>/run_bepinex.sh" %command%
#      (Steam has to be closed for that; you are asked first)
#
# Options: --install  --uninstall  --check  --lang <locale>  --game-dir <path>
#          --payload <extracted zip folder>  --yes (no questions, use defaults)
#          --remove-bepinex (with --uninstall)  --close-steam (close Steam
#          without asking)  --terminal (no dialogs)  --ui en|ja|zh
#          --bepinex-zip <file>  --doorstop-zip <file>  --framework-zip <file>
#          (zips you downloaded yourself; checked the same way as downloads)
#
# Written for the bash 3.2 that ships with macOS: no mapfile, no associative
# arrays, no empty arrays under set -u, no here-documents inside $(...), and
# only the BSD forms of sed, cp and stat.
set -euo pipefail

APP_ID=4739660
APP_BUNDLE="DragNWash.app"
GAME_PROCESS="DragNWash.app/Contents/MacOS/DragNWash"
PLUGIN="DragNWashLocalization"
CFG_NAME="com.tomxv.dragnwash.localization.cfg"
MARKER=".bepinex-installed-by-dragnwash-localization"
# Lists the ModFramework parts this script added to the game folder, one per
# line (plugins/<folder> or patchers/<file>). Uninstall removes those and
# nothing else of ModFramework: on a Mac any other copy was put there by hand,
# maybe patched, and is not this script's to delete.
FW_MARKER=".modframework-installed-by-dragnwash-localization"
BACKUP_SUFFIX=".dragnwash-backup"
# Where the macOS setup is followed.
MACOS_ISSUE="https://github.com/TomXV/dragnwash-modframework/issues/85"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip"
BEPINEX_SHA256="01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323"
# ---- The pinned Doorstop build. These four lines change together. ----
# DOORSTOP_URL        the zip; the only Doorstop this script installs
# DOORSTOP_SHA256     that zip, checked before anything in it is used
# DOORSTOP_DYLIB_SHA256  universal/libdoorstop.dylib in that zip. A game folder
#                     that already has this file and a run_bepinex.sh made
#                     from a Doorstop run.sh (ci_run_script) needs no download.
# DOORSTOP_BUILD      how messages and the log name the build (not where it
#                     comes from: the download list takes the repository
#                     from DOORSTOP_URL)
#
# Now: UnityDoorstop's ci 4.6.0 build, the doorstop_macos_release_4.6.0.zip
# that its master 97293a28 built (upstream Build run 36186569352) and that was
# published to the "ci" pre-release on 2026-09-25 20:37 UTC, downloaded from an
# unchanged copy kept as a pre-release in 223n's fork:
#   https://github.com/223n/UnityDoorstop/releases/tag/ci-4.6.0-97293a28
# Its tag is at the same commit, so GitHub's source archives of that tag are
# the source of the build; it is LGPL-2.1, like UnityDoorstop, and the LICENSE
# file is in the zip. This build was tried with the game on the Mac named at
# the top, set up by hand and then with this script run over that setup (so
# the script downloaded nothing); a fresh install on the real game is not
# tested yet. Pinned on 2026-09-26.
#
# Why a copy: the ci pre-release is built again under the same URL on every
# push to UnityDoorstop's master (10 pushes from 2026-09-20 to 2026-09-25), so
# its bytes change, and when the version goes up the file is renamed and the
# old one removed. That is how the pin before this one, the ci 4.5.0 build
# (master d8973b22, .../releases/download/ci/doorstop_macos_release_4.5.0.zip),
# went away (HTTP 404) when ci became 4.6.0 on 2026-09-25 (master 97293a28,
# after NeighTools/UnityDoorstop#121), and fresh installs that had to download
# the Doorstop were blocked. The first decision (2026-09-26) was to wait for
# the stable 4.6.0; it was revised the same day so that fresh installs work
# now, from a copy that keeps the tried bytes at a URL that no push to
# UnityDoorstop changes. The ci URL itself is not pinned again for that
# reason. The ci 4.6.0 has the same libdoorstop.dylib as the ci 4.5.0, byte for
# byte, so a game folder set up from the 4.5.0 build needs no download and
# keeps its run_bepinex.sh (the overlay edit below adds the hand-over when it
# is missing).
#
# Should the copy be removed, or GitHub serve other bytes under its URL,
# installs that have to download the Doorstop stop without changing anything,
# with a message that sends users to MACOS_ISSUE; a game folder that already
# has the pinned libdoorstop.dylib, and --doorstop-zip with a saved copy of the
# pinned zip, still work.
#
# For the pull request to TomXV/dragnwash-localization: its maintainer may
# prefer to keep the same zip in a release of their own. Then, of the four
# lines, only DOORSTOP_URL changes; the SHA-256 values and DOORSTOP_BUILD
# stay as they are. The links to the copy move with it: the header at the
# top, "Now:" above, README*.md and docs/ROADMAP*.md.
#
# When the stable UnityDoorstop 4.6.0 is released: try it with the game on a
# Mac, then set these four lines to
#   DOORSTOP_URL="https://github.com/NeighTools/UnityDoorstop/releases/download/v4.6.0/doorstop_macos_release_4.6.0.zip"
#   DOORSTOP_SHA256="<shasum -a 256 of that zip>"
#   DOORSTOP_DYLIB_SHA256="<shasum -a 256 of universal/libdoorstop.dylib in it>"
#   DOORSTOP_BUILD="v4.6.0 release"
# and say so in MACOS_ISSUE. The release replaces the copy; keep the copy
# downloadable for a while all the same, since copies of this script from
# before the switch still fetch it. (UnityDoorstop already has a tag v4.6.0 at
# master 97293a28, and its tag build succeeded, but no v4.6.0 release was
# published as of 2026-09-26.) Nothing else has to change: the zip has the
# same layout (universal/libdoorstop.dylib, run.sh, .doorstop_version), and the
# stop message leaves out the part about the ci pre-release's churn (ds_ci) as
# it does for the copy, since that part is only added for a DOORSTOP_URL on the
# ci pre-release itself. The stable run.sh is expected to hand on Steam's
# overlay like the ci 4.6.0 one (NeighTools/UnityDoorstop#121), so the overlay
# edit in configure_run_script finds it and does nothing. If the stable
# libdoorstop.dylib is the pinned one byte for byte, game folders set up from
# the ci builds need no download; otherwise their next install downloads the
# release and replaces libdoorstop.dylib and run_bepinex.sh.
#
# Later, the archpreference line in configure_run_script is expected to go
# once BepInEx's arm64 fixes (BepInEx/BepInEx#1288 and BepInEx/BepInEx#1402)
# are released, so the game runs natively without Rosetta - not yet tested.
DOORSTOP_URL="https://github.com/223n/UnityDoorstop/releases/download/ci-4.6.0-97293a28/doorstop_macos_release_4.6.0.zip"
DOORSTOP_SHA256="fa3c9e4638f82873620b7e7e12ce1730ce01cdc23a7ba3bac2e6bfa31c0e929d"
DOORSTOP_DYLIB_SHA256="5f31b9fca678536ed1636206f47b77431ac5b972ff92a0a2badf84bc065f9562"
DOORSTOP_BUILD="ci 4.6.0 build of UnityDoorstop master 97293a28, unchanged copy"
# ---- end of the pinned Doorstop build ----
# The GitHub repository DOORSTOP_URL downloads from, for the download list.
DOORSTOP_REPO="${DOORSTOP_URL#https://github.com/}"
DOORSTOP_REPO="${DOORSTOP_REPO%%/releases/*}"
FRAMEWORK_REPO="TomXV/dragnwash-modframework"
FRAMEWORK_PREFIX="DragNWash.ModFramework"
FRAMEWORK_PATCHER="DragNWash.ModFramework.Preloader.dll"
# The lists the ModFramework Preloader keeps in BepInEx/config (DisabledMods.cs
# and PendingUninstalls.cs in ModFramework): what the Mods screen switched off,
# which DLLs the Preloader itself renamed to .dll.disabled, and which mods wait
# to be uninstalled at the next start of the game.
FW_LISTS="com.tomxv.dragnwash.modframework.disabled.txt com.tomxv.dragnwash.modframework.state.txt com.tomxv.dragnwash.modframework.uninstall.txt"
MAX_DOWNLOAD=$((20 * 1024 * 1024))
STEAM_ROOT="$HOME/Library/Application Support/Steam"
# For tests only: DRAGNWASH_TEST_STEAM_ROOT points the script at a fake Steam
# folder.
if [ -n "${DRAGNWASH_TEST_STEAM_ROOT:-}" ]; then STEAM_ROOT="$DRAGNWASH_TEST_STEAM_ROOT"; fi

# Folders are compared by what they are, not by how their path is spelled.
# One folder has many spellings: through a symlink, in other upper and lower
# case (the Mac's disk ignores case), or through /System/Volumes/Data. pwd -P
# only resolves the first of these. /usr/bin/stat by its full path: a GNU stat
# earlier in PATH reads -f as "file system", and would make every folder on
# the disk look the same.
dir_id() {  # dir_id <path>: the folder's "device:inode", or nothing when it is not a folder
    [ -d "$1" ] || return 0
    /usr/bin/stat -L -f '%d:%i' "$1" 2>/dev/null || true
}
# 0 when <path> is that folder or lies anywhere inside it. The walk goes up the
# path with symlinks resolved, so a symlink inside a test folder that leads to
# a real game folder counts as where it leads.
inside_dir() {  # inside_dir <path> <dir_id of the folder>
    local d
    [ -n "$2" ] || return 1
    d="$(cd "$1" 2>/dev/null && pwd -P)" || return 1
    while :; do
        if [ "$(dir_id "$d")" = "$2" ]; then return 0; fi
        if [ "$d" = / ]; then return 1; fi
        d="$(dirname "$d")"
    done
}

# A run is a test whenever STEAM_ROOT is not this account's own Steam folder:
# the override above, or a HOME set to a sandbox, which moves STEAM_ROOT just
# the same. The Steam that is running belongs to the real folder, so a test
# never closes or starts it, and it only works on a game folder inside the
# test Steam folder. The account's home comes from the user database (~name),
# because $HOME is exactly what a test changes; the name is checked first
# since eval has to expand it. If the lookup fails, $HOME is trusted, as it
# was before this check existed.
ACCOUNT_HOME=""
account="$(id -un 2>/dev/null || true)"
case "$account" in
    "" | *[!A-Za-z0-9._-]*) ;;
    *) eval "ACCOUNT_HOME=~$account" ;;
esac
case "$ACCOUNT_HOME" in /*) ;; *) ACCOUNT_HOME="$HOME" ;; esac
REAL_STEAM_ROOT="$ACCOUNT_HOME/Library/Application Support/Steam"
REAL_STEAM_ID="$(dir_id "$REAL_STEAM_ROOT")"
TEST_STEAM=1
if [ -n "$REAL_STEAM_ID" ]; then
    if [ "$(dir_id "$STEAM_ROOT")" = "$REAL_STEAM_ID" ]; then TEST_STEAM=0; fi
elif [ "$STEAM_ROOT" = "$REAL_STEAM_ROOT" ]; then
    # This account has no Steam folder, so there is only the path to go by.
    TEST_STEAM=0
fi
TAB="$(printf '\t')"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

MODE=""
LANG_CHOICE=""
GAME_DIR=""
PAYLOAD_ROOT=""
ASSUME_YES=0
REMOVE_BEPINEX=0
CLOSE_STEAM=0
FORCE_TERMINAL=0
UI=""
WARNINGS=""
BEPINEX_ZIP=""
DOORSTOP_ZIP=""
FRAMEWORK_ZIP=""
# Filled in along the way; some messages name them.
ZIP_PATH="" BAD_PART="" BAD_WHAT="" ARCH_PREF="" LAST_START_TEXT=""
FW_VERSION="" FW_SHA256="" FW_SIZE=0 FW_NEEDS="" FW_HAVE="" FW_PARTS=""
FW_OFF="" FW_OFF_HAND="" FW_OFF_PRE=0 FW_OFF_OLD=0 MOD_ON=0

# The loop below shifts every argument away, and the language picker later
# replaces the positional parameters again, so the invocation has to be kept
# here to be able to log it at all. installer.log is usually the only
# artefact a user can attach to a bug report.
ARGV="$*"

# Without this, an option given as the last word shifts twice: once in the
# case branch and once at the end of the loop. The second shift fails with
# nothing left, and set -e ends the run with no message at all.
need_value() { [ $# -ge 2 ] || { echo "Option $1 needs a value" >&2; exit 2; }; }

while [ $# -gt 0 ]; do
    case "$1" in
        --install) MODE=install ;;
        --uninstall) MODE=uninstall ;;
        --check) MODE=check ;;
        --lang) need_value "$@"; LANG_CHOICE="$2"; shift ;;
        --game-dir) need_value "$@"; GAME_DIR="$2"; shift ;;
        --payload) need_value "$@"; PAYLOAD_ROOT="$2"; shift ;;
        --yes|-y) ASSUME_YES=1 ;;
        --remove-bepinex) REMOVE_BEPINEX=1 ;;
        --close-steam) CLOSE_STEAM=1 ;;
        --terminal) FORCE_TERMINAL=1 ;;
        --ui) need_value "$@"; UI="$2"; shift ;;
        --bepinex-zip) need_value "$@"; BEPINEX_ZIP="$2"; shift ;;
        --doorstop-zip) need_value "$@"; DOORSTOP_ZIP="$2"; shift ;;
        --framework-zip) need_value "$@"; FRAMEWORK_ZIP="$2"; shift ;;
        -h|--help) sed -n '2,/^set -euo pipefail$/{/^#/p;}' "$0"; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
    shift
done

# The release zip has BepInEx/ at its root. This script lives in
# installer/experimental/ in the repository, so also look two levels up.
if [ -z "$PAYLOAD_ROOT" ]; then
    for cand in "$HERE" "$HERE/.." "$HERE/../.." "$PWD"; do
        if [ -f "$cand/BepInEx/plugins/$PLUGIN/$PLUGIN.dll" ]; then
            PAYLOAD_ROOT="$(cd "$cand" && pwd)"
            break
        fi
    done
fi
PAYLOAD="${PAYLOAD_ROOT:-$HERE}/BepInEx/plugins/$PLUGIN"
# What the release says about the mod, next to BepInEx/ in the zip. Only its
# "framework" block is used here.
MANIFEST="${PAYLOAD_ROOT:-$HERE}/mod-install.json"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# ----------------------------------------------------------------- strings --
# Which language to talk in, and which translation to preselect:
#   1. the locale of this shell, when it is not English
#   2. the first language in macOS System Settings, when it is not English
#   3. Steam's own language
#   4. English
# Prompts exist in English, Japanese and Chinese; for the other packs only the
# preselected language follows.
locale_pair() {  # locale_pair <tag> ; prints "<ui> <pack>" or nothing
    case "$1" in
        ja*) echo "ja ja" ;;
        zh_TW*|zh_HK*|zh_MO*|zh-Hant*|zh-TW*|zh-HK*|zh-MO*) echo "zh zh-Hant" ;;
        zh*) echo "zh zh-Hans" ;;
        ko*) echo "en ko" ;;
        de*) echo "en de" ;;
        fr*) echo "en fr" ;;
        es*) echo "en es" ;;
        pt_BR*|pt-BR*) echo "en pt-BR" ;;
        ru*) echo "en ru" ;;
        pl*) echo "en pl" ;;
        he*|iw*) echo "en he" ;;
        eo*) echo "en eo" ;;
    esac
}
detect_ui() {
    local v pair
    for v in "${LC_ALL:-}" "${LC_MESSAGES:-}" "${LANG:-}"; do
        pair="$(locale_pair "$v")"
        if [ -n "$pair" ]; then echo "$pair"; return; fi
    done
    v="$(defaults read -g AppleLanguages 2>/dev/null | sed -n '2p' | tr -d ' ",' || true)"
    pair="$(locale_pair "$v")"
    if [ -n "$pair" ]; then echo "$pair"; return; fi
    v="$(sed -n 's/^[[:space:]]*"language"[[:space:]]*"\([^"]*\)".*/\1/p' "$STEAM_ROOT/registry.vdf" 2>/dev/null | head -1 || true)"
    case "$v" in
        japanese) echo "ja ja" ;;
        schinese) echo "zh zh-Hans" ;;
        tchinese) echo "zh zh-Hant" ;;
        koreana) echo "en ko" ;;
        german) echo "en de" ;;
        french) echo "en fr" ;;
        spanish|latam) echo "en es" ;;
        brazilian) echo "en pt-BR" ;;
        russian) echo "en ru" ;;
        polish) echo "en pl" ;;
        *) echo "en en" ;;
    esac
}
DETECTED="$(detect_ui)"
DETECTED_UI="${DETECTED%% *}"
DEFAULT_LOCALE="${DETECTED#* }"
[ -n "$UI" ] || UI="$DETECTED_UI"

t() {
    local key="$1"
    case "$UI:$key" in
        ja:title) echo "Drag'n Wash Localization（macOS・実験的）" ;;
        zh:title) echo "Drag'n Wash Localization（macOS・实验性）" ;;
        *:title) echo "Drag'n Wash Localization (macOS, experimental)" ;;
        ja:nopayload) echo "Mod のファイルが見つかりません。リリースの zip を丸ごと展開して、そのフォルダーを --payload で指定するか、そのフォルダーの中でこのスクリプトを実行してください。" ;;
        zh:nopayload) echo "找不到 Mod 文件。请完整解压发布版 zip，并用 --payload 指定该文件夹，或在该文件夹中运行此脚本。" ;;
        *:nopayload) echo "The mod files are missing. Extract the whole release zip and pass that folder with --payload, or run this script from inside it." ;;
        ja:badmanifest) echo "Mod の隣にある mod-install.json を使えませんでした。リリースの zip をもう一度ダウンロードして、丸ごと展開してください。理由:" ;;
        zh:badmanifest) echo "无法使用 Mod 旁边的 mod-install.json。请重新下载发布版 zip 并完整解压。原因：" ;;
        *:badmanifest) echo "mod-install.json next to the mod could not be used. Download the release zip again and extract it whole. The reason:" ;;
        ja:nogame) echo "Drag'n Wash（DragNWash.app）が見つかりません。--game-dir でゲームのフォルダーを指定してください。" ;;
        zh:nogame) echo "找不到 Drag'n Wash（DragNWash.app）。请用 --game-dir 指定游戏文件夹。" ;;
        *:nogame) echo "Drag'n Wash (DragNWash.app) was not found. Pass the game folder with --game-dir." ;;
        ja:running) echo "ゲームが起動中です。先に終了してください。" ;;
        zh:running) echo "游戏正在运行。请先关闭游戏。" ;;
        *:running) echo "The game is running. Close it first." ;;
        ja:norosetta) echo "Rosetta が入っていません。今は、ゲームを Rosetta（x86_64）で動かす必要があります。Apple シリコンのネイティブ実行では、BepInEx 5.4.23.5 がパッチを当てられないためです（BepInEx/BepInEx#1402、未リリース）。
ターミナルで次を実行して Rosetta を入れてから、このスクリプトをもう一度実行してください:
  softwareupdate --install-rosetta --agree-to-license" ;;
        zh:norosetta) echo "未安装 Rosetta。目前游戏必须在 Rosetta（x86_64）下运行：在 Apple 芯片上原生运行时，BepInEx 5.4.23.5 无法应用补丁（BepInEx/BepInEx#1402，尚未发布）。
请在终端中运行以下命令安装 Rosetta，然后再次运行此脚本：
  softwareupdate --install-rosetta --agree-to-license" ;;
        *:norosetta) echo "Rosetta is not installed. For now the game has to run as x86_64 under Rosetta: running natively on Apple silicon, BepInEx 5.4.23.5 cannot apply its patches (BepInEx/BepInEx#1402, not released yet).
Install Rosetta in Terminal, then run this script again:
  softwareupdate --install-rosetta --agree-to-license" ;;
        ja:pick) echo "言語を選んでください" ;;
        zh:pick) echo "请选择语言" ;;
        *:pick) echo "Choose a language" ;;
        ja:dl_head) echo "先に GitHub から次をダウンロードします。SHA-256 が一致したときだけ使い、それまでゲームのフォルダーは変わりません:" ;;
        zh:dl_head) echo "将先从 GitHub 下载以下文件。只有 SHA-256 一致时才会使用，在此之前游戏文件夹不会有任何改动：" ;;
        *:dl_head) echo "These are downloaded from GitHub first. Each is used only when its SHA-256 matches, and until then the game folder is not changed:" ;;
        ja:unchanged) echo "ゲームのフォルダーは何も変わっていません。" ;;
        zh:unchanged) echo "游戏文件夹没有任何改动。" ;;
        *:unchanged) echo "The game folder was not changed." ;;
        ja:zip_missing) echo "zip が見つかりません: $ZIP_PATH" ;;
        zh:zip_missing) echo "找不到 zip：$ZIP_PATH" ;;
        *:zip_missing) echo "The zip was not found: $ZIP_PATH" ;;
        ja:dl_fail) echo "ダウンロードに失敗しました: $BAD_PART" ;;
        zh:dl_fail) echo "下载失败：$BAD_PART" ;;
        *:dl_fail) echo "The download failed: $BAD_PART" ;;
        ja:unpack_failed) echo "zip を展開できませんでした: $BAD_PART" ;;
        zh:unpack_failed) echo "无法解压 zip：$BAD_PART" ;;
        *:unpack_failed) echo "The zip could not be unpacked: $BAD_PART" ;;
        ja:bep_have) echo "BepInEx: 導入済み" ;;
        zh:bep_have) echo "BepInEx：已安装" ;;
        *:bep_have) echo "BepInEx: already installed" ;;
        ja:bep_get) echo "BepInEx: ダウンロード中..." ;;
        zh:bep_get) echo "BepInEx：正在下载..." ;;
        *:bep_get) echo "BepInEx: downloading..." ;;
        ja:bep_bad) echo "BepInEx のダウンロードが改ざんされているか壊れています（SHA-256 不一致）。中止します。" ;;
        zh:bep_bad) echo "下载的 BepInEx 已损坏或被篡改（SHA-256 不一致）。已中止。" ;;
        *:bep_bad) echo "The BepInEx download is corrupt or tampered with (SHA-256 mismatch). Stopping." ;;
        ja:bep_zip_bad) echo "$ZIP_PATH は、公開されている ${BEPINEX_URL##*/} と一致しません（SHA-256 不一致）。使いませんでした。" ;;
        zh:bep_zip_bad) echo "$ZIP_PATH 与公开的 ${BEPINEX_URL##*/} 不一致（SHA-256 不一致），未使用。" ;;
        *:bep_zip_bad) echo "$ZIP_PATH is not ${BEPINEX_URL##*/} as released (SHA-256 mismatch). It was not used." ;;
        ja:bep_ok) echo "BepInEx: 検証 OK、展開しました" ;;
        zh:bep_ok) echo "BepInEx：校验通过，已解压" ;;
        *:bep_ok) echo "BepInEx: verified and unpacked" ;;
        ja:ds_have) echo "Doorstop: 確かめたビルドを導入済み" ;;
        zh:ds_have) echo "Doorstop：已是测试过的版本" ;;
        *:ds_have) echo "Doorstop: the tested build is already in place" ;;
        ja:ds_get) echo "Doorstop: ${DOORSTOP_URL##*/} をダウンロード中..." ;;
        zh:ds_get) echo "Doorstop：正在下载 ${DOORSTOP_URL##*/}..." ;;
        *:ds_get) echo "Doorstop: downloading ${DOORSTOP_URL##*/}..." ;;
        ja:ds_ok) echo "Doorstop: 検証して配置しました" ;;
        zh:ds_ok) echo "Doorstop：校验通过，已放置" ;;
        *:ds_ok) echo "Doorstop: verified and put in place" ;;
        # The pinned Doorstop cannot be used: ds_blocked_text puts together
        # what happened (ds_bad or ds_gone), ds_ci only when DOORSTOP_URL is
        # on UnityDoorstop's ci pre-release itself (not the copy pinned now,
        # nor a release), and ds_blocked.
        ja:ds_bad) echo "ダウンロードした Doorstop は、このスクリプトで確かめたビルドではありません（SHA-256 不一致）。そのため導入していません。" ;;
        zh:ds_bad) echo "下载的 Doorstop 不是此脚本测试过的版本（SHA-256 不一致），因此没有安装。" ;;
        *:ds_bad) echo "The Doorstop download is not the build this script was tested with (SHA-256 mismatch), so it was not installed." ;;
        ja:ds_gone) echo "このスクリプトで確かめた Doorstop のビルド（${DOORSTOP_URL##*/}）は、もうダウンロードできません（GitHub の応答は HTTP ${FETCH_HTTP}）。そのため何も導入していません。" ;;
        zh:ds_gone) echo "此脚本测试过的 Doorstop 版本（${DOORSTOP_URL##*/}）已无法下载（GitHub 返回 HTTP ${FETCH_HTTP}），因此没有安装任何内容。" ;;
        *:ds_gone) echo "The Doorstop build this script was tested with (${DOORSTOP_URL##*/}) can no longer be downloaded (GitHub answered HTTP ${FETCH_HTTP}), so nothing was installed." ;;
        ja:ds_ci) echo "UnityDoorstop の「ci」プレリリースは、master ブランチが変わるたびに同じ URL のまま作り直されます。版が上がるとファイル名が変わり、前のファイルは消えます。これは、壊れているのではなく新しいビルドに替わったためである可能性が高いです。ただし、このスクリプトは新しいビルドを固定していません。次は UnityDoorstop 4.6.0 の安定版が出る見込みで、それを試してから固定します。" ;;
        zh:ds_ci) echo "UnityDoorstop 的「ci」预发布版每当 master 分支变化时都会以相同的 URL 重新生成；版本号提升时文件名也会改变，旧文件会被删除。因此这很可能是换成了更新的版本，而不是文件损坏；但此脚本没有固定新版本。预计接下来会发布 UnityDoorstop 4.6.0 稳定版，测试后脚本将固定它。" ;;
        *:ds_ci) echo "UnityDoorstop's \"ci\" pre-release is made again under the same URL whenever its master branch changes, and when its version goes up, the file gets a new name and the old one is removed. So this most likely means a newer build rather than a damaged one, but the script does not pin that build. A stable UnityDoorstop 4.6.0 release is expected next; the script will pin it once it has been tested." ;;
        ja:ds_blocked) echo "スクリプトの管理者が、ゲームで確かめてあってダウンロードできる Doorstop のビルドをスクリプトに固定するまで、このスクリプトではインストールできません。
進み具合は $MACOS_ISSUE で確かめられます。このことがまだ書かれていなければ、https://github.com/TomXV/dragnwash-localization/issues で知らせてください。
確かめたビルド（${DOORSTOP_URL##*/}、SHA-256 ${DOORSTOP_SHA256}）を保存してあれば、--doorstop-zip <そのファイル> を付けてもう一度実行するとインストールできます。" ;;
        zh:ds_blocked) echo "在脚本维护者把一个用游戏测试过、并且可以下载的 Doorstop 版本固定到脚本中之前，无法用此脚本安装。
进展可在 $MACOS_ISSUE 查看；如果那里还没有提到这个问题，请在 https://github.com/TomXV/dragnwash-localization/issues 报告。
如果保存了测试过的版本（${DOORSTOP_URL##*/}，SHA-256 ${DOORSTOP_SHA256}），加上 --doorstop-zip <该文件> 再次运行即可安装。" ;;
        *:ds_blocked) echo "Installing with this script is blocked until its maintainer has pinned a Doorstop build that was tested with the game and can be downloaded.
You can follow that at $MACOS_ISSUE. If this is not mentioned there yet, please report it at https://github.com/TomXV/dragnwash-localization/issues.
If you kept a copy of the tested build (${DOORSTOP_URL##*/}, SHA-256 $DOORSTOP_SHA256), run the script again with --doorstop-zip <that file> to install with it." ;;
        ja:ds_pin_bad) echo "Doorstop の zip に、このスクリプトが想定する libdoorstop.dylib が入っていません（${BAD_PART}、SHA-256 不一致）。スクリプトに固定した値どうしが合っていません。https://github.com/TomXV/dragnwash-localization/issues で知らせてください。" ;;
        zh:ds_pin_bad) echo "Doorstop 的 zip 中没有此脚本预期的 libdoorstop.dylib（${BAD_PART}，SHA-256 不一致）。脚本中固定的值互相不符，请在 https://github.com/TomXV/dragnwash-localization/issues 报告。" ;;
        *:ds_pin_bad) echo "The Doorstop zip does not hold the libdoorstop.dylib this script expects ($BAD_PART, SHA-256 mismatch). The values pinned in the script do not fit together; please report this at https://github.com/TomXV/dragnwash-localization/issues." ;;
        ja:ds_zip_bad) echo "$ZIP_PATH は、このスクリプトで確かめた Doorstop のビルドではありません（SHA-256 不一致）。使いませんでした。使えるのは、SHA-256 が $DOORSTOP_SHA256 の ${DOORSTOP_URL##*/} だけです。" ;;
        zh:ds_zip_bad) echo "$ZIP_PATH 不是此脚本测试过的 Doorstop 版本（SHA-256 不一致），未使用。只接受 SHA-256 为 $DOORSTOP_SHA256 的 ${DOORSTOP_URL##*/}。" ;;
        *:ds_zip_bad) echo "$ZIP_PATH is not the Doorstop build this script was tested with (SHA-256 mismatch). It was not used. Only ${DOORSTOP_URL##*/} with SHA-256 $DOORSTOP_SHA256 is accepted." ;;
        ja:rb_ok) echo "run_bepinex.sh: Rosetta（x86_64）で起動し、Steam オーバーレイを引き継ぐよう設定しました" ;;
        zh:rb_ok) echo "run_bepinex.sh：已设置为在 Rosetta（x86_64）下启动，并传递 Steam 覆盖界面" ;;
        *:rb_ok) echo "run_bepinex.sh: set to start the game as x86_64 (Rosetta) and to pass the Steam overlay on" ;;
        ja:rb_same) echo "run_bepinex.sh: 設定済み" ;;
        zh:rb_same) echo "run_bepinex.sh：已设置" ;;
        *:rb_same) echo "run_bepinex.sh: already set up" ;;
        ja:backup_kept) echo "元のファイルを次の名前で残しました:" ;;
        zh:backup_kept) echo "原文件已保留为" ;;
        *:backup_kept) echo "the previous copy is kept as" ;;
        ja:rb_bad) echo "run_bepinex.sh が、このインストーラーの知っている Doorstop のスクリプトと違うため、書き換えていません（${BAD_PART}）。ゲームのフォルダーから run_bepinex.sh と libdoorstop.dylib をどけてからインストールをもう一度実行すると、入れ直します。" ;;
        zh:rb_bad) echo "run_bepinex.sh 与此安装程序已知的 Doorstop 脚本不同，因此未修改（${BAD_PART}）。请从游戏文件夹中移走 run_bepinex.sh 和 libdoorstop.dylib，然后再次运行安装即可重新放置。" ;;
        *:rb_bad) echo "run_bepinex.sh is not the Doorstop script this installer knows, so it was not changed ($BAD_PART). Move run_bepinex.sh and libdoorstop.dylib out of the game folder and run the install again to get fresh ones." ;;
        ja:fw_get) echo "ModFramework: DragNWash.ModFramework-$FW_VERSION.zip をダウンロード中..." ;;
        zh:fw_get) echo "ModFramework：正在下载 DragNWash.ModFramework-$FW_VERSION.zip..." ;;
        *:fw_get) echo "ModFramework: downloading DragNWash.ModFramework-$FW_VERSION.zip ..." ;;
        ja:fw_ok) echo "ModFramework: $FW_VERSION 検証 OK（SHA-256）" ;;
        zh:fw_ok) echo "ModFramework：$FW_VERSION 校验通过（SHA-256）" ;;
        *:fw_ok) echo "ModFramework: $FW_VERSION verified (SHA-256)" ;;
        ja:fw_have) echo "ModFramework: $FW_HAVE 導入済み（この Mod にはこれで足ります）" ;;
        zh:fw_have) echo "ModFramework：已安装 ${FW_HAVE}，满足此 Mod 的需要" ;;
        *:fw_have) echo "ModFramework: $FW_HAVE installed, enough for this mod" ;;
        ja:fw_off) echo "ModFramework: 次のライブラリはオフになっているため、そのままにしました:${FW_OFF}
この Mod はこれらを使うので、どれか 1 つでもオフだと読み込まれません。使うときは、ゲームの Mods 画面でオンに戻し、ゲームを起動し直してください。自分で .dll.disabled に名前を変えたファイルは、.dll に戻すとオンになります。" ;;
        zh:fw_off) echo "ModFramework：以下库已被关闭，因此保持原样：${FW_OFF}
此 Mod 需要它们，只要有一个关闭就不会加载。如需使用，请在游戏的 Mods 界面中重新开启它们，然后重新启动游戏。自己改名为 .dll.disabled 的文件，改回 .dll 即可重新开启。" ;;
        *:fw_off) echo "ModFramework: these libraries are switched off, so they were left as they are:${FW_OFF}
This mod needs them, and it does not load while one of them is off. To use it, switch them back on in the game's Mods screen and restart the game. A file you renamed to .dll.disabled yourself comes back on when you rename it back to .dll." ;;
        ja:fw_off_hand) echo "ModFramework: 次の部分は名前が .dll.disabled に変わっているため、そのままにしました:${FW_OFF_HAND}
これらは Mods 画面ではオン・オフできません。オンに戻すときは、.dll.disabled の名前を .dll に戻してください。" ;;
        zh:fw_off_hand) echo "ModFramework：以下部分已被改名为 .dll.disabled，因此保持原样：${FW_OFF_HAND}
Mods 界面无法开启或关闭它们。如需重新开启，请把 .dll.disabled 改回 .dll。" ;;
        *:fw_off_hand) echo "ModFramework: these parts are renamed to .dll.disabled, so they were left as they are:${FW_OFF_HAND}
The Mods screen cannot switch them on or off. To switch one back on, rename its .dll.disabled back to .dll." ;;
        ja:fw_off_pre) echo "Preloader がオフの間は、Mods 画面で Mod をオン・オフしても反映されません。" ;;
        zh:fw_off_pre) echo "Preloader 关闭期间，在 Mods 界面中开启或关闭 Mod 不会生效。" ;;
        *:fw_off_pre) echo "While the Preloader is off, switching mods on or off in the Mods screen does not take effect." ;;
        ja:fw_off_old) echo "このうち、この Mod が必要とする版より古いものがあります。オンに戻ったら、このインストールをもう一度実行して更新してください。" ;;
        zh:fw_off_old) echo "其中有的版本低于此 Mod 的要求。重新开启后，请再运行一次安装来更新它们。" ;;
        *:fw_off_old) echo "Some of them are older than this mod needs. Once they are back on, run this install again to update them." ;;
        ja:fw_bad) echo "ModFramework $FW_VERSION のダウンロードが、この Mod のリリースに記録されたものと一致しません（${BAD_WHAT}）。使いませんでした。" ;;
        zh:fw_bad) echo "下载的 ModFramework $FW_VERSION 与此 Mod 发布版中记录的不一致（${BAD_WHAT}），未使用。" ;;
        *:fw_bad) echo "The ModFramework $FW_VERSION download does not match the one recorded in this mod's release ($BAD_WHAT). It was not used." ;;
        ja:fw_zip_bad) echo "$ZIP_PATH は、この Mod のリリースに記録された ModFramework $FW_VERSION と一致しません（${BAD_WHAT}）。使いませんでした。" ;;
        zh:fw_zip_bad) echo "$ZIP_PATH 与此 Mod 发布版中记录的 ModFramework $FW_VERSION 不一致（${BAD_WHAT}），未使用。" ;;
        *:fw_zip_bad) echo "$ZIP_PATH is not the ModFramework $FW_VERSION recorded in this mod's release ($BAD_WHAT). It was not used." ;;
        ja:fw_short) echo "ModFramework $FW_VERSION には、この Mod が必要とする版の $BAD_PART がありません。Mod の作者に知らせてください。" ;;
        zh:fw_short) echo "ModFramework $FW_VERSION 中没有此 Mod 所需版本的 ${BAD_PART}。请告知 Mod 作者。" ;;
        *:fw_short) echo "ModFramework $FW_VERSION does not have $BAD_PART in the version this mod needs. Please tell the mod's author." ;;
        ja:fw_nomanifest) echo "ゲームのフォルダーに Drag'n Wash ModFramework がありません。Mod の隣に mod-install.json がないので、どのリリースが必要か分かりません。https://github.com/$FRAMEWORK_REPO/releases からフレームワークの zip を入手し、その中の BepInEx フォルダーをゲームのフォルダーに展開してください。" ;;
        zh:fw_nomanifest) echo "游戏文件夹中没有 Drag'n Wash ModFramework。Mod 旁边没有 mod-install.json，无法得知需要哪个版本。请从 https://github.com/$FRAMEWORK_REPO/releases 获取框架的 zip，并将其中的 BepInEx 文件夹解压到游戏文件夹。" ;;
        *:fw_nomanifest) echo "Drag'n Wash ModFramework is not in the game folder, and there is no mod-install.json next to the mod to say which release it needs. Get the framework's zip from https://github.com/$FRAMEWORK_REPO/releases and extract its BepInEx folder into the game folder." ;;
        ja:fw_kept) echo "ModFramework: 他の Mod があるため残しました" ;;
        zh:fw_kept) echo "ModFramework：存在其他 Mod，已保留" ;;
        *:fw_kept) echo "ModFramework: kept, other mods are installed" ;;
        ja:fw_removed) echo "ModFramework: 削除しました" ;;
        zh:fw_removed) echo "ModFramework：已删除" ;;
        *:fw_removed) echo "ModFramework: removed" ;;
        ja:fw_ask) echo "ゲームのフォルダーに Drag'n Wash ModFramework がありますが、このスクリプトが入れたものではありません（前からあったか、手作業で入れたものです）。これも削除しますか？
BepInEx/plugins にある DragNWash.ModFramework のフォルダーすべてと、その Preloader と設定ファイルを消します。" ;;
        zh:fw_ask) echo "游戏文件夹中有 Drag'n Wash ModFramework，但它不是此脚本安装的（之前就有，或是手动放入的）。也要删除它吗？
这会删除 BepInEx/plugins 中所有 DragNWash.ModFramework 文件夹、它的 Preloader 和设置文件。" ;;
        *:fw_ask) echo "Drag'n Wash ModFramework is in the game folder, but this script did not install it (it was there before, or was put there by hand). Remove it too?
That deletes every DragNWash.ModFramework folder in BepInEx/plugins, its Preloader and its settings." ;;
        ja:fw_not_ours) echo "ModFramework: このスクリプトで入れたものではないため、残しました" ;;
        zh:fw_not_ours) echo "ModFramework：不是此脚本安装的，已保留" ;;
        *:fw_not_ours) echo "ModFramework: kept, this script did not install it" ;;
        ja:fw_removed_ours) echo "ModFramework: このスクリプトで入れた部分を削除しました" ;;
        zh:fw_removed_ours) echo "ModFramework：已删除此脚本安装的部分" ;;
        *:fw_removed_ours) echo "ModFramework: removed the parts this script installed" ;;
        ja:fw_left) echo "ModFramework: このスクリプトで入れたものではない次の部分は残しました:" ;;
        zh:fw_left) echo "ModFramework：以下部分不是此脚本安装的，已保留：" ;;
        *:fw_left) echo "ModFramework: kept these parts, which this script did not install:" ;;
        ja:off_tag) echo "（オフ）" ;;
        zh:off_tag) echo "（已关闭）" ;;
        *:off_tag) echo " (switched off)" ;;
        ja:fw_left_off) echo "（オフ）と書いた部分は、オフのままです。オンに戻す役の Preloader は、ほかの部分と一緒に削除しました。また使うときは、その .dll.disabled の名前を .dll に戻してください。" ;;
        zh:fw_left_off) echo "标有（已关闭）的部分将保持关闭：负责重新开启它们的 Preloader 已随其他部分一起删除。如需再次使用，请把它的 .dll.disabled 改回 .dll。" ;;
        *:fw_left_off) echo "A part marked (switched off) stays off: the Preloader, which switches parts back on, was removed with the rest. To use one again, rename its .dll.disabled back to .dll." ;;
        ja:list_failed) echo "BepInEx/config/${BAD_PART} を更新できませんでした。そのため、その中の ${BAD_WHAT} の行（オフにする、またはアンインストールする）は、次にゲームを起動したときにも効きます。${BAD_WHAT} で始まる行を手で消してください。" ;;
        zh:list_failed) echo "无法更新 BepInEx/config/${BAD_PART}，因此其中 ${BAD_WHAT} 的行（关闭或卸载）在下次启动游戏时仍会生效。请手动删除以 ${BAD_WHAT} 开头的行。" ;;
        *:list_failed) echo "BepInEx/config/$BAD_PART could not be updated, so its lines for $BAD_WHAT (switch off, or uninstall) still apply at the next start of the game. Remove the lines that start with $BAD_WHAT by hand." ;;
        ja:bep_kept_fw) echo "BepInEx と Steam の起動オプション: ModFramework が残っていて使うため、残しました" ;;
        zh:bep_kept_fw) echo "BepInEx 和 Steam 启动选项：ModFramework 仍在并需要它们，已保留" ;;
        *:bep_kept_fw) echo "BepInEx and the Steam launch option: kept, ModFramework is still there and needs them" ;;
        ja:mod_ok) echo "Mod: ファイルをコピーしました" ;;
        zh:mod_ok) echo "Mod：文件已复制" ;;
        *:mod_ok) echo "Mod: files copied" ;;
        ja:mod_on) echo "Mod: Mods 画面でオフ、またはアンインストール予定になっていたので、オンに戻しました" ;;
        zh:mod_on) echo "Mod：它在 Mods 界面中被关闭或被设为卸载，已重新开启" ;;
        *:mod_on) echo "Mod: it was switched off or set to be uninstalled in the Mods screen; it is switched back on" ;;
        ja:lang_set) echo "言語を設定しました:" ;;
        zh:lang_set) echo "语言已设置：" ;;
        *:lang_set) echo "Language set to" ;;
        ja:lo_same) echo "起動オプション: 設定済み" ;;
        zh:lo_same) echo "启动选项：已设置" ;;
        *:lo_same) echo "Launch option: already set" ;;
        ja:lo_other) echo "起動オプションに、別の場所の run_bepinex.sh が設定されています。Steam でゲームのプロパティ → 起動オプション を次の内容に直してください:" ;;
        zh:lo_other) echo "启动选项中设置了其他位置的 run_bepinex.sh。请在 Steam 中打开游戏属性 → 启动选项，改为以下内容：" ;;
        *:lo_other) echo "The launch option points at a run_bepinex.sh somewhere else. In Steam, game Properties → Launch Options, change it to:" ;;
        ja:lo_ask) echo "Steam の起動オプションを変更するため、Steam を一度終了します。Steam は起動中に起動オプションを上書きするので、終了しないと変更が反映されません。変更後に Steam を自動で起動し直します。

Steam を終了して続けますか？（「いいえ」の場合は、起動オプションを手動で変更してください）" ;;
        zh:lo_ask) echo "要修改 Steam 启动选项，需要先关闭 Steam。Steam 运行时会覆盖启动选项，不关闭则修改不会生效。修改后会自动重新启动 Steam。

关闭 Steam 并继续吗？（选择“否”则需要手动修改启动选项）" ;;
        *:lo_ask) echo "Steam will be closed briefly to change the launch option. Steam overwrites launch options while it runs, so the change only sticks with Steam closed. Steam is started again afterwards.

Close Steam and continue? (If not, change the launch option by hand.)" ;;
        ja:lo_manual) echo "起動オプションは手動で設定してください: Steam でゲームのプロパティ → 起動オプション に次を入力" ;;
        zh:lo_manual) echo "请手动设置启动选项：在 Steam 中打开游戏属性 → 启动选项，输入以下内容" ;;
        *:lo_manual) echo "Set the launch option by hand: in Steam, game Properties → Launch Options, enter" ;;
        ja:lo_done) echo "起動オプションを設定しました:" ;;
        zh:lo_done) echo "启动选项已设置：" ;;
        *:lo_done) echo "Launch option set:" ;;
        ja:attention) echo "【要確認】うまくいかなかった手順があります:" ;;
        zh:attention) echo "【请注意】有步骤未能完成：" ;;
        *:attention) echo "Something needs your attention:" ;;
        ja:steam_slow) echo "Steam が終了しなかったため、起動オプションを設定できませんでした。" ;;
        zh:steam_slow) echo "Steam 没有退出，无法设置启动选项。" ;;
        *:steam_slow) echo "Steam did not exit, so the launch option could not be set." ;;
        ja:steam_slow_remove) echo "Steam が終了しなかったため、起動オプションを変更できませんでした。" ;;
        zh:steam_slow_remove) echo "Steam 没有退出，无法修改启动选项。" ;;
        *:steam_slow_remove) echo "Steam did not exit, so the launch option could not be changed." ;;
        ja:lo_manual_remove) echo "起動オプションは手動で元に戻してください: Steam でゲームのプロパティ → 起動オプション から run_bepinex.sh の部分を消す" ;;
        zh:lo_manual_remove) echo "请手动恢复启动选项：在 Steam 中打开游戏属性 → 启动选项，删除 run_bepinex.sh 部分" ;;
        *:lo_manual_remove) echo "Restore the launch option by hand: in Steam, game Properties → Launch Options, remove the run_bepinex.sh part" ;;
        ja:lo_removed) echo "起動オプションを元に戻しました" ;;
        zh:lo_removed) echo "启动选项已恢复" ;;
        *:lo_removed) echo "Launch option restored" ;;
        ja:steam_wait) echo "Steam の終了を待っています..." ;;
        zh:steam_wait) echo "正在等待 Steam 退出..." ;;
        *:steam_wait) echo "Waiting for Steam to exit..." ;;
        ja:done) echo "完了しました。いつもどおり Steam の「プレイ」からゲームを起動してください。
・ターミナルから起動すると Steam API が使えず、セーブが見つかりません。
・起動中にゲームのウィンドウが前面にないと、タイトル画面の前で止まったように見えることがあります。ウィンドウをクリックすると進みます。
起動したあとでこのスクリプトをもう一度実行し「動作確認」（--check）を選ぶと、Mod が読み込まれたか確かめられます。" ;;
        zh:done) echo "完成。请像平常一样从 Steam 的「开始游戏」启动游戏。
・从终端启动时无法使用 Steam API，找不到存档。
・启动过程中，如果游戏窗口不在最前面，标题画面出现前可能看起来像卡住了。点击游戏窗口即可继续。
启动后再次运行此脚本并选择「检查运行状态」（--check），即可确认 Mod 是否已加载。" ;;
        *:done) echo "Done. Start the game from Steam's Play button, as usual.
- Started from Terminal, the game has no Steam API and does not find your saves.
- While the game starts, it can look frozen before the title screen if its window is not in front. Click the window and it goes on.
Afterwards, run this script again and choose Check (--check) to see whether the mod loaded." ;;
        ja:keep) echo "セーブ履歴と翻訳作業ファイルは残しますか？" ;;
        zh:keep) echo "保留存档历史和翻译工作文件吗？" ;;
        *:keep) echo "Keep save history and translation working files?" ;;
        ja:rmbep) echo "BepInEx も削除しますか？（BepInEx を使う Mod はほかにありません）" ;;
        zh:rmbep) echo "同时删除 BepInEx 吗？（没有其他使用 BepInEx 的 Mod）" ;;
        *:rmbep) echo "Also remove BepInEx? (no other mod uses it)" ;;
        ja:bep_kept) echo "BepInEx: 他の Mod があるため残しました" ;;
        zh:bep_kept) echo "BepInEx：存在其他 Mod，已保留" ;;
        *:bep_kept) echo "BepInEx: kept, other mods use it" ;;
        ja:bep_removed) echo "BepInEx: 削除しました" ;;
        zh:bep_removed) echo "BepInEx：已删除" ;;
        *:bep_removed) echo "BepInEx: removed" ;;
        ja:mod_removed) echo "Mod: 削除しました" ;;
        zh:mod_removed) echo "Mod：已删除" ;;
        *:mod_removed) echo "Mod: removed" ;;
        ja:undone) echo "アンインストールが完了しました。" ;;
        zh:undone) echo "卸载完成。" ;;
        *:undone) echo "Uninstall finished." ;;
        ja:confirm_install) echo "次のフォルダーに Mod を導入します。よろしいですか？" ;;
        zh:confirm_install) echo "将把 Mod 安装到以下文件夹。继续吗？" ;;
        *:confirm_install) echo "Install the mod into this folder?" ;;
        ja:confirm_uninstall) echo "次のフォルダーから Mod を削除します。よろしいですか？" ;;
        zh:confirm_uninstall) echo "将从以下文件夹删除 Mod。继续吗？" ;;
        *:confirm_uninstall) echo "Remove the mod from this folder?" ;;
        ja:action) echo "何をしますか？" ;;
        zh:action) echo "要执行什么操作？" ;;
        *:action) echo "What would you like to do?" ;;
        ja:act_install) echo "インストール / 更新" ;;
        zh:act_install) echo "安装 / 更新" ;;
        *:act_install) echo "Install / Update" ;;
        ja:act_uninstall) echo "アンインストール" ;;
        zh:act_uninstall) echo "卸载" ;;
        *:act_uninstall) echo "Uninstall" ;;
        ja:act_check) echo "動作確認" ;;
        zh:act_check) echo "检查运行状态" ;;
        *:act_check) echo "Check" ;;
        ja:st_yes) echo "導入済み" ;;
        zh:st_yes) echo "已安装" ;;
        *:st_yes) echo "installed" ;;
        ja:st_no) echo "未導入" ;;
        zh:st_no) echo "未安装" ;;
        *:st_no) echo "not installed" ;;
        ja:nothing) echo "Mod は導入されていません。" ;;
        zh:nothing) echo "尚未安装 Mod。" ;;
        *:nothing) echo "The mod is not installed." ;;
        ja:btn_yes) echo "はい" ;;
        zh:btn_yes) echo "是" ;;
        *:btn_yes) echo "Yes" ;;
        ja:btn_no) echo "いいえ" ;;
        zh:btn_no) echo "否" ;;
        *:btn_no) echo "No" ;;
        ja:chk_loaded) echo "Mod は読み込まれています。" ;;
        zh:chk_loaded) echo "Mod 已加载。" ;;
        *:chk_loaded) echo "The mod is loaded." ;;
        ja:chk_bep_only) echo "BepInEx は起動しましたが、Mod が読み込まれていません。次のログを確認してください:" ;;
        zh:chk_bep_only) echo "BepInEx 已启动，但 Mod 未加载。请查看以下日志：" ;;
        *:chk_bep_only) echo "BepInEx started, but the mod did not load. See this log:" ;;
        ja:chk_no_chain) echo "BepInEx は起動しましたが、プラグインの読み込みが終わっていません（「Chainloader startup complete」がありません）。次のログを確認してください:" ;;
        zh:chk_no_chain) echo "BepInEx 已启动，但没有完成插件加载（没有「Chainloader startup complete」）。请查看以下日志：" ;;
        *:chk_no_chain) echo "BepInEx started, but did not finish loading plugins (there is no \"Chainloader startup complete\"). See this log:" ;;
        ja:chk_stale) echo "BepInEx/LogOutput.log が、最後にゲームを起動した時刻（${LAST_START_TEXT}）より古いままです。そのときは BepInEx が起動していません。" ;;
        zh:chk_stale) echo "BepInEx/LogOutput.log 早于最后一次启动游戏的时间（${LAST_START_TEXT}），说明那次 BepInEx 没有启动。" ;;
        *:chk_stale) echo "BepInEx/LogOutput.log is older than the last start of the game ($LAST_START_TEXT), so BepInEx did not start that time." ;;
        ja:chk_not_started) echo "導入後にゲームは起動されていますが、BepInEx が起動していません（BepInEx/LogOutput.log がありません）。" ;;
        zh:chk_not_started) echo "安装后游戏已启动过，但 BepInEx 没有启动（没有 BepInEx/LogOutput.log）。" ;;
        *:chk_not_started) echo "The game has been started since the install, but BepInEx did not start (there is no BepInEx/LogOutput.log)." ;;
        ja:chk_no_launch_option) echo "Steam の起動オプションが設定されていません。インストールをもう一度実行するか、手動で設定してください:" ;;
        zh:chk_no_launch_option) echo "尚未设置 Steam 启动选项。请重新运行安装，或手动设置：" ;;
        *:chk_no_launch_option) echo "The Steam launch option is not set. Run the install again, or set it by hand:" ;;
        ja:chk_from_steam) echo "起動オプションは設定済みです。ゲームは Steam の「プレイ」から起動してください。run_bepinex.sh は、起動オプションを通したときだけ実行されます。" ;;
        zh:chk_from_steam) echo "启动选项已设置。请通过 Steam 的「开始游戏」启动游戏：run_bepinex.sh 只有通过启动选项才会运行。" ;;
        *:chk_from_steam) echo "The launch option is set. Start the game with Steam's Play button: run_bepinex.sh only runs through the launch option." ;;
        ja:chk_not_run) echo "導入後、まだゲームを起動していないようです。Steam から一度起動して、終了してからもう一度確認してください。" ;;
        zh:chk_not_run) echo "安装后似乎还没有启动过游戏。请先从 Steam 启动一次，退出后再检查。" ;;
        *:chk_not_run) echo "The game does not seem to have been started since the install. Start it once from Steam, quit, and check again." ;;
        ja:chk_arch_x86) echo "ゲームは今、x86_64（Rosetta）で動いています。これで正しい状態です。" ;;
        zh:chk_arch_x86) echo "游戏当前以 x86_64（Rosetta）运行，这是正确的。" ;;
        *:chk_arch_x86) echo "The game is running as x86_64 (Rosetta) right now, as it should." ;;
        ja:chk_arch_arm) echo "ゲームは今、ネイティブ（arm64）で動いています。この状態では BepInEx がパッチを当てられません。インストールをもう一度実行すると、run_bepinex.sh の archpreference が \"x86_64\" に戻ります。" ;;
        zh:chk_arch_arm) echo "游戏当前以原生（arm64）运行。这样 BepInEx 无法应用补丁。再次运行安装会把 run_bepinex.sh 中的 archpreference 改回 \"x86_64\"。" ;;
        *:chk_arch_arm) echo "The game is running natively (arm64) right now, and BepInEx cannot patch it that way. Running the install again sets archpreference back to \"x86_64\" in run_bepinex.sh." ;;
        ja:chk_arch_log) echo "BepInEx も Unity もログにアーキテクチャを書かないので、x86_64 で動いたかはゲームの実行中にしか確かめられません（そのときに --check を実行してください）。run_bepinex.sh の archpreference: $ARCH_PREF" ;;
        zh:chk_arch_log) echo "BepInEx 和 Unity 都不会在日志中记录架构，因此只能在游戏运行时确认是否以 x86_64 运行（请在那时运行 --check）。run_bepinex.sh 的 archpreference：$ARCH_PREF" ;;
        *:chk_arch_log) echo "Neither BepInEx nor Unity writes the architecture to its log, so whether the game ran as x86_64 can only be seen while it runs (run --check then). archpreference in run_bepinex.sh: $ARCH_PREF" ;;
        *) echo "$key" ;;
    esac
}

# -------------------------------------------------------------- dialogs ----
GUI=0
if [ "$ASSUME_YES" -eq 0 ] && [ "$FORCE_TERMINAL" -eq 0 ] && [ -z "${SSH_CONNECTION:-}" ] &&
    command -v osascript >/dev/null 2>&1; then
    GUI=1
fi

LOG="$HOME/Library/Logs/dragnwash-localization/installer.log"
mkdir -p "$(dirname "$LOG")" 2>/dev/null || true
log() { printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >> "$LOG" 2>/dev/null || true; }
say() { echo "$*"; log "$*"; }
warn() { say "$*"; WARNINGS="${WARNINGS}${WARNINGS:+

}$*"; }
# What an uninstall left in place on purpose. The dialog only shows the last
# message, so these go into it as well as on screen.
NOTES=""
note() { say "$*"; NOTES="${NOTES}${NOTES:+
}$*"; }

# AppleScript receives every piece of text through argv, so nothing needs quoting.
dialog_message() {  # dialog_message <text> note|caution|stop
    osascript -e 'on run argv' \
        -e 'display dialog (item 2 of argv) with title (item 1 of argv) buttons {"OK"} default button 1 with icon (item 3 of argv)' \
        -e 'end run' "$(t title)" "$1" "$2" >/dev/null 2>&1 || true
}
dialog_yesno() {  # dialog_yesno <text> <default 1|0> ; 0 for yes
    local def reply
    if [ "$2" -eq 1 ]; then def="$(t btn_yes)"; else def="$(t btn_no)"; fi
    reply="$(osascript -e 'on run argv' \
        -e 'set r to display dialog (item 2 of argv) with title (item 1 of argv) buttons {item 3 of argv, item 4 of argv} default button (item 5 of argv)' \
        -e 'return button returned of r' \
        -e 'end run' "$(t title)" "$1" "$(t btn_no)" "$(t btn_yes)" "$def" 2>/dev/null || true)"
    [ "$reply" = "$(t btn_yes)" ]
}
dialog_choose() {  # dialog_choose <prompt> <default item> <item>... ; prints the item or nothing
    osascript -e 'on run argv' \
        -e 'set r to choose from list (items 4 thru -1 of argv) with title (item 1 of argv) with prompt (item 2 of argv) default items {item 3 of argv}' \
        -e 'if r is false then return ""' \
        -e 'return item 1 of r' \
        -e 'end run' "$(t title)" "$@" 2>/dev/null || true
}

fail() {
    echo "ERROR: $*" >&2
    log "ERROR: $*"
    if [ "$GUI" -eq 1 ]; then dialog_message "$*" stop; fi
    exit 1
}
ask_yes() {  # ask_yes "question" default(1=yes,0=no)
    local q="$1" def="${2:-1}"
    if [ "$ASSUME_YES" -eq 1 ]; then [ "$def" -eq 1 ]; return; fi
    if [ "$GUI" -eq 1 ]; then dialog_yesno "$q" "$def"; return; fi
    local hint="[Y/n]" reply
    [ "$def" -eq 0 ] && hint="[y/N]"
    read -r -p "$q $hint " reply || reply=""
    case "$reply" in
        [Yy]*) return 0 ;;
        [Nn]*) return 1 ;;
        *) [ "$def" -eq 1 ] ;;
    esac
}
finish_message() {
    local text="$1"
    if [ -n "$WARNINGS" ]; then
        text="$(t attention)

$WARNINGS

$1"
    fi
    say "$text"
    if [ "$GUI" -eq 1 ]; then
        if [ -n "$WARNINGS" ]; then dialog_message "$text" caution; else dialog_message "$text" note; fi
    fi
}

# ------------------------------------------------------------ discovery ----
library_paths() {
    echo "$STEAM_ROOT"
    local vdf="$STEAM_ROOT/steamapps/libraryfolders.vdf"
    if [ -f "$vdf" ]; then
        sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf"
    fi
}

find_game() {
    local lib dir name
    while IFS= read -r lib; do
        [ -n "$lib" ] || continue
        name="Drag'n Wash"
        if [ -f "$lib/steamapps/appmanifest_$APP_ID.acf" ]; then
            name="$(sed -n 's/^[[:space:]]*"installdir"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$lib/steamapps/appmanifest_$APP_ID.acf" | head -1)"
        fi
        dir="$lib/steamapps/common/$name"
        if [ -d "$dir/$APP_BUNDLE" ]; then echo "$dir"; return 0; fi
    done < <(library_paths | awk '!seen[$0]++')
    return 1
}

game_running() { pgrep -f "$GAME_PROCESS" >/dev/null 2>&1; }
steam_running() { [ "$TEST_STEAM" -eq 0 ] && pgrep -x steam_osx >/dev/null 2>&1; }

# Until BepInEx ships BepInEx/BepInEx#1402, the game has to run as x86_64,
# which on Apple silicon means Rosetta. Installing Rosetta needs the user to
# agree to Apple's licence, so this only checks. On an Intel Mac x86_64 is
# native and the check passes as well.
rosetta_ok() { /usr/bin/arch -x86_64 /usr/bin/true >/dev/null 2>&1; }

available_locales() {  # prints "code<TAB>name" lines; English first
    printf 'en\tEnglish\n'
    local d code name
    for d in "$PAYLOAD/Translations"/*/; do
        [ -d "$d" ] || continue
        code="$(basename "$d")"
        case "$code" in _*) continue ;; esac
        name="$code"
        [ -f "$d/name.txt" ] && name="$(head -1 "$d/name.txt" | tr -d '\r')"
        printf '%s\t%s\n' "$code" "$name"
    done
}

# ------------------------------------------------------------- helpers ----
# DLL versions and mod-install.json are read with JavaScript for Automation,
# for the same reason as the launch options below: every Mac has it, while
# python3 may only be a stub that offers to install the developer tools.
HELPER_JS="$WORK/helper.js"
cat > "$HELPER_JS" <<'JS'
// dllver <file>: the FileVersion from a .NET DLL's version resource, or
// nothing. The file is decoded as Latin-1 so that every byte stays one
// character, and the UTF-16 key and value are matched byte by byte.
// framework <mod-install.json>: the "framework" block, checked as
// install-steamdeck.sh checks it: version, sha256 and size on the first three
// lines, then "<folder><TAB><minimum>" for each of its "needs". Nothing when
// there is no block; "error: <reason>" when the file cannot be used.
function run(argv) {
    ObjC.import('Foundation');
    const [cmd, path] = argv;
    if (cmd === 'dllver') {
        const data = $.NSData.dataWithContentsOfFile(path);
        if (data.isNil()) return '';
        const s = $.NSString.alloc.initWithDataEncoding(data, $.NSISOLatin1StringEncoding).js;
        const m = /F\0i\0l\0e\0V\0e\0r\0s\0i\0o\0n\0\0+((?:[0-9.]\0)+)/.exec(s);
        return m ? m[1].replace(/\0/g, '') : '';
    }
    const str = $.NSString.stringWithContentsOfFileEncodingError(path, $.NSUTF8StringEncoding, null);
    if (str.isNil()) return 'error: mod-install.json could not be read';
    let m;
    try {
        m = JSON.parse(str.js.replace(/^﻿/, ''));
    } catch (e) {
        return 'error: mod-install.json is not valid JSON';
    }
    if (!m || typeof m !== 'object') return 'error: mod-install.json is not a JSON object';
    if (!Number.isInteger(m.schema) || m.schema < 1 || m.schema > 2) {
        return `error: mod-install.json schema ${m.schema} is not supported (this script reads 1 to 2)`;
    }
    // Schema 1 has no framework block; one there is left alone.
    const fw = m.framework;
    if (m.schema < 2 || fw === undefined || fw === null) return '';
    // These values end up in a URL and in file names.
    const VERSION = /^[0-9]{1,9}(\.[0-9]{1,9}){1,3}$/;
    if (typeof fw !== 'object' || Array.isArray(fw)) return 'error: "framework" must be an object';
    if (typeof fw.version !== 'string' || !VERSION.test(fw.version)) return 'error: the framework version must be digits and dots, like 1.4.3';
    if (typeof fw.sha256 !== 'string' || !/^[0-9a-f]{64}$/.test(fw.sha256)) return 'error: the framework sha256 must be 64 lowercase hex digits';
    if (!Number.isInteger(fw.size) || fw.size < 1 || fw.size > 20 * 1024 * 1024) return 'error: the framework size must be a whole number of bytes up to 20 MB';
    const needs = fw.needs || {};
    if (typeof needs !== 'object' || Array.isArray(needs)) return 'error: framework "needs" must be an object of folder name: minimum version';
    const lines = [fw.version, fw.sha256, String(fw.size)];
    for (const k of Object.keys(needs)) {
        if (!/^DragNWash\.ModFramework(\.[A-Za-z0-9]{1,64})?$/.test(k)) return `error: "${k}" in framework needs is not a ModFramework folder name`;
        if (typeof needs[k] !== 'string' || !VERSION.test(needs[k])) return `error: the minimum for ${k} must be digits and dots, like 1.2.0`;
        lines.push(k + '\t' + needs[k]);
    }
    return lines.join('\n');
}
JS

dll_version() {  # dll_version <file>: its FileVersion, or nothing
    [ -f "$1" ] || return 0
    osascript -l JavaScript "$HELPER_JS" dllver "$1" 2>/dev/null || true
}

# Compares two versions part by part, a missing part counting as 0, so 1.5.0
# and 1.5.0.0 are the same. Prints -1, 0 or 1.
ver_cmp() {
    local a="$1." b="$2." x y i
    for i in 1 2 3 4; do
        x="${a%%.*}" y="${b%%.*}"
        a="${a#*.}" b="${b#*.}"
        case "$x" in ''|*[!0-9]*) x=0 ;; esac
        case "$y" in ''|*[!0-9]*) y=0 ;; esac
        if [ "$((10#$x))" -gt "$((10#$y))" ]; then echo 1; return; fi
        if [ "$((10#$x))" -lt "$((10#$y))" ]; then echo -1; return; fi
    done
    echo 0
}

sha256_of() { shasum -a 256 "$1" | cut -d' ' -f1; }

# 0 when the file has the SHA-256 (and, when one is given, the size);
# BAD_WHAT names what differed, and both values go to the log.
check_file() {  # check_file <file> <sha256> [<size>]
    local size actual
    size="$(wc -c < "$1" | tr -d ' ')"
    if [ -n "${3:-}" ] && [ "$size" != "$3" ]; then
        BAD_WHAT="size"; log "$1: size $size bytes, expected $3"; return 1
    fi
    actual="$(sha256_of "$1")"
    if [ "$actual" != "$2" ]; then
        BAD_WHAT="SHA-256"; log "$1: SHA-256 mismatch: expected $2, actual $actual, $size bytes"; return 1
    fi
    log "$1: SHA-256 OK, $size bytes"
}

# One file from a GitHub release: HTTPS only (redirects too) and nothing over
# 20 MB. -f makes an HTTP error a failure; FETCH_HTTP gets the status of the
# last response ("000" when there was none, empty when curl printed nothing).
# The status is what tells a file that is gone from GitHub (404) from a
# network problem; curl's exit code cannot, since the curl that ships with
# macOS (8.7.1 where this was checked) ends a 404 over HTTP/2 with 56, not 22.
FETCH_HTTP=""
fetch() {  # fetch <url> <file>
    local rc=0
    log "downloading $1"
    FETCH_HTTP="$(curl -fsSL --proto =https --proto-redir =https --connect-timeout 30 --max-time 600 \
        --max-filesize "$MAX_DOWNLOAD" -w '%{http_code}' -o "$2" "$1")" || rc=$?
    if [ "$rc" -ne 0 ]; then
        log "download failed: curl exit $rc, HTTP ${FETCH_HTTP:-?}"
        return "$rc"
    fi
    log "downloaded: HTTP ${FETCH_HTTP:-?}"
}

# Puts a local zip (--*-zip) or a download at <file>; stops when neither works.
# When the URL is no longer there (HTTP 404 or 410) and a fifth argument names
# a function, the message is what that function prints: it knows what a file
# that is gone means. Any other failed download gets the plain message.
get_zip() {  # get_zip <local zip or ""> <url> <file> <downloading message> [<gone message function>]
    if [ -n "$1" ]; then
        ZIP_PATH="$1"
        log "using $1 instead of downloading $2"
        [ -f "$1" ] || fail "$(t zip_missing)
$(t unchanged)"
        cp -f "$1" "$3"
    else
        say "$4"
        BAD_PART="$2"
        if ! fetch "$2" "$3"; then
            if [ -n "${5:-}" ]; then
                case "$FETCH_HTTP" in 404|410) fail "$("$5")
$(t unchanged)" ;; esac
            fi
            fail "$(t dl_fail)
$(t unchanged)"
        fi
    fi
}

# Why the pinned Doorstop was not installed: <key> is what happened (ds_bad or
# ds_gone). ds_ci, which explains the ci pre-release's churn and names the
# release that is expected next, is added only when DOORSTOP_URL is on the ci
# pre-release itself (.../releases/download/ci/...). The pin is not: it is an
# unchanged copy under a tag of its own, and later a release, and for those a
# 404 or other bytes mean that the file was removed or replaced; the message
# then says only that, and ds_blocked.
ds_blocked_text() {  # ds_blocked_text <key>
    t "$1"
    case "$DOORSTOP_URL" in */releases/download/ci/*) t ds_ci ;; esac
    t ds_blocked
}
ds_gone_text() { ds_blocked_text ds_gone; }

# ------------------------------------------------------ launch options ----
# localconfig.vdf: UserLocalConfigStore > Software > Valve > Steam > apps > "<id>".
# Steam rewrites this file when it exits, so it must not be running. Edited
# with JavaScript for Automation, which every Mac has (python3 may only be a
# stub that offers to install the developer tools).
VDF_JS="$WORK/vdf.js"
cat > "$VDF_JS" <<'JS'
// ---- core (pure; tested with node) ----
function findBlock(text, key, start, end) {
    const re = new RegExp('"' + key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '"\\s*\\{', 'g');
    const part = text.slice(start, end);
    let m;
    while ((m = re.exec(part)) !== null) {
        const open = start + m.index + m[0].length - 1;
        let depth = 0;
        for (let i = open; i < end; i++) {
            const c = text[i];
            if (c === '"') {
                i++;
                while (i < end && text[i] !== '"') i += text[i] === '\\' ? 2 : 1;
            } else if (c === '{') {
                depth++;
            } else if (c === '}') {
                depth--;
                if (depth === 0) return [open, i];
            }
        }
    }
    return null;
}
function unescapeVdf(s) { return s.replace(/\\(.)/g, '$1'); }
function escapeVdf(s) { return s.replace(/\\/g, '\\\\').replace(/"/g, '\\"'); }
function indentAt(text, i) {
    const lineStart = text.lastIndexOf('\n', i - 1) + 1;
    return /^[\t ]*/.exec(text.slice(lineStart))[0];
}
// action: check -> yes | other | no | noapp ; set -> ok | same | other ; remove -> ok | none
// Any structural problem -> error. Returns {status, text?}; text is set when the file changes.
function editVdf(text, app, option, action) {
    let blk = findBlock(text, 'Software', 0, text.length);
    for (const key of ['Valve', 'Steam', 'apps']) blk = blk && findBlock(text, key, blk[0], blk[1]);
    if (!blk) return { status: 'error' };
    const appBlk = findBlock(text, app, blk[0], blk[1]);
    const wrapper = option.replace(/ %command%$/, '');
    const anyWrapper = /("[^"]*run_bepinex\.sh"|\S*run_bepinex\.sh)\s*/;
    const optRe = /"LaunchOptions"\s*"((?:[^"\\]|\\.)*)"/;

    if (appBlk === null) {
        if (action === 'check') return { status: 'noapp' };
        if (action === 'remove') return { status: 'none' };
        const ind = indentAt(text, blk[1]) + '\t';
        const insert = `${ind}"${app}"\n${ind}{\n${ind}\t"LaunchOptions"\t\t"${escapeVdf(option)}"\n${ind}}\n`;
        const lineStart = text.lastIndexOf('\n', blk[1] - 1) + 1;
        return { status: 'ok', text: text.slice(0, lineStart) + insert + text.slice(lineStart) };
    }
    const body = text.slice(appBlk[0], appBlk[1]);
    const m = optRe.exec(body);
    const current = m ? unescapeVdf(m[1]) : '';
    let value;
    if (action === 'check') {
        if (current.includes(wrapper)) return { status: 'yes' };
        return { status: anyWrapper.test(current) ? 'other' : 'no' };
    } else if (action === 'set') {
        if (current.includes(wrapper)) return { status: 'same' };
        if (anyWrapper.test(current)) return { status: 'other' };
        if (current.includes('%command%')) {
            // String.replace scans the replacement for $-patterns ($&, $`,
            // $', $$), so a game path containing one would be rewritten
            // instead of inserted. Splice it in by index instead.
            const at = current.indexOf('%command%');
            value = current.slice(0, at) + option + current.slice(at + '%command%'.length);
        } else if (current.trim()) {
            value = option + ' ' + current.trim();
        } else {
            value = option;
        }
    } else if (action === 'remove') {
        if (!anyWrapper.test(current)) return { status: 'none' };
        value = current.replace(anyWrapper, '').trim();
        if (value === '%command%') value = '';
    } else {
        return { status: 'error' };
    }
    if (m) {
        const s = appBlk[0] + m.index + m[0].indexOf('"', '"LaunchOptions"'.length) + 1;
        const e = appBlk[0] + m.index + m[0].length - 1;
        return { status: 'ok', text: text.slice(0, s) + escapeVdf(value) + text.slice(e) };
    }
    const ind = indentAt(text, appBlk[1]) + '\t';
    const lineStart = text.lastIndexOf('\n', appBlk[1] - 1) + 1;
    return { status: 'ok', text: text.slice(0, lineStart) + `${ind}"LaunchOptions"\t\t"${escapeVdf(value)}"\n` + text.slice(lineStart) };
}
// ---- end core ----
function run(argv) {
    ObjC.import('Foundation');
    const [path, app, option, action] = argv;
    const str = $.NSString.stringWithContentsOfFileEncodingError(path, $.NSUTF8StringEncoding, null);
    if (str.isNil()) return 'error';
    const r = editVdf(str.js, app, option, action);
    if (r.text !== undefined) {
        const fm = $.NSFileManager.defaultManager;
        const backup = path + '.dragnwash-backup';
        fm.removeItemAtPathError(backup, null);
        if (!fm.copyItemAtPathToPathError(path, backup, null)) return 'error';
        if (!$(r.text).writeToFileAtomicallyEncodingError(path, true, $.NSUTF8StringEncoding, null)) return 'error';
    }
    return r.status;
}
JS

LAUNCH_OPTION=""   # set once the game folder is known

vdf_tool() {  # vdf_tool <file> set|remove|check ; prints a status word
    osascript -l JavaScript "$VDF_JS" "$1" "$APP_ID" "$LAUNCH_OPTION" "$2" 2>/dev/null || echo error
}

profiles() {
    local vdf
    for vdf in "$STEAM_ROOT/userdata"/*/config/localconfig.vdf; do
        [ -f "$vdf" ] && echo "$vdf"
    done
    return 0
}

launch_option_state() {  # yes | other | no  (yes: every profile that knows the game has our wrapper)
    local vdf st known=0 missing=0 other=0
    while IFS= read -r vdf; do
        st="$(vdf_tool "$vdf" check)"
        case "$st" in
            yes) known=1 ;;
            other) known=1; other=1 ;;
            no) known=1; missing=1 ;;
        esac
    done < <(profiles)
    if [ "$other" -eq 1 ]; then echo other
    elif [ "$known" -eq 1 ] && [ "$missing" -eq 0 ]; then echo yes
    else echo no
    fi
}

launch_option_any() {  # yes when at least one profile has any run_bepinex.sh wrapper
    local vdf st
    while IFS= read -r vdf; do
        st="$(vdf_tool "$vdf" check)"
        case "$st" in yes|other) echo yes; return ;; esac
    done < <(profiles)
    echo no
}

edit_launch_options() {  # edit_launch_options set|remove ; 0 changed, 4 other wrapper found, 1 nothing
    local action="$1" changed=0 other=0 vdf st
    while IFS= read -r vdf; do
        st="$(vdf_tool "$vdf" "$action")"
        log "launch option $action in $vdf: $st"
        case "$st" in
            ok) changed=1 ;;
            other) other=1 ;;
            same|none) ;;
            *) say "  (could not update $vdf)" ;;
        esac
    done < <(profiles)
    if [ "$other" -eq 1 ]; then return 4; fi
    [ "$changed" -eq 1 ]
}

with_steam_closed() {  # with_steam_closed set|remove ; 0 applied, 2 declined, 3 Steam did not exit, 4 other wrapper
    local action="$1" was_running=0 i
    if steam_running; then
        log "Steam is running; launch option change needs it closed"
        if [ "$CLOSE_STEAM" -eq 1 ]; then
            log "closing Steam without asking (--close-steam)"
        elif ! ask_yes "$(t lo_ask)" 1; then
            log "user chose not to close Steam"
            return 2
        fi
        was_running=1
        log "asking Steam to exit (steam://exit)"
        open "steam://exit" >/dev/null 2>&1 || true
        say "$(t steam_wait)"
        for i in $(seq 1 90); do
            steam_running || break
            if [ "$i" -eq 30 ]; then
                log "Steam still running after 30 s; asking it to quit through AppleScript"
                osascript -e 'tell application "Steam" to quit' >/dev/null 2>&1 || true
            fi
            sleep 1
        done
        if steam_running; then
            log "Steam still running after 90 s"
            return 3
        fi
        log "Steam exited"
        # Steam writes its config on the way out; give that a moment.
        sleep 3
    fi
    local rc=0
    edit_launch_options "$action" || rc=$?
    log "launch option $action: edit returned $rc"
    if [ "$was_running" -eq 1 ]; then
        log "starting Steam again"
        open -a Steam >/dev/null 2>&1 || true
    fi
    return "$rc"
}

# ---------------------------------------------------------- run_bepinex.sh --
# The lines of a Doorstop run.sh this script looks for, without their indent.
# The DYLD_INSERT_LIBRARIES block of a run.sh from before UnityDoorstop 4.6.0
# (the ci 4.5.0 build):
OV_OLD_IF='if [ -z "$DYLD_INSERT_LIBRARIES" ]; then'
OV_OLD_SET='dyld_insert_libraries="${doorstop_name}:${DYLD_INSERT_LIBRARIES}"'
# and the block that hands on Steam's overlay. UnityDoorstop 4.6.0 and later
# have it (NeighTools/UnityDoorstop#121, merged 2026-09-25); into an older
# run.sh this script's edit writes it (the overlay edit in the workaround of
# TomXV/dragnwash-modframework#85). Only the comment above it differs:
OV_NEW_INHERIT='inherited_libraries="${DYLD_INSERT_LIBRARIES:-${STEAM_DYLD_INSERT_LIBRARIES}}"'
OV_NEW_IF='if [ -z "$inherited_libraries" ]; then'
OV_NEW_SET='dyld_insert_libraries="${doorstop_name}:${inherited_libraries}"'
# The last line of each comment, which tells the log where the block came from.
OV_NOTE_UPSTREAM='# it after Doorstop so the Steam overlay still works.'
OV_NOTE_SCRIPT='# from there and keep the overlay working.'
# UnityDoorstop's run.sh as the ci 4.5.0 and 4.6.0 builds have it passes the
# DYLD variables to the game with arch -e; BepInEx 5.4.23.5's run_bepinex.sh
# does not.
CI_EXEC='-e DYLD_INSERT_LIBRARIES="${dyld_insert_libraries}" \'
# Where the script makes target_assembly absolute. A pattern like
# ^target_assembly=.* would rewrite this line too, and Doorstop would then get
# a relative path and look for it in whatever folder the game started from.
TARGET_ABS='target_assembly="$(abs_path "$target_assembly")"'
TARGET="BepInEx/core/BepInEx.Preloader.dll"

count_line() {  # count_line <file> <text>: lines that are <text> once their indent is off
    WANT="$2" awk '{ s = $0; sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); if (s == ENVIRON["WANT"]) n++ } END { print n + 0 }' "$1"
}
count_re() {  # count_re <file> <extended regex>
    grep -cE -- "$2" "$1" || true
}
count_exact() {  # count_exact <file> <whole line>
    grep -cxF -- "$2" "$1" || true
}

# True when the file is made from UnityDoorstop's run.sh (the ci 4.5.0 or
# 4.6.0, set up or not), not the older one that ships in BepInEx 5.4.23.5.
ci_run_script() {
    [ "$(count_line "$1" "$CI_EXEC")" -eq 1 ] && [ "$(count_re "$1" '^archpreference=')" -eq 1 ]
}

# Sets up a copy of UnityDoorstop's run.sh: writes <file>.new and returns 0, or
# sets BAD_PART to the first thing that is not as expected and returns 1.
#   executable_name  the full path of the .app, so the script also works when
#                    it is not started from the game folder; the bare name
#                    when the path would break the shell assignment
#   archpreference   "x86_64": Rosetta, until BepInEx/BepInEx#1402 is released
#   target_assembly  BepInEx's preloader instead of the Doorstop.dll example
#   overlay          /bin/sh is a platform binary, so a DYLD_INSERT_LIBRARIES
#                    that Steam sets never reaches the script; Steam passes
#                    the same list as STEAM_DYLD_INSERT_LIBRARIES too. Only a
#                    run.sh from before UnityDoorstop 4.6.0, with the old
#                    block, is edited. One that already hands the list on
#                    (4.6.0 and later, this script's earlier edit, or the
#                    same lines put in by hand) is left as it is; one with
#                    neither block is not used at all.
# Only the lines that hold the defaults (or values this function wrote before)
# are changed, each has to be there exactly once, and the result is checked
# again line by line, so running it twice changes nothing the second time.
#
# The earlier version of this script also patched the arch line of BepInEx's
# own run_bepinex.sh (NeighTools/UnityDoorstop#107: arch is arm64e,
# libdoorstop.dylib was not, and newer macOS killed arch for an exported
# DYLD_INSERT_LIBRARIES). UnityDoorstop's run.sh never exports the DYLD
# variables and hands them to the game with arch -e, so that patch is gone; a
# run_bepinex.sh that still has it is BepInEx's, and is replaced by the pinned
# Doorstop's run.sh.
configure_run_script() {  # configure_run_script <file> <executable_name>
    local f="$1" out="$1.new" overlay from lines_in lines_out want
    BAD_PART=""
    if [ "$(count_re "$f" '^executable_name="[^"$`\\]*"$')" -ne 1 ]; then BAD_PART='executable_name="..."'
    elif [ "$(count_re "$f" '^archpreference="[A-Za-z0-9_,]*"$')" -ne 1 ]; then BAD_PART='archpreference="..."'
    elif [ "$(count_re "$f" '^target_assembly="[^"$`\\]*"$')" -ne 1 ]; then BAD_PART='target_assembly="..."'
    elif [ "$(count_exact "$f" "$TARGET_ABS")" -ne 1 ]; then BAD_PART="$TARGET_ABS"
    elif [ "$(count_line "$f" "$CI_EXEC")" -ne 1 ]; then BAD_PART="$CI_EXEC"
    fi
    # The overlay block: already handing the list on (each new line once, no
    # old line left), or the old block only, to be edited. Anything else, a
    # mix of both say, is not guessed at.
    if [ "$(count_line "$f" "$OV_NEW_INHERIT")" -eq 1 ] && [ "$(count_line "$f" "$OV_NEW_IF")" -eq 1 ] &&
        [ "$(count_line "$f" "$OV_NEW_SET")" -eq 1 ] && [ "$(count_line "$f" "$OV_OLD_IF")" -eq 0 ] &&
        [ "$(count_line "$f" "$OV_OLD_SET")" -eq 0 ]; then
        overlay=present
        if [ "$(count_line "$f" "$OV_NOTE_UPSTREAM")" -ge 1 ]; then
            from="as UnityDoorstop 4.6.0 and later have it (NeighTools/UnityDoorstop#121)"
        elif [ "$(count_line "$f" "$OV_NOTE_SCRIPT")" -ge 1 ]; then
            from="as this script's earlier edit wrote it"
        else
            from="without either known comment (edited by hand?)"
        fi
    elif [ "$(count_line "$f" "$OV_OLD_IF")" -eq 1 ] && [ "$(count_line "$f" "$OV_OLD_SET")" -eq 1 ] &&
        [ "$(count_line "$f" "$OV_NEW_INHERIT")" -eq 0 ] && [ "$(count_line "$f" "$OV_NEW_IF")" -eq 0 ] &&
        [ "$(count_line "$f" "$OV_NEW_SET")" -eq 0 ]; then
        overlay=add
    else
        overlay=""
        [ -n "$BAD_PART" ] || BAD_PART="the DYLD_INSERT_LIBRARIES block"
    fi
    if [ -n "$BAD_PART" ]; then
        log "run_bepinex.sh: not changed, $BAD_PART is not there exactly once"
        return 1
    fi
    if [ "$overlay" = present ]; then
        log "run_bepinex.sh: the Steam overlay hand-over (STEAM_DYLD_INSERT_LIBRARIES) is already there, $from; left as it is"
    else
        log "run_bepinex.sh: a run.sh from before UnityDoorstop 4.6.0; adding the Steam overlay hand-over"
    fi

    NEW_EXE="$2" TARGET="$TARGET" OVERLAY="$overlay" \
    OV_OLD_IF="$OV_OLD_IF" OV_OLD_SET="$OV_OLD_SET" OV_NEW_INHERIT="$OV_NEW_INHERIT" \
    OV_NEW_IF="$OV_NEW_IF" OV_NEW_SET="$OV_NEW_SET" OV_NOTE_SCRIPT="$OV_NOTE_SCRIPT" \
    awk '
        function body(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }
        function indent(s) { sub(/[^ \t].*$/, "", s); return s }
        /^executable_name="[^"$`\\]*"$/ { print "executable_name=\"" ENVIRON["NEW_EXE"] "\""; next }
        /^archpreference="[A-Za-z0-9_,]*"$/ { print "archpreference=\"x86_64\""; next }
        /^target_assembly="[^"$`\\]*"$/ { print "target_assembly=\"" ENVIRON["TARGET"] "\""; next }
        ENVIRON["OVERLAY"] == "add" && body($0) == ENVIRON["OV_OLD_IF"] {
            ind = indent($0)
            print ind "# /bin/sh is a platform binary too, so a DYLD_INSERT_LIBRARIES that"
            print ind "# Steam sets never reaches this script. Steam passes the same list"
            print ind "# (its loader and overlay) as STEAM_DYLD_INSERT_LIBRARIES, so take it"
            print ind ENVIRON["OV_NOTE_SCRIPT"]
            print ind ENVIRON["OV_NEW_INHERIT"]
            print ind ENVIRON["OV_NEW_IF"]
            next
        }
        ENVIRON["OVERLAY"] == "add" && body($0) == ENVIRON["OV_OLD_SET"] { print indent($0) ENVIRON["OV_NEW_SET"]; next }
        { print }
    ' "$f" > "$out"

    # Check the result, not only the input: every line that was meant to
    # change changed, exactly once, and nothing else did.
    lines_in="$(wc -l < "$f" | tr -d ' ')"
    lines_out="$(wc -l < "$out" | tr -d ' ')"
    want="$lines_in"
    if [ "$overlay" = add ]; then want=$((lines_in + 5)); fi
    if [ "$(count_exact "$out" "executable_name=\"$2\"")" -ne 1 ]; then BAD_PART="executable_name=\"$2\""
    elif [ "$(count_exact "$out" 'archpreference="x86_64"')" -ne 1 ]; then BAD_PART='archpreference="x86_64"'
    elif [ "$(count_exact "$out" "target_assembly=\"$TARGET\"")" -ne 1 ]; then BAD_PART="target_assembly=\"$TARGET\""
    elif [ "$(count_exact "$out" "$TARGET_ABS")" -ne 1 ]; then BAD_PART="$TARGET_ABS"
    elif [ "$(count_line "$out" "$OV_NEW_INHERIT")" -ne 1 ]; then BAD_PART="$OV_NEW_INHERIT"
    elif [ "$(count_line "$out" "$OV_NEW_IF")" -ne 1 ]; then BAD_PART="$OV_NEW_IF"
    elif [ "$(count_line "$out" "$OV_NEW_SET")" -ne 1 ]; then BAD_PART="$OV_NEW_SET"
    elif [ "$(count_line "$out" "$OV_OLD_IF")" -ne 0 ] || [ "$(count_line "$out" "$OV_OLD_SET")" -ne 0 ]; then BAD_PART="$OV_OLD_IF"
    elif [ "$lines_out" -ne "$want" ]; then BAD_PART="$lines_out lines instead of $want"
    elif ! /bin/sh -n "$out" 2>/dev/null; then BAD_PART="sh -n"
    fi
    if [ -n "$BAD_PART" ]; then
        log "run_bepinex.sh: the result failed its check at $BAD_PART; not used"
        rm -f "$out"
        return 1
    fi
    log "run_bepinex.sh: executable_name=\"$2\", archpreference=\"x86_64\", target_assembly=\"$TARGET\", overlay $overlay"
}

# Keeps the game folder's own copy of a file this run is about to replace, once:
# a later run finds the backup there already and leaves it, so it stays the
# file from before the first install.
backup_once() {  # backup_once <name in the game folder>
    local f="$GAME_DIR/$1"
    [ -f "$f" ] || return 0
    [ ! -e "$f$BACKUP_SUFFIX" ] || return 0
    cp -p "$f" "$f$BACKUP_SUFFIX"
    say "$1: $(t backup_kept) $1$BACKUP_SUFFIX"
}

# -------------------------------------------------------------- framework --
# The minimum mod-install.json gives for a ModFramework folder, or nothing.
need_min() {
    local name min
    while IFS="$TAB" read -r name min; do
        if [ "$name" = "$1" ]; then echo "$min"; return; fi
    done <<< "$FW_NEEDS"
}

# A part the player switched off in the game's Mods screen: the ModFramework
# Preloader renamed its DLL to <name>.dll.disabled, and renames it back when
# the part is switched on again, but only while no <name>.dll is in the way.
# So such a part counts as there, and nothing is copied next to it. The Mods
# screen can switch off the libraries only, not the core or the Preloader; a
# .dll.disabled of those was renamed by hand, and is left alone just the same.
switched_off() {  # switched_off <DLL path>
    [ ! -f "$1" ] && [ -f "$1.disabled" ]
}
# Notes a switched-off part for the message at the end of the install: a
# library in FW_OFF (the Mods screen switches it back on), the core or the
# Preloader in FW_OFF_HAND (renamed by hand, and only renaming it back helps).
fw_off_add() {  # fw_off_add <name> <DLL path> <minimum or nothing>
    local have
    have="$(dll_version "$2.disabled")"
    log "$1: switched off (${2##*/}.disabled${have:+, $have}); left as it is"
    case "$1" in
        "$FRAMEWORK_PREFIX" | "${FRAMEWORK_PATCHER%.dll}")
            FW_OFF_HAND="$FW_OFF_HAND
  $1${have:+ $have}"
            if [ "$1" != "$FRAMEWORK_PREFIX" ]; then FW_OFF_PRE=1; fi
            ;;
        *)
            FW_OFF="$FW_OFF
  $1${have:+ $have}"
            ;;
    esac
    if [ -n "$3" ] && [ -n "$have" ] && [ "$(ver_cmp "$have" "$3")" = -1 ]; then FW_OFF_OLD=1; fi
}

# ------------------------------------------------ the Preloader's lists ----
# The entries of FW_LISTS name a DLL under BepInEx/plugins (<folder>/<file>.dll)
# or, in uninstall.txt, a folder there, in the first tab-separated field. This
# drops the entries of one folder, compared the way the Preloader compares
# them: trimmed, \ read as /, in any case. Every other line, comments and the
# end of the file included, is printed byte for byte. With ASK=1 it prints
# nothing and only says, by its exit status, whether there is an entry to
# drop (0) or not (1).
FORGET_AWK='
function unit(s,   i) {
    sub(/^[ \t\r]+/, "", s)
    i = index(s, "\t")
    if (i) s = substr(s, 1, i - 1)
    sub(/[ \t\r]+$/, "", s)
    gsub(/\\/, "/", s)
    return tolower(s)
}
function gone(s) { s = unit(s); return s == d || index(s, d "/") == 1 }
BEGIN { d = tolower(ENVIRON["FOLDER"]); ask = (ENVIRON["ASK"] == "1") }
ask { if (gone($0)) { found = 1; exit } next }
NR > 1 && !drop { printf "%s\n", last }
{ last = $0; drop = gone($0) }
END {
    if (ask) exit !found
    if (NR > 0 && !drop) printf "%s%s", last, (ENVIRON["NL"] == "1" ? "\n" : "")
}
'

# Forgets what the Preloader's lists say about one folder under
# BepInEx/plugins: switched off in the Mods screen, renamed to .dll.disabled
# by the Preloader, or waiting to be uninstalled (install-steamdeck.sh's
# forget_folder). A list with nothing to drop is not touched. The new list is
# written next to the old one, with its permissions, and moved over it, so the
# game never reads half a list. FORGOT is set to 1 when a line went.
FORGOT=0
forget_folder() {  # forget_folder <folder under BepInEx/plugins>
    local list f tmp nl rc
    for list in $FW_LISTS; do
        f="$GAME_DIR/BepInEx/config/$list"
        [ -f "$f" ] || continue
        rc=0; FOLDER="$1" ASK=1 LC_ALL=C awk "$FORGET_AWK" "$f" 2>/dev/null || rc=$?
        if [ "$rc" -eq 1 ]; then continue; fi
        # Whether the last line ends in a newline: $(...) drops a trailing
        # newline, so an empty result means it does (or the list is empty).
        nl=1; [ -z "$(tail -c 1 "$f" 2>/dev/null)" ] || nl=0
        tmp=""
        if [ "$rc" -eq 0 ] && tmp="$(mktemp "$f.XXXXXX" 2>/dev/null)" && cp -p "$f" "$tmp" &&
            FOLDER="$1" NL="$nl" LC_ALL=C awk "$FORGET_AWK" "$f" > "$tmp"; then
            if cmp -s "$f" "$tmp"; then rm -f "$tmp"; continue; fi
            if mv -f "$tmp" "$f"; then
                log "$list: forgot $1"
                FORGOT=1
                continue
            fi
        fi
        [ -z "$tmp" ] || rm -f "$tmp"
        BAD_PART="$list" BAD_WHAT="$1"
        warn "$(t list_failed)"
    done
}

# 0 when the list still has an entry: a line that is neither blank nor a
# comment.
list_has_entries() {  # list_has_entries <file>
    LC_ALL=C awk '{ s = $0; sub(/^[ \t\r]+/, "", s); if (s != "" && substr(s, 1, 1) != "#") { n = 1; exit } } END { exit !n }' "$1"
}

# Installing means wanting the mod on. The Mods screen may have switched it
# off (the Preloader renamed its DLL to .dll.disabled) or set it to be
# uninstalled at the next start, and either would undo this install when the
# game starts. So, as install-steamdeck.sh's enable_folder does, a
# .dll.disabled next to a DLL this install has just copied goes, and the
# Preloader's lists forget the mod. MOD_ON is 1 when any of that was needed.
enable_plugin() {
    local dst="$GAME_DIR/BepInEx/plugins/$PLUGIN" f
    FORGOT=0
    while IFS= read -r f; do
        f="${f#./}"
        if [ -f "$dst/$f" ] && [ -f "$dst/$f.disabled" ]; then
            rm -f "$dst/$f.disabled"
            log "$PLUGIN/$f.disabled: removed, the fresh $f takes its place"
            MOD_ON=1
        fi
    done < <(cd "$PAYLOAD" && find . -type f -name '*.dll' ! -path './Translations/_*' ! -path './SaveHistory/*')
    forget_folder "$PLUGIN"
    if [ "$FORGOT" -eq 1 ]; then MOD_ON=1; fi
}

# What the mod needs of ModFramework, against what the game folder has: the
# core, the Preloader and the libraries named in "needs", nothing else (the
# same rules as install-steamdeck.sh). 0 when the release has to be fetched.
framework_needed() {
    local name have min dll fetch=1
    FW_HAVE="$(dll_version "$GAME_DIR/BepInEx/plugins/$FRAMEWORK_PREFIX/$FRAMEWORK_PREFIX.dll")"
    FW_PARTS="$FRAMEWORK_PREFIX"
    FW_OFF="" FW_OFF_HAND="" FW_OFF_PRE=0 FW_OFF_OLD=0
    while IFS="$TAB" read -r name min; do
        if [ -n "$name" ] && [ "$name" != "$FRAMEWORK_PREFIX" ]; then FW_PARTS="$FW_PARTS $name"; fi
    done <<< "$FW_NEEDS"
    for name in $FW_PARTS; do
        dll="$GAME_DIR/BepInEx/plugins/$name/$name.dll"
        have="$(dll_version "$dll")"
        min="$(need_min "$name")"
        if switched_off "$dll"; then
            fw_off_add "$name" "$dll" "$min"
        elif [ -z "$have" ]; then
            log "$name: missing"; fetch=0
        elif [ -n "$min" ] && [ "$(ver_cmp "$have" "$min")" = -1 ]; then
            log "$name: $have, below the minimum $min"; fetch=0
        elif [ "$name" = "$FRAMEWORK_PREFIX" ] && [ "$(ver_cmp "$have" "$FW_VERSION")" = -1 ]; then
            log "$name: $have, older than $FW_VERSION"; fetch=0
        else
            log "$name: $have, kept"
        fi
    done
    dll="$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER"
    if switched_off "$dll"; then
        fw_off_add "${FRAMEWORK_PATCHER%.dll}" "$dll" ""
    elif [ ! -f "$dll" ]; then
        log "Preloader: missing"; fetch=0
    fi
    return "$fetch"
}

# Notes a ModFramework part this run added in FW_MARKER, once. Only parts that
# were not there before count: a part that was there and only gets updated
# still belongs to whoever put it there, and uninstall leaves it. For a plugin
# that means its folder, not just its DLL: a folder with the DLL missing (or
# switched off) was still put there by someone else.
fw_marker_add() {  # fw_marker_add plugins/<folder> | patchers/<file>
    local m="$GAME_DIR/BepInEx/$FW_MARKER"
    if [ ! -f "$m" ]; then
        echo "# ModFramework parts that install-macos.sh (Drag'n Wash Localization) added. Its uninstall removes these, and nothing else of ModFramework." > "$m"
    fi
    grep -qxF -- "$1" "$m" || echo "$1" >> "$m"
    log "framework marker: $1"
}

# One ModFramework folder from the checked release, unless the game folder's
# copy is the same version or newer (a newer or patched copy is left alone).
# Only the files the release has are written; others in the folder stay.
install_part() {  # install_part <folder>
    local src="$WORK/framework/BepInEx/plugins/$1" dst="$GAME_DIR/BepInEx/plugins/$1" have offered
    if switched_off "$dst/$1.dll"; then
        say "$1: switched off, left as it is"
        return 0
    fi
    have="$(dll_version "$dst/$1.dll")"
    offered="$(dll_version "$src/$1.dll")"
    if [ -n "$have" ] && [ -n "$offered" ] && [ "$(ver_cmp "$have" "$offered")" != -1 ]; then
        say "$1: kept $have"
        return 0
    fi
    if [ ! -d "$dst" ]; then fw_marker_add "plugins/$1"; fi
    mkdir -p "$dst"
    cp -R "$src/." "$dst/"
    say "$1: ${offered:-ok}"
}

install_preloader() {
    local rel="BepInEx/patchers/$FRAMEWORK_PATCHER" have offered
    if switched_off "$GAME_DIR/$rel"; then
        say "Preloader: switched off, left as it is"
        return 0
    fi
    have="$(dll_version "$GAME_DIR/$rel")"
    offered="$(dll_version "$WORK/framework/$rel")"
    if [ -n "$have" ] && [ -n "$offered" ] && [ "$(ver_cmp "$have" "$offered")" != -1 ]; then
        log "Preloader: kept $have"
        return 0
    fi
    if [ ! -f "$GAME_DIR/$rel" ]; then fw_marker_add "patchers/$FRAMEWORK_PATCHER"; fi
    mkdir -p "$GAME_DIR/BepInEx/patchers"
    cp -f "$WORK/framework/$rel" "$GAME_DIR/$rel"
    log "Preloader: ${offered:-ok}"
}

# The settings and state files that belong to one ModFramework plugin: each
# keeps <its GUID>.cfg (com.tomxv.dragnwash.modframework[.<part>]), the core
# also its update cache. The Preloader's lists (FW_LISTS) also hold entries of
# parts and mods that stay, so remove_marked_framework sees to them itself.
fw_config_files() {  # fw_config_files <folder>: names in BepInEx/config
    local base="com.tomxv.dragnwash.modframework" suffix
    case "$1" in
        "$FRAMEWORK_PREFIX") echo "$base.cfg $base.updates.txt" ;;
        "$FRAMEWORK_PREFIX".*)
            suffix="$(printf '%s' "${1#"$FRAMEWORK_PREFIX".}" | tr '[:upper:]' '[:lower:]')"
            echo "$base.$suffix.cfg" ;;
    esac
}

# Uninstall, with no other mod left: removes the ModFramework parts FW_MARKER
# lists, with their settings. An entry has to name a ModFramework folder or the
# Preloader, so an edited marker cannot point anywhere else. Prints nothing;
# what is left of ModFramework afterwards is for the caller to find.
#
# The Preloader's lists forget each plugin folder that goes, so a later
# install of it is not switched off again. When the Preloader itself goes, a
# list that still has entries stays as long as a ModFramework part stays: the
# entries of the parts that stay (a library switched off in the Mods screen,
# say) are what a Preloader installed again needs to switch them back on.
# Other lists go. (Other mods are not left at this point: the caller keeps
# ModFramework whole while there are any.)
remove_marked_framework() {
    local m="$GAME_DIR/BepInEx/$FW_MARKER" entry name c list f preloader=0 parts_left=0
    while IFS= read -r entry; do
        case "$entry" in
            "plugins/$FRAMEWORK_PREFIX" | "plugins/$FRAMEWORK_PREFIX".*)
                name="${entry#plugins/}"
                case "$name" in *[!A-Za-z0-9.]* | *..*) log "framework marker: skipped $entry"; continue ;; esac
                rm -rf "${GAME_DIR:?}/BepInEx/plugins/$name"
                for c in $(fw_config_files "$name"); do rm -f "$GAME_DIR/BepInEx/config/$c"; done
                forget_folder "$name"
                ;;
            "patchers/$FRAMEWORK_PATCHER")
                rm -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER" "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER.disabled"
                preloader=1
                ;;
            *) continue ;;
        esac
        log "framework: removed $entry"
    done < "$m"
    rm -f "$m"
    if [ "$preloader" -eq 1 ]; then
        if [ -n "$(framework_left)" ]; then parts_left=1; fi
        for list in $FW_LISTS; do
            f="$GAME_DIR/BepInEx/config/$list"
            [ -f "$f" ] || continue
            if [ "$parts_left" -eq 1 ] && list_has_entries "$f"; then
                log "framework: kept $list, for the ModFramework parts that stay"
            else
                rm -f "$f"
            fi
        done
    fi
}

# framework_left's lines for a message, two spaces in, with a plugin folder
# whose DLL is switched off marked as such.
describe_parts() {  # describe_parts <framework_left output>
    local p
    while IFS= read -r p; do
        [ -n "$p" ] || continue
        if switched_off "$GAME_DIR/BepInEx/plugins/$p/$p.dll"; then
            printf '  %s%s\n' "$p" "$(t off_tag)"
        else
            printf '  %s\n' "$p"
        fi
    done <<< "$1"
}
# 0 when a ModFramework plugin folder that is left has its DLL switched off.
parts_switched_off() {  # parts_switched_off <framework_left output>
    local p
    while IFS= read -r p; do
        [ -n "$p" ] || continue
        if switched_off "$GAME_DIR/BepInEx/plugins/$p/$p.dll"; then return 0; fi
    done <<< "$1"
    return 1
}

# What of ModFramework is in the game folder: its plugin folders and the
# Preloader (switched off or not), one per line.
framework_left() {
    if [ -d "$GAME_DIR/BepInEx/plugins" ]; then
        find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 -name "$FRAMEWORK_PREFIX*" 2>/dev/null | sed 's|.*/||' | sort || true
    fi
    if [ -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER" ]; then echo "$FRAMEWORK_PATCHER"; fi
    if [ -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER.disabled" ]; then echo "$FRAMEWORK_PATCHER.disabled"; fi
    return 0
}

# ------------------------------------------------------------------ main ----
log "---- start: $0 $ARGV (mode=${MODE:-ask}, ui=$UI, gui=$GUI, payload=$PAYLOAD, test_steam=$TEST_STEAM, steam_root=$STEAM_ROOT, account_home=$ACCOUNT_HOME)"
say "== $(t title)"
if [ "$TEST_STEAM" -eq 1 ]; then
    say "Test run: the Steam folder is $STEAM_ROOT, not this account's, so Steam is not closed or started."
fi

if [ -z "$GAME_DIR" ]; then GAME_DIR="$(find_game || true)"; fi
[ -n "$GAME_DIR" ] && [ -d "$GAME_DIR/$APP_BUNDLE" ] || fail "$(t nogame)"
GAME_DIR="$(cd "$GAME_DIR" && pwd)"
# A test run stays inside its own Steam folder: a --game-dir, or a
# libraryfolders.vdf that lists a real library, would otherwise let it change
# a real game folder. A test Steam folder that holds this account's own one
# (DRAGNWASH_TEST_STEAM_ROOT set to the home folder, say) does not open up the
# real one either.
if [ "$TEST_STEAM" -eq 1 ]; then
    if ! inside_dir "$GAME_DIR" "$(dir_id "$STEAM_ROOT")" || inside_dir "$GAME_DIR" "$REAL_STEAM_ID"; then
        fail "Test run: the game folder is not inside the test Steam folder, or is inside this account's own, so it is not used.
  game folder: $GAME_DIR
  test Steam folder: $STEAM_ROOT
$(t unchanged)"
    fi
fi
LAUNCH_OPTION="\"$GAME_DIR/run_bepinex.sh\" %command%"
say "Game: $GAME_DIR"
if [ "$MODE" != check ] && game_running; then fail "$(t running)"; fi

if [ -z "$MODE" ]; then
    have_bep="$(t st_no)"; [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ] && have_bep="$(t st_yes)"
    have_mod="$(t st_no)"; [ -f "$GAME_DIR/BepInEx/plugins/$PLUGIN/$PLUGIN.dll" ] && have_mod="$(t st_yes)"
    status="BepInEx: $have_bep    Mod: $have_mod"
    if [ "$ASSUME_YES" -eq 1 ]; then
        MODE=install
    elif [ "$GUI" -eq 1 ]; then
        picked="$(dialog_choose "$GAME_DIR
$status

$(t action)" "$(t act_install)" "$(t act_install)" "$(t act_uninstall)" "$(t act_check)")"
        case "$picked" in
            "$(t act_install)") MODE=install ;;
            "$(t act_uninstall)") MODE=uninstall ;;
            "$(t act_check)") MODE=check ;;
            *) exit 1 ;;
        esac
    else
        say "$status"
        say "$(t action)"
        say "  1) $(t act_install)"
        say "  2) $(t act_uninstall)"
        say "  3) $(t act_check)"
        read -r -p "> " pick || pick=""
        case "$pick" in
            1|"") MODE=install ;;
            2) MODE=uninstall ;;
            3) MODE=check ;;
            *) exit 1 ;;
        esac
    fi
fi

if [ "$MODE" = install ]; then
    [ -f "$PAYLOAD/$PLUGIN.dll" ] || fail "$(t nopayload)"
    rosetta_ok || fail "$(t norosetta)"
    if [ -f "$MANIFEST" ]; then
        fw_info="$(osascript -l JavaScript "$HELPER_JS" framework "$MANIFEST" 2>&1)" || fw_info="error: $fw_info"
        case "$fw_info" in error:*) fail "$(t badmanifest) ${fw_info#error: }" ;; esac
        FW_VERSION="$(printf '%s\n' "$fw_info" | sed -n 1p)"
        FW_SHA256="$(printf '%s\n' "$fw_info" | sed -n 2p)"
        FW_SIZE="$(printf '%s\n' "$fw_info" | sed -n 3p)"
        FW_NEEDS="$(printf '%s\n' "$fw_info" | sed -n '4,$p')"
        log "mod-install.json: framework ${FW_VERSION:-none}${FW_VERSION:+, sha256 $FW_SHA256, $FW_SIZE bytes}"
    else
        log "no mod-install.json at $MANIFEST"
    fi

    # Language
    if [ -z "$LANG_CHOICE" ]; then
        default="$DEFAULT_LOCALE"
        if [ "$default" != en ] && [ ! -d "$PAYLOAD/Translations/$default" ]; then default=en; fi
        if [ "$ASSUME_YES" -eq 1 ]; then
            LANG_CHOICE="$default"
        elif [ "$GUI" -eq 1 ]; then
            labels=""
            default_label=""
            while IFS="$TAB" read -r code name; do
                labels="${labels}${name} (${code})
"
                [ "$code" = "$default" ] && default_label="${name} (${code})"
            done < <(available_locales)
            # Split the labels into arguments without arrays (bash 3.2 + set -u).
            set -f
            OLDIFS="$IFS"; IFS='
'
            # shellcheck disable=SC2086
            set -- $labels
            IFS="$OLDIFS"
            set +f
            picked="$(dialog_choose "$(t pick)" "$default_label" "$@")"
            [ -n "$picked" ] || exit 1
            LANG_CHOICE="$(printf '%s' "$picked" | sed -n 's/.*(\([^()]*\))$/\1/p')"
        else
            say "$(t pick):"
            i=0
            codes=" "
            while IFS="$TAB" read -r code name; do
                i=$((i + 1))
                mark=" "; [ "$code" = "$default" ] && mark="*"
                printf ' %s %2d) %s (%s)\n' "$mark" "$i" "$name" "$code"
                codes="$codes$i=$code "
            done < <(available_locales)
            read -r -p "> " pick || pick=""
            if [ -z "$pick" ]; then
                LANG_CHOICE="$default"
            elif printf '%s' "$pick" | grep -Eq '^[0-9]+$'; then
                LANG_CHOICE="$(printf '%s' "$codes" | tr ' ' '\n' | sed -n "s/^$pick=//p")"
            else
                LANG_CHOICE="$pick"
            fi
        fi
    fi
    # Every way of choosing a language lands here: --lang, the GUI picker and
    # the code typed at the prompt above. That is why the check sits after the
    # picker and not next to the option parser.
    #
    # Shape first, because a directory test on its own accepts "ja/" and the
    # code is spliced into a sed s/// replacement further down, where / and &
    # change what the expression means. A leading _ is out as well: the picker
    # hides those directories (the mod keeps its working files in
    # Translations/_discovered/), so they are not languages to offer here
    # either. A locale directory name never needs anything outside these
    # characters.
    case "$LANG_CHOICE" in
        "" | _* | *[!A-Za-z0-9_-]*) fail "Unknown language: ${LANG_CHOICE:-?}" ;;
    esac
    if [ "$LANG_CHOICE" != en ] && [ ! -d "$PAYLOAD/Translations/$LANG_CHOICE" ]; then
        fail "Unknown language: $LANG_CHOICE"
    fi

    # What is missing. A game folder that already has BepInEx next to the
    # .app (installed by hand, or by an earlier run) keeps it; the Doorstop is
    # only fetched when libdoorstop.dylib is not the pinned build's or
    # run_bepinex.sh is not made from a Doorstop run.sh (ci_run_script).
    NEED_BEP=0
    if [ ! -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ] || [ ! -f "$GAME_DIR/BepInEx/core/BepInEx.Preloader.dll" ]; then
        NEED_BEP=1
    fi
    NEED_DS=1
    if [ -f "$GAME_DIR/libdoorstop.dylib" ] && [ -f "$GAME_DIR/run_bepinex.sh" ] &&
        [ "$(sha256_of "$GAME_DIR/libdoorstop.dylib")" = "$DOORSTOP_DYLIB_SHA256" ] &&
        ci_run_script "$GAME_DIR/run_bepinex.sh"; then
        NEED_DS=0
    fi
    NEED_FW=0
    if [ -n "$FW_VERSION" ]; then
        if framework_needed; then NEED_FW=1; fi
    fi
    if [ -n "$FRAMEWORK_ZIP" ] && [ "$NEED_FW" -eq 0 ]; then
        log "--framework-zip not used: nothing of ModFramework has to be installed"
    fi
    log "plan: bepinex=$NEED_BEP doorstop=$NEED_DS framework=$NEED_FW"

    downloads=""
    if [ "$NEED_BEP" -eq 1 ] && [ -z "$BEPINEX_ZIP" ]; then
        downloads="$downloads
  ${BEPINEX_URL##*/} (BepInEx/BepInEx)"
    fi
    if [ "$NEED_DS" -eq 1 ] && [ -z "$DOORSTOP_ZIP" ]; then
        downloads="$downloads
  ${DOORSTOP_URL##*/} ($DOORSTOP_REPO, $DOORSTOP_BUILD)"
    fi
    if [ "$NEED_FW" -eq 1 ] && [ -z "$FRAMEWORK_ZIP" ]; then
        downloads="$downloads
  DragNWash.ModFramework-$FW_VERSION.zip ($FRAMEWORK_REPO)"
    fi
    question="$(t confirm_install)
$GAME_DIR"
    if [ -n "$downloads" ]; then question="$question

$(t dl_head)$downloads"; fi
    ask_yes "$question" 1 || exit 1

    # Every zip is fetched and checked in the download folder first, and
    # run_bepinex.sh is set up there too, so a failed check leaves the game
    # folder as it was.
    if [ "$NEED_BEP" -eq 1 ]; then
        get_zip "$BEPINEX_ZIP" "$BEPINEX_URL" "$WORK/bepinex.zip" "$(t bep_get)"
        if ! check_file "$WORK/bepinex.zip" "$BEPINEX_SHA256"; then
            if [ -n "$BEPINEX_ZIP" ]; then fail "$(t bep_zip_bad)
$(t unchanged)"; fi
            fail "$(t bep_bad)
$(t unchanged)"
        fi
    fi
    if [ "$NEED_DS" -eq 1 ]; then
        # Only the pinned build is installed. The pinned file is not meant to
        # change (an unchanged copy now, a release later), so a mismatch or a
        # 404 here means it was replaced or removed, or altered on the way.
        # Either way it is not the one that was tested: the same message for
        # both says installs wait for a new pin, where to follow that, and how
        # to install with a saved copy meanwhile. Any other failed download
        # (no network, say) gets the plain download message.
        get_zip "$DOORSTOP_ZIP" "$DOORSTOP_URL" "$WORK/doorstop.zip" "$(t ds_get)" ds_gone_text
        if ! check_file "$WORK/doorstop.zip" "$DOORSTOP_SHA256"; then
            if [ -n "$DOORSTOP_ZIP" ]; then fail "$(t ds_zip_bad)
$(t unchanged)"; fi
            fail "$(ds_blocked_text ds_bad)
$(t unchanged)"
        fi
        BAD_PART="${DOORSTOP_URL##*/}"
        unzip -q "$WORK/doorstop.zip" 'universal/*' -d "$WORK/doorstop" || fail "$(t unpack_failed)
$(t unchanged)"
        for f in libdoorstop.dylib run.sh .doorstop_version; do
            BAD_PART="${DOORSTOP_URL##*/}: universal/$f"
            [ -f "$WORK/doorstop/universal/$f" ] || fail "$(t unpack_failed)
$(t unchanged)"
        done
        # The zip matched DOORSTOP_SHA256, so this only fails when the pinned
        # values do not belong together (DOORSTOP_DYLIB_SHA256 not updated
        # with the rest). Installing anyway would make every later run
        # download the Doorstop again.
        BAD_PART="${DOORSTOP_URL##*/}: universal/libdoorstop.dylib"
        check_file "$WORK/doorstop/universal/libdoorstop.dylib" "$DOORSTOP_DYLIB_SHA256" || fail "$(t ds_pin_bad)
$(t unchanged)"
        log "Doorstop: $DOORSTOP_BUILD, version $(head -c 32 "$WORK/doorstop/universal/.doorstop_version" | tr -cd '0-9A-Za-z.+-')"
    fi
    if [ "$NEED_FW" -eq 1 ]; then
        get_zip "$FRAMEWORK_ZIP" "https://github.com/$FRAMEWORK_REPO/releases/download/v$FW_VERSION/DragNWash.ModFramework-$FW_VERSION.zip" \
            "$WORK/framework.zip" "$(t fw_get)"
        if ! check_file "$WORK/framework.zip" "$FW_SHA256" "$FW_SIZE"; then
            if [ -n "$FRAMEWORK_ZIP" ]; then fail "$(t fw_zip_bad)
$(t unchanged)"; fi
            fail "$(t fw_bad)
$(t unchanged)"
        fi
        BAD_PART="DragNWash.ModFramework-$FW_VERSION.zip"
        unzip -q "$WORK/framework.zip" 'BepInEx/*' -d "$WORK/framework" || fail "$(t unpack_failed)
$(t unchanged)"
        # Only the libraries this mod needs, and each at least its minimum.
        for name in $FW_PARTS; do
            offered="$(dll_version "$WORK/framework/BepInEx/plugins/$name/$name.dll")"
            min="$(need_min "$name")"
            if [ -z "$offered" ] || { [ -n "$min" ] && [ "$(ver_cmp "$offered" "$min")" = -1 ]; }; then
                BAD_PART="$name${min:+ $min}"
                fail "$(t fw_short)
$(t unchanged)"
            fi
        done
        BAD_PART="Preloader"
        [ -f "$WORK/framework/BepInEx/patchers/$FRAMEWORK_PATCHER" ] || fail "$(t fw_short)
$(t unchanged)"
        say "$(t fw_ok)"
    fi

    # The script checks executable_name relative to the current folder when
    # nothing is passed to it, and Steam does not start it from the game
    # folder, so use the full path unless the path would break the shell
    # assignment. From Steam, %command% hands over the game itself anyway.
    exe="$GAME_DIR/$APP_BUNDLE"
    case "$exe" in *'"'*|*'$'*|*'`'*|*'\'*) exe="$APP_BUNDLE" ;; esac
    if [ "$NEED_DS" -eq 1 ]; then
        cp -f "$WORK/doorstop/universal/run.sh" "$WORK/run_bepinex.sh"
    else
        cp -f "$GAME_DIR/run_bepinex.sh" "$WORK/run_bepinex.sh"
    fi
    configure_run_script "$WORK/run_bepinex.sh" "$exe" || fail "$(t rb_bad)
$(t unchanged)"

    # From here on the game folder changes.
    # BepInEx, without its own libdoorstop.dylib and run_bepinex.sh: the pinned
    # Doorstop's take their place below.
    if [ "$NEED_BEP" -eq 1 ]; then
        BAD_PART="${BEPINEX_URL##*/}"
        unzip -oq "$WORK/bepinex.zip" -x libdoorstop.dylib run_bepinex.sh -d "$GAME_DIR" || fail "$(t unpack_failed)"
        echo "BepInEx was added by install-macos.sh (Drag'n Wash Localization)." > "$GAME_DIR/BepInEx/$MARKER"
        say "$(t bep_ok)"
    else
        say "$(t bep_have)"
    fi

    # Doorstop. A libdoorstop.dylib from before this install (BepInEx's own,
    # or one put there by hand) is kept as a backup.
    if [ "$NEED_DS" -eq 1 ]; then
        if [ -f "$GAME_DIR/libdoorstop.dylib" ] && ! cmp -s "$WORK/doorstop/universal/libdoorstop.dylib" "$GAME_DIR/libdoorstop.dylib"; then
            backup_once libdoorstop.dylib
        fi
        cp -f "$WORK/doorstop/universal/libdoorstop.dylib" "$GAME_DIR/libdoorstop.dylib"
        cp -f "$WORK/doorstop/universal/.doorstop_version" "$GAME_DIR/.doorstop_version"
        say "$(t ds_ok)"
    else
        say "$(t ds_have)"
    fi
    if [ -f "$GAME_DIR/run_bepinex.sh" ] && cmp -s "$WORK/run_bepinex.sh.new" "$GAME_DIR/run_bepinex.sh"; then
        say "$(t rb_same)"
    else
        backup_once run_bepinex.sh
        cp -f "$WORK/run_bepinex.sh.new" "$GAME_DIR/run_bepinex.sh"
        say "$(t rb_ok)"
    fi
    chmod u+x "$GAME_DIR/run_bepinex.sh"
    say "run_bepinex.sh: executable_name=\"$exe\""

    # Drag'n Wash ModFramework: the core, the Preloader and the libraries the
    # mod needs.
    if [ "$NEED_FW" -eq 1 ]; then
        for name in $FW_PARTS; do install_part "$name"; done
        install_preloader
    elif [ -n "$FW_VERSION" ]; then
        if [ -z "$FW_OFF$FW_OFF_HAND" ]; then say "$(t fw_have)"; fi
    elif [ ! -f "$GAME_DIR/BepInEx/plugins/$FRAMEWORK_PREFIX/$FRAMEWORK_PREFIX.dll" ]; then
        warn "$(t fw_nomanifest)"
    fi
    # Switched-off parts stay off, so the summary says how to get them back:
    # a library in the Mods screen (the mod does not load without it), the
    # core or the Preloader, which the Mods screen cannot switch, only by
    # renaming it back.
    if [ -n "$FW_OFF$FW_OFF_HAND" ]; then
        off_text=""
        if [ -n "$FW_OFF" ]; then off_text="$(t fw_off)"; fi
        if [ -n "$FW_OFF_HAND" ]; then off_text="${off_text}${off_text:+
}$(t fw_off_hand)"; fi
        if [ "$FW_OFF_PRE" -eq 1 ]; then off_text="$off_text
$(t fw_off_pre)"; fi
        if [ "$FW_OFF_OLD" -eq 1 ]; then off_text="$off_text
$(t fw_off_old)"; fi
        warn "$off_text"
    fi

    # Mod files, copied over what is there (SaveHistory and
    # Translations/_discovered are the player's and left alone), plus the
    # release's mod-install.json: the Mods screen reads its "keep" and
    # "configFiles" when the mod is uninstalled from there.
    dst="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    mkdir -p "$dst"
    while IFS= read -r f; do
        f="${f#./}"
        mkdir -p "$dst/$(dirname "$f")"
        cp -f "$PAYLOAD/$f" "$dst/$f"
    done < <(cd "$PAYLOAD" && find . -type f ! -path './Translations/_*' ! -path './SaveHistory/*' ! -name .DS_Store)
    if [ -f "$MANIFEST" ]; then cp -f "$MANIFEST" "$dst/mod-install.json"; fi
    # Files extracted from a browser download carry the quarantine flag.
    xattr -dr com.apple.quarantine "$GAME_DIR/BepInEx" "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib" 2>/dev/null || true
    say "$(t mod_ok): $dst"
    enable_plugin
    if [ "$MOD_ON" -eq 1 ]; then say "$(t mod_on)"; fi

    # Language in the plugin config
    cfg_dir="$GAME_DIR/BepInEx/config"; cfg="$cfg_dir/$CFG_NAME"
    mkdir -p "$cfg_dir"
    if [ -f "$cfg" ] && grep -q '^TargetLocale[[:space:]]*=' "$cfg"; then
        sed -i '' "s/^TargetLocale[[:space:]]*=.*/TargetLocale = $LANG_CHOICE/" "$cfg"
    elif [ -f "$cfg" ]; then
        printf '\n[General]\nTargetLocale = %s\n' "$LANG_CHOICE" >> "$cfg"
    else
        printf '[General]\n\nTargetLocale = %s\n' "$LANG_CHOICE" > "$cfg"
    fi
    say "$(t lang_set) $LANG_CHOICE"

    # Steam launch option. The full path in double quotes; Steam handles the
    # apostrophe in "Drag'n Wash" inside them.
    case "$(launch_option_state)" in
        yes) say "$(t lo_same)" ;;
        other) warn "$(t lo_other)
  $LAUNCH_OPTION" ;;
        *)
            rc=0; with_steam_closed set || rc=$?
            case "$rc" in
                0) say "$(t lo_done) $LAUNCH_OPTION" ;;
                3) warn "$(t steam_slow)
$(t lo_manual):
  $LAUNCH_OPTION" ;;
                4) warn "$(t lo_other)
  $LAUNCH_OPTION" ;;
                *) warn "$(t lo_manual):
  $LAUNCH_OPTION" ;;
            esac
            ;;
    esac

    finish_message "$(t done)"

elif [ "$MODE" = uninstall ]; then
    if [ ! -d "$GAME_DIR/BepInEx/plugins/$PLUGIN" ] && [ ! -f "$GAME_DIR/BepInEx/config/$CFG_NAME" ]; then
        finish_message "$(t nothing)"
        exit 0
    fi
    ask_yes "$(t confirm_uninstall)
$GAME_DIR" 1 || exit 1

    dir="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    keep=0
    # BepInEx/SaveHistory holds the snapshots of ModFramework's saves library.
    if [ -d "$dir/SaveHistory" ] || [ -d "$dir/Translations/_discovered" ] || [ -d "$GAME_DIR/BepInEx/SaveHistory" ]; then
        if ask_yes "$(t keep)" 1; then keep=1; fi
    fi
    if [ -d "$dir" ]; then
        if [ "$keep" -eq 1 ]; then
            find "$dir" -mindepth 1 -maxdepth 1 ! -name SaveHistory ! -name Translations -exec rm -rf {} +
            if [ -d "$dir/Translations" ]; then
                find "$dir/Translations" -mindepth 1 -maxdepth 1 ! -name _discovered -exec rm -rf {} +
                [ -d "$dir/Translations/_discovered" ] || rmdir "$dir/Translations" 2>/dev/null || true
            fi
            rmdir "$dir" 2>/dev/null || true
        else
            rm -rf "$dir"
        fi
        say "$(t mod_removed)"
    fi
    rm -f "$GAME_DIR/BepInEx/config/$CFG_NAME"
    # What the Mods screen recorded about the mod (switched off, or to be
    # uninstalled at the next start) goes with it; a ModFramework that stays
    # would otherwise keep those entries, and apply them to a later install.
    forget_folder "$PLUGIN"

    # Same rules as the Deck script: ModFramework, BepInEx and the launch
    # option only matter to other mods now.
    # The config file alone is enough to get here, so plugins/ may be gone. A
    # failing find would take the whole uninstall down with it under
    # set -e/pipefail, half-done and with nothing on screen.
    others=""
    if [ -d "$GAME_DIR/BepInEx/plugins" ]; then
        # Only whether the listing is empty matters, so the exit status is the
        # part to keep: a probe that fails must not read as "no other mods
        # left", because that answer removes BepInEx and everything under it.
        # An unreadable directory keeps BepInEx, the same as a mod being there.
        if ! others="$(find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" ! -name "$FRAMEWORK_PREFIX*" 2>/dev/null)"; then
            others="?"
            log "could not list BepInEx/plugins; keeping BepInEx"
        fi
    fi
    if [ -z "$others" ] && [ -d "$GAME_DIR/BepInEx/patchers" ]; then
        # Another mod's patcher needs BepInEx as much as a plugin does.
        if ! others="$(find "$GAME_DIR/BepInEx/patchers" -mindepth 1 -maxdepth 1 ! -name "$FRAMEWORK_PATCHER" ! -name "$FRAMEWORK_PATCHER.disabled" 2>/dev/null)"; then
            others="?"
            log "could not list BepInEx/patchers; keeping BepInEx"
        fi
    fi
    if [ -n "$others" ]; then
        if [ -n "$(framework_left)" ]; then note "$(t fw_kept)"; fi
        note "$(t bep_kept)"
    else
        # ModFramework goes only when this script installed it, and then only
        # the parts it added (FW_MARKER). A copy that was there before, put
        # there by hand and maybe patched, stays unless the user says to
        # remove it; with --yes it stays.
        fw_before="$(framework_left)"
        if [ -f "$GAME_DIR/BepInEx/$FW_MARKER" ]; then
            remove_marked_framework
            fw_rest="$(framework_left)"
            if [ -z "$fw_before" ]; then
                log "framework marker without ModFramework; marker removed"
            elif [ "$fw_rest" = "$fw_before" ]; then
                note "$(t fw_not_ours)"
            elif [ -n "$fw_rest" ]; then
                say "$(t fw_removed_ours)"
                note "$(t fw_left)
$(describe_parts "$fw_rest")"
                # Without a Preloader nothing renames a switched-off part back.
                if parts_switched_off "$fw_rest" && [ ! -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER" ] &&
                    [ ! -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER.disabled" ]; then
                    note "$(t fw_left_off)"
                fi
            else
                say "$(t fw_removed)"
            fi
        elif [ -n "$fw_before" ]; then
            if ask_yes "$(t fw_ask)" 0; then
                find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 -name "$FRAMEWORK_PREFIX*" -exec rm -rf {} + 2>/dev/null || true
                rm -f "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER" "$GAME_DIR/BepInEx/patchers/$FRAMEWORK_PATCHER.disabled" \
                    "$GAME_DIR/BepInEx/config/com.tomxv.dragnwash.modframework"*
                log "ModFramework removed on request; this script had not installed it"
                say "$(t fw_removed)"
            else
                log "ModFramework kept: there is no $FW_MARKER, so this script did not install it"
                note "$(t fw_not_ours)"
            fi
        fi
        if [ -n "$(framework_left)" ]; then
            # What is left of ModFramework still loads through BepInEx and the
            # launch option. Taking those away would leave it installed but
            # never started, and removing BepInEx/ would delete it outright.
            if [ "$REMOVE_BEPINEX" -eq 1 ]; then
                log "--remove-bepinex not applied: ModFramework stays"
                warn "$(t bep_kept_fw)"
            else
                note "$(t bep_kept_fw)"
            fi
        else
            if [ "$keep" -eq 0 ]; then rm -rf "$GAME_DIR/BepInEx/SaveHistory"; fi
            if [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]; then
                # The marker says this script put BepInEx there; then removing
                # it is the default, also with --yes. BepInEx that was there
                # before stays unless --remove-bepinex or the answer says
                # otherwise.
                default_remove=0; [ -f "$GAME_DIR/BepInEx/$MARKER" ] && default_remove=1
                remove_bep=0
                if [ "$REMOVE_BEPINEX" -eq 1 ]; then
                    remove_bep=1
                elif ask_yes "$(t rmbep)" "$default_remove"; then
                    remove_bep=1
                fi
                if [ "$remove_bep" -eq 1 ]; then
                    rm -f "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib" "$GAME_DIR/.doorstop_version" \
                        "$GAME_DIR/run_bepinex.sh$BACKUP_SUFFIX" "$GAME_DIR/libdoorstop.dylib$BACKUP_SUFFIX"
                    # BepInEx's changelog.txt only says "0 commits since
                    # v5.4.23.5".
                    if [ -f "$GAME_DIR/changelog.txt" ] && grep -Eqi 'bepinex|doorstop|commits since v5\.4\.23' "$GAME_DIR/changelog.txt"; then
                        rm -f "$GAME_DIR/changelog.txt"
                    fi
                    # Left by a start that failed, next to the game's executable.
                    rm -f "$GAME_DIR/$APP_BUNDLE/Contents/MacOS/"preloader_*.log
                    if [ "$keep" -eq 1 ] && { [ -d "$dir" ] || [ -d "$GAME_DIR/BepInEx/SaveHistory" ]; }; then
                        find "$GAME_DIR/BepInEx" -mindepth 1 -maxdepth 1 ! -name plugins ! -name SaveHistory -exec rm -rf {} +
                        if [ -d "$GAME_DIR/BepInEx/plugins" ]; then
                            find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" -exec rm -rf {} +
                            rmdir "$GAME_DIR/BepInEx/plugins" 2>/dev/null || true
                        fi
                    else
                        rm -rf "$GAME_DIR/BepInEx"
                    fi
                    say "$(t bep_removed)"
                fi
            fi
            if [ "$(launch_option_any)" = no ]; then
                log "no launch option to take out"
            else
                rc=0; with_steam_closed remove || rc=$?
                case "$rc" in
                    0) say "$(t lo_removed)" ;;
                    3) warn "$(t steam_slow_remove)
$(t lo_manual_remove)" ;;
                    *) warn "$(t lo_manual_remove)" ;;
                esac
            fi
        fi
    fi
    finish_message "$(t undone)${NOTES:+

$NOTES}"

else  # check
    plugin_dll="$GAME_DIR/BepInEx/plugins/$PLUGIN/$PLUGIN.dll"
    if [ ! -f "$plugin_dll" ]; then
        finish_message "$(t nothing)"
        exit 0
    fi
    bep_log="$GAME_DIR/BepInEx/LogOutput.log"
    # Unity starts a new Player.log at every start and moves the old one to
    # Player-prev.log, so the newest Player.log was created when the game was
    # last started.
    last_start=0
    for player_log in "$HOME/Library/Logs"/*/DragNWash/Player.log; do
        [ -f "$player_log" ] || continue
        born="$(stat -f %B "$player_log" 2>/dev/null || echo 0)"
        if [ "$born" -gt "$last_start" ]; then last_start="$born"; fi
    done
    started_after=0
    if [ "$last_start" -gt 0 ] && [ "$last_start" -ge "$(stat -f %m "$plugin_dll")" ]; then started_after=1; fi
    stale=0
    if [ "$last_start" -gt 0 ]; then
        LAST_START_TEXT="$(date -r "$last_start" '+%Y-%m-%d %H:%M:%S')"
        if [ -f "$bep_log" ] && [ "$(stat -f %m "$bep_log")" -lt "$last_start" ]; then stale=1; fi
    fi
    lo_state="$(launch_option_state)"

    # Neither BepInEx nor Unity writes the process architecture to its log, so
    # it can only be read from a running game: macOS marks a process that
    # Rosetta translates with P_TRANSLATED (0x20000) in the flags ps prints.
    arch_now=""
    game_pid="$(pgrep -f "$GAME_PROCESS" 2>/dev/null | head -1 || true)"
    if [ -n "$game_pid" ]; then
        flags="$(ps -o flags= -p "$game_pid" 2>/dev/null | tr -d ' ' || true)"
        case "$flags" in
            "" | *[!0-9a-fA-F]*) ;;
            *) if [ $((0x$flags & 0x20000)) -ne 0 ] || [ "$(uname -m)" = x86_64 ]; then arch_now=x86_64; else arch_now=arm64; fi ;;
        esac
    fi
    ARCH_PREF="$(sed -n 's/^archpreference="\([^"]*\)"$/\1/p' "$GAME_DIR/run_bepinex.sh" 2>/dev/null | head -1 || true)"
    ARCH_PREF="${ARCH_PREF:-?}"
    case "$arch_now" in
        x86_64) arch_text="$(t chk_arch_x86)" ;;
        arm64) arch_text="" ; warn "$(t chk_arch_arm)" ;;
        *) arch_text="$(t chk_arch_log)" ;;
    esac
    log "check: LogOutput.log=$([ -f "$bep_log" ] && echo yes || echo no) stale=$stale last_start=${LAST_START_TEXT:-none} started_after=$started_after launch_option=$lo_state arch_now=${arch_now:-unknown} archpreference=$ARCH_PREF"
    if ! rosetta_ok; then warn "$(t norosetta)"; fi

    if [ -f "$bep_log" ] && [ "$stale" -eq 0 ]; then
        mod_line="$(grep -F -m1 "Loading [$PLUGIN " "$bep_log" || true)"
        fw_line="$(grep -F -m1 "Loading [$FRAMEWORK_PREFIX " "$bep_log" || true)"
        chain_line="$(grep -F -m1 'Chainloader startup complete' "$bep_log" || true)"
        if [ -n "$mod_line" ] && [ -n "$chain_line" ]; then
            finish_message "$(t chk_loaded)
  $chain_line
  ${fw_line:+$fw_line
  }$mod_line${arch_text:+

$arch_text}"
        elif [ -n "$chain_line" ]; then
            # BepInEx names the plugin when it refuses it, for example for a
            # missing or too old dependency.
            about="$(grep -F "$PLUGIN" "$bep_log" | head -3 || true)"
            warn "$(t chk_bep_only)
  $bep_log${about:+
$about}"
            finish_message "$GAME_DIR${arch_text:+

$arch_text}"
        else
            warn "$(t chk_no_chain)
  $bep_log"
            finish_message "$GAME_DIR${arch_text:+

$arch_text}"
        fi
    elif [ "$stale" -eq 1 ] || [ "$started_after" -eq 1 ]; then
        if [ "$stale" -eq 1 ]; then why="$(t chk_stale)"; else why="$(t chk_not_started)"; fi
        if [ "$lo_state" = other ]; then
            # Set, but to a run_bepinex.sh in another folder (a moved library,
            # a reinstall elsewhere). Install leaves such an option alone, so
            # sending the user back to it would change nothing.
            warn "$why

$(t lo_other)
  $LAUNCH_OPTION"
        elif [ "$lo_state" != yes ]; then
            warn "$why

$(t chk_no_launch_option)
  $LAUNCH_OPTION"
        else
            warn "$why

$(t chk_from_steam)"
        fi
        finish_message "$GAME_DIR${arch_text:+

$arch_text}"
    else
        finish_message "$(t chk_not_run)"
    fi
fi
