using System;
using System.Collections.Generic;
using TMPro;

namespace DragNWashLocalization
{
    // The game's shipped TMP font assets have no CJK glyphs, so translated
    // Japanese/Chinese text renders as tofu boxes. Rather than touching the
    // game's own font assets, we build dynamic TMP font assets straight from
    // OS-installed CJK fonts (Unity 6's AtlasPopulationMode.DynamicOS) and
    // register them as global TMP fallbacks, so any existing text component
    // picks up missing glyphs from them automatically.
    //
    // Why the extra machinery below: a DynamicOS asset starts out holding a 1x1
    // placeholder atlas texture. The first glyph added reinitializes it to full
    // size, each batch of new glyphs calls Texture2D.Apply, and a filled atlas
    // allocates a whole new texture - all at runtime, on whichever frame a
    // character first appears on screen. On Direct3D 12 that stream of texture
    // allocations and uploads is what reaches
    // D3D12ScratchAllocator::ReleaseExcessScratch and kills the process
    // (Unity UUM-140564), which is why Options - a screen dense with text the
    // player has not seen yet - was the reliable trigger.
    //
    // So: keep the fallback chain down to one font per script, and rasterize
    // every character the loaded translations can need during load rather than
    // during gameplay.
    internal static class FontFallback
    {
        private const int DefaultAtlasPointSize = 64;

        // Ordered by preference; the first one present on the machine wins.
        private static readonly string[] JapaneseCandidates =
        {
            "Yu Gothic UI",
            "Meiryo UI",
            "Meiryo",
            "MS Gothic",
            // macOS
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            // Linux / Steam Deck (Proton exposes fontconfig fonts)
            "Noto Sans CJK JP",
            "Noto Sans JP",
        };

        private static readonly string[] ChineseCandidates =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "SimSun",
            // macOS
            "PingFang SC",
            "Hiragino Sans GB",
            // Linux / Steam Deck
            "Noto Sans CJK SC",
            "Noto Sans SC",
        };

        private static readonly List<TMP_FontAsset> Registered = new List<TMP_FontAsset>();
        private static readonly HashSet<char> Warmed = new HashSet<char>();

        private static bool _installed;

        public static void EnsureCjkFallback()
        {
            if (_installed)
            {
                return;
            }
            _installed = true;

            int pointSize = Plugin.FontAtlasPointSize != null
                ? Plugin.FontAtlasPointSize.Value
                : DefaultAtlasPointSize;

            AddFirstAvailable(JapaneseCandidates, pointSize);
            AddFirstAvailable(ChineseCandidates, pointSize);

            if (Registered.Count == 0)
            {
                Plugin.Log("WARNING: no CJK-capable OS font could be loaded. Japanese/Chinese text may render as missing glyphs.");
                return;
            }

            List<TMP_FontAsset> fallbackList = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
            fallbackList.AddRange(Registered);
            TMP_Settings.fallbackFontAssets = fallbackList;
        }

        private static void AddFirstAvailable(string[] candidates, int pointSize)
        {
            foreach (string family in candidates)
            {
                TMP_FontAsset asset;
                try
                {
                    asset = TMP_FontAsset.CreateFontAsset(family, "Regular", pointSize);
                }
                catch (Exception ex)
                {
                    Plugin.Log($"CJK fallback font '{family}' could not be created: {ex.Message}");
                    continue;
                }

                if (asset == null)
                {
                    // CreateFontAsset already logged which family name it could not resolve.
                    continue;
                }

                asset.name = "DragNWashLocalization_Fallback_" + family.Replace(" ", "");
                Registered.Add(asset);
                Plugin.Log($"CJK fallback font registered: {family} (atlas point size {pointSize})");
                return;
            }
        }

        // Rasterize every non-ASCII character the given texts contain, so no
        // glyph has to be added to an atlas once gameplay is running. Call this
        // from Update or Awake - never from OnGUI or any render callback, since
        // the whole point is to keep atlas uploads away from frames the
        // renderer is busy with.
        public static void Prewarm(IEnumerable<string> texts)
        {
            if (Registered.Count == 0 || texts == null)
            {
                return;
            }

            var pending = new HashSet<char>();
            foreach (string text in texts)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                foreach (char c in text)
                {
                    // ASCII is already covered by the game's own font assets,
                    // so it never reaches the fallback chain.
                    if (c > 0x7F && !Warmed.Contains(c))
                    {
                        pending.Add(c);
                    }
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            var buffer = new char[pending.Count];
            pending.CopyTo(buffer);
            string characters = new string(buffer);

            foreach (TMP_FontAsset asset in Registered)
            {
                try
                {
                    asset.TryAddCharacters(characters, out string missing, includeFontFeatures: false);
                    int missingCount = missing?.Length ?? 0;
                    Plugin.Log($"Prewarmed {pending.Count - missingCount}/{pending.Count} characters into {asset.name}.");
                }
                catch (Exception ex)
                {
                    Plugin.Log($"Failed to prewarm {asset.name}: {ex.Message}");
                }
            }

            Warmed.UnionWith(pending);
        }

        // The debug menu renders these same strings through IMGUI, which has its
        // own dynamic font texture to keep out of gameplay frames.
        public static string WarmedCharacters()
        {
            var buffer = new char[Warmed.Count];
            Warmed.CopyTo(buffer);
            return new string(buffer);
        }
    }
}
