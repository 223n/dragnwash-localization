using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DragNWashLocalization
{
    internal static class TmpTextHook
    {
        // The game only sets a TMP_Text's text when it wants to change it. Once
        // we rewrite English -> a translation, that translation is what the
        // component now holds, and switching locale never re-runs the setter,
        // so text translated once would stay frozen in the first language.
        //
        // Remember the original English source per component, and on locale
        // switch re-apply the new translation to every component we have seen.
        // Numbers/resolutions/timers are skipped: they are the game's own
        // dynamic values and keep updating themselves.
        private static readonly Dictionary<TMP_Text, string> SourceByComponent =
            new Dictionary<TMP_Text, string>();

        // Set while RefreshAll re-applies text so the hook does not re-translate
        // (or "discover") the translations it just wrote.
        internal static bool SuppressRewrite;

        internal static void Rewrite(TMP_Text instance, ref string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (SuppressRewrite)
            {
                return;
            }

            string source = text;
            try
            {
                bool translated = TranslationStore.TryGetTranslation(source, out var translation);
                bool ignored = IgnoreRules.IsIgnored(source);

                if (instance != null && !ignored)
                {
                    SourceByComponent[instance] = source;
                }

                if (translated)
                {
                    text = translation;
                }
                else
                {
                    // Queues in memory; Plugin.Update writes the discovery CSV.
                    TranslationStore.NoteDiscoveredText(source);
                }

                // Slider values, resolutions and the like would otherwise bury
                // the lines a translator is looking for. An explicit entry in
                // strings.csv still wins, hence the `translated` check first.
                if (Plugin.VerboseTextLog != null && Plugin.VerboseTextLog.Value &&
                    TranslationStore.IsFirstApplication(source) &&
                    (translated || !ignored))
                {
                    Plugin.Log(translated
                        ? $"[OK] \"{source}\" -> \"{translation}\""
                        : $"[--] \"{source}\"");
                }
            }
            catch (Exception ex)
            {
                // A localization failure must not prevent TMP from setting text.
                Plugin.Log($"[text] Failed to localize text: {ex.Message}");
            }
        }

        // A component we have already translated reports its translation from
        // .text, not the English it started as. UiTextDumper needs the source.
        internal static bool TryGetTrackedSource(TMP_Text instance, out string source)
        {
            return SourceByComponent.TryGetValue(instance, out source);
        }

        // Text authored in a prefab and never assigned at runtime is
        // deserialized straight into m_text: neither the setter nor SetText is
        // ever called, so the hook above never sees it. That covers most of the
        // game's static UI - the pause menu, the main menu. Catch those when
        // the component is enabled, which is also when a menu is shown.
        internal static void TranslateExisting(TMP_Text instance)
        {
            if (instance == null || SuppressRewrite)
            {
                return;
            }

            try
            {
                // Already seen through the setter; its text is ours to manage
                // and re-reading it here would treat a translation as a source.
                if (SourceByComponent.ContainsKey(instance))
                {
                    return;
                }

                string source = instance.text;
                if (string.IsNullOrEmpty(source))
                {
                    return;
                }

                bool translated = TranslationStore.TryGetTranslation(source, out var translation);
                bool ignored = IgnoreRules.IsIgnored(source);

                if (!ignored)
                {
                    SourceByComponent[instance] = source;
                }

                if (translated)
                {
                    // Assigning would re-enter the setter patch and treat the
                    // translation as a new source string.
                    SuppressRewrite = true;
                    try
                    {
                        instance.text = translation;
                    }
                    finally
                    {
                        SuppressRewrite = false;
                    }
                }
                else
                {
                    TranslationStore.NoteDiscoveredText(source);
                }

                if (Plugin.VerboseTextLog != null && Plugin.VerboseTextLog.Value &&
                    TranslationStore.IsFirstApplication(source) &&
                    (translated || !ignored))
                {
                    Plugin.Log(translated
                        ? $"[OK] \"{source}\" -> \"{translation}\""
                        : $"[--] \"{source}\"");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[text] Failed to localize existing text: {ex.Message}");
            }
        }

        // Re-apply the currently loaded locale to every tracked component.
        // Runs on the main thread (Plugin.Update); never from a render callback.
        internal static void RefreshAll()
        {
            var snapshot = new List<KeyValuePair<TMP_Text, string>>(SourceByComponent);
            var dead = new List<TMP_Text>();

            SuppressRewrite = true;
            try
            {
                foreach (KeyValuePair<TMP_Text, string> kv in snapshot)
                {
                    TMP_Text instance = kv.Key;
                    if (instance == null)
                    {
                        dead.Add(instance);
                        continue;
                    }

                    string source = kv.Value;
                    bool translated = TranslationStore.TryGetTranslation(source, out var translation);
                    instance.text = translated ? translation : source;

                    // Re-applying on a locale switch is exactly what the verbose
                    // log exists to show, but the suppression above bypasses the
                    // normal hook path, so log it here instead. AppliedOnce is
                    // cleared by TranslationStore.Load, so each switch logs its
                    // strings once.
                    if (Plugin.VerboseTextLog != null && Plugin.VerboseTextLog.Value &&
                        TranslationStore.IsFirstApplication(source))
                    {
                        Plugin.Log(translated
                            ? $"[OK] \"{source}\" -> \"{translation}\""
                            : $"[--] \"{source}\"");
                    }
                }
            }
            finally
            {
                SuppressRewrite = false;
            }

            foreach (TMP_Text key in dead)
            {
                SourceByComponent.Remove(key);
            }
        }
    }

    // Rewrite the argument before TMP builds its internal text buffers. Calling
    // the setter again from a postfix needlessly re-enters TMP's update path.
    [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
    internal static class TmpText_Set_Patch
    {
        private static void Prefix(TMP_Text __instance, ref string __0)
        {
            TmpTextHook.Rewrite(__instance, ref __0);
        }
    }

    // In the shipped TMP assembly both overloads write their own buffers and
    // neither calls the text setter or the other overload. Patch both paths.
    [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string) })]
    internal static class TmpText_SetText_Patch
    {
        private static void Prefix(TMP_Text __instance, ref string __0)
        {
            TmpTextHook.Rewrite(__instance, ref __0);
        }
    }

    [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string), typeof(bool) })]
    internal static class TmpText_SetTextWithBool_Patch
    {
        private static void Prefix(TMP_Text __instance, ref string __0)
        {
            TmpTextHook.Rewrite(__instance, ref __0);
        }
    }

    // Static prefab text reaches us only here. Postfix so the component has
    // finished its own enable work before the text changes underneath it.
    [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
    internal static class TmpUgui_OnEnable_Patch
    {
        private static void Postfix(TextMeshProUGUI __instance)
        {
            TmpTextHook.TranslateExisting(__instance);
        }
    }

    [HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
    internal static class TmpWorld_OnEnable_Patch
    {
        private static void Postfix(TextMeshPro __instance)
        {
            TmpTextHook.TranslateExisting(__instance);
        }
    }
}
