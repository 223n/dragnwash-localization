using System.Collections.Generic;
using UnityEngine;
using DragNWash.ModFramework.ToolWindow;

namespace DragNWashLocalization
{
    // The Translation tab of the tool window (F1). It is laid out in the order
    // the README and CONTRIBUTING give: export the working copy, edit and save
    // it, check the layout, hash it for commit. The raw exports of the game's
    // text come after, since they are only needed for a new language or after
    // a game update. The buttons only ask; the tools run in Update (Plugin.cs).
    //
    // Every literal is ASCII, for the reason given above DrawAbout in
    // Plugin.ImGui.cs.
    public partial class Plugin
    {
        private const float StepIndent = 34;

        // Measured from the last draw, so a description that wraps in a narrow
        // window makes the content taller instead of being cut off.
        private float _toolsHeight = 600;

        private void DrawTools(Rect area)
        {
            ToolWindow.Fill(area, ToolWindow.InsetColor);
            float innerWidth = Mathf.Max(100, area.width - 36);
            float width = innerWidth - 12;
            ToolWindow.ApplyScroll(area, ref _localeScroll);
            _localeScroll = GUI.BeginScrollView(area, _localeScroll,
                new Rect(0, 0, innerWidth, Mathf.Max(area.height, _toolsHeight)), false, false);

            float y = DrawLanguages(8, width);
            y = DrawSteps(y + 12, width);
            y = DrawGameTextExports(y + 12, width);
            y = DrawOtherMods(y, width);

            GUI.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                _toolsHeight = y + 12;
            }
        }

        private float DrawLanguages(float y, float width)
        {
            GUI.Label(new Rect(12, y, width, 26), "LANGUAGE", S.Label);
            y += 34;
            float buttonWidth = (width - 16) / 3;
            for (int i = 0; i < _availableLocales.Length; i++)
            {
                string locale = _availableLocales[i];
                bool selected = locale == TargetLocale.Value;
                string label = LocaleDisplayName(locale);
                if (!MenuFontCanDraw(label)) label = locale;
                if (GUI.Button(new Rect(12 + (i % 3) * (buttonWidth + 8), y + (i / 3) * 38, buttonWidth, RowHeight),
                    selected ? label + "  [active]" : label, selected ? S.SelectedButton : S.Button))
                {
                    _pendingLocale = locale;
                    _pendingLocalePersist = true;
                    ToolWindow.ShowNotice("See Activity log for the language change result.");
                }
            }
            if (_availableLocales.Length == 0)
                GUI.Label(new Rect(12, y, width, RowHeight), "No language folders installed.", S.MutedLabel);
            return y + Mathf.Max(1, Mathf.Ceil(_availableLocales.Length / 3f)) * 38 - 8;
        }

        // 1 working copy, 2 edit and save, 3 layout check, 4 hash. The hash
        // button names the language: it rewrites that language's strings.csv,
        // whichever one the translator was looking at before.
        private float DrawSteps(float y, float width)
        {
            string locale = TargetLocale.Value;
            GUI.Label(new Rect(12, y, width, 26), "TRANSLATING  " + locale, S.Label);
            y += 34;
            if (locale == "en")
            {
                return WrappedText(12, y, width,
                    "English is the game's own text, so there is nothing to translate. Pick the language you're working on above.", S.WrappedLabel);
            }

            y = StepButton(y, width, "1", _pendingWorkingCopy ? "Exporting..." : "Export working copy",
                $"Writes _discovered/{WorkingCopy.FileNameFor(locale)} with the English beside each line. Edits already in it are kept.",
                () =>
                {
                    _pendingWorkingCopy = true;
                    ToolWindow.ShowNotice("See Activity log for the working copy result.");
                });

            y = DrawEditStep(y, width, locale);

            y = StepButton(y, width, "3", _pendingLayoutCheck ? "Checking..." : "Check layout",
                "Finds translated labels that don't fit. Open the screens you want checked first.",
                () =>
                {
                    _pendingLayoutCheck = true;
                    ToolWindow.ShowNotice("See Activity log for the layout check result.");
                });

            y = StepButton(y, width, "4", _pendingHashFile ? "Hashing..." : $"Hash {locale} for commit",
                $"Rebuilds {locale}/strings.csv from the working copy, with no English in it. Do this before a pull request.",
                () =>
                {
                    _pendingHashFile = true;
                    ToolWindow.ShowNotice("See Activity log for the hashing result.");
                });
            return y - 10;
        }

