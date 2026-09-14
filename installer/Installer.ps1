# Installer / uninstaller for the Drag'n Wash localization mod.
#
# Double-click Install.exe (next to this folder) for the window. The same
# script also works from a terminal without the window, which is how it is
# tested:
#
#   powershell -ExecutionPolicy Bypass -File installer\Installer.ps1 -Action install -Locale ja
#   powershell -ExecutionPolicy Bypass -File installer\Installer.ps1 -Action uninstall -GamePath "D:\Games\Drag'n Wash"
#
# What install does:
#   1. finds the game through Steam (or asks for the folder)
#   2. downloads BepInEx 5.4.23.5 (x64) and unpacks it into the game folder if
#      it is not there yet; the download is checked against a pinned SHA-256
#   3. copies Drag'n Wash ModFramework and the libraries the mod uses
#      (..\BepInEx\plugins\DragNWash.ModFramework*, ..\BepInEx\patchers), unless
#      the same or a newer version is already installed
#   4. copies the plugin and the translation files from the payload that ships
#      in the same zip as this script (..\BepInEx\plugins\DragNWashLocalization)
#   5. writes the chosen language into the plugin's config file
# Uninstall removes the plugin folder, the framework when no other mod is
# installed, and, when the installer was the one that added BepInEx, BepInEx
# itself. Save-history snapshots are kept unless asked.
[CmdletBinding()]
param(
    [ValidateSet('install', 'uninstall')]
    [string]$Action,
    [string]$GamePath,
    [string]$Locale = 'ja',
    [switch]$RemoveBepInEx,
    [switch]$RemoveEverything
)

$ErrorActionPreference = 'Stop'

$SteamAppId = '4739660'
$GameExe = 'DragNWash.exe'
$PluginFolderName = 'DragNWashLocalization'
$PluginConfigName = 'com.tomxv.dragnwash.localization.cfg'
$BepInExUrl = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip'
$BepInExSha256 = '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
$MarkerName = '.bepinex-installed-by-dragnwash-localization'
$Payload = Join-Path (Split-Path -Parent $PSScriptRoot) "BepInEx\plugins\$PluginFolderName"
$PayloadBepInEx = Join-Path (Split-Path -Parent $PSScriptRoot) 'BepInEx'
$FrameworkPrefix = 'DragNWash.ModFramework'
$FrameworkPatcher = 'DragNWash.ModFramework.Preloader.dll'

