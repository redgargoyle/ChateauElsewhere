using UnityEditor;
using UnityEngine;

public sealed class AnimationLibrarySpriteImportPostprocessor : AssetPostprocessor
{
    private const string LibraryRoot = "Assets/Art/Library/AnimationLibrary/";

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');

        if (!IsAnimationLibrarySprite(normalizedPath))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        // Sprite mode and PPU are authored presentation data. In particular,
        // Chapter 2 panic frames use fixed per-character PPU values, and run
        // frames retain their cropped sub-sprite IDs. Do not overwrite either.
        importer.spritePivot = new Vector2(0.5f, 0f);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty spriteMeshType = serializedImporter.FindProperty("m_SpriteMeshType");

        if (spriteMeshType != null)
        {
            spriteMeshType.intValue = (int)SpriteMeshType.FullRect;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool IsAnimationLibrarySprite(string normalizedPath)
    {
        if (!normalizedPath.StartsWith(LibraryRoot) || !normalizedPath.EndsWith(".png"))
        {
            return false;
        }

        return normalizedPath.Contains("/approved/") ||
            normalizedPath.Contains("/intake/") ||
            normalizedPath.Contains("/reference/full_body/");
    }
}
