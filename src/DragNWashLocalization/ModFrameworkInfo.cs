using System;
using System.Reflection;

namespace DragNWashLocalization
{
    // Tells Drag'n Wash ModFramework's Mods screen what this mod is, when the
    // framework is installed. The framework is optional for now (it becomes
    // required in v1.0.0), so it is reached by reflection instead of a
    // compile-time reference, and a soft BepInDependency makes BepInEx load it
    // first when it is there.
    internal static class ModFrameworkInfo
    {
        internal const string FrameworkGuid = "com.tomxv.dragnwash.modframework";

        // Shown on the Mods screen; translation packs key it by this exact English.
        internal const string Description = "Play Drag'n Wash in 13 languages. Translates dialogue, choices, UI and options.";

        internal static void Register()
        {
            try
            {
                Type modFramework = Type.GetType("DragNWash.ModFramework.ModFramework, DragNWash.ModFramework");
                Type modInfo = Type.GetType("DragNWash.ModFramework.ModInfo, DragNWash.ModFramework");
                MethodInfo register = modFramework?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                if (modInfo == null || register == null)
                {
                    return;
                }

                object info = Activator.CreateInstance(modInfo);
                Set(modInfo, info, "Guid", Plugin.PluginGuid);
                Set(modInfo, info, "DisplayName", "Drag'n Wash Localization");
                Set(modInfo, info, "Description", Description);
                Set(modInfo, info, "Authors", new[] { "TomXV" });
                Set(modInfo, info, "Website", "https://github.com/TomXV/dragnwash-localization");
                register.Invoke(null, new[] { info });
                Plugin.Log("[modframework] Registered with Drag'n Wash ModFramework.");
            }
            catch (Exception ex)
            {
                Plugin.Log($"[modframework] Could not register with Drag'n Wash ModFramework: {ex.Message}");
            }
        }

        // A property this framework version does not have is skipped.
        private static void Set(Type type, object target, string name, object value)
        {
            type.GetProperty(name)?.SetValue(target, value, null);
        }
    }
}
