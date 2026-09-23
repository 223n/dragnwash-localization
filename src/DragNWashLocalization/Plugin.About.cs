using System;
using System.Collections.Generic;
using UnityEngine;
using DragNWash.ModFramework.ToolWindow;

namespace DragNWashLocalization
{
    // The About tab: what is running, who made it, the license and this session.
    //
    // Every literal here is ASCII on purpose. Rasterizing a glyph the menu
    // font has not seen yet uploads a texture, and on Direct3D 12 an upload
    // while the menu is open is what crashes the game (Unity UUM-140564).
    // The values that are not ours to choose - the install path, the folder
    // names under Translations and the configured locale - cannot be kept
    // ASCII, so Plugin.PrepareWindowCharacters feeds them to the font at
    // startup instead.
    public partial class Plugin
    {
        private Vector2 _aboutScroll;

        // The content is drawn top to bottom with a running y; its height is
        // only known at the end, so the scroll view uses the last pass's.
        private float _aboutHeight = 1200;
        private float _aboutY;
        private float _aboutWidth;

        private static readonly Color AboutLineColor = new Color(0.165f, 0.20f, 0.26f);
        private GUIStyle _aboutHeadStyle;
        private GUIStyle _aboutHeadBase;

        // Headings in the accent colour. Same font, size and weight as Label:
        // a bold or bigger face would be another font texture to fill, which
        // is the upload Direct3D 12 does not survive.
        private GUIStyle AboutHeadStyle
        {
            get
            {
                if (_aboutHeadStyle == null || _aboutHeadBase != S.Label)
                {
                    _aboutHeadBase = S.Label;
                    _aboutHeadStyle = new GUIStyle(S.Label);
                    _aboutHeadStyle.normal.textColor = ToolWindow.AccentColor;
                }
                return _aboutHeadStyle;
            }
        }

        private void DrawAbout(Rect area)
        {
            ToolWindow.Fill(area, ToolWindow.InsetColor);
            float innerWidth = Mathf.Max(100, area.width - 36);
            _aboutWidth = innerWidth - 24;

            ToolWindow.ApplyScroll(area, ref _aboutScroll);
            _aboutScroll = GUI.BeginScrollView(area, _aboutScroll,
                new Rect(0, 0, innerWidth, Mathf.Max(area.height, _aboutHeight)), false, false);
            _aboutY = 12;

            DrawAboutCard();
            AboutText("It doesn't touch the game's files. Text is swapped as it appears on screen, so game updates don't break it, and uninstalling puts everything back the way it was.");

            AboutHeading("BUILT ON DRAG'N WASH MODFRAMEWORK");
            AboutText("A small shared base for Drag'n Wash mods. It gives you the Mods screen in Options, update notices, one installer for every mod, and this tool window. Its first rule is that mods run safely together. It's a separate open project, and anyone can build a mod on it.");

            AboutHeading("CREDITS");
            AboutText("Created by TomXV. Translation files by TomXV, with corrections from contributors credited in the README and in each language file.");
            AboutText("Korean proofread by Hotcake.");
            AboutText("This mod's logo by Mister ERIO, who also drew the framework's Mods button. The framework's logo and icon by NotaGames.");
            AboutText("Source, issues and translation contributions: github.com/TomXV/dragnwash-localization");

            AboutHeading("LANGUAGES");
            AboutText("Supervised by the author: Japanese (ja), Simplified Chinese (zh-Hans).");
            AboutText("Converted from the supervised Simplified Chinese: Traditional Chinese (zh-Hant).");
            AboutText("Proofread by a native speaker: Korean (ko), by Hotcake.");
            AboutText("Provisional, not reviewed by native speakers: German (de), French (fr), Spanish (es), Brazilian Portuguese (pt-BR), Russian (ru), Polish (pl), Hebrew (he), Ukrainian (uk), Thai (th), Vietnamese (vi).");
            AboutText("Just for fun: Esperanto (eo), Toki Pona (tok).");
            AboutText("Provisional lines may read unnaturally. Native speakers: corrections are very welcome as pull requests.");
            AboutText("Installed in this copy: " + string.Join(", ", _availableLocales ?? new string[0]));

            AboutHeading("TOOLS IN THIS WINDOW");
            AboutText("These are for translators and mod makers, and you don't need them to play. There's the Translation tab (working copies, exports, hot reload), the Saves tab, the framework's Assets tab (see what's loaded, replace textures) and Console (the log with levels, and commands).");
            AboutText("What people make with these tools is their own work and their own responsibility. Nothing here exports or ships the game's files as part of this mod.");

            AboutHeading("LICENSE");
            AboutText("The mod's code is MIT licensed (see LICENSE in the repository).");
            AboutText("The artwork named under Credits (this mod's logo, the framework's icon and its Mods button) belongs to its artists, is used with their permission, and is not covered by the MIT license.");
            AboutText("The bundled menu font is Noto Sans JP, (c) 2014-2021 Adobe, with Reserved Font Name 'Source', under the SIL Open Font License 1.1. Its full text ships next to the plugin as dragnwash-menufont-LICENSE.txt.");

            AboutHeading("THIS SESSION");
            AboutPairs(12, _aboutWidth,
                "Game", $"Unity {Application.unityVersion}",
                "Graphics", SystemInfo.graphicsDeviceType.ToString(),
                "Platform", Application.platform.ToString());
            AboutText($"Plugin folder: {PluginDirectory}");

            _aboutY += 6;
            if (GUI.Button(new Rect(12, _aboutY, 260, RowHeight), "Copy the repository address", S.Button))
            {
                GUIUtility.systemCopyBuffer = "https://github.com/TomXV/dragnwash-localization";
                ToolWindow.ShowNotice("Repository address copied to the clipboard.");
            }
            _aboutY += RowHeight;

            _aboutHeight = _aboutY + 16;
            GUI.EndScrollView();
        }

