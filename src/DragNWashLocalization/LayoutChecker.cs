using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace DragNWashLocalization
{
    // Phase 3 layout pass. CJK glyphs render roughly twice as wide as the Latin
    // text this UI was laid out for, so a faithful, short translation can still
    // overflow a button.
    //
    // The first version compared character counts between source and
    // translation, which flagged every CJK string that grew at all - including
    // labels sitting in boxes with room to spare. It had no idea how wide the
    // container was. This version asks TMP itself: GetPreferredValues reports
    // what the translation needs in this component's own font, size and
    // spacing, compared against the component's rect.
    //
    // Auto-sizing labels need a different question. GetPreferredValues measures
    // at the configured font size, but an auto-sizing component shrinks to fit,
    // so the raw numbers say a label needs 96px in a 22px box while the screen
    // shows it rendering perfectly. Such a label cannot overflow - it can only
    // get smaller. For those, compare the translation against the source at the
    // same size: that ratio is how much further the text has to shrink, which
    // is a readability problem rather than a clipping one.
    internal static class LayoutChecker
    {
        // Rects this small belong to components that were never laid out.
        private const float MinimumUsableExtent = 2f;

        // An auto-sizing label that has to render at less than half the size the
        // English did is worth looking at; below that it is just CJK being CJK.
        private const double ShrinkRatioWorthReporting = 2.0;

        public static void Report(string pluginDirectory, double threshold)
        {
            if (TranslationStore.EntryCount == 0)
            {
                return;
            }

            var rows = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int measured = 0;
            int unlaidOut = 0;
            int autoSized = 0;

            try
            {
                foreach (TMP_Text component in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    string source;
                    string translation;
                    Rect rect;
                    try
                    {
                        // A component we translated reports the translation from
                        // .text; the tracked value is the English it had.
                        if (!TmpTextHook.TryGetTrackedSource(component, out source))
                        {
                            source = component.text;
                        }
                        if (string.IsNullOrEmpty(source) ||
                            !TranslationStore.TryGetTranslation(source, out translation))
                        {
                            continue;
                        }
                        rect = component.rectTransform.rect;
                    }
                    catch
                    {
                        continue;
                    }

                    if (rect.width < MinimumUsableExtent || rect.height < MinimumUsableExtent)
                    {
                        unlaidOut++;
                        continue;
                    }

                    string path = DescribePath(component);
                    if (!seen.Add(source + "\u0000" + path))
                    {
                        continue;
                    }

                    double required;
                    double available;
                    string axis;
                    double limit;
                    try
                    {
                        if (component.enableAutoSizing)
                        {
                            autoSized++;
                            // How much smaller this has to render than English.
                            required = component.GetPreferredValues(translation).x;
                            available = component.GetPreferredValues(source).x;
                            axis = "shrink";
                            limit = ShrinkRatioWorthReporting;
                        }
                        else if (component.textWrappingMode == TextWrappingModes.NoWrap ||
                                 component.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap)
                        {
                            // Nowhere to put the extra width but outside the box.
                            required = component.GetPreferredValues(translation).x;
                            available = rect.width;
                            axis = "width";
                            limit = threshold;
                        }
                        else
                        {
                            // Wrapping turns extra width into extra lines, so the
                            // question is whether those lines still fit.
                            required = component.GetPreferredValues(translation, rect.width, 0f).y;
                            available = rect.height;
                            axis = "height";
                            limit = threshold;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    measured++;
                    if (available <= 0 || required <= available * limit)
                    {
                        continue;
                    }

                    rows.Add(string.Concat(
                        CsvReader.Escape(source), ",",
                        CsvReader.Escape(translation), ",",
                        axis, ",",
                        required.ToString("0.#"), ",",
                        available.ToString("0.#"), ",",
                        (required / available).ToString("0.##"), ",",
                        CsvReader.Escape(path)));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[layout] Layout check failed: {ex.Message}");
                return;
            }

            if (measured == 0)
            {
                Plugin.Log("[layout] No translated text is on screen yet; open the screens you want checked first.");
                return;
            }

            // Worst offenders first: the ratio is the last field before the path.
            rows.Sort((a, b) => RatioOf(b).CompareTo(RatioOf(a)));

            string dir = Path.Combine(pluginDirectory, "Translations", "_discovered");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, "layout_risks.csv");

            // Written even when empty. Returning early here used to leave the
            // previous run's file on disk, so a clean result still read as a
            // list of problems.
            if (rows.Count == 0)
            {
                try
                {
                    using (var empty = new StreamWriter(filePath, append: false, Encoding.UTF8))
                    {
                        empty.WriteLine("source_en,translation,axis,required_px,available_px,ratio,object_path");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log($"[layout] Could not clear the previous report: {ex.Message}");
                }

                Plugin.Log($"[layout] {measured} translated label(s) measured, all fit. {autoSized} auto-size (they shrink rather than overflow), {unlaidOut} not laid out yet.");
                return;
            }

            using (var writer = new StreamWriter(filePath, append: false, Encoding.UTF8))
            {
                writer.WriteLine("source_en,translation,axis,required_px,available_px,ratio,object_path");
                foreach (string row in rows)
                {
                    writer.WriteLine(row);
                }
            }

            Plugin.Log($"[layout] {rows.Count} of {measured} translated label(s) need a look ({autoSized} auto-size, reported as 'shrink' rather than overflow); see {filePath}");
        }

        private static double RatioOf(string row)
        {
            try
            {
                string[] parts = row.Split(',');
                double value;
                // Walk back from the path field, which may itself contain commas.
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    if (double.TryParse(parts[i], out value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
            }
            return 0;
        }

        private static string DescribePath(TMP_Text component)
        {
            try
            {
                var parts = new List<string>();
                Transform current = component.transform;
                for (int depth = 0; current != null && depth < 12; depth++)
                {
                    parts.Add(current.name);
                    current = current.parent;
                }
                parts.Reverse();
                return string.Join("/", parts.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