# ---------------------------------------------------------------- strings --
$Lang = 'en'
$ui = (Get-Culture).Name
if ($ui -like 'ja*') { $Lang = 'ja' } elseif ($ui -like 'zh*') { $Lang = 'zh' }
$Strings = @{
    en = @{
        title = "Drag'n Wash Localization"; folder = 'Game folder'; browse = 'Browse...'
        language = 'Language'; install = 'Install / Update'; uninstall = 'Uninstall'
        keepSaves = 'Keep save history and translation working files'; alsoBepInEx = 'Also remove BepInEx'
        notFound = 'Game not found. Pick the folder that contains DragNWash.exe.'
        running = 'Close the game first.'; noPayload = 'Plugin files are missing next to the installer. Extract the whole zip first.'
        stBep = 'BepInEx'; stMod = 'Mod'; yes = 'installed'; no = 'not installed'
        done = 'Done. Start the game from Steam.'; undone = 'Done. The mod has been removed.'
        pickFolder = "Select the Drag'n Wash folder"
        uiLang = 'Installer language'; about = 'About'
        aboutTitle = 'About this installer'
        aboutBody = @'
Drag'n Wash Localization {0}

An unofficial fan-made multilingual localization mod.
Not affiliated with the developers of Drag'n Wash.

Created by TomXV. All translation files by TomXV.
github.com/TomXV/dragnwash-localization

Languages
- Supervised by the author: Japanese, Simplified Chinese
- Converted from the supervised Simplified Chinese:
  Traditional Chinese
- Provisional (not reviewed by native speakers): German,
  French, Spanish, Brazilian Portuguese, Korean, Russian,
  Polish, Hebrew
- Just for fun: Esperanto, Toki Pona

Provisional lines may read unnaturally. Corrections from
native speakers are very welcome as pull requests.

The mod is MIT licensed. The bundled menu font is Noto Sans JP
under the SIL Open Font License 1.1.
'@
    }
    ja = @{
        title = "Drag'n Wash Localization"; folder = 'ゲームフォルダ'; browse = '参照...'
        language = '言語'; install = 'インストール / 更新'; uninstall = 'アンインストール'
        keepSaves = 'セーブ履歴と翻訳作業ファイルは残す'; alsoBepInEx = 'BepInEx も削除する'
        notFound = 'ゲームが見つかりません。DragNWash.exe のあるフォルダを選んでください。'
        running = '先にゲームを終了してください。'; noPayload = 'インストーラーの隣にプラグインのファイルがありません。zip を丸ごと展開してから実行してください。'
        stBep = 'BepInEx'; stMod = 'Mod'; yes = '導入済み'; no = '未導入'
        done = '完了しました。Steam からゲームを起動してください。'; undone = '完了しました。Mod を削除しました。'
        pickFolder = "Drag'n Wash のフォルダを選択"
        uiLang = 'インストーラーの言語'; about = 'Mod情報'
        aboutTitle = 'このModについて'
        aboutBody = @'
Drag'n Wash Localization {0}

非公式のファンメイド多言語翻訳Modです。
ゲームの開発元とは関係ありません。

制作: TomXV（翻訳ファイルもすべて TomXV が作成）
github.com/TomXV/dragnwash-localization

対応言語
- 作者が監修: 日本語、簡体字中国語
- 監修済みの簡体字版から変換: 繁体字中国語
- 仮翻訳（ネイティブ監修なし）: ドイツ語、フランス語、スペイン語、
  ブラジルポルトガル語、韓国語、ロシア語、ポーランド語、ヘブライ語
- 面白枠: エスペラント、トキポナ

仮翻訳には不自然な言い回しがあり得ます。
ネイティブの方からの修正 PR を歓迎します。

ModのライセンスはMITです。同梱のメニュー用フォントは
Noto Sans JP（SIL Open Font License 1.1）です。
'@
    }
    zh = @{
        title = "Drag'n Wash Localization"; folder = '游戏文件夹'; browse = '浏览...'
        language = '语言'; install = '安装 / 更新'; uninstall = '卸载'
        keepSaves = '保留存档历史和翻译工作文件'; alsoBepInEx = '同时删除 BepInEx'
        notFound = '未找到游戏。请选择包含 DragNWash.exe 的文件夹。'
        running = '请先关闭游戏。'; noPayload = '安装器旁边缺少插件文件。请先完整解压 zip。'
        stBep = 'BepInEx'; stMod = 'Mod'; yes = '已安装'; no = '未安装'
        done = '完成。请从 Steam 启动游戏。'; undone = '完成。Mod 已删除。'
        pickFolder = "选择 Drag'n Wash 文件夹"
        uiLang = '安装器语言'; about = '关于'
        aboutTitle = '关于本Mod'
        aboutBody = @'
Drag'n Wash Localization {0}

非官方的爱好者制作的多语言本地化Mod。
与游戏开发者无关。

制作: TomXV（翻译文件均由 TomXV 制作）
github.com/TomXV/dragnwash-localization

支持的语言
- 作者审校: 日语、简体中文
- 由审校过的简体中文转换: 繁体中文
- 暂定翻译（未经母语者审校）: 德语、法语、西班牙语、
  巴西葡萄牙语、韩语、俄语、波兰语、希伯来语
- 趣味: 世界语、道本语（Toki Pona）

暂定翻译可能有不自然的表达。
欢迎母语者通过 Pull Request 提交修正。

Mod采用MIT许可证。随附的菜单字体为
Noto Sans JP（SIL Open Font License 1.1）。
'@
    }
}
$T = $Strings[$Lang]

# ------------------------------------------------------------------ helpers --
$script:LogSink = { param($m) Write-Host $m }
function Log([string]$m) { & $script:LogSink $m }

