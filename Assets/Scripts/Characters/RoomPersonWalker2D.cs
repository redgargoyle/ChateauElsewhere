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
	[SerializeField] private bool disableRaycastTarget = true;

	private RectTransform rectTransform;
	private RectTransform facingTransform;
	private Quaternion authoredFacingRotation = Quaternion.identity;
	private bool hasAuthoredFacingRotation;
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

	public Graphic TargetGraphic => targetGraphic;
	public Vector2 CurrentPosition => currentPosition;

#if UNITY_EDITOR
	private double lastEditorTime;
#endif

	private void Reset()
	{
		ResolveReferences();
		currentPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
	}

	private void Awake()
	{
		ResolveReferences();
		CacheAnimatorParameters();
		ResetPathPositionIfNeeded();
		ApplyVisuals();
	}

	private void OnEnable()
	{
		ResolveReferences();
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
		ResolveReferences();
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

		RectTransform resolvedFacingTransform = targetGraphic != null ? targetGraphic.rectTransform : null;
		if (resolvedFacingTransform != facingTransform)
		{
			facingTransform = resolvedFacingTransform;
			hasAuthoredFacingRotation = false;
		}

		if (!hasAuthoredFacingRotation && facingTransform != null)
		{
			authoredFacingRotation = facingTransform.localRotation;
			hasAuthoredFacingRotation = true;
		}
	}

	private void CacheAnimatorParameters()
	{
		animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
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

		if (targetGraphic != null)
		{
			if (disableRaycastTarget)
				targetGraphic.raycastTarget = false;
		}

		if (rectTransform != null)
		{
			rectTransform.anchoredPosition = GetRenderedPosition(currentPosition + GetMotionOffset());
		}

		animatorParameters.ApplyMovement(animator, movingAlongPath, walkDirection, animationSpeed);
		ApplyFacing();
	}

	private void ApplyFacing()
	{
		if (!hasAuthoredFacingRotation || facingTransform == null)
		{
			return;
		}

		Quaternion mirrorRotation = Application.isPlaying && facingSign < 0
			? Quaternion.Euler(0f, 180f, 0f)
			: Quaternion.identity;
		facingTransform.localRotation = authoredFacingRotation * mirrorRotation;
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

	private Vector2 GetRenderedPosition(Vector2 position)
	{
		if (!snapToWholePixels)
			return position;

		return new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
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
