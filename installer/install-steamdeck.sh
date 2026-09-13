#!/usr/bin/env bash
# Drag'n Wash Localization - installer / uninstaller for Steam Deck and Linux.
#
# Run it from the extracted release folder, in Desktop Mode:
#   bash install-steamdeck.sh              asks whether to install or uninstall
#   bash install-steamdeck.sh --install    install or update straight away
#   bash install-steamdeck.sh --uninstall  remove the mod straight away
#
# What it does (the same as the manual steps in the README):
#   1. finds Drag'n Wash in your Steam libraries
#   2. downloads the Linux build of BepInEx 5.4.23.5 if it is missing and
#      checks its SHA-256
#   3. sets executable_name="DragNWash" in run_bepinex.sh
#   4. copies the mod and sets the language you pick
#   5. sets the Steam launch option ./run_bepinex.sh %command%
#      (Steam has to be closed for that; you are asked first)
#
# Options: --install  --uninstall  --lang <locale>  --game-dir <path>
#          --yes (no questions, use defaults; installs unless --uninstall)
#          --remove-bepinex (with --uninstall)  --ui en|ja|zh
set -euo pipefail

APP_ID=4739660
GAME_BIN="DragNWash"
PLUGIN="DragNWashLocalization"
CFG_NAME="com.tomxv.dragnwash.localization.cfg"
MARKER=".bepinex-installed-by-dragnwash-localization"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x64_5.4.23.5.zip"
BEPINEX_SHA256="e538560be65739f562519ab518a75f9c65b3f57f87457403ae7cde683c12dab7"
LAUNCH_OPTION="./run_bepinex.sh %command%"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PAYLOAD="$HERE/BepInEx/plugins/$PLUGIN"

MODE=""
LANG_CHOICE=""
GAME_DIR=""
ASSUME_YES=0
REMOVE_BEPINEX=0
UI=""

while [ $# -gt 0 ]; do
    case "$1" in
        --install) MODE=install ;;
        --uninstall) MODE=uninstall ;;
        --lang) LANG_CHOICE="${2:-}"; shift ;;
        --game-dir) GAME_DIR="${2:-}"; shift ;;
        --yes|-y) ASSUME_YES=1 ;;
        --remove-bepinex) REMOVE_BEPINEX=1 ;;
        --ui) UI="${2:-}"; shift ;;
        -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
    shift
done