function Find-GameFolder {
    $steam = $null
    foreach ($k in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        try { $v = (Get-ItemProperty -Path $k -ErrorAction Stop).SteamPath; if ($v) { $steam = $v; break } } catch {}
    }
    if (-not $steam) { return $null }
    $libs = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($line in Get-Content -LiteralPath $vdf) {
            if ($line -match '^\s*"path"\s+"(.+)"\s*$') { $libs += ($Matches[1] -replace '\\\\', '\') }
        }
    }
    foreach ($lib in $libs | Select-Object -Unique) {
        $acf = Join-Path $lib "steamapps\appmanifest_$SteamAppId.acf"
        if (-not (Test-Path -LiteralPath $acf)) { continue }
        $dir = (Get-Content -LiteralPath $acf | Where-Object { $_ -match '^\s*"installdir"\s+"(.+)"' } | ForEach-Object { $Matches[1] } | Select-Object -First 1)
        if ($dir) {
            $full = Join-Path $lib "steamapps\common\$dir"
            if (Test-Path -LiteralPath (Join-Path $full $GameExe)) { return $full }
        }
    }
    return $null
}

function Get-AvailableLocales {
    $list = @()
    $t = Join-Path $Payload 'Translations'
    if (Test-Path -LiteralPath $t) {
        foreach ($d in Get-ChildItem -LiteralPath $t -Directory | Where-Object { $_.Name -notlike '_*' } | Sort-Object Name) {
            $name = $d.Name
            $nf = Join-Path $d.FullName 'name.txt'
            if (Test-Path -LiteralPath $nf) { $txt = ([IO.File]::ReadAllText($nf, [Text.Encoding]::UTF8)).Trim(); if ($txt) { $name = $txt } }
            $list += [pscustomobject]@{ Code = $d.Name; Name = $name }
        }
    }
    $list += [pscustomobject]@{ Code = 'en'; Name = 'English' }
    return $list
}

function Test-GameFolder([string]$p) { $p -and (Test-Path -LiteralPath (Join-Path $p $GameExe)) }
function Test-GameRunning { [bool](Get-Process -Name 'DragNWash' -ErrorAction SilentlyContinue) }
function Test-BepInEx([string]$g) { Test-Path -LiteralPath (Join-Path $g 'BepInEx\core\BepInEx.dll') }
function Get-PluginDir([string]$g) { Join-Path $g "BepInEx\plugins\$PluginFolderName" }
function Get-PayloadVersion {
    $dll = Join-Path $Payload "$PluginFolderName.dll"
    if (-not (Test-Path -LiteralPath $dll)) { return $null }
    $v = (Get-Item -LiteralPath $dll).VersionInfo.FileVersion
    if ($v) { $v } else { $null }
}

function Get-InstalledVersion([string]$g) {
    $dll = Join-Path (Get-PluginDir $g) "$PluginFolderName.dll"
    if (-not (Test-Path -LiteralPath $dll)) { return $null }
    $v = (Get-Item -LiteralPath $dll).VersionInfo.FileVersion
    if ($v) { $v } else { '?' }
}

function Install-BepInEx([string]$g) {
    if (Test-BepInEx $g) { Log "BepInEx: already present"; return }
    $tmp = Join-Path ([IO.Path]::GetTempPath()) 'BepInEx_win_x64_5.4.23.5.zip'
    Log "BepInEx: downloading $BepInExUrl"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $BepInExUrl -OutFile $tmp -UseBasicParsing
    $hash = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $BepInExSha256) { Remove-Item -LiteralPath $tmp -Force; throw "BepInEx download hash mismatch ($hash)" }
    Log "BepInEx: hash OK, unpacking"
    Expand-Archive -LiteralPath $tmp -DestinationPath $g -Force
    Remove-Item -LiteralPath $tmp -Force
    New-Item -ItemType File -Force -Path (Join-Path $g "BepInEx\$MarkerName") | Out-Null
    Log "BepInEx: installed"
}

