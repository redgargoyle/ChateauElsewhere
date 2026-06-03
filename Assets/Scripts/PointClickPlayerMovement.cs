using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PointClickPlayerMovement : MonoBehaviour
{
	private const string DiagnosticPrefix = "[Ch2ClickDiag]";
	private const float MovementEpsilon = 0.0001f;
	private const float WalkableInsetStep = 0.015f;
	private const int WalkableInsetAttempts = 12;
	private const int WalkableInsetRadialSamples = 16;
	private const int WalkableSearchRings = 36;
	private const int WalkableSearchSamplesPerRing = 32;

	[SerializeField] private string walkableFloorName = "PlayerBoundary_Entrance";
	[SerializeField] private Collider2D walkableFloor;
	[SerializeField] private bool useCurrentRoomBoundary = true;
	[SerializeField] private string roomBoundaryNamePrefix = "PlayerBoundary";
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
	[SerializeField] private bool sortPlayerByVisibleFeet = true;
	[SerializeField] private float playerSortingYOffset;

	private Rigidbody2D body;
	private Animator animator;
	private SpriteRenderer spriteRenderer;
	private SpriteRenderer[] spriteRenderers;
	private CameraManager cameraManager;
	private RoomNavigationManager navigationManager;
	private Vector2 destination;
	private Vector2 finalDestination;
	private Vector2 logicalPosition;
	private Vector3 currentVisualOffset;
	private Vector3 currentRoomStageWorldCenter;
	private Vector3 roomStageReferenceWorldCenter;
	private float roomStageReferenceScale = 1f;
	private float currentRoomStageScaleRatio = 1f;
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private string currentWalkableBoundaryRoom;
	private string roomStageVisualReferenceRoom;
	private bool hasRoomStageVisualReference;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;
	private bool inputEnabled = true;
	private int currentSortingOrder;
	private int movementPathIndex;
	private readonly List<Vector2> movementPath = new List<Vector2>();
	private readonly List<Vector2> movementQueryPath = new List<Vector2>();
	private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

	public event Action ArrivedAtDestination;
	public event Action MovementStopped;
	public Vector2 LogicalPosition => logicalPosition;
	public bool HasDestination => hasDestination;
	public int CurrentSortingOrder => currentSortingOrder;
	public bool InputEnabled => inputEnabled;

	public void SetInputEnabled(bool enabled)
	{
		inputEnabled = enabled;

		if (!inputEnabled)
		{
			NavigationCursorController.ClearWalkHover(this);
			CancelDestination();
		}
	}

	public void RefreshAnimatorParameters()
	{
		CacheReferences();
		CacheAnimatorParameters();
	}

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
		InitializeVisualStateFromTransform();
	}

	private void Start()
	{
		FindWalkableFloor();
		RefreshWalkableFloorForCurrentRoom();

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

		RefreshWalkableFloorForCurrentRoom();

		if (inputEnabled)
		{
			UpdateWalkCursor();

			if (TryGetFloorClick(out Vector2 clickPosition, out Vector2 screenPosition, out bool pointerOverUi))
			{
				SetDestination(clickPosition);
				LogAcceptedFloorClick(screenPosition, pointerOverUi, clickPosition, hasDestination);
			}
		}
		else
		{
			NavigationCursorController.ClearWalkHover(this);
		}

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

		if (navigationManager == null)
			navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
	}

	private void CacheAnimatorParameters()
	{
		animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
	}

	private void InitializeVisualStateFromTransform()
	{
		logicalPosition = transform.position;
		destination = logicalPosition;
		finalDestination = logicalPosition;
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
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

	public void RefreshWalkableFloorForCurrentRoom()
	{
		if (!useCurrentRoomBoundary)
			return;

		if (navigationManager == null)
			navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);

		string currentRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;
		if (string.IsNullOrWhiteSpace(currentRoom))
			return;

		string cleanRoom = CleanRoomName(currentRoom);
		if (string.Equals(currentWalkableBoundaryRoom, cleanRoom, StringComparison.OrdinalIgnoreCase) &&
			walkableFloor != null &&
			walkableFloor.enabled &&
			walkableFloor.gameObject.activeInHierarchy)
		{
			return;
		}

		if (!TryFindPlayerBoundaryForRoom(currentRoom, out Collider2D roomBoundary))
			return;

		walkableFloor = roomBoundary;
		currentWalkableBoundaryRoom = cleanRoom;
		ResetRoomStageVisualReference();

		if (!isReady)
			return;

		logicalPosition = ClampToWalkableArea(logicalPosition);
		destination = logicalPosition;
		finalDestination = logicalPosition;
		movementPath.Clear();
		movementPathIndex = 0;
		hasDestination = false;
		isWalking = false;
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
		finalDestination = startPosition;
		movementPath.Clear();
		movementPathIndex = 0;
		walkDirection = CharacterWalkDirection.Right;
		isReady = true;
		isWalking = false;

		UpdateAnimator();
		ApplySpriteMirror();
		ApplyVisualPosition();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private bool TryGetFloorClick(out Vector2 clickPosition, out Vector2 screenPosition, out bool pointerOverUi)
	{
		clickPosition = Vector2.zero;
		screenPosition = Vector2.zero;
		pointerOverUi = false;

		if (!TryGetPrimaryPointerDown(out screenPosition))
			return false;

		if (Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(screenPosition))
			return false;

		if (DoorTriggerNavigation.IsPointerOverActiveTrigger(screenPosition))
			return false;

		pointerOverUi = IsPointerOverBlockingUi(screenPosition);

		if (pointerOverUi)
			return false;

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, false, out MovementTargetQuery movementQuery))
			return false;

		if (!movementQuery.HasReachableDestination || !movementQuery.WouldMove)
			return false;

		clickPosition = movementQuery.Destination;
		return true;
	}

	private void LogAcceptedFloorClick(Vector2 screenPosition, bool pointerOverUi, Vector2 destination, bool movementStarted)
	{
		Debug.Log(
			$"{DiagnosticPrefix} PlayerMovement accepted floor click frame={Time.frameCount} " +
			$"screen={FormatDiagnosticVector(screenPosition)} pointerOverUi={pointerOverUi} " +
			$"destination={FormatDiagnosticVector(destination)} movementStarted={movementStarted}",
			this);
	}

	private static string FormatDiagnosticVector(Vector2 value)
	{
		return $"({value.x:0.##},{value.y:0.##})";
	}

	public bool TrySetDestinationFromScreenPoint(Vector2 screenPosition, bool clampToWalkableArea = false, bool ignoreInputEnabled = false)
	{
		if (!ignoreInputEnabled && !inputEnabled)
			return false;

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, clampToWalkableArea, out MovementTargetQuery movementQuery) ||
			!movementQuery.HasReachableDestination)
			return false;

		SetDestination(movementQuery.Destination);
		return true;
	}

	public bool TrySetDestination(Vector2 targetPosition, bool clampToWalkableArea = false, bool ignoreInputEnabled = false)
	{
		if (!ignoreInputEnabled && !inputEnabled)
			return false;

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();

		if (!TryEvaluateMovementTarget(targetPosition, clampToWalkableArea, out MovementTargetQuery movementQuery) ||
			!movementQuery.HasReachableDestination)
			return false;

		SetDestination(movementQuery.Destination);
		return true;
	}

	public bool TryWarpTo(Vector2 targetPosition, bool clampToWalkableArea = true)
	{
		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();

		if (!TryEvaluateMovementTarget(targetPosition, clampToWalkableArea, out MovementTargetQuery movementQuery) ||
			!movementQuery.HasReachableDestination)
		{
			return false;
		}

		StopImmediatelyAt(movementQuery.Destination);
		return true;
	}

	public bool TryFindClosestReachableDestinationToWorldPoint(Vector2 worldPoint, out Vector2 destination)
	{
		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);
		return TryFindClosestReachableDestinationToWorldPoint(worldPoint, LogicalToWalkableWorldPoint(logicalPosition), out destination);
	}

	public bool TryFindClosestReachableDestinationToWorldPointTowardRoomCenter(Vector2 worldPoint, out Vector2 destination)
	{
		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);
		Vector2 preferredWorldPoint = walkableFloor != null
			? (Vector2)walkableFloor.bounds.center
			: LogicalToWalkableWorldPoint(logicalPosition);

		return TryFindClosestReachableDestinationToWorldPoint(worldPoint, preferredWorldPoint, out destination);
	}

	public bool TryFindClosestReachableDestinationToWorldPoint(Vector2 worldPoint, Vector2 preferredWorldPoint, out Vector2 destination)
	{
		destination = Vector2.zero;

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);

		Vector2 logicalPoint = WalkableWorldToLogicalPoint(worldPoint);
		Vector2 preferredLogicalPoint = WalkableWorldToLogicalPoint(preferredWorldPoint);
		Vector2 candidateDestination = IsPointWalkable(logicalPoint)
			? logicalPoint
			: ClampToWalkableArea(logicalPoint, preferredLogicalPoint);

		if (TryBuildMovementPath(logicalPosition, candidateDestination, movementQueryPath))
		{
			destination = candidateDestination;
			return true;
		}

		if (TryFindWalkableWorldPointNear(worldPoint, preferredWorldPoint, out Vector2 walkableWorldPoint))
		{
			Vector2 walkableLogicalPoint = WalkableWorldToLogicalPoint(walkableWorldPoint);

			if (TryBuildMovementPath(logicalPosition, walkableLogicalPoint, movementQueryPath))
			{
				destination = walkableLogicalPoint;
				return true;
			}
		}

		return false;
	}

	public bool TryEvaluateMovementAtScreenPoint(Vector2 screenPosition, bool clampToWalkableArea, out MovementTargetQuery movementQuery)
	{
		movementQuery = default;

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();

		if (!TryGetLogicalPointFromScreen(screenPosition, out Vector2 targetPosition))
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
		Vector2 visualPoint = LogicalToWalkableWorldPoint(logicalPoint);
		Vector3 visualPosition = new Vector3(visualPoint.x, visualPoint.y, transform.position.z);

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

		RefreshWalkableFloorForCurrentRoom();

		bool exactPointWalkable = IsPointWalkable(targetPosition);
		Vector2 destinationPosition = targetPosition;

		if (!exactPointWalkable && clampToWalkableArea)
		{
			destinationPosition = ClampToWalkableArea(targetPosition, logicalPosition);
		}

		if (clampToWalkableArea && !IsPointWalkable(destinationPosition))
		{
			destinationPosition = ClampToWalkableArea(destinationPosition, logicalPosition);
		}

		bool hasReachableDestination = TryBuildMovementPath(logicalPosition, destinationPosition, movementQueryPath);
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
		float depth = GetVisualWorldDepth(mainCamera);
		Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
		logicalPoint = WalkableWorldToLogicalPoint(worldPosition);
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

	private static bool IsPointerOverBlockingUi(Vector2 screenPosition)
	{
		EventSystem eventSystem = EventSystem.current;
		if (eventSystem == null)
			return false;

		PointerEventData pointerEventData = new PointerEventData(eventSystem)
		{
			position = screenPosition
		};

		uiRaycastResults.Clear();
		eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

		for (int i = 0; i < uiRaycastResults.Count; i++)
		{
			GameObject hitObject = uiRaycastResults[i].gameObject;

			if (hitObject == null || IsPassiveRoomUi(hitObject))
				continue;

			return true;
		}

		return false;
	}

	private static bool IsPassiveRoomUi(GameObject hitObject)
	{
		return hitObject.GetComponentInParent<DoorTriggerNavigation>() != null ||
			hitObject.GetComponentInParent<RoomContentGroup>() != null;
	}

	private void UpdateWalkCursor()
	{
		if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition))
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		if (Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(screenPosition))
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		if (DoorTriggerNavigation.IsPointerOverActiveTrigger(screenPosition) || IsPointerOverBlockingUi(screenPosition))
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, false, out MovementTargetQuery movementQuery))
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

		NavigationCursorController.SetWalkHover(
			this,
			true,
			movementQuery.ExactPointWalkable && movementQuery.HasReachableDestination);
	}

	private void SetDestination(Vector2 clickPosition)
	{
		finalDestination = clickPosition;
		movementPathIndex = 0;
		movementPath.Clear();

		if (!TryBuildMovementPath(logicalPosition, clickPosition, movementPath) || movementPath.Count == 0)
		{
			destination = logicalPosition;
			hasDestination = false;
			isWalking = false;
			return;
		}

		destination = movementPath[0];
		Vector2 movement = destination - logicalPosition;
		hasDestination = movement.magnitude > stopDistance;
		if (hasDestination)
			UpdateWalkDirection(movement);

		isWalking = hasDestination;
	}

	private void StopImmediatelyAt(Vector2 targetPosition)
	{
		logicalPosition = targetPosition;
		destination = targetPosition;
		finalDestination = targetPosition;
		movementPath.Clear();
		movementPathIndex = 0;
		hasDestination = false;
		isWalking = false;

		if (body != null)
		{
			body.linearVelocity = Vector2.zero;
			body.angularVelocity = 0f;
		}

		UpdateAnimator();
		ApplySpriteMirror();
		ApplyVisualPosition();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private void CancelDestination()
	{
		bool wasMoving = hasDestination || isWalking || movementPath.Count > 0;

		destination = logicalPosition;
		finalDestination = logicalPosition;
		movementPath.Clear();
		movementPathIndex = 0;
		hasDestination = false;
		isWalking = false;

		if (body != null)
		{
			body.linearVelocity = Vector2.zero;
			body.angularVelocity = 0f;
		}

		UpdateAnimator();
		ApplySpriteMirror();

		if (wasMoving)
		{
			MovementStopped?.Invoke();
		}
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

		logicalPosition = nextPosition;
		UpdateWalkDirection(movement);

		if (Vector2.Distance(nextPosition, destination) <= stopDistance)
		{
			logicalPosition = destination;

			if (TryAdvancePathWaypoint())
			{
				isWalking = true;
				UpdateWalkDirection(destination - logicalPosition);
			}
			else
			{
				hasDestination = false;
				isWalking = false;
				finalDestination = logicalPosition;
				movementPath.Clear();
				movementPathIndex = 0;
				ApplySpriteMirror();
				ArrivedAtDestination?.Invoke();
				MovementStopped?.Invoke();
			}
		}
		else
		{
			isWalking = true;
		}
	}

	private bool TryAdvancePathWaypoint()
	{
		movementPathIndex++;

		while (movementPathIndex < movementPath.Count)
		{
			destination = movementPath[movementPathIndex];

			if (Vector2.Distance(logicalPosition, destination) > stopDistance)
			{
				hasDestination = true;
				return true;
			}

			logicalPosition = destination;
			movementPathIndex++;
		}

		return false;
	}

	private void ApplyPerspectiveScale()
	{
		UpdateVisualOffset(Camera.main);
		float depth = Mathf.InverseLerp(nearY, farY, logicalPosition.y);
		float scale = Mathf.Lerp(nearScale, farScale, depth) * currentRoomStageScaleRatio;
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

		float verticalMultiplier = Mathf.Clamp(verticalMovementSpeedMultiplier, 0.25f, 1f);
		Vector2 scaledMovement = new Vector2(movement.x, movement.y / verticalMultiplier);
		float scaledDistance = scaledMovement.magnitude;

		if (scaledDistance <= maxDistance)
			return targetPosition;

		return currentPosition + movement * (maxDistance / scaledDistance);
	}

	private void ApplySpriteMirror()
	{
		if (spriteRenderer != null)
			spriteRenderer.flipX = false;
	}

	private void ApplyVisualPosition()
	{
		UpdateVisualOffset(Camera.main);
		Vector2 visualPoint = LogicalToWalkableWorldPoint(logicalPosition);
		Vector3 visualPosition = new Vector3(visualPoint.x, visualPoint.y, transform.position.z);

		transform.position = visualPosition;

		if (body != null)
			body.position = visualPosition;
	}

	private void UpdateVisualOffset(Camera mainCamera)
	{
		currentVisualOffset = Vector3.zero;
		currentRoomStageScaleRatio = 1f;

		if (cameraManager == null)
			cameraManager = FindAnyObjectByType<CameraManager>();

		if (navigationManager == null)
			navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);

		if (cameraManager == null || mainCamera == null)
		{
			hasRoomStageVisualReference = false;
			return;
		}

		float depth = GetVisualWorldDepth(mainCamera);

		if (cameraManager.TryGetActiveRoomStageWorldPoint(
			Vector2.zero,
			depth,
			out Vector3 stageWorldCenter,
			out float stageScale))
		{
			string currentRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;

			if (!hasRoomStageVisualReference ||
				!SameRoomName(roomStageVisualReferenceRoom, currentRoom))
			{
				roomStageReferenceWorldCenter = stageWorldCenter;
				roomStageReferenceScale = Mathf.Max(0.0001f, stageScale);
				roomStageVisualReferenceRoom = currentRoom;
				hasRoomStageVisualReference = true;
			}

			currentRoomStageWorldCenter = stageWorldCenter;
			currentRoomStageScaleRatio = stageScale / Mathf.Max(0.0001f, roomStageReferenceScale);
			currentVisualOffset = currentRoomStageWorldCenter - roomStageReferenceWorldCenter;
			currentVisualOffset.z = 0f;
			return;
		}

		hasRoomStageVisualReference = false;

		if (cameraManager.TryGetRoomStageWorldOffset(mainCamera, out Vector3 roomOffset))
			currentVisualOffset = roomOffset;
	}

	private void ResetRoomStageVisualReference()
	{
		hasRoomStageVisualReference = false;
		roomStageVisualReferenceRoom = string.Empty;
		roomStageReferenceWorldCenter = Vector3.zero;
		currentRoomStageWorldCenter = Vector3.zero;
		roomStageReferenceScale = 1f;
		currentRoomStageScaleRatio = 1f;
		currentVisualOffset = Vector3.zero;
	}

	private float GetVisualWorldDepth(Camera mainCamera)
	{
		if (mainCamera == null)
			return 10f;

		float depth = transform.position.z - mainCamera.transform.position.z;

		if (depth <= 0.01f)
			depth = Mathf.Abs(depth);

		return depth > 0.01f ? depth : 10f;
	}

	private bool IsPointWalkable(Vector2 point)
	{
		UpdateVisualOffset(Camera.main);
		return walkableFloor == null || walkableFloor.OverlapPoint(LogicalToWalkableWorldPoint(point));
	}

	private bool TryBuildMovementPath(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		path.Clear();

		if (!IsPointWalkable(targetPosition))
		{
			return false;
		}

		path.Add(targetPosition);
		return true;
	}

	private Vector2 LogicalToWalkableWorldPoint(Vector2 logicalPoint)
	{
		if (hasRoomStageVisualReference)
		{
			Vector2 referenceOffset = logicalPoint - (Vector2)roomStageReferenceWorldCenter;
			return (Vector2)currentRoomStageWorldCenter + referenceOffset * currentRoomStageScaleRatio;
		}

		return logicalPoint + new Vector2(currentVisualOffset.x, currentVisualOffset.y);
	}

	private Vector2 WalkableWorldToLogicalPoint(Vector2 worldPoint)
	{
		if (hasRoomStageVisualReference)
		{
			float safeScaleRatio = Mathf.Max(0.0001f, currentRoomStageScaleRatio);
			Vector2 currentOffset = worldPoint - (Vector2)currentRoomStageWorldCenter;
			return (Vector2)roomStageReferenceWorldCenter + currentOffset / safeScaleRatio;
		}

		return worldPoint - new Vector2(currentVisualOffset.x, currentVisualOffset.y);
	}

	private Vector2 ClampToWalkableArea(Vector2 point)
	{
		return ClampToWalkableArea(point, logicalPosition);
	}

	private Vector2 ClampToWalkableArea(Vector2 point, Vector2 preferredInsidePoint)
	{
		if (walkableFloor == null)
			return point;

		UpdateVisualOffset(Camera.main);
		Vector2 worldPoint = LogicalToWalkableWorldPoint(point);
		if (walkableFloor.OverlapPoint(worldPoint))
			return point;

		Vector2 closestPoint = walkableFloor.ClosestPoint(worldPoint);
		if (walkableFloor.OverlapPoint(closestPoint))
			return WalkableWorldToLogicalPoint(closestPoint);

		Vector2 preferredWorldPoint = LogicalToWalkableWorldPoint(preferredInsidePoint);
		Vector2 insetDirection = preferredWorldPoint - closestPoint;
		if (insetDirection.sqrMagnitude <= MovementEpsilon || !walkableFloor.OverlapPoint(preferredWorldPoint))
			insetDirection = (Vector2)walkableFloor.bounds.center - closestPoint;

		if (insetDirection.sqrMagnitude <= MovementEpsilon)
			return WalkableWorldToLogicalPoint(closestPoint);

		insetDirection.Normalize();

		for (int i = 1; i <= WalkableInsetAttempts; i++)
		{
			Vector2 insetPoint = closestPoint + insetDirection * (WalkableInsetStep * i);
			if (walkableFloor.OverlapPoint(insetPoint))
				return WalkableWorldToLogicalPoint(insetPoint);
		}

		for (int i = 1; i <= WalkableInsetAttempts; i++)
		{
			float radius = WalkableInsetStep * i;

			for (int sample = 0; sample < WalkableInsetRadialSamples; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / WalkableInsetRadialSamples;
				Vector2 radialPoint = closestPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

				if (walkableFloor.OverlapPoint(radialPoint))
					return WalkableWorldToLogicalPoint(radialPoint);
			}
		}

		if (TryFindWalkableWorldPointNear(worldPoint, preferredWorldPoint, out Vector2 searchedPoint))
			return WalkableWorldToLogicalPoint(searchedPoint);

		return WalkableWorldToLogicalPoint(closestPoint);
	}

	private bool TryFindWalkableWorldPointNear(Vector2 targetWorldPoint, Vector2 preferredWorldPoint, out Vector2 walkableWorldPoint)
	{
		walkableWorldPoint = Vector2.zero;

		if (walkableFloor == null)
		{
			walkableWorldPoint = targetWorldPoint;
			return true;
		}

		if (walkableFloor.OverlapPoint(targetWorldPoint))
		{
			walkableWorldPoint = targetWorldPoint;
			return true;
		}

		Vector2 closestPoint = walkableFloor.ClosestPoint(targetWorldPoint);
		bool foundPoint = false;
		float bestSqrDistance = float.MaxValue;
		Vector2 bestPoint = Vector2.zero;

		TryAcceptWalkableWorldPoint(closestPoint, targetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);

		Vector2 boundsCenter = walkableFloor.bounds.center;
		TrySearchTowardWalkablePoint(closestPoint, preferredWorldPoint, targetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);
		TrySearchTowardWalkablePoint(closestPoint, boundsCenter, targetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);
		TryAcceptWalkableWorldPoint(boundsCenter, targetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);

		Bounds bounds = walkableFloor.bounds;
		float minExtent = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.y));
		float step = Mathf.Max(WalkableInsetStep, minExtent / WalkableSearchRings);

		for (int ring = 1; ring <= WalkableSearchRings; ring++)
		{
			float radius = step * ring;

			for (int sample = 0; sample < WalkableSearchSamplesPerRing; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / WalkableSearchSamplesPerRing;
				Vector2 samplePoint = closestPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
				TryAcceptWalkableWorldPoint(samplePoint, targetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);
			}

			if (foundPoint)
			{
				walkableWorldPoint = bestPoint;
				return true;
			}
		}

		if (!foundPoint)
			return false;

		walkableWorldPoint = bestPoint;
		return true;
	}

	private void TrySearchTowardWalkablePoint(
		Vector2 startWorldPoint,
		Vector2 directionTargetWorldPoint,
		Vector2 distanceTargetWorldPoint,
		ref bool foundPoint,
		ref float bestSqrDistance,
		ref Vector2 bestPoint)
	{
		Vector2 direction = directionTargetWorldPoint - startWorldPoint;

		if (direction.sqrMagnitude <= MovementEpsilon)
			return;

		for (int i = 1; i <= WalkableSearchRings; i++)
		{
			float t = i / (float)WalkableSearchRings;
			Vector2 samplePoint = Vector2.Lerp(startWorldPoint, directionTargetWorldPoint, t);
			TryAcceptWalkableWorldPoint(samplePoint, distanceTargetWorldPoint, ref foundPoint, ref bestSqrDistance, ref bestPoint);
		}
	}

	private void TryAcceptWalkableWorldPoint(
		Vector2 candidateWorldPoint,
		Vector2 targetWorldPoint,
		ref bool foundPoint,
		ref float bestSqrDistance,
		ref Vector2 bestPoint)
	{
		if (!walkableFloor.OverlapPoint(candidateWorldPoint))
			return;

		float sqrDistance = (candidateWorldPoint - targetWorldPoint).sqrMagnitude;

		if (foundPoint && sqrDistance >= bestSqrDistance)
			return;

		foundPoint = true;
		bestSqrDistance = sqrDistance;
		bestPoint = candidateWorldPoint;
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

	private bool TryFindPlayerBoundaryForRoom(string roomName, out Collider2D boundary)
	{
		boundary = null;

		if (string.IsNullOrWhiteSpace(roomName))
			return false;

#if UNITY_2023_1_OR_NEWER
		RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);
#else
		RoomContentGroup[] rooms = FindObjectsOfType<RoomContentGroup>(true);
#endif

		for (int i = 0; i < rooms.Length; i++)
		{
			RoomContentGroup room = rooms[i];
			if (room == null || !SameRoomName(room.RoomName, roomName))
				continue;

			return TryFindPlayerBoundaryInRoom(room, roomName, out boundary);
		}

		return false;
	}

	private bool TryFindPlayerBoundaryInRoom(RoomContentGroup room, string roomName, out Collider2D boundary)
	{
		boundary = null;

		if (room == null)
			return false;

		Collider2D fallback = null;
		Collider2D[] colliders = room.GetComponentsInChildren<Collider2D>(true);
		string prefixKey = NormalizeBoundaryName(roomBoundaryNamePrefix);
		string roomKey = NormalizeBoundaryName(roomName);
		string firstRoomWordKey = NormalizeBoundaryName(GetFirstWord(roomName));

		for (int i = 0; i < colliders.Length; i++)
		{
			Collider2D candidate = colliders[i];
			if (candidate == null || !candidate.enabled)
				continue;

			string candidateKey = NormalizeBoundaryName(candidate.name);
			if (!candidateKey.StartsWith(prefixKey, StringComparison.OrdinalIgnoreCase))
				continue;

			if (fallback == null)
				fallback = candidate;

			if (candidateKey == prefixKey ||
				candidateKey == prefixKey + roomKey ||
				(!string.IsNullOrEmpty(firstRoomWordKey) && candidateKey == prefixKey + firstRoomWordKey))
			{
				boundary = candidate;
				return true;
			}
		}

		boundary = fallback;
		return boundary != null;
	}

	private static bool SameRoomName(string first, string second)
	{
		return string.Equals(NormalizeBoundaryName(first), NormalizeBoundaryName(second), StringComparison.OrdinalIgnoreCase);
	}

	private static string CleanRoomName(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
	}

	private static string GetFirstWord(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		string[] words = value.Trim().Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
		return words.Length > 0 ? words[0] : string.Empty;
	}

	private static string NormalizeBoundaryName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		char[] normalized = new char[value.Length];
		int count = 0;
		for (int i = 0; i < value.Length; i++)
		{
			char character = value[i];
			if (!char.IsLetterOrDigit(character))
				continue;

			normalized[count] = char.ToLowerInvariant(character);
			count++;
		}

		return new string(normalized, 0, count);
	}

	private void ApplyPlayerSorting()
	{
		if (spriteRenderers == null || spriteRenderers.Length == 0)
			return;

		string sortingLayerName = GetSortingLayerName(playerSortingLayerName);
		float sortingY = GetPlayerSortingY();
		int sortingOrder = playerSortingOrderBase - Mathf.RoundToInt(sortingY * playerSortingOrderPerYUnit);
		currentSortingOrder = sortingOrder;

		for (int i = 0; i < spriteRenderers.Length; i++)
		{
			SpriteRenderer targetRenderer = spriteRenderers[i];
			if (targetRenderer == null)
				continue;

			targetRenderer.sortingLayerName = sortingLayerName;
			targetRenderer.sortingOrder = sortingOrder;
		}
	}

	private float GetPlayerSortingY()
	{
		float sortingY = logicalPosition.y;

		if (sortPlayerByVisibleFeet)
		{
			bool foundRendererBounds = false;
			float lowestVisibleY = float.PositiveInfinity;

			for (int i = 0; i < spriteRenderers.Length; i++)
			{
				SpriteRenderer targetRenderer = spriteRenderers[i];

				if (targetRenderer == null || !targetRenderer.enabled || targetRenderer.sprite == null)
				{
					continue;
				}

				lowestVisibleY = Mathf.Min(lowestVisibleY, targetRenderer.bounds.min.y);
				foundRendererBounds = true;
			}

			if (foundRendererBounds)
			{
				sortingY = lowestVisibleY;
			}
		}

		return sortingY + playerSortingYOffset;
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
