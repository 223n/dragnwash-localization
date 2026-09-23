using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using DragNWash.ModFramework.Dialogue;
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
            // Back from another tab or a closed window: the language list
            // starts folded again.
            if (Time.frameCount - _toolsDrawnFrame > 2)
            {
                _languagesOpen = false;
            }
            _toolsDrawnFrame = Time.frameCount;

            ToolWindow.Fill(area, ToolWindow.InsetColor);
            float innerWidth = Mathf.Max(100, area.width - 36);
            float width = innerWidth - 12;
            ToolWindow.ApplyScroll(area, ref _localeScroll);
            _localeScroll = GUI.BeginScrollView(area, _localeScroll,
                new Rect(0, 0, innerWidth, Mathf.Max(area.height, _toolsHeight)), false, false);

            float y = DrawLanguages(8, width);
            y = DrawSteps(y + 12, width);
            y = DrawGameTextExports(y + 12, width);
            y = DrawLastResult(y, width);
            y = DrawOtherMods(y, width);

            GUI.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                _toolsHeight = y + 12;
            }

            string busy = BusyText();
            if (busy != null && ShowBusy(busy) && Event.current.type == EventType.Repaint)
            {
                _busyPainted = true;
            }
        }

        // A tool pressed here freezes the game for as long as it runs, so the
        // tab first shows the framework's Busy panel for one frame and Update
        // starts the tool after that (a hotkey runs at once, as before).
        private bool _busyPainted;
        private int _toolAskedFrame = -1;
        private static bool _noBusyPanel;

        private string BusyText()
        {
            if (_pendingWorkingCopy) return "Exporting the working copy...";
            if (_pendingHashFile) return "Hashing strings.csv...";
            if (_pendingLayoutCheck) return "Checking the layout...";
            if (_pendingDump) return "Exporting dialogue...";
            if (_pendingUiDump) return "Exporting UI text...";
            if (_pendingFlowDump) return "Exporting the game flow...";
            return null;
        }

        // False on a framework without the Busy panel (before ModFramework 1.5.0).
        private static bool ShowBusy(string what)
        {
            if (_noBusyPanel)
            {
                return false;
            }
            try
            {
                MarkBusy(what);
                return true;
            }
            catch (MissingMethodException)
            {
                _noBusyPanel = true;
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void MarkBusy(string what)
        {
            ToolWindow.Busy(what);
        }

        // Called by Update: true while a tool asked for from the tab should
        // wait for the Busy panel to be on screen. Not for long: a closed
        // window, or a panel that never shows, lets it go a few frames later.
        private bool HoldToolsForBusy()
        {
            if (BusyText() == null)
            {
                _toolAskedFrame = -1;
                _busyPainted = false;
                return false;
            }
            if (_toolAskedFrame < 0)
            {
                _toolAskedFrame = Time.frameCount;
            }
            bool hold = !_busyPainted && !_noBusyPanel && ToolWindow.IsOpen && Time.frameCount - _toolAskedFrame < 5;
            if (!hold)
            {
                _toolAskedFrame = -1;
                _busyPainted = false;
            }
            return hold;
        }

        private bool _languagesOpen;
        private GUIStyle _languageNameBase;
        private GUIStyle _languageNameStyle;
        private int _toolsDrawnFrame = -10;
        // Set when this tab asked for a language, so Update says how it went.
        private bool _announceLocale;

        // One line, "LANGUAGE  Japanese (ja)  Change...": players switch in
        // Options (Language (Mod)), so here it only needs to be at hand. The
        // list opens on Change... and closes again once a language is picked.
        private float DrawLanguages(float y, float width)
        {
            if (!_languagesOpen)
            {
                string name = LanguageLabel(_pendingLocale ?? TargetLocale.Value);
                // On one line: the label style wraps, and a name measured a
                // pixel short (as Japanese in the fallback font is) went onto
                // a second line, cut off at the top.
                if (!ReferenceEquals(_languageNameBase, S.Label))
                {
                    _languageNameBase = S.Label;
                    _languageNameStyle = OneLine(S.Label);
                }
                float nameWidth = Mathf.Max(0, Mathf.Min(width - 110 - 118, Mathf.Ceil(_languageNameStyle.CalcSize(new GUIContent(name)).x) + 2));
                GUI.Label(new Rect(12, y, 110, RowHeight), "LANGUAGE", S.Label);
                GUI.Label(new Rect(122, y, nameWidth, RowHeight), name, _languageNameStyle);
                if (GUI.Button(new Rect(122 + nameWidth + 12, y, 106, RowHeight), "Change...", S.Button))
                {
                    _languagesOpen = true;
                }
                return y + RowHeight;
            }

            GUI.Label(new Rect(12, y, width - 100, 26), "LANGUAGE", S.Label);
            if (GUI.Button(new Rect(12 + width - 90, y, 90, RowHeight), "Close", S.Button))
            {
                _languagesOpen = false;
            }
            y += 30;
            y = WrappedText(12, y, width - 100, "Pick one. Text on screen changes now, and it's saved for the next start too.", S.WrappedLabel) + 8;

            int columns = width >= 3 * 160 + 16 ? 3 : 2;
            float buttonWidth = (width - 8 * (columns - 1)) / columns;
            for (int i = 0; i < _availableLocales.Length; i++)
            {
                string locale = _availableLocales[i];
                // "> " as well as the colour, so the choice doesn't rest on colour alone.
                bool selected = locale == TargetLocale.Value;
                if (GUI.Button(new Rect(12 + (i % columns) * (buttonWidth + 8), y + (i / columns) * 38, buttonWidth, RowHeight),
                    (selected ? "> " : "") + LanguageLabel(locale), selected ? S.SelectedButton : S.Button))
                {
                    _languagesOpen = false;
                    if (!selected)
                    {
                        _pendingLocale = locale;
                        _pendingLocalePersist = true;
                        _announceLocale = true;
                    }
                }
            }
            if (_availableLocales.Length == 0)
                GUI.Label(new Rect(12, y, width, RowHeight), "No language folders installed.", S.MutedLabel);
            return y + Mathf.Max(1, Mathf.Ceil(_availableLocales.Length / (float)columns)) * 38 - 8;
        }

        // Called by Update once a language picked here is in use.
        private void AnnounceLocale(string locale)
        {
            if (_announceLocale)
            {
                _announceLocale = false;
                ToolWindow.ShowNotice($"Switched to {LanguageLabel(locale)}: {TranslationStore.EntryCount} entries. Saved.");
            }
        }

        // "Name (code)". A name the window font cannot draw (Thai and Hebrew)
        // is given in English instead.
        private string LanguageLabel(string locale)
        {
            string name = LocaleDisplayName(locale);
            if (!MenuFontCanDraw(name))
            {
                name = EnglishLanguageName(locale);
            }
            else
            {
                // The window draws in stored order; a Hebrew name would read backwards.
                name = RightToLeft.ForLeftToRightDrawing(name);
            }
            return name == locale ? locale : $"{name} ({locale})";
        }

        private static readonly Dictionary<string, string> _englishNames = new Dictionary<string, string>();

        // For when a culture is missing from the runtime's own table.
        private static readonly Dictionary<string, string> FallbackEnglishNames = new Dictionary<string, string>
        {
            ["he"] = "Hebrew",
            ["th"] = "Thai",
        };

        private static string EnglishLanguageName(string locale)
        {
            if (_englishNames.TryGetValue(locale, out string cached))
            {
                return cached;
            }
            string name = null;
            try
            {
                name = System.Globalization.CultureInfo.GetCultureInfo(locale).EnglishName;
            }
            catch (Exception)
            {
                // Not a culture this runtime knows.
            }
            if (string.IsNullOrEmpty(name) || name.StartsWith("Unknown", StringComparison.Ordinal) || !IsAscii(name))
            {
                name = FallbackEnglishNames.TryGetValue(locale, out string known) ? known : locale;
            }
            _englishNames[locale] = name;
            return name;
        }

        private static bool IsAscii(string text)
        {
            foreach (char c in text)
            {
                if (c > 127) return false;
            }
            return true;
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
                });

            y = DrawEditStep(y, width, locale);
            y = DrawReviewList(y, width);

            y = StepButton(y, width, "3", _pendingLayoutCheck ? "Checking..." : "Check layout",
                "Finds translated labels that don't fit. Open the screens you want checked first.",
                () =>
                {
                    _pendingLayoutCheck = true;
                });

            y = StepButton(y, width, "4", _pendingHashFile ? "Hashing..." : $"Hash {locale} for commit",
                $"Rebuilds {locale}/strings.csv from the working copy, with no English in it. Do this before a pull request.",
                () =>
                {
                    _pendingHashFile = true;
                });
            return y - 10;
        }

        private float _workingCopyCheckedAt = -10;
        private string _workingCopyCheckedFor;
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
            if (Time.unscaledTime >= _workingCopyCheckedAt + 2f || locale != _workingCopyCheckedFor)
            {
                _workingCopyCheckedAt = Time.unscaledTime;
                _workingCopyCheckedFor = locale;
                _workingCopyExists = File.Exists(WorkingCopy.PathFor(PluginDirectory, locale));
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

        private const int MaxReviewRows = 40;
        private List<LineResolution.Review> _reviews = new List<LineResolution.Review>();
        private int _reviewsCount = -1;
        private float _reviewsAt;

        // Lines whose English a game update changed: the old translation is
        // shown, and the translator should look at them. Only the line ID,
        // node and speaker are listed, never the English itself, and Copy ID
        // puts the ID on the clipboard to search the working copy with. The
        // best guesses (Fuzzy) are the likeliest to be wrong, so they go first.
        private float DrawReviewList(float y, float width)
        {
            int count = LineResolution.ReviewCount;
            if (count == 0)
            {
                return y;
            }
            if (count != _reviewsCount || Time.unscaledTime >= _reviewsAt + 2f)
            {
                _reviewsCount = count;
                _reviewsAt = Time.unscaledTime;
                _reviews = LineResolution.ReviewList;
                _reviews.Sort((a, b) =>
                {
                    int byLayer = ReviewOrder(a.Layer).CompareTo(ReviewOrder(b.Layer));
                    return byLayer != 0 ? byLayer : string.CompareOrdinal(a.LineId ?? a.Key, b.LineId ?? b.Key);
                });
            }

            float x = 12 + StepIndent, w = width - StepIndent;
            GUI.Label(new Rect(x, y, w, 26), $"LINES TO REVIEW  ({count})", S.Label);
            y += 30;
            y = WrappedText(x, y, w,
                "Their English changed in a game update, so the old translation is still shown. Check them in the working copy. The list starts over on every reload and fills again as the lines come up.",
                S.WrappedLabel) + 6;

            const float copyWidth = 90, lineHeight = 24;
            float textWidth = Mathf.Max(60, w - copyWidth - 8);
            for (int i = 0; i < _reviews.Count && i < MaxReviewRows; i++)
            {
                LineResolution.Review r = _reviews[i];
                string id = r.LineId ?? r.Key ?? "";
                float idWidth = Mathf.Min(textWidth, S.Label.CalcSize(new GUIContent(id)).x);
                GUI.Label(new Rect(x, y + 3, idWidth, lineHeight), id, S.Label);
                GUI.Label(new Rect(x + idWidth + 12, y + 3, Mathf.Max(0, textWidth - idWidth - 12), lineHeight),
                    ToolWindow.Drawable($"{r.Node ?? "-"} / {r.Speaker ?? "-"}"), S.MutedLabel);
                GUI.Label(new Rect(x, y + 3 + lineHeight, textWidth, lineHeight), HowMatched(r.Layer),
                    r.Layer == LineMatchLayer.Fuzzy ? TabStyles.Warning : S.MutedLabel);
                if (GUI.Button(new Rect(x + w - copyWidth, y + 3 + (2 * lineHeight - RowHeight) / 2, copyWidth, RowHeight), "Copy ID", S.Button))
                {
                    // The ID only, on this machine's clipboard.
                    GUIUtility.systemCopyBuffer = id;
                    ToolWindow.ShowNotice($"Copied {id}. Search the working copy for it.");
                }
                y += 2 * lineHeight + 8;
                ToolWindow.Fill(new Rect(x, y - 1, w, 1), ToolWindow.PanelColor);
            }
            if (_reviews.Count > MaxReviewRows)
            {
                y = WrappedText(x, y + 4, w, $"... and {_reviews.Count - MaxReviewRows} more. \"tl review\" in the Console lists them all.", S.WrappedLabel);
            }
            return y + 12;
        }

        private static int ReviewOrder(LineMatchLayer layer)
        {
            switch (layer)
            {
                case LineMatchLayer.Fuzzy: return 0;
                case LineMatchLayer.Normalized: return 1;
                case LineMatchLayer.LineId: return 2;
                default: return 3;
            }
        }

        // How the line was recognised, in words rather than the layer's name.
        private static string HowMatched(LineMatchLayer layer)
        {
            switch (layer)
            {
                case LineMatchLayer.Fuzzy: return "English changed, best guess";
                case LineMatchLayer.Normalized: return "Only punctuation or case changed";
                case LineMatchLayer.LineId: return "Same line, English edited";
                default: return layer.ToString();
            }
        }

        private float StepNumber(float y, string number)
        {
            GUI.Label(new Rect(12, y, StepIndent - 12, RowHeight), number, TabStyles.Accent);
            return y;
        }

        private float StepButton(float y, float width, string number, string label, string description, Action press)
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
            public GUIStyle Plain;
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
                    _tabStyles.Plain = Tinted(S.WrappedLabel, S.Label.normal.textColor);
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
            void Export(string label, Action press)
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
            });
            Export(_pendingUiDump ? "Exporting..." : $"UI text  ({DumpUiTextKey.Value})", () =>
            {
                _pendingUiDump = true;
            });
            Export(_pendingFlowDump ? "Exporting..." : "Game flow", () =>
            {
                _pendingFlowDump = true;
            });
            return oneRow ? y + RowHeight : y - 8;
        }

        // ---- the last result -------------------------------------------------

        private enum ResultKind { Done, Warning, Failed }

        private sealed class ToolResult
        {
            public ResultKind Kind;
            public string Title;
            // The notice in the footer; the title when there is nothing shorter.
            public string Short;
            public string Body;
            // The file (or folder) it wrote, for Copy path; null for none.
            public string Path;
        }

        // Set in Update, read by the draw; both run on the main thread.
        private ToolResult _lastResult;

        // Lines the running tool logs, or null when no tool is running. Written
        // under the LogLines lock (Plugin.Log).
        private static List<string> _captured;

        // Runs one tool from Update and keeps what it said for the tab, with a
        // short version as the notice. A tool that returns its message has it
        // logged here; the dumpers and the layout check log their own lines,
        // which are picked up while they run. Only lines with the tool's own
        // tags are kept: a line of dialogue can log a review note meanwhile.
        // A tool that returns its message, and the kind to log it as.
        private delegate string ToolRun(out LogKind kind);

        private void RunTool(string what, string tags, ToolRun run, Func<List<string>, ToolResult> describe)
        {
            var said = new List<string>();
            lock (LogLines)
            {
                _captured = said;
            }
            ToolResult result;
            try
            {
                string message = run(out LogKind kind);
                if (message != null)
                {
                    Log(message, kind);
                }
                lock (LogLines)
                {
                    _captured = null;
                }
                said.RemoveAll(line => !line.StartsWith("[", StringComparison.Ordinal) ||
                    tags.IndexOf(line.Substring(0, Math.Max(1, line.IndexOf(']') + 1)), StringComparison.Ordinal) < 0);
                result = describe(said);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{what} failed: {ex}");
                Log($"[tools] {what} failed: {ex.Message}", LogKind.Error);
                result = new ToolResult { Kind = ResultKind.Failed, Title = what + " failed.", Body = ex.Message };
            }
            finally
            {
                lock (LogLines)
                {
                    _captured = null;
                }
            }
            if (result.Body == null)
            {
                result.Body = BodyOf(said);
            }
            _lastResult = result;
            ShowResultNotice(result.Short ?? result.Title, result.Kind);
        }

        private static bool _plainNoticesOnly;

        // With the colour bar of its kind (ModFramework 1.5.0 and later); a
        // framework without coloured notices shows it plain.
        private static void ShowResultNotice(string message, ResultKind kind)
        {
            if (!_plainNoticesOnly)
            {
                try
                {
                    ShowColouredNotice(message, kind);
                    return;
                }
                catch (Exception ex) when (ex is MissingMethodException || ex is TypeLoadException)
                {
                    _plainNoticesOnly = true;
                }
            }
            ToolWindow.ShowNotice(message);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ShowColouredNotice(string message, ResultKind kind)
        {
            ToolWindow.ShowNotice(message,
                kind == ResultKind.Failed ? NoticeKind.Error : kind == ResultKind.Warning ? NoticeKind.Warning : NoticeKind.Info);
        }

        private void RunWorkingCopy()
        {
            string locale = TargetLocale.Value;
            RunTool("Working copy", "[working]", (out LogKind kind) => WorkingCopy.Export(PluginDirectory, locale, out kind), said =>
            {
                var r = new ToolResult { Path = WorkingCopy.PathFor(PluginDirectory, locale) };
                string m = Said(said, "[working] Wrote");
                if (m == null)
                {
                    r.Kind = ResultKind.Failed;
                    r.Title = "Working copy not written.";
                    r.Path = null;
                    return r;
                }
                r.Kind = m.Contains("No script order data found") ? ResultKind.Warning : ResultKind.Done;
                r.Title = "Working copy written.";
                string rows = Number(m, @"Wrote (\d+) row"), untranslated = Number(m, @"(\d+) still untranslated");
                if (rows != null && untranslated != null)
                {
                    r.Short = $"Working copy written: {rows} rows, {untranslated} untranslated.";
                }
                return r;
            });
        }

        private void RunHash()
        {
            string locale = TargetLocale.Value;
            RunTool("Hashing", "[hash]", (out LogKind kind) => TranslationStore.HashFileInPlace(PluginDirectory, locale, out kind), said =>
            {
                var r = new ToolResult { Path = Path.Combine(PluginDirectory, "Translations", locale, "strings.csv") };
                string m = Said(said, "[hash]");
                if (m == null || !m.Contains(" written"))
                {
                    r.Kind = ResultKind.Failed;
                    r.Title = $"{locale}/strings.csv not rebuilt.";
                    r.Path = null;
                    return r;
                }
                int.TryParse(Number(m, @"(\d+) malformed dropped"), out int dropped);
                bool stale = m.Contains("export it again");
                r.Kind = dropped > 0 || stale ? ResultKind.Warning : ResultKind.Done;
                r.Title = $"{locale}/strings.csv written" +
                    (dropped > 0 ? $", {dropped} row(s) dropped" : "") +
                    (stale ? ", but the working copy looks older than the game" : "") + ".";
                return r;
            });
        }

        private void RunFlowExport()
        {
            RunTool("Game flow export", "[flow][graph][order]", (out LogKind kind) => { kind = LogKind.Result; return FlowDumper.Export(PluginDirectory); }, said =>
            {
                // Three files: level flow, dialogue graph, script order.
                string m = Said(said, "[flow]") ?? "";
                int wrote = Regex.Matches(m, @"\[(flow|graph|order)\] Wrote").Count;
                var r = new ToolResult { Path = Path.Combine(PluginDirectory, "Translations", "_discovered") };
                r.Kind = wrote == 3 ? ResultKind.Done : ResultKind.Warning;
                r.Title = wrote == 3 ? "Game flow exported." : wrote > 0 ? "Game flow exported in part." : "Game flow not exported.";
                if (wrote == 0) r.Path = null;
                return r;
            });
        }

        private void RunDialogueExport()
        {
            RunTool("Dialogue export", "[dump]", (out LogKind kind) => { kind = LogKind.Info; DialogueDumper.DumpAll(PluginDirectory); return null; }, said =>
            {
                var r = new ToolResult { Path = DiscoveredFile("dialogue_lines.csv") };
                string m = Said(said, "[dump] Wrote");
                if (m != null)
                {
                    r.Kind = Said(said, "[dump] Could not") != null ? ResultKind.Warning : ResultKind.Done;
                    r.Title = "Dialogue exported.";
                    string lines = Number(m, @"Wrote (\d+) line");
                    if (lines != null) r.Short = $"Dialogue exported: {lines} lines.";
                    return r;
                }
                r.Path = null;
                bool notYet = Said(said, "[dump] No YarnProject") != null;
                r.Kind = notYet ? ResultKind.Warning : ResultKind.Failed;
                r.Title = notYet ? "No dialogue is loaded yet." : "Dialogue not exported.";
                return r;
            });
        }

        private void RunUiTextExport()
        {
            RunTool("UI text export", "[ui]", (out LogKind kind) => { kind = LogKind.Info; UiTextDumper.DumpAll(PluginDirectory); return null; }, said =>
            {
                var r = new ToolResult { Path = DiscoveredFile("ui_texts.csv") };
                string m = Said(said, "[ui]");
                if (m != null && m.Contains("UI string(s)"))
                {
                    r.Kind = ResultKind.Done;
                    r.Title = "UI text exported.";
                    string count = Number(m, @"\[ui\] (\d+) UI string");
                    if (count != null) r.Short = $"UI text exported: {count} strings.";
                    return r;
                }
                r.Path = null;
                bool none = m != null && m.Contains("No UI text found");
                r.Kind = none ? ResultKind.Warning : ResultKind.Failed;
                r.Title = none ? "No UI text found." : "UI text not exported.";
                return r;
            });
        }

        private void RunLayoutCheck()
        {
            RunTool("Layout check", "[layout]", (out LogKind kind) => { kind = LogKind.Info; LayoutChecker.Report(PluginDirectory, LayoutRiskThreshold.Value); return null; }, said =>
            {
                var r = new ToolResult();
                string m = Said(said, "[layout]") ?? "";
                if (m.Contains("need a look"))
                {
                    r.Kind = ResultKind.Warning;
                    string count = Number(m, @"\[layout\] (\d+) of");
                    r.Title = count != null ? $"{count} label(s) need a look." : "Some labels need a look.";
                    r.Path = DiscoveredFile("layout_risks.csv");
                }
                else if (m.Contains("all fit"))
                {
                    r.Kind = m.Contains("could not be rewritten") ? ResultKind.Warning : ResultKind.Done;
                    r.Title = "Every translated label fits.";
                }
                else if (m.Contains("nothing to check") || m.Contains("No translated text is on screen"))
                {
                    r.Kind = ResultKind.Warning;
                    r.Title = "Nothing to check yet.";
                }
                else
                {
                    r.Kind = ResultKind.Failed;
                    r.Title = "Layout check failed.";
                }
                return r;
            });
        }

        private static string DiscoveredFile(string name) => Path.Combine(PluginDirectory, "Translations", "_discovered", name);

        // The last line the tool logged that starts with the prefix.
        private static string Said(List<string> said, string prefix)
        {
            for (int i = said.Count - 1; i >= 0; i--)
            {
                if (said[i].StartsWith(prefix, StringComparison.Ordinal)) return said[i];
            }
            return null;
        }

        private static string Number(string text, string pattern)
        {
            Match match = Regex.Match(text ?? "", pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        // What the tool logged, one line per message and without the "[tag]"
        // in front. The game flow export puts its three results on one line.
        private static string BodyOf(List<string> said)
        {
            var lines = new List<string>();
            foreach (string message in said)
            {
                foreach (string part in message.Replace("  [", "\n[").Split('\n'))
                {
                    lines.Add(Regex.Replace(part, @"^\[[a-z]+\] ", ""));
                }
            }
            return string.Join("\n", lines);
        }

        private float DrawLastResult(float y, float width)
        {
            ToolResult r = _lastResult;
            if (r == null)
            {
                return y;
            }
            y += 20;
            GUI.Label(new Rect(12, y, width, 26), "LAST RESULT", S.Label);
            y += 30;

            float textX = 12 + 13, textWidth = width - 23;
            var title = new GUIContent(ToolWindow.Drawable(r.Title));
            var body = new GUIContent(ToolWindow.Drawable(r.Body ?? ""));
            float titleHeight = TabStyles.Plain.CalcHeight(title, textWidth);
            float bodyHeight = string.IsNullOrEmpty(r.Body) ? 0 : S.WrappedLabel.CalcHeight(body, textWidth);
            float height = 8 + titleHeight + (bodyHeight > 0 ? 2 + bodyHeight : 0) + (r.Path != null ? 8 + RowHeight : 0) + 8;

            ToolWindow.Fill(new Rect(12, y, width, height), ToolWindow.PanelColor);
            ToolWindow.Fill(new Rect(12, y, 3, height),
                r.Kind == ResultKind.Failed ? ToolWindow.ErrorColor : r.Kind == ResultKind.Warning ? ToolWindow.WarningColor : ToolWindow.AccentColor);
            float ty = y + 8;
            GUI.Label(new Rect(textX, ty, textWidth, titleHeight), title, TabStyles.Plain);
            ty += titleHeight + 2;
            if (bodyHeight > 0)
            {
                GUI.Label(new Rect(textX, ty, textWidth, bodyHeight), body, S.WrappedLabel);
                ty += bodyHeight;
            }
            if (r.Path != null)
            {
                ty += 8;
                if (GUI.Button(new Rect(textX, ty, 120, RowHeight), "Copy path", S.Button))
                {
                    // The path only, on this machine's clipboard.
                    GUIUtility.systemCopyBuffer = r.Path;
                    ToolWindow.ShowNotice("Path copied.");
                }
                if (CanOpenFolders && GUI.Button(new Rect(textX + 128, ty, 120, RowHeight), "Open folder", S.Button))
                {
                    OpenFolder(r.Path);
                }
            }
            return y + height;
        }

        private static bool? _canOpenFolders;

        // Windows only. Under Proton (Steam Deck) the folder would open in
        // Wine's own file manager, if at all, behind the game in Gaming Mode.
        private static bool CanOpenFolders
        {
            get
            {
                if (_canOpenFolders == null)
                {
                    _canOpenFolders = Application.platform == RuntimePlatform.WindowsPlayer && !RunningUnderWine();
                }
                return _canOpenFolders.Value;
            }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                string folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception ex)
            {
                ToolWindow.ShowNotice(ToolWindow.Drawable("Could not open the folder: " + ex.Message));
            }
        }

        // Wine (and Proton) export wine_get_version from their ntdll; Windows
        // does not. The framework's crash reporter tells them apart the same way.
        private static bool RunningUnderWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

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
