using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildBundle
{
    public static void Build()
    {
        string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Bundles");
        Directory.CreateDirectory(outDir);
        var importer = AssetImporter.GetAtPath("Assets/Fonts/NotoSansJP.ttf") as TrueTypeFontImporter;
        if (importer != null)
        {
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
            importer.fontSize = 14;
            importer.SaveAndReimport();
        }
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "dragnwash-menufont",
                assetNames = new[] { "Assets/Fonts/NotoSansJP.ttf" },
            },
        };
        var manifest = BuildPipeline.BuildAssetBundles(outDir, builds,
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        Debug.Log("BUNDLE_RESULT " + (manifest != null ? "ok" : "failed"));
    }
}
