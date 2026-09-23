using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DragNWash.ModFramework.Saves;
using DragNWash.ModFramework.ToolWindow;

namespace DragNWashLocalization
{
    // The Saves tab: the save slots, their level and event flags, and the
    // history of snapshots the framework's Saves library keeps.
    public partial class Plugin
    {
        private int _editLevel;
        private int _editLevelBase = -2;
        private string _editLevelSlot;
        private bool _progressConfirm;
        private bool _newestMatchesSave;
        private bool _showFlags;
        private List<SaveFlag> _savesFlags = new List<SaveFlag>();
        private string _flagFilter = "";
        private bool _flagClearConfirm;
        private bool _showOnceLines;

        // One row of the flag editor: catalog entry (may be null) + save state.
        private sealed class FlagRow
        {
            public string Id;
            public string Group;
            public string Description;
            public bool? Value;   // null = never set in this save
            public bool IsHeader;
        }
        private List<FlagRow> _flagRows = new List<FlagRow>();

        private void RebuildFlagRows()
        {
            _flagRows.Clear();
            var inSave = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (SaveFlag f in _savesFlags)
            {
                inSave[f.Id] = f.Value;
            }

            string filter = (_flagFilter ?? "").Trim();
            bool Match(string id, string desc) =>
                filter.Length == 0 ||
                id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (desc != null && desc.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            var groups = new List<string>();
            var byGroup = new Dictionary<string, List<FlagRow>>(StringComparer.Ordinal);
            void Add(string group, FlagRow row)
            {
                if (!byGroup.TryGetValue(group, out List<FlagRow> list))
                {
                    list = new List<FlagRow>();
                    byGroup[group] = list;
                    groups.Add(group);
                }
                list.Add(row);
            }

            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlagInfo e in GameFlags.Catalog)
            {
                known.Add(e.Id);
                if (!Match(e.Id, e.Description)) continue;
                Add(e.Group, new FlagRow
                {
                    Id = e.Id, Group = e.Group, Description = e.Description,
                    Value = inSave.TryGetValue(e.Id, out bool v) ? v : (bool?)null,
                });
            }
            foreach (SaveFlag f in _savesFlags)
            {
                if (known.Contains(f.Id) || !Match(f.Id, null)) continue;
                bool once = f.Id.StartsWith("Yarn.Internal.Once.", StringComparison.Ordinal);
                if (once && !_showOnceLines) continue;
                Add(once ? "Once-only dialogue lines" : "Other (in save, not in catalog)",
                    new FlagRow { Id = f.Id, Value = f.Value, Description = once ? "one-time line already said" : "" });
            }

            foreach (string g in groups)
            {
                _flagRows.Add(new FlagRow { IsHeader = true, Id = g, Group = g });
                _flagRows.AddRange(byGroup[g]);
            }
        }

        private const float FlagOnceWidth = 130, FlagResetWidth = 170;

        // The search box, the once-lines toggle and Reset all share a row when
        // the search box still gets a useful width; otherwise the two buttons
        // go on a row of their own under it.
        private static bool FlagControlsOnOneRow(float innerWidth) =>
            innerWidth - 24 - FlagOnceWidth - FlagResetWidth - 16 >= 140;

        private static readonly GUIContent FlagClearQuestion = new GUIContent(
            "Set every flag in this save to false (the level is kept). The current save is kept as a snapshot first. Continue?");

        // Where the flag rows start in the scroll view: under the controls and,
        // while it is asked, the Reset all question.
        private float FlagRowsTop(float innerWidth)
        {
            float top = 4 + (FlagControlsOnOneRow(innerWidth) ? 34 : 68);
            if (_flagClearConfirm)
            {
                top += S.WrappedLabel.CalcHeight(FlagClearQuestion, innerWidth - 24) + 4 + 38;
            }
            return top;
        }

        private float FlagRowsHeight(float innerWidth) => FlagRowsTop(innerWidth) + _flagRows.Count * 30 + 8;

        private Vector2 _savesScroll;
        private string _savesSlot;
        private float _savesRefreshAt;
        private List<string> _savesSlots = new List<string>();
        private List<SaveSnapshot> _savesList = new List<SaveSnapshot>();
        private int _savesLevel = -1;

        // How long a held control may hold the refresh back. Without a limit a
        // press that never gets its release - the window loses focus mid-click,
        // say - would freeze the listing for the rest of the session.
        private const float SavesRefreshHoldLimit = 5f;

