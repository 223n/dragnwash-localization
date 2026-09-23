using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using DragNWash.ModFramework;
using DragNWash.ModFramework.ToolWindow;
using UnityEngine;

namespace DragNWashLocalization
{
    // What a line of the Activity log is. It picks the line's colour there and
    // its level in BepInEx's log, which is also what the framework's Console
    // shows and what puts the "!" on the Console tab.
    internal enum LogKind
    {
        // Startup notices and details: Info in BepInEx.
        Info,
        // What came of something the player did (an export, a language switch,
        // a reload): Info in BepInEx, the accent colour here.
        Result,
        // Worth a look, though nothing failed: Warning in BepInEx.
        Warning,
        // Something failed: Error in BepInEx.
        Error,
        // A replaced text, [OK] or [--] ([Debug] VerboseTextLog). Info in
        // BepInEx rather than Debug, so LogOutput.log still has it.
        Text,
    }

    // The Activity log tab: the mod's own lines, newest last, each with its
    // time and its kind's colour.
    public partial class Plugin
    {
        // For each of the two lists below. Only the lines in view are drawn, so
        // this bounds memory rather than the geometry drawn each frame (which
        // the Direct3D 12 bug chokes on).
        private const int MaxLogLines = 100;

        private sealed class LogLine
        {
            public long Seq;
            public DateTime Time;
            public LogKind Kind;
            public string Text;
            // The same line again straight after itself: counted, not added.
            public int Count = 1;
        }

        // Text lines are kept apart from the rest: opening a menu logs dozens
        // of [OK] lines, which would otherwise push a result or a failure out
        // before anyone saw it. LogLines is also the lock for both.
        private static readonly List<LogLine> LogLines = new List<LogLine>();
        private static readonly List<LogLine> TextLines = new List<LogLine>();
        private static LogLine _lastLogLine;
        private static long _logSeq;

        // Count alone stops changing once the buffer is full, which would freeze
        // the rendered log. Bumped on every change instead.
        private static int _logVersion;

        internal static void Log(string message, LogKind kind = LogKind.Info)
        {
            // Logging failures must not propagate into the game's text updates.
            try
            {
                message = message ?? "";
                if (_instance != null)
                {
                    switch (kind)
                    {
                        case LogKind.Error: _instance.Logger.LogError(message); break;
                        case LogKind.Warning: _instance.Logger.LogWarning(message); break;
                        default: _instance.Logger.LogInfo(message); break;
                    }
                }

                lock (LogLines)
                {
                    // A button pressed twice gives the same line twice. Dropping
                    // the second made the press look lost; it is counted instead.
                    if (_lastLogLine != null && _lastLogLine.Text == message && _lastLogLine.Kind == kind)
                    {
                        _lastLogLine.Count++;
                        _lastLogLine.Time = DateTime.Now;
                        _logVersion++;
                        return;
                    }

                    var line = new LogLine { Seq = ++_logSeq, Time = DateTime.Now, Kind = kind, Text = message };
                    List<LogLine> list = kind == LogKind.Text ? TextLines : LogLines;
                    list.Add(line);
                    if (list.Count > MaxLogLines)
                    {
                        list.RemoveAt(0);
                    }
                    _lastLogLine = line;
                    _logVersion++;
                }
            }
            catch
            {
                // Swallow - see comment above.
            }
        }

        // One line as the tab draws it: already passed through Drawable, and
        // measured at the width it was measured for. Copying takes the text as
        // it was logged, characters the window font cannot draw included.
        private struct LogRow
        {
            public long Seq;
            public LogKind Kind;
            public string Drawn;
            public float Height;
            public string Line;
            public string Text;
        }

        private readonly List<LogRow> _logRows = new List<LogRow>();
        private int _logRowsVersion = -1;
        private float _logRowsWidth = -1;
        private float _logContentHeight;
        // Some row had a character the window font could not draw yet, shown
        // as '?'. Drawable prepares it for a later frame where that is safe, so
        // the rows are built again a little later to pick it up.
        private float _logRowsRetryAt = -1;
        private Vector2 _logScroll;
        // Following the end, as the Console does: at the bottom the log
        // follows new lines, scrolled up it stays put, and scrolled back down
        // it follows again.
        private bool _followLog = true;
        // The newest line seen while following; lines after it are the "new
        // lines" the button below the log jumps to.
        private long _logSeenSeq;
        private GUIStyle[] _logStyles;
        private GUIStyle _logToggleOn;
        private GUIStyle _logToggleOff;
        private int _logRowsShown = -1;
        private int _logHidden;
        private int _logHiddenText;

