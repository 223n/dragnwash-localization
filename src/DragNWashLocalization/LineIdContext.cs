using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Yarn.Unity;

namespace DragNWashLocalization
{
    // Per-line translations for dialogue.
    //
    // The TMP hook only sees the English it is about to show, so an English
    // line spoken by two characters ("Wonderful!" from Ryan in level 1 and from
    // Alexander in level 5) can only ever get one translation. Yarn knows which
    // line it is, though: the presenter receives a LocalizedLine with its line
    // ID just before its typewriter (or an option item) writes the text into a
    // TMP component.
    //
    // So remember "this component is about to show line X, whose text is T".
    // When the TMP hook then sees exactly T arrive on that component, a row
    // keyed line:X in strings.csv wins over the usual hash row. Anything else
    // the component shows later does not match T and goes the normal way.
    //
    // Verified against the game's stock Yarn Spinner 3 LinePresenter and
    // OptionsPresenter; see docs/PER_LINE_TRANSLATION.ja.md.
    internal static class LineIdContext
    {
        private struct Entry
        {
            public string LineId;
            public string WithName;
            public string WithoutName;
        }

        private static readonly Dictionary<TMP_Text, Entry> ByComponent = new Dictionary<TMP_Text, Entry>();
        private static FieldInfo _optionItemText;

        public static void Install(Harmony harmony)
        {
            // Patched by hand rather than through PatchAll so a Yarn Spinner
            // change that removes one of these members costs this feature only.
            try
            {
                MethodInfo runLine = AccessTools.Method(typeof(LinePresenter), nameof(LinePresenter.RunLineAsync));
                if (runLine != null)
                {
                    harmony.Patch(runLine, prefix: new HarmonyMethod(typeof(LineIdContext), nameof(BeforeRunLine)));
                }
                else
                {
                    Plugin.Log("[lines] LinePresenter.RunLineAsync not found; per-line translations are off for dialogue lines.");
                }

                _optionItemText = AccessTools.Field(typeof(OptionItem), "text");
                MethodInfo optionSetter = AccessTools.PropertySetter(typeof(OptionItem), nameof(OptionItem.Option));
                if (optionSetter != null && _optionItemText != null)
                {
                    harmony.Patch(optionSetter, prefix: new HarmonyMethod(typeof(LineIdContext), nameof(BeforeSetOption)));
                }
                else
                {
                    Plugin.Log("[lines] OptionItem.Option or its text field not found; per-line translations are off for options.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[lines] Could not install per-line translation hooks: {ex.Message}");
            }
        }

        private static void BeforeRunLine(LinePresenter __instance, LocalizedLine line)
        {
            try
            {
                Remember(__instance.lineText, line);
            }
            catch
            {
                // Never stand in the way of the game showing its line.
            }
        }

        private static void BeforeSetOption(OptionItem __instance, DialogueOption value)
        {
            try
            {
                Remember(_optionItemText.GetValue(__instance) as TMP_Text, value?.Line);
            }
            catch
            {
                // As above.
            }
        }

        private static void Remember(TMP_Text component, LocalizedLine line)
        {
            if (component == null || line == null)
            {
                return;
            }
            if (ByComponent.Count > 64)
            {
                Prune();
            }
            ByComponent[component] = new Entry
            {
                LineId = line.TextID,
                WithName = line.Text.Text,
                WithoutName = line.TextWithoutCharacterName.Text,
            };
        }

        // The per-line translation for what is arriving on this component, if
        // the component was just handed that line and the pack has a row for it.
        public static bool TryGetTranslation(TMP_Text component, string incoming, out string translation, out string lineId)
        {
            translation = null;
            lineId = null;
            if (component == null || string.IsNullOrEmpty(incoming) || TranslationStore.LineEntryCount == 0 ||
                !ByComponent.TryGetValue(component, out Entry e))
            {
                return false;
            }

            // OptionItem strikes an unavailable option through.
            const string StrikeOpen = "<s>", StrikeClose = "</s>";
            bool struck = incoming.StartsWith(StrikeOpen, StringComparison.Ordinal) && incoming.EndsWith(StrikeClose, StringComparison.Ordinal);
            string text = struck ? incoming.Substring(StrikeOpen.Length, incoming.Length - StrikeOpen.Length - StrikeClose.Length) : incoming;
            if (text != e.WithName && text != e.WithoutName)
            {
                return false;
            }
            if (!TranslationStore.TryGetLineTranslation(e.LineId, out translation))
            {
                return false;
            }
            if (struck)
            {
                translation = StrikeOpen + translation + StrikeClose;
            }
            lineId = e.LineId;
            return true;
        }

        private static void Prune()
        {
            var dead = new List<TMP_Text>();
            foreach (TMP_Text key in ByComponent.Keys)
            {
                if (key == null)
                {
                    dead.Add(key);
                }
            }
            foreach (TMP_Text key in dead)
            {
                ByComponent.Remove(key);
            }
        }
    }
}
