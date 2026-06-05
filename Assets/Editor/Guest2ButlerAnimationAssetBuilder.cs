using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Guest2ButlerAnimationAssetBuilder
{
	private const string SpriteSheetPath = "Assets/Art/Characters/guest2/butlersprite.png";
	private const string OutputFolder = "Assets/Animation/ButlerGuest";
	private const string ControllerPath = OutputFolder + "/ButlerGuest.overrideController";
	private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
	private const string BaseControllerPath = "Assets/Animation/Player/Player.controller";
	private const float WalkFrameRate = 12f;

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
		EnsureFolder(OutputFolder);

		Sprite[] frames = LoadButlerSprites();
		if (frames.Length < 32)
		{
			throw new InvalidOperationException($"Expected at least 32 sliced sprites in {SpriteSheetPath}, but found {frames.Length}.");
		}

		AnimationClip idleClip = CreateSpriteClip("ButlerGuest_Idle", new[] { frames[0] }, false);
		AnimationClip walkDownClip = CreateSpriteClip("ButlerGuest_Walk_Down", frames.Skip(0).Take(8).ToArray(), true);
		AnimationClip walkLeftClip = CreateSpriteClip("ButlerGuest_Walk_Left", frames.Skip(8).Take(8).ToArray(), true);
		AnimationClip walkRightClip = CreateSpriteClip("ButlerGuest_Walk_Right", frames.Skip(16).Take(8).ToArray(), true);
		AnimationClip walkUpClip = CreateSpriteClip("ButlerGuest_Walk_Up", frames.Skip(24).Take(8).ToArray(), true);

		AnimatorOverrideController controller = CreateOverrideController(
			idleClip,
			walkDownClip,
			walkUpClip,
			walkLeftClip,
			walkRightClip);

		ApplyToGuest2(controller, frames[0]);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Rebuilt Guest 2 butler directional animations.");
	}

	private static Sprite[] LoadButlerSprites()
	{
		return AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath)
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

	private static AnimationClip CreateSpriteClip(string clipName, IReadOnlyList<Sprite> sprites, bool loop)
	{
		string assetPath = $"{OutputFolder}/{clipName}.anim";
		AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
		bool isNewAsset = clip == null;
		if (isNewAsset)
			clip = new AnimationClip();

		clip.name = clipName;
		clip.frameRate = WalkFrameRate;
		clip.ClearCurves();

		ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
		for (int i = 0; i < sprites.Count; i++)
		{
			keyframes[i] = new ObjectReferenceKeyframe
			{
				time = i / WalkFrameRate,
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
		AnimationClip walkRightClip)
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

	private static void ApplyToGuest2(RuntimeAnimatorController controller, Sprite previewSprite)
	{
		EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
		GameObject guest2 = FindSceneObjectByName("Guest 2");
		if (guest2 == null)
			throw new InvalidOperationException("Could not find Guest 2 in Gameplay.unity.");

		Animator animator = guest2.GetComponentInChildren<Animator>(true);
		if (animator == null)
			animator = guest2.AddComponent<Animator>();

		animator.runtimeAnimatorController = controller;
		EditorUtility.SetDirty(animator);
		PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

		SpriteRenderer renderer = guest2.GetComponentsInChildren<SpriteRenderer>(true)
			.FirstOrDefault(candidate => !IsCoatVisualTransform(candidate.transform));
		if (renderer != null)
		{
			renderer.sprite = previewSprite;
			EditorUtility.SetDirty(renderer);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
		}

		EditorSceneManager.MarkSceneDirty(guest2.scene);
		EditorSceneManager.SaveScene(guest2.scene);
	}

	private static bool IsCoatVisualTransform(Transform transform)
	{
		while (transform != null)
		{
			if (transform.name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0)
				return true;

			transform = transform.parent;
		}

		return false;
	}

	private static GameObject FindSceneObjectByName(string objectName)
	{
		foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
		{
			GameObject match = FindInChildren(root.transform, objectName);
			if (match != null)
				return match;
		}

		return null;
	}

	private static GameObject FindInChildren(Transform transform, string objectName)
	{
		if (transform.name == objectName)
			return transform.gameObject;

		for (int i = 0; i < transform.childCount; i++)
		{
			GameObject match = FindInChildren(transform.GetChild(i), objectName);
			if (match != null)
				return match;
		}

		return null;
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