        // Snapshots of the game's own save file, one per write, newest first.
        // Restore puts one back; the player then reloads the slot from the
        // title screen. Listing is cached and refreshed every couple of
        // seconds so OnGUI does not hit the disk every frame.
        private void DrawSaves(Rect area)
        {
            ToolWindow.Fill(area, ToolWindow.InsetColor);
            float innerWidth = Mathf.Max(100, area.width - 36);

            // Not while a control is held. Both lists below are addressed by
            // index, and a snapshot taken between the press and the release
            // shifts every row down without changing the control ids, so the
            // click would land on a different entry than the one under the
            // cursor. GUI.Button releases the control before it returns true,
            // so the handlers that set _savesRefreshAt = 0 still take effect on
            // the next pass.
            bool held = GUIUtility.hotControl != 0 &&
                        Time.unscaledTime < _savesRefreshAt + SavesRefreshHoldLimit;

            if (Time.unscaledTime >= _savesRefreshAt && !held)
            {
                _savesRefreshAt = Time.unscaledTime + 2f;
                _savesSlots = GameSaves.Slots();
                if (_savesSlot == null || !_savesSlots.Contains(_savesSlot))
                {
                    // The slot played last, whatever order the buttons are in.
                    _savesSlot = null;
                    DateTime newest = DateTime.MinValue;
                    foreach (string slot in _savesSlots)
                    {
                        DateTime written = File.GetLastWriteTimeUtc(GameSaves.SavePath(slot));
                        if (_savesSlot == null || written > newest)
                        {
                            _savesSlot = slot;
                            newest = written;
                        }
                    }
                }
                _savesList = _savesSlot != null ? GameSaves.Snapshots(_savesSlot) : new List<SaveSnapshot>();
                _savesFlags = _savesSlot != null ? GameSaves.ReadFlags(_savesSlot) : new List<SaveFlag>();
                _savesLevel = _savesSlot != null ? GameSaves.ReadLevel(_savesSlot) : -1;
                _newestMatchesSave = _savesSlot != null && _savesList.Count > 0 && GameSaves.SnapshotMatchesSave(_savesSlot, _savesList[0]);
                RebuildFlagRows();
            }

            float y = 8;
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "SAVE SLOT", S.Label);
            y += 32;
            float x = 12;
            foreach (string slot in _savesSlots)
            {
                string shown = GameSaves.ShortName(slot);
                float w = 90;
                if (GUI.Button(new Rect(area.x + x, area.y + y, w, RowHeight), shown, slot == _savesSlot ? S.SelectedButton : S.Button))
                {
                    _savesSlot = slot;
                    // The rest of the listing reloads at the top of the next
                    // pass, but the progress editor below reads the level in
                    // this one, and it must not show the slot we just left.
                    _savesLevel = GameSaves.ReadLevel(slot);
                    _savesRefreshAt = 0;
                }
                x += w + 8;
            }
            if (_savesSlots.Count == 0)
                GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, RowHeight), "No save files found.", S.MutedLabel);
            y += 42;

            if (_savesSlot != null)
            {
                y = DrawSavesProgress(area, y, innerWidth);
            }

            // The flag editor replaces the history list while it is open, so
            // the flags start at the top instead of below 30 snapshot rows.
            bool flagsOpen = _showFlags && _savesSlot != null;

            // The heading and the footer both wrap on a narrow window, so size
            // them from the text instead of assuming one line.
            var headingText = new GUIContent(flagsOpen
                ? $"EVENT FLAGS  ({GameSaves.ShortName(_savesSlot)}, {_savesFlags.Count} set)"
                : $"HISTORY  ({Count(_savesList.Count, "snapshot")}, newest first)");
            float headingHeight = S.Label.CalcHeight(headingText, innerWidth);
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, headingHeight), headingText, S.Label);
            y += headingHeight + 6;

            var footerText = new GUIContent(flagsOpen
                ? "Click a value to change it: unset, then true, then false and back. The save before the change is kept as a snapshot."
                : "A snapshot is taken every time the game writes the save. After a restore, go to the title screen and load the slot. Saving in game writes over it again.");
            float footerHeight = S.MutedLabel.CalcHeight(footerText, innerWidth);
            // The footer sits at the bottom; in a window too short for it, it
            // is left out rather than drawn over the rows above.
            bool footerFits = area.height - y - footerHeight - 12 >= 60;
            if (!footerFits)
            {
                footerHeight = 0;
            }

            var view = new Rect(area.x, area.y + y, area.width, Mathf.Max(40, area.height - y - footerHeight - 12));
            float contentHeight = flagsOpen ? FlagRowsHeight(innerWidth) : _savesList.Count * 36;
            ToolWindow.ApplyScroll(view, ref _savesScroll);
            _savesScroll = GUI.BeginScrollView(view, _savesScroll, new Rect(0, 0, innerWidth, Mathf.Max(view.height, contentHeight)), false, false);
            if (flagsOpen)
            {
                DrawFlagRows(innerWidth);
            }
            else
            {
                DrawSnapshotRows(innerWidth);
            }
            GUI.EndScrollView();

            if (footerFits)
            {
                GUI.Label(new Rect(area.x + 12, area.y + area.height - footerHeight - 6, innerWidth, footerHeight), footerText, S.MutedLabel);
            }
        }

        private static string Count(int n, string what) => n == 1 ? "1 " + what : $"{n} {what}s";

        // The level editor, and the switch between the history and the flags.
        // Returns where the next part starts.
        private float DrawSavesProgress(Rect area, float y, float innerWidth)
        {
            // Cached with the rest of the listing: ReadLevel reads and parses
            // the whole save file, and OnGUI runs several times per rendered
            // frame.
            int current = _savesLevel;
            if (_editLevelSlot != _savesSlot || _editLevelBase != current)
            {
                _editLevelSlot = _savesSlot;
                _editLevelBase = current;
                _editLevel = current;
                _progressConfirm = false;
            }

            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "PROGRESS", S.Label);
            y += 32;
            GUI.Label(new Rect(area.x + 12, area.y + y, 200, RowHeight),
                _editLevel == current ? $"level {current}" : $"level {current}  ->  {_editLevel}", S.Label);
            if (GUI.Button(new Rect(area.x + 216, area.y + y, 40, RowHeight), "-", S.Button) && _editLevel > 0)
            {
                _editLevel--; _progressConfirm = false;
            }
            if (GUI.Button(new Rect(area.x + 262, area.y + y, 40, RowHeight), "+", S.Button))
            {
                _editLevel++; _progressConfirm = false;
            }
            bool canApply = _editLevel != current && current >= 0;
            GUI.enabled = canApply;
            if (GUI.Button(new Rect(area.x + 314, area.y + y, 90, RowHeight), "Apply", S.Button))
            {
                if (_editLevel > current)
                {
                    _progressConfirm = true;   // going forward can spoil the story
                }
                else
                {
                    Log("[saves] " + GameSaves.SetLevel(PluginGuid, _savesSlot, _editLevel));
                    _savesRefreshAt = 0;
                }
            }
            GUI.enabled = true;

            // History | Flags at the right end of the row; in a narrow window
            // on a row of its own instead of over the buttons.
            const float historyWidth = 90, flagsWidth = 80;
            float switchX = area.x + 12 + innerWidth - historyWidth - 8 - flagsWidth;
            if (switchX < area.x + 414)
            {
                y += 36;
                switchX = area.x + 12;
            }
            if (GUI.Button(new Rect(switchX, area.y + y, historyWidth, RowHeight), "History", _showFlags ? S.Button : S.SelectedButton))
            {
                _showFlags = false;
            }
            if (GUI.Button(new Rect(switchX + historyWidth + 8, area.y + y, flagsWidth, RowHeight), "Flags", _showFlags ? S.SelectedButton : S.Button))
            {
                _showFlags = true;
            }
            y += 36;

            if (_progressConfirm)
            {
                var warn = new GUIContent($"Warning: jumping ahead to level {_editLevel} may spoil content you have not seen. Continue?");
                float wh = S.Label.CalcHeight(warn, innerWidth);
                GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, wh), warn, S.Label);
                y += wh + 4;
                if (GUI.Button(new Rect(area.x + 12, area.y + y, 120, RowHeight), "Yes, continue", S.Button))
                {
                    Log("[saves] " + GameSaves.SetLevel(PluginGuid, _savesSlot, _editLevel));
                    _progressConfirm = false;
                    _savesRefreshAt = 0;
                }
                if (GUI.Button(new Rect(area.x + 140, area.y + y, 90, RowHeight), "Cancel", S.Button))
                {
                    _progressConfirm = false;
                    _editLevel = current;
                }
                y += 36;
            }
            return y;
        }

        // Inside the scroll view.
        private void DrawSnapshotRows(float innerWidth)
        {
            for (int i = 0; i < _savesList.Count; i++)
            {
                SaveSnapshot s = _savesList[i];
                // "current" only when the newest snapshot really is the save on
                // disk; after a progress or flag edit it is the pre-edit state.
                bool isCurrent = i == 0 && _newestMatchesSave;
                GUI.Label(new Rect(12, i * 36, innerWidth - 130, RowHeight), (isCurrent ? "current   " : "") + s.Label, S.Label);
                if (!isCurrent && GUI.Button(new Rect(innerWidth - 110, i * 36, 98, RowHeight), "Restore", S.Button))
                {
                    _pendingRestoreSlot = _savesSlot;
                    _pendingRestoreSnapshot = s;
                    _savesRefreshAt = 0;
                }
            }
        }

        // Inside the scroll view.
        private void DrawFlagRows(float innerWidth)
        {
            float fy = 4;

            // Search box, once-lines toggle, and the bulk reset.
            int dbg = FlagPanelDebug != null ? FlagPanelDebug.Value : 0;
            bool oneRow = FlagControlsOnOneRow(innerWidth);
            float searchWidth = oneRow ? innerWidth - 24 - FlagOnceWidth - FlagResetWidth - 16 : innerWidth - 24;
            if ((dbg & 1) == 0)
            {
                var searchRect = new Rect(12, fy, searchWidth, RowHeight);
                string newFilter = GUI.TextField(searchRect, _flagFilter ?? "", S.TextField);
                if (string.IsNullOrEmpty(newFilter))
                {
                    GUI.Label(new Rect(searchRect.x + 6, searchRect.y, searchRect.width - 6, searchRect.height), "Search flags", S.MutedLabel);
                }
                if (newFilter != _flagFilter)
                {
                    _flagFilter = newFilter;
                    RebuildFlagRows();
                }
            }
            float bx = oneRow ? 12 + searchWidth + 8 : 12;
            if (!oneRow)
            {
                fy += 34;
            }
            if (GUI.Button(new Rect(bx, fy, FlagOnceWidth, RowHeight), _showOnceLines ? "Hide once-lines" : "Show once-lines", S.Button))
            {
                _showOnceLines = !_showOnceLines;
                RebuildFlagRows();
            }
            if (GUI.Button(new Rect(bx + FlagOnceWidth + 8, fy, FlagResetWidth, RowHeight), "Reset all to false...", _flagClearConfirm ? S.SelectedButton : S.Button))
            {
                _flagClearConfirm = !_flagClearConfirm;
            }
            fy += 34;

            if (_flagClearConfirm)
            {
                float qh = S.WrappedLabel.CalcHeight(FlagClearQuestion, innerWidth - 24);
                GUI.Label(new Rect(12, fy, innerWidth - 24, qh), FlagClearQuestion, S.WrappedLabel);
                fy += qh + 4;
                if (GUI.Button(new Rect(12, fy, 120, RowHeight), "Yes, reset", S.Button))
                {
                    var all = new List<KeyValuePair<string, bool>>();
                    foreach (SaveFlag f in _savesFlags) all.Add(new KeyValuePair<string, bool>(f.Id, false));
                    Log("[saves] " + GameSaves.SetFlags(PluginGuid, _savesSlot, all, $"all {all.Count} flags set to false"));
                    _flagClearConfirm = false;
                    _savesRefreshAt = 0;
                }
                if (GUI.Button(new Rect(140, fy, 90, RowHeight), "Cancel", S.Button))
                {
                    _flagClearConfirm = false;
                }
                fy += 38;
            }

            for (int i = 0; i < _flagRows.Count && (dbg & 4) == 0; i++)
            {
                FlagRow r = _flagRows[i];
                float ry = fy + i * 30;
                if (r.IsHeader)
                {
                    if ((dbg & 8) == 0)
                        GUI.Label(new Rect(12, ry + 4, innerWidth - 24, 26), r.Id.ToUpperInvariant(), S.Label);
                    continue;
                }
                GUI.Label(new Rect(24, ry, Mathf.Max(60, innerWidth * 0.42f), RowHeight), r.Id, S.Label);
                if ((dbg & 2) == 0)
                    GUI.Label(new Rect(24 + Mathf.Max(60, innerWidth * 0.42f), ry, Mathf.Max(40, innerWidth * 0.58f - 130), RowHeight), r.Description ?? "", S.MutedLabel);
                string shown = r.Value == null ? "unset" : (r.Value.Value ? "true" : "false");
                GUIStyle st = r.Value == true ? S.SelectedButton : S.Button;
                if (GUI.Button(new Rect(innerWidth - 90, ry, 78, RowHeight), shown, st))
                {
                    bool next = r.Value != true;   // unset -> true, true -> false, false -> true
                    Log("[saves] " + GameSaves.SetFlag(PluginGuid, _savesSlot, r.Id, next));
                    _savesRefreshAt = 0;
                }
            }
        }
    }
}
