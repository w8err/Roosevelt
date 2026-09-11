using UnityEditor;
using UnityEngine;

public class ArtTexturePostprocessor : AssetPostprocessor
{
    const string ArtRoot = "Assets/Art/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        // Block compression smears 1px palette detail on tiny textures.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