        // Which kinds the tab shows, one bit per LogKind; the toggles above the
        // log change it and the config keeps it.
        internal static ConfigEntry<string> ActivityLogShown;
        private static int _logShownMask = -1;

        private static readonly (LogKind kind, string label)[] LogToggles =
        {
            (LogKind.Error, "Error"), (LogKind.Warning, "Warning"), (LogKind.Result, "Result"), (LogKind.Info, "Info"), (LogKind.Text, "Text"),
        };

        private void BindActivityLogSettings()
        {
            ActivityLogShown = Config.Bind(
                "Debug",
                "ActivityLogShown",
                "Error, Warning, Result, Info, Text",
                new ConfigDescription(
                    "The kinds of line the Activity log in the tool window (F1) shows. The toggles above the log change this.",
                    null, new SettingMeta { Advanced = true }));
            _logShownMask = ParseLogKinds(ActivityLogShown.Value);
            ActivityLogShown.SettingChanged += (sender, args) => _logShownMask = ParseLogKinds(ActivityLogShown.Value);
        }

        // Names this does not know are skipped, so a typo in the config file
        // hides only the kind it misspelt.
        private static int ParseLogKinds(string value)
        {
            int mask = 0;
            foreach (string part in (value ?? "").Split(','))
            {
                foreach (LogKind kind in Enum.GetValues(typeof(LogKind)))
                {
                    if (string.Equals(part.Trim(), kind.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        mask |= 1 << (int)kind;
                    }
                }
            }
            return mask;
        }

        private static string FormatLogKinds(int mask)
        {
            var names = new List<string>();
            foreach (LogKind kind in Enum.GetValues(typeof(LogKind)))
            {
                if ((mask & (1 << (int)kind)) != 0)
                {
                    names.Add(kind.ToString());
                }
            }
            return string.Join(", ", names.ToArray());
        }

        private static readonly Color LogInfoColor = new Color(0.86f, 0.91f, 0.94f);

        private static Color KindColor(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Error: return ToolWindow.ErrorColor;
                case LogKind.Warning: return ToolWindow.WarningColor;
                case LogKind.Result: return ToolWindow.AccentColor;
                case LogKind.Text: return ToolWindow.MutedColor;
                default: return LogInfoColor;
            }
        }

        // The same colours as the framework's Console: Error, Warning, Message
        // (the accent), Info and Debug (muted).
        private void EnsureLogStyles()
        {
            if (_logStyles != null)
            {
                return;
            }
            _logStyles = new GUIStyle[Enum.GetValues(typeof(LogKind)).Length];
            foreach (LogKind kind in Enum.GetValues(typeof(LogKind)))
            {
                var style = new GUIStyle(S.LogLabel) { wordWrap = true, padding = new RectOffset(4, 4, 1, 1) };
                style.normal.textColor = KindColor(kind);
                style.hover.textColor = KindColor(kind);
                _logStyles[(int)kind] = style;
            }
            // The kind toggles, drawn like the Console's: words on a painted
            // panel with room on the left for the square that says on or off.
            _logToggleOn = new GUIStyle(S.Label) { padding = new RectOffset(28, 10, 0, 0), wordWrap = false };
            _logToggleOff = new GUIStyle(S.MutedLabel) { padding = new RectOffset(28, 10, 0, 0), wordWrap = false };
        }

        // Errors and warnings say so in words too, for anyone who cannot tell
        // the colours apart. The other lines already start with their [tag].
        private static string FormatLogLine(DateTime time, LogKind kind, string text, int count)
        {
            string mark = kind == LogKind.Error ? "[E] " : kind == LogKind.Warning ? "[W] " : "";
            string times = count > 1 ? $" (x{count})" : "";
            return time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + mark + text + times;
        }

