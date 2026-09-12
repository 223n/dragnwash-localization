using System.IO;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragNWashLocalization
{
    public partial class Plugin
    {
        private const float MenuPadding = 16f;
        private const float RowHeight = 30f;
        private int _editLevel;
        private int _editLevelBase = -2;
        private string _editLevelSlot;
        private bool _progressConfirm;
        private GUIStyle _textFieldStyle;
        private bool _newestMatchesSave;
        private bool _showFlags;
        private List<SaveHistory.Flag> _savesFlags = new List<SaveHistory.Flag>();
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
            foreach (SaveHistory.Flag f in _savesFlags)
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
            foreach (FlagCatalog.Entry e in FlagCatalog.Entries)
            {
                known.Add(e.Id);
                if (!Match(e.Id, e.Description)) continue;
                Add(e.Group, new FlagRow
                {
                    Id = e.Id, Group = e.Group, Description = e.Description,
                    Value = inSave.TryGetValue(e.Id, out bool v) ? v : (bool?)null,
                });
            }
            foreach (SaveHistory.Flag f in _savesFlags)
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

        private float FlagRowsHeight() => 40 + 34 + _flagRows.Count * 30 + (_flagClearConfirm ? 70 : 0) + 8;
        private static readonly Color MenuPanel = new Color(0.09f, 0.11f, 0.15f);
        private static readonly Color MenuInset = new Color(0.055f, 0.07f, 0.10f);
        private static readonly Color MenuAccent = new Color(0.32f, 0.78f, 0.72f);
        private static readonly Color MenuMuted = new Color(0.60f, 0.66f, 0.73f);

        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _mutedLabelStyle;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _logLabelStyle;
        private int _menuTab;
        private bool _followLog = true;
        private bool _logNeedsScroll;
        private float _logContentHeight;
        private float _logContentWidth = -1;
        private bool _resizingMenu;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;
        private int _resizeControl;
        private Vector2? _requestedMenuSize;
        private string _menuNotice = string.Empty;

        private GUIStyle MenuStyle(GUIStyle basis, Color textColor)
        {
            var style = new GUIStyle(basis)
            {
                font = _menuFont != null ? _menuFont : basis.font,
                fontSize = MenuFontSize,
                // The bundled Noto Sans JP comes out thin in IMGUI; Unity's
                // synthetic bold gives it the weight of the OS fonts.
                fontStyle = _menuFontBold ? FontStyle.Bold : FontStyle.Normal,
                richText = false
            };
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
            return style;
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            // Reuse the texture and font prepared in Awake. All controls use the
            // same prewarmed font size/style; opening a tab allocates no texture.
            _windowStyle = MenuStyle(GUI.skin.window, Color.white);
            _windowStyle.normal.background = _darkBackground;
            _windowStyle.onNormal.background = _darkBackground;
            _windowStyle.focused.background = _darkBackground;
            _windowStyle.border = new RectOffset(0, 0, 0, 0);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _labelStyle = MenuStyle(GUI.skin.label, new Color(0.91f, 0.94f, 0.97f));
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            _mutedLabelStyle = MenuStyle(_labelStyle, MenuMuted);
            _wrappedLabelStyle = MenuStyle(_mutedLabelStyle, MenuMuted);
            _wrappedLabelStyle.wordWrap = true;
            _wrappedLabelStyle.alignment = TextAnchor.UpperLeft;
            _logLabelStyle = MenuStyle(_labelStyle, new Color(0.86f, 0.91f, 0.94f));
            _logLabelStyle.wordWrap = true;
            _logLabelStyle.alignment = TextAnchor.UpperLeft;
            _logLabelStyle.padding = new RectOffset(4, 4, 4, 4);

            _buttonStyle = MenuStyle(GUI.skin.button, new Color(0.88f, 0.92f, 0.95f));
            _buttonStyle.padding = new RectOffset(10, 10, 4, 4);
            _selectedButtonStyle = MenuStyle(_buttonStyle, MenuAccent);
            // The search box must not touch GUI.skin.textField: its built-in
            // background textures (normal and, once it has keyboard focus,
            // focused) would be uploaded on first draw while the menu is open,
            // which is the Direct3D 12 crash. Use our own 1x1 texture for every
            // state instead, like the window does.
            _textFieldStyle = MenuStyle(_labelStyle, new Color(0.91f, 0.94f, 0.97f));
            _textFieldStyle.alignment = TextAnchor.MiddleLeft;
            _textFieldStyle.padding = new RectOffset(8, 8, 4, 4);
            _textFieldStyle.border = new RectOffset(0, 0, 0, 0);
            foreach (GUIStyleState st in new[] { _textFieldStyle.normal, _textFieldStyle.hover, _textFieldStyle.active, _textFieldStyle.focused,
                                                 _textFieldStyle.onNormal, _textFieldStyle.onHover, _textFieldStyle.onActive, _textFieldStyle.onFocused })
            {
                st.background = _darkBackground;
            }
        }

        private static void FillMenuRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static Rect ClampMenuRect(Rect rect, float screenWidth, float screenHeight)
        {
            float width = Mathf.Max(1, screenWidth);
            float height = Mathf.Max(1, screenHeight);
            rect.width = Mathf.Clamp(rect.width, Mathf.Min(420, width), width);
            rect.height = Mathf.Clamp(rect.height, Mathf.Min(340, height), height);
            rect.x = Mathf.Clamp(rect.x, 0, width - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0, height - rect.height);
            return rect;
        }

        private void OnGUI()
        {
            if (!_showMenu)
            {
                if (_resizingMenu && GUIUtility.hotControl == _resizeControl)
                    GUIUtility.hotControl = 0;
                _resizingMenu = false;
                return;
            }

            EnsureStyles();
            _windowRect = ClampMenuRect(_windowRect, Screen.width, Screen.height);
            // Pad / trackpad presses that never reach IMGUI as mouse buttons
            // (Steam Deck) are turned into clicks by VirtualClick.
            VirtualClick.Observe(Event.current);
            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            try
            {
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.contentColor = Color.white;
                MenuText.Begin(_menuFont, MenuFontSize);
                _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, string.Empty, _windowStyle);
                MenuText.End();
                // GUI.Window returns its own rectangle after the callback. Apply
                // a resize afterwards so that return value cannot undo it.
                if (_requestedMenuSize.HasValue)
                {
                    _windowRect.size = _requestedMenuSize.Value;
                    _requestedMenuSize = null;
                    _windowRect = ClampMenuRect(_windowRect, Screen.width, Screen.height);
                }
            }
            finally
            {
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
            }
        }

        private void DrawWindow(int id)
        {
            float width = _windowRect.width;
            float height = _windowRect.height;
            float bodyWidth = width - MenuPadding * 2;

            FillMenuRect(new Rect(0, 0, width, 48), MenuPanel);
            FillMenuRect(new Rect(0, 0, 4, 48), MenuAccent);
            GUI.Label(new Rect(MenuPadding, 8, width - 80, 32), "DRAG'N WASH  /  LOCALIZATION", _labelStyle);
            if (GUI.Button(new Rect(width - 46, 10, 30, 28), "X", _buttonStyle))
                _showMenu = false;

            string localeStatus = _pendingLocale == null ? TargetLocale.Value : TargetLocale.Value + " -> " + _pendingLocale;
            GUI.Label(new Rect(MenuPadding, 54, bodyWidth, 24),
                $"Locale: {localeStatus}    |    Entries: {TranslationStore.EntryCount}", _mutedLabelStyle);

            if (GUI.Button(new Rect(MenuPadding, 84, 114, RowHeight), "Activity log", _menuTab == 0 ? _selectedButtonStyle : _buttonStyle))
                _menuTab = 0;
            if (GUI.Button(new Rect(MenuPadding + 122, 84, 114, RowHeight), "Tools", _menuTab == 1 ? _selectedButtonStyle : _buttonStyle))
                _menuTab = 1;
            if (GUI.Button(new Rect(MenuPadding + 244, 84, 114, RowHeight), "Saves", _menuTab == 2 ? _selectedButtonStyle : _buttonStyle))
                _menuTab = 2;

            var body = new Rect(MenuPadding, 126, bodyWidth, Mathf.Max(80, height - 164));
            if (_menuTab == 0) DrawActivityLog(body);
            else if (_menuTab == 1) DrawTools(body);
            else DrawSaves(body);

            GUI.Label(new Rect(MenuPadding, height - 30, width - 54, 24),
                string.IsNullOrEmpty(_menuNotice) ? $"{ToggleMenuKey.Value}: toggle    |    Drag title to move    |    Drag corner to resize" : _menuNotice,
                _mutedLabelStyle);

            HandleMenuResize(new Rect(width - 22, height - 22, 22, 22));
            // Restrict dragging to the title, so text selection and scrolling
            // never start moving the entire window.
            GUI.DragWindow(new Rect(4, 0, width - 58, 48));
        }

        private void DrawActivityLog(Rect area)
        {
            if (GUI.Button(new Rect(area.x, area.y, 122, RowHeight), _followLog ? "Follow: ON" : "Follow: OFF",
                _followLog ? _selectedButtonStyle : _buttonStyle))
            {
                _followLog = !_followLog;
                _logNeedsScroll = _followLog;
            }
            if (GUI.Button(new Rect(area.x + 130, area.y, 110, RowHeight), "Clear log", _buttonStyle))
            {
                lock (LogBuffer)
                {
                    LogBuffer.Clear();
                    _lastLogMessage = null;
                    _logVersion++;
                }
                TranslationStore.ResetAppliedOnceTracking();
                _logScroll = Vector2.zero;
                _menuNotice = "Log cleared.";
            }

            bool changed = false;
            int count;
            lock (LogBuffer)
            {
                count = LogBuffer.Count;
                if (_lastLogVersion != _logVersion)
                {
                    _lastLogVersion = _logVersion;
                    _logText = string.Join("\n", LogBuffer.ToArray());
                    changed = true;
                }
            }

            var viewport = new Rect(area.x, area.y + 40, area.width, Mathf.Max(20, area.height - 40));
            FillMenuRect(viewport, MenuInset);
            float contentWidth = Mathf.Max(40, viewport.width - 24);
            if (changed || !Mathf.Approximately(_logContentWidth, contentWidth))
            {
                _logContentWidth = contentWidth;
                _logContentHeight = string.IsNullOrEmpty(_logText) ? 0 :
                    _logLabelStyle.CalcHeight(new GUIContent(_logText), contentWidth);
                if (_followLog) _logNeedsScroll = true;
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

            if (VirtualClick.ApplyScroll(viewport, ref _logScroll)) { _followLog = false; _logNeedsScroll = false; }
            _logScroll = GUI.BeginScrollView(viewport, _logScroll,
                new Rect(0, 0, contentWidth, Mathf.Max(viewport.height - 1, _logContentHeight)), false, true);
            if (count == 0)
                GUI.Label(new Rect(12, 12, contentWidth - 24, 64),
                    "No activity yet.\nOpen a game menu or dialogue to capture text.", _wrappedLabelStyle);
            else
                GUI.Label(new Rect(0, 0, contentWidth, _logContentHeight), _logText, _logLabelStyle);
            GUI.EndScrollView();

            GUI.Label(new Rect(area.x + 250, area.y, Mathf.Max(0, area.width - 250), RowHeight),
                $"{count} / {MaxLogLines}", _mutedLabelStyle);
        }

        private void DrawTools(Rect area)
        {
            FillMenuRect(area, MenuInset);
            float innerWidth = Mathf.Max(100, area.width - 36);
            const float contentHeight = 394;
            VirtualClick.ApplyScroll(area, ref _localeScroll);
            _localeScroll = GUI.BeginScrollView(area, _localeScroll,
                new Rect(0, 0, innerWidth, contentHeight + Mathf.Ceil(_availableLocales.Length / 3f) * 38), false, false);
            GUI.Label(new Rect(12, 8, innerWidth - 12, 26), "LANGUAGE", _labelStyle);
            float buttonWidth = (innerWidth - 28) / 3;
            float y = 42;
            for (int i = 0; i < _availableLocales.Length; i++)
            {
                string locale = _availableLocales[i];
                bool selected = locale == TargetLocale.Value;
                string label = LocaleDisplayName(locale);
                if (!MenuFontCanDraw(label)) label = locale;
                if (GUI.Button(new Rect(12 + (i % 3) * (buttonWidth + 8), y + (i / 3) * 38, buttonWidth, RowHeight),
                    selected ? label + "  [active]" : label, selected ? _selectedButtonStyle : _buttonStyle))
                {
                    _pendingLocale = locale;
                    _menuNotice = "See Activity log for the language change result.";
                }
            }
            if (_availableLocales.Length == 0)
                GUI.Label(new Rect(12, y, innerWidth, RowHeight), "No language folders installed.", _mutedLabelStyle);
            y += Mathf.Max(1, Mathf.Ceil(_availableLocales.Length / 3f)) * 38 + 12;
            GUI.Label(new Rect(12, y, innerWidth - 12, 26), "TRANSLATION TOOLS", _labelStyle);
            y += 34;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingDump ? "Dialogue export queued..." : $"Export loaded dialogue  ({DumpDialogueKey.Value})", _buttonStyle))
            {
                _pendingDump = true;
                _menuNotice = "See Activity log for the dialogue export result.";
            }
            y += 38;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingUiDump ? "UI text export queued..." : $"Export UI text  ({DumpUiTextKey.Value})", _buttonStyle))
            {
                _pendingUiDump = true;
                _menuNotice = "See Activity log for the UI text export result.";
            }
            y += 38;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingLayoutCheck ? "Layout check queued..." : "Check translation layout", _buttonStyle))
            {
                _pendingLayoutCheck = true;
                _menuNotice = "See Activity log for the layout check result.";
            }
            y += 38;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingWorkingCopy ? "Export queued..." : "Export working copy (English beside each line)", _buttonStyle))
            {
                _pendingWorkingCopy = true;
                _menuNotice = "See Activity log for the working copy result.";
            }
            y += 38;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingHashFile ? "Hashing queued..." : "Hash for commit (rebuild strings.csv, no English)", _buttonStyle))
            {
                _pendingHashFile = true;
                _menuNotice = "See Activity log for the hashing result.";
            }
            y += 38;
            if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingFlowDump ? "Flow export queued..." : "Export game flow (levels + dialogue graph)", _buttonStyle))
            {
                _pendingFlowDump = true;
                _menuNotice = "See Activity log for the flow export result.";
            }
            y += 42;
            GUI.Label(new Rect(12, y, innerWidth - 12, 52),
                "Exports are written to Translations/_discovered.\nLanguage changes apply to text already on screen.", _wrappedLabelStyle);
            GUI.EndScrollView();
        }

        private Vector2 _savesScroll;
        private string _savesSlot;
        private float _savesRefreshAt;
        private List<string> _savesSlots = new List<string>();
        private List<SaveHistory.Snapshot> _savesList = new List<SaveHistory.Snapshot>();

        // Snapshots of the game's own save file, one per write, newest first.
        // Restore puts one back; the player then reloads the slot from the
        // title screen. Listing is cached and refreshed every couple of
        // seconds so OnGUI does not hit the disk every frame.
        private void DrawSaves(Rect area)
        {
            FillMenuRect(area, MenuInset);
            float innerWidth = Mathf.Max(100, area.width - 36);

            if (Time.unscaledTime >= _savesRefreshAt)
            {
                _savesRefreshAt = Time.unscaledTime + 2f;
                _savesSlots = SaveHistory.Slots();
                if (_savesSlot == null || !_savesSlots.Contains(_savesSlot))
                {
                    _savesSlot = _savesSlots.Count > 0 ? _savesSlots[0] : null;
                }
                _savesList = _savesSlot != null ? SaveHistory.SnapshotsFor(_savesSlot) : new List<SaveHistory.Snapshot>();
                _savesFlags = _savesSlot != null ? SaveHistory.ReadFlags(_savesSlot) : new List<SaveHistory.Flag>();
                _newestMatchesSave = _savesSlot != null && _savesList.Count > 0 && SaveHistory.SnapshotMatchesSave(_savesSlot, _savesList[0]);
                RebuildFlagRows();
            }

            float y = 8;
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "SAVE SLOT", _labelStyle);
            y += 32;
            float x = 12;
            foreach (string slot in _savesSlots)
            {
                string shown = slot.Contains("_slot") ? "slot " + slot.Substring(slot.IndexOf("_slot") + 5) : slot;
                float w = 90;
                if (GUI.Button(new Rect(area.x + x, area.y + y, w, RowHeight), shown, slot == _savesSlot ? _selectedButtonStyle : _buttonStyle))
                {
                    _savesSlot = slot;
                    _savesRefreshAt = 0;
                }
                x += w + 8;
            }
            if (_savesSlots.Count == 0)
                GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, RowHeight), "No save files found.", _mutedLabelStyle);
            y += 42;

            // ---- progress editor: levelIndex and the boolean flags ----------
            if (_savesSlot != null)
            {
                int current = SaveHistory.ReadLevel(_savesSlot, out _);
                if (_editLevelSlot != _savesSlot || _editLevelBase != current)
                {
                    _editLevelSlot = _savesSlot;
                    _editLevelBase = current;
                    _editLevel = current;
                    _progressConfirm = false;
                }

                GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, 26), "PROGRESS", _labelStyle);
                y += 32;
                GUI.Label(new Rect(area.x + 12, area.y + y, 200, RowHeight),
                    _editLevel == current ? $"level {current}" : $"level {current}  ->  {_editLevel}", _labelStyle);
                if (GUI.Button(new Rect(area.x + 216, area.y + y, 40, RowHeight), "-", _buttonStyle) && _editLevel > 0)
                {
                    _editLevel--; _progressConfirm = false;
                }
                if (GUI.Button(new Rect(area.x + 262, area.y + y, 40, RowHeight), "+", _buttonStyle))
                {
                    _editLevel++; _progressConfirm = false;
                }
                bool canApply = _editLevel != current && current >= 0;
                GUI.enabled = canApply;
                if (GUI.Button(new Rect(area.x + 314, area.y + y, 90, RowHeight), "Apply", _buttonStyle))
                {
                    if (_editLevel > current)
                    {
                        _progressConfirm = true;   // going forward can spoil the story
                    }
                    else
                    {
                        Log(SaveHistory.SetLevel(_savesSlot, _editLevel));
                        _savesRefreshAt = 0;
                    }
                }
                GUI.enabled = true;
                if (GUI.Button(new Rect(area.x + 414, area.y + y, Mathf.Max(60, innerWidth - 414 + 12), RowHeight),
                    _showFlags ? "Hide flags" : "Flags...", _buttonStyle))
                {
                    _showFlags = !_showFlags;
                }
                y += 36;

                if (_progressConfirm)
                {
                    var warn = new GUIContent($"Warning: jumping ahead to level {_editLevel} may spoil content you have not seen. Continue?");
                    float wh = _labelStyle.CalcHeight(warn, innerWidth);
                    GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, wh), warn, _labelStyle);
                    y += wh + 4;
                    if (GUI.Button(new Rect(area.x + 12, area.y + y, 120, RowHeight), "Yes, continue", _buttonStyle))
                    {
                        Log(SaveHistory.SetLevel(_savesSlot, _editLevel));
                        _progressConfirm = false;
                        _savesRefreshAt = 0;
                    }
                    if (GUI.Button(new Rect(area.x + 140, area.y + y, 90, RowHeight), "Cancel", _buttonStyle))
                    {
                        _progressConfirm = false;
                        _editLevel = current;
                    }
                    y += 36;
                }
            }

            // Both explanatory labels wrap on a narrow window, so size them from
            // the text instead of assuming one line.
            var historyText = new GUIContent($"HISTORY  ({_savesList.Count} snapshot(s), newest first)");
            float historyHeight = _labelStyle.CalcHeight(historyText, innerWidth);
            GUI.Label(new Rect(area.x + 12, area.y + y, innerWidth, historyHeight), historyText, _labelStyle);
            y += historyHeight + 6;

            var footerText = new GUIContent("A snapshot is taken whenever the game writes the save. After Restore: go to the title screen and load the slot. Saving in game overwrites it again.");
            float footerHeight = _mutedLabelStyle.CalcHeight(footerText, innerWidth);

            var view = new Rect(area.x, area.y + y, area.width, Mathf.Max(40, area.height - y - footerHeight - 12));
            VirtualClick.ApplyScroll(view, ref _savesScroll);
            _savesScroll = GUI.BeginScrollView(view, _savesScroll, new Rect(0, 0, innerWidth, Mathf.Max(view.height, _showFlags && _savesSlot != null ? FlagRowsHeight() : _savesList.Count * 36)), false, false);
            // The flag editor replaces the history list while it is open, so
            // the flags start at the top instead of below 30 snapshot rows.
            bool flagsOpen = _showFlags && _savesSlot != null;
            for (int i = 0; i < _savesList.Count && !flagsOpen; i++)
            {
                SaveHistory.Snapshot s = _savesList[i];
                // "current" only when the newest snapshot really is the save on
                // disk; after a progress or flag edit it is the pre-edit state.
                bool isCurrent = i == 0 && _newestMatchesSave;
                GUI.Label(new Rect(12, i * 36, innerWidth - 130, RowHeight), (isCurrent ? "current   " : "") + s.Label, _labelStyle);
                if (!isCurrent && GUI.Button(new Rect(innerWidth - 110, i * 36, 98, RowHeight), "Restore", _buttonStyle))
                {
                    _pendingRestoreSlot = _savesSlot;
                    _pendingRestoreSnapshot = s;
                    _savesRefreshAt = 0;
                }
            }
            if (flagsOpen)
            {
                float fy = 4;
                GUI.Label(new Rect(12, fy, innerWidth, 26), "EVENT FLAGS   click a value: unset -> true -> false", _labelStyle);
                fy += 30;

                // Search box, once-lines toggle, and the bulk reset.
                int dbg = FlagPanelDebug != null ? FlagPanelDebug.Value : 0;
                if ((dbg & 1) == 0)
                {
                    string newFilter = GUI.TextField(new Rect(12, fy, Mathf.Max(80, innerWidth - 330), RowHeight), _flagFilter ?? "", _textFieldStyle);
                    if (newFilter != _flagFilter)
                    {
                        _flagFilter = newFilter;
                        RebuildFlagRows();
                    }
                }
                if (GUI.Button(new Rect(innerWidth - 310, fy, 120, RowHeight), _showOnceLines ? "Hide once-lines" : "Show once-lines", _buttonStyle))
                {
                    _showOnceLines = !_showOnceLines;
                    RebuildFlagRows();
                }
                if (GUI.Button(new Rect(innerWidth - 182, fy, 170, RowHeight), "Reset all to false...", _buttonStyle))
                {
                    _flagClearConfirm = !_flagClearConfirm;
                }
                fy += 34;

                if (_flagClearConfirm)
                {
                    GUI.Label(new Rect(12, fy, innerWidth - 24, 30),
                        "Set every flag in this save to false (the level index is kept). The current save is snapshotted first. Continue?", _mutedLabelStyle);
                    fy += 32;
                    if (GUI.Button(new Rect(12, fy, 120, RowHeight), "Yes, reset", _buttonStyle))
                    {
                        var all = new List<KeyValuePair<string, bool>>();
                        foreach (SaveHistory.Flag f in _savesFlags) all.Add(new KeyValuePair<string, bool>(f.Id, false));
                        Log(SaveHistory.SetFlags(_savesSlot, all, $"all {all.Count} flags set to false"));
                        _flagClearConfirm = false;
                        _savesRefreshAt = 0;
                    }
                    if (GUI.Button(new Rect(140, fy, 90, RowHeight), "Cancel", _buttonStyle))
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
                            GUI.Label(new Rect(12, ry + 4, innerWidth - 24, 26), r.Id.ToUpperInvariant(), _labelStyle);
                        continue;
                    }
                    GUI.Label(new Rect(24, ry, Mathf.Max(60, innerWidth * 0.42f), RowHeight), r.Id, _labelStyle);
                    if ((dbg & 2) == 0)
                        GUI.Label(new Rect(24 + Mathf.Max(60, innerWidth * 0.42f), ry, Mathf.Max(40, innerWidth * 0.58f - 130), RowHeight), r.Description ?? "", _mutedLabelStyle);
                    string shown = r.Value == null ? "unset" : (r.Value.Value ? "true" : "false");
                    GUIStyle st = r.Value == true ? _selectedButtonStyle : _buttonStyle;
                    if (GUI.Button(new Rect(innerWidth - 90, ry, 78, RowHeight), shown, st))
                    {
                        bool next = r.Value != true;   // unset -> true, true -> false, false -> true
                        Log(SaveHistory.SetFlag(_savesSlot, r.Id, next));
                        _savesRefreshAt = 0;
                    }
                }
            }
            GUI.EndScrollView();

            GUI.Label(new Rect(area.x + 12, area.y + area.height - footerHeight - 6, innerWidth, footerHeight), footerText, _mutedLabelStyle);
        }

        private void HandleMenuResize(Rect grip)
        {
            GUI.Label(grip, "/", _mutedLabelStyle);
            int control = GUIUtility.GetControlID("DragNWashMenuResize".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            Vector2 screenMouse = current.mousePosition + _windowRect.position;
            if (current.type == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                _resizingMenu = true;
                _resizeControl = control;
                GUIUtility.hotControl = control;
                _resizeStartMouse = screenMouse;
                _resizeStartSize = _windowRect.size;
                current.Use();
            }
            if (_resizingMenu && GUIUtility.hotControl == _resizeControl)
            {
                if (current.type == EventType.MouseDrag)
                {
                    _requestedMenuSize = _resizeStartSize + screenMouse - _resizeStartMouse;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp)
                {
                    _resizingMenu = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                }
            }
        }
    
        // True when every character of the text has a glyph in the menu font.
        // With Unity's built-in font (no CJK), "日本語" would draw as nothing,
        // so the caller shows the locale code instead.
        private bool MenuFontCanDraw(string text)
        {
            return MenuText.CanDraw(_menuFont, MenuFontSize, text);
        }

        // Shown on the language buttons; the folder name is what the config stores.
        // Translators set the name in Translations/<locale>/name.txt.
        private static readonly Dictionary<string, string> _localeNames = new Dictionary<string, string>();

        private static string LocaleDisplayName(string locale)
        {
            if (_localeNames.TryGetValue(locale, out string cached))
            {
                return cached;
            }

            string name = locale == "en" ? "English" : locale;
            try
            {
                string path = Path.Combine(PluginDirectory, "Translations", locale, "name.txt");
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path).Trim();
                    if (text.Length > 0)
                    {
                        name = text;
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to the folder name.
            }

            _localeNames[locale] = name;
            return name;
        }
}
}
