using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class HeadTurnSpriteImportPostprocessor : AssetPostprocessor
{
    private const string HeadTurnPath = "Assets/Art/Creep_Lady_Frames/headturn/";
    private const float PixelsPerUnit = 100f;

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);

        if (!normalizedPath.StartsWith(HeadTurnPath) || !fileName.StartsWith("headturn_"))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;

        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty spriteMeshType = serializedImporter.FindProperty("m_SpriteMeshType");

        if (spriteMeshType != null)
        {
            spriteMeshType.intValue = (int)SpriteMeshType.FullRect;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
