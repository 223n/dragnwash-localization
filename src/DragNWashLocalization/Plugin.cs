using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragNWashLocalization
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.tomxv.dragnwash.localization";
        public const string PluginName = "DragNWashLocalization";
        public const string PluginVersion = "0.2.1";

        // Every visible line costs dynamic geometry each frame the menu is open,
        // and that scratch memory is what the Direct3D 12 bug chokes on at
        // present time. See CreateMenuFont.
        private const int MaxLogLines = 100;
        private const int MenuFontSize = 14;

        internal static ConfigEntry<string> TargetLocale;
        internal static ConfigEntry<int> FlagPanelDebug;
        internal static ConfigEntry<bool> LogDiscoveredKeys;
        internal static ConfigEntry<bool> VerboseTextLog;
        internal static ConfigEntry<KeyboardShortcut> ToggleMenuKey;
        internal static ConfigEntry<KeyboardShortcut> DumpDialogueKey;
        internal static ConfigEntry<KeyboardShortcut> DumpUiTextKey;
        internal static ConfigEntry<int> FontAtlasPointSize;
        internal static ConfigEntry<double> LayoutRiskThreshold;
        internal static ConfigEntry<bool> HotReloadTranslations;
        internal static ConfigEntry<bool> SaveHistoryEnabled;
        internal static ConfigEntry<int> SaveHistoryKeep;
        internal static string PluginDirectory;

        private static Plugin _instance;
        private static readonly List<string> LogBuffer = new List<string>();
        private static string _lastLogMessage;

        // Count alone stops changing once the buffer is full, which would freeze
        // the rendered log. Bumped on every mutation instead.
        private static int _logVersion;

        internal static void Log(string message)
        {
            // Logging failures must not propagate into the game's text updates.
            try
            {
                _instance?.Logger.LogInfo(message);

                lock (LogBuffer)
                {
                    if (message == _lastLogMessage)
                    {
                        return;
                    }
                    _lastLogMessage = message;

                    LogBuffer.Add(message);
                    if (LogBuffer.Count > MaxLogLines)
                    {
                        LogBuffer.RemoveAt(0);
                    }
                    _logVersion++;
                }
            }
            catch
            {
                // Swallow - see comment above.
            }
        }

        private bool _showMenu;
        private Vector2 _logScroll;
        private Vector2 _localeScroll;
        private string[] _availableLocales = Array.Empty<string>();
        private Rect _windowRect = new Rect(24, 24, 780, 580);
        private int _lastLogVersion = -1;

        private void Awake()
        {
            _instance = this;
            PluginDirectory = Path.GetDirectoryName(Info.Location);

            TargetLocale = Config.Bind(
                "General",
                "TargetLocale",
                "ja",
                "Translations/<TargetLocale>/ 以下のファイルを読み込みます。例: ja, zh-Hans。en を指定すると翻訳せず英語のままになります");

            LogDiscoveredKeys = Config.Bind(
                "Debug",
                "LogDiscoveredKeys",
                true,
                "未翻訳のテキストを Translations/_discovered/strings.csv に自動記録するか");

            VerboseTextLog = Config.Bind(
                "Debug",
                "VerboseTextLog",
                true,
                "TMPテキストの置き換え結果（成功／未対応）をデバッグメニューのログに逐次表示するか");

            ToggleMenuKey = Config.Bind(
                "Debug",
                "ToggleMenuKey",
                new KeyboardShortcut(KeyCode.F1),
                "デバッグメニュー（言語切り替え・会話ログ抽出・ログ表示）の表示切り替え");

            DumpDialogueKey = Config.Bind(
                "Debug",
                "DumpDialogueKey",
                new KeyboardShortcut(KeyCode.F6),
                "ロード済みの全会話行を Translations/_discovered/dialogue_lines.csv に書き出す");

            DumpUiTextKey = Config.Bind(
                "Debug",
                "DumpUiTextKey",
                new KeyboardShortcut(KeyCode.F7),
                "読み込み済みの全UIテキスト（非表示のメニューを含む）を Translations/_discovered/ui_texts.csv に書き出す");

            FontAtlasPointSize = Config.Bind(
                "Font",
                "AtlasPointSize",
                80,
                "CJKフォールバックフォントのアトラス解像度（サンプリングポイントサイズ）。大きいほど文字が鮮明になります。グリフは起動時にまとめて生成するので、上げても実行中の負荷は増えず、起動時のアトラス生成が少し重くなるだけです");

            // Renamed from LayoutRiskThreshold: that compared the translation's
            // character width against the source's, so 1.4 meant "40% longer".
            // This compares what the text needs against the space it has, where
            // 1.0 is exactly full, and a stale 1.4 would hide real overflow.
            LayoutRiskThreshold = Config.Bind(
                "Debug",
                "LayoutOverflowThreshold",
                1.0,
                "訳文が必要とする大きさがコンテナのこの倍率を超えると、はみ出しとして Translations/_discovered/layout_risks.csv に記録します（1.0 = ちょうど収まる）");

            SaveHistoryEnabled = Config.Bind(
                "Debug",
                "SaveHistoryEnabled",
                true,
                "セーブが書き込まれるたびに BepInEx/plugins/DragNWashLocalization/SaveHistory/ に世代コピーを残し、F1 の Saves タブから任意の世代へ戻せるようにする");

            SaveHistoryKeep = Config.Bind(
                "Debug",
                "SaveHistoryKeep",
                30,
                "スロットごとに残す世代数");

            FlagPanelDebug = Config.Bind(
                "Debug",
                "FlagPanelDebug",
                0,
                "Troubleshooting only. Bit 1: no search box. Bit 2: no descriptions. Bit 4: no flag rows. Bit 8: no group headers.");

            HotReloadTranslations = Config.Bind(
                "Debug",
                "HotReloadTranslations",
                true,
                "現在の言語の strings.csv が保存されたら、再起動なしで読み直して画面に反映する");

            TranslationStore.Load(PluginDirectory, TargetLocale.Value);
            FontFallback.EnsureCjkFallback();
            // Rasterize every installed locale now: every glyph added later
            // would be a runtime atlas texture upload, and on Direct3D 12 that
            // upload is what freezes/crashes the game (see FontFallback).
            FontFallback.Prewarm(TranslationStore.CollectAllLocalesTexts(PluginDirectory));
            HotReload.Track(PluginDirectory, TargetLocale.Value);
            SaveHistory.Configure(PluginDirectory, SaveHistoryKeep.Value);
            CreateMenuBackgroundTexture();
            // Locales first: the font warm-up below needs their display names.
            RefreshAvailableLocales();
            CreateMenuFont();

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            Logger.LogInfo($"DragNWashLocalization loaded. TargetLocale={TargetLocale.Value}, loaded entries={TranslationStore.EntryCount}, ignore patterns={IgnoreRules.PatternCount}, graphics={SystemInfo.graphicsDeviceType}");

            // Glyphs are prewarmed above so this should no longer be reachable,
            // but the underlying engine bug is still there: anything else that
            // allocates textures at runtime can hit it.
            if (Application.unityVersion == "6000.3.14f1" &&
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
            {
                Logger.LogInfo("Running on Unity 6000.3.14f1 / Direct3D12, which has a native crash in D3D12ScratchAllocator around runtime texture uploads (Unity issue UUM-140564). If the game still crashes, add -force-d3d11 to its Steam launch options and restart.");
            }
        }

        private float _nextDiscoveredFlushTime;
        private string _pendingLocale;
        private bool _pendingDump;
        private bool _pendingUiDump;
        private bool _pointerGrabbedByMenu;
        private bool _inputBlockingBroken;
        private bool _pendingLayoutCheck;
        private bool _pendingHashFile;
        private bool _pendingFlowDump;
        private bool _pendingWorkingCopy;
        private string _pendingRestoreSlot;
        private SaveHistory.Snapshot _pendingRestoreSnapshot;

        private void Update()
        {
            // Locale switching is requested from OnGUI but performed here: it
            // reads files and rasterizes glyphs, neither of which belongs in a
            // render callback.
            if (_pendingLocale != null)
            {
                string locale = _pendingLocale;
                _pendingLocale = null;

                TargetLocale.Value = locale;
                TranslationStore.Load(PluginDirectory, locale);
                // No Prewarm here: all locales were rasterized at startup. Doing
                // it mid-game would upload atlas textures on a frame the
                // Direct3D 12 renderer is busy with, which freezes the game.
                TmpTextHook.RefreshAll();
                HotReload.Track(PluginDirectory, locale);
                Log($"Switched locale to {locale}. Loaded entries={TranslationStore.EntryCount}");
            }

            if (HotReloadTranslations.Value)
            {
                HotReload.Tick(PluginDirectory, TargetLocale.Value);
            }

            if (ToggleMenuKey.Value.IsDown())
            {
                _showMenu = !_showMenu;
            }

            if (DumpDialogueKey.Value.IsDown() || _pendingDump)
            {
                _pendingDump = false;
                DialogueDumper.DumpAll(PluginDirectory);
            }

            if (DumpUiTextKey.Value.IsDown() || _pendingUiDump)
            {
                _pendingUiDump = false;
                UiTextDumper.DumpAll(PluginDirectory);
            }

            if (SaveHistoryEnabled.Value)
            {
                SaveHistory.Tick();
            }

            if (_pendingRestoreSnapshot != null)
            {
                SaveHistory.Snapshot snapshot = _pendingRestoreSnapshot;
                string slot = _pendingRestoreSlot;
                _pendingRestoreSnapshot = null;
                _pendingRestoreSlot = null;
                string result = SaveHistory.Restore(slot, snapshot);
                Log(result);
                _menuNotice = result;
            }

            if (_pendingWorkingCopy)
            {
                _pendingWorkingCopy = false;
                Log(WorkingCopy.Export(PluginDirectory, TargetLocale.Value));
            }

            if (_pendingHashFile)
            {
                _pendingHashFile = false;
                // Rewrites the file; hot reload then re-reads it, which is a
                // no-op for the table since every row resolves to the same key.
                Log(TranslationStore.HashFileInPlace(PluginDirectory, TargetLocale.Value));
            }

            if (_pendingFlowDump)
            {
                _pendingFlowDump = false;
                Log(FlowDumper.Export(PluginDirectory));
            }

            if (_pendingLayoutCheck)
            {
                _pendingLayoutCheck = false;
                LayoutChecker.Report(PluginDirectory, LayoutRiskThreshold.Value);
            }

            // Periodic, main-thread, low-frequency: see the comment on
            // TranslationStore.NoteDiscoveredText for why this isn't done inline.
            if (Time.unscaledTime >= _nextDiscoveredFlushTime)
            {
                _nextDiscoveredFlushTime = Time.unscaledTime + 2f;
                TranslationStore.FlushDiscoveredToDisk();
            }

            // Last, and swallowing its own failures: an exception here used to
            // abort the rest of Update, which silently disabled the export and
            // layout buttons.
            if (!_inputBlockingBroken)
            {
                try
                {
                    UpdateInputBlocking();
                }
                catch (Exception ex)
                {
                    // Give up rather than throw once per frame forever.
                    _inputBlockingBroken = true;
                    InputBlocker.SetBlocking(false);
                    Log($"[input] Input blocking disabled: {ex.Message}");
                }
            }
        }

        // Suspend the game's own input while the pointer is working the debug
        // window. Held mouse buttons keep the block even once the pointer
        // leaves, so dragging the window or its resize grip past the edge does
        // not hand the drag back to the game mid-gesture.
        //
        // Read the pointer through the Input System, not UnityEngine.Input:
        // this game has legacy input handling switched off, so every legacy
        // read throws. BepInEx's own KeyboardShortcut picks the right backend
        // for us, which is why the hotkeys work either way.
        private void UpdateInputBlocking()
        {
            if (!_showMenu)
            {
                _pointerGrabbedByMenu = false;
                InputBlocker.SetBlocking(false);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _pointerGrabbedByMenu = false;
                InputBlocker.SetBlocking(false);
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            // The Input System measures from the bottom left, GUI from the top.
            bool over = _windowRect.Contains(new Vector2(position.x, Screen.height - position.y));

            bool held = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                _pointerGrabbedByMenu = over;
            }
            else if (!held)
            {
                _pointerGrabbedByMenu = false;
            }

            InputBlocker.SetBlocking(over || _pointerGrabbedByMenu);
        }

        private void OnDestroy()
        {
            // Leaving the game's input suspended would soft-lock it.
            InputBlocker.SetBlocking(false);
        }

        private void RefreshAvailableLocales()
        {
            string translationsDir = Path.Combine(PluginDirectory, "Translations");
            if (!Directory.Exists(translationsDir))
            {
                _availableLocales = Array.Empty<string>();
                return;
            }

            // "en" is always offered: it has no translation folder and means
            // "leave the game's own English text alone".
            _localeNames.Clear();
            _availableLocales = new[] { "en" }
                .Concat(Directory.GetDirectories(translationsDir)
                    .Select(Path.GetFileName)
                    .Where(name => !name.StartsWith("_") && name != "en")
                    .OrderBy(name => name, StringComparer.Ordinal))
                .ToArray();
        }

        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private Texture2D _darkBackground;
        private Font _menuFont;
        private string _logText = string.Empty;

        // Built during Awake rather than on the first F1 press: Texture2D.Apply
        // uploads to the GPU, and doing that on the frame the menu opens is
        // exactly the runtime upload that trips the Direct3D 12 bug described
        // in FontFallback. GUIStyles still have to wait for GUI.skin.
        private void CreateMenuBackgroundTexture()
        {
            _darkBackground = new Texture2D(1, 1);
            _darkBackground.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.08f, 0.95f));
            _darkBackground.Apply();
        }

        // IMGUI draws through its own dynamic font, which grows its texture the
        // first time it is asked for a character - and the log shows translated
        // text, so opening the menu would upload a texture full of freshly
        // rasterized kanji on that very frame. The crash is
        // D3D12ScratchAllocator::ReleaseExcessScratch during PresentFrame, so
        // the frame that opens the menu is exactly the wrong one to do this on.
        // Own the font instead of relying on GUI.skin's, and fill it here.
        private void CreateMenuFont()
        {
            try
            {
                foreach (string name in new[] { "Yu Gothic UI", "Meiryo UI", "Hiragino Sans", "PingFang SC", "Noto Sans CJK JP", "Noto Sans CJK SC" })
                {
                    _menuFont = Font.CreateDynamicFontFromOSFont(name, MenuFontSize);
                    if (_menuFont != null) break;
                }
                if (_menuFont == null)
                {
                    return;
                }

                var ascii = new StringBuilder();
                for (char c = ' '; c <= '~'; c++)
                {
                    ascii.Append(c);
                }
                ascii.Append(FontFallback.WarmedCharacters());

                _menuFont.RequestCharactersInTexture(ascii.ToString(), MenuFontSize, FontStyle.Normal);
                // Locale display names may be non-Latin (name.txt is translator-set).
                foreach (string locale in _availableLocales ?? new string[0])
                {
                    _menuFont.RequestCharactersInTexture(LocaleDisplayName(locale), MenuFontSize, FontStyle.Normal);
                }
                // The flag editor shows catalog text (Japanese descriptions) in
                // this font; rasterizing those glyphs while the menu is open is
                // the D3D12 crash trigger, so they are warmed here at startup.
                var catalog = new StringBuilder();
                foreach (FlagCatalog.Entry e in FlagCatalog.Entries)
                {
                    catalog.Append(e.Id).Append(e.Group).Append(e.Description);
                }
                _menuFont.RequestCharactersInTexture(catalog.ToString(), MenuFontSize, FontStyle.Normal);
            }
            catch (Exception ex)
            {
                Log($"Failed to prepare the debug menu font: {ex.Message}");
                _menuFont = null;
            }
        }

    }
}