        private void RebuildLogRows(float width)
        {
            var copies = new List<LogLine>();
            _logHidden = 0;
            _logHiddenText = 0;
            lock (LogLines)
            {
                _logRowsVersion = _logVersion;
                _logRowsShown = _logShownMask;
                // Both lists are in the order the lines came; merged back by it.
                int i = 0, j = 0;
                while (i < LogLines.Count || j < TextLines.Count)
                {
                    LogLine line = j >= TextLines.Count || (i < LogLines.Count && LogLines[i].Seq < TextLines[j].Seq)
                        ? LogLines[i++]
                        : TextLines[j++];
                    if ((_logShownMask & (1 << (int)line.Kind)) == 0)
                    {
                        _logHidden++;
                        if (line.Kind == LogKind.Text) _logHiddenText++;
                        continue;
                    }
                    copies.Add(new LogLine { Seq = line.Seq, Time = line.Time, Kind = line.Kind, Text = line.Text, Count = line.Count });
                }
            }

            _logRows.Clear();
            _logRowsWidth = width;
            _logContentHeight = 0;
            bool missing = false;
            foreach (LogLine line in copies)
            {
                string text = FormatLogLine(line.Time, line.Kind, line.Text, line.Count);
                // Log lines carry text the mod did not choose: another mod's
                // translation read after startup, symbols in the game's own
                // English. Drawing (or measuring) a character the window font
                // has not prepared uploads its atlas mid-frame, which is the
                // Direct3D 12 crash; Drawable shows those as '?' instead.
                string drawn = ToolWindow.Drawable(text);
                missing |= drawn != text;
                float height = _logStyles[(int)line.Kind].CalcHeight(new GUIContent(drawn), width);
                _logRows.Add(new LogRow { Seq = line.Seq, Kind = line.Kind, Drawn = drawn, Height = height, Line = text, Text = line.Text });
                _logContentHeight += height;
            }
            _logRowsRetryAt = missing ? Time.unscaledTime + 1f : -1;
        }

        private void ClearLog()
        {
            lock (LogLines)
            {
                LogLines.Clear();
                TextLines.Clear();
                _lastLogLine = null;
                _logVersion++;
            }
            TranslationStore.ResetAppliedOnceTracking();
            _logScroll = Vector2.zero;
        }

