using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

            AboutHeading("LINKS");
            DrawAboutLinks();

            AboutHeading("BUILT ON DRAG'N WASH MODFRAMEWORK");
            AboutText("A small shared base for Drag'n Wash mods. It gives you the Mods screen in Options, update notices, one installer for every mod, and this tool window. Its first rule is that mods run safely together. It's a separate open project, and anyone can build a mod on it.");

            AboutHeading("CREDITS");
            DrawAboutCredits();

            AboutHeading("LANGUAGES");
            DrawAboutLanguages();
            AboutText("No native speaker has checked the provisional packs yet, so some lines may sound off. If it's your language, corrections are very welcome as pull requests.");

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
            AboutText(ToolWindow.Drawable($"Plugin folder: {PluginDirectory}"));
            _aboutY += 2;
            if (GUI.Button(new Rect(12, _aboutY, 220, RowHeight), "Copy for a bug report", S.Button))
            {
                GUIUtility.systemCopyBuffer = BugReportText();
                ToolWindow.ShowNotice("Copied. Paste it into your report.");
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

        // The project's own pages and nothing else. The address is shown in
        // full, the domain brighter than the rest, so it is plain where Open
        // goes before pressing it. Copy stays for when a browser is no help:
        // switching away from exclusive fullscreen, or Steam Deck game mode.
        private const string RepositoryUrl = "https://github.com/TomXV/dragnwash-localization";
        private const string SteamGuideJapaneseUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3801418794";
        private const string SteamGuideEnglishUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3801420947";
        private const string FrameworkWikiUrl = "https://github.com/TomXV/dragnwash-modframework/wiki";

        private void DrawAboutLinks()
        {
            AboutLink("This mod on GitHub", RepositoryUrl);
            AboutLink("Report a problem", RepositoryUrl + "/issues");
            AboutLink("Steam guide", TargetLocale.Value == "ja" ? SteamGuideJapaneseUrl : SteamGuideEnglishUrl);
            AboutLink("ModFramework wiki", FrameworkWikiUrl);
            _aboutY += 2;
            GUI.Label(new Rect(12, _aboutY, _aboutWidth, 26), "Open switches to your web browser.", S.MutedLabel);
            _aboutY += 30;
        }

        private void AboutLink(string label, string url)
        {
            const float labelWidth = 150, buttonWidth = 70, gap = 8;
            string shown = url.Substring(url.IndexOf("//", StringComparison.Ordinal) + 2);
            int slash = shown.IndexOf('/');
            string domain = slash < 0 ? shown : shown.Substring(0, slash);
            string path = slash < 0 ? "" : shown.Substring(slash);
            float domainWidth = S.Label.CalcSize(new GUIContent(domain)).x;
            float pathWidth = S.MutedLabel.CalcSize(new GUIContent(path)).x;
            float buttonsWidth = 2 * buttonWidth + gap;

            // One line when it all fits; in a narrow window the address goes
            // under the name and the buttons under the address.
            bool oneLine = labelWidth + domainWidth + pathWidth + 16 + buttonsWidth <= _aboutWidth;
            GUI.Label(new Rect(12, _aboutY, labelWidth, RowHeight), label, S.Label);
            float urlX = 12 + labelWidth;
            if (!oneLine && labelWidth + domainWidth + pathWidth > _aboutWidth)
            {
                _aboutY += 26;
                urlX = 24;
            }
            GUI.Label(new Rect(urlX, _aboutY, domainWidth, RowHeight), domain, S.Label);
            GUI.Label(new Rect(urlX + domainWidth, _aboutY, Mathf.Max(20, 12 + _aboutWidth - urlX - domainWidth), RowHeight), path, S.MutedLabel);

            float buttonX = 12 + _aboutWidth - buttonsWidth;
            if (!oneLine)
            {
                _aboutY += 28;
                buttonX = urlX;
            }
            if (GUI.Button(new Rect(buttonX, _aboutY, buttonWidth, RowHeight), "Open", S.Button))
            {
                try
                {
                    Application.OpenURL(url);
                    ToolWindow.ShowNotice("Opening your web browser. If nothing shows up, use Copy.");
                }
                catch (Exception ex)
                {
                    Log($"[about] Could not open {url}: {ex.Message}");
                    ToolWindow.ShowNotice("Couldn't open the browser. Use Copy instead.");
                }
            }
            if (GUI.Button(new Rect(buttonX + buttonWidth + gap, _aboutY, buttonWidth, RowHeight), "Copy", S.Button))
            {
                GUIUtility.systemCopyBuffer = url;
                ToolWindow.ShowNotice("Address copied to the clipboard.");
            }
            _aboutY += RowHeight + 6;
        }

        // CREDITS.txt, which tools/pack.ps1 puts next to the plugin as well as
        // at the top of the zip, so a new name only has to be written there
        // and not here too. Read once at startup; null when the file is not
        // there (a hand-made install from an older zip), and the tab then
        // shows the credits it always had.
        private sealed class CreditItem
        {
            public string Heading;   // a section title, such as "Artwork"
            public string Who;       // an entry's name, its URL left out
            public string Text;      // what they did, or a paragraph
        }

        private static List<CreditItem> _credits;

        // Translations/<locale>/credits.txt: the pack's status on the first
        // line (supervised, proofread, converted, provisional or fun), then who
        // checked it, one per line. A pack without the file is provisional.
        private sealed class LocaleCredit
        {
            public string Status = "provisional";
            public string Names = "";
        }

        private static readonly Dictionary<string, LocaleCredit> _localeCredits =
            new Dictionary<string, LocaleCredit>(StringComparer.Ordinal);

        private static LocaleCredit ReadLocaleCredit(string locale, StringBuilder text)
        {
            var credit = new LocaleCredit();
            string path = Path.Combine(Path.Combine(PluginDirectory, "Translations"), Path.Combine(locale, "credits.txt"));
            try
            {
                if (!File.Exists(path))
                {
                    return credit;
                }
                var names = new List<string>();
                bool first = true;
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (first)
                    {
                        credit.Status = line.ToLowerInvariant();
                        first = false;
                    }
                    else
                    {
                        names.Add(line);
                        text.Append(line);
                    }
                }
                credit.Names = string.Join(", ", names);
            }
            catch (Exception ex)
            {
                Log($"[about] Could not read {locale}/credits.txt: {ex.Message}");
            }
            return credit;
        }

        // Everything the About tab reads from files, loaded now. Returns the
        // text for PrepareWindowCharacters: a name in the credits may be in
        // any script.
        private string LoadAboutFiles()
        {
            var text = new StringBuilder();
            _localeCredits.Clear();
            foreach (string locale in _availableLocales)
            {
                if (locale != "en")
                {
                    _localeCredits[locale] = ReadLocaleCredit(locale, text);
                }
            }
            _credits = null;
            string path = Path.Combine(PluginDirectory, "CREDITS.txt");
            try
            {
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    _credits = ParseCredits(lines);
                    foreach (string line in lines)
                    {
                        text.Append(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[about] Could not read CREDITS.txt: {ex.Message}");
            }
            return text.ToString();
        }

        // The layout CREDITS.txt is written in: a title over a ==== line,
        // sections over ---- lines, an entry as a name (and URL) with what they
        // did indented under it, other paragraphs as they are, and a "---"
        // line before the closing note.
        private static List<CreditItem> ParseCredits(string[] lines)
        {
            var items = new List<CreditItem>();
            var block = new List<string>();
            void Flush()
            {
                if (block.Count == 0)
                {
                    return;
                }
                bool entry = block.Count > 1 && !Indented(block[0]);
                for (int k = 1; k < block.Count && entry; k++)
                {
                    entry = Indented(block[k]);
                }
                var words = new List<string>();
                for (int k = entry ? 1 : 0; k < block.Count; k++)
                {
                    words.Add(block[k].Trim());
                }
                items.Add(new CreditItem
                {
                    Who = entry ? WithoutAddress(block[0].Trim()) : null,
                    Text = string.Join(" ", words),
                });
                block.Clear();
            }

            // The title and its ==== line: the tab has its own heading.
            int start = lines.Length > 1 && IsRule(lines[1], '=') ? 2 : 0;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0)
                {
                    Flush();
                }
                else if (IsRule(line, '-'))
                {
                    Flush();
                    items.Add(new CreditItem());   // the "---" before the closing note
                }
                else if (i + 1 < lines.Length && IsRule(lines[i + 1], '-') && block.Count == 0)
                {
                    items.Add(new CreditItem { Heading = line.Trim() });
                    i++;
                }
                else
                {
                    block.Add(line);
                }
            }
            Flush();
            return items;
        }

        private static bool Indented(string line) => line.Length > 0 && char.IsWhiteSpace(line[0]);

        private static bool IsRule(string line, char c)
        {
            string t = line.Trim();
            return t.Length >= 3 && t.Trim(c).Length == 0;
        }

        // "223n (https://github.com/223n)" -> "223n". The tab links only to
        // this project's own pages.
        private static string WithoutAddress(string who)
        {
            int open = who.IndexOf(" (http", StringComparison.Ordinal);
            return open > 0 && who.EndsWith(")", StringComparison.Ordinal) ? who.Substring(0, open) : who;
        }

        private void DrawAboutCredits()
        {
            if (_credits == null)
            {
                AboutText("Created by TomXV. Translation files by TomXV, with corrections from contributors credited in the README and in each language file.");
                AboutText("Korean proofread by Hotcake.");
                AboutText("This mod's logo by Mister ERIO, who also drew the framework's Mods button. The framework's logo and icon by NotaGames.");
                return;
            }

            AboutText("Made by TomXV.");
            // Names in a column of their own, what they did beside them; one
            // under the other when the window is too narrow for two columns.
            float whoWidth = Mathf.Min(180, _aboutWidth * 0.35f);
            bool twoColumns = _aboutWidth >= 360;
            foreach (CreditItem item in _credits)
            {
                if (item.Heading != null)
                {
                    _aboutY += 6;
                    GUI.Label(new Rect(12, _aboutY, _aboutWidth, 26), ToolWindow.Drawable(item.Heading), S.Label);
                    _aboutY += 28;
                }
                else if (item.Who != null)
                {
                    string who = ToolWindow.Drawable(item.Who);
                    var what = new GUIContent(ToolWindow.Drawable(item.Text));
                    if (twoColumns)
                    {
                        float h = Mathf.Max(26, S.WrappedLabel.CalcHeight(what, _aboutWidth - whoWidth - 12));
                        GUI.Label(new Rect(24, _aboutY, whoWidth - 12, 26), who, S.Label);
                        GUI.Label(new Rect(12 + whoWidth + 12, _aboutY + 3, _aboutWidth - whoWidth - 12, h), what, S.WrappedLabel);
                        _aboutY += h + 6;
                    }
                    else
                    {
                        GUI.Label(new Rect(24, _aboutY, _aboutWidth - 12, 26), who, S.Label);
                        _aboutY += 26;
                        float h = S.WrappedLabel.CalcHeight(what, _aboutWidth - 24);
                        GUI.Label(new Rect(36, _aboutY, _aboutWidth - 24, h), what, S.WrappedLabel);
                        _aboutY += h + 6;
                    }
                }
                else if (!string.IsNullOrEmpty(item.Text))
                {
                    AboutText(ToolWindow.Drawable(item.Text));
                }
                else
                {
                    _aboutY += 8;
                }
            }
        }

        // What a status looks like in the table: its tag, the tag's colour and
        // where it sorts. Colours are the window's own, each at least 4.5:1 on
        // the tab's background. An unknown word reads as provisional.
        private static void LocaleStatus(string status, out string tag, out Color color, out int rank)
        {
            switch (status)
            {
                case "supervised": tag = "SUPERVISED"; color = ToolWindow.AccentColor; rank = 0; break;
                case "proofread": tag = "PROOFREAD"; color = ToolWindow.AccentColor; rank = 1; break;
                case "converted": tag = "CONVERTED"; color = ToolWindow.WarningColor; rank = 2; break;
                case "fun": tag = "FOR FUN"; color = ToolWindow.MutedColor; rank = 4; break;
                default: tag = "PROVISIONAL"; color = ToolWindow.WarningColor; rank = 3; break;
            }
        }

        private GUIStyle _aboutTagStyle;
        private GUIStyle _aboutTagBase;

        // One line per installed language: its name, code, status and who
        // checked it, the one in use marked with the accent line.
        private void DrawAboutLanguages()
        {
            if (_aboutTagStyle == null || _aboutTagBase != S.MutedLabel)
            {
                _aboutTagBase = S.MutedLabel;
                _aboutTagStyle = new GUIStyle(S.MutedLabel) { alignment = TextAnchor.MiddleCenter };
            }

            var rows = new List<string>(_availableLocales);
            rows.Sort((a, b) =>
            {
                int ra = AboutLocaleRank(a), rb = AboutLocaleRank(b);
                return ra != rb ? ra.CompareTo(rb) : string.CompareOrdinal(a, b);
            });

            float nameWidth = Mathf.Clamp(_aboutWidth * 0.25f, 90, 170);
            const float codeWidth = 70;
            float tagWidth = _aboutTagStyle.CalcSize(new GUIContent("PROVISIONAL")).x + 16;
            float inUseWidth = S.Label.CalcSize(new GUIContent("in use")).x;
            float byX = 24 + nameWidth + codeWidth + tagWidth + 12;
            // In a narrow window who checked it goes on a line of its own.
            bool byBelow = 12 + _aboutWidth - byX < 120;
            if (rows.Count == 0)
            {
                AboutText("No language folders installed.");
                return;
            }
            foreach (string locale in rows)
            {
                bool inUse = locale == TargetLocale.Value;
                string name = locale == "en" ? "English" : LocaleDisplayName(locale);
                if (!MenuFontCanDraw(name))
                {
                    name = locale;
                }
                string by;
                bool hasTag = locale != "en";
                string tag = null;
                Color color = ToolWindow.MutedColor;
                if (hasTag)
                {
                    LocaleCredit credit = _localeCredits.TryGetValue(locale, out LocaleCredit c) ? c : new LocaleCredit();
                    LocaleStatus(credit.Status, out tag, out color, out int _);
                    by = credit.Names;
                }
                else
                {
                    by = "the game's own text";
                }

                float rowHeight = byBelow && (by.Length > 0 || inUse) ? 54 : 28;
                if (inUse)
                {
                    ToolWindow.Fill(new Rect(12, _aboutY, 2, rowHeight - 2), ToolWindow.AccentColor);
                }
                GUI.Label(new Rect(24, _aboutY, nameWidth - 8, 26), ToolWindow.Drawable(name), S.Label);
                GUI.Label(new Rect(24 + nameWidth, _aboutY, codeWidth - 8, 26), locale, S.MutedLabel);
                if (hasTag)
                {
                    var box = new Rect(24 + nameWidth + codeWidth, _aboutY + 2, tagWidth, 22);
                    ToolWindow.Fill(new Rect(box.x, box.y, box.width, 1), color);
                    ToolWindow.Fill(new Rect(box.x, box.yMax - 1, box.width, 1), color);
                    ToolWindow.Fill(new Rect(box.x, box.y, 1, box.height), color);
                    ToolWindow.Fill(new Rect(box.xMax - 1, box.y, 1, box.height), color);
                    Color previous = _aboutTagStyle.normal.textColor;
                    _aboutTagStyle.normal.textColor = color;
                    GUI.Label(box, tag, _aboutTagStyle);
                    _aboutTagStyle.normal.textColor = previous;
                }

                float lineY = byBelow ? _aboutY + 26 : _aboutY;
                float left = byBelow ? 36 : byX;
                float right = 12 + _aboutWidth;
                if (inUse)
                {
                    GUI.Label(new Rect(right - inUseWidth, lineY, inUseWidth, 26), "in use", AboutHeadStyle);
                    right -= inUseWidth + 12;
                }
                if (by.Length > 0)
                {
                    GUI.Label(new Rect(left, lineY, Mathf.Max(20, right - left), 26), ToolWindow.Drawable(by), S.MutedLabel);
                }
                _aboutY += rowHeight;
            }
            _aboutY += 8;
        }

        // English, which is no pack, goes last.
        private static int AboutLocaleRank(string locale)
        {
            if (locale == "en")
            {
                return 9;
            }
            LocaleCredit credit = _localeCredits.TryGetValue(locale, out LocaleCredit c) ? c : new LocaleCredit();
            LocaleStatus(credit.Status, out string _, out Color _, out int rank);
            return rank;
        }

        // What a bug report needs to say about this install, a few lines to
        // paste. The folder is shown in full on screen (it is the player's own
        // PC), but the copy hides the user name in it: that is often a real
        // name, and the text is meant to be posted in public.
        private string BugReportText()
        {
            string build = BuildId();
            var text = new StringBuilder();
            text.Append("Drag'n Wash Localization ").Append(PluginVersion);
            if (!string.IsNullOrEmpty(build))
            {
                text.Append(" (build ").Append(build).Append(')');
            }
            text.Append('\n');
            text.Append("ModFramework core ").Append(DragNWash.ModFramework.ModFramework.Version).Append('\n');
            text.Append($"Language {TargetLocale.Value}, {TranslationStore.EntryCount} lines loaded, {LineResolution.ReviewCount} to review\n");
            text.Append($"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType}, {Application.platform}\n");
            text.Append("Plugin folder: ").Append(WithoutUserName(PluginDirectory));
            return text.ToString();
        }

        // The user's own folder becomes %USERPROFILE%; any other ...\Users\<name>
        // or /home/<name> left in the path (a Steam library elsewhere, Proton's
        // Z: drive on Steam Deck) becomes <user>.
        private static readonly Regex UserFolder = new Regex(@"([\\/](?:users|home)[\\/])[^\\/]+", RegexOptions.IgnoreCase);

        private static string WithoutUserName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }
            try
            {
                string home = (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "").TrimEnd('\\', '/');
                if (home.Length > 3 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase) &&
                    (path.Length == home.Length || path[home.Length] == '\\' || path[home.Length] == '/'))
                {
                    path = "%USERPROFILE%" + path.Substring(home.Length);
                }
            }
            catch (Exception)
            {
                // No profile folder to name; the pattern below still applies.
            }
            return UserFolder.Replace(path, "$1<user>");
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
