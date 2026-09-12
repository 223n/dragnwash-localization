# Menu font bundle

`dragnwash-menufont.bundle` is a Unity AssetBundle holding **Noto Sans JP**
(variable font, `NotoSansJP-VF.ttf`, as shipped with Windows 11) imported as a
dynamic `Font` asset at 14 px.

The F1 menu is drawn with Unity's IMGUI, which can only use fonts the OS
lists or `Font` assets. Inside Steam's Linux runtime (Steam Deck) the OS list
holds only DejaVu, so without this file every CJK character in the menu is a
box. The plugin loads the bundle only when no OS font renders; on Windows the
menu keeps using Yu Gothic UI / Meiryo UI.

## License

Noto Sans JP: © 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font
Name "Source". Licensed under the SIL Open Font License, Version 1.1
(http://scripts.sil.org/OFL). The full license text is in `OFL.txt` here and
ships in the release zip as `dragnwash-menufont-LICENSE.txt` next to the
bundle. The font is redistributed unmodified inside the bundle.

## Rebuilding

`BuildBundle.cs` is the editor script used. Put it under `Assets/Editor/` of
an empty Unity project (built with 6000.0.54f1; the game runs 6000.3), put the
font at `Assets/Fonts/NotoSansJP.ttf`, then run:

```
Unity.exe -batchmode -nographics -quit -projectPath <project> -executeMethod BuildBundle.Build
```

The bundle lands in `<project>/Bundles/dragnwash-menufont`; copy it here as
`dragnwash-menufont.bundle`. It is built for the Windows standalone target and
loads on the Linux player as well (verified on the Steam Deck).