        private void DrawActivityLog(Rect area)
        {
            EnsureLogStyles();
            const float pad = 0;
            float x = area.x + pad, y = area.y + pad, w = area.width - 2 * pad;

            // Kind toggles, wrapping onto more rows in a narrow window, then
            // the buttons on the right, on a row of their own when the toggles
            // leave no room beside them. A toggle that is on has a bar in its
            // kind's colour along the top and a filled square; one that is off
            // has an empty square and dimmer words. Bar and square are painted
            // with Fill, not font glyphs, so the font atlas is untouched.
            float bx = x;
            foreach ((LogKind kind, string label) in LogToggles)
            {
                int bit = 1 << (int)kind;
                bool on = (_logShownMask & bit) != 0;
                float bw = Mathf.Max(70, _logToggleOn.CalcSize(new GUIContent(label)).x);
                if (bx + bw > x + w && bx > x)
                {
                    bx = x;
                    y += RowHeight + 6;
                }
                var r = new Rect(bx, y, bw, RowHeight);
                var square = new Rect(bx + 10, y + (RowHeight - 10) / 2, 10, 10);
                ToolWindow.Fill(r, ToolWindow.PanelColor);
                if (on)
                {
                    ToolWindow.Fill(new Rect(r.x, r.y, r.width, 3), KindColor(kind));
                    ToolWindow.Fill(square, KindColor(kind));
                }
                else
                {
                    ToolWindow.Fill(new Rect(square.x, square.y, square.width, 1), ToolWindow.MutedColor);
                    ToolWindow.Fill(new Rect(square.x, square.yMax - 1, square.width, 1), ToolWindow.MutedColor);
                    ToolWindow.Fill(new Rect(square.x, square.y + 1, 1, square.height - 2), ToolWindow.MutedColor);
                    ToolWindow.Fill(new Rect(square.xMax - 1, square.y + 1, 1, square.height - 2), ToolWindow.MutedColor);
                }
                if (GUI.Button(r, label, on || r.Contains(Event.current.mousePosition) ? _logToggleOn : _logToggleOff))
                {
                    _logShownMask = on ? _logShownMask & ~bit : _logShownMask | bit;
                    if (ActivityLogShown != null)
                    {
                        ActivityLogShown.Value = FormatLogKinds(_logShownMask);
                    }
                }
                bx += bw + 6;
            }
            const float buttonsWidth = 70 + 6 + 110;
            if (x + w - buttonsWidth - bx < 0)
            {
                y += RowHeight + 6;
            }
            if (GUI.Button(new Rect(x + w - buttonsWidth, y, 70, RowHeight), "Copy", S.Button))
            {
                CopyShownLines();
            }
            if (GUI.Button(new Rect(x + w - 110, y, 110, RowHeight), "Clear log", S.Button))
            {
                ClearLog();
                ToolWindow.ShowNotice("Log cleared.");
            }
            y += RowHeight + 6;

            string note = $"{_logRows.Count} / {MaxLogLines}";
            if (_logHidden > 0)
            {
                note += _logHidden == _logHiddenText ? $", {_logHidden} text line(s) hidden" : $", {_logHidden} hidden";
            }
            GUI.Label(new Rect(x, y, w, RowHeight), note, S.MutedLabel);
            y += RowHeight;

            var viewport = new Rect(x, y, w, Mathf.Max(20, area.yMax - pad - y));
            ToolWindow.Fill(viewport, ToolWindow.InsetColor);
            float contentWidth = Mathf.Max(40, viewport.width - 24);
            bool retry = _logRowsRetryAt >= 0 && Time.unscaledTime >= _logRowsRetryAt;
            if (_logRowsVersion != _logVersion || _logRowsShown != _logShownMask ||
                !Mathf.Approximately(_logRowsWidth, contentWidth) || retry)
            {
                RebuildLogRows(contentWidth);
            }

            // Lines that came while the log was scrolled up: a button in the
            // corner says how many and goes down to them. Where it is, the
            // lines under it take no clicks, since IMGUI hands a click to the
            // control drawn first and the lines are drawn before the button.
            int fresh = 0;
            for (int i = _logRows.Count - 1; i >= 0 && _logRows[i].Seq > _logSeenSeq; i--)
            {
                fresh++;
            }
            string freshLabel = fresh == 1 ? "1 new line  v" : $"{fresh} new lines  v";
            float freshWidth = S.Button.CalcSize(new GUIContent(freshLabel)).x + 16;
            var freshRect = new Rect(viewport.xMax - 20 - 8 - freshWidth, viewport.yMax - RowHeight - 8, freshWidth, RowHeight);
            bool showFresh = !_followLog && fresh > 0;
            bool overFresh = showFresh && freshRect.Contains(Event.current.mousePosition);

            float bottom = Mathf.Max(0, _logContentHeight - viewport.height);
            if (_followLog)
            {
                _logScroll.y = bottom;
            }
            Vector2 before = _logScroll;
            ToolWindow.ApplyScroll(viewport, ref _logScroll);
            // The scroll view takes the wheel event for itself, so it is
            // looked at before.
            bool wheel = Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition);
            _logScroll = GUI.BeginScrollView(viewport, _logScroll,
                new Rect(0, 0, contentWidth, Mathf.Max(viewport.height - 1, _logContentHeight)), false, true);
            if (_logRows.Count == 0)
            {
                GUI.Label(new Rect(12, 12, contentWidth - 24, 64),
                    "No activity yet.\nOpen a game menu or dialogue to capture text.", S.WrappedLabel);
            }
            // Only the lines in view are drawn.
            float ry = 0;
            foreach (LogRow row in _logRows)
            {
                if (ry + row.Height >= _logScroll.y && ry <= _logScroll.y + viewport.height)
                {
                    // Click a line to copy it; the line under the pointer gets
                    // the panel colour behind it.
                    var rowRect = new Rect(0, ry, contentWidth, row.Height);
                    bool hot = !overFresh && rowRect.Contains(Event.current.mousePosition);
                    if (hot && Event.current.type == EventType.Repaint)
                    {
                        ToolWindow.Fill(rowRect, ToolWindow.PanelColor);
                    }
                    GUI.Label(rowRect, row.Drawn, _logStyles[(int)row.Kind]);
                    if (!overFresh && GUI.Button(rowRect, new GUIContent("", "Click a line to copy it."), GUIStyle.none))
                    {
                        GUIUtility.systemCopyBuffer = row.Text;
                        string shown = row.Text.Length > 80 ? row.Text.Substring(0, 77) + "..." : row.Text;
                        ToolWindow.ShowNotice(ToolWindow.Drawable("Copied: " + shown));
                    }
                }
                ry += row.Height;
            }
            GUI.EndScrollView();
            if (wheel)
            {
                _followLog = false;
            }
            if (_logScroll.y >= bottom - 2)
            {
                _followLog = true;
            }
            else if (_logScroll != before)
            {
                _followLog = false;
            }

            if (_followLog || fresh == 0)
            {
                _logSeenSeq = _logRows.Count > 0 ? Math.Max(_logSeenSeq, _logRows[_logRows.Count - 1].Seq) : _logSeenSeq;
            }
            else if (showFresh && GUI.Button(freshRect, freshLabel, S.Button))
            {
                _followLog = true;
            }
        }

        // The lines the toggles leave shown, with their times, for a bug
        // report or a note.
        private void CopyShownLines()
        {
            if (_logRows.Count == 0)
            {
                ToolWindow.ShowNotice("Nothing to copy.");
                return;
            }
            var lines = new string[_logRows.Count];
            for (int i = 0; i < _logRows.Count; i++)
            {
                lines[i] = _logRows[i].Line;
            }
            GUIUtility.systemCopyBuffer = string.Join("\n", lines);
            ToolWindow.ShowNotice(lines.Length == 1 ? "Copied 1 line." : $"Copied {lines.Length} lines.");
        }
    }
}
