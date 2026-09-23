# Builds the plugin and packages it into a release zip under release/.
#
# The game's reference assemblies (libs/) are NOT committed to this repository,
# so this must run on a machine with Drag'n Wash installed and libs/ populated
# (see the .csproj comment), or in the Build workflow, which fetches them from a
# private repository.
#
# The mod runs on Drag'n Wash ModFramework, which is NOT in this zip. The mod is
# built against one published framework release, the version in
# framework-version.txt, and the zip only names it (mod-install.json's
# "framework" block): Install.exe and install-steamdeck.sh fetch that release
# from GitHub when the game folder does not have it yet, and a manual install
# gets it from the framework's Releases page. The installers themselves are
# taken unchanged from that release's installer/ folder.
#
# Usage:
#   pwsh tools/pack.ps1            # version read from Plugin.cs; the framework zip
#                                  # downloaded once into .cache/framework/
#   pwsh tools/pack.ps1 -Version 0.2.0
#   pwsh tools/pack.ps1 -FrameworkZip D:\Downloads\DragNWash.ModFramework-1.4.3.zip
#
# A SHA256SUMS file next to the framework zip, when there is one, is checked.
# The Build workflow always has one: it refuses a framework release without it.
#
# Output:
#   release/DragNWashLocalization-<version>.zip
#     BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll, icon.png
#     BepInEx/plugins/DragNWashLocalization/FlagCatalog.csv, CREDITS.txt
#     BepInEx/plugins/DragNWashLocalization/dragnwash-menufont.bundle
#     BepInEx/plugins/DragNWashLocalization/dragnwash-menufont-LICENSE.txt
#     BepInEx/plugins/DragNWashLocalization/data/script_order.csv, level_flow.csv
#     BepInEx/plugins/DragNWashLocalization/Translations/<locale>/strings.csv, name.txt, credits.txt
#     BepInEx/plugins/DragNWashLocalization/Translations/ignore.txt
#     Install.exe                <- Drag'n Wash ModFramework's shared installer (Windows)
#     install-steamdeck.sh       <- the same for Steam Deck / Linux: bash install-steamdeck.sh
#     mod-install.json           <- what the installers need to know about this mod (schema 2)
#     README.md, README.ja.md, CREDITS.txt
param(
    [string]$Version,
    [string]$FrameworkZip
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src/DragNWashLocalization/DragNWashLocalization.csproj'
$SrcDir = Split-Path -Parent $Project
$Dll = Join-Path $SrcDir 'bin/Release/DragNWashLocalization.dll'
$OutDir = Join-Path $Root 'release'

# 0. The version lives in two places: Plugin.PluginVersion, which the Mods
#    screen shows, and the .csproj's <Version>/<FileVersion>, which become the
#    DLL's file version and are what the installer shows. The .csproj comment
#    says to keep them in step, but nothing checked it: a release that updated
#    only one shipped a mod whose two version numbers disagree, and neither the
#    build nor any test notices. Check before anything is built.
#    <FileVersion> is checked too: it is a separate element, so it can drift on
#    its own, and it is the one Windows shows in the file properties.
#
#    A missing element is not the same as a matching one, so the two are told
#    apart. No <FileVersion> is fine: MSBuild takes it from <Version>. No
#    <Version> is not: the assembly version falls back to 1.0.0.0 - and with
#    both elements gone the file version is stamped 1.0.0.0 too - while
#    Plugin.cs still says something else. That is the very drift this check
#    exists to catch, so it must not read as "nothing to compare, carry on".
#
#    The values are trimmed: <Version> 1.1.2 </Version> is valid MSBuild and
#    builds a 1.1.2 DLL, so it must not be reported as a mismatch.
$PluginCs = Get-Content -LiteralPath (Join-Path $SrcDir 'Plugin.cs') -Raw
$PluginVersion = if ($PluginCs -match 'PluginVersion\s*=\s*"([^"]+)"') { $Matches[1].Trim() } else { $null }
$Csproj = Get-Content -LiteralPath $Project -Raw
$CsprojVersion = if ($Csproj -match '<Version>([^<]*)</Version>') { $Matches[1].Trim() } else { $null }
$CsprojFileVersion = if ($Csproj -match '<FileVersion>([^<]*)</FileVersion>') { $Matches[1].Trim() } else { $null }
if (-not $PluginVersion -and -not $Version) {
    throw 'Could not read PluginVersion from Plugin.cs. Pass -Version explicitly.'
}
if (-not $CsprojVersion) {
    throw "No <Version> in $Project. Without it the assembly version falls back to 1.0.0.0 and nothing matches Plugin.PluginVersion. Add <Version> (and <FileVersion>) and keep both in step with it."
}
$Mismatched = @()
if ($PluginVersion -and $PluginVersion -ne $CsprojVersion) {
    $Mismatched += "<Version> $CsprojVersion"
}
if ($PluginVersion -and $CsprojFileVersion -and $PluginVersion -ne $CsprojFileVersion) {
    $Mismatched += "<FileVersion> $CsprojFileVersion"
}
if ($Mismatched) {
    throw "Version mismatch: Plugin.cs says $PluginVersion but the .csproj says $($Mismatched -join ' and '). Keep <Version> and <FileVersion> in step with Plugin.PluginVersion."
}

# 1. The reference assemblies are copied from the game install and never
#    committed. Fail loudly and early with the list of what is missing.
$Required = @(
    'BepInEx.dll', '0Harmony.dll',
    'UnityEngine.CoreModule.dll', 'UnityEngine.dll',
    'Unity.TextMeshPro.dll', 'UnityEngine.UI.dll',
    'YarnSpinner.dll', 'YarnSpinner.Unity.dll', 'Yarn.Google.Protobuf.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll',
    'Unity.InputSystem.dll', 'UnityEngine.TextCoreFontEngineModule.dll',
    'UnityEngine.AssetBundleModule.dll', 'Naelstrof.UnityScriptableSettings.dll', 'Unity.Localization.dll'
)
$Missing = $Required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $SrcDir "libs/$_")) }
if ($Missing) {
    throw "Missing reference assemblies in src/DragNWashLocalization/libs/: $($Missing -join ', '). Copy them from your game install (see the .csproj comment)."
}

