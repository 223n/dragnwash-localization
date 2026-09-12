using System;
using System.IO;
using UnityEngine;

namespace DragNWashLocalization
{
    // IMGUI can only draw with fonts Unity itself knows: OS fonts by name, or
    // Font assets. Inside Steam's Linux runtime (Steam Deck) no OS font has
    // CJK glyphs, so the menu ships its own: Noto Sans JP baked into an
    // AssetBundle (see assets/menufont/). It is used whenever no OS font
    // renders, and is what makes the menu readable on the Deck.
    internal static class MenuFontBundle
    {
        public const string FileName = "dragnwash-menufont.bundle";
        private static AssetBundle _bundle;

        public static Font TryLoad(string pluginDirectory)
        {
            string path = Path.Combine(pluginDirectory, FileName);
            if (!File.Exists(path)) return null;
            try
            {
                _bundle = _bundle ?? AssetBundle.LoadFromFile(path);
                if (_bundle == null)
                {
                    Plugin.Log($"Menu font bundle could not be opened: {path}");
                    return null;
                }
                Font[] fonts = _bundle.LoadAllAssets<Font>();
                if (fonts == null || fonts.Length == 0)
                {
                    Plugin.Log("Menu font bundle holds no Font asset.");
                    return null;
                }
                Font font = fonts[0];
                Plugin.Log($"Menu font: {font.name} from {FileName} (dynamic={font.dynamic}, size={font.fontSize})");
                return font;
            }
            catch (Exception ex)
            {
                Plugin.Log($"Menu font bundle failed: {ex.Message}");
                return null;
            }
        }
    }
}
