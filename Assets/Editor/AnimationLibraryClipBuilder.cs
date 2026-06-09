using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AnimationLibraryClipBuilder
{
    private const string LibraryRoot = "Assets/AnimationLibrary";
    private const float DefaultFrameRate = 12f;

    [MenuItem("Dreadforge/Animation Library/Rebuild Approved Full-Body Clips")]
    public static void RebuildApprovedFullBodyClips()
    {
        if (!AssetDatabase.IsValidFolder(LibraryRoot))
        {
            Debug.LogWarning($"Animation library folder does not exist: {LibraryRoot}");
            return;
        }

        int clipCount = 0;
        foreach (string characterFolder in Directory.GetDirectories(LibraryRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string approvedRoot = Path.Combine(characterFolder, "approved/full_body").Replace('\\', '/');
            if (!Directory.Exists(approvedRoot))
            {
                continue;
            }

            foreach (string actionFolder in Directory.GetDirectories(approvedRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string framesFolder = Path.Combine(actionFolder, "frames").Replace('\\', '/');
                if (!Directory.Exists(framesFolder))
                {
                    continue;
                }

                Sprite[] sprites = LoadSprites(framesFolder);
                if (sprites.Length == 0)
                {
                    continue;
                }

                string characterName = Path.GetFileName(characterFolder);
                string actionName = Path.GetFileName(actionFolder);
                string clipsFolder = Path.Combine(characterFolder, "clips").Replace('\\', '/');
                EnsureFolder(clipsFolder);

                bool loop = actionName.IndexOf("reaction", StringComparison.OrdinalIgnoreCase) < 0;
                CreateSpriteClip($"{characterName}_{ToPascalCase(actionName)}", clipsFolder, sprites, loop, DefaultFrameRate);
                clipCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt {clipCount} approved full-body animation clips from {LibraryRoot}.");
    }

    private static Sprite[] LoadSprites(string framesFolder)
    {
        return Directory.GetFiles(framesFolder, "*.png")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/')))
            .Where(sprite => sprite != null)
            .ToArray();
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

        AnimationUtility.SetObjectReferenceCurve(clip, CreateSpriteBinding(typeof(Image)), keyframes);
        AnimationUtility.SetObjectReferenceCurve(clip, CreateSpriteBinding(typeof(SpriteRenderer)), keyframes);

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

    private static EditorCurveBinding CreateSpriteBinding(Type componentType)
    {
        return new EditorCurveBinding
        {
            path = string.Empty,
            type = componentType,
            propertyName = "m_Sprite"
        };
    }

    private static string ToPascalCase(string value)
    {
        string[] parts = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
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
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
    }
}