# 2. The framework release the mod is built against: the version pinned in
#    framework-version.txt, never a branch. Raising it is a pull request of its
#    own, after that framework release is published.
$FrameworkVersionFile = Join-Path $Root 'framework-version.txt'
$FrameworkVersion = ([IO.File]::ReadAllText($FrameworkVersionFile)).Trim()
if ($FrameworkVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "framework-version.txt says '$FrameworkVersion', not a version like 1.4.3."
}
$FrameworkZipName = "DragNWash.ModFramework-$FrameworkVersion.zip"
$FrameworkCache = Join-Path $Root '.cache/framework'
if (-not $FrameworkZip) {
    # The same public URL the installers use. Downloaded once; delete the file
    # in .cache/framework/ to fetch it again.
    $FrameworkZip = Join-Path $FrameworkCache $FrameworkZipName
    if (-not (Test-Path -LiteralPath $FrameworkZip)) {
        New-Item -ItemType Directory -Force -Path $FrameworkCache | Out-Null
        $ReleaseUrl = "https://github.com/TomXV/dragnwash-modframework/releases/download/v$FrameworkVersion"
        Write-Host "Downloading $ReleaseUrl/$FrameworkZipName ..."
        try {
            Invoke-WebRequest -Uri "$ReleaseUrl/$FrameworkZipName" -OutFile "$FrameworkZip.part"
        } catch {
            throw "Could not download $FrameworkZipName ($($_.Exception.Message)). Is Drag'n Wash ModFramework $FrameworkVersion published? Or pass -FrameworkZip."
        }
        Move-Item -LiteralPath "$FrameworkZip.part" -Destination $FrameworkZip
        # Releases before the framework started publishing SHA256SUMS have none.
        try {
            Invoke-WebRequest -Uri "$ReleaseUrl/SHA256SUMS" -OutFile (Join-Path $FrameworkCache 'SHA256SUMS')
        } catch {
            Remove-Item -LiteralPath (Join-Path $FrameworkCache 'SHA256SUMS') -Force -ErrorAction SilentlyContinue
        }
    }
}
if (-not (Test-Path -LiteralPath $FrameworkZip)) {
    throw "Framework zip not found: $FrameworkZip"
}
$FrameworkZip = (Resolve-Path -LiteralPath $FrameworkZip).Path
if ((Split-Path -Leaf $FrameworkZip) -ne $FrameworkZipName) {
    throw "framework-version.txt pins $FrameworkVersion, so the framework zip must be $FrameworkZipName, not $(Split-Path -Leaf $FrameworkZip)."
}
$FrameworkSha256 = (Get-FileHash -LiteralPath $FrameworkZip -Algorithm SHA256).Hash.ToLowerInvariant()
$FrameworkSize = (Get-Item -LiteralPath $FrameworkZip).Length
$SumsFile = Join-Path (Split-Path -Parent $FrameworkZip) 'SHA256SUMS'
if (Test-Path -LiteralPath $SumsFile) {
    # "<hash>  <name>" (or "<hash> *<name>"), one file per line.
    $Listed = @(Get-Content -LiteralPath $SumsFile |
        Where-Object { $_ -match '^([0-9a-fA-F]{64}) [ *](.+)$' -and $Matches[2].Trim() -eq $FrameworkZipName } |
        ForEach-Object { ($_ -split ' ')[0].ToLowerInvariant() } |
        Sort-Object -Unique)
    if ($Listed.Count -ne 1) {
        throw "SHA256SUMS next to $FrameworkZipName should list it once, with one hash; it has $($Listed.Count)."
    }
    $Listed = $Listed[0]
    if ($Listed -ne $FrameworkSha256) {
        throw "$FrameworkZipName does not match its SHA256SUMS ($FrameworkSha256, listed $Listed). Delete it and download it again."
    }
    Write-Host "$FrameworkZipName matches SHA256SUMS."
} else {
    Write-Warning "No SHA256SUMS next to $FrameworkZipName, so its hash is not checked here. The Build workflow refuses a framework release without one."
}
Write-Host "Drag'n Wash ModFramework $FrameworkVersion`: sha256 $FrameworkSha256, $FrameworkSize bytes"

