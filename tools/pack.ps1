# Builds the plugin and packages it into a release zip under release/.
#
# The game's reference assemblies (libs/) are NOT committed to this repository,
# so this must run on a machine with Drag'n Wash installed and libs/ populated
# (see the .csproj comment). CI cannot build this project.
#
# Usage:
#   pwsh tools/pack.ps1            # version read from Plugin.cs
#   pwsh tools/pack.ps1 -Version 0.2.0
#
# Output:
#   release/DragNWashLocalization-<version>.zip
#     BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
#     BepInEx/plugins/DragNWashLocalization/Translations/<locale>/strings.csv
#     BepInEx/plugins/DragNWashLocalization/Translations/ignore.txt
#     Install.cmd                <- double-click installer / uninstaller
#     installer/Installer.ps1
#     README.md
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src/DragNWashLocalization/DragNWashLocalization.csproj'
$SrcDir = Split-Path -Parent $Project
$Dll = Join-Path $SrcDir 'bin/Release/DragNWashLocalization.dll'
$OutDir = Join-Path $Root 'release'

# 1. The reference assemblies are copied from the game install and never
#    committed. Fail loudly and early with the list of what is missing.
$Required = @(
    'BepInEx.dll', '0Harmony.dll',
    'UnityEngine.CoreModule.dll', 'UnityEngine.dll',
    'Unity.TextMeshPro.dll', 'UnityEngine.UI.dll',
    'YarnSpinner.dll', 'YarnSpinner.Unity.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll',
    'Unity.InputSystem.dll'
)
$Missing = $Required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $SrcDir "libs/$_")) }
if ($Missing) {
    throw "Missing reference assemblies in src/DragNWashLocalization/libs/: $($Missing -join ', '). Copy them from your game install (see the .csproj comment)."
}

# 2. Build.
Write-Host "Building $Project ..."
dotnet build $Project -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

if (-not (Test-Path -LiteralPath $Dll)) {
    throw "Build succeeded but $Dll was not produced."
}

# 3. Version: explicit argument wins, otherwise read PluginVersion from Plugin.cs.
if (-not $Version) {
    $pluginCs = Get-Content -LiteralPath (Join-Path $SrcDir 'Plugin.cs') -Raw
    if ($pluginCs -match 'PluginVersion\s*=\s*"([^"]+)"') {
        $Version = $Matches[1]
    }
    else {
        throw 'Could not read PluginVersion from Plugin.cs. Pass -Version explicitly.'
    }
}

# 4. Stage the files under the same layout the game expects, so extracting the
#    zip into the game root is all the user has to do.
$Stage = Join-Path $OutDir "DragNWashLocalization-$Version"
if (Test-Path -LiteralPath $Stage) {
    Remove-Item -LiteralPath $Stage -Recurse -Force
}

$PluginDir = Join-Path $Stage 'BepInEx/plugins/DragNWashLocalization'
$TranslationsDir = Join-Path $PluginDir 'Translations'
New-Item -ItemType Directory -Force -Path $TranslationsDir | Out-Null

Copy-Item -LiteralPath $Dll -Destination $PluginDir

# Locale folders and ignore.txt only; never the runtime _discovered/ output
# (it contains the game's own text).
$SrcTranslations = Join-Path $Root 'Translations'
Copy-Item -LiteralPath (Join-Path $SrcTranslations 'ignore.txt') -Destination $TranslationsDir
Get-ChildItem -LiteralPath $SrcTranslations -Directory |
    Where-Object { $_.Name -notlike '_*' } |
    ForEach-Object {
        $dest = Join-Path $TranslationsDir $_.Name
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        # Only the published file ships.
        Copy-Item -LiteralPath (Join-Path $_.FullName 'strings.csv') -Destination $dest
    }

Copy-Item -LiteralPath (Join-Path $Root 'README.md') -Destination $Stage

# The one-click installer: Install.cmd at the zip root, script beside the payload.
New-Item -ItemType Directory -Force -Path (Join-Path $Stage 'installer') | Out-Null
Copy-Item -LiteralPath (Join-Path $Root 'installer/Installer.ps1') -Destination (Join-Path $Stage 'installer')
Copy-Item -LiteralPath (Join-Path $Root 'installer/Install.cmd') -Destination $Stage

# 5. Zip the stage contents (so the zip root holds BepInEx/ and README.md).
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Zip = Join-Path $OutDir "DragNWashLocalization-$Version.zip"
if (Test-Path -LiteralPath $Zip) {
    Remove-Item -LiteralPath $Zip -Force
}

Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Zip

Write-Host ''
Write-Host "Created $Zip"
Write-Host 'Install: extract anywhere and double-click Install.cmd (or merge BepInEx/ into the game folder by hand).'
