#!/bin/bash
# Drag'n Wash Localization - EXPERIMENTAL installer / uninstaller for macOS.
#
# !! EXPERIMENTAL. The mod does not work on macOS yet: the Doorstop loader
# !! that BepInEx 5.4.23.5 uses cannot hook Unity 6.3 games
# !! (https://github.com/NeighTools/UnityDoorstop/issues/108). This script is
# !! kept ready for when a BepInEx release carries that fix. It is not in the
# !! release zip.
#
# Run it from the extracted release folder, in Terminal:
#   bash /path/to/install-macos.sh              asks what to do
#   bash /path/to/install-macos.sh --install    install or update straight away
#   bash /path/to/install-macos.sh --uninstall  remove the mod straight away
#   bash /path/to/install-macos.sh --check      tell whether the mod loaded
#
# What it does (the macOS version of the manual steps):
#   1. finds DragNWash.app in your Steam libraries
#   2. downloads the macOS build of BepInEx if it is missing and checks its
#      SHA-256
#   3. sets executable_name in run_bepinex.sh to the full path of the .app, so
#      it works whatever folder Steam starts it from
#   4. copies the mod and sets the language you pick
#   5. sets the Steam launch option "<game folder>/run_bepinex.sh" %command%
#      (Steam has to be closed for that; you are asked first)
#
# Options: --install  --uninstall  --check  --lang <locale>  --game-dir <path>
#          --payload <extracted zip folder>  --yes (no questions, use defaults)
#          --remove-bepinex (with --uninstall)  --close-steam (close Steam
#          without asking)  --terminal (no dialogs)  --ui en|ja|zh
#
# Written for the bash 3.2 that ships with macOS: no mapfile, no empty arrays
# under set -u, no here-documents inside $(...).
set -euo pipefail

APP_ID=4739660
APP_BUNDLE="DragNWash.app"
GAME_PROCESS="DragNWash.app/Contents/MacOS/DragNWash"
PLUGIN="DragNWashLocalization"
CFG_NAME="com.tomxv.dragnwash.localization.cfg"
MARKER=".bepinex-installed-by-dragnwash-localization"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip"
BEPINEX_SHA256="01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323"
# 1 while the BepInEx above is known not to start on Unity 6.3 (UnityDoorstop#108).
# When a fixed BepInEx is released: update BEPINEX_URL / BEPINEX_SHA256, test
# on a Mac, and set this to 0.
KNOWN_ISSUE=1
KNOWN_ISSUE_URL="https://github.com/NeighTools/UnityDoorstop/issues/108"
STEAM_ROOT="$HOME/Library/Application Support/Steam"

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
        -h|--help) sed -n '2,31p' "$0"; exit 0 ;;
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
        ja:known_issue) echo "【ご注意】現在、macOS では BepInEx が起動しない既知の不具合があります。BepInEx が使う読み込み役（Doorstop）が Unity 6.3 に対応していないためです。
$KNOWN_ISSUE_URL

修正された BepInEx が出るまで、導入しても Mod は動きません（ゲームは英語のまま起動します）。" ;;
        zh:known_issue) echo "【注意】目前在 macOS 上有 BepInEx 无法启动的已知问题：BepInEx 使用的加载器（Doorstop）尚不支持 Unity 6.3。
$KNOWN_ISSUE_URL

在修复后的 BepInEx 发布之前，即使安装，Mod 也不会生效（游戏仍以英文启动）。" ;;
        *:known_issue) echo "Heads-up: BepInEx does not start on macOS at the moment. The loader it uses (Doorstop) cannot hook Unity 6.3 games yet.
$KNOWN_ISSUE_URL

