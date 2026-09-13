using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityScriptableSettings;

namespace DragNWashLocalization
{
    // A language picker in the game's own Options screen, so players do not
    // need to know about the F1 menu.
    //
    // The Options rows are not hand-built: UnityScriptableSettings'
    // ScriptableSettingSpawner generates one row per Setting in
    // SettingsManager's list whenever Options is opened. So nothing in the
    // game's UI is cloned or edited - a Setting is added to that list and the
    // game builds the row itself, with its own dropdown prefab, styling and
    // pad navigation. If the library is not where it is expected (a game
    // update), nothing is added and the F1 menu stays the way to switch.
    internal static class OptionsLanguage
    {
        // Shown as the row's label; strings.csv translates it like any other
        // UI text. "(Mod)" keeps anyone from mistaking it for a game feature.
        internal const string LabelText = "Language (Mod)";

        private static ModLanguageSetting _setting;
        private static bool _finished;
        private static int _attempts;

        // The game's dropdown prefab is sized for two or three choices, so a
        // dozen languages showed four at a time. When the spawner builds our
        // row, make that row's list tall enough to be usable.
        public static void Install(Harmony harmony)
        {
            try
            {
                MethodInfo create = AccessTools.Method(typeof(ScriptableSettingSpawner), "CreateDropDown", new[] { typeof(SettingInt) });
                if (create != null)
                {
                    harmony.Patch(create, postfix: new HarmonyMethod(typeof(OptionsLanguage), nameof(AfterCreateDropDown)));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[options] Could not hook the dropdown builder: {ex.Message}");
            }
        }

        private static readonly FieldInfo DropdownsField = AccessTools.Field(typeof(ScriptableSettingSpawner), "dropdowns");
        private const int VisibleItems = 10;

        private static void AfterCreateDropDown(ScriptableSettingSpawner __instance, SettingInt option)
        {
            if (_setting == null || !ReferenceEquals(option, _setting) || DropdownsField == null) return;
            try
            {
                var map = DropdownsField.GetValue(__instance) as Dictionary<Setting, TMP_Dropdown>;
                TMP_Dropdown dropdown;
                if (map == null || !map.TryGetValue(option, out dropdown) || dropdown == null || dropdown.template == null) return;

                RectTransform template = dropdown.template;
                float itemHeight = 0f;
                if (dropdown.itemText != null && dropdown.itemText.transform.parent is RectTransform item)
                {
                    itemHeight = item.rect.height;
                }
                if (itemHeight < 1f) itemHeight = 20f;

                int visible = Mathf.Min(_setting.Count, VisibleItems);
                float wanted = itemHeight * visible + 8f;
                Vector2 before = template.sizeDelta;
                if (before.y < wanted)
                {
                    template.sizeDelta = new Vector2(before.x, wanted);
                }

                ScrollRect scroll = template.GetComponent<ScrollRect>();
                if (scroll != null && scroll.scrollSensitivity < itemHeight)
                {
                    scroll.scrollSensitivity = itemHeight;
                }

                Plugin.Log($"[options] Language list: item height {itemHeight:0.#}, list height {before.y:0.#} -> {template.sizeDelta.y:0.#} ({visible} of {_setting.Count} visible), scroll sensitivity {(scroll != null ? scroll.scrollSensitivity.ToString("0.#") : "n/a")}.");
            }
            catch (Exception ex)
            {
                Plugin.Log($"[options] Could not resize the language list: {ex.Message}");
            }
        }

        // Called from Update. SettingsManager lives in the first scene, so this
        // keeps trying for a while and then gives up quietly.
        public static void Tick(string[] locales, Func<string, string> displayName, Func<string> currentLocale,
            Action<string, bool> request, Action commit)
        {
            if (_finished || Time.frameCount % 30 != 0)
            {
                return;
            }
            _attempts++;

            try
            {
                FieldInfo instanceField = AccessTools.Field(typeof(SettingsManager), "instance");
                FieldInfo listField = AccessTools.Field(typeof(SettingsManager), "settings");
                FieldInfo labelField = AccessTools.Field(typeof(Setting), "label");
                if (instanceField == null || listField == null || labelField == null)
                {
                    Finish("[options] The game's settings library has changed shape; the Options language row is disabled. Use the F1 menu.");
                    return;
                }

                object instance = instanceField.GetValue(null);
                var settings = instance != null ? listField.GetValue(instance) as List<Setting> : null;
                if (settings == null || settings.Count == 0)
                {
                    if (_attempts > 240)
                    {
                        Finish("[options] The game's settings never appeared; the Options language row is disabled. Use the F1 menu.");
                    }
                    return;
                }
                if (locales == null || locales.Length < 2)
                {
                    Finish("[options] Fewer than two languages installed; no Options language row.");
                    return;
                }

                SettingGroup group = PickGroup(settings);
                if (group == null)
                {
                    Finish("[options] No settings group to join; the Options language row is disabled.");
                    return;
                }

                var names = new string[locales.Length];
                for (int i = 0; i < locales.Length; i++)
                {
                    names[i] = displayName(locales[i]);
                    // Not game text: keep it out of the untranslated list.
                    IgnoreRules.AddExact(names[i]);
                }

                ModLanguageSetting setting = ScriptableObject.CreateInstance<ModLanguageSetting>();
                setting.name = "DragNWashLocalization_Language";
                // The game unloads unused assets on scene changes; this one is
                // only referenced from the settings list, so pin it.
                setting.hideFlags = HideFlags.DontUnloadUnusedAsset;
                labelField.SetValue(setting, new ScriptableSettingString(LabelText));
                setting.group = group;
                setting.Configure(locales, names, request, commit, currentLocale());
                setting.Sync(currentLocale(), notify: false, committed: true);

                // Not SettingsManager.AddSetting: that re-sorts the whole list
                // with an unstable sort and can shuffle the game's own rows.
                // Put ours after the last row of its group instead.
                int insertAt = settings.Count;
                for (int i = settings.Count - 1; i >= 0; i--)
                {
                    if (settings[i] != null && settings[i].group == group)
                    {
                        insertAt = i + 1;
                        break;
                    }
                }
                settings.Insert(insertAt, setting);
                _setting = setting;

                Finish($"[options] Added \"{LabelText}\" to the Options screen ({locales.Length} languages) after the {Describe(group)} settings. Game settings: {DescribeAll(settings)}");
            }
            catch (Exception ex)
            {
                Finish($"[options] Could not add the Options language row: {ex.GetType().Name}: {ex.Message}. Use the F1 menu.");
            }
        }

        // Keep the dropdown showing the language after a switch made
        // anywhere else (the F1 menu, a hot reload).
        public static void Sync(string locale, bool committed)
        {
            if (_setting != null)
            {
                _setting.Sync(locale, notify: true, committed: committed);
            }
        }

        private static void Finish(string message)
        {
            _finished = true;
            Plugin.Log(message);
        }

        // Gameplay is where a player would look for language; fall back to the
        // last group the game lists.
        private static SettingGroup PickGroup(List<Setting> settings)
        {
            SettingGroup last = null;
            foreach (Setting s in settings)
            {
                if (s == null || s.group == null) continue;
                last = s.group;
                if (Describe(s.group).IndexOf("gameplay", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return s.group;
                }
            }
            return last;
        }

        private static string Describe(SettingGroup group)
        {
            if (group == null) return "(none)";
            string label = null;
            try { label = group.GetLabel().backupString; } catch { }
            return string.IsNullOrEmpty(label) ? group.name : label + " (" + group.name + ")";
        }

        private static string DescribeAll(List<Setting> settings)
        {
            var sb = new StringBuilder();
            foreach (Setting s in settings)
            {
                if (s == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(s.name).Append('/').Append(s.GetType().Name).Append('@').Append(s.group != null ? s.group.name : "-");
            }
            return sb.ToString();
        }
    }

    // The dropdown the game's spawner turns into a row. Its value is the index
    // of the selected locale. It follows the game's own Options flow
    // (SaveButtonOnlyAppearOnChanges), where every setting applies at once and
    // Save makes it stick:
    //   pick an entry  -> the language switches right away, as a preview, and
    //                     the change brings up the game's Save button;
    //   Save           -> the language in use is written to the config;
    //   Back unsaved   -> the game calls Load, which switches back to the last
    //                     saved language.
    // The config file only ever holds a saved choice, so quitting mid-preview
    // starts the next session in the saved language.
    internal sealed class ModLanguageSetting : SettingDropdown
    {
        private string[] _locales = new string[0];
        private Action<string, bool> _request;
        private Action _commit;
        private string _committed;

        internal int Count => _locales.Length;

        internal void Configure(string[] locales, string[] names, Action<string, bool> request, Action commit, string committed)
        {
            _locales = locales;
            _request = request;
            _commit = commit;
            _committed = committed;
            dropdownOptions = new ScriptableSettingString[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                dropdownOptions[i] = new ScriptableSettingString(names[i]);
            }
        }

        // The player picked an entry: preview it.
        public override void SetValue(int value)
        {
            if (_locales.Length == 0) return;
            value = Mathf.Clamp(value, 0, _locales.Length - 1);
            if (value == selectedValue) return;
            selectedValue = value;
            _request?.Invoke(_locales[value], false);
            NotifyChange();
        }

        public override int GetValue()
        {
            return selectedValue;
        }

        // committed: the switch was a saved one (the F1 menu, Save), not a preview.
        internal void Sync(string locale, bool notify, bool committed)
        {
            if (committed && !string.IsNullOrEmpty(locale))
            {
                _committed = locale;
            }
            int index = Array.IndexOf(_locales, locale);
            if (index < 0 || index == selectedValue) return;
            selectedValue = index;
            if (notify) NotifyChange();
        }

        // Save pressed: keep the language now in use.
        public override void Save()
        {
            if (_locales.Length == 0) return;
            string picked = _locales[Mathf.Clamp(selectedValue, 0, _locales.Length - 1)];
            if (picked == _committed) return;
            _committed = picked;
            _commit?.Invoke();
        }

        // Back without saving: return to the saved language.
        public override void Load()
        {
            if (_locales.Length == 0 || string.IsNullOrEmpty(_committed)) return;
            string picked = _locales[Mathf.Clamp(selectedValue, 0, _locales.Length - 1)];
            if (picked == _committed) return;
            _request?.Invoke(_committed, true);
            Sync(_committed, notify: true, committed: true);
        }

        // "Set Default" resets the game's settings, not this one. The language
        // is chosen when the mod is installed (Install.exe writes it), so there
        // is no "default" to go back to, and switching languages behind the
        // player's back would be a surprise.
        public override void ResetToDefault()
        {
        }
    }
}