# ----------------------------------------------------------------- strings --
# Which language to talk in, and which translation to preselect.
#   1. The desktop's language, when it is not English. Prompts exist in
#      English, Japanese and Chinese; for the other packs only the
#      preselected language follows.
#   2. Otherwise Steam's own language. Desktop Mode on a Deck is English
#      unless someone changed it in System Settings, while Steam itself is
#      often set to the player's language, and that is what they see in
#      Gaming Mode.
#   3. English.
DEFAULT_LOCALE=en
detect_ui() {
    local v
    for v in "${LC_ALL:-}" "${LC_MESSAGES:-}" "${LANGUAGE:-}" "${LANG:-}"; do
        case "$v" in
            ja*) echo "ja ja"; return ;;
            zh_TW*|zh_HK*|zh_MO*|zh-Hant*) echo "zh zh-Hant"; return ;;
            zh*) echo "zh zh-Hans"; return ;;
            # No prompts in these languages, but preselect their pack.
            ko*) echo "en ko"; return ;;
            de*) echo "en de"; return ;;
            fr*) echo "en fr"; return ;;
            es*) echo "en es"; return ;;
            pt_BR*|pt-BR*) echo "en pt-BR"; return ;;
            ru*) echo "en ru"; return ;;
            pl*) echo "en pl"; return ;;
            he*|iw*) echo "en he"; return ;;
            eo*) echo "en eo"; return ;;
        esac
    done
    local steam_lang
    steam_lang="$(sed -n 's/^[[:space:]]*"language"[[:space:]]*"\([^"]*\)".*/\1/p' "$HOME/.steam/registry.vdf" 2>/dev/null | head -1)"
    case "$steam_lang" in
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
read -r DETECTED_UI DEFAULT_LOCALE < <(detect_ui)
[ -n "$UI" ] || UI="$DETECTED_UI"

t() {
    local key="$1"
    case "$UI:$key" in
        ja:title) echo "Drag'n Wash Localization（Steam Deck / Linux）" ;;
        zh:title) echo "Drag'n Wash Localization（Steam Deck / Linux）" ;;
        *:title) echo "Drag'n Wash Localization (Steam Deck / Linux)" ;;
        ja:nopayload) echo "Mod のファイルが見つかりません。zip を丸ごと展開して、その中でこのスクリプトを実行してください。" ;;
        zh:nopayload) echo "找不到 Mod 文件。请完整解压 zip，并在解压后的文件夹中运行此脚本。" ;;
        *:nopayload) echo "The mod files are missing. Extract the whole zip and run this script from inside it." ;;
        ja:nogame) echo "Drag'n Wash が見つかりません。--game-dir でゲームのフォルダを指定してください。" ;;
        zh:nogame) echo "找不到 Drag'n Wash。请用 --game-dir 指定游戏文件夹。" ;;
        *:nogame) echo "Drag'n Wash was not found. Pass the game folder with --game-dir." ;;
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
        ja:lo_ask) echo "Steam の起動オプションを自動で設定するには、Steam を一度終了する必要があります。Steam を終了して設定しますか？（終了後、Steam を再起動します）" ;;
        zh:lo_ask) echo "要自动设置 Steam 启动选项，需要先关闭 Steam。现在关闭 Steam 并设置吗？（设置后会重新启动 Steam）" ;;
        *:lo_ask) echo "Setting the Steam launch option automatically needs Steam to be closed. Close Steam and set it now? (Steam is started again afterwards)" ;;
        ja:lo_manual) echo "起動オプションは手動で設定してください: Steam でゲームのプロパティ → 起動オプション に次を入力" ;;
        zh:lo_manual) echo "请手动设置启动选项：在 Steam 中打开游戏属性 → 启动选项，输入以下内容" ;;
        *:lo_manual) echo "Set the launch option by hand: in Steam, game Properties → Launch Options, enter" ;;
        ja:lo_done) echo "起動オプションを設定しました:" ;;
        zh:lo_done) echo "启动选项已设置：" ;;
        *:lo_done) echo "Launch option set:" ;;
        ja:lo_removed) echo "起動オプションを元に戻しました" ;;
        zh:lo_removed) echo "启动选项已恢复" ;;
        *:lo_removed) echo "Launch option restored" ;;
        ja:steam_wait) echo "Steam の終了を待っています..." ;;
        zh:steam_wait) echo "正在等待 Steam 退出..." ;;
        *:steam_wait) echo "Waiting for Steam to exit..." ;;
        ja:steam_slow) echo "Steam が終了しませんでした。起動オプションは手動で設定してください。" ;;
        zh:steam_slow) echo "Steam 没有退出。请手动设置启动选项。" ;;
        *:steam_slow) echo "Steam did not exit. Set the launch option by hand." ;;
        ja:done) echo "完了しました。ゲームモードに戻って、Steam からゲームを起動してください。言語はゲームの Options →「言語（Mod）」でも変えられます。" ;;
        zh:done) echo "完成。请回到游戏模式，从 Steam 启动游戏。也可以在游戏的 Options →「语言（Mod）」中更改语言。" ;;
        *:done) echo "Done. Go back to Gaming Mode and start the game from Steam. You can also change language in the game's Options → Language (Mod)." ;;
        ja:keep) echo "セーブ履歴と翻訳作業ファイルは残しますか？" ;;
        zh:keep) echo "保留存档历史和翻译工作文件吗？" ;;
        *:keep) echo "Keep save history and translation working files?" ;;
        ja:rmbep) echo "BepInEx も削除しますか？（このインストーラーが入れて、他の Mod がない場合のみ）" ;;
        zh:rmbep) echo "同时删除 BepInEx 吗？（仅当由本安装器安装且没有其他 Mod 时）" ;;
        *:rmbep) echo "Also remove BepInEx? (only if this installer put it there and no other mod uses it)" ;;
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
        ja:st_bep) echo "BepInEx" ;;
        zh:st_bep) echo "BepInEx" ;;
        *:st_bep) echo "BepInEx" ;;
        ja:st_mod) echo "Mod" ;;
        zh:st_mod) echo "Mod" ;;
        *:st_mod) echo "Mod" ;;
        ja:st_yes) echo "導入済み" ;;
        zh:st_yes) echo "已安装" ;;
        *:st_yes) echo "installed" ;;
        ja:st_no) echo "未導入" ;;
        zh:st_no) echo "未安装" ;;
        *:st_no) echo "not installed" ;;
        ja:nothing) echo "Mod は導入されていません。" ;;
        zh:nothing) echo "尚未安装 Mod。" ;;
        *:nothing) echo "The mod is not installed." ;;
        *) echo "$key" ;;
    esac
}

