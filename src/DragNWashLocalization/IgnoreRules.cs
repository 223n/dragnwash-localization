using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DragNWashLocalization
{
    // The TMP hook sees every string the game puts on screen, including plenty
    // that is never worth translating: slider values, resolutions, framerates,
    // build stamps. Those would otherwise pile up in the discovery CSV and in
    // the debug log and bury the lines a translator actually needs.
    //
    // This only suppresses *reporting* a string as untranslated. Translation
    // lookup happens first and is untouched, so putting an ignored string in
    // strings.csv still translates it - useful if a game ever labels something
    // like "1080p" in a way that does need localizing.
    internal static class IgnoreRules
    {
        private static readonly string[] BuiltIn =
        {
            // Numbers on their own: "0", "-3", "0.001", "1,250", "45%".
            // At least one actual digit, and the lookahead is what demands it.
            // Writing that as [\d.,]*\d[\d.,]* splits one run of digits two
            // ways and backtracks quadratically over any long string that does
            // not match. Everything before the first digit can only be "." or
            // ",", so the lookahead itself never backtracks, and [\d.,] shares
            // no character with \s or %, so the run never gives anything back.
            @"^[+-]?(?=[.,]*\d)(?>[\d.,]+)\s*%?$",
            // Resolutions, with or without a refresh rate.
            @"^\d+\s*[x×]\s*\d+(\s*@\s*[\d.]+\s*Hz)?$",
            // Bare refresh rates and framerates: "60Hz", "144 Hz", "30 FPS".
            @"^[\d.]+\s*(Hz|FPS)$",
            // Build stamps such as "9/9/2026_ee944596", and plain dates.
            @"^\d{1,2}/\d{1,2}/\d{2,4}(_[0-9a-fA-F]+)?$",
            // Clocks and timers: "1:23", "00:05:12".
            @"^\d{1,2}(:\d{2})+$",
        };

        private static readonly List<Regex> Patterns = new List<Regex>();

        // Patterns come from Translations/ignore.txt, which a translator edits.
        // Catastrophic backtracking does not throw - it simply never returns, on
        // the Unity main thread, for every distinct string the game shows. A
        // match timeout turns that into a RegexMatchTimeoutException IsIgnored
        // can handle.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        // One log line per pattern, not per string: IsIgnored runs for every
        // distinct string on screen, so reporting every timeout would bury the
        // log. Index-aligned with Patterns.
        private static readonly List<bool> TimeoutReported = new List<bool>();

        // Whole strings the mod itself puts on screen, such as language names
        // in the Options dropdown, which are not game text to translate.
        private static readonly HashSet<string> Exact = new HashSet<string>(StringComparer.Ordinal);

        public static void AddExact(string text)
        {
            if (!string.IsNullOrEmpty(text)) Exact.Add(text.Trim());
        }

        // Loaded once; the rules do not depend on the selected locale.
        private static bool _loaded;

        public static int PatternCount => Patterns.Count;

        public static void Load(string pluginDirectory)
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            foreach (string pattern in BuiltIn)
            {
                TryAdd(pattern, "built-in");
            }

            // Optional, so a missing file is not a problem. Anything here is
            // added to the built-in rules rather than replacing them.
            string path = Path.Combine(pluginDirectory, "Translations", "ignore.txt");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    TryAdd(line, "ignore.txt");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"Failed to read ignore.txt: {ex.Message}");
            }
        }

        private static void TryAdd(string pattern, string origin)
        {
            try
            {
                Patterns.Add(new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout));
                TimeoutReported.Add(false);
            }
            catch (ArgumentException ex)
            {
                Plugin.Log($"Skipping invalid ignore pattern from {origin}: {pattern} ({ex.Message})");
            }
        }

        // Callers dedupe before reaching this, so each distinct string is
        // matched once rather than once per frame.
        public static bool IsIgnored(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return true;
            }

            if (Exact.Contains(trimmed))
            {
                return true;
            }

            for (int i = 0; i < Patterns.Count; i++)
            {
                try
                {
                    if (Patterns[i].IsMatch(trimmed))
                    {
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Without this the rule the translator wrote would quietly
                    // stop working, with nothing anywhere to say why.
                    if (!TimeoutReported[i])
                    {
                        TimeoutReported[i] = true;
                        Plugin.Log($"Ignore pattern timed out after {MatchTimeout.TotalMilliseconds:0} ms and is treated as no match: {Patterns[i]}");
                    }
                }
                catch (Exception)
                {
                    // A pathological pattern must not break text rendering.
                }
            }

            return false;
        }
    }
}
