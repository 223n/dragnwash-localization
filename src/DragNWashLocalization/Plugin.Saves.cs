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
        // The history row that holds what the save holds now, or -1.
        private int _currentIndex = -1;
        private bool _saveExists;
        // Snapshot files don't change once written, so each is read once. The
        // time it was written is kept with it: framework 1.4.3 writes a second
        // snapshot taken in the same second over the first, under its name.
        private readonly Dictionary<string, KeyValuePair<DateTime, string>> _snapshotText =
            new Dictionary<string, KeyValuePair<DateTime, string>>(StringComparer.OrdinalIgnoreCase);
        private bool _showFlags;
        private List<SaveFlag> _savesFlags = new List<SaveFlag>();
        private string _flagFilter = "";
        // ToolWindow.Confirm's ids: one question at a time in the window.
        private const string ConfirmLevelAhead = PluginGuid + ".saves.levelahead";
        private const string ConfirmResetFlags = PluginGuid + ".saves.resetflags";
        // The slot a question was asked on. Picking another slot hides it,
        // since its Yes would change the slot now picked, and it runs out on
        // its own.
        private string _confirmSlot;

        private void AskAboutSlot(string id)
        {
            _confirmSlot = _savesSlot;
            ToolWindow.AskConfirm(id);
        }

        private bool AskingAboutSlot(string id) => ToolWindow.IsConfirming(id) && _confirmSlot == _savesSlot;
        private bool _showOnceLines;

        // One row of the flag editor: catalog entry (may be null) + save state.
        private sealed class FlagRow
        {
            public string Id;
            public string Group;
            public string Description;
            public bool? Value;   // null = never set in this save
            public bool IsHeader;
            // As drawn: through ToolWindow.Drawable, since a flag another mod
            // wrote into the save can have any characters, and a header in
            // capitals. Made with the row, not on every pass.
            public string ShownId;
            public string ShownDescription;
            public string HintText;
            // Cut to the columns, for the width they were cut for.
            public float CutFor = -1;
            public string CutId;
            public string CutDescription;
        }
        private List<FlagRow> _flagRows = new List<FlagRow>();
        // Reset all to false has something to do: a flag in the save is true.
        private bool _anyFlagTrue;

        // Flag values clicked but not written yet, per slot (id -> the value it
        // gets). Apply writes them in one edit, so a round of clicking costs
        // one snapshot instead of one per click, and the history the game
        // wrote is not pushed out by it.
        private readonly Dictionary<string, Dictionary<string, bool>> _flagEdits =
            new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);
        // Set when the tab was left, or the window closed, with some of them
        // still not written; the band turns yellow until the next click.
        private bool _flagEditsLeft;
        private int _savesDrawnFrame = -1;

        private Dictionary<string, bool> FlagEdits(string slot, bool create)
        {
            if (slot == null)
            {
                return null;
            }
            if (!_flagEdits.TryGetValue(slot, out Dictionary<string, bool> edits) && create)
            {
                edits = new Dictionary<string, bool>(StringComparer.Ordinal);
                _flagEdits[slot] = edits;
            }
            return edits;
        }

        private int FlagEditCount(string slot) => FlagEdits(slot, false)?.Count ?? 0;

        // After the save was read again: a change the save already has (the
        // game or another mod wrote it meanwhile) is no longer a change.
        private void PruneFlagEdits()
        {
            Dictionary<string, bool> edits = FlagEdits(_savesSlot, false);
            if (edits == null)
            {
                return;
            }
            foreach (SaveFlag f in _savesFlags)
            {
                if (edits.TryGetValue(f.Id, out bool v) && v == f.Value)
                {
                    edits.Remove(f.Id);
                }
            }
            if (edits.Count == 0)
            {
                _flagEdits.Remove(_savesSlot);
            }
        }

        private void RebuildFlagRows()
        {
            _flagRows.Clear();
            var inSave = new Dictionary<string, bool>(StringComparer.Ordinal);
            _anyFlagTrue = false;
            foreach (SaveFlag f in _savesFlags)
            {
                inSave[f.Id] = f.Value;
                _anyFlagTrue |= f.Value;
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
                _flagRows.Add(new FlagRow { IsHeader = true, Id = g, Group = g, ShownId = ToolWindow.Drawable(g.ToUpperInvariant()) });
                foreach (FlagRow row in byGroup[g])
                {
                    row.ShownId = ToolWindow.Drawable(row.Id);
                    row.ShownDescription = ToolWindow.Drawable(row.Description ?? "");
                    row.HintText = row.ShownDescription.Length == 0 ? row.ShownId : row.ShownId + ": " + row.ShownDescription;
                    _flagRows.Add(row);
                }
            }
        }

        private const float FlagOnceWidth = 130, FlagResetWidth = 170;

        // The search box, the once-lines toggle and Reset all share a row when
        // the search box still gets a useful width; otherwise the two buttons
        // go on a row of their own under it.
        private static bool FlagControlsOnOneRow(float innerWidth) =>
            innerWidth - 24 - FlagOnceWidth - FlagResetWidth - 16 >= 140;

        // Where the flag rows start in the scroll view: under the controls and,
        // while it is asked, the Reset all question.
        private float FlagRowsTop(float innerWidth)
        {
            float top = 4 + (FlagControlsOnOneRow(innerWidth) ? 34 : 68);
            if (AskingAboutSlot(ConfirmResetFlags))
            {
                top += 38;
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

            // The tab is drawn every frame while it shows; a gap means it was
            // left or the window was closed.
            if (_savesDrawnFrame >= 0 && Time.frameCount - _savesDrawnFrame > 2 && _flagEdits.Count > 0)
            {
                _flagEditsLeft = true;
            }
            _savesDrawnFrame = Time.frameCount;

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
                string saveText = _savesSlot != null ? ReadText(GameSaves.SavePath(_savesSlot)) : null;
                FindCurrentSnapshot(saveText);
                MakeShortLabels();
                CheckUndo(saveText);
                PruneFlagEdits();
                RebuildFlagRows();
            }

            float y = 8;
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "SAVE SLOT", S.Label);
            y += 32;
            float x = 12;
            foreach (string slot in _savesSlots)
            {
                // "*" on a slot with flag changes not written yet: its band only
                // shows while it is the one picked.
                string shown = FlagEditCount(slot) > 0 ? GameSaves.ShortName(slot) + " *" : GameSaves.ShortName(slot);
                float w = 90;
                if (GUI.Button(new Rect(area.x + x, area.y + y, w, RowHeight), shown, slot == _savesSlot ? S.SelectedButton : S.Button))
                {
                    if (slot != _savesSlot)
                    {
                        _savesScroll = Vector2.zero;
                    }
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
                : _savesList.Count >= GameSaves.Keep
                    ? $"HISTORY  ({_savesList.Count} of {GameSaves.Keep} kept, the oldest drops off next)"
                    : $"HISTORY  ({_savesList.Count} of {GameSaves.Keep} kept, newest first)");
            float headingHeight = S.Label.CalcHeight(headingText, innerWidth);
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, headingHeight), headingText, S.Label);
            y += headingHeight + 6;

            // In both views, so flag changes not written yet never go out of sight.
            int edits = FlagEditCount(_savesSlot);
            if (edits > 0)
            {
                y = DrawFlagEditsBand(area, y, innerWidth, edits, _flagEditsLeft || !flagsOpen);
            }

            var footerText = new GUIContent(flagsOpen
                ? (edits == 0 ? "Click a value to change it. Nothing is written until you press Apply."
                    : edits == 1 ? "Apply writes it. The save before it is kept as a snapshot." + OldestDropsOff()
                    : $"Apply writes all {edits} in one go. The save before it is kept as one snapshot." + OldestDropsOff())
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
            float contentHeight = flagsOpen ? FlagRowsHeight(innerWidth) : (_savesList.Count + (ShowSaveNotListed ? 1 : 0)) * 36;
            ToolWindow.ApplyScroll(view, ref _savesScroll);
            _pointerInList = Event.current != null && view.Contains(Event.current.mousePosition);
            _savesScroll = GUI.BeginScrollView(view, _savesScroll, new Rect(0, 0, innerWidth, Mathf.Max(view.height, contentHeight)), false, false);
            // Only the rows in sight are drawn: a save can hold hundreds of
            // flags, and IMGUI draws the tab several times a frame.
            float visibleTop = _savesScroll.y, visibleBottom = _savesScroll.y + view.height;
            if (flagsOpen)
            {
                DrawFlagRows(innerWidth, visibleTop, visibleBottom);
            }
            else
            {
                DrawSnapshotRows(innerWidth, visibleTop, visibleBottom);
            }
            GUI.EndScrollView();

            if (footerFits)
            {
                GUI.Label(new Rect(area.x + 12, area.y + area.height - footerHeight - 6, innerWidth, footerHeight), footerText, S.MutedLabel);
            }
        }

        // Hints are made only for what the pointer is on: IMGUI draws the tab
        // several times a frame. In the list the pointer also has to be inside
        // it, not on the note under it, where a row scrolled out of sight lies.
        private bool _pointerInList;

        private bool PointerOn(Rect rect, bool inList)
        {
            Event ev = Event.current;
            return ev != null && (!inList || _pointerInList) && rect.Contains(ev.mousePosition);
        }

        private static string Count(int n, string what) => n == 1 ? "1 " + what : $"{n} {what}s";

        // "3 flag changes not written yet  [Apply] [Discard]" on a band with a
        // 3 px bar: accent while they are being clicked, yellow once the tab
        // was left with them or the history is showing.
        private float DrawFlagEditsBand(Rect area, float y, float innerWidth, int edits, bool warn)
        {
            var band = new Rect(area.x + 12, area.y + y, innerWidth, RowHeight + 6);
            ToolWindow.Fill(band, ToolWindow.PanelColor);
            ToolWindow.Fill(new Rect(band.x, band.y, 3, band.height), warn ? ToolWindow.WarningColor : ToolWindow.AccentColor);
            const float applyWidth = 90, discardWidth = 90;
            float bx = band.xMax - 8 - discardWidth - 8 - applyWidth;
            float room = bx - 8 - (band.x + 12);
            string text = $"{Count(edits, "flag change")} not written yet";
            if (S.Label.CalcSize(new GUIContent(text)).x > room)
            {
                text = $"{edits} not written yet";
            }
            GUI.Label(new Rect(band.x + 12, band.y + 3, Mathf.Max(0, room), RowHeight), text, S.Label);
            if (GUI.Button(new Rect(bx, band.y + 3, applyWidth, RowHeight), "Apply", S.SelectedButton))
            {
                ApplyFlagEdits();
            }
            if (GUI.Button(new Rect(bx + applyWidth + 8, band.y + 3, discardWidth, RowHeight), "Discard", S.Button))
            {
                _flagEdits.Remove(_savesSlot);
                _flagEditsLeft = false;
            }
            return y + band.height + 8;
        }

        // One SetFlags for every change clicked on this slot: one write, and
        // one snapshot of the save before it.
        private void ApplyFlagEdits()
        {
            Dictionary<string, bool> edits = FlagEdits(_savesSlot, false);
            if (edits == null || edits.Count == 0)
            {
                return;
            }
            var list = new List<KeyValuePair<string, bool>>(edits);
            var parts = new List<string>();
            foreach (KeyValuePair<string, bool> kv in list)
            {
                parts.Add($"{kv.Key} = {(kv.Value ? "true" : "false")}");
            }
            string what = list.Count == 1 ? parts[0] : $"{list.Count} flags changed";
            // Kept for another try when the write failed. Ones the save turns
            // out to have already go at the next refresh (PruneFlagEdits).
            if (RunSaveEdit(_savesSlot, () => GameSaves.SetFlags(PluginGuid, _savesSlot, list, what),
                list.Count > 1 ? $" ({string.Join(", ", parts)})" : ""))
            {
                _flagEdits.Remove(_savesSlot);
            }
            _flagEditsLeft = false;
        }

        // What "Undo last change" puts back: the snapshot of the save from just
        // before the last change made here, while the save still holds what
        // that change wrote.
        private string _undoSlot;
        private SaveSnapshot _undoSnapshot;
        private string _undoWritten;
        private bool _undoValid;

        private static string ReadText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex VersionEntry =
            new System.Text.RegularExpressions.Regex("^\\[\\s*\\{\\s*\"version\"\\s*:\\s*\\d+\\s*\\}\\s*,?\\s*");

        // A snapshot from before the game update of 2026-09-14 comes back with
        // a {"version":1} entry added in front, so that entry is left out of
        // the comparison.
        private static string ForCompare(string text)
        {
            return text == null ? null : VersionEntry.Replace(text.Trim(), "[", 1);
        }

        // The snapshot each slot was last restored from in this session. Two
        // snapshots can hold the same save; CURRENT goes on the one the
        // player put back rather than on the newer twin.
        private readonly Dictionary<string, string> _restoredFrom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Every row is compared, not just the newest: after a restore or an
        // edit, the save matches an older row or none at all. Of several
        // matches, the one restored from wins, else the newest.
        private void FindCurrentSnapshot(string saveText)
        {
            _saveExists = saveText != null;
            _currentIndex = -1;
            string save = ForCompare(saveText);
            _restoredFrom.TryGetValue(_savesSlot ?? "", out string restoredFrom);
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _savesList.Count; i++)
            {
                SaveSnapshot snapshot = _savesList[i];
                listed.Add(snapshot.Path);
                string text;
                if (_snapshotText.TryGetValue(snapshot.Path, out KeyValuePair<DateTime, string> known) && known.Key == snapshot.Taken)
                {
                    text = known.Value;
                }
                else
                {
                    text = ForCompare(ReadText(snapshot.Path));
                    if (text != null)
                    {
                        _snapshotText[snapshot.Path] = new KeyValuePair<DateTime, string>(snapshot.Taken, text);
                    }
                }
                if (save != null && text == save &&
                    (_currentIndex < 0 || (restoredFrom != null && string.Equals(snapshot.Path, restoredFrom, StringComparison.OrdinalIgnoreCase))))
                {
                    _currentIndex = i;
                }
            }
            // Files pushed out of the history, or of another slot.
            if (_snapshotText.Count > listed.Count)
            {
                var gone = new List<string>();
                foreach (string path in _snapshotText.Keys)
                {
                    if (!listed.Contains(path)) gone.Add(path);
                }
                foreach (string path in gone) _snapshotText.Remove(path);
            }
        }

        // The row above the history when the save matches none of it.
        private bool ShowSaveNotListed => _saveExists && _currentIndex < 0;

        // Every change to a save goes through here. The result goes to the
        // notice strip as well as the log: it says the slot has to be loaded
        // again from the title screen, which the player would otherwise never
        // see. Returns true when the save changed.
        private bool RunSaveEdit(string slot, Func<string> edit, string logDetail = "")
        {
            string savePath = GameSaves.SavePath(slot);
            string before = ReadText(savePath);
            string result = edit();
            Log("[saves] " + result + logDetail);
            // A flag's id is in it, and another mod's flag can have any characters.
            ToolWindow.ShowNotice(ToolWindow.Drawable(result), result.StartsWith("Edit failed", StringComparison.Ordinal) ? NoticeKind.Error : NoticeKind.Info);
            _savesRefreshAt = 0;
            string after = ReadText(savePath);
            if (before == null || after == null || after == before)
            {
                return false;
            }

            // The library kept the save from before the change as a snapshot:
            // a new one, or the newest one when it already held the same.
            _undoValid = false;
            foreach (SaveSnapshot s in GameSaves.Snapshots(slot))
            {
                if (ReadText(s.Path) == before)
                {
                    _undoSlot = slot;
                    _undoSnapshot = s;
                    _undoWritten = after;
                    _undoValid = true;
                    break;
                }
            }
            return true;
        }

        // At the refresh: Undo stays only while the save is what the change
        // wrote. Once the game saved, or something was restored, it would put
        // back something other than what the player expects.
        private void CheckUndo(string saveText)
        {
            _undoValid = _undoSnapshot != null && _undoSlot == _savesSlot && saveText != null &&
                         saveText == _undoWritten && File.Exists(_undoSnapshot.Path);
        }

        // After a restore went through. Flag changes clicked on the save it
        // replaced don't belong on the one put back, and a later Apply would
        // write them over it. Returns true when there were any.
        private bool DropFlagEditsAfterRestore(string slot)
        {
            if (slot == null || !_flagEdits.Remove(slot))
            {
                return false;
            }
            _flagEditsLeft = _flagEdits.Count > 0 && _flagEditsLeft;
            return true;
        }

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
            }

            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "PROGRESS", S.Label);
            y += 32;
            // The level takes what the buttons leave, up to 200, so Apply stays
            // inside the narrowest window.
            float levelWidth = Mathf.Clamp(innerWidth - 40 - 6 - 40 - 12 - 90 - 4, 100, 200);
            float bx = area.x + 12 + levelWidth + 4;
            GUI.Label(new Rect(area.x + 12, area.y + y, levelWidth, RowHeight),
                _editLevel == current ? $"level {current}" : $"level {current}  ->  {_editLevel}", LineLabel(false));
            if (GUI.Button(new Rect(bx, area.y + y, 40, RowHeight), "-", S.Button) && _editLevel > 0)
            {
                _editLevel--;
            }
            if (GUI.Button(new Rect(bx + 46, area.y + y, 40, RowHeight), "+", S.Button))
            {
                _editLevel++;
            }
            bool canApply = _editLevel != current && current >= 0;
            bool askingAhead = AskingAboutSlot(ConfirmLevelAhead) && _editLevel > current;
            GUI.enabled = canApply;
            if (GUI.Button(new Rect(bx + 98, area.y + y, 90, RowHeight), "Apply", askingAhead ? S.SelectedButton : S.Button))
            {
                if (_editLevel > current)
                {
                    AskAboutSlot(ConfirmLevelAhead);   // going forward can spoil the story
                }
                else
                {
                    RunSaveEdit(_savesSlot, () => GameSaves.SetLevel(PluginGuid, _savesSlot, _editLevel));
                }
            }
            GUI.enabled = true;

            // Undo last change after Apply, then History | Flags at the right
            // end. What does not fit goes on a second row, decided by the
            // width alone so nothing jumps when Undo comes and goes.
            const float undoWidth = 150, historyWidth = 90, flagsWidth = 80;
            float right = area.x + 12 + innerWidth;
            float undoX = bx + 98 + 90 + 10;
            bool undoOnFirst = undoX + undoWidth <= right;
            bool switchOnFirst = undoX + undoWidth + 8 + historyWidth + 8 + flagsWidth <= right;
            float switchX = switchOnFirst ? right - flagsWidth - 8 - historyWidth : area.x + 12;
            float switchY = switchOnFirst ? y : y + 36;
            if (_undoValid && _undoSlot == _savesSlot)
            {
                var undoRect = undoOnFirst
                    ? new Rect(undoX, area.y + y, undoWidth, RowHeight)
                    : new Rect(switchX + historyWidth + 8 + flagsWidth + 8, area.y + switchY, undoWidth, RowHeight);
                if (PointerOn(undoRect, false))
                {
                    ToolWindow.Hint($"Puts back {When(_undoSnapshot)} (level {_undoSnapshot.Level}), the save from just before this change." + OldestDropsOff() + DroppedByRestore());
                }
                if (GUI.Button(undoRect, "Undo last change", S.Button))
                {
                    // A restore like the History's: the save it replaces is
                    // kept as a snapshot too, so the undo can be undone.
                    _pendingRestoreSlot = _undoSlot;
                    _pendingRestoreSnapshot = _undoSnapshot;
                    _undoValid = false;
                    _savesRefreshAt = 0;
                }
            }
            // Each list starts at its top: the history's scroll means nothing
            // in the flags.
            if (GUI.Button(new Rect(switchX, area.y + switchY, historyWidth, RowHeight), "History", _showFlags ? S.Button : S.SelectedButton) && _showFlags)
            {
                _showFlags = false;
                _savesScroll = Vector2.zero;
            }
            if (GUI.Button(new Rect(switchX + historyWidth + 8, area.y + switchY, flagsWidth, RowHeight), "Flags", _showFlags ? S.SelectedButton : S.Button) && !_showFlags)
            {
                _showFlags = true;
                _savesScroll = Vector2.zero;
            }
            y = switchY + 36;

            // Only while the level asked about is still ahead; otherwise the
            // question runs out on its own.
            if (askingAhead)
            {
                if (ToolWindow.Confirm(new Rect(area.x + 12, area.y + y, innerWidth, RowHeight), ConfirmLevelAhead,
                    $"Jump ahead to level {_editLevel}? It may spoil what you haven't seen.", "Yes, jump ahead",
                    $"Yes sets {GameSaves.ShortName(_savesSlot)} to level {_editLevel}. The save as it is now is kept, and Undo last change puts it back." + OldestDropsOff() + " Cancel or 5 s leaves it. Esc = Cancel."))
                {
                    RunSaveEdit(_savesSlot, () => GameSaves.SetLevel(PluginGuid, _savesSlot, _editLevel));
                }
                y += 36;
            }
            return y;
        }

        // S.Label and S.MutedLabel wrap. A history or flag row has room for
        // one line, so its text stops at the column's edge instead of
        // spilling into the rows around it.
        private GUIStyle _lineBasis, _lineLabel, _lineMuted;

        private GUIStyle LineLabel(bool muted)
        {
            if (!ReferenceEquals(_lineBasis, S.Label))
            {
                _lineBasis = S.Label;
                _lineLabel = new GUIStyle(S.Label) { wordWrap = false, clipping = TextClipping.Clip };
                _lineMuted = new GUIStyle(S.MutedLabel) { wordWrap = false, clipping = TextClipping.Clip };
            }
            return muted ? _lineMuted : _lineLabel;
        }

        private const string SaveNotListedText = "The save now isn't in the list yet. It changed after the newest snapshot.";
        private float _notListedWidth = -1;
        private string _notListedShown;

        // "09-22 22:15  level 1", or the time alone for today: for a window
        // too narrow for the whole date. Made at the refresh.
        private readonly List<string> _savesShortLabels = new List<string>();
        private float _savesLabelFitWidth = -1;
        private bool _savesLabelsFit = true;

        private void MakeShortLabels()
        {
            _savesShortLabels.Clear();
            foreach (SaveSnapshot s in _savesList)
            {
                _savesShortLabels.Add((s.Taken.Date == DateTime.Today ? s.Taken.ToString("HH:mm:ss") : s.Taken.ToString("MM-dd HH:mm")) + "  level " + s.Level);
            }
        }

        // Inside the scroll view; rows outside visibleTop..visibleBottom are
        // skipped.
        private void DrawSnapshotRows(float innerWidth, float visibleTop, float visibleBottom)
        {
            // Every row keeps a column for the CURRENT mark, so the dates line up.
            const float markWidth = 80, restoreWidth = 80;
            float labelWidth = innerWidth - 12 - markWidth - restoreWidth - 20;
            if (labelWidth != _savesLabelFitWidth)
            {
                // Measured once per width: every full label is as long as this one.
                _savesLabelFitWidth = labelWidth;
                _savesLabelsFit = LineLabel(false).CalcSize(new GUIContent("2026-09-23 00:00:00  level 00")).x <= labelWidth;
            }
            float y = 0;
            if (ShowSaveNotListed)
            {
                DrawCurrentMark(y, innerWidth);
                var notListed = new Rect(12 + markWidth, y, innerWidth - 24 - markWidth, RowHeight);
                if (notListed.width != _notListedWidth)
                {
                    _notListedWidth = notListed.width;
                    _notListedShown = ToolWindow.Elide(SaveNotListedText, LineLabel(true), notListed.width - 4);
                }
                GUI.Label(notListed, _notListedShown, LineLabel(true));
                if (_notListedShown != SaveNotListedText && PointerOn(notListed, true))
                {
                    ToolWindow.Hint(SaveNotListedText);
                }
                y += 36;
            }
            for (int i = 0; i < _savesList.Count; i++, y += 36)
            {
                if (y + RowHeight < visibleTop || y > visibleBottom)
                {
                    continue;
                }
                SaveSnapshot s = _savesList[i];
                bool isCurrent = i == _currentIndex;
                if (isCurrent)
                {
                    DrawCurrentMark(y, innerWidth);
                }
                string label = _savesLabelsFit || i >= _savesShortLabels.Count ? s.Label : _savesShortLabels[i];
                GUI.Label(new Rect(12 + markWidth, y, labelWidth, RowHeight), label, LineLabel(false));
                // Nothing to restore on the row the save already is.
                var restoreRect = new Rect(innerWidth - 12 - restoreWidth, y, restoreWidth, RowHeight);
                if (!isCurrent && PointerOn(restoreRect, true))
                {
                    ToolWindow.Hint($"Puts {When(s)} (level {s.Level}) back as {GameSaves.ShortName(_savesSlot)}'s save. The save it replaces is kept." + OldestDropsOff() + DroppedByRestore());
                }
                if (!isCurrent && GUI.Button(restoreRect, "Restore", S.Button))
                {
                    _pendingRestoreSlot = _savesSlot;
                    _pendingRestoreSnapshot = s;
                    _savesRefreshAt = 0;
                }
            }
        }

        // The P4 selection: the row on the panel colour with a 2 px accent
        // line on its left, and the tag in the accent colour.
        private void DrawCurrentMark(float y, float innerWidth)
        {
            ToolWindow.Fill(new Rect(4, y - 2, innerWidth - 8, RowHeight + 4), ToolWindow.PanelColor);
            ToolWindow.Fill(new Rect(4, y - 2, 2, RowHeight + 4), ToolWindow.AccentColor);
            Color previous = GUI.contentColor;
            GUI.contentColor = ToolWindow.AccentColor;
            GUI.Label(new Rect(12, y, 76, RowHeight), "CURRENT", S.Tag);
            GUI.contentColor = previous;
        }

        // Said wherever a change is about to be made: with the history full, the
        // snapshot of the save as it is now pushes the oldest one out. None is
        // taken when the newest snapshot already holds the save.
        private string OldestDropsOff()
        {
            if (_savesList.Count == 0 || _savesList.Count < GameSaves.Keep || _currentIndex == 0)
            {
                return "";
            }
            return $" The oldest snapshot ({When(_savesList[_savesList.Count - 1])}) drops off to make room.";
        }

        // Said on Restore and Undo last change while the slot has flag changes
        // not written yet: a restore drops them (DropFlagEditsAfterRestore).
        private string DroppedByRestore() =>
            FlagEditCount(_savesSlot) > 0 ? " Your changes that aren't applied yet will be dropped." : "";

        // "13:20:40" for today, the date as well for another day.
        private static string When(SaveSnapshot s) =>
            s.Taken.Date == DateTime.Today ? s.Taken.ToString("HH:mm:ss") : s.Taken.ToString("yyyy-MM-dd HH:mm:ss");

        // Inside the scroll view; rows outside visibleTop..visibleBottom are
        // skipped.
        private void DrawFlagRows(float innerWidth, float visibleTop, float visibleBottom)
        {
            float fy = 4;

            // Search box, once-lines toggle, and the bulk reset.
            int dbg = FlagPanelDebug != null ? FlagPanelDebug.Value : 0;
            bool oneRow = FlagControlsOnOneRow(innerWidth);
            float searchWidth = oneRow ? innerWidth - 24 - FlagOnceWidth - FlagResetWidth - 16 : innerWidth - 24;
            if ((dbg & 1) == 0)
            {
                string newFilter = ToolWindow.FilterField(new Rect(12, fy, searchWidth, RowHeight), _flagFilter, "Search flags", S);
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
            // Nothing to reset while no flag in the save is true; an edit that
            // changes nothing would still keep a snapshot.
            bool askingReset = AskingAboutSlot(ConfirmResetFlags);
            GUI.enabled = _anyFlagTrue || askingReset;
            if (GUI.Button(new Rect(bx + FlagOnceWidth + 8, fy, FlagResetWidth, RowHeight), "Reset all to false...", askingReset ? S.SelectedButton : S.Button))
            {
                AskAboutSlot(ConfirmResetFlags);
            }
            GUI.enabled = true;
            fy += 34;

            if (askingReset)
            {
                int pending = FlagEditCount(_savesSlot);
                if (ToolWindow.Confirm(new Rect(12, fy, innerWidth - 24, RowHeight), ConfirmResetFlags,
                    $"Set all {_savesFlags.Count} flags to false? The level is kept.", "Yes, reset",
                    $"Yes sets every flag in {GameSaves.ShortName(_savesSlot)} to false. The save as it is now is kept, and Undo last change puts it back." + OldestDropsOff() +
                    (pending > 0 ? $" The {Count(pending, "change")} not written yet are dropped too." : "") + " Cancel or 5 s leaves it. Esc = Cancel."))
                {
                    var all = new List<KeyValuePair<string, bool>>();
                    foreach (SaveFlag f in _savesFlags) all.Add(new KeyValuePair<string, bool>(f.Id, false));
                    if (RunSaveEdit(_savesSlot, () => GameSaves.SetFlags(PluginGuid, _savesSlot, all, $"all {all.Count} flags set to false")))
                    {
                        _flagEdits.Remove(_savesSlot);
                        _flagEditsLeft = false;
                    }
                }
                fy += 38;
            }

            Dictionary<string, bool> edits = FlagEdits(_savesSlot, false);
            for (int i = 0; i < _flagRows.Count && (dbg & 4) == 0; i++)
            {
                FlagRow r = _flagRows[i];
                float ry = fy + i * 30;
                if (ry + 30 < visibleTop || ry > visibleBottom)
                {
                    continue;
                }
                if (r.IsHeader)
                {
                    if ((dbg & 8) == 0)
                        GUI.Label(new Rect(12, ry + 4, innerWidth - 24, 26), r.ShownId, LineLabel(false));
                    continue;
                }
                // Cut with "..." to their columns; the whole of both is on the
                // hint line while the pointer is on the row.
                float idWidth = Mathf.Max(60, innerWidth * 0.42f);
                float descWidth = Mathf.Max(40, innerWidth * 0.58f - 130);
                if (r.CutFor != innerWidth)
                {
                    r.CutFor = innerWidth;
                    r.CutId = ToolWindow.Elide(r.ShownId, LineLabel(false), idWidth - 8);
                    r.CutDescription = ToolWindow.Elide(r.ShownDescription, LineLabel(true), descWidth - 4);
                }
                GUI.Label(new Rect(24, ry, idWidth, RowHeight), r.CutId, LineLabel(false));
                if ((dbg & 2) == 0)
                    GUI.Label(new Rect(24 + idWidth, ry, descWidth, RowHeight), r.CutDescription, LineLabel(true));
                if (PointerOn(new Rect(24, ry, idWidth + descWidth, RowHeight), true))
                {
                    ToolWindow.Hint(r.HintText);
                }
                // The value it is going to have: a change not written yet, else
                // the save's own.
                bool changed = edits != null && edits.ContainsKey(r.Id);
                bool? value = changed ? edits[r.Id] : r.Value;
                string shown = (value == null ? "unset" : (value.Value ? "true" : "false")) + (changed ? " *" : "");
                GUIStyle st = value == true ? S.SelectedButton : S.Button;
                if (GUI.Button(new Rect(innerWidth - 90, ry, 78, RowHeight), shown, st))
                {
                    bool next = value != true;   // unset -> true, true -> false, false -> true
                    if (r.Value == next)
                    {
                        edits.Remove(r.Id);   // back to what the save has
                        if (edits.Count == 0) _flagEdits.Remove(_savesSlot);
                    }
                    else
                    {
                        FlagEdits(_savesSlot, true)[r.Id] = next;
                    }
                    edits = FlagEdits(_savesSlot, false);
                    _flagEditsLeft = false;
                }
            }
        }
    }
}