# -------------------------------------------------------------- dialogs ----
GUI=0
if [ "$ASSUME_YES" -eq 0 ] && command -v kdialog >/dev/null 2>&1 && { [ -n "${DISPLAY:-}" ] || [ -n "${WAYLAND_DISPLAY:-}" ]; }; then
    GUI=1
fi

say() { echo "$*"; }
fail() {
    echo "ERROR: $*" >&2
    if [ "$GUI" -eq 1 ]; then kdialog --title "$(t title)" --error "$*" >/dev/null 2>&1 || true; fi
    exit 1
}
ask_yes() {  # ask_yes "question" default(1=yes,0=no)
    local q="$1" def="${2:-1}"
    if [ "$ASSUME_YES" -eq 1 ]; then [ "$def" -eq 1 ]; return; fi
    if [ "$GUI" -eq 1 ]; then kdialog --title "$(t title)" --yesno "$q" >/dev/null 2>&1; return; fi
    local hint="[Y/n]"; [ "$def" -eq 0 ] && hint="[y/N]"
    local reply; read -r -p "$q $hint " reply || reply=""
    case "$reply" in
        [Yy]*) return 0 ;;
        [Nn]*) return 1 ;;
        *) [ "$def" -eq 1 ] ;;
    esac
}
finish_message() {
    say "$1"
    if [ "$GUI" -eq 1 ]; then kdialog --title "$(t title)" --msgbox "$1" >/dev/null 2>&1 || true; fi
}

# ------------------------------------------------------------ discovery ----
steam_roots() {
    local r
    for r in "$HOME/.local/share/Steam" "$HOME/.steam/steam" "$HOME/.steam/root"; do
        [ -d "$r/steamapps" ] && readlink -f "$r"
    done | awk '!seen[$0]++'
}

library_paths() {
    local root vdf
    for root in $(steam_roots); do
        echo "$root"
        vdf="$root/steamapps/libraryfolders.vdf"
        [ -f "$vdf" ] && sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf"
    done | awk '!seen[$0]++'
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
        if [ -f "$dir/$GAME_BIN" ]; then echo "$dir"; return 0; fi
    done < <(library_paths)
    return 1
}

steam_running() {
    local root pid
    for root in "$HOME/.steam"; do
        pid="$(cat "$root/steam.pid" 2>/dev/null || true)"
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then return 0; fi
    done
    return 1
}

