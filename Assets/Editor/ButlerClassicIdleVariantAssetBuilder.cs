using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class ButlerClassicIdleVariantAssetBuilder
{
	private const string FrameRoot = "Assets/Characters/ButlerClassic/idle_variants";
	private const string AnimationRoot = "Assets/Animation/ButlerClassic/IdleVariants";
	private const float IdleFrameRate = 8f;

	private static readonly string[] Directions = { "Down", "Left", "Right", "Up" };

	private static readonly VariantDefinition[] Variants =
	{
		new VariantDefinition("still_breathe", "StillBreathe"),
		new VariantDefinition("still_weight_shift", "StillWeightShift"),
		new VariantDefinition("action_pocket_watch", "PocketWatch"),
		new VariantDefinition("action_smoke", "Smoke"),
		new VariantDefinition("action_beard_scratch", "BeardScratch")
	};

	[MenuItem("Dreadforge/Characters/Rebuild ButlerClassic Idle Variant Assets")]
	public static void RebuildAssets()
	{
		EnsureFolder(AnimationRoot);

		AnimationClip walkDown = LoadRequired<AnimationClip>("Assets/Animation/ButlerClassic/ButlerClassic_Walk_Down.anim");
		AnimationClip walkLeft = LoadRequired<AnimationClip>("Assets/Animation/ButlerClassic/ButlerClassic_Walk_Left.anim");
		AnimationClip walkRight = LoadRequired<AnimationClip>("Assets/Animation/ButlerClassic/ButlerClassic_Walk_Right.anim");
		AnimationClip walkUp = LoadRequired<AnimationClip>("Assets/Animation/ButlerClassic/ButlerClassic_Walk_Up.anim");

		foreach (VariantDefinition variant in Variants)
		{
			Dictionary<string, AnimationClip> idleClips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
			foreach (string direction in Directions)
			{
				List<Sprite> sprites = LoadVariantSprites(variant.FolderName, direction.ToLowerInvariant());
				idleClips.Add(direction, CreateSpriteClip($"ButlerClassic_{variant.ClipName}_Idle_{direction}", sprites));
			}

			CreateController(
				variant,
				idleClips,
				walkDown,
				walkLeft,
				walkRight,
				walkUp);
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Rebuilt ButlerClassic idle variant clips and Animator controllers.");
	}

	private static AnimationClip CreateSpriteClip(string clipName, IReadOnlyList<Sprite> sprites)
	{
		if (sprites.Count == 0)
			throw new InvalidOperationException($"Cannot create {clipName}; no sprites were found.");

		string path = $"{AnimationRoot}/{clipName}.anim";
		AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		bool isNew = clip == null;
		if (isNew)
			clip = new AnimationClip();

		clip.name = clipName;
		clip.frameRate = IdleFrameRate;
		clip.ClearCurves();

		ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
		for (int i = 0; i < sprites.Count; i++)
		{
			keyframes[i] = new ObjectReferenceKeyframe
			{
				time = i / IdleFrameRate,
				value = sprites[i]
			};
		}

		AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(typeof(Image)), keyframes);
		AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(typeof(SpriteRenderer)), keyframes);

		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = true;
		settings.stopTime = sprites.Count / IdleFrameRate;
		AnimationUtility.SetAnimationClipSettings(clip, settings);

		if (isNew)
			AssetDatabase.CreateAsset(clip, path);
		else
			EditorUtility.SetDirty(clip);

		return clip;
	}

	private static void CreateController(
		VariantDefinition variant,
		IReadOnlyDictionary<string, AnimationClip> idleClips,
		AnimationClip walkDown,
		AnimationClip walkLeft,
		AnimationClip walkRight,
		AnimationClip walkUp)
	{
		string controllerPath = $"{AnimationRoot}/ButlerClassic_{variant.ClipName}.controller";
		AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
		if (controller == null)
			controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

		controller.name = $"ButlerClassic_{variant.ClipName}";
		EnsureParameters(controller);

		AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
		ClearStateMachine(stateMachine);

		AnimatorState idleDown = AddState(stateMachine, $"ButlerClassic_{variant.ClipName}_Idle_Down", idleClips["Down"], 450, 170);
		AnimatorState idleLeft = AddState(stateMachine, $"ButlerClassic_{variant.ClipName}_Idle_Left", idleClips["Left"], 450, 250);
		AnimatorState idleRight = AddState(stateMachine, $"ButlerClassic_{variant.ClipName}_Idle_Right", idleClips["Right"], 450, 330);
		AnimatorState idleUp = AddState(stateMachine, $"ButlerClassic_{variant.ClipName}_Idle_Up", idleClips["Up"], 450, 410);
		AnimatorState walkDownState = AddState(stateMachine, "ButlerClassic_Walk_Down", walkDown, 730, 170);
		AnimatorState walkLeftState = AddState(stateMachine, "ButlerClassic_Walk_Left", walkLeft, 730, 250);
		AnimatorState walkRightState = AddState(stateMachine, "ButlerClassic_Walk_Right", walkRight, 730, 330);
		AnimatorState walkUpState = AddState(stateMachine, "ButlerClassic_Walk_Up", walkUp, 730, 410);

		stateMachine.defaultState = idleRight;
		AddWalkTransition(stateMachine, walkUpState, "IsWalkingUp");
		AddWalkTransition(stateMachine, walkDownState, "IsWalkingDown");
		AddWalkTransition(stateMachine, walkLeftState, "IsWalkingLeft");
		AddWalkTransition(stateMachine, walkRightState, "IsWalkingRight");
		AddIdleTransition(stateMachine, idleUp, "IsFacingUp");
		AddIdleTransition(stateMachine, idleDown, "IsFacingDown");
		AddIdleTransition(stateMachine, idleLeft, "IsFacingLeft");
		AddIdleTransition(stateMachine, idleRight, "IsFacingRight");

		EditorUtility.SetDirty(controller);
	}

	private static void EnsureParameters(AnimatorController controller)
	{
		EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float, false);
		EnsureParameter(controller, "IsCrouching", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsJumping", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsWalkingUp", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsWalkingDown", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsWalkingLeft", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsWalkingRight", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsFacingUp", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsFacingDown", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsFacingLeft", AnimatorControllerParameterType.Bool, false);
		EnsureParameter(controller, "IsFacingRight", AnimatorControllerParameterType.Bool, true);
	}

	private static void EnsureParameter(
		AnimatorController controller,
		string parameterName,
		AnimatorControllerParameterType parameterType,
		bool defaultBool)
	{
		AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == parameterName);
		if (parameter == null)
		{
			controller.AddParameter(parameterName, parameterType);
			parameter = controller.parameters.First(item => item.name == parameterName);
		}

		parameter.type = parameterType;
		parameter.defaultBool = defaultBool;
	}

	private static void ClearStateMachine(AnimatorStateMachine stateMachine)
	{
		foreach (ChildAnimatorState child in stateMachine.states.ToArray())
			stateMachine.RemoveState(child.state);

		foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
			stateMachine.RemoveAnyStateTransition(transition);
	}

	private static AnimatorState AddState(
		AnimatorStateMachine stateMachine,
		string stateName,
		Motion motion,
		float x,
		float y)
	{
		AnimatorState state = stateMachine.AddState(stateName, new Vector3(x, y, 0f));
		state.motion = motion;
		state.writeDefaultValues = true;
		return state;
	}

	private static void AddWalkTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string parameterName)
	{
		AnimatorStateTransition transition = AddAnyStateTransition(stateMachine, destination);
		transition.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");
		transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
	}

	private static void AddIdleTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string parameterName)
	{
		AnimatorStateTransition transition = AddAnyStateTransition(stateMachine, destination);
		transition.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
		transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
	}

	private static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination)
	{
		AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
		transition.hasExitTime = false;
		transition.hasFixedDuration = true;
		transition.duration = 0.05f;
		transition.offset = 0f;
		transition.exitTime = 0f;
		transition.canTransitionToSelf = false;
		return transition;
	}

	private static EditorCurveBinding SpriteBinding(Type type)
	{
		return new EditorCurveBinding
		{
			path = string.Empty,
			type = type,
			propertyName = "m_Sprite"
		};
	}

	private static List<Sprite> LoadVariantSprites(string variantFolder, string direction)
	{
		string folder = $"{FrameRoot}/{variantFolder}/aligned";
		if (!Directory.Exists(folder))
			throw new DirectoryNotFoundException(folder);

		return Directory.GetFiles(folder, $"butler_classic_{variantFolder}_{direction}_*.png")
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/')))
			.Where(sprite => sprite != null)
			.ToList();
	}

	private static T LoadRequired<T>(string path) where T : UnityEngine.Object
	{
		T asset = AssetDatabase.LoadAssetAtPath<T>(path);
		if (asset == null)
			throw new FileNotFoundException($"Could not load required asset at {path}.");

		return asset;
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

	private readonly struct VariantDefinition
	{
		public VariantDefinition(string folderName, string clipName)
		{
			FolderName = folderName;
			ClipName = clipName;
		}

		public string FolderName { get; }
		public string ClipName { get; }
	}
}
