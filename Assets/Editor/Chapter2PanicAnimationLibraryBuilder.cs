using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Chapter2PanicAnimationLibraryBuilder
{
    private const string AnimationLibraryRoot = "Assets/Art/Library/AnimationLibrary";
    private const string GuestArtRoot = "Assets/Art/Characters";
    private const string PanicClipRoot = "Assets/Animation/Chapter2Panic";
    private const string OutputFolder = "Assets/Resources/Chapter2";
    private const string OutputPath = OutputFolder + "/PanicAnimationLibrary.asset";
    private const float ClipFrameRate = 12f;
    private const string PanicHandsUpActionId = "panic_hands_up";
    private const string PanicPopActionId = "panic_pop";
    private const string Guest7CharacterId = "LordAmbroseVeil";
    private const string Guest7RunDownFolder = "Assets/Art/Characters/guest7/guest7down";
    private const string Guest7RunLeftFolder = "Assets/Art/Characters/guest7/guest7left";
    private const string Guest7RunRightFolder = "Assets/Art/Characters/guest7/guest7right";
    private const string Guest7RunUpFolder = "Assets/Art/Characters/guest7/guest07up";

    private static readonly GuestFolderSpec[] GuestFolders =
    {
        new GuestFolderSpec("Lady", "guest1", "Guest01_Lady"),
        new GuestFolderSpec("ButlerGuest", "guest2", "Guest02_ButlerGuest"),
        new GuestFolderSpec("MisterFlorianKnell", "guest3", "Guest03_MisterFlorianKnell"),
        new GuestFolderSpec("CountessElowenDusk", "guest4", "Guest04_CountessElowenDusk"),
        new GuestFolderSpec("BaronHectorGlass", "guest5", "Guest05_BaronHectorGlass"),
        new GuestFolderSpec("LadySabineMarrow", "guest6", "Guest06_LadySabineMarrow"),
        new GuestFolderSpec("LordAmbroseVeil", "guest7", "Guest07_LordAmbroseVeil"),
        new GuestFolderSpec("MadameCoralieThread", "guest8", "Guest08_MadameCoralieThread")
    };

    [MenuItem("Dreadforge/Chapter 2/Rebuild Panic Animation Library")]
    public static void RebuildPanicAnimationLibrary()
    {
        if (!EditorUtility.DisplayDialog(
            "Rebuild Chapter 2 Panic Animation Library?",
            "This rebuild clears and rewrites generated panic clips and the runtime panic animation library.",
            "Rebuild",
            "Cancel"))
        {
            return;
        }

        List<PanicBuildInput> buildInputs = CollectValidatedBuildInputs(out List<string> errors);

        if (errors.Count > 0)
        {
            string message = "Chapter 2 panic animation rebuild was canceled before writing assets because inputs are incomplete:\n" +
                string.Join("\n", errors);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        EnsureFolder(OutputFolder);
        EnsureFolder(PanicClipRoot);
        AssetDatabase.Refresh();

        List<Chapter2PanicCharacterAnimation> characterAnimations =
            new List<Chapter2PanicCharacterAnimation>(buildInputs.Count);

        for (int i = 0; i < buildInputs.Count; i++)
        {
            PanicBuildInput input = buildInputs[i];
            string clipFolder = $"{PanicClipRoot}/{input.GuestFolder.clipFolderName}";
            EnsureFolder(clipFolder);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicHandsUp",
                clipFolder,
                input.HandsUpSprites,
                false,
                ClipFrameRate);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicPop",
                clipFolder,
                input.PanicPopSprites,
                false,
                ClipFrameRate);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicRunDown",
                clipFolder,
                input.RunDownSprites,
                true,
                ClipFrameRate);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicRunLeft",
                clipFolder,
                input.RunLeftSprites,
                true,
                ClipFrameRate);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicRunRight",
                clipFolder,
                input.RunRightSprites,
                true,
                ClipFrameRate);
            CreateSpriteClip(
                $"{input.GuestFolder.clipFolderName}_PanicRunUp",
                clipFolder,
                input.RunUpSprites,
                true,
                ClipFrameRate);

            Chapter2PanicCharacterAnimation animation = new Chapter2PanicCharacterAnimation();
            animation.Configure(
                input.CharacterId,
                input.DisplayName,
                input.HandsUpSprites,
                input.PanicPopSprites,
                input.RunDownSprites,
                input.RunLeftSprites,
                input.RunRightSprites,
                input.RunUpSprites);
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

        Debug.Log($"Built Chapter 2 panic animation library at {OutputPath}.");
    }

    private static List<PanicBuildInput> CollectValidatedBuildInputs(out List<string> errors)
    {
        errors = new List<string>();
        List<PanicBuildInput> inputs = new List<PanicBuildInput>(Chapter2PanicRoster.CharacterIds.Length);

        for (int i = 0; i < Chapter2PanicRoster.CharacterIds.Length; i++)
        {
            string characterId = Chapter2PanicRoster.CharacterIds[i];
            string displayName = i < Chapter2PanicRoster.DisplayNames.Length
                ? Chapter2PanicRoster.DisplayNames[i]
                : characterId;

            if (!TryGetGuestFolder(characterId, out GuestFolderSpec guestFolder))
            {
                errors.Add($"{characterId}: missing guest folder mapping");
                continue;
            }

            string handsUpFramesFolder = GetGuestActionFramesFolder(guestFolder, PanicHandsUpActionId);
            string panicPopFramesFolder = GetGuestActionFramesFolder(guestFolder, PanicPopActionId);
            Sprite[] handsUpSprites = LoadSprites(handsUpFramesFolder);
            Sprite[] panicPopSprites = LoadSprites(panicPopFramesFolder);

            if (panicPopSprites.Length == 0)
            {
                panicPopFramesFolder = GetLegacyGuestPanicFramesFolder(guestFolder);
                panicPopSprites = LoadSprites(panicPopFramesFolder);
            }

            Sprite[] runDownSprites = LoadRunSprites(characterId, "walk_down", 4);
            Sprite[] runLeftSprites = LoadRunSprites(characterId, "walk_left", 4);
            Sprite[] runRightSprites = LoadRunSprites(characterId, "walk_right", 4);
            Sprite[] runUpSprites = LoadRunSprites(characterId, "walk_up", 4);

            AppendCountError(errors, characterId, PanicHandsUpActionId, handsUpSprites, 4, handsUpFramesFolder);
            AppendCountError(errors, characterId, PanicPopActionId, panicPopSprites, 8, panicPopFramesFolder);
            AppendCountError(errors, characterId, "panic_run_down", runDownSprites, 4, "normal walk_down references");
            AppendCountError(errors, characterId, "panic_run_left", runLeftSprites, 4, "normal walk_left references");
            AppendCountError(errors, characterId, "panic_run_right", runRightSprites, 4, "normal walk_right references");
            AppendCountError(errors, characterId, "panic_run_up", runUpSprites, 4, "normal walk_up references");

            inputs.Add(new PanicBuildInput(
                characterId,
                displayName,
                guestFolder,
                handsUpSprites,
                panicPopSprites,
                runDownSprites,
                runLeftSprites,
                runRightSprites,
                runUpSprites));
        }

        return inputs;
    }

    private static bool TryGetGuestFolder(string characterId, out GuestFolderSpec guestFolder)
    {
        for (int i = 0; i < GuestFolders.Length; i++)
        {
            GuestFolderSpec candidate = GuestFolders[i];

            if (string.Equals(candidate.characterId, characterId, StringComparison.Ordinal))
            {
                guestFolder = candidate;
                return true;
            }
        }

        guestFolder = default;
        return false;
    }

    private static string GetGuestActionFramesFolder(GuestFolderSpec guestFolder, string actionId)
    {
        return $"{GuestArtRoot}/{guestFolder.guestFolder}/panic/{actionId}/frames";
    }

    private static string GetLegacyGuestPanicFramesFolder(GuestFolderSpec guestFolder)
    {
        return $"{GuestArtRoot}/{guestFolder.guestFolder}/{guestFolder.guestFolder}panic";
    }

    private static Sprite[] LoadRunSprites(string characterId, string direction, int expectedFrameCount)
    {
        string framesFolder = $"{AnimationLibraryRoot}/{characterId}/reference/full_body/{direction}";

        if (string.Equals(characterId, Guest7CharacterId, StringComparison.Ordinal))
        {
            switch (direction)
            {
                case "walk_down":
                    framesFolder = Guest7RunDownFolder;
                    break;
                case "walk_left":
                    framesFolder = Guest7RunLeftFolder;
                    break;
                case "walk_right":
                    framesFolder = Guest7RunRightFolder;
                    break;
                case "walk_up":
                    framesFolder = Guest7RunUpFolder;
                    break;
            }
        }

        return LoadSpritesCycled(framesFolder, expectedFrameCount);
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

    private static AnimationClip CreateSpriteClip(
        string clipName,
        string outputFolder,
        IReadOnlyList<Sprite> sprites,
        bool loop,
        float frameRate)
    {
        string assetPath = $"{outputFolder}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        bool isNewAsset = clip == null;

        if (isNewAsset)
        {
            clip = new AnimationClip();
        }

        clip.name = clipName;
        clip.frameRate = frameRate;
        clip.ClearCurves();

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];

        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, CreateSpriteBinding(), keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNewAsset)
        {
            AssetDatabase.CreateAsset(clip, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(clip);
        }

        return clip;
    }

    private static EditorCurveBinding CreateSpriteBinding()
    {
        return new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
    }

    private static void AppendCountError(
        List<string> errors,
        string characterId,
        string actionId,
        Sprite[] sprites,
        int expectedFrameCount,
        string sourceDescription)
    {
        if (sprites.Length == expectedFrameCount)
        {
            return;
        }

        errors.Add($"{characterId}/{actionId}: expected {expectedFrameCount} frame(s), found {sprites.Length} in {sourceDescription}");
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

    private sealed class PanicBuildInput
    {
        public readonly string CharacterId;
        public readonly string DisplayName;
        public readonly GuestFolderSpec GuestFolder;
        public readonly Sprite[] HandsUpSprites;
        public readonly Sprite[] PanicPopSprites;
        public readonly Sprite[] RunDownSprites;
        public readonly Sprite[] RunLeftSprites;
        public readonly Sprite[] RunRightSprites;
        public readonly Sprite[] RunUpSprites;

        public PanicBuildInput(
            string characterId,
            string displayName,
            GuestFolderSpec guestFolder,
            Sprite[] handsUpSprites,
            Sprite[] panicPopSprites,
            Sprite[] runDownSprites,
            Sprite[] runLeftSprites,
            Sprite[] runRightSprites,
            Sprite[] runUpSprites)
        {
            CharacterId = characterId;
            DisplayName = displayName;
            GuestFolder = guestFolder;
            HandsUpSprites = handsUpSprites;
            PanicPopSprites = panicPopSprites;
            RunDownSprites = runDownSprites;
            RunLeftSprites = runLeftSprites;
            RunRightSprites = runRightSprites;
            RunUpSprites = runUpSprites;
        }
    }

    private readonly struct GuestFolderSpec
    {
        public readonly string characterId;
        public readonly string guestFolder;
        public readonly string clipFolderName;

        public GuestFolderSpec(string characterId, string guestFolder, string clipFolderName)
        {
            this.characterId = characterId;
            this.guestFolder = guestFolder;
            this.clipFolderName = clipFolderName;
        }
    }
}