available_locales() {  # prints "code<TAB>name" lines; English first
    printf 'en\tEnglish\n'
    local d code name
    for d in "$PAYLOAD/Translations"/*/; do
        code="$(basename "$d")"
        case "$code" in _*) continue ;; esac
        name="$code"
        [ -f "$d/name.txt" ] && name="$(head -1 "$d/name.txt" | tr -d '\r')"
        printf '%s\t%s\n' "$code" "$name"
    done
}

# ------------------------------------------------------ launch options ----
# localconfig.vdf: UserLocalConfigStore > Software > Valve > Steam > apps > "<id>".
# Steam rewrites this file when it exits, so it must not be running.
vdf_tool() {  # vdf_tool <file> set|remove|check  (the Python program comes from stdin)
    python3 - "$1" "$APP_ID" "$LAUNCH_OPTION" "$2" <<'PY'
import re, shutil, sys
path, app, option, action = sys.argv[1:5]
text = open(path, encoding="utf-8").read()

def find_block(text, key, start=0, end=None):
    """Return (open_brace_index, close_brace_index) of "key" { ... } within [start, end)."""
    end = len(text) if end is None else end
    for m in re.finditer(r'"%s"\s*\{' % re.escape(key), text[start:end]):
        o = start + m.end() - 1
        depth, i = 0, o
        while i < end:
            c = text[i]
            if c == '"':
                i += 1
                while i < end and text[i] != '"':
                    i += 2 if text[i] == '\\' else 1
            elif c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
                if depth == 0:
                    return o, i
            i += 1
    return None

blk = find_block(text, "Software")
blk = blk and find_block(text, "Valve", blk[0], blk[1])
blk = blk and find_block(text, "Steam", blk[0], blk[1])
blk = blk and find_block(text, "apps", blk[0], blk[1])
if not blk:
    sys.exit(1)
app_blk = find_block(text, app, blk[0], blk[1])

def indent_at(i):
    line_start = text.rfind("\n", 0, i) + 1
    return re.match(r"[\t ]*", text[line_start:]).group(0)

wrapped = option.replace(" %command%", "")   # ./run_bepinex.sh
if action == "check":
    if app_blk is None:
        sys.exit(4)
    body = text[app_blk[0]:app_blk[1]]
    m = re.search(r'"LaunchOptions"\s*"((?:[^"\\]|\\.)*)"', body)
    sys.exit(0 if (m and wrapped in m.group(1)) else 3)
if app_blk is None:
    if action == "remove":
        sys.exit(3)
    ind = indent_at(blk[1]) + "\t"
    insert = f'{ind}"{app}"\n{ind}{{\n{ind}\t"LaunchOptions"\t\t"{option}"\n{ind}}}\n'
    line_start = text.rfind("\n", 0, blk[1]) + 1
    new = text[:line_start] + insert + text[line_start:]
else:
    body = text[app_blk[0]:app_blk[1]]
    m = re.search(r'"LaunchOptions"\s*"((?:[^"\\]|\\.)*)"', body)
    current = m.group(1) if m else ""
    if action == "set":
        if wrapped in current:
            sys.exit(3)
        if "%command%" in current:
            value = current.replace("%command%", option, 1)
        elif current.strip():
            value = option + " " + current.strip()
        else:
            value = option
    else:
        if wrapped not in current:
            sys.exit(3)
        value = current.replace(option, "%command%", 1).replace(wrapped + " ", "", 1).strip()
        if value == "%command%":
            value = ""
    if m:
        s, e = app_blk[0] + m.start(1), app_blk[0] + m.end(1)
        new = text[:s] + value + text[e:]
    else:
        ind = indent_at(app_blk[1]) + "\t"
        line_start = text.rfind("\n", 0, app_blk[1]) + 1
        new = text[:line_start] + f'{ind}"LaunchOptions"\t\t"{value}"\n' + text[line_start:]

shutil.copy2(path, path + ".dragnwash-backup")
open(path, "w", encoding="utf-8").write(new)
sys.exit(0)
PY
}

edit_launch_options() {  # edit_launch_options set|remove ; 0 if any profile changed
    local action="$1" changed=0 vdf rc
    for vdf in "$HOME/.local/share/Steam/userdata"/*/config/localconfig.vdf; do
        [ -f "$vdf" ] || continue
        rc=0; vdf_tool "$vdf" "$action" || rc=$?
        case "$rc" in
            0) changed=1 ;;
            3|4) ;;        # nothing to change in this profile
            *) say "  (could not update $vdf)" ;;
        esac
    done
    [ "$changed" -eq 1 ]
}

