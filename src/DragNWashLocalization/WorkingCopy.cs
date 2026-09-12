using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace DragNWashLocalization
{
    // The published strings.csv has no English in it. A translator who owns
    // the game does not need any of it from the repository, though: the plugin
    // is running inside the game, so every line the game can show is right
    // here. This expands the hashed file back into a plain working copy -
    //
    //   key,speaker,source_en,translation
    //
    // written as _discovered/<locale>.working.csv (never committed), in script
    // order, with the English filled in from the loaded Yarn project, every
    // TMP_Text in the scene, and whatever play has discovered so far. Rows
    // whose key matches nothing the game has loaded are kept with an empty
    // source_en rather than dropped.
    //
    // Running inside the game is the ownership check. There is no need to
    // talk to Steam: BepInEx only loads because Steam launched the game.
    internal static class WorkingCopy
    {
        public static string FileNameFor(string locale) => locale + ".working.csv";

        // Kept with the other local-only exports so a locale folder holds
        // nothing but the published strings.csv.
        public static string PathFor(string pluginDirectory, string locale)
        {
            return Path.Combine(pluginDirectory, "Translations", "_discovered", FileNameFor(locale));
        }

        public static string Export(string pluginDirectory, string locale)
        {
            try
            {
                string localeDir = Path.Combine(pluginDirectory, "Translations", locale);
                string published = Path.Combine(localeDir, "strings.csv");
                if (!File.Exists(published))
                {
                    return $"[working] {locale}/strings.csv not found.";
                }

                // key -> translation, in file order.
                var translations = new Dictionary<string, string>(StringComparer.Ordinal);
                var fileOrder = new List<string>();
                foreach (var row in CsvReader.ReadRows(published))
                {
                    row.TryGetValue("key", out string key);
                    row.TryGetValue("source_en", out string src);
                    row.TryGetValue("translation", out string tr);
                    key = key?.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(src))
                    {
                        key = TranslationStore.KeyFor(src);
                    }
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    if (!translations.ContainsKey(key))
                    {
                        fileOrder.Add(key);
                    }
                    translations[key] = tr ?? string.Empty;
                }

                // key -> English, from everything the game has in memory.
                var sources = new Dictionary<string, string>(StringComparer.Ordinal);
                var speakers = new Dictionary<string, string>(StringComparer.Ordinal);
                var scriptOrder = new List<string>();
                foreach (KeyValuePair<string, string> line in DialogueDumper.EnumerateOrderedLines())
                {
                    string key = TranslationStore.KeyFor(line.Key);
                    if (!sources.ContainsKey(key))
                    {
                        sources[key] = line.Key;
                        speakers[key] = line.Value;
                        scriptOrder.Add(key);
                    }
                }
                foreach (TMP_Text component in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    string text;
                    try
                    {
                        if (!TmpTextHook.TryGetTrackedSource(component, out text))
                        {
                            text = component.text;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(text) || IgnoreRules.IsIgnored(text))
                    {
                        continue;
                    }
                    string key = TranslationStore.KeyFor(text);
                    if (!sources.ContainsKey(key))
                    {
                        sources[key] = text;
                        scriptOrder.Add(key);
                    }
                }
                string discovered = Path.Combine(pluginDirectory, "Translations", "_discovered", "strings.csv");
                if (File.Exists(discovered))
                {
                    foreach (var row in CsvReader.ReadRows(discovered))
                    {
                        if (row.TryGetValue("source_en", out string text) && !string.IsNullOrEmpty(text))
                        {
                            string key = TranslationStore.KeyFor(text);
                            if (!sources.ContainsKey(key))
                            {
                                sources[key] = text;
                                scriptOrder.Add(key);
                            }
                        }
                    }
                }

                // Every key worth a row: what the game has loaded plus what the
                // file already holds. Then write them in play order with section
                // headers; keys the order does not know go last (UI and such).
                var all = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in scriptOrder) if (seen.Add(key)) all.Add(key);
                foreach (string key in fileOrder) if (seen.Add(key)) all.Add(key);

                int written = 0, resolved = 0, unresolved = 0, untranslated = 0;
                string path = PathFor(pluginDirectory, locale);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                ScriptOrder.Data order = ScriptOrder.Load(pluginDirectory);
                using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("key,section,node,order,speaker,source_en,translation");
                    void Emit(string key, string section, string node, string ord, string fallbackSpeaker)
                    {
                        sources.TryGetValue(key, out string src);
                        translations.TryGetValue(key, out string tr);
                        if (src != null) resolved++; else unresolved++;
                        if (string.IsNullOrEmpty(tr)) untranslated++;
                        speakers.TryGetValue(key, out string who);
                        if (string.IsNullOrEmpty(who)) who = fallbackSpeaker;
                        if (string.IsNullOrEmpty(who) && src != null) who = "UI";
                        writer.WriteLine(key + "," + CsvReader.Escape(section) + "," + CsvReader.Escape(node) + "," + ord + "," + CsvReader.Escape(who ?? string.Empty) + "," + CsvReader.Escape(src ?? string.Empty) + "," + CsvReader.Escape(tr ?? string.Empty));
                        written++;
                    }
                    List<string> leftovers;
                    if (order == null)
                    {
                        leftovers = all;
                    }
                    else
                    {
                        ScriptOrder.WriteOrdered(writer, order, all, (key, e) => Emit(key, e.Section, e.Node, e.Order.ToString(), e.Speaker), out leftovers);
                        if (leftovers.Count > 0)
                        {
                            writer.WriteLine();
                            writer.WriteLine("# ===== UI and other text (not part of the dialogue script) =====");
                        }
                    }
                    foreach (string key in leftovers)
                    {
                        Emit(key, order == null ? "" : "UI", "", "", "");
                    }
                }

                string ordered = order == null ? " No script order data found (Export game flow with a level loaded), so rows are in discovery order." : "";
                return $"[working] Wrote {written} row(s) to _discovered/{FileNameFor(locale)}: {resolved} with English, {unresolved} whose text the game has not loaded, {untranslated} still untranslated.{ordered} Edit this file; hot reload applies it. Hash it before committing.";
            }
            catch (Exception ex)
            {
                return $"[working] Failed: {ex.Message}";
            }
        }
    }
}
