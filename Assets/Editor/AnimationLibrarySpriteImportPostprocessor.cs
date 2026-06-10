using UnityEditor;
using UnityEngine;

public sealed class AnimationLibrarySpriteImportPostprocessor : AssetPostprocessor
{
    private const string LibraryRoot = "Assets/Art/Library/AnimationLibrary/";
    private const float PixelsPerUnit = 100f;

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');

        if (!IsAnimationLibrarySprite(normalizedPath))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
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