launch_options_state() {  # yes when every profile that knows the game has the wrapper
    local vdf rc known=0 missing=0
    for vdf in "$HOME/.local/share/Steam/userdata"/*/config/localconfig.vdf; do
        [ -f "$vdf" ] || continue
        rc=0; vdf_tool "$vdf" check || rc=$?
        case "$rc" in
            0) known=1 ;;
            3) known=1; missing=1 ;;
        esac
    done
    if [ "$known" -eq 1 ] && [ "$missing" -eq 0 ]; then echo yes; else echo no; fi
}

with_steam_closed() {  # with_steam_closed set|remove ; returns 0 if applied
    local action="$1" was_running=0
    if steam_running; then
        ask_yes "$(t lo_ask)" 0 || return 1
        was_running=1
        steam -shutdown >/dev/null 2>&1 || true
        say "$(t steam_wait)"
        local i
        for i in $(seq 1 60); do
            steam_running || break
            sleep 1
        done
        if steam_running; then say "$(t steam_slow)"; return 1; fi
        sleep 2
    fi
    local rc=0
    edit_launch_options "$action" || rc=$?
    if [ "$was_running" -eq 1 ]; then
        (nohup steam >/dev/null 2>&1 &) || true
    fi
    return "$rc"
}

# ------------------------------------------------------------------ main ----
say "== $(t title)"

if [ -z "$GAME_DIR" ]; then GAME_DIR="$(find_game || true)"; fi
[ -n "$GAME_DIR" ] && [ -f "$GAME_DIR/$GAME_BIN" ] || fail "$(t nogame)"
say "Game: $GAME_DIR"
if pgrep -x "$GAME_BIN" >/dev/null 2>&1; then fail "$(t running)"; fi

if [ -z "$MODE" ]; then
    have_bep="$(t st_no)"; [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ] && have_bep="$(t st_yes)"
    have_mod="$(t st_no)"; [ -f "$GAME_DIR/BepInEx/plugins/$PLUGIN/$PLUGIN.dll" ] && have_mod="$(t st_yes)"
    status="$(t st_bep): $have_bep    $(t st_mod): $have_mod"
    if [ "$ASSUME_YES" -eq 1 ]; then
        MODE=install
    elif [ "$GUI" -eq 1 ]; then
        MODE="$(kdialog --title "$(t title)" --menu "$GAME_DIR
$status

$(t action)" install "$(t act_install)" uninstall "$(t act_uninstall)")" || exit 1
    else
        say "$status"
        say "$(t action)"
        say "  1) $(t act_install)"
        say "  2) $(t act_uninstall)"
        read -r -p "> " pick || pick=""
        case "$pick" in
            2) MODE=uninstall ;;
            1|"") MODE=install ;;
            *) exit 1 ;;
        esac
    fi
fi

if [ "$MODE" = install ]; then
    [ -f "$PAYLOAD/$PLUGIN.dll" ] || fail "$(t nopayload)"

    # Language
    if [ -z "$LANG_CHOICE" ]; then
        default="$DEFAULT_LOCALE"
        if [ "$default" != en ] && [ ! -d "$PAYLOAD/Translations/$default" ]; then default=en; fi
        if [ "$ASSUME_YES" -eq 1 ]; then
            LANG_CHOICE="$default"
        elif [ "$GUI" -eq 1 ]; then
            args=()
            while IFS=$'\t' read -r code name; do
                state=off; [ "$code" = "$default" ] && state=on
                args+=("$code" "$name" "$state")
            done < <(available_locales)
            LANG_CHOICE="$(kdialog --title "$(t title)" --radiolist "$(t pick)" "${args[@]}")" || exit 1
        else
            say "$(t pick):"
            mapfile -t rows < <(available_locales)
            for i in "${!rows[@]}"; do
                code="${rows[$i]%%$'\t'*}"; name="${rows[$i]#*$'\t'}"
                mark=" "; [ "$code" = "$default" ] && mark="*"
                printf ' %s %2d) %s (%s)\n' "$mark" "$((i + 1))" "$name" "$code"
            done
            read -r -p "> " pick || pick=""
            if [ -z "$pick" ]; then
                LANG_CHOICE="$default"
            elif [[ "$pick" =~ ^[0-9]+$ ]] && [ "$pick" -ge 1 ] && [ "$pick" -le "${#rows[@]}" ]; then
                LANG_CHOICE="${rows[$((pick - 1))]%%$'\t'*}"
            else
                LANG_CHOICE="$pick"
            fi
        fi
    fi
    if [ "$LANG_CHOICE" != en ] && [ ! -d "$PAYLOAD/Translations/$LANG_CHOICE" ]; then
        fail "Unknown language: $LANG_CHOICE"
    fi

    ask_yes "$(t confirm_install)
$GAME_DIR" 1 || exit 1

    # BepInEx
    if [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]; then
        say "$(t bep_have)"
    else
        say "$(t bep_get)"
        tmp="$(mktemp -d)"
        trap 'rm -rf "$tmp"' EXIT
        curl -fsSL -o "$tmp/bepinex.zip" "$BEPINEX_URL"
        actual="$(sha256sum "$tmp/bepinex.zip" | cut -d' ' -f1)"
        [ "$actual" = "$BEPINEX_SHA256" ] || fail "$(t bep_bad)"
        unzip -oq "$tmp/bepinex.zip" -d "$GAME_DIR"
        : > "$GAME_DIR/BepInEx/$MARKER"
        say "$(t bep_ok)"
    fi

    # run_bepinex.sh
    if [ -f "$GAME_DIR/run_bepinex.sh" ]; then
        sed -i 's/^executable_name=.*/executable_name="'"$GAME_BIN"'"/' "$GAME_DIR/run_bepinex.sh"
        chmod +x "$GAME_DIR/run_bepinex.sh"
        say "run_bepinex.sh: executable_name=\"$GAME_BIN\""
    fi

    # Mod files (SaveHistory and Translations/_discovered are left alone)
    dst="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    mkdir -p "$dst/Translations"
    for f in "$PLUGIN.dll" FlagCatalog.csv dragnwash-menufont.bundle dragnwash-menufont-LICENSE.txt; do
        [ -f "$PAYLOAD/$f" ] && cp -f "$PAYLOAD/$f" "$dst/"
    done
    if [ -d "$PAYLOAD/data" ]; then mkdir -p "$dst/data"; cp -rf "$PAYLOAD/data/." "$dst/data/"; fi
    [ -f "$PAYLOAD/Translations/ignore.txt" ] && cp -f "$PAYLOAD/Translations/ignore.txt" "$dst/Translations/"
    for d in "$PAYLOAD/Translations"/*/; do
        code="$(basename "$d")"
        case "$code" in _*) continue ;; esac
        mkdir -p "$dst/Translations/$code"
        cp -rf "$d." "$dst/Translations/$code/"
    done
    say "$(t mod_ok): $dst"

    # Language in the plugin config
    cfg_dir="$GAME_DIR/BepInEx/config"; cfg="$cfg_dir/$CFG_NAME"
    mkdir -p "$cfg_dir"
    if [ -f "$cfg" ] && grep -q '^TargetLocale[[:space:]]*=' "$cfg"; then
        sed -i "s/^TargetLocale[[:space:]]*=.*/TargetLocale = $LANG_CHOICE/" "$cfg"
    elif [ -f "$cfg" ]; then
        printf '\n[General]\nTargetLocale = %s\n' "$LANG_CHOICE" >> "$cfg"
    else
        printf '[General]\n\nTargetLocale = %s\n' "$LANG_CHOICE" > "$cfg"
    fi
    say "$(t lang_set) $LANG_CHOICE"

    # Steam launch option
    if [ "$(launch_options_state)" = yes ]; then
        say "$(t lo_same)"
    elif with_steam_closed set; then
        say "$(t lo_done) $LAUNCH_OPTION"
    else
        say "$(t lo_manual):"
        say "  $LAUNCH_OPTION"
    fi

    finish_message "$(t done)"
else
    if [ ! -d "$GAME_DIR/BepInEx/plugins/$PLUGIN" ] && [ ! -f "$GAME_DIR/BepInEx/config/$CFG_NAME" ]; then
        finish_message "$(t nothing)"
        exit 0
    fi
    ask_yes "$(t confirm_uninstall)
$GAME_DIR" 1 || exit 1

    dir="$GAME_DIR/BepInEx/plugins/$PLUGIN"
    keep=0
    if [ -d "$dir/SaveHistory" ] || [ -d "$dir/Translations/_discovered" ]; then
        ask_yes "$(t keep)" 1 && keep=1
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

    if [ -f "$GAME_DIR/BepInEx/$MARKER" ]; then
        if [ "$REMOVE_BEPINEX" -eq 1 ] || { [ "$ASSUME_YES" -eq 0 ] && ask_yes "$(t rmbep)" 0; }; then
            others="$(find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" 2>/dev/null | head -1)"
            if [ -n "$others" ]; then
                say "$(t bep_kept)"
            else
                rm -f "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.so" "$GAME_DIR/.doorstop_version"
                if [ -f "$GAME_DIR/changelog.txt" ] && grep -qi 'bepinex\|doorstop' "$GAME_DIR/changelog.txt"; then rm -f "$GAME_DIR/changelog.txt"; fi
                if [ "$keep" -eq 1 ] && [ -d "$dir" ]; then
                    find "$GAME_DIR/BepInEx" -mindepth 1 -maxdepth 1 ! -name plugins -exec rm -rf {} +
                    find "$GAME_DIR/BepInEx/plugins" -mindepth 1 -maxdepth 1 ! -name "$PLUGIN" -exec rm -rf {} +
                else
                    rm -rf "$GAME_DIR/BepInEx"
                fi
                say "$(t bep_removed)"
                if with_steam_closed remove; then say "$(t lo_removed)"; fi
            fi
        fi
    fi
    finish_message "$(t undone)"
fi
