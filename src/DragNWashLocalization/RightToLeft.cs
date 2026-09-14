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
                Plugin.Log($"[rtl] Could not set the text direction: {ex.Message}");
            }
        }

        // Hebrew, Arabic (with its supplements and presentation forms), Syriac,
        // Thaana and N'Ko. Rich-text tags are Latin and do not count.
        internal static bool HasRightToLeftLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= '\u0590' && c <= '\u08FF') ||
                    (c >= '\uFB1D' && c <= '\uFDFF') ||
                    (c >= '\uFE70' && c <= '\uFEFC'))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
