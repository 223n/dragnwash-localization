using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Only the lines in view are drawn, so this bounds memory rather than
        // the geometry drawn each frame (which the Direct3D 12 bug chokes on).
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

        private static readonly List<LogLine> LogBuffer = new List<LogLine>();
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

                lock (LogBuffer)
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
                    LogBuffer.Add(line);
                    if (LogBuffer.Count > MaxLogLines)
                    {
                        LogBuffer.RemoveAt(0);
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
        // measured at the width it was measured for.
        private struct LogRow
        {
            public long Seq;
            public LogKind Kind;
            public string Drawn;
            public float Height;
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
        private bool _followLog = true;
        private bool _logNeedsScroll;
        private GUIStyle[] _logStyles;

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
            lock (LogBuffer)
            {
                _logRowsVersion = _logVersion;
                foreach (LogLine line in LogBuffer)
                {
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
                _logRows.Add(new LogRow { Seq = line.Seq, Kind = line.Kind, Drawn = drawn, Height = height });
                _logContentHeight += height;
            }
            _logRowsRetryAt = missing ? Time.unscaledTime + 1f : -1;
        }

        private void ClearLog()
        {
            lock (LogBuffer)
            {
                LogBuffer.Clear();
                _lastLogLine = null;
                _logVersion++;
            }
            TranslationStore.ResetAppliedOnceTracking();
            _logScroll = Vector2.zero;
        }

        private void DrawActivityLog(Rect area)
        {
            EnsureLogStyles();
            if (GUI.Button(new Rect(area.x, area.y, 122, RowHeight), _followLog ? "Follow: ON" : "Follow: OFF",
                _followLog ? S.SelectedButton : S.Button))
            {
                _followLog = !_followLog;
                _logNeedsScroll = _followLog;
            }
            if (GUI.Button(new Rect(area.x + 130, area.y, 110, RowHeight), "Clear log", S.Button))
            {
                ClearLog();
                ToolWindow.ShowNotice("Log cleared.");
            }

            var viewport = new Rect(area.x, area.y + 40, area.width, Mathf.Max(20, area.height - 40));
            ToolWindow.Fill(viewport, ToolWindow.InsetColor);
            float contentWidth = Mathf.Max(40, viewport.width - 24);
            bool retry = _logRowsRetryAt >= 0 && Time.unscaledTime >= _logRowsRetryAt;
            if (_logRowsVersion != _logVersion || !Mathf.Approximately(_logRowsWidth, contentWidth) || retry)
            {
                bool grew = _logRowsVersion != _logVersion;
                RebuildLogRows(contentWidth);
                if (grew && _followLog) _logNeedsScroll = true;
            }

            Event current = Event.current;
            if (viewport.Contains(current.mousePosition) &&
                (current.type == EventType.ScrollWheel ||
                 (current.type == EventType.MouseDown && current.mousePosition.x >= viewport.xMax - 20)))
            {
                _followLog = false;
                _logNeedsScroll = false;
            }
            float maxScroll = Mathf.Max(0, _logContentHeight - viewport.height);
            _logScroll.y = _logNeedsScroll ? maxScroll : Mathf.Clamp(_logScroll.y, 0, maxScroll);
            _logNeedsScroll = false;

            if (ToolWindow.ApplyScroll(viewport, ref _logScroll)) { _followLog = false; _logNeedsScroll = false; }
            _logScroll = GUI.BeginScrollView(viewport, _logScroll,
                new Rect(0, 0, contentWidth, Mathf.Max(viewport.height - 1, _logContentHeight)), false, true);
            if (_logRows.Count == 0)
            {
                GUI.Label(new Rect(12, 12, contentWidth - 24, 64),
                    "No activity yet.\nOpen a game menu or dialogue to capture text.", S.WrappedLabel);
            }
            // Only the lines in view are drawn.
            float y = 0;
            foreach (LogRow row in _logRows)
            {
                if (y + row.Height >= _logScroll.y && y <= _logScroll.y + viewport.height)
                {
                    GUI.Label(new Rect(0, y, contentWidth, row.Height), row.Drawn, _logStyles[(int)row.Kind]);
                }
                y += row.Height;
            }
            GUI.EndScrollView();

            GUI.Label(new Rect(area.x + 250, area.y, Mathf.Max(0, area.width - 250), RowHeight),
                $"{_logRows.Count} / {MaxLogLines}", S.MutedLabel);
        }
    }
}
