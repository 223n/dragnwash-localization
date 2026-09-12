# Releasing

[日本語](RELEASING.ja.md)

This document describes how to distribute the mod. Building the plugin requires game-derived reference assemblies in `libs/`. They cannot be committed to this repository for copyright reasons. As a result, **the plugin cannot be built in CI; releases must be built locally on a computer with the game installed** and uploaded to GitHub Releases.

Translation-only changes do not require a build. Translators should see [CONTRIBUTING.md](../CONTRIBUTING.md).

## Prerequisites

- The Steam version of Drag'n Wash is installed.
- All game-derived reference assemblies are present in `src/DragNWashLocalization/libs/`. See the comments in the `.csproj` file for the complete list.
- The .NET SDK and PowerShell 7 (`pwsh`) are installed.
- To create GitHub Releases from the command line, install [`gh`](https://cli.github.com/).

## Procedure

### 1. Update the version

Update `PluginVersion` in `src/DragNWashLocalization/Plugin.cs` and `<Version>` / `<FileVersion>` in the `.csproj` (the installer shows the file version).

The value is used in the BepInEx plugin ID string and must use the `x.y.z` format, for example `0.2.0`.

Update the status in `docs/PLAN.md` and `README.md` as needed.

### 2. Commit, tag, then build the ZIP

Commit the version bump and create the tag **before** building. The DLL's informational version embeds the commit hash of the checkout it was built from (visible as `0.1.1+<hash>` in the file properties), so building first would stamp the previous commit. The build is a clean one so the hash is refreshed:

```powershell
git commit -am "Release 0.2.0"
git tag -a v0.2.0 -m "v0.2.0"
Remove-Item -Recurse -Force src/DragNWashLocalization/obj, src/DragNWashLocalization/bin
pwsh tools/pack.ps1
```

This creates `release/DragNWashLocalization-<version>.zip` with the following structure:

```text
BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
BepInEx/plugins/DragNWashLocalization/Translations/<locale>/strings.csv
BepInEx/plugins/DragNWashLocalization/Translations/ignore.txt
BepInEx/plugins/DragNWashLocalization/Translations/<locale>/name.txt
BepInEx/plugins/DragNWashLocalization/FlagCatalog.csv
BepInEx/plugins/DragNWashLocalization/data/script_order.csv
BepInEx/plugins/DragNWashLocalization/data/level_flow.csv
Install.exe
installer/Installer.ps1
README.md
```

`Install.exe` is a small console-less launcher compiled by `pack.ps1` with the C# compiler that ships with .NET Framework 4 (`%WINDIR%\Microsoft.NET\Framework644.0.30319\csc.exe`); nothing extra needs to be installed. Users double-click it to install, update, or uninstall. Extracting the `BepInEx/` directory into the game folder by hand still works.

To install it, extract the archive into the game directory and merge the included `BepInEx/` directory.

### 3. Validate the package

- Confirm that the Release build produces no warnings or errors.
- Extract the ZIP and confirm that the DLL and `Translations/` directory are in the correct locations.
- If possible, launch the game once with a clean BepInEx installation and verify the F1 menu and language switching.

### 4. Create the GitHub Release

```powershell
gh release create v0.2.0 release/DragNWashLocalization-0.2.0.zip `
  --title "v0.2.0" `
  --notes "Describe the changes here"
```

Use tags prefixed with `v`, such as `v0.2.0`. You may also use the GitHub web interface: open Releases, draft a new release, create the tag, and upload the ZIP.

The release notes must state that **BepInEx is required separately** and mention the `-force-d3d11` workaround for crashes under Direct3D 12. See the [README](../README.md).

## Why releases are not built in CI

The game DLLs required for compilation, including `UnityEngine.CoreModule.dll` and `YarnSpinner.dll`, cannot be included in the repository. GitHub Actions therefore cannot compile the plugin. Builds are created locally, and only the resulting ZIP is attached to a release.