# The zip is unpacked beside it; only the libraries' DLLs and the installers
# are used from it.
$FrameworkDir = Join-Path $FrameworkCache "DragNWash.ModFramework-$FrameworkVersion"
if (Test-Path -LiteralPath $FrameworkDir) {
    Remove-Item -LiteralPath $FrameworkDir -Recurse -Force
}
Expand-Archive -LiteralPath $FrameworkZip -DestinationPath $FrameworkDir

# The mod compiles against the release's DLLs: every DragNWash.ModFramework*
# reference in the .csproj comes from BepInEx/plugins/<name>/<name>.dll in the
# zip, so what the mod was compiled against is exactly what players get.
$FrameworkRefs = @([regex]::Matches($Csproj, 'libs[\\/](DragNWash\.ModFramework[A-Za-z0-9.]*)\.dll') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if (-not $FrameworkRefs) {
    throw "Found no DragNWash.ModFramework*.dll reference in $Project."
}
foreach ($name in $FrameworkRefs) {
    $source = Join-Path $FrameworkDir "BepInEx/plugins/$name/$name.dll"
    if (-not (Test-Path -LiteralPath $source)) {
        throw "The .csproj references $name.dll, but Drag'n Wash ModFramework $FrameworkVersion has no BepInEx/plugins/$name/$name.dll."
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $SrcDir 'libs') -Force
}

# A full rebuild: the release's DLLs keep their own (older) dates when copied,
# so an incremental build could keep a DLL compiled against other ones.
Write-Host "Building $Project ..."
dotnet build $Project -c Release --no-incremental
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

if (-not (Test-Path -LiteralPath $Dll)) {
    throw "Build succeeded but $Dll was not produced."
}

# 2b. What the installers must make sure of: the minimum version of each
#     framework library, read from the BepInDependency attributes of the DLL
#     just built, so Plugin.cs stays the only list. The installers work with
#     plugin folders, so each GUID is turned into the folder of the library in
#     the framework zip whose BepInPlugin has that GUID. Every library must be
#     at least its minimum in the pinned release, or the zip would pin a
#     framework the mod refuses to run on.
function Get-BepInExAttributes([string]$Path) {
    # The BepInPlugin and BepInDependency attributes on the assembly's types,
    # as @{ Name; Args }. Read from the metadata, so nothing is loaded or
    # locked, and BepInEx.dll is not needed. BepInDependency(guid, flags) has
    # no minimum; its Args hold the flags (an int) instead of a version.
    $stream = [IO.File]::OpenRead($Path)
    try {
        $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
        $md = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($typeHandle in $md.TypeDefinitions) {
            foreach ($attrHandle in $md.GetTypeDefinition($typeHandle).GetCustomAttributes()) {
                $attr = $md.GetCustomAttribute($attrHandle)
                if ($attr.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
                $ctor = $md.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$attr.Constructor)
                if ($ctor.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
                $name = $md.GetString($md.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$ctor.Parent).Name)
                if ($name -ne 'BepInPlugin' -and $name -ne 'BepInDependency') { continue }
                # The constructor's parameter types, then the arguments in that order.
                $sig = $md.GetBlobReader($ctor.Signature)
                $null = $sig.ReadSignatureHeader()
                $count = $sig.ReadCompressedInteger()
                $null = $sig.ReadSignatureTypeCode()
                $types = @(for ($i = 0; $i -lt $count; $i++) { $sig.ReadSignatureTypeCode() })
                $value = $md.GetBlobReader($attr.Value)
                $null = $value.ReadUInt16()
                $values = foreach ($type in $types) {
                    if ($type -eq [Reflection.Metadata.SignatureTypeCode]::String) { $value.ReadSerializedString() } else { $value.ReadInt32() }
                }
                @{ Name = $name; Args = @($values) }
            }
        }
    } finally {
        $stream.Dispose()
    }
}

$LibraryByGuid = @{}
Get-ChildItem -LiteralPath (Join-Path $FrameworkDir 'BepInEx/plugins') -Directory | ForEach-Object {
    $libraryDll = Join-Path $_.FullName "$($_.Name).dll"
    if (-not (Test-Path -LiteralPath $libraryDll)) { return }
    foreach ($attr in Get-BepInExAttributes $libraryDll | Where-Object { $_.Name -eq 'BepInPlugin' }) {
        $LibraryByGuid[$attr.Args[0]] = @{
            Folder      = $_.Name
            Version     = [version]$attr.Args[2]
            FileVersion = [version][Diagnostics.FileVersionInfo]::GetVersionInfo($libraryDll).FileVersion
        }
    }
}
$Needs = [ordered]@{}
$TooOld = @()
foreach ($dep in Get-BepInExAttributes $Dll | Where-Object { $_.Name -eq 'BepInDependency' }) {
    $guid = $dep.Args[0]
    if ($dep.Args[1] -is [int]) {
        # DependencyFlags: 1 = hard, 2 = soft. A soft dependency is not the
        # installers' business; a hard one without a minimum takes any version.
        if (($dep.Args[1] -band 1) -eq 0) { continue }
        $minimum = [version]'0.0.0'
    } else {
        $minimum = [version]$dep.Args[1]
    }
    $library = $LibraryByGuid[$guid]
    if (-not $library) {
        throw "Plugin.cs depends on $guid, which Drag'n Wash ModFramework $FrameworkVersion does not carry. The installers can only fetch the framework's own libraries."
    }
    $Needs[$library.Folder] = $minimum.ToString(3)
    if ($library.Version -lt $minimum -or $library.FileVersion -lt $minimum) {
        $TooOld += "$($library.Folder) is $($library.Version) (file version $($library.FileVersion)) but Plugin.cs needs $($minimum.ToString(3))"
    }
}
if ($TooOld) {
    throw "Drag'n Wash ModFramework $FrameworkVersion is too old for this mod: $($TooOld -join '; '). Pin a newer framework release in framework-version.txt."
}
Write-Host "Needs: $(($Needs.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', ')"

# 3. Version: explicit argument wins, otherwise the PluginVersion read in step 0,
#    which already refused to go on without one.
if (-not $Version) {
    $Version = $PluginVersion
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
# This mod's icon on the Mods screen (the logo by Mister ERIO).
Copy-Item -LiteralPath (Join-Path $Root 'src/DragNWashLocalization/icon.png') -Destination $PluginDir
Copy-Item -LiteralPath (Join-Path $Root 'FlagCatalog.csv') -Destination $PluginDir
# The credits go next to the plugin too (as well as at the top of the zip,
# below): the About tab in the F1 menu reads them from there.
Copy-Item -LiteralPath (Join-Path $Root 'CREDITS.txt') -Destination $PluginDir
# Menu font for systems whose OS fonts have no CJK glyphs (Steam Deck).
Copy-Item -LiteralPath (Join-Path $Root 'assets/menufont/dragnwash-menufont.bundle') -Destination $PluginDir
Copy-Item -LiteralPath (Join-Path $Root 'assets/menufont/OFL.txt') -Destination (Join-Path $PluginDir 'dragnwash-menufont-LICENSE.txt')
# Play-order data (node names, line ids, hashes; no English).
New-Item -ItemType Directory -Force -Path (Join-Path $PluginDir 'data') | Out-Null
Copy-Item -Path (Join-Path $Root 'data/*.csv') -Destination (Join-Path $PluginDir 'data')

# Locale folders and ignore.txt only; never the runtime _discovered/ output
# (it contains the game's own text).
$SrcTranslations = Join-Path $Root 'Translations'
Copy-Item -LiteralPath (Join-Path $SrcTranslations 'ignore.txt') -Destination $TranslationsDir
Get-ChildItem -LiteralPath $SrcTranslations -Directory |
    Where-Object { $_.Name -notlike '_*' } |
    ForEach-Object {
        $dest = Join-Path $TranslationsDir $_.Name
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        # Only the published file, the display name and the status and
        # reviewers for the About tab ship.
        Copy-Item -LiteralPath (Join-Path $_.FullName 'strings.csv') -Destination $dest
        foreach ($small in 'name.txt', 'credits.txt') {
            $smallFile = Join-Path $_.FullName $small
            if (Test-Path -LiteralPath $smallFile) { Copy-Item -LiteralPath $smallFile -Destination $dest }
        }
        # Translated pictures (docs/TRANSLATED_TEXTURES.md): the PNGs, their credits and the fallback list.
        $textures = Join-Path $_.FullName 'textures'
        if (Test-Path -LiteralPath $textures) {
            $destTextures = Join-Path $dest 'textures'
            New-Item -ItemType Directory -Force -Path $destTextures | Out-Null
            Get-ChildItem -LiteralPath $textures -File |
                Where-Object { $_.Extension -eq '.png' -or $_.Name -eq 'credits.csv' -or $_.Name -eq 'fallback.txt' } |
                ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destTextures }
        }
    }

# Both READMEs ship: the user most likely to be stuck is looking at the extracted
# folder offline, and most of them read Japanese.
Copy-Item -LiteralPath (Join-Path $Root 'README.md') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $Root 'README.ja.md') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $Root 'CREDITS.txt') -Destination $Stage

