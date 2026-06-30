using System;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RoomPersonWalker2D : MonoBehaviour
{
	private const float FullCycle = 6.28318530718f;

	[SerializeField] private Animator animator;
	[SerializeField] private Graphic targetGraphic;
	[SerializeField] private bool previewInEditMode = true;
	[SerializeField] private bool previewPathInEditMode;
	[SerializeField] private bool snapToWholePixels;
	[SerializeField] [Min(0f)] private float animationSpeed = 40f;
	[SerializeField] [Range(0.5f, 1f)] private float horizontalDirectionThreshold = 0.58f;
	[Header("Motion Polish")]
	[SerializeField] private bool addStepMotion = true;
	[SerializeField] [Min(1f)] private float pixelsPerWalkCycle = 72f;
	[SerializeField] [Min(0f)] private float walkBobPixels = 2.6f;
	[SerializeField] [Min(0f)] private float walkSwayPixels = 0.9f;
	[SerializeField] private bool animateIdlePose = true;
	[SerializeField] [Min(0f)] private float idleBobPixels = 0.9f;
	[SerializeField] [Min(0f)] private float idleSwayPixels = 0.35f;
	[SerializeField] [Min(0.1f)] private float idleCycleSeconds = 2.4f;
	[SerializeField] [Min(0f)] private float pointPauseSeconds = 0f;
	[SerializeField] [Min(0f)] private float endpointPauseSeconds = 0.65f;
	[SerializeField] private bool mirrorWhenWalkingLeft = true;
	[SerializeField] private Vector2[] pathPoints = new Vector2[0];
	[SerializeField] [Min(1f)] private float pixelsPerSecond = 95f;
	[SerializeField] private bool loopPath = true;
	[SerializeField] private bool pingPongPath;
	[Header("Painted-Room Depth")]
	[SerializeField] private RoomPerspectiveProfile roomProfile;
	[SerializeField] private bool useRoomPerspectiveProfileScale = true;
	[SerializeField] private bool useButlerCharacterScaleRules = true;
	[SerializeField] private PointClickPlayerMovement butlerScaleSource;
	[SerializeField] private bool preserveAuthoredLocalScaleWhenUsingButlerRules = true;
	[SerializeField] private float nearY = -360f;
	[SerializeField] private float farY = 150f;
	[SerializeField] [Min(0.01f)] private float nearScale = 1f;
	[SerializeField] [Min(0.01f)] private float farScale = 0.42f;
	[SerializeField] private Color nearTint = new Color(0.92f, 0.88f, 0.78f, 0.93f);
	[SerializeField] private Color farTint = new Color(0.70f, 0.72f, 0.66f, 0.72f);
	[SerializeField] private bool disableRaycastTarget = true;

	private RectTransform rectTransform;
	private RoomProjectedEntity roomProjection;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private int targetPathIndex = 1;
	private int pathDirection = 1;
	private Vector2 currentPosition;
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private int facingSign = 1;
	private float walkCycle;
	private float idleCycle;
	private float pauseTimer;
	private bool movingAlongPath;
	private bool isUsingButlerCharacterScaleRules;
	private float currentButlerCharacterScale = 1f;
	private float currentButlerCharacterDepth01;
	private string currentButlerCharacterScaleSource = string.Empty;
	[SerializeField, HideInInspector] private Vector3 authoredWalkerLocalScale = Vector3.one;
	[SerializeField, HideInInspector] private bool hasAuthoredWalkerLocalScale;

	public RoomPerspectiveProfile RoomProfile => roomProfile;
	public Graphic TargetGraphic => targetGraphic;
	public bool UseButlerCharacterScaleRules => useButlerCharacterScaleRules;
	public PointClickPlayerMovement ButlerScaleSource => butlerScaleSource;
	public bool PreserveAuthoredLocalScaleWhenUsingButlerRules => preserveAuthoredLocalScaleWhenUsingButlerRules;
	public bool IsUsingButlerCharacterScaleRules => isUsingButlerCharacterScaleRules;
	public float CurrentButlerCharacterScale => currentButlerCharacterScale;
	public float CurrentButlerCharacterDepth01 => currentButlerCharacterDepth01;
	public string CurrentButlerCharacterScaleSource => currentButlerCharacterScaleSource;
	public Vector2 CurrentPosition => currentPosition;
	public float CurrentDepthScale => GetDepthScale();

#if UNITY_EDITOR
	private double lastEditorTime;
#endif

	private void Reset()
	{
		ResolveReferences();
		currentPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
		CaptureAuthoredWalkerScale(true);
	}

	private void Awake()
	{
		ResolveReferences();
		CaptureAuthoredWalkerScaleIfNeeded();
		CacheAnimatorParameters();
		ResetPathPositionIfNeeded();
		ApplyVisuals();
	}

	private void OnEnable()
	{
		ResolveReferences();
		CaptureAuthoredWalkerScaleIfNeeded();
		CacheAnimatorParameters();
		ResetPathPositionIfNeeded();
		ApplyVisuals();

#if UNITY_EDITOR
		EditorApplication.update -= EditorTick;
		EditorApplication.update += EditorTick;
		lastEditorTime = EditorApplication.timeSinceStartup;
#endif
	}

	private void OnDisable()
	{
#if UNITY_EDITOR
		EditorApplication.update -= EditorTick;
#endif
	}

	public void RefreshDepthVisualsNow()
	{
		ResolveReferences();
		ApplyVisuals();
	}

	public void SetButlerCharacterScaleRulesEnabled(bool value, bool refreshImmediately = true)
	{
		useButlerCharacterScaleRules = value;

		if (refreshImmediately)
		{
			RefreshDepthVisualsNow();
		}
	}

	public void SetButlerScaleSource(PointClickPlayerMovement source, bool refreshImmediately = true)
	{
		butlerScaleSource = source;

		if (refreshImmediately)
		{
			RefreshDepthVisualsNow();
		}
	}

	public void SetPreserveAuthoredLocalScaleWhenUsingButlerRules(bool value, bool refreshImmediately = true)
	{
		preserveAuthoredLocalScaleWhenUsingButlerRules = value;

		if (refreshImmediately)
		{
			RefreshDepthVisualsNow();
		}
	}

	public void ResetAuthoredWalkerScaleForEditor()
	{
		CaptureAuthoredWalkerScale(true);
		RefreshDepthVisualsNow();
	}

	[Obsolete("Guest body scale is now applied by GuestRoomScaleApplier.")]
	public void ApplyButlerCharacterScaleNow(PointClickPlayerMovement source = null)
	{
		ApplyButlerCharacterScaleNow(source, 1f);
	}

	[Obsolete("Guest body scale is now applied by GuestRoomScaleApplier.")]
	public void ApplyButlerCharacterScaleNow(PointClickPlayerMovement source, float debugScaleMultiplier)
	{
		if (HasActiveGuestScaleParticipant())
		{
			ClearButlerCharacterScaleDebug();
			return;
		}

		if (source != null)
		{
			butlerScaleSource = source;
		}

		ResolveReferences();
		CaptureAuthoredWalkerScaleIfNeeded();

		if (roomProjection != null && roomProjection.IsProjectionActive)
		{
			roomProjection.SetRoomLocalFootPoint(GetRenderedPosition(currentPosition));
			roomProjection.ApplyButlerCharacterScaleNow(source, debugScaleMultiplier);
			return;
		}

		if (!TryGetButlerCharacterScaleForWalker(out PointClickPlayerMovement.ButlerCharacterScaleSample sample) ||
			rectTransform == null)
		{
			ClearButlerCharacterScaleDebug();
			return;
		}

		ApplyButlerScaleSample(sample, debugScaleMultiplier);
	}

	public bool TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample)
	{
		return TryGetButlerCharacterScaleForWalker(out sample);
	}

	public bool UsesPerspectiveProfile(RoomPerspectiveProfile profile)
	{
		return profile != null &&
			TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile currentProfile) &&
			currentProfile == profile;
	}

	private void OnValidate()
	{
		animationSpeed = Mathf.Max(0f, animationSpeed);
		horizontalDirectionThreshold = Mathf.Clamp(horizontalDirectionThreshold, 0.5f, 1f);
		pixelsPerSecond = Mathf.Max(1f, pixelsPerSecond);
		pixelsPerWalkCycle = Mathf.Max(1f, pixelsPerWalkCycle);
		walkBobPixels = Mathf.Max(0f, walkBobPixels);
		walkSwayPixels = Mathf.Max(0f, walkSwayPixels);
		idleBobPixels = Mathf.Max(0f, idleBobPixels);
		idleSwayPixels = Mathf.Max(0f, idleSwayPixels);
		idleCycleSeconds = Mathf.Max(0.1f, idleCycleSeconds);
		pointPauseSeconds = Mathf.Max(0f, pointPauseSeconds);
		endpointPauseSeconds = Mathf.Max(0f, endpointPauseSeconds);
		nearScale = Mathf.Max(0.01f, nearScale);
		farScale = Mathf.Max(0.01f, farScale);
		ResolveReferences();
		CaptureAuthoredWalkerScaleIfNeeded();
		CacheAnimatorParameters();
		ApplyVisuals();
	}

	private void Update()
	{
		if (!Application.isPlaying)
			return;

		Tick(Time.deltaTime, true);
	}

	private void ResolveReferences()
	{
		if (rectTransform == null)
			rectTransform = transform as RectTransform;

		if (animator == null)
			animator = GetComponent<Animator>();

		if (targetGraphic == null)
			targetGraphic = GetComponent<Graphic>();

		if (roomProjection == null)
			roomProjection = GetComponent<RoomProjectedEntity>();

		if (roomProfile == null)
		{
			RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);

			if (roomContent != null)
			{
				roomProfile = roomContent.PerspectiveProfile;
			}
		}
	}

	private void CacheAnimatorParameters()
	{
		animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
	}

	private void CaptureAuthoredWalkerScaleIfNeeded()
	{
		if (hasAuthoredWalkerLocalScale)
		{
			return;
		}

		CaptureAuthoredWalkerScale(false);
	}

	private void CaptureAuthoredWalkerScale(bool force)
	{
		if (!force && hasAuthoredWalkerLocalScale)
		{
			return;
		}

		authoredWalkerLocalScale = rectTransform != null ? rectTransform.localScale : transform.localScale;
		authoredWalkerLocalScale = SanitizeScale(authoredWalkerLocalScale);
		hasAuthoredWalkerLocalScale = true;
	}

	private void ResetPathPositionIfNeeded()
	{
		if (pathPoints == null || pathPoints.Length == 0)
		{
			currentPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
			return;
		}

		if (currentPosition == Vector2.zero || rectTransform == null)
			currentPosition = pathPoints[0];

		if (rectTransform != null)
			rectTransform.anchoredPosition = currentPosition;

		targetPathIndex = Mathf.Clamp(targetPathIndex, 0, pathPoints.Length - 1);

		if (pathPoints.Length > 1 && targetPathIndex == 0)
			targetPathIndex = 1;
	}

	private void Tick(float deltaTime, bool moveAlongPath)
	{
		float safeDeltaTime = Mathf.Max(0f, deltaTime);
		Vector2 previousPosition = currentPosition;
		float distanceMoved = 0f;

		if (moveAlongPath)
			distanceMoved = AdvanceAlongPath(safeDeltaTime);

		Vector2 movement = currentPosition - previousPosition;
		UpdateWalkDirection(movement);
		AdvanceMotionCycles(safeDeltaTime, distanceMoved);
		ApplyVisuals();
	}

	private float AdvanceAlongPath(float deltaTime)
	{
		if (pathPoints == null || pathPoints.Length < 2 || rectTransform == null)
		{
			movingAlongPath = false;
			return 0f;
		}

		if (pauseTimer > 0f)
		{
			pauseTimer = Mathf.Max(0f, pauseTimer - deltaTime);
			movingAlongPath = false;
			return 0f;
		}

		float remainingDistance = pixelsPerSecond * deltaTime;
		float movedDistance = 0f;

		while (remainingDistance > 0f)
		{
			int arrivedPointIndex = Mathf.Clamp(targetPathIndex, 0, pathPoints.Length - 1);
			Vector2 target = pathPoints[arrivedPointIndex];
			Vector2 toTarget = target - currentPosition;
			float distanceToTarget = toTarget.magnitude;

			if (distanceToTarget <= 0.01f)
			{
				bool shouldPause = BeginPauseForPoint(arrivedPointIndex);
				AdvancePathTarget();
				if (shouldPause)
					break;

				continue;
			}

			float stepDistance = Mathf.Min(remainingDistance, distanceToTarget);
			Vector2 step = toTarget / distanceToTarget * stepDistance;
			currentPosition += step;
			remainingDistance -= stepDistance;
			movedDistance += stepDistance;

			if (stepDistance >= distanceToTarget - 0.01f)
			{
				bool shouldPause = BeginPauseForPoint(arrivedPointIndex);
				AdvancePathTarget();
				if (shouldPause)
					break;
			}
		}

		movingAlongPath = movedDistance > 0.001f;
		return movedDistance;
	}

	private void AdvancePathTarget()
	{
		if (pathPoints == null || pathPoints.Length < 2)
			return;

		if (pingPongPath)
		{
			targetPathIndex += pathDirection;

			if (targetPathIndex >= pathPoints.Length)
			{
				pathDirection = -1;
				targetPathIndex = Mathf.Max(0, pathPoints.Length - 2);
			}
			else if (targetPathIndex < 0)
			{
				pathDirection = 1;
				targetPathIndex = Mathf.Min(1, pathPoints.Length - 1);
			}

			return;
		}

		targetPathIndex++;

		if (targetPathIndex >= pathPoints.Length)
			targetPathIndex = loopPath ? 0 : pathPoints.Length - 1;
	}

	private bool BeginPauseForPoint(int pointIndex)
	{
		float pauseSeconds = IsEndpoint(pointIndex) ? endpointPauseSeconds : pointPauseSeconds;
		if (pauseSeconds <= 0f)
			return false;

		pauseTimer = pauseSeconds;
		return true;
	}

	private bool IsEndpoint(int pointIndex)
	{
		return pathPoints != null &&
			pathPoints.Length > 1 &&
			(pointIndex <= 0 || pointIndex >= pathPoints.Length - 1);
	}

	private void UpdateWalkDirection(Vector2 movement)
	{
		if (movement.sqrMagnitude <= 0.0001f)
			return;

		walkDirection = CharacterAnimatorDriver.DetermineDirection(
			movement,
			walkDirection,
			horizontalDirectionThreshold);

		if (walkDirection == CharacterWalkDirection.Up || walkDirection == CharacterWalkDirection.Down)
			facingSign = 1;
		else if (movement.x < -0.01f && mirrorWhenWalkingLeft)
			facingSign = -1;
		else if (movement.x > 0.01f)
			facingSign = 1;
	}

	private void AdvanceMotionCycles(float deltaTime, float distanceMoved)
	{
		if (distanceMoved > 0.001f)
		{
			walkCycle = Mathf.Repeat(
				walkCycle + (distanceMoved / Mathf.Max(1f, pixelsPerWalkCycle)) * FullCycle,
				FullCycle);
		}

		idleCycle = Mathf.Repeat(
			idleCycle + (deltaTime / Mathf.Max(0.1f, idleCycleSeconds)) * FullCycle,
			FullCycle);
	}

	private void ApplyVisuals()
	{
		ResolveReferences();
		bool useRoomProjection = roomProjection != null && roomProjection.IsProjectionActive;

		if (targetGraphic != null)
		{
			if (!useRoomProjection)
				targetGraphic.color = GetDepthTint();

			if (disableRaycastTarget)
				targetGraphic.raycastTarget = false;
		}

		if (useRoomProjection)
		{
			roomProjection.SetRoomLocalFootPoint(GetRenderedPosition(currentPosition));
		}
		else if (rectTransform != null)
		{
			rectTransform.anchoredPosition = GetRenderedPosition(currentPosition + GetMotionOffset());

			if (!HasActiveGuestScaleParticipant())
			{
				rectTransform.localScale = BuildDepthScaleVector(GetDepthScale(), isUsingButlerCharacterScaleRules, 1f);
			}
		}

		animatorParameters.ApplyMovement(animator, movingAlongPath, walkDirection, animationSpeed);
	}

	private Vector2 GetMotionOffset()
	{
		if (!Application.isPlaying)
			return Vector2.zero;

		if (movingAlongPath && addStepMotion)
		{
			float stride = Mathf.Sin(walkCycle);
			return new Vector2(
				stride * walkSwayPixels * facingSign,
				Mathf.Abs(stride) * walkBobPixels);
		}

		if (!animateIdlePose)
			return Vector2.zero;

		return new Vector2(
			Mathf.Sin(idleCycle * 0.7f) * idleSwayPixels,
			Mathf.Sin(idleCycle) * idleBobPixels);
	}

	private float GetDepth01()
	{
		if (TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile profile))
			return profile.GetDepth01(currentPosition);

		return Mathf.Clamp01(Mathf.InverseLerp(nearY, farY, currentPosition.y));
	}

	private float GetDepthScale()
	{
		if (HasActiveGuestScaleParticipant())
		{
			ClearButlerCharacterScaleDebug();

			if (TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile participantProfile))
				return participantProfile.GetScale(currentPosition);

			return Mathf.Lerp(nearScale, farScale, GetDepth01());
		}

		if (TryGetButlerCharacterScaleForWalker(out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
			return sample.NormalizedScale;

		ClearButlerCharacterScaleDebug();

		if (TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile profile))
			return profile.GetScale(currentPosition);

		return Mathf.Lerp(nearScale, farScale, GetDepth01());
	}

	private Vector3 BuildDepthScaleVector(float depthScale, bool useAuthoredScale, float debugScaleMultiplier)
	{
		float safeDepthScale = Mathf.Max(0.001f, depthScale) * Mathf.Max(0.001f, debugScaleMultiplier);

		if (!useAuthoredScale || !preserveAuthoredLocalScaleWhenUsingButlerRules)
		{
			Vector3 scale = Vector3.one * safeDepthScale;
			scale.x *= facingSign;
			return scale;
		}

		Vector3 baseScale = hasAuthoredWalkerLocalScale ? authoredWalkerLocalScale : Vector3.one;
		return new Vector3(
			Mathf.Abs(baseScale.x) * safeDepthScale * facingSign,
			Mathf.Abs(baseScale.y) * safeDepthScale,
			baseScale.z);
	}

	private void ApplyButlerScaleSample(PointClickPlayerMovement.ButlerCharacterScaleSample sample, float debugScaleMultiplier)
	{
		if (HasActiveGuestScaleParticipant())
		{
			ClearButlerCharacterScaleDebug();
			return;
		}

		isUsingButlerCharacterScaleRules = true;
		currentButlerCharacterScale = sample.NormalizedScale;
		currentButlerCharacterDepth01 = sample.Depth01;
		currentButlerCharacterScaleSource = sample.Source;
		rectTransform.localScale = BuildDepthScaleVector(sample.NormalizedScale, true, debugScaleMultiplier);
	}

	private bool TryGetButlerCharacterScaleForWalker(out PointClickPlayerMovement.ButlerCharacterScaleSample sample)
	{
		sample = default;

		if (HasActiveGuestScaleParticipant())
		{
			return false;
		}

		if (!useButlerCharacterScaleRules)
		{
			return false;
		}

		string roomId = ResolveButlerScaleRoomId();

		if (string.IsNullOrWhiteSpace(roomId))
		{
			return false;
		}

		PointClickPlayerMovement source = ResolveButlerScaleSource();

		if (source == null || !source.TryEvaluateButlerCharacterScale(roomId, currentPosition, out sample))
		{
			return false;
		}

		isUsingButlerCharacterScaleRules = true;
		currentButlerCharacterScale = sample.NormalizedScale;
		currentButlerCharacterDepth01 = sample.Depth01;
		currentButlerCharacterScaleSource = sample.Source;
		return true;
	}

	private string ResolveButlerScaleRoomId()
	{
		RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);

		if (roomContent != null && !string.IsNullOrWhiteSpace(roomContent.RoomName))
		{
			return roomContent.RoomName;
		}

		if (roomProfile != null && !string.IsNullOrWhiteSpace(roomProfile.RoomId))
		{
			return roomProfile.RoomId;
		}

		ActorRoomState actorRoomState = GetComponentInParent<ActorRoomState>(true);
		return actorRoomState != null ? actorRoomState.CurrentRoomId : string.Empty;
	}

	private PointClickPlayerMovement ResolveButlerScaleSource()
	{
		if (butlerScaleSource != null)
		{
			return butlerScaleSource;
		}

		PointClickPlayerMovement activeTaggedPlayer = null;
		PointClickPlayerMovement activeNamedPlayer = null;
		PointClickPlayerMovement firstActive = null;
		PointClickPlayerMovement firstInactive = null;
		PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

		for (int i = 0; i < candidates.Length; i++)
		{
			PointClickPlayerMovement candidate = candidates[i];

			if (candidate == null || candidate.gameObject == null)
			{
				continue;
			}

			bool isActive = candidate.gameObject.activeInHierarchy;

			if (isActive)
			{
				firstActive ??= candidate;

				if (string.Equals(candidate.gameObject.tag, "Player", System.StringComparison.OrdinalIgnoreCase))
				{
					activeTaggedPlayer ??= candidate;
				}

				if (NameLooksLikePlayerOrButler(candidate.name) ||
					NameLooksLikePlayerOrButler(candidate.gameObject.name))
				{
					activeNamedPlayer ??= candidate;
				}
			}
			else if (!Application.isPlaying)
			{
				firstInactive ??= candidate;
			}
		}

		butlerScaleSource =
			activeTaggedPlayer != null
				? activeTaggedPlayer
				: activeNamedPlayer != null
					? activeNamedPlayer
					: firstActive != null
						? firstActive
						: firstInactive;
		return butlerScaleSource;
	}

	private bool HasActiveGuestScaleParticipant()
	{
		GuestScaleParticipant participant = GetComponent<GuestScaleParticipant>();

		if (participant == null)
		{
			participant = GetComponentInParent<GuestScaleParticipant>(true);
		}

		if (participant == null)
		{
			participant = GetComponentInChildren<GuestScaleParticipant>(true);
		}

		if (participant == null && targetGraphic != null)
		{
			participant = targetGraphic.GetComponentInParent<GuestScaleParticipant>(true);
		}

		if (participant == null && targetGraphic != null)
		{
			participant = targetGraphic.GetComponentInChildren<GuestScaleParticipant>(true);
		}

		if (participant == null ||
			participant.ExcludeFromGuestScaling ||
			participant.IsButler)
		{
			return false;
		}

		Transform participantRoot = participant.ResolveScaleRoot();
		return participantRoot == transform ||
			(rectTransform != null && participantRoot == rectTransform) ||
			(targetGraphic != null && participantRoot == targetGraphic.rectTransform);
	}

	private Color GetDepthTint()
	{
		if (TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile profile))
			return profile.GetTint(currentPosition);

		return Color.Lerp(nearTint, farTint, GetDepth01());
	}

	private bool TryGetRoomPerspectiveProfile(out RoomPerspectiveProfile profile)
	{
		profile = null;

		if (!useRoomPerspectiveProfileScale)
			return false;

		if (roomProfile != null)
		{
			profile = roomProfile;
			return true;
		}

		RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);
		if (roomContent != null && roomContent.TryGetPerspectiveProfile(out profile))
			return true;

		return false;
	}

	private Vector2 GetRenderedPosition(Vector2 position)
	{
		if (!snapToWholePixels)
			return position;

		return new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
	}

	private void ClearButlerCharacterScaleDebug()
	{
		isUsingButlerCharacterScaleRules = false;
		currentButlerCharacterScale = 1f;
		currentButlerCharacterDepth01 = 0f;
		currentButlerCharacterScaleSource = string.Empty;
	}

	private static Vector3 SanitizeScale(Vector3 scale)
	{
		return new Vector3(
			Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
			Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
			Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
	}

	private static bool NameLooksLikePlayerOrButler(string value)
	{
		return !string.IsNullOrWhiteSpace(value) &&
			(value.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
			value.IndexOf("Butler", System.StringComparison.OrdinalIgnoreCase) >= 0);
	}

#if UNITY_EDITOR
	private void EditorTick()
	{
		if (Application.isPlaying || this == null)
			return;

		double now = EditorApplication.timeSinceStartup;
		float deltaTime = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.2f);
		lastEditorTime = now;

		if (previewInEditMode)
			Tick(deltaTime, previewPathInEditMode);
	}
#endif
}
