using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterAnimationAssetBuilder
{
	private const string BaseControllerPath = "Assets/Animation/Player/Player.controller";
	private const string CharacterRoot = "Assets/Characters";
	private const string AnimationRoot = "Assets/Animation";
	private const float FrameRate = 12f;

	private static readonly string[] BaseClipNames =
	{
		"Player_Idle",
		"Player_Walk_Down",
		"Player_Walk_Up",
		"Player_Walk_Left",
		"Player_Walk_Right",
		"Player_Climb",
		"Player_Croutch",
		"Player_Jump"
	};

	[MenuItem("Dreadforge/Characters/Rebuild Character Animation Assets")]
	public static void RebuildAllCharacterAnimationAssets()
	{
		AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
		if (baseController == null)
		{
			Debug.LogError($"Could not load base character Animator controller at {BaseControllerPath}.");
			return;
		}

		foreach (string characterFolder in Directory.GetDirectories(CharacterRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
		{
			BuildCharacter(characterFolder, baseController);
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Rebuilt character Animation clips and Animator override controllers.");
	}

	private static void BuildCharacter(string characterFolder, AnimatorController baseController)
	{
		string characterName = Path.GetFileName(characterFolder);
		string alignedFolder = GetBestAlignedFrameFolder(characterFolder);
		if (string.IsNullOrEmpty(alignedFolder))
		{
			Debug.LogWarning($"Skipped {characterName}; no aligned frame folder was found.");
			return;
		}

		string outputFolder = $"{AnimationRoot}/{characterName}";
		EnsureFolder(outputFolder);

		Dictionary<int, List<Sprite>> rows = LoadSpritesByRow(alignedFolder);
		if (rows.Count == 0)
		{
			Debug.LogWarning($"Skipped {characterName}; aligned frames did not import as Sprites.");
			return;
		}

		List<Sprite> downFrames = GetRowOrFallback(rows, 1);
		List<Sprite> leftFrames = rows.Count >= 4 ? GetRowOrFallback(rows, 2) : GetRowOrFallback(rows, 1);
		List<Sprite> rightFrames = rows.Count >= 4 ? GetRowOrFallback(rows, 3) : GetRowOrFallback(rows, 1);
		List<Sprite> upFrames = rows.Count >= 4 ? GetRowOrFallback(rows, 4) : GetRowOrFallback(rows, 1);

		AnimationClip idleClip = CreateSpriteClip($"{characterName}_Idle", outputFolder, new[] { downFrames[0] }, false);
		AnimationClip walkDownClip = CreateSpriteClip($"{characterName}_Walk_Down", outputFolder, downFrames, true);
		AnimationClip walkUpClip = CreateSpriteClip($"{characterName}_Walk_Up", outputFolder, upFrames, true);
		AnimationClip walkLeftClip = CreateSpriteClip($"{characterName}_Walk_Left", outputFolder, leftFrames, true);
		AnimationClip walkRightClip = CreateSpriteClip($"{characterName}_Walk_Right", outputFolder, rightFrames, true);

		CreateOverrideController(
			characterName,
			outputFolder,
			baseController,
			idleClip,
			walkDownClip,
			walkUpClip,
			walkLeftClip,
			walkRightClip);
	}

	private static string GetBestAlignedFrameFolder(string characterFolder)
	{
		string explicitDirectional = Path.Combine(characterFolder, "directional/aligned").Replace('\\', '/');
		if (Directory.Exists(explicitDirectional) && Directory.GetFiles(explicitDirectional, "*.png").Length >= 16)
			return explicitDirectional;

		string directionalWalk = Path.Combine(characterFolder, "walk/aligned").Replace('\\', '/');
		if (Directory.Exists(directionalWalk) && Directory.GetFiles(directionalWalk, "*.png").Length >= 16)
			return directionalWalk;

		string sideWalk = Path.Combine(characterFolder, "walk_cycle/aligned").Replace('\\', '/');
		if (Directory.Exists(sideWalk) && Directory.GetFiles(sideWalk, "*.png").Length >= 4)
			return sideWalk;

		return string.Empty;
	}

	private static Dictionary<int, List<Sprite>> LoadSpritesByRow(string alignedFolder)
	{
		Dictionary<int, List<Sprite>> rows = new Dictionary<int, List<Sprite>>();
		foreach (string filePath in Directory.GetFiles(alignedFolder, "*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
		{
			string assetPath = filePath.Replace('\\', '/');
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
			if (sprite == null)
				continue;

			int row = ParseRowIndex(Path.GetFileNameWithoutExtension(assetPath));
			if (!rows.TryGetValue(row, out List<Sprite> rowSprites))
			{
				rowSprites = new List<Sprite>();
				rows.Add(row, rowSprites);
			}

			rowSprites.Add(sprite);
		}

		return rows;
	}

	private static int ParseRowIndex(string fileName)
	{
		int rowMarker = fileName.IndexOf("_r", StringComparison.OrdinalIgnoreCase);
		if (rowMarker < 0 || rowMarker + 4 > fileName.Length)
			return 1;

		string rowText = fileName.Substring(rowMarker + 2, 2);
		return int.TryParse(rowText, out int row) ? Mathf.Max(1, row) : 1;
	}

	private static List<Sprite> GetRowOrFallback(Dictionary<int, List<Sprite>> rows, int row)
	{
		if (rows.TryGetValue(row, out List<Sprite> sprites) && sprites.Count > 0)
			return sprites;

		return rows.OrderBy(pair => pair.Key).First().Value;
	}

	private static AnimationClip CreateSpriteClip(
		string clipName,
		string outputFolder,
		IReadOnlyList<Sprite> sprites,
		bool loop)
	{
		string assetPath = $"{outputFolder}/{clipName}.anim";
		AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
		bool isNewAsset = clip == null;
		if (isNewAsset)
		{
			clip = new AnimationClip();
		}

		clip.name = clipName;
		clip.frameRate = FrameRate;
		clip.ClearCurves();

		ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[Mathf.Max(1, sprites.Count)];
		for (int i = 0; i < keyframes.Length; i++)
		{
			keyframes[i] = new ObjectReferenceKeyframe
			{
				time = i / FrameRate,
				value = sprites[Mathf.Min(i, sprites.Count - 1)]
			};
		}

		EditorCurveBinding spriteBinding = new EditorCurveBinding
		{
			path = string.Empty,
			type = typeof(Image),
			propertyName = "m_Sprite"
		};
		EditorCurveBinding spriteRendererBinding = new EditorCurveBinding
		{
			path = string.Empty,
			type = typeof(SpriteRenderer),
			propertyName = "m_Sprite"
		};

		AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
		AnimationUtility.SetObjectReferenceCurve(clip, spriteRendererBinding, keyframes);

		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = loop;
		AnimationUtility.SetAnimationClipSettings(clip, settings);

		if (isNewAsset)
			AssetDatabase.CreateAsset(clip, assetPath);
		else
			EditorUtility.SetDirty(clip);

		return clip;
	}

	private static void CreateOverrideController(
		string characterName,
		string outputFolder,
		AnimatorController baseController,
		AnimationClip idleClip,
		AnimationClip walkDownClip,
		AnimationClip walkUpClip,
		AnimationClip walkLeftClip,
		AnimationClip walkRightClip)
	{
		string assetPath = $"{outputFolder}/{characterName}.overrideController";
		AnimatorOverrideController overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
		bool isNewAsset = overrideController == null;
		if (isNewAsset)
		{
			overrideController = new AnimatorOverrideController(baseController);
		}

		overrideController.name = characterName;
		overrideController.runtimeAnimatorController = baseController;

		List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
		foreach (AnimationClip baseClip in baseController.animationClips)
		{
			if (baseClip == null || !BaseClipNames.Contains(baseClip.name))
				continue;

			AnimationClip replacement = baseClip.name switch
			{
				"Player_Walk_Down" => walkDownClip,
				"Player_Walk_Up" => walkUpClip,
				"Player_Walk_Left" => walkLeftClip,
				"Player_Walk_Right" => walkRightClip,
				"Player_Climb" => walkUpClip,
				_ => idleClip
			};

			overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseClip, replacement));
		}

		overrideController.ApplyOverrides(overrides);
		if (isNewAsset)
			AssetDatabase.CreateAsset(overrideController, assetPath);
		else
			EditorUtility.SetDirty(overrideController);
	}

	private static void EnsureFolder(string folderPath)
	{
		if (AssetDatabase.IsValidFolder(folderPath))
			return;

		string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
		string folderName = Path.GetFileName(folderPath);
		if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
			EnsureFolder(parent);

		AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
	}
}