# The installers: Drag'n Wash ModFramework's shared Install.exe and
# install-steamdeck.sh, the same files every mod ships (see the framework's
# https://github.com/TomXV/dragnwash-modframework/wiki/Installer). They are
# copied from the pinned release's installer/ folder, not rebuilt, so their
# hashes - and the antivirus reputation that follows Install.exe - are the
# framework release's own.
foreach ($installer in 'Install.exe', 'install-steamdeck.sh') {
    $source = Join-Path $FrameworkDir "installer/$installer"
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Drag'n Wash ModFramework $FrameworkVersion has no installer/$installer."
    }
    Copy-Item -LiteralPath $source -Destination $Stage
}
$InstallerHash = (Get-FileHash -LiteralPath (Join-Path $Stage 'Install.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Install.exe sha256 $InstallerHash (the one in Drag'n Wash ModFramework $FrameworkVersion)"

# mod-install.json (schema 2): the framework release the installers fetch when
# the game folder has none new enough - its size and hash are what a download
# is checked against, the needs what an installed framework is checked against -
# then this mod's folder, the player's data the installers keep (translation
# working files and old save snapshots), the config file, and the language
# question with every pack that ships.
$Options = @()
Get-ChildItem -LiteralPath $TranslationsDir -Directory | Sort-Object Name | ForEach-Object {
    $display = $_.Name
    $nameFile = Join-Path $_.FullName 'name.txt'
    if (Test-Path -LiteralPath $nameFile) {
        $text = ([IO.File]::ReadAllText($nameFile, [Text.Encoding]::UTF8)).Trim()
        if ($text) { $display = $text }
    }
    $Options += [ordered]@{ value = $_.Name; name = $display }
}
$Options += [ordered]@{ value = 'en'; name = 'English' }
$Manifest = [ordered]@{
    schema      = 2
    name        = "Drag'n Wash Localization"
    version     = $Version
    website     = 'https://github.com/TomXV/dragnwash-localization'
    framework   = [ordered]@{
        version = $FrameworkVersion
        sha256  = $FrameworkSha256
        size    = $FrameworkSize
        needs   = $Needs
    }
    plugins     = @('DragNWashLocalization')
    keep        = @('DragNWashLocalization/Translations/_discovered', 'DragNWashLocalization/SaveHistory')
    configFiles = @('com.tomxv.dragnwash.localization.cfg')
    choices     = @([ordered]@{
        id      = 'language'
        label   = [ordered]@{ en = 'Language'; ja = '言語'; zh = '语言' }
        config  = [ordered]@{ file = 'com.tomxv.dragnwash.localization.cfg'; section = 'General'; key = 'TargetLocale' }
        options = $Options
        default = 'ui-language'
    })
}
[IO.File]::WriteAllText((Join-Path $Stage 'mod-install.json'), ($Manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))

# 5. Zip the stage contents (so the zip root holds BepInEx/ and the READMEs).
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Zip = Join-Path $OutDir "DragNWashLocalization-$Version.zip"
if (Test-Path -LiteralPath $Zip) {
    Remove-Item -LiteralPath $Zip -Force
}

Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Zip

Write-Host ''
Write-Host "Created $Zip"
Write-Host "Install: extract anywhere and double-click Install.exe (or merge BepInEx/ into the game folder by hand, with Drag'n Wash ModFramework $FrameworkVersion from its Releases page)."
