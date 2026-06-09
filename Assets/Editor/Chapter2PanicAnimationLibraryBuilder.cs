using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Chapter2PanicAnimationLibraryBuilder
{
    private const string AnimationLibraryRoot = "Assets/AnimationLibrary";
    private const string OutputFolder = "Assets/Resources/Chapter2";
    private const string OutputPath = OutputFolder + "/PanicAnimationLibrary.asset";
    private const string Guest7CharacterId = "LordAmbroseVeil";
    private const string Guest7RunLeftFolder = "Assets/Art/Characters/guest7/guest7left";
    private const string Guest7RunRightFolder = "Assets/Art/Characters/guest7/guest7right";

    private static readonly ActionSpec[] RequiredActions =
    {
        new ActionSpec("panic_reaction_down", 6),
        new ActionSpec("panic_shriek_down", 8),
        new ActionSpec("panic_run_left", 8),
        new ActionSpec("panic_run_right", 8),
        new ActionSpec("panic_turnaround", 6),
        new ActionSpec("cover_face_cower", 6),
    };

    [MenuItem("Dreadforge/Chapter 2/Rebuild Panic Animation Library")]
    public static void RebuildPanicAnimationLibrary()
    {
        EnsureFolder(OutputFolder);
        AssetDatabase.Refresh();

        List<Chapter2PanicCharacterAnimation> characterAnimations = new List<Chapter2PanicCharacterAnimation>();
        List<string> errors = new List<string>();

        for (int i = 0; i < Chapter2PanicRoster.CharacterIds.Length; i++)
        {
            string characterId = Chapter2PanicRoster.CharacterIds[i];
            string displayName = i < Chapter2PanicRoster.DisplayNames.Length
                ? Chapter2PanicRoster.DisplayNames[i]
                : characterId;

            Sprite[][] actionSprites = new Sprite[RequiredActions.Length][];

            for (int actionIndex = 0; actionIndex < RequiredActions.Length; actionIndex++)
            {
                ActionSpec action = RequiredActions[actionIndex];
                string framesFolder = $"{AnimationLibraryRoot}/{characterId}/approved/full_body/{action.id}/frames";
                Sprite[] sprites = TryLoadCharacterActionOverride(characterId, action.id, action.expectedFrameCount, out Sprite[] overrideSprites)
                    ? overrideSprites
                    : LoadSprites(framesFolder);
                actionSprites[actionIndex] = sprites;

                if (sprites.Length != action.expectedFrameCount)
                {
                    errors.Add($"{characterId}/{action.id}: expected {action.expectedFrameCount} frame(s), found {sprites.Length} in {framesFolder}");
                }
            }

            Chapter2PanicCharacterAnimation animation = new Chapter2PanicCharacterAnimation();
            animation.Configure(
                characterId,
                displayName,
                actionSprites[0],
                actionSprites[1],
                actionSprites[2],
                actionSprites[3],
                actionSprites[4],
                actionSprites[5]);
            characterAnimations.Add(animation);
        }

        Chapter2PanicAnimationLibrary library = AssetDatabase.LoadAssetAtPath<Chapter2PanicAnimationLibrary>(OutputPath);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<Chapter2PanicAnimationLibrary>();
            AssetDatabase.CreateAsset(library, OutputPath);
        }

        library.Configure(characterAnimations.ToArray());
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (errors.Count > 0)
        {
            string message = "Built Chapter 2 panic animation library with missing frames:\n" + string.Join("\n", errors);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        Debug.Log($"Built Chapter 2 panic animation library at {OutputPath}.");
    }

    private static bool TryLoadCharacterActionOverride(string characterId, string actionId, int expectedFrameCount, out Sprite[] sprites)
    {
        sprites = Array.Empty<Sprite>();

        if (!string.Equals(characterId, Guest7CharacterId, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(actionId, "panic_run_left", StringComparison.Ordinal))
        {
            sprites = LoadSpritesCycled(Guest7RunLeftFolder, expectedFrameCount);
            return true;
        }

        if (string.Equals(actionId, "panic_run_right", StringComparison.Ordinal))
        {
            sprites = LoadSpritesCycled(Guest7RunRightFolder, expectedFrameCount);
            return true;
        }

        return false;
    }

    private static Sprite[] LoadSpritesCycled(string framesFolder, int expectedFrameCount)
    {
        Sprite[] sourceSprites = LoadSprites(framesFolder);

        if (sourceSprites.Length == 0 || expectedFrameCount <= 0)
        {
            return sourceSprites;
        }

        Sprite[] cycledSprites = new Sprite[expectedFrameCount];

        for (int i = 0; i < cycledSprites.Length; i++)
        {
            cycledSprites[i] = sourceSprites[i % sourceSprites.Length];
        }

        return cycledSprites;
    }

    private static Sprite[] LoadSprites(string framesFolder)
    {
        if (!Directory.Exists(framesFolder))
        {
            return Array.Empty<Sprite>();
        }

        string[] paths = Directory.GetFiles(framesFolder, "*.png")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => path.Replace('\\', '/'))
            .ToArray();

        List<Sprite> sprites = new List<Sprite>(paths.Length);

        for (int i = 0; i < paths.Length; i++)
        {
            AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceSynchronousImport);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);

            if (sprite != null)
            {
                sprites.Add(sprite);
                continue;
            }

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(paths[i]);

            for (int subAssetIndex = 0; subAssetIndex < subAssets.Length; subAssetIndex++)
            {
                if (subAssets[subAssetIndex] is Sprite subSprite)
                {
                    sprites.Add(subSprite);
                }
            }
        }

        return sprites.ToArray();
    }

    private static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace('\\', '/');

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrWhiteSpace(parent) ? "Assets" : parent, folderName);
    }

    private readonly struct ActionSpec
    {
        public readonly string id;
        public readonly int expectedFrameCount;

        public ActionSpec(string id, int expectedFrameCount)
        {
            this.id = id;
            this.expectedFrameCount = expectedFrameCount;
        }
    }
}
