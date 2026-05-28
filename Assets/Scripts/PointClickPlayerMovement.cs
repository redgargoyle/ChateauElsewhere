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
	private const float MovementEpsilon = 0.0001f;
	private const float WalkableInsetStep = 0.015f;
	private const int WalkableInsetAttempts = 12;

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
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private string currentWalkableBoundaryRoom;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;
	private int currentSortingOrder;
	private int movementPathIndex;
	private readonly List<Vector2> movementPath = new List<Vector2>();
	private readonly List<Vector2> movementQueryPath = new List<Vector2>();

	public event Action ArrivedAtDestination;
	public event Action MovementStopped;
	public Vector2 LogicalPosition => logicalPosition;
	public bool HasDestination => hasDestination;
	public int CurrentSortingOrder => currentSortingOrder;

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

	private bool TryGetFloorClick(out Vector2 clickPosition)
	{
		clickPosition = Vector2.zero;

		if (!TryGetPrimaryPointerDown(out Vector2 screenPosition))
			return false;

		if (IsPointerOverUi())
			return false;

		if (!TryEvaluateMovementAtScreenPoint(screenPosition, false, out MovementTargetQuery movementQuery))
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

	private void UpdateWalkCursor()
	{
		if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition) || IsPointerOverUi())
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
		return logicalPoint + new Vector2(currentVisualOffset.x, currentVisualOffset.y);
	}

	private Vector2 WalkableWorldToLogicalPoint(Vector2 worldPoint)
	{
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

		return WalkableWorldToLogicalPoint(closestPoint);
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