function Get-DllVersion([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $v = (Get-Item -LiteralPath $path).VersionInfo.FileVersion
    try { return [version]$v } catch { return [version]'0.0' }
}

# A mod file the player switched off on the Mods screen is renamed to
# .dll.disabled by the framework's patcher. Installing means wanting it on.
function Enable-ModFile([string]$g, [string]$rel) {
    $off = Join-Path $g ("BepInEx\plugins\" + ($rel -replace '/', '\') + '.disabled')
    if (Test-Path -LiteralPath $off) { Remove-Item -LiteralPath $off -Force }
    $utf8 = New-Object Text.UTF8Encoding($false)
    foreach ($name in 'com.tomxv.dragnwash.modframework.disabled.txt', 'com.tomxv.dragnwash.modframework.state.txt') {
        $list = Join-Path $g "BepInEx\config\$name"
        if (-not (Test-Path -LiteralPath $list)) { continue }
        $lines = @([IO.File]::ReadAllLines($list, $utf8) | Where-Object { ($_ -split "`t")[0].Trim().Replace('\', '/') -ne $rel })
        [IO.File]::WriteAllLines($list, [string[]]$lines, $utf8)
    }
}

# The framework and its libraries, each in its own plugins folder. Another mod
# may have installed a newer version already; never replace it with an older one.
function Install-Framework([string]$g) {
    $src = Join-Path $PayloadBepInEx 'plugins'
    $folders = @(Get-ChildItem -LiteralPath $src -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$FrameworkPrefix*" })
    if ($folders.Count -eq 0) { throw $T.noPayload }
    foreach ($folder in $folders) {
        $dll = Join-Path $folder.FullName "$($folder.Name).dll"
        $dst = Join-Path $g "BepInEx\plugins\$($folder.Name)"
        $have = Get-DllVersion (Join-Path $dst "$($folder.Name).dll")
        $offered = Get-DllVersion $dll
        if ($have -and $offered -and $have -gt $offered) {
            Log "$($folder.Name): kept $have (newer than $offered)"
            continue
        }
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item -Path (Join-Path $folder.FullName '*') -Destination $dst -Recurse -Force
        Enable-ModFile $g "$($folder.Name)/$($folder.Name).dll"
        Log "$($folder.Name): $offered"
    }
    $patcher = Join-Path $PayloadBepInEx "patchers\$FrameworkPatcher"
    if (Test-Path -LiteralPath $patcher) {
        $dst = Join-Path $g 'BepInEx\patchers'
        $have = Get-DllVersion (Join-Path $dst $FrameworkPatcher)
        $offered = Get-DllVersion $patcher
        if (-not ($have -and $offered -and $have -gt $offered)) {
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            Copy-Item -LiteralPath $patcher -Destination $dst -Force
        }
    }
}

# Removed only when nothing else is installed that could be using it.
function Uninstall-Framework([string]$g) {
    $plugins = Join-Path $g 'BepInEx\plugins'
    $framework = @(Get-ChildItem -LiteralPath $plugins -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$FrameworkPrefix*" })
    if ($framework.Count -eq 0) { return }
    $others = @(Get-ChildItem -LiteralPath $plugins -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $PluginFolderName -and $_.Name -notlike "$FrameworkPrefix*" })
    if ($others.Count -gt 0) {
        Log "ModFramework: kept, other mods are installed ($($others.Name -join ', '))"
        return
    }
    $framework | Remove-Item -Recurse -Force
    $patcher = Join-Path $g "BepInEx\patchers\$FrameworkPatcher"
    if (Test-Path -LiteralPath $patcher) { Remove-Item -LiteralPath $patcher -Force }
    Get-ChildItem -LiteralPath (Join-Path $g 'BepInEx\config') -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'com.tomxv.dragnwash.modframework*' } | Remove-Item -Force
    Log 'ModFramework: removed'
}

function Install-Plugin([string]$g, [string]$loc) {
    if (-not (Test-Path -LiteralPath (Join-Path $Payload "$PluginFolderName.dll"))) { throw $T.noPayload }
    $dst = Get-PluginDir $g
    New-Item -ItemType Directory -Force -Path (Join-Path $dst 'Translations') | Out-Null
    Copy-Item -LiteralPath (Join-Path $Payload "$PluginFolderName.dll") -Destination $dst -Force
    $cat = Join-Path $Payload 'FlagCatalog.csv'
    if (Test-Path -LiteralPath $cat) { Copy-Item -LiteralPath $cat -Destination $dst -Force }
    $fontBundle = Join-Path $Payload 'dragnwash-menufont.bundle'
    if (Test-Path -LiteralPath $fontBundle) { Copy-Item -LiteralPath $fontBundle -Destination $dst -Force }
    $fontLicense = Join-Path $Payload 'dragnwash-menufont-LICENSE.txt'
    if (Test-Path -LiteralPath $fontLicense) { Copy-Item -LiteralPath $fontLicense -Destination $dst -Force }
    $dataSrc = Join-Path $Payload 'data'
    if (Test-Path -LiteralPath $dataSrc) { New-Item -ItemType Directory -Force -Path (Join-Path $dst 'data') | Out-Null; Copy-Item -Path (Join-Path $dataSrc '*') -Destination (Join-Path $dst 'data') -Force }
    $srcT = Join-Path $Payload 'Translations'
    Copy-Item -LiteralPath (Join-Path $srcT 'ignore.txt') -Destination (Join-Path $dst 'Translations') -Force
    Get-ChildItem -LiteralPath $srcT -Directory | ForEach-Object {
        $d = Join-Path $dst "Translations\$($_.Name)"
        New-Item -ItemType Directory -Force -Path $d | Out-Null
        Copy-Item -Path (Join-Path $_.FullName '*') -Destination $d -Recurse -Force
    }
    Enable-ModFile $g "$PluginFolderName/$PluginFolderName.dll"
    Log "Mod: files copied to $dst"
    Set-PluginLocale $g $loc
}

function Set-PluginLocale([string]$g, [string]$loc) {
    $cfgDir = Join-Path $g 'BepInEx\config'
    New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
    $cfg = Join-Path $cfgDir $PluginConfigName
    $utf8 = New-Object Text.UTF8Encoding($false)
    if (Test-Path -LiteralPath $cfg) {
        $text = [IO.File]::ReadAllText($cfg, $utf8)
        if ($text -match '(?m)^TargetLocale\s*=') {
            $text = [regex]::Replace($text, '(?m)^TargetLocale\s*=.*$', "TargetLocale = $loc")
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[General]`r`nTargetLocale = $loc`r`n"
        }
        [IO.File]::WriteAllText($cfg, $text, $utf8)
    } else {
        [IO.File]::WriteAllText($cfg, "[General]`r`n`r`nTargetLocale = $loc`r`n", $utf8)
    }
    Log "Mod: language set to $loc"
}

function Uninstall-Plugin([string]$g, [bool]$keepSaves, [bool]$removeBep) {
    $dir = Get-PluginDir $g
    $keep = @()
    if (Test-Path -LiteralPath $dir) {
        if ($keepSaves) {
            if (Test-Path -LiteralPath (Join-Path $dir 'SaveHistory')) { $keep += 'SaveHistory' }
            if (Test-Path -LiteralPath (Join-Path $dir 'Translations\_discovered')) { $keep += 'Translations\_discovered' }
        }
        if ($keep.Count -gt 0) {
            # Keep the user's own data: save snapshots and translator working files.
            Get-ChildItem -LiteralPath $dir -Force | Where-Object { $_.Name -ne 'SaveHistory' -and $_.Name -ne 'Translations' } |
                Remove-Item -Recurse -Force
            $t = Join-Path $dir 'Translations'
            if (Test-Path -LiteralPath $t) {
                Get-ChildItem -LiteralPath $t -Force | Where-Object { $_.Name -ne '_discovered' } | Remove-Item -Recurse -Force
                if (-not (Test-Path -LiteralPath (Join-Path $t '_discovered'))) { Remove-Item -LiteralPath $t -Recurse -Force }
            }
            Log "Mod: removed (kept: $($keep -join ', ') in $dir)"
        } else {
            Remove-Item -LiteralPath $dir -Recurse -Force
            Log "Mod: removed"
        }
    } else { Log "Mod: not installed" }
    $cfg = Join-Path $g "BepInEx\config\$PluginConfigName"
    if (Test-Path -LiteralPath $cfg) { Remove-Item -LiteralPath $cfg -Force }
    Uninstall-Framework $g
    # Save snapshots taken by the framework's saves library.
    $history = Join-Path $g 'BepInEx\SaveHistory'
    if (Test-Path -LiteralPath $history) {
        if ($keepSaves) { $keep += 'BepInEx\SaveHistory' } else { Remove-Item -LiteralPath $history -Recurse -Force }
    }
    if ($removeBep -and (Test-BepInEx $g)) {
        $others = @(Get-ChildItem -LiteralPath (Join-Path $g 'BepInEx\plugins') -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne $PluginFolderName })
        if ($others.Count -gt 0) {
            Log "BepInEx: kept, other plugins are installed ($($others.Name -join ', '))"
        } else {
            foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') {
                $p = Join-Path $g $f; if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
            }
            $cl = Join-Path $g 'changelog.txt'
            if ((Test-Path -LiteralPath $cl) -and ((Get-Content -LiteralPath $cl -Raw) -match 'BepInEx|Doorstop|commits since v5')) { Remove-Item -LiteralPath $cl -Force }
            $bep = Join-Path $g 'BepInEx'
            if ($keep.Count -gt 0) {
                # The user's data lives inside BepInEx/plugins/<mod>/; take BepInEx
                # apart around it instead of deleting the whole tree.
                Get-ChildItem -LiteralPath $bep -Force | Where-Object { $_.Name -ne 'plugins' -and $_.Name -ne 'SaveHistory' } | Remove-Item -Recurse -Force -ErrorAction Continue
                $plugins = Join-Path $bep 'plugins'
                if (Test-Path -LiteralPath $plugins) {
                    Get-ChildItem -LiteralPath $plugins -Force | Where-Object { $_.Name -ne $PluginFolderName } | Remove-Item -Recurse -Force -ErrorAction Continue
                }
                Log "BepInEx: removed (your data stays: $($keep -join ', '))"
            } else {
                Remove-Item -LiteralPath $bep -Recurse -Force -ErrorAction Continue
                Log "BepInEx: removed"
            }
        }
    }
}

function Invoke-Install([string]$g, [string]$loc) {
    if (-not (Test-GameFolder $g)) { throw $T.notFound }
    if (Test-GameRunning) { throw $T.running }
    Install-BepInEx $g
    Install-Framework $g
    Install-Plugin $g $loc
    Log $T.done
}

function Invoke-Uninstall([string]$g, [bool]$keepSaves, [bool]$removeBep) {
    if (-not (Test-GameFolder $g)) { throw $T.notFound }
    if (Test-GameRunning) { throw $T.running }
    Uninstall-Plugin $g $keepSaves $removeBep
    Log $T.undone
}

# --------------------------------------------------------------- CLI mode --
if ($Action) {
    if (-not $GamePath) { $GamePath = Find-GameFolder }
    if (-not $GamePath) { throw $T.notFound }
    Log "Game: $GamePath"
    if ($Action -eq 'install') { Invoke-Install $GamePath $Locale }
    else { Invoke-Uninstall $GamePath (-not $RemoveEverything) ($RemoveBepInEx -or $RemoveEverything) }
    exit 0
}

# --------------------------------------------------------------- GUI mode --
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object Windows.Forms.Form
$form.Text = $T.title
$form.FormBorderStyle = 'FixedDialog'; $form.MaximizeBox = $false; $form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI', 9)

$lblFolder = New-Object Windows.Forms.Label; $lblFolder.Text = $T.folder; $lblFolder.Location = '12,14'; $lblFolder.AutoSize = $true
$txtFolder = New-Object Windows.Forms.TextBox; $txtFolder.Location = '12,34'; $txtFolder.Width = 410
$btnBrowse = New-Object Windows.Forms.Button; $btnBrowse.Text = $T.browse; $btnBrowse.Location = '428,32'; $btnBrowse.Width = 80

# Installer language, separate from the language being installed.
$lblUi = New-Object Windows.Forms.Label; $lblUi.Text = $T.uiLang; $lblUi.Location = '286,14'; $lblUi.AutoSize = $true
$cmbUi = New-Object Windows.Forms.ComboBox; $cmbUi.Location = '408,10'; $cmbUi.Width = 100
$cmbUi.DropDownStyle = 'DropDownList'
$UiLangs = @('en', 'ja', 'zh')
[void]$cmbUi.Items.AddRange(@('English', '日本語', '中文'))
$cmbUi.SelectedIndex = [Math]::Max(0, [Array]::IndexOf($UiLangs, $Lang))

$lblStatus = New-Object Windows.Forms.Label; $lblStatus.Location = '12,64'; $lblStatus.AutoSize = $true

# One radio per shipped locale (name from Translations/<locale>/name.txt), plus
# English = off. There are more than a dozen now, so they wrap into rows.
$grpLang = New-Object Windows.Forms.GroupBox; $grpLang.Text = $T.language; $grpLang.Location = '12,92'
$locales = @(Get-AvailableLocales)
$widest = 0
foreach ($loc in $locales) {
    $wpx = [Windows.Forms.TextRenderer]::MeasureText($loc.Name, $form.Font).Width
    if ($wpx -gt $widest) { $widest = $wpx }
}
$colWidth = [Math]::Max(110, $widest + 34)
$perRow = [Math]::Max(2, [Math]::Min(4, [Math]::Floor(472 / $colWidth)))
$rowCount = [Math]::Max(1, [Math]::Ceiling($locales.Count / $perRow))
$grpLang.Size = New-Object Drawing.Size(496, (24 + $rowCount * 24))
$radios = @()
$index = 0
foreach ($loc in $locales) {
    $rb = New-Object Windows.Forms.RadioButton
    $rb.Text = $loc.Name; $rb.Tag = $loc.Code; $rb.AutoSize = $true
    $rb.Location = New-Object Drawing.Point((12 + ($index % $perRow) * $colWidth), (18 + [Math]::Floor($index / $perRow) * 24))
    $grpLang.Controls.Add($rb); $radios += $rb
    $index++
}
$preferred = if ($Lang -eq 'zh') { 'zh-Hans' } elseif ($Lang -eq 'ja') { 'ja' } else { 'en' }
$pick = $radios | Where-Object { $_.Tag -eq $preferred } | Select-Object -First 1
if (-not $pick) { $pick = $radios[0] }
$pick.Checked = $true

$rowY = 92 + $grpLang.Height + 12
$btnInstall = New-Object Windows.Forms.Button; $btnInstall.Text = $T.install; $btnInstall.Location = New-Object Drawing.Point(12, $rowY); $btnInstall.Size = '200,36'
$btnUninstall = New-Object Windows.Forms.Button; $btnUninstall.Text = $T.uninstall; $btnUninstall.Location = New-Object Drawing.Point(308, $rowY); $btnUninstall.Size = '200,36'
$btnAbout = New-Object Windows.Forms.Button; $btnAbout.Text = $T.about; $btnAbout.Location = New-Object Drawing.Point(220, $rowY); $btnAbout.Size = '80,36'
$chkKeep = New-Object Windows.Forms.CheckBox; $chkKeep.Text = $T.keepSaves; $chkKeep.Location = New-Object Drawing.Point(308, ($rowY + 42)); $chkKeep.AutoSize = $true; $chkKeep.Checked = $true
$chkBep = New-Object Windows.Forms.CheckBox; $chkBep.Text = $T.alsoBepInEx; $chkBep.Location = New-Object Drawing.Point(308, ($rowY + 64)); $chkBep.AutoSize = $true

$logY = $rowY + 92
$txtLog = New-Object Windows.Forms.TextBox; $txtLog.Location = New-Object Drawing.Point(12, $logY); $txtLog.Size = New-Object Drawing.Size(496, 142)
$txtLog.Multiline = $true; $txtLog.ReadOnly = $true; $txtLog.ScrollBars = 'Vertical'; $txtLog.BackColor = 'White'

$form.ClientSize = New-Object Drawing.Size(520, ($logY + 142 + 12))
$form.Controls.AddRange(@($lblFolder, $txtFolder, $btnBrowse, $lblUi, $cmbUi, $lblStatus, $grpLang, $btnInstall, $btnAbout, $btnUninstall, $chkKeep, $chkBep, $txtLog))

$script:LogSink = {
    param($m)
    $txtLog.AppendText("$m`r`n")
    [Windows.Forms.Application]::DoEvents()
}

function Refresh-Status {
    $g = $txtFolder.Text
    $ok = Test-GameFolder $g
    $bep = $ok -and (Test-BepInEx $g)
    $ver = if ($ok) { Get-InstalledVersion $g } else { $null }
    $modText = if ($ver) { "$($T.yes) (v$ver)" } else { $T.no }
    $lblStatus.Text = "$($T.stBep): $(if ($bep) { $T.yes } else { $T.no })    $($T.stMod): $modText"
    $btnInstall.Enabled = $ok
    $btnUninstall.Enabled = $ok -and ($ver -or $bep)
    $chkBep.Checked = $bep -and (Test-Path -LiteralPath (Join-Path $g "BepInEx\$MarkerName"))
}

function Show-About {
    $ver = Get-PayloadVersion
    $body = [string]::Format($T.aboutBody, $(if ($ver) { "v$ver" } else { '' })).Trim()
    [Windows.Forms.MessageBox]::Show($form, $body, $T.aboutTitle, 'OK', 'Information') | Out-Null
}

function Update-Texts {
    $form.Text = $T.title
    $lblFolder.Text = $T.folder
    $btnBrowse.Text = $T.browse
    $lblUi.Text = $T.uiLang
    $grpLang.Text = $T.language
    $btnInstall.Text = $T.install
    $btnUninstall.Text = $T.uninstall
    $btnAbout.Text = $T.about
    $chkKeep.Text = $T.keepSaves
    $chkBep.Text = $T.alsoBepInEx
    Refresh-Status
}

$cmbUi.Add_SelectedIndexChanged({
    $script:Lang = $UiLangs[$cmbUi.SelectedIndex]
    $script:T = $Strings[$script:Lang]
    Update-Texts
})
$btnAbout.Add_Click({ Show-About })
$txtFolder.Add_TextChanged({ Refresh-Status })
$btnBrowse.Add_Click({
    $dlg = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = $T.pickFolder
    if ($txtFolder.Text) { $dlg.SelectedPath = $txtFolder.Text }
    if ($dlg.ShowDialog($form) -eq 'OK') { $txtFolder.Text = $dlg.SelectedPath }
})

function Run-Guarded([scriptblock]$work) {
    $form.Cursor = 'WaitCursor'
    foreach ($c in $btnInstall, $btnUninstall, $btnBrowse, $txtFolder) { $c.Enabled = $false }
    try { & $work }
    catch {
        Log "ERROR: $($_.Exception.Message)"
        [Windows.Forms.MessageBox]::Show($form, $_.Exception.Message, $T.title, 'OK', 'Error') | Out-Null
    }
    finally {
        $form.Cursor = 'Default'
        foreach ($c in $btnBrowse, $txtFolder) { $c.Enabled = $true }
        Refresh-Status
    }
}

$btnInstall.Add_Click({
    Run-Guarded { Invoke-Install $txtFolder.Text ($radios | Where-Object Checked | Select-Object -First 1).Tag }
})
$btnUninstall.Add_Click({
    Run-Guarded { Invoke-Uninstall $txtFolder.Text $chkKeep.Checked $chkBep.Checked }
})

$found = if ($GamePath) { $GamePath } else { Find-GameFolder }
if ($found) { $txtFolder.Text = $found } else { Log $T.notFound }
Refresh-Status
[void]$form.ShowDialog()
