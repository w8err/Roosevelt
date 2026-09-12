using UnityEditor;
using UnityEngine;

// There is no per-font preprocess hook, and OnPreprocessAsset would make every asset
// in the project depend on this class. So fix the settings after import instead.
public class PixelFontPostprocessor : AssetPostprocessor
{
    const string FontRoot = "Assets/Resources/Fonts/";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (!path.StartsWith(FontRoot)) continue;
            if (AssetImporter.GetAtPath(path) is not TrueTypeFontImporter importer) continue;
            // Hinted raster keeps pixel glyph edges hard instead of antialiased.
            if (importer.fontRenderingMode == FontRenderingMode.HintedRaster &&
                importer.fontTextureCase == FontTextureCase.Dynamic) continue;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.SaveAndReimport();
        }
    }
}
