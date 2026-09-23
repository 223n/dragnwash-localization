using System;
using System.Collections.Generic;
using TMPro;

namespace DragNWashLocalization
{
    // Hebrew (and any other right-to-left locale someone adds) has to be drawn
    // right-to-left. TextMeshPro does not detect that from the text; it has an
    // explicit per-component switch. Flip it on for a component while an RTL
    // locale is loaded and the text it is about to show is written in an RTL
    // script, and off otherwise. Text the pack leaves in English - the names in
    // the credits, numbers, the version string - stays left-to-right, or TMP
    // would print it backwards.
    //
    // Translation files stay in normal logical order - the order the language is
    // typed and stored. TMP reverses it for display. Because TMP reverses the
    // whole line rather than running the Unicode bidi algorithm, an RTL
    // translation should avoid embedded Latin words and digits: those come out
    // backwards. The Hebrew pack transliterates names for that reason.
    internal static class RightToLeft
    {
        private static readonly HashSet<string> RtlLanguages =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "he", "iw",   // Hebrew (iw is the old code)
                "ar",         // Arabic
                "fa",         // Persian
                "ur",         // Urdu
                "yi",         // Yiddish
            };

        public static bool Active { get; private set; }

        public static void SetLocale(string locale)
        {
            string language = locale ?? string.Empty;
            int dash = language.IndexOf('-');
            if (dash > 0) language = language.Substring(0, dash);
            bool active = RtlLanguages.Contains(language);
            if (active != Active)
            {
                Plugin.Log(active
                    ? $"[rtl] {locale} is right-to-left; TMP text is flipped while it is loaded."
                    : "[rtl] Back to a left-to-right locale.");
            }
            Active = active;
        }

        public static void Apply(TMP_Text instance, string shownText)
        {
            if (instance == null) return;
            try
            {
                bool rtl = Active && HasRightToLeftLetters(shownText);
                if (instance.isRightToLeftText != rtl)
                {
                    instance.isRightToLeftText = rtl;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[rtl] Could not set the text direction: {ex.Message}", LogKind.Error);
            }
        }

        // Hebrew, Arabic (with its supplements and presentation forms), Syriac,
        // Thaana and N'Ko. Rich-text tags are Latin and do not count.
        internal static bool HasRightToLeftLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (IsRightToLeftLetter(c))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRightToLeftLetter(char c)
        {
            return (c >= '\u0590' && c <= '\u08FF') ||
                   (c >= '\uFB1D' && c <= '\uFDFF') ||
                   (c >= '\uFE70' && c <= '\uFEFC');
        }

        private static bool IsLeftToRightLetter(char c)
        {
            return char.IsLetterOrDigit(c) && !IsRightToLeftLetter(c);
        }

        // The F1 window (IMGUI) draws a string left to right in the order it is
        // stored, so a Hebrew name there would read backwards. This puts the
        // characters in the order they are seen. A line that starts in Hebrew
        // is turned around with its runs of Latin letters and digits turned
        // back; a line that starts in Latin letters keeps its order with the
        // Hebrew runs in it turned around. Brackets in the turned parts are
        // mirrored. Enough for a language name or a translator's name. It is
        // not the whole Unicode bidi algorithm, and Arabic would also need its
        // letters joined, which this does not do.
        internal static string ForLeftToRightDrawing(string text)
        {
            if (!HasRightToLeftLetters(text)) return text;
            char[] c = text.ToCharArray();
            bool rightToLeftLine = false;
            foreach (char ch in c)
            {
                if (IsRightToLeftLetter(ch)) { rightToLeftLine = true; break; }
                if (IsLeftToRightLetter(ch)) break;
            }
            if (rightToLeftLine)
            {
                TurnAround(c, 0, c.Length);
                TurnRunsAround(c, IsLeftToRightLetter, IsRightToLeftLetter);
            }
            else
            {
                TurnRunsAround(c, IsRightToLeftLetter, IsLeftToRightLetter);
            }
            return new string(c);
        }

        // Turns around each run that starts and ends with a letter of one
        // direction, going over spaces and punctuation but not over a letter of
        // the other direction. Turning a run back also mirrors its brackets
        // back.
        private static void TurnRunsAround(char[] c, Func<char, bool> inRun, Func<char, bool> endsRun)
        {
            int i = 0;
            while (i < c.Length)
            {
                if (!inRun(c[i]))
                {
                    i++;
                    continue;
                }
                int end = i;
                for (int j = i; j < c.Length && !endsRun(c[j]); j++)
                {
                    if (inRun(c[j])) end = j;
                }
                TurnAround(c, i, end - i + 1);
                i = end + 1;
            }
        }

        private static void TurnAround(char[] c, int start, int length)
        {
            Array.Reverse(c, start, length);
            for (int k = start; k < start + length; k++)
            {
                c[k] = Mirrored(c[k]);
            }
        }

        private static char Mirrored(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '<': return '>';
                case '>': return '<';
                default: return c;
            }
        }
    }
}
