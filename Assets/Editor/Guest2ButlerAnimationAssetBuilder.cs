using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class Guest2ButlerAnimationAssetBuilder
{
	private const string SpriteSheetPath = "Assets/Art/Characters/guest2/butlersprite.png";
	private const string SittingSpriteSheetPath = "Assets/Art/Characters/guest2/butlerspritesit.png";
	private const string OutputFolder = "Assets/Animation/ButlerGuest";
	private const string ControllerPath = OutputFolder + "/ButlerGuest.overrideController";
	private const string BaseControllerPath = "Assets/Animation/Player/Player.controller";
	private const float WalkFrameRate = 12f;
	private const float SittingFrameRate = 4f;

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

	[MenuItem("Dreadforge/Characters/Rebuild Guest 2 Butler Animation")]
	public static void RebuildGuest2ButlerAnimation()
	{
		if (!EditorUtility.DisplayDialog(
			"Rebuild Guest 2 Animation Assets?",
			"This rebuild clears and rewrites the generated Guest 2 animation clips and override controller.",
			"Rebuild",
			"Cancel"))
		{
			return;
		}

		Sprite[] frames = LoadButlerSprites();
		if (frames.Length < 32)
		{
			throw new InvalidOperationException($"Expected at least 32 sliced sprites in {SpriteSheetPath}, but found {frames.Length}.");
		}

		Sprite[] sittingFrames = LoadSprites(SittingSpriteSheetPath);
		if (sittingFrames.Length < 4)
		{
			throw new InvalidOperationException($"Expected at least 4 sliced sprites in {SittingSpriteSheetPath}, but found {sittingFrames.Length}.");
		}

		EnsureFolder(OutputFolder);

		AnimationClip idleClip = CreateSpriteClip("ButlerGuest_Idle", new[] { frames[0] }, false);
		AnimationClip walkDownClip = CreateSpriteClip("ButlerGuest_Walk_Down", frames.Skip(0).Take(8).ToArray(), true);
		AnimationClip walkLeftClip = CreateSpriteClip("ButlerGuest_Walk_Left", frames.Skip(8).Take(8).ToArray(), true);
		AnimationClip walkRightClip = CreateSpriteClip("ButlerGuest_Walk_Right", frames.Skip(16).Take(8).ToArray(), true);
		AnimationClip walkUpClip = CreateSpriteClip("ButlerGuest_Walk_Up", frames.Skip(24).Take(8).ToArray(), true);
		AnimationClip sittingClip = CreateSpriteClip(
			"ButlerGuest_Sitting",
			sittingFrames.Take(4).ToArray(),
			true,
			SittingFrameRate);

		CreateOverrideController(
			idleClip,
			walkDownClip,
			walkUpClip,
			walkLeftClip,
			walkRightClip,
			sittingClip);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Rebuilt Guest 2 butler directional animations.");
	}

	private static Sprite[] LoadButlerSprites()
	{
		return LoadSprites(SpriteSheetPath);
	}

	private static Sprite[] LoadSprites(string assetPath)
	{
		return AssetDatabase.LoadAllAssetsAtPath(assetPath)
			.OfType<Sprite>()
			.OrderBy(sprite => ParseTrailingIndex(sprite.name))
			.ToArray();
	}

	private static int ParseTrailingIndex(string spriteName)
	{
		int marker = spriteName.LastIndexOf('_');
		if (marker < 0 || marker == spriteName.Length - 1)
			return int.MaxValue;

		return int.TryParse(spriteName.Substring(marker + 1), out int index) ? index : int.MaxValue;
	}

	private static AnimationClip CreateSpriteClip(string clipName, IReadOnlyList<Sprite> sprites, bool loop, float frameRate = WalkFrameRate)
	{
		string assetPath = $"{OutputFolder}/{clipName}.anim";
		AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
		bool isNewAsset = clip == null;
		if (isNewAsset)
			clip = new AnimationClip();

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
			AssetDatabase.CreateAsset(clip, assetPath);
		else
			EditorUtility.SetDirty(clip);

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

	private static AnimatorOverrideController CreateOverrideController(
		AnimationClip idleClip,
		AnimationClip walkDownClip,
		AnimationClip walkUpClip,
		AnimationClip walkLeftClip,
		AnimationClip walkRightClip,
		AnimationClip sittingClip)
	{
		AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
		if (baseController == null)
			throw new FileNotFoundException($"Could not load base Animator controller at {BaseControllerPath}.");

		AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
		bool isNewAsset = controller == null;
		if (isNewAsset)
			controller = new AnimatorOverrideController(baseController);

		controller.name = "ButlerGuest";
		controller.runtimeAnimatorController = baseController;

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
				"Player_Croutch" => sittingClip,
				_ => idleClip
			};

			overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseClip, replacement));
		}

		controller.ApplyOverrides(overrides);
		if (isNewAsset)
			AssetDatabase.CreateAsset(controller, ControllerPath);
		else
			EditorUtility.SetDirty(controller);

		return controller;
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
