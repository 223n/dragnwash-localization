using System.IO;
using System.Collections.Generic;
using System;
using UnityEngine;
using DragNWash.ModFramework.Saves;
using DragNWash.ModFramework.ToolWindow;

namespace DragNWashLocalization
{
    public partial class Plugin
    {
        private const float RowHeight = ToolWindow.RowHeight;
        private static ToolWindowStyles S => ToolWindow.Styles;

        // AddTab hands back the handle that removes the tab again; Awake uses it
        // to take the tabs down if startup fails after this point.
        private readonly List<IDisposable> _toolTabs = new List<IDisposable>();

        private void AddToolTabs()
        {
            _toolTabs.Add(ToolWindow.AddTab(PluginGuid, "Activity log", area => DrawWithStatus(area, DrawActivityLog), 100));
            _toolTabs.Add(ToolWindow.AddTab(PluginGuid, "Translation", area => DrawWithStatus(area, DrawTools), 101));
            _toolTabs.Add(ToolWindow.AddTab(PluginGuid, "Saves", DrawSaves, 102));
            _toolTabs.Add(ToolWindow.AddTab(PluginGuid, "About", DrawAbout, 103));
            ToolWindow.AddCommand(PluginGuid, "tl", "tl status | tl reload | tl find <text> | tl review", ConsoleCommand,
                args => args.Length == 1 ? new[] { "status", "reload", "find", "review" } : new string[0]);
        }

        // The console's "tl" command (experimental; the framework's Console tab).
        private string ConsoleCommand(string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            switch (what)
            {
                case "status":
                    return $"Language {TargetLocale.Value}, {TranslationStore.EntryCount} entries loaded, {LineResolution.RecordCount} line records, {LineResolution.ReviewCount} line(s) to review, graphics {SystemInfo.graphicsDeviceType}.";
                case "reload":
                    TranslationStore.Load(PluginDirectory, TargetLocale.Value);
                    TmpTextHook.RefreshAll();
                    return $"Reloaded {TargetLocale.Value}: {TranslationStore.EntryCount} entries.";
                case "find":
                {
                    if (args.Length < 2)
                    {
                        return "tl find <text>: rows whose translation contains the text";
                    }
                    string needle = string.Join(" ", args, 1, args.Length - 1);
                    var lines = new List<string>();
                    foreach (KeyValuePair<string, string> kv in TranslationStore.Entries)
                    {
                        if (kv.Value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            lines.Add($"{kv.Key}  {kv.Value}");
                            if (lines.Count >= 20)
                            {
                                lines.Add("(more; narrow the text)");
                                break;
                            }
                        }
                    }
                    return lines.Count == 0 ? "No translation contains that." : string.Join("\n", lines);
                }
                case "review":
                {
                    List<LineResolution.Review> reviews = LineResolution.ReviewList;
                    if (reviews.Count == 0)
                    {
                        return "No line needs review: every line shown so far matched its exact English.";
                    }
                    var lines = new List<string>();
                    foreach (LineResolution.Review r in reviews)
                    {
                        lines.Add($"{r.LineId ?? r.Key}  {r.Node} / {r.Speaker}  matched by {r.Layer}");
                    }
                    return string.Join("\n", lines);
                }
                default:
                    return "tl status | tl reload | tl find <text> | tl review";
            }
        }

        // Reloaded by the framework (or unloaded at quit): the tabs and the
        // console command go with this build. Hot reload polls from Update, so
        // it stops with the component.
        private void OnDestroy()
        {
            RemoveToolTabs();
        }

        private void RemoveToolTabs()
        {
            foreach (IDisposable tab in _toolTabs)
            {
                // Called from the failure path of Awake; it must not throw a
                // second exception over the one being reported.
                try { tab.Dispose(); } catch { }
            }
            _toolTabs.Clear();
        }

        // The language and entry count above the translation tabs.
        private void DrawWithStatus(Rect area, Action<Rect> draw)
        {
            string localeStatus = _pendingLocale == null ? TargetLocale.Value : TargetLocale.Value + " -> " + _pendingLocale;
            GUI.Label(new Rect(area.x, area.y, area.width, 24),
                $"Language: {localeStatus}    |    Entries: {TranslationStore.EntryCount}" + (LineResolution.ReviewCount > 0 ? $"    |    Review: {LineResolution.ReviewCount} line(s)" : ""), S.MutedLabel);
            draw(new Rect(area.x, area.y + 32, area.width, Mathf.Max(40, area.height - 32)));
        }

        // "0.3.1+<commit>" is stamped into the assembly at build time; show the
        // commit so a bug report says exactly which build is running.
        private static string BuildId()
        {
            try
            {
                var attr = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(Plugin).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                string v = attr != null ? attr.InformationalVersion : null;
                int plus = v != null ? v.IndexOf('+') : -1;
                if (plus < 0) return string.Empty;
                string commit = v.Substring(plus + 1);
                return commit.Length > 7 ? commit.Substring(0, 7) : commit;
            }
            catch
            {
                return string.Empty;
            }
        }

        // True when every character of the text has a glyph in the menu font.
        // With Unity's built-in font (no CJK), "日本語" would draw as nothing,
        // so the caller shows the locale code instead.
        private bool MenuFontCanDraw(string text)
        {
            return ToolWindow.CanDraw(text);
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
