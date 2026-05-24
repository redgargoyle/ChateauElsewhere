using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PointClickPlayerMovement : MonoBehaviour
{
	private const float MovementEpsilon = 0.0001f;
	private const float WalkableInsetStep = 0.015f;
	private const int WalkableInsetAttempts = 12;

	[SerializeField] private string walkableFloorName = "PlayerBoundary_Entrance";
	[SerializeField] private Collider2D walkableFloor;
	[SerializeField] private float moveSpeed = 3.2f;
	[SerializeField] private float stopDistance = 0.04f;
	[SerializeField, Range(0.5f, 1f)] private float horizontalDirectionThreshold = 0.58f;
	[SerializeField] private bool allowMovementWithoutWalkableFloor;
	[SerializeField] private string playerSortingLayerName = "People";
	[SerializeField] private int playerSortingOrderBase = 1000;
	[SerializeField] private float playerSortingOrderPerYUnit = 100f;
	[SerializeField, Range(0.25f, 1f)] private float verticalMovementSpeedMultiplier = 0.7f;
	[SerializeField] private float nearY = -4.25f;
	[SerializeField] private float farY = -2.25f;
	[SerializeField] private float nearScale = 1f;
	[SerializeField] private float farScale = 0.58f;
	[SerializeField] private float runningAnimationSpeed = 40f;
	[SerializeField] private bool disablePlatformMovement = true;

	private Rigidbody2D body;
	private Animator animator;
	private SpriteRenderer spriteRenderer;
	private SpriteRenderer[] spriteRenderers;
	private CameraManager cameraManager;
	private Vector2 destination;
	private Vector2 logicalPosition;
	private Vector3 currentVisualOffset;
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;

	public event Action ArrivedAtDestination;
	public event Action MovementStopped;
	public Vector2 LogicalPosition => logicalPosition;
	public bool HasDestination => hasDestination;

	public readonly struct MovementTargetQuery
	{
		public MovementTargetQuery(
			Vector2 screenPosition,
			Vector2 requestedLogicalPosition,
			Vector2 destination,
			bool exactPointWalkable,
			bool hasReachableDestination,
			bool wouldMove)
		{
			ScreenPosition = screenPosition;
			RequestedLogicalPosition = requestedLogicalPosition;
			Destination = destination;
			ExactPointWalkable = exactPointWalkable;
			HasReachableDestination = hasReachableDestination;
			WouldMove = wouldMove;
		}

		public Vector2 ScreenPosition { get; }
		public Vector2 RequestedLogicalPosition { get; }
		public Vector2 Destination { get; }
		public bool ExactPointWalkable { get; }
		public bool HasReachableDestination { get; }
		public bool WouldMove { get; }
	}

	private void Awake()
	{
		CacheReferences();
		CacheAnimatorParameters();
	}

	private void Start()
	{
		FindWalkableFloor();

		if (walkableFloor == null && !allowMovementWithoutWalkableFloor)
		{
			enabled = false;
			return;
		}

		ConfigurePointAndClickMovement();
	}

	private void Update()
	{
		if (!isReady)
			return;

		UpdateWalkCursor();

		if (TryGetFloorClick(out Vector2 clickPosition))
			SetDestination(clickPosition);

		UpdateAnimator();
	}

	private void OnDisable()
	{
		NavigationCursorController.ClearWalkHover(this);
	}

	private void FixedUpdate()
	{
		if (!isReady)
			return;

		MoveTowardDestination();
	}

	private void LateUpdate()
	{
		if (!isReady)
			return;

		ApplyVisualPosition();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private void CacheReferences()
	{
		if (body == null)
			TryGetComponent(out body);

		if (animator == null)
			TryGetComponent(out animator);

		if (spriteRenderer == null)
		{
			if (!TryGetComponent(out spriteRenderer))
				spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}

		if (spriteRenderers == null || spriteRenderers.Length == 0)
			spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

		if (cameraManager == null)
			cameraManager = FindAnyObjectByType<CameraManager>();
	}

	private void CacheAnimatorParameters()
	{
		animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
	}

	private void FindWalkableFloor()
	{
		if (walkableFloor != null)
			return;

		GameObject floorObject = GameObject.Find(walkableFloorName);
		if (floorObject != null)
			walkableFloor = floorObject.GetComponent<Collider2D>();

		if (walkableFloor == null)
			walkableFloor = FindPlayerBoundaryCollider();
	}

	private void ConfigurePointAndClickMovement()
	{
		if (disablePlatformMovement)
		{
			if (TryGetComponent(out PlayerMovement platformMovement))
				platformMovement.enabled = false;

			if (TryGetComponent(out CharacterController2D platformController))
				platformController.enabled = false;
		}

		body.bodyType = RigidbodyType2D.Kinematic;
		body.gravityScale = 0f;
		body.freezeRotation = true;
		body.linearVelocity = Vector2.zero;
		body.angularVelocity = 0f;

		Vector2 startPosition = ClampToWalkableArea(transform.position);
		logicalPosition = startPosition;
		destination = startPosition;
		walkDirection = CharacterWalkDirection.Right;
		isReady = true;
		isWalking = false;

		UpdateAnimator();
		ApplySpriteMirror();
		ApplyVisualPosition();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private bool TryGetFloorClick(out Vector2 clickPosition)
	{
		clickPosition = Vector2.zero;

		if (!TryGetPrimaryPointerDown(out Vector2 screenPosition))
			return false;

		if (CharacterSelectionMenu.IsBlockingGameplayInput(screenPosition))
			return false;

		if (IsPointerOverUi())
			return false;

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, true, out MovementTargetQuery movementQuery))
			return false;

		if (!movementQuery.HasReachableDestination || !movementQuery.WouldMove)
			return false;

		clickPosition = movementQuery.Destination;
		return true;
	}

	public bool TrySetDestinationFromScreenPoint(Vector2 screenPosition, bool clampToWalkableArea = false)
	{
		if (!TryEvaluateMovementAtScreenPoint(screenPosition, clampToWalkableArea, out MovementTargetQuery movementQuery) ||
			!movementQuery.HasReachableDestination)
			return false;

		SetDestination(movementQuery.Destination);
		return true;
	}

	public bool TrySetDestination(Vector2 targetPosition, bool clampToWalkableArea = false)
	{
		if (!isReady)
			return false;

		if (!TryEvaluateMovementTarget(targetPosition, clampToWalkableArea, out MovementTargetQuery movementQuery) ||
			!movementQuery.HasReachableDestination)
			return false;

		SetDestination(movementQuery.Destination);
		return true;
	}

	public bool TryEvaluateMovementAtScreenPoint(Vector2 screenPosition, bool clampToWalkableArea, out MovementTargetQuery movementQuery)
	{
		movementQuery = default;

		if (!isReady || !TryGetLogicalPointFromScreen(screenPosition, out Vector2 targetPosition))
			return false;

		return TryEvaluateMovementTarget(targetPosition, clampToWalkableArea, screenPosition, out movementQuery);
	}

	public bool TryGetScreenPointFromLogicalPosition(Vector2 logicalPoint, out Vector2 screenPoint)
	{
		screenPoint = Vector2.zero;

		Camera mainCamera = Camera.main;
		if (mainCamera == null)
			return false;

		UpdateVisualOffset(mainCamera);
		Vector3 visualPosition = new Vector3(
			logicalPoint.x + currentVisualOffset.x,
			logicalPoint.y + currentVisualOffset.y,
			transform.position.z);

		screenPoint = mainCamera.WorldToScreenPoint(visualPosition);
		return true;
	}

	private bool TryEvaluateMovementTarget(Vector2 targetPosition, bool clampToWalkableArea, out MovementTargetQuery movementQuery)
	{
		return TryEvaluateMovementTarget(targetPosition, clampToWalkableArea, Vector2.zero, out movementQuery);
	}

	private bool TryEvaluateMovementTarget(
		Vector2 targetPosition,
		bool clampToWalkableArea,
		Vector2 screenPosition,
		out MovementTargetQuery movementQuery)
	{
		movementQuery = default;

		if (!isReady)
			return false;

		bool blockedByPickup = IsPickupObjectAtPoint(targetPosition);
		bool exactPointWalkable = !blockedByPickup && IsPointWalkable(targetPosition);
		Vector2 destinationPosition = targetPosition;

		if (!exactPointWalkable && clampToWalkableArea && !blockedByPickup)
			destinationPosition = ClampToWalkableArea(targetPosition, logicalPosition);

		bool hasReachableDestination = !blockedByPickup && IsPointWalkable(destinationPosition);
		bool wouldMove = hasReachableDestination &&
			Vector2.Distance(logicalPosition, destinationPosition) > stopDistance;

		movementQuery = new MovementTargetQuery(
			screenPosition,
			targetPosition,
			destinationPosition,
			exactPointWalkable,
			hasReachableDestination,
			wouldMove);
		return true;
	}

	private bool TryGetLogicalPointFromScreen(Vector2 screenPosition, out Vector2 logicalPoint)
	{
		logicalPoint = Vector2.zero;

		Camera mainCamera = Camera.main;
		if (mainCamera == null)
			return false;

		UpdateVisualOffset(mainCamera);
		Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
		logicalPoint = worldPosition - currentVisualOffset;
		return true;
	}

	private static bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
	{
		screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
		Mouse mouse = Mouse.current;
		if (mouse != null && mouse.leftButton.wasPressedThisFrame)
		{
			screenPosition = mouse.position.ReadValue();
			return true;
		}
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
		try
		{
			if (!Input.GetMouseButtonDown(0))
				return false;

			screenPosition = Input.mousePosition;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
#else
		return false;
#endif
	}

	private static bool TryGetPrimaryPointerPosition(out Vector2 screenPosition)
	{
		screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
		Mouse mouse = Mouse.current;
		if (mouse != null)
		{
			screenPosition = mouse.position.ReadValue();
			return true;
		}
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
		try
		{
			screenPosition = Input.mousePosition;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
#else
		return false;
#endif
	}

	private static bool IsPointerOverUi()
	{
		EventSystem eventSystem = EventSystem.current;
		if (eventSystem == null)
			return false;

		return eventSystem.IsPointerOverGameObject();
	}

	private static bool IsPickupObjectAtPoint(Vector2 point)
	{
		Collider2D[] hits = Physics2D.OverlapPointAll(point);
		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] != null && hits[i].GetComponentInParent<PickupObject>() != null)
				return true;
		}

		return false;
	}

	private void UpdateWalkCursor()
	{
		if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition) ||
			CharacterSelectionMenu.IsBlockingGameplayInput(screenPosition) ||
			IsPointerOverUi())
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, true, out MovementTargetQuery movementQuery))
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		NavigationCursorController.SetWalkHover(this, true, movementQuery.WouldMove);
	}

	private void SetDestination(Vector2 clickPosition)
	{
		destination = clickPosition;
		Vector2 movement = destination - logicalPosition;
		hasDestination = movement.magnitude > stopDistance;
		if (hasDestination)
			UpdateWalkDirection(movement);

		isWalking = hasDestination;
	}

	private void MoveTowardDestination()
	{
		if (!hasDestination)
		{
			isWalking = false;
			ApplySpriteMirror();
			return;
		}

		Vector2 currentPosition = logicalPosition;
		Vector2 nextPosition = MoveLogicalPositionToward(currentPosition, destination, moveSpeed * Time.fixedDeltaTime);
		Vector2 movement = nextPosition - currentPosition;

		if (!IsPointWalkable(nextPosition))
		{
			hasDestination = false;
			isWalking = false;
			ApplySpriteMirror();
			MovementStopped?.Invoke();
			return;
		}

		logicalPosition = nextPosition;
		UpdateWalkDirection(movement);

		if (Vector2.Distance(nextPosition, destination) <= stopDistance)
		{
			logicalPosition = destination;
			hasDestination = false;
			isWalking = false;
			ApplySpriteMirror();
			ArrivedAtDestination?.Invoke();
			MovementStopped?.Invoke();
		}
		else
		{
			isWalking = true;
		}
	}

	private void ApplyPerspectiveScale()
	{
		float depth = Mathf.InverseLerp(nearY, farY, logicalPosition.y);
		float scale = Mathf.Lerp(nearScale, farScale, depth);
		transform.localScale = new Vector3(scale, scale, transform.localScale.z);
	}

	private void UpdateAnimator()
	{
		animatorParameters.ApplyMovement(animator, isWalking, walkDirection, runningAnimationSpeed);
		ApplySpriteMirror();
	}

	private void UpdateWalkDirection(Vector2 movement)
	{
		if (movement.sqrMagnitude <= MovementEpsilon)
			return;

		walkDirection = DetermineWalkDirection(movement);
	}

	private CharacterWalkDirection DetermineWalkDirection(Vector2 movement)
	{
		return CharacterAnimatorDriver.DetermineDirection(movement, walkDirection, horizontalDirectionThreshold);
	}

	private Vector2 MoveLogicalPositionToward(Vector2 currentPosition, Vector2 targetPosition, float maxDistance)
	{
		Vector2 movement = targetPosition - currentPosition;
		if (movement.magnitude <= stopDistance)
			return targetPosition;

		Vector2 direction = movement.normalized;
		direction.y *= Mathf.Clamp(verticalMovementSpeedMultiplier, 0.25f, 1f);

		Vector2 step = direction * maxDistance;
		if (step.sqrMagnitude >= movement.sqrMagnitude)
			return targetPosition;

		return currentPosition + step;
	}

	private void ApplySpriteMirror()
	{
		if (spriteRenderer != null)
			spriteRenderer.flipX = false;
	}

	private void ApplyVisualPosition()
	{
		UpdateVisualOffset(Camera.main);
		Vector3 visualPosition = new Vector3(
			logicalPosition.x + currentVisualOffset.x,
			logicalPosition.y + currentVisualOffset.y,
			transform.position.z);

		transform.position = visualPosition;

		if (body != null)
			body.position = visualPosition;
	}

	private void UpdateVisualOffset(Camera mainCamera)
	{
		currentVisualOffset = Vector3.zero;

		if (cameraManager == null)
			cameraManager = FindAnyObjectByType<CameraManager>();

		if (cameraManager != null && cameraManager.TryGetRoomStageWorldOffset(mainCamera, out Vector3 roomOffset))
			currentVisualOffset = roomOffset;
	}

	private bool IsPointWalkable(Vector2 point)
	{
		return walkableFloor == null || walkableFloor.OverlapPoint(point);
	}

	private Vector2 ClampToWalkableArea(Vector2 point)
	{
		return ClampToWalkableArea(point, logicalPosition);
	}

	private Vector2 ClampToWalkableArea(Vector2 point, Vector2 preferredInsidePoint)
	{
		if (walkableFloor == null || walkableFloor.OverlapPoint(point))
			return point;

		Vector2 closestPoint = walkableFloor.ClosestPoint(point);
		if (walkableFloor.OverlapPoint(closestPoint))
			return closestPoint;

		Vector2 insetDirection = preferredInsidePoint - closestPoint;
		if (insetDirection.sqrMagnitude <= MovementEpsilon || !walkableFloor.OverlapPoint(preferredInsidePoint))
			insetDirection = (Vector2)walkableFloor.bounds.center - closestPoint;

		if (insetDirection.sqrMagnitude <= MovementEpsilon)
			return closestPoint;

		insetDirection.Normalize();

		for (int i = 1; i <= WalkableInsetAttempts; i++)
		{
			Vector2 insetPoint = closestPoint + insetDirection * (WalkableInsetStep * i);
			if (walkableFloor.OverlapPoint(insetPoint))
				return insetPoint;
		}

		return closestPoint;
	}

	private static Collider2D FindPlayerBoundaryCollider()
	{
#if UNITY_2023_1_OR_NEWER
		Collider2D[] colliders = FindObjectsByType<Collider2D>();
#else
		Collider2D[] colliders = FindObjectsOfType<Collider2D>();
#endif

		for (int i = 0; i < colliders.Length; i++)
		{
			Collider2D candidate = colliders[i];
			if (candidate == null || !candidate.enabled)
				continue;

			if (candidate.name.StartsWith("PlayerBoundary", StringComparison.OrdinalIgnoreCase))
				return candidate;
		}

		return null;
	}

	private void ApplyPlayerSorting()
	{
		if (spriteRenderers == null || spriteRenderers.Length == 0)
			return;

		string sortingLayerName = GetSortingLayerName(playerSortingLayerName);
		int sortingOrder = playerSortingOrderBase - Mathf.RoundToInt(logicalPosition.y * playerSortingOrderPerYUnit);

		for (int i = 0; i < spriteRenderers.Length; i++)
		{
			SpriteRenderer targetRenderer = spriteRenderers[i];
			if (targetRenderer == null)
				continue;

			targetRenderer.sortingLayerName = sortingLayerName;
			targetRenderer.sortingOrder = sortingOrder;
		}
	}

	private static string GetSortingLayerName(string requestedLayerName)
	{
		if (string.IsNullOrWhiteSpace(requestedLayerName))
			return "Default";

		if (string.Equals(requestedLayerName, "Default", StringComparison.OrdinalIgnoreCase) ||
			SortingLayer.NameToID(requestedLayerName) != 0)
			return requestedLayerName;

		return "Default";
	}

}