        // Name, version, the framework under it and the language in use, on a
        // panel at the top: what a player opens the tab to check.
        private void DrawAboutCard()
        {
            const float pad = 12, bar = 3;
            string build = BuildId();
            string locale = TargetLocale.Value;
            string language = locale == "en" ? "English" : LocaleDisplayName(locale);
            if (!MenuFontCanDraw(language))
            {
                language = locale;
            }
            language = language == locale ? locale : $"{language} ({locale})";
            string lines = TranslationStore.EntryCount.ToString() +
                (LineResolution.ReviewCount > 0 ? $" ({LineResolution.ReviewCount} to review)" : "");

            float x = 12 + bar + pad;
            float w = _aboutWidth - bar - 2 * pad;
            var disclaimer = new GUIContent("This is an unofficial fan mod. It has nothing to do with the Drag'n Wash developers, so please don't ask them for help with it.");
            float disclaimerHeight = S.WrappedLabel.CalcHeight(disclaimer, w);
            float pairsHeight = AboutPairsHeight(w, 4);
            float height = pad + 28 + pairsHeight + 6 + disclaimerHeight + pad;

            var card = new Rect(12, _aboutY, _aboutWidth, height);
            ToolWindow.Fill(card, ToolWindow.PanelColor);
            ToolWindow.Fill(new Rect(card.x, card.y, bar, card.height), ToolWindow.AccentColor);

            float y = _aboutY + pad;
            GUI.Label(new Rect(x, y, w, 26), "DRAG'N WASH LOCALIZATION", S.Label);
            y += 28;
            float saved = _aboutY;
            _aboutY = y;
            AboutPairs(x, w,
                "Version", PluginVersion + (string.IsNullOrEmpty(build) ? "" : $" (build {build})"),
                "ModFramework core", DragNWash.ModFramework.ModFramework.Version,
                "Language", ToolWindow.Drawable(language),
                "Lines loaded", lines);
            y = _aboutY + 6;
            GUI.Label(new Rect(x, y, w, disclaimerHeight), disclaimer, S.WrappedLabel);
            _aboutY = saved + height + 10;
        }

        // A heading in the accent colour with a thin line under it, so it does
        // not read as one more line of text.
        private void AboutHeading(string text)
        {
            _aboutY += 14;
            GUI.Label(new Rect(12, _aboutY, _aboutWidth, 26), text, AboutHeadStyle);
            _aboutY += 26;
            ToolWindow.Fill(new Rect(12, _aboutY, _aboutWidth, 1), AboutLineColor);
            _aboutY += 8;
        }

        private void AboutText(string text)
        {
            var content = new GUIContent(text);
            float h = S.WrappedLabel.CalcHeight(content, _aboutWidth);
            GUI.Label(new Rect(12, _aboutY, _aboutWidth, h), content, S.WrappedLabel);
            _aboutY += h + 6;
        }

        // Key and value pairs, two to a line when the window is wide enough:
        // the key muted, the value in the normal text colour.
        private const float AboutPairWideWidth = 460;

        private static float AboutPairsHeight(float width, int pairs)
        {
            int perLine = width >= AboutPairWideWidth ? 2 : 1;
            return Mathf.Ceil(pairs / (float)perLine) * 26;
        }

        private void AboutPairs(float x, float width, params string[] keysAndValues)
        {
            int perLine = width >= AboutPairWideWidth ? 2 : 1;
            float column = width / perLine;
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2)
            {
                int n = i / 2;
                float cx = x + (n % perLine) * column;
                if (n > 0 && n % perLine == 0)
                {
                    _aboutY += 26;
                }
                float keyWidth = S.MutedLabel.CalcSize(new GUIContent(keysAndValues[i])).x;
                GUI.Label(new Rect(cx, _aboutY, keyWidth, 26), keysAndValues[i], S.MutedLabel);
                GUI.Label(new Rect(cx + keyWidth + 8, _aboutY, Mathf.Max(20, column - keyWidth - 16), 26), keysAndValues[i + 1], S.Label);
            }
            _aboutY += 26;
        }
    }
}