Until a fixed BepInEx is released, the mod will not work even when installed (the game starts in English)." ;;
        ja:known_issue_ask) echo "それでも導入しますか？" ;;
        zh:known_issue_ask) echo "仍要安装吗？" ;;
        *:known_issue_ask) echo "Install anyway?" ;;
        ja:nopayload) echo "Mod のファイルが見つかりません。zip を丸ごと展開して、その中のこのスクリプトを実行するか、--payload で展開したフォルダを指定してください。" ;;
        zh:nopayload) echo "找不到 Mod 文件。请完整解压 zip 并运行其中的此脚本，或用 --payload 指定解压后的文件夹。" ;;
        *:nopayload) echo "The mod files are missing. Extract the whole zip and run this script from inside it, or pass the extracted folder with --payload." ;;
        ja:nogame) echo "Drag'n Wash（DragNWash.app）が見つかりません。--game-dir でゲームのフォルダを指定してください。" ;;
        zh:nogame) echo "找不到 Drag'n Wash（DragNWash.app）。请用 --game-dir 指定游戏文件夹。" ;;
        *:nogame) echo "Drag'n Wash (DragNWash.app) was not found. Pass the game folder with --game-dir." ;;
        ja:running) echo "ゲームが起動中です。先に終了してください。" ;;
        zh:running) echo "游戏正在运行。请先关闭游戏。" ;;
        *:running) echo "The game is running. Close it first." ;;
        ja:pick) echo "言語を選んでください" ;;
        zh:pick) echo "请选择语言" ;;
        *:pick) echo "Choose a language" ;;
        ja:bep_have) echo "BepInEx: 導入済み" ;;
        zh:bep_have) echo "BepInEx：已安装" ;;
        *:bep_have) echo "BepInEx: already installed" ;;
        ja:bep_get) echo "BepInEx: ダウンロード中..." ;;
        zh:bep_get) echo "BepInEx：正在下载..." ;;
        *:bep_get) echo "BepInEx: downloading..." ;;
        ja:bep_bad) echo "BepInEx のダウンロードが改ざんされているか壊れています（SHA-256 不一致）。中止します。" ;;
        zh:bep_bad) echo "下载的 BepInEx 已损坏或被篡改（SHA-256 不一致）。已中止。" ;;
        *:bep_bad) echo "The BepInEx download is corrupt or tampered with (SHA-256 mismatch). Stopping." ;;
        ja:bep_ok) echo "BepInEx: 検証 OK、展開しました" ;;
        zh:bep_ok) echo "BepInEx：校验通过，已解压" ;;
        *:bep_ok) echo "BepInEx: verified and unpacked" ;;
        ja:mod_ok) echo "Mod: ファイルをコピーしました" ;;
        zh:mod_ok) echo "Mod：文件已复制" ;;
        *:mod_ok) echo "Mod: files copied" ;;
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
        ja:done) echo "完了しました。Steam からゲームを起動してください。起動したあとでこのスクリプトをもう一度実行し「動作確認」を選ぶと、Mod が読み込まれたか確かめられます。" ;;
        zh:done) echo "完成。请从 Steam 启动游戏。启动后再次运行此脚本并选择「检查运行状态」，即可确认 Mod 是否已加载。" ;;
        *:done) echo "Done. Start the game from Steam. Afterwards, run this script again and choose Check to see whether the mod loaded." ;;
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
        ja:confirm_install) echo "次のフォルダに Mod を導入します。よろしいですか？" ;;
        zh:confirm_install) echo "将把 Mod 安装到以下文件夹。继续吗？" ;;
        *:confirm_install) echo "Install the mod into this folder?" ;;
        ja:confirm_uninstall) echo "次のフォルダから Mod を削除します。よろしいですか？" ;;
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
        ja:chk_not_started) echo "導入後にゲームは起動されていますが、BepInEx が起動していません（BepInEx/LogOutput.log がありません）。" ;;
        zh:chk_not_started) echo "安装后游戏已启动过，但 BepInEx 没有启动（没有 BepInEx/LogOutput.log）。" ;;
        *:chk_not_started) echo "The game has been started since the install, but BepInEx did not start (there is no BepInEx/LogOutput.log)." ;;
        ja:chk_no_launch_option) echo "Steam の起動オプションが設定されていません。インストールをもう一度実行するか、手動で設定してください:" ;;
        zh:chk_no_launch_option) echo "尚未设置 Steam 启动选项。请重新运行安装，或手动设置：" ;;
        *:chk_no_launch_option) echo "The Steam launch option is not set. Run the install again, or set it by hand:" ;;
        ja:chk_known) echo "起動オプションは設定済みなので、既知の不具合（Doorstop が Unity 6.3 に未対応）によるものと思われます。" ;;
        zh:chk_known) echo "启动选项已设置，因此很可能是已知问题（Doorstop 尚不支持 Unity 6.3）所致。" ;;
        *:chk_known) echo "The launch option is set, so this is most likely the known issue (Doorstop cannot hook Unity 6.3)." ;;
        ja:chk_not_run) echo "導入後、まだゲームを起動していないようです。Steam から一度起動して、終了してからもう一度確認してください。" ;;
        zh:chk_not_run) echo "安装后似乎还没有启动过游戏。请先从 Steam 启动一次，退出后再检查。" ;;
        *:chk_not_run) echo "The game does not seem to have been started since the install. Start it once from Steam, quit, and check again." ;;
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
steam_running() { pgrep -x steam_osx >/dev/null 2>&1; }

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
patch_run_script() {
    local script="$GAME_DIR/run_bepinex.sh" exe tmp="$WORK/run_bepinex.sh"
    [ -f "$script" ] || return 0
    # The script checks executable_name relative to the current folder, and
    # Steam does not start it from the game folder, so use the full path
    # unless the path would break the shell assignment.
    exe="$GAME_DIR/$APP_BUNDLE"
    case "$exe" in *'"'*|*'$'*|*'`'*|*'\'*) exe="$APP_BUNDLE" ;; esac
    # On Apple Silicon the script execs /usr/bin/arch with DYLD_INSERT_LIBRARIES
    # still exported. arch is arm64e, libdoorstop.dylib is not, and newer macOS
    # kills arch for it (UnityDoorstop#107). Move the variable out of arch's
    # own environment; arch -e hands it to the game.
    NEW_EXE="$exe" \
    ARCH_LINE='exec arch -e DYLD_INSERT_LIBRARIES="${DYLD_INSERT_LIBRARIES}" "$executable_path" "$@"' \
    awk '
        /^executable_name=/ { print "executable_name=\"" ENVIRON["NEW_EXE"] "\""; next }
        {
            line = $0
            ind = line; sub(/[^ \t].*$/, "", ind)
            body = line; sub(/^[ \t]+/, "", body)
            if (body == ENVIRON["ARCH_LINE"]) {
                print ind "# dragnwash-localization: keep DYLD_INSERT_LIBRARIES out of arch itself (UnityDoorstop#107)"
                print ind "doorstop_insert=\"${DYLD_INSERT_LIBRARIES}\""
                print ind "unset DYLD_INSERT_LIBRARIES"
                print ind "exec arch -e DYLD_INSERT_LIBRARIES=\"${doorstop_insert}\" \"$executable_path\" \"$@\""
                next
            }
            print
        }' "$script" > "$tmp"
    cat "$tmp" > "$script"
    chmod +x "$script"
    say "run_bepinex.sh: executable_name=\"$exe\""
    if grep -q 'UnityDoorstop#107' "$script"; then log "run_bepinex.sh: arch helper patch present"; fi
}

