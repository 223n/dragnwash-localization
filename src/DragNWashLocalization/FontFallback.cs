using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEngine.TextCore.LowLevel;

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

            int before = Registered.Count;
            AddFirstAvailable(JapaneseCandidates, pointSize);
            bool gotJapanese = Registered.Count > before;
            before = Registered.Count;
            AddFirstAvailable(ChineseCandidates, pointSize);
            bool gotChinese = Registered.Count > before;

            // Steam's Linux runtime container, macOS, and stripped-down systems
            // do not always expose fonts by family name, but the files are
            // still there. Try known paths (and a fonts/ folder next to the
            // plugin, for anyone who wants to drop in their own).
            if (!gotJapanese) gotJapanese = AddFirstFile(JapaneseFiles, 0, pointSize, "Japanese");
            if (!gotChinese) gotChinese = AddFirstFile(ChineseFiles, 2, pointSize, "Chinese");

            if (Registered.Count == 0)
            {
                Plugin.Log("WARNING: no CJK-capable OS font could be loaded. Japanese/Chinese text may render as missing glyphs.");
                LogSystemFontNames();
                return;
            }

            List<TMP_FontAsset> fallbackList = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
            fallbackList.AddRange(Registered);
            TMP_Settings.fallbackFontAssets = fallbackList;
        }

        // (path, face index inside a .ttc). Face 0 of NotoSansCJK-*.ttc is JP,
        // 1 KR, 2 SC, 3 TC. "~" expands to the home directory; "fonts/" is
        // relative to the plugin folder.
        private static readonly string[] JapaneseFiles =
        {
            "fonts/*jp*.ttf", "fonts/*jp*.otf", "fonts/*.ttc", "fonts/*.ttf", "fonts/*.otf",
            "/run/host/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
            "~/.local/share/fonts/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/noto/NotoSansJP-Regular.ttf",
            "/System/Library/Fonts/ヒラギノ角ゴシック W3.ttc",
            "/System/Library/Fonts/Hiragino Sans GB.ttc",
            "C:/Windows/Fonts/YuGothM.ttc",
            "C:/Windows/Fonts/msgothic.ttc",
        };

        private static readonly string[] ChineseFiles =
        {
            "fonts/*sc*.ttf", "fonts/*sc*.otf", "fonts/*.ttc",
            "/run/host/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
            "~/.local/share/fonts/NotoSansCJK-Regular.ttc",
            "/System/Library/Fonts/PingFang.ttc",
            "C:/Windows/Fonts/msyh.ttc",
        };

        private static bool AddFirstFile(string[] patterns, int ttcFace, int pointSize, string label)
        {
            foreach (string pattern in patterns)
            {
                foreach (string path in Expand(pattern))
                {
                    int face = path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) ? ttcFace : 0;
                    TMP_FontAsset asset;
                    try
                    {
                        asset = TMP_FontAsset.CreateFontAsset(path, face, pointSize, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log($"CJK fallback font file '{path}' could not be loaded: {ex.Message}");
                        continue;
                    }
                    if (asset == null)
                    {
                        continue;
                    }
                    asset.name = "DragNWashLocalization_Fallback_" + label + "_" + Path.GetFileNameWithoutExtension(path);
                    Registered.Add(asset);
                    Plugin.Log($"CJK fallback font registered from file: {path} (face {face}, atlas point size {pointSize})");
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<string> Expand(string pattern)
        {
            string home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (pattern.StartsWith("~/", StringComparison.Ordinal))
            {
                pattern = Path.Combine(home, pattern.Substring(2));
            }
            else if (pattern.StartsWith("fonts/", StringComparison.Ordinal))
            {
                pattern = Path.Combine(Plugin.PluginDirectory, pattern);
            }

            string dir = Path.GetDirectoryName(pattern);
            string file = Path.GetFileName(pattern);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                yield break;
            }
            if (file.IndexOf('*') < 0)
            {
                if (File.Exists(pattern)) yield return pattern;
                yield break;
            }
            string[] matches;
            try { matches = Directory.GetFiles(dir, file); } catch { yield break; }
            Array.Sort(matches, StringComparer.OrdinalIgnoreCase);
            foreach (string m in matches) yield return m;
        }

        private static void LogSystemFontNames()
        {
            try
            {
                string[] names = FontEngine.GetSystemFontNames() ?? new string[0];
                var cjk = new List<string>();
                foreach (string n in names)
                {
                    if (n.IndexOf("CJK", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Noto", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Gothic", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Hei", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Hiragino", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("PingFang", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cjk.Add(n);
                        if (cjk.Count >= 12) break;
                    }
                }
                Plugin.Log($"[font] The system reports {names.Length} font families; CJK-looking ones: {(cjk.Count == 0 ? "(none)" : string.Join(", ", cjk))}");
            }
            catch (Exception ex)
            {
                Plugin.Log($"[font] Could not list system fonts: {ex.Message}");
            }
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
