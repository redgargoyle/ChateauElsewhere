using UnityEditor;

public sealed class CursorIconImportPostprocessor : AssetPostprocessor
{
    private const string CursorStyleRoot = "Assets/Resources/UI/Cursors/styles/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CursorStyleRoot, System.StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TextureImporter importer = assetImporter as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Cursor;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
