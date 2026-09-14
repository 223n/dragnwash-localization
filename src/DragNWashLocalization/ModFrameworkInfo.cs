using System;
using DragNWash.ModFramework;

namespace DragNWashLocalization
{
    // Tells Drag'n Wash ModFramework's Mods screen what this mod is.
    internal static class ModFrameworkInfo
    {
        // Shown on the Mods screen; translation packs key it by this exact English.
        internal const string Description = "Play Drag'n Wash in 13 languages. Translates dialogue, choices, UI and options.";

        internal static void Register()
        {
            try
            {
                ModFramework.Register(new ModInfo
                {
                    Guid = Plugin.PluginGuid,
                    DisplayName = "Drag'n Wash Localization",
                    Description = Description,
                    Authors = new[] { "TomXV" },
                    Website = "https://github.com/TomXV/dragnwash-localization",
                    // The Mods screen tells players when a newer release is out.
                    UpdateRepository = "TomXV/dragnwash-localization",
                });
            }
            catch (Exception ex)
            {
                Plugin.Log($"[modframework] Could not register with Drag'n Wash ModFramework: {ex.Message}");
            }
        }
    }
}