        private float _workingCopyCheckedAt = -10;
        private bool _workingCopyExists;

        // Step 2: which file hot reload is watching, when it last read it and
        // what changed, or in the warning colour why it could not read it (a
        // spreadsheet program holding the file, usually). "Reload now" does
        // what the console's "tl reload" does, for when hot reload is off.
        private float DrawEditStep(float y, float width, string locale)
        {
            StepNumber(y, "2");
            const float reloadWidth = 120;
            float x = 12 + StepIndent;
            float textWidth = width - StepIndent;
            bool buttonBeside = textWidth - reloadWidth - 8 >= S.Label.CalcSize(new GUIContent("Edit the working copy and save it.")).x;
            GUI.Label(new Rect(x, y, textWidth, RowHeight), "Edit the working copy and save it.", S.Label);
            if (!buttonBeside)
            {
                y += RowHeight + 4;
            }
            if (GUI.Button(new Rect(buttonBeside ? x + textWidth - reloadWidth : x, y, reloadWidth, RowHeight),
                _pendingReload ? "Reloading..." : "Reload now", S.Button))
            {
                _pendingReload = true;
            }
            y += RowHeight + 4;

            // Checked now and then rather than on every draw.
            if (Time.unscaledTime >= _workingCopyCheckedAt + 2f)
            {
                _workingCopyCheckedAt = Time.unscaledTime;
                _workingCopyExists = System.IO.File.Exists(WorkingCopy.PathFor(PluginDirectory, locale));
            }
            string watched = _workingCopyExists ? "_discovered/" + WorkingCopy.FileNameFor(locale) : locale + "/strings.csv";

            string problem = HotReload.Problem ?? TranslationStore.LastLoadProblem;
            string status;
            if (problem != null)
            {
                status = problem;
            }
            else
            {
                status = HotReloadTranslations.Value
                    ? $"Hot reload on, watching {watched}."
                    : "Hot reload is off ([Debug] HotReloadTranslations). Press Reload now after you save.";
                if (HotReload.LastReload.HasValue)
                {
                    status += $" Last reload {HotReload.LastReload.Value:HH:mm:ss}: {HotReload.LastChanges}.";
                }
            }
            return WrappedText(x, y, textWidth, ToolWindow.Drawable(status), problem != null ? TabStyles.Warning : S.WrappedLabel) + 10;
        }

        private float StepNumber(float y, string number)
        {
            GUI.Label(new Rect(12, y, StepIndent - 12, RowHeight), number, TabStyles.Accent);
            return y;
        }

        private float StepButton(float y, float width, string number, string label, string description, System.Action press)
        {
            StepNumber(y, number);
            float buttonWidth = Mathf.Min(width - StepIndent, Mathf.Max(160, S.Button.CalcSize(new GUIContent(label)).x + 24));
            if (GUI.Button(new Rect(12 + StepIndent, y, buttonWidth, RowHeight), label, S.Button))
            {
                press();
            }
            return WrappedText(12 + StepIndent, y + RowHeight + 4, width - StepIndent, description, S.WrappedLabel) + 10;
        }

        // The window's styles in the colours this tab needs. Made again when
        // the window makes its own styles again.
        private sealed class TranslationTabStyles
        {
            public GUIStyle From;
            public GUIStyle Accent;
            public GUIStyle Warning;
        }
        private readonly TranslationTabStyles _tabStyles = new TranslationTabStyles();

        private TranslationTabStyles TabStyles
        {
            get
            {
                if (_tabStyles.From != S.WrappedLabel || _tabStyles.Accent == null)
                {
                    _tabStyles.From = S.WrappedLabel;
                    _tabStyles.Accent = Tinted(S.Label, ToolWindow.AccentColor);
                    _tabStyles.Warning = Tinted(S.WrappedLabel, ToolWindow.WarningColor);
                }
                return _tabStyles;
            }
        }

