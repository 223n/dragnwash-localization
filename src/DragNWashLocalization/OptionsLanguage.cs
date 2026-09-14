using System;
using DragNWash.ModFramework;

namespace DragNWashLocalization
{
    // A language picker in the game's own Options screen, so players do not
    // need to know about the F1 tool window. The framework builds the row
    // (GameOptions) and follows the game's Options flow:
    //   pick an entry  -> the language switches right away, as a preview, and
    //                     the change brings up the game's Save button;
    //   Save           -> the language in use is written to the config;
    //   Back unsaved   -> the framework previews the saved language again.
    // The config file only ever holds a saved choice, so quitting mid-preview
    // starts the next session in the saved language.
    internal static class OptionsLanguage
    {
        // Shown as the row's label; strings.csv translates it like any other
        // UI text. "(Mod)" keeps anyone from mistaking it for a game feature.
        internal const string LabelText = "Language (Mod)";
        internal const string Id = "com.tomxv.dragnwash.localization.language";

        private static bool _registered;

        public static void Register(string[] locales, Func<string, string> displayName, Func<string> savedLocale,
            Action<string, bool> request, Action<string> commit)
        {
            if (locales == null || locales.Length < 2)
            {
                Plugin.Log("[options] Fewer than two languages installed; no Options language row.");
                return;
            }

            var names = new string[locales.Length];
            for (int i = 0; i < locales.Length; i++)
            {
                names[i] = displayName(locales[i]);
                // Not game text: keep it out of the untranslated list.
                IgnoreRules.AddExact(names[i]);
            }

            GameOptions.AddChoice(new OptionsChoice
            {
                Id = Id,
                Label = LabelText,
                Choices = names,
                Section = OptionsSection.Gameplay,
                // "Set Default" resets the game's settings, not this one. The
                // language is chosen when the mod is installed, so there is no
                // default to go back to.
                DefaultIndex = -1,
                GetSaved = () => Math.Max(0, Array.IndexOf(locales, savedLocale())),
                Save = i => commit(locales[i]),
                Preview = i => request(locales[i], false),
            });
            _registered = true;
            Plugin.Log($"[options] Asked the framework for the \"{LabelText}\" row ({locales.Length} languages).");
        }

        // Show the saved language again after a switch made elsewhere (the F1
        // window, a Back without saving).
        public static void Refresh()
        {
            if (_registered)
            {
                GameOptions.Refresh(Id);
            }
        }
    }
}