# ------------------------------------------------------------------ main ----
log "---- start: $0 $ARGV (mode=${MODE:-ask}, ui=$UI, gui=$GUI, payload=$PAYLOAD)"
say "== $(t title)"

if [ -z "$GAME_DIR" ]; then GAME_DIR="$(find_game || true)"; fi
[ -n "$GAME_DIR" ] && [ -d "$GAME_DIR/$APP_BUNDLE" ] || fail "$(t nogame)"
GAME_DIR="$(cd "$GAME_DIR" && pwd)"
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

    if [ "$KNOWN_ISSUE" -eq 1 ]; then
        if [ "$ASSUME_YES" -eq 1 ]; then
            warn "$(t known_issue)"
        elif ! ask_yes "$(t known_issue)

$(t known_issue_ask)" 0; then
            exit 1
        fi
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
            while IFS="$(printf '\t')" read -r code name; do
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
            while IFS="$(printf '\t')" read -r code name; do
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

    ask_yes "$(t confirm_install)
$GAME_DIR" 1 || exit 1

    # BepInEx
    if [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ] && [ -f "$GAME_DIR/libdoorstop.dylib" ]; then
        say "$(t bep_have)"
    else
        say "$(t bep_get)"
        curl -fsSL -o "$WORK/bepinex.zip" "$BEPINEX_URL"
        actual="$(shasum -a 256 "$WORK/bepinex.zip" | cut -d' ' -f1)"
        [ "$actual" = "$BEPINEX_SHA256" ] || fail "$(t bep_bad)"
        unzip -oq "$WORK/bepinex.zip" -d "$GAME_DIR"
        : > "$GAME_DIR/BepInEx/$MARKER"
        say "$(t bep_ok)"
    fi
    patch_run_script

    # Mod files (SaveHistory and Translations/_discovered are left alone)
    dst="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    mkdir -p "$dst/Translations"
    for f in "$PLUGIN.dll" FlagCatalog.csv dragnwash-menufont.bundle dragnwash-menufont-LICENSE.txt; do
        if [ -f "$PAYLOAD/$f" ]; then cp -f "$PAYLOAD/$f" "$dst/"; fi
    done
    if [ -d "$PAYLOAD/data" ]; then mkdir -p "$dst/data"; cp -R "$PAYLOAD/data/." "$dst/data/"; fi
    if [ -f "$PAYLOAD/Translations/ignore.txt" ]; then cp -f "$PAYLOAD/Translations/ignore.txt" "$dst/Translations/"; fi
    for d in "$PAYLOAD/Translations"/*/; do
        [ -d "$d" ] || continue
        code="$(basename "$d")"
        case "$code" in _*) continue ;; esac
        mkdir -p "$dst/Translations/$code"
        cp -R "$d." "$dst/Translations/$code/"
    done
    # Files extracted from a browser download carry the quarantine flag.
    xattr -dr com.apple.quarantine "$GAME_DIR/BepInEx" "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib" 2>/dev/null || true
    say "$(t mod_ok): $dst"

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

    # Steam launch option
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

    if [ "$KNOWN_ISSUE" -eq 1 ] && [ "$ASSUME_YES" -eq 0 ]; then
        finish_message "$(t done)

$(t known_issue)"
    else
        finish_message "$(t done)"
    fi

elif [ "$MODE" = uninstall ]; then
    if [ ! -d "$GAME_DIR/BepInEx/plugins/$PLUGIN" ] && [ ! -f "$GAME_DIR/BepInEx/config/$CFG_NAME" ]; then
        finish_message "$(t nothing)"
        exit 0
    fi
    ask_yes "$(t confirm_uninstall)
$GAME_DIR" 1 || exit 1

    dir="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    keep=0
    if [ -d "$dir/SaveHistory" ] || [ -d "$dir/Translations/_discovered" ]; then
        if ask_yes "$(t keep)" 1; then keep=1; fi
    fi
    if [ -d "$dir" ]; then
        if [ "$keep" -eq 1 ]; then
            find "$dir" -mindepth 1 -maxdepth 1 ! -name SaveHistory ! -name Translations -exec rm -rf {} +
            if [ -d "$dir/Translations" ]; then
                find "$dir/Translations" -mindepth 1 -maxdepth 1 ! -name _discovered -exec rm -rf {} +
                [ -d "$dir/Translations/_discovered" ] || rmdir "$dir/Translations" 2>/dev/null || true
            fi
        else
            rm -rf "$dir"
        fi
        say "$(t mod_removed)"
    fi
    rm -f "$GAME_DIR/BepInEx/config/$CFG_NAME"

    # Same rules as the Deck script: BepInEx and the launch option only matter
    # to other mods now.
    # The config file alone is enough to get here, so plugins/ may be gone. A
    # failing find would take the whole uninstall down with it under
    # set -e/pipefail, half-done and with nothing on screen.
    others=""
    if [ -d "$GAME_DIR/BepInEx/plugins" ]; then
        # Only whether the listing is empty matters, so the exit status is the
        # part to keep: a probe that fails must not read as "no other mods
        # left", because that answer removes BepInEx and everything under it.
        # An unreadable directory keeps BepInEx, the same as a mod being there.
        if ! others="$(find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" 2>/dev/null)"; then
            others="?"
            log "could not list BepInEx/plugins; keeping BepInEx"
        fi
    fi
    if [ -n "$others" ]; then
        say "$(t bep_kept)"
    else
        if [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]; then
            default_remove=0; [ -f "$GAME_DIR/BepInEx/$MARKER" ] && default_remove=1
            remove_bep=0
            if [ "$REMOVE_BEPINEX" -eq 1 ]; then
                remove_bep=1
            elif [ "$ASSUME_YES" -eq 0 ] && ask_yes "$(t rmbep)" "$default_remove"; then
                remove_bep=1
            fi
            if [ "$remove_bep" -eq 1 ]; then
                rm -f "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib" "$GAME_DIR/.doorstop_version"
                if [ -f "$GAME_DIR/changelog.txt" ] && grep -Eqi 'bepinex|doorstop' "$GAME_DIR/changelog.txt"; then rm -f "$GAME_DIR/changelog.txt"; fi
                if [ "$keep" -eq 1 ] && [ -d "$dir" ]; then
                    find "$GAME_DIR/BepInEx" -mindepth 1 -maxdepth 1 ! -name plugins -exec rm -rf {} +
                    find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" -exec rm -rf {} +
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
    finish_message "$(t undone)"

else  # check
    plugin_dll="$GAME_DIR/BepInEx/plugins/$PLUGIN/$PLUGIN.dll"
    if [ ! -f "$plugin_dll" ]; then
        finish_message "$(t nothing)"
        exit 0
    fi
    bep_log="$GAME_DIR/BepInEx/LogOutput.log"
    started_after=0
    for player_log in "$HOME/Library/Logs"/*/DragNWash/Player.log; do
        if [ -f "$player_log" ] && [ "$player_log" -nt "$plugin_dll" ]; then started_after=1; fi
    done
    lo_state="$(launch_option_state)"
    log "check: LogOutput.log=$([ -f "$bep_log" ] && echo yes || echo no) started_after=$started_after launch_option=$lo_state"
    if [ -f "$bep_log" ] && grep -Eq "$PLUGIN|Drag'n Wash Localization" "$bep_log"; then
        finish_message "$(t chk_loaded)"
    elif [ -f "$bep_log" ]; then
        warn "$(t chk_bep_only)
  $bep_log"
        finish_message "$GAME_DIR"
    elif [ "$started_after" -eq 1 ]; then
        if [ "$lo_state" != yes ]; then
            warn "$(t chk_not_started)

$(t chk_no_launch_option)
  $LAUNCH_OPTION"
        elif [ "$KNOWN_ISSUE" -eq 1 ]; then
            warn "$(t chk_not_started)

$(t chk_known)
$KNOWN_ISSUE_URL"
        else
            warn "$(t chk_not_started)"
        fi
        finish_message "$GAME_DIR"
    else
        finish_message "$(t chk_not_run)"
    fi
fi