        private static GUIStyle Tinted(GUIStyle from, Color color)
        {
            var style = new GUIStyle(from);
            style.normal.textColor = color;
            style.hover.textColor = color;
            return style;
        }

        private float WrappedText(float x, float y, float width, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            float height = style.CalcHeight(content, width);
            GUI.Label(new Rect(x, y, width, height), content, style);
            return y + height;
        }

        // Dialogue, UI text and game flow: the game's own text, for starting a
        // language or catching up with a game update. One row when it fits.
        private float DrawGameTextExports(float y, float width)
        {
            GUI.Label(new Rect(12, y, width, 26), "GAME TEXT EXPORTS", S.Label);
            y += 30;
            y = WrappedText(12, y, width, "For a new language or after a game update. Written to Translations/_discovered.", S.WrappedLabel) + 8;

            bool oneRow = width >= 3 * 150 + 16;
            float buttonWidth = oneRow ? (width - 16) / 3 : width;
            float x = 12;
            void Export(string label, System.Action press)
            {
                if (GUI.Button(new Rect(x, y, buttonWidth, RowHeight), label, S.Button))
                {
                    press();
                }
                if (oneRow) x += buttonWidth + 8;
                else y += 38;
            }
            Export(_pendingDump ? "Exporting..." : $"Dialogue  ({DumpDialogueKey.Value})", () =>
            {
                _pendingDump = true;
                ToolWindow.ShowNotice("See Activity log for the dialogue export result.");
            });
            Export(_pendingUiDump ? "Exporting..." : $"UI text  ({DumpUiTextKey.Value})", () =>
            {
                _pendingUiDump = true;
                ToolWindow.ShowNotice("See Activity log for the UI text export result.");
            });
            Export(_pendingFlowDump ? "Exporting..." : "Game flow", () =>
            {
                _pendingFlowDump = true;
                ToolWindow.ShowNotice("See Activity log for the flow export result.");
            });
            return oneRow ? y + RowHeight : y - 8;
        }

        private float DrawOtherMods(float y, float width)
        {
            List<string> modLines = OtherModsLines();
            if (modLines.Count == 0)
            {
                return y;
            }
            y += 20;
            GUI.Label(new Rect(12, y, width, 26), "OTHER MODS (EXPERIMENTAL)", S.Label);
            y += 34;
            foreach (string line in modLines)
            {
                GUI.Label(new Rect(12, y, width, 26), ToolWindow.Drawable(line), S.MutedLabel);
                y += 26;
            }
            return y;
        }

        private const int MaxConflictsShown = 10;

        // The packs of other mods read for this language and the lines they
        // disagree on (experimental, off by default: nothing to show then).
        private static List<string> OtherModsLines()
        {
            var lines = new List<string>();
            if (!ModTranslations.Enabled)
            {
                return lines;
            }
            if (ModTranslations.Packs.Count == 0)
            {
                lines.Add("No other mod ships a translation for this language.");
                return lines;
            }
            foreach (ModTranslations.Pack pack in ModTranslations.Packs)
            {
                lines.Add($"{pack.Name}: {pack.Rows} line(s)" + (pack.Conflicts > 0 ? $", {pack.Conflicts} conflict(s)" : "") + (pack.Problem != null ? $" ({pack.Problem})" : ""));
            }
            int shown = 0;
            foreach (ModTranslations.Conflict c in ModTranslations.Conflicts)
            {
                if (shown++ >= MaxConflictsShown) break;
                lines.Add($"Conflict: \"{TranslationStore.DescribeKey(c.Key)}\": {c.Other} \"{c.Kept}\" kept, {c.Mod} \"{c.Dropped}\" left out");
            }
            if (ModTranslations.ConflictCount > MaxConflictsShown)
            {
                lines.Add($"... and {ModTranslations.ConflictCount - MaxConflictsShown} more conflict(s) in the Activity log and BepInEx/LogOutput.log.");
            }
            return lines;
        }
    }
}
