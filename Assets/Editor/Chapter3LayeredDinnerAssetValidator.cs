#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Chapter3LayeredDinnerAssetValidator
{
    private const string LayeredFolder = "Assets/Resources/Chapter3/Dining/Layered";
    private const string ManifestPath = "Assets/Resources/Chapter3/Dining/LayeredDinnerManifest.asset";
    private const string AutoRunSessionKey = "Chapter3LayeredDinnerAssetValidator.AutoRun";

    [InitializeOnLoadMethod]
    private static void AutoCreateManifestIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(AutoRunSessionKey, false))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(LayeredFolder) ||
                AssetDatabase.LoadAssetAtPath<Chapter3LayeredDinnerAssetManifest>(ManifestPath) != null)
            {
                return;
            }

            SessionState.SetBool(AutoRunSessionKey, true);
            CreateOrUpdateLayeredDinnerManifest();
        };
    }

    [MenuItem("Dreadforge/Chapter 3/Validate Layered Dinner Assets")]
    public static void ValidateLayeredDinnerAssets()
    {
        Chapter3LayeredDinnerAssetManifest manifest =
            AssetDatabase.LoadAssetAtPath<Chapter3LayeredDinnerAssetManifest>(ManifestPath);

        if (manifest == null)
        {
            manifest = CreateOrUpdateLayeredDinnerManifest();
        }

        if (manifest == null)
        {
            Debug.LogError("[Ch3DiningValidator] Could not create or load LayeredDinnerManifest.");
            return;
        }

        bool valid = manifest.Validate(out string message);

        if (valid)
        {
            Debug.Log($"[Ch3DiningValidator] Layered dinner assets valid.\n{BuildReport(manifest)}\n{message}");
        }
        else
        {
            Debug.LogError($"[Ch3DiningValidator] Layered dinner assets invalid.\n{BuildReport(manifest)}\n{message}");
        }
    }

    [MenuItem("Dreadforge/Chapter 3/Create/Update Layered Dinner Manifest")]
    private static void CreateOrUpdateLayeredDinnerManifestMenuItem()
    {
        CreateOrUpdateLayeredDinnerManifest();
    }

    public static Chapter3LayeredDinnerAssetManifest CreateOrUpdateLayeredDinnerManifest()
    {
        EnsureManifestFolder();

        Chapter3LayeredDinnerAssetManifest manifest =
            AssetDatabase.LoadAssetAtPath<Chapter3LayeredDinnerAssetManifest>(ManifestPath);

        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<Chapter3LayeredDinnerAssetManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
        }

        manifest.canvasSize = new Vector2Int(1448, 1086);
        manifest.tableBack = LoadSprite("table_back.png");
        manifest.tableFrontOverlay = LoadSprite("table_front_overlay.png");
        manifest.tableTopProps = LoadSprite("table_top_props.png");
        manifest.coveredDish = LoadSprite("covered_dish.png");
        manifest.foodFull = LoadSprite("food_full.png");
        manifest.foodHalf = LoadSprite("food_half.png");
        manifest.foodEmpty = LoadSprite("food_empty.png");
        manifest.seats = new Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet[8];

        for (int i = 0; i < 8; i++)
        {
            string seatFolder = $"{LayeredFolder}/Seat{i + 1:00}";
            manifest.seats[i] = new Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet
            {
                seatId = $"Seat{i + 1:00}",
                idleFrames = LoadFrameSet(seatFolder, "idle_"),
                eatFrames = LoadFrameSet(seatFolder, "eat_"),
                talkFrames = LoadFrameSet(seatFolder, "talk_"),
                headFrames = LoadFrameSet(seatFolder, "head_"),
                utensilFrames = LoadFrameSet(seatFolder, "utensil_"),
                handOverlayFrames = LoadFrameSet(seatFolder, "hand_overlay_")
            };
        }

        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool valid = manifest.Validate(out string validationMessage);
        string report = BuildReport(manifest);

        if (valid)
        {
            Debug.Log($"[Ch3DiningValidator] Created/updated layered dinner manifest.\n{report}\n{validationMessage}");
        }
        else
        {
            Debug.LogError($"[Ch3DiningValidator] Created/updated manifest, but required layered art is missing or invalid.\n{report}\n{validationMessage}");
        }

        return manifest;
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = $"{LayeredFolder}/{fileName}";

        if (!File.Exists(path))
        {
            return null;
        }

        ConfigureTextureImporter(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] LoadFrameSet(string folderPath, string prefix)
    {
        if (!Directory.Exists(folderPath))
        {
            return System.Array.Empty<Sprite>();
        }

        string[] paths = Directory.GetFiles(folderPath, $"{prefix}*.png", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => Path.GetFileName(path), System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < paths.Length; i++)
        {
            ConfigureTextureImporter(paths[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);

            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static void ConfigureTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        bool changed = false;
        changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
        changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
        changed |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);
        changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
        changed |= SetIfDifferent(importer.spritePixelsPerUnit, 100f, value => importer.spritePixelsPerUnit = value);
        changed |= SetIfDifferent(importer.maxTextureSize, 4096, value => importer.maxTextureSize = value);

        if (importer.spritePivot != new Vector2(0.5f, 0.5f))
        {
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static bool SetIfDifferent<T>(T current, T desired, System.Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired))
        {
            return false;
        }

        setter(desired);
        return true;
    }

    private static string BuildReport(Chapter3LayeredDinnerAssetManifest manifest)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Layer folder: {LayeredFolder}");
        builder.AppendLine($"table_back: {Found(manifest.tableBack)}");
        builder.AppendLine($"table_front_overlay: {Found(manifest.tableFrontOverlay)}");
        builder.AppendLine($"table_top_props: {Found(manifest.tableTopProps)}");
        builder.AppendLine($"covered_dish: {Found(manifest.coveredDish)}");
        builder.AppendLine($"food_full: {Found(manifest.foodFull)}");
        builder.AppendLine($"food_half: {Found(manifest.foodHalf)}");
        builder.AppendLine($"food_empty: {Found(manifest.foodEmpty)}");

        for (int i = 0; i < 8; i++)
        {
            Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet seat = manifest.GetSeat(i);
            builder.AppendLine(
                $"Seat{i + 1:00}: idle={Count(seat?.idleFrames)} eat={Count(seat?.eatFrames)} " +
                $"talk={Count(seat?.talkFrames)} head={Count(seat?.headFrames)} " +
                $"utensil={Count(seat?.utensilFrames)} hand={Count(seat?.handOverlayFrames)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string Found(Object asset)
    {
        return asset != null ? "found" : "missing";
    }

    private static int Count(Sprite[] frames)
    {
        return frames != null ? frames.Count(frame => frame != null) : 0;
    }

    private static void EnsureManifestFolder()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Chapter3");
        EnsureFolder("Assets/Resources/Chapter3/Dining");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        string parent = slash > 0 ? path.Substring(0, slash) : "Assets";
        string folderName = slash > 0 ? path.Substring(slash + 1) : path;
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
