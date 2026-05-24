using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RoomForegroundOccluderExtractor
{
    private const string OutputRoot = "Assets/Art/ForegroundOccluders";

    [MenuItem("Dreadforge/Rooms/Occluders/Export Selected Rectangular Plate")]
    public static void ExportSelectedRectangularPlate()
    {
        ExportSelectedPlate(false);
    }

    [MenuItem("Dreadforge/Rooms/Occluders/Export Selected Rectangular Plate", true)]
    public static bool CanExportSelectedRectangularPlate()
    {
        return HasSelectedOccluderImage();
    }

    [MenuItem("Dreadforge/Rooms/Occluders/Export Selected Dark Foreground Plate")]
    public static void ExportSelectedDarkForegroundPlate()
    {
        ExportSelectedPlate(true);
    }

    [MenuItem("Dreadforge/Rooms/Occluders/Export Selected Dark Foreground Plate", true)]
    public static bool CanExportSelectedDarkForegroundPlate()
    {
        return HasSelectedOccluderImage();
    }

    private static void ExportSelectedPlate(bool darkForegroundOnly)
    {
        if (!TryGetSelectedOccluder(out RoomForegroundOccluder occluder, out RawImage image))
        {
            EditorUtility.DisplayDialog("Occluder Export", "Select a GameObject with a RoomForegroundOccluder or RawImage first.", "OK");
            return;
        }

        Texture sourceTexture = ResolveExtractionTexture(image);
        Rect sourceUvRect = ResolveExtractionUvRect(image);

        if (!TryCreateReadableCopy(sourceTexture, out Texture2D readableSource, out string failureReason))
        {
            EditorUtility.DisplayDialog("Occluder Export", failureReason, "OK");
            return;
        }

        Texture2D plate = ExtractPlate(readableSource, sourceUvRect, darkForegroundOnly);
        UnityEngine.Object.DestroyImmediate(readableSource);

        string assetPath = SavePlateAsset(plate, image, darkForegroundOnly);
        UnityEngine.Object.DestroyImmediate(plate);

        Texture2D importedPlate = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        if (importedPlate == null)
        {
            EditorUtility.DisplayDialog("Occluder Export", $"The plate was written, but Unity could not load it yet:\n{assetPath}", "OK");
            return;
        }

        Undo.RecordObject(image, "Use Extracted Occluder Plate");
        image.texture = importedPlate;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.color = Color.white;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);

        if (occluder != null)
        {
            Undo.RecordObject(occluder, "Use Extracted Occluder Plate");
            occluder.Configure(importedPlate, false, new Rect(0f, 0f, 1f, 1f));
            EditorUtility.SetDirty(occluder);
        }

        Scene scene = image.gameObject.scene;

        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Selection.activeObject = importedPlate;
        EditorGUIUtility.PingObject(importedPlate);
        Debug.Log($"Exported foreground occluder plate: {assetPath}");
    }

    private static bool HasSelectedOccluderImage()
    {
        return TryGetSelectedOccluder(out _, out RawImage image) && ResolveExtractionTexture(image) != null;
    }

    private static bool TryGetSelectedOccluder(out RoomForegroundOccluder occluder, out RawImage image)
    {
        occluder = null;
        image = null;

        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            return false;
        }

        occluder = selected.GetComponent<RoomForegroundOccluder>();

        if (occluder == null)
        {
            occluder = selected.GetComponentInParent<RoomForegroundOccluder>(true);
        }

        image = selected.GetComponent<RawImage>();

        if (image == null && occluder != null)
        {
            image = occluder.GetComponent<RawImage>();
        }

        if (image == null)
        {
            image = selected.GetComponentInChildren<RawImage>(true);
        }

        return image != null;
    }

    private static Texture ResolveExtractionTexture(RawImage image)
    {
        if (image == null)
        {
            return null;
        }

        RoomContentGroup roomContentGroup = image.GetComponentInParent<RoomContentGroup>(true);

        if (roomContentGroup != null && roomContentGroup.RoomBackgroundTexture != null)
        {
            return roomContentGroup.RoomBackgroundTexture;
        }

        return image.texture;
    }

    private static Rect ResolveExtractionUvRect(RawImage image)
    {
        if (image == null)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        RoomContentGroup roomContentGroup = image.GetComponentInParent<RoomContentGroup>(true);
        RectTransform imageRect = image.transform as RectTransform;
        RectTransform roomRect = roomContentGroup != null ? roomContentGroup.transform as RectTransform : null;

        if (roomRect == null || imageRect == null)
        {
            return image.uvRect;
        }

        Vector2 roomSize = roomRect.rect.size;

        if (roomSize.x <= 1f || roomSize.y <= 1f)
        {
            return image.uvRect;
        }

        Vector2 size = imageRect.rect.size;
        Vector2 anchoredPosition = imageRect.anchoredPosition;
        float localLeft = anchoredPosition.x - imageRect.pivot.x * size.x;
        float localBottom = anchoredPosition.y - imageRect.pivot.y * size.y;
        float uvX = (localLeft + roomRect.pivot.x * roomSize.x) / roomSize.x;
        float uvY = (localBottom + roomRect.pivot.y * roomSize.y) / roomSize.y;

        return new Rect(uvX, uvY, size.x / roomSize.x, size.y / roomSize.y);
    }

    private static bool TryCreateReadableCopy(Texture texture, out Texture2D readableTexture, out string failureReason)
    {
        readableTexture = null;
        failureReason = string.Empty;

        if (texture == null)
        {
            failureReason = "The selected RawImage has no texture assigned.";
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(texture);

        if (!string.IsNullOrEmpty(assetPath))
        {
            string fullPath = Path.GetFullPath(assetPath);

            if (File.Exists(fullPath))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                readableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (ImageConversion.LoadImage(readableTexture, bytes))
                {
                    readableTexture.name = texture.name + "_Readable";
                    return true;
                }

                UnityEngine.Object.DestroyImmediate(readableTexture);
                readableTexture = null;
            }
        }

        if (texture is Texture2D texture2D)
        {
            try
            {
                readableTexture = new Texture2D(texture2D.width, texture2D.height, TextureFormat.RGBA32, false);
                readableTexture.SetPixels32(texture2D.GetPixels32());
                readableTexture.Apply(false, false);
                readableTexture.name = texture.name + "_Readable";
                return true;
            }
            catch (UnityException)
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
                readableTexture = null;
            }
        }

        failureReason = $"Could not read pixels from '{texture.name}'. Use an imported PNG/JPG texture or enable Read/Write for the source texture.";
        return false;
    }

    private static Texture2D ExtractPlate(Texture2D source, Rect sourceUvRect, bool darkForegroundOnly)
    {
        Rect uvRect = ClampUvRect(sourceUvRect);
        int left = Mathf.Clamp(Mathf.RoundToInt(uvRect.xMin * source.width), 0, source.width - 1);
        int bottom = Mathf.Clamp(Mathf.RoundToInt(uvRect.yMin * source.height), 0, source.height - 1);
        int right = Mathf.Clamp(Mathf.RoundToInt(uvRect.xMax * source.width), left + 1, source.width);
        int top = Mathf.Clamp(Mathf.RoundToInt(uvRect.yMax * source.height), bottom + 1, source.height);
        int width = right - left;
        int height = top - bottom;

        Color32[] sourcePixels = source.GetPixels32();
        Color32[] cropPixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            int sourceIndex = (bottom + y) * source.width + left;
            int targetIndex = y * width;
            Array.Copy(sourcePixels, sourceIndex, cropPixels, targetIndex, width);
        }

        if (darkForegroundOnly)
        {
            ApplyDarkForegroundAlpha(cropPixels, width, height);
        }

        Texture2D plate = new Texture2D(width, height, TextureFormat.RGBA32, false);
        plate.SetPixels32(cropPixels);
        plate.Apply(false, false);
        return plate;
    }

    private static void ApplyDarkForegroundAlpha(Color32[] pixels, int width, int height)
    {
        float[] luminance = new float[pixels.Length];
        float[] saturation = new float[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            float r = pixel.r / 255f;
            float g = pixel.g / 255f;
            float b = pixel.b / 255f;
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            luminance[i] = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            saturation[i] = max <= 0.001f ? 0f : (max - min) / max;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float lum = luminance[index];
                float sat = saturation[index];
                float edge = GetEdgeStrength(luminance, width, height, x, y);
                float darkInk = Mathf.InverseLerp(0.5f, 0.14f, lum);
                float colorInk = Mathf.InverseLerp(0.42f, 0.12f, lum) * Mathf.Clamp01(sat * 0.95f);
                float edgeInk = Mathf.InverseLerp(0.16f, 0.36f, edge) * Mathf.InverseLerp(0.6f, 0.18f, lum);
                float alpha = Mathf.Max(darkInk, Mathf.Max(colorInk * 0.85f, edgeInk * 0.65f));

                if (lum > 0.56f)
                {
                    alpha = Mathf.Min(alpha, 0.18f);
                }

                if (lum > 0.64f)
                {
                    alpha = 0f;
                }

                if (alpha < 0.13f)
                {
                    alpha = 0f;
                }

                Color32 pixel = pixels[index];
                pixel.a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alpha)) * 255f);
                pixels[index] = pixel;
            }
        }
    }

    private static float GetEdgeStrength(float[] luminance, int width, int height, int x, int y)
    {
        int left = Mathf.Max(0, x - 1);
        int right = Mathf.Min(width - 1, x + 1);
        int bottom = Mathf.Max(0, y - 1);
        int top = Mathf.Min(height - 1, y + 1);
        float horizontal = Mathf.Abs(luminance[y * width + right] - luminance[y * width + left]);
        float vertical = Mathf.Abs(luminance[top * width + x] - luminance[bottom * width + x]);
        return horizontal + vertical;
    }

    private static string SavePlateAsset(Texture2D plate, RawImage sourceImage, bool darkForegroundOnly)
    {
        string roomName = ResolveRoomName(sourceImage.gameObject);
        string directory = $"{OutputRoot}/{SanitizeFileName(roomName)}";
        Directory.CreateDirectory(directory);

        string suffix = darkForegroundOnly ? "dark-plate" : "rect-plate";
        string baseName = SanitizeFileName(sourceImage.gameObject.name);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}_{suffix}.png");
        string fullPath = Path.GetFullPath(assetPath);
        File.WriteAllBytes(fullPath, plate.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return assetPath;
    }

    private static string ResolveRoomName(GameObject gameObject)
    {
        RoomEnvironmentMarker marker = gameObject.GetComponent<RoomEnvironmentMarker>();

        if (marker != null && !string.IsNullOrWhiteSpace(marker.RoomName))
        {
            return marker.RoomName;
        }

        RoomContentGroup roomContentGroup = gameObject.GetComponentInParent<RoomContentGroup>(true);

        if (roomContentGroup != null && !string.IsNullOrWhiteSpace(roomContentGroup.RoomName))
        {
            return roomContentGroup.RoomName;
        }

        return gameObject.scene.IsValid() ? gameObject.scene.name : "UnknownRoom";
    }

    private static Rect ClampUvRect(Rect rect)
    {
        float xMin = Mathf.Clamp01(Mathf.Min(rect.xMin, rect.xMax));
        float yMin = Mathf.Clamp01(Mathf.Min(rect.yMin, rect.yMax));
        float xMax = Mathf.Clamp01(Mathf.Max(rect.xMin, rect.xMax));
        float yMax = Mathf.Clamp01(Mathf.Max(rect.yMin, rect.yMax));
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Item";
        }

        char[] chars = value.Trim().ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim('_');
    }
}
