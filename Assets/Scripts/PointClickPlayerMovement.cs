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
	private const float PathNodeMergeDistance = 0.02f;
	private const float PathStraightnessBias = 0.08f;

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
	[SerializeField] private bool sortPlayerByVisibleFeet = true;
	[SerializeField] private float playerSortingYOffset;
	[SerializeField] private bool avoidSolidObstacleFootprints = true;
	[SerializeField] [Min(0f)] private float movementObstaclePadding = 0.02f;
	[SerializeField] [Min(0f)] private float pathCornerPadding = 0.06f;
	[SerializeField] [Min(0.03f)] private float pathProbeStep = 0.08f;

	private Rigidbody2D body;
	private Animator animator;
	private SpriteRenderer spriteRenderer;
	private SpriteRenderer[] spriteRenderers;
	private CameraManager cameraManager;
	private Vector2 destination;
	private Vector2 finalDestination;
	private Vector2 logicalPosition;
	private Vector3 currentVisualOffset;
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;
	private int currentSortingOrder;
	private int movementPathIndex;
	private readonly List<Vector2> movementPath = new List<Vector2>();
	private readonly List<Vector2> movementQueryPath = new List<Vector2>();
	private readonly List<Bounds> navigationObstacleBounds = new List<Bounds>();
	private readonly List<Vector2> navigationNodes = new List<Vector2>();
	private readonly List<int> reversePath = new List<int>();

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
		bool exactPointWalkable = !blockedByPickup &&
			IsPointWalkable(targetPosition) &&
			!IsMovementPointBlocked(targetPosition);
		Vector2 destinationPosition = targetPosition;

		if (!exactPointWalkable && clampToWalkableArea && !blockedByPickup)
		{
			destinationPosition = ClampToWalkableArea(targetPosition, logicalPosition);
		}

		if (clampToWalkableArea && !IsPointWalkable(destinationPosition))
		{
			destinationPosition = ClampToWalkableArea(destinationPosition, logicalPosition);
		}

		bool hasReachableDestination = !blockedByPickup &&
			TryBuildMovementPath(logicalPosition, destinationPosition, movementQueryPath);
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

		if (!IsPointWalkable(nextPosition) || IsMovementSegmentBlocked(currentPosition, nextPosition))
		{
			if (TryRestartPathFrom(currentPosition))
			{
				isWalking = true;
				return;
			}

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

	private bool TryRestartPathFrom(Vector2 currentPosition)
	{
		movementPathIndex = 0;
		movementPath.Clear();

		if (!TryBuildMovementPath(currentPosition, finalDestination, movementPath) || movementPath.Count == 0)
		{
			return false;
		}

		destination = movementPath[0];
		UpdateWalkDirection(destination - currentPosition);
		return true;
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
		return walkableFloor == null || walkableFloor.OverlapPoint(point);
	}

	private bool TryBuildMovementPath(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		path.Clear();
		CollectMovementObstacleBounds(navigationObstacleBounds);

		if (!IsPointWalkable(targetPosition) || IsMovementPointBlocked(targetPosition, navigationObstacleBounds))
		{
			return false;
		}

		if ((!avoidSolidObstacleFootprints || navigationObstacleBounds.Count == 0) &&
			IsWalkableSegment(startPosition, targetPosition))
		{
			path.Add(targetPosition);
			return true;
		}

		if (IsNavigationSegmentClear(startPosition, targetPosition, navigationObstacleBounds))
		{
			path.Add(targetPosition);
			return true;
		}

		navigationNodes.Clear();
		navigationNodes.Add(startPosition);
		navigationNodes.Add(targetPosition);
		AddObstacleNavigationNodes(navigationObstacleBounds, navigationNodes);

		return TryFindPathThroughNodes(targetPosition, path);
	}

	private bool TryFindPathThroughNodes(Vector2 targetPosition, List<Vector2> path)
	{
		int nodeCount = navigationNodes.Count;
		if (nodeCount < 2)
		{
			return false;
		}

		float[] pathCost = new float[nodeCount];
		float[] estimatedCost = new float[nodeCount];
		int[] previousNode = new int[nodeCount];
		bool[] visited = new bool[nodeCount];

		for (int i = 0; i < nodeCount; i++)
		{
			pathCost[i] = float.PositiveInfinity;
			estimatedCost[i] = float.PositiveInfinity;
			previousNode[i] = -1;
		}

		pathCost[0] = 0f;
		estimatedCost[0] = Vector2.Distance(navigationNodes[0], targetPosition);

		for (int step = 0; step < nodeCount; step++)
		{
			int currentNode = FindBestOpenPathNode(estimatedCost, visited);
			if (currentNode < 0)
			{
				break;
			}

			if (currentNode == 1)
			{
				break;
			}

			visited[currentNode] = true;

			for (int neighborNode = 0; neighborNode < nodeCount; neighborNode++)
			{
				if (neighborNode == currentNode || visited[neighborNode])
				{
					continue;
				}

				Vector2 currentPoint = navigationNodes[currentNode];
				Vector2 neighborPoint = navigationNodes[neighborNode];

				if (!IsNavigationSegmentClear(currentPoint, neighborPoint, navigationObstacleBounds))
				{
					continue;
				}

				float straightnessPenalty = DistancePointToSegment(neighborPoint, navigationNodes[0], targetPosition) * PathStraightnessBias;
				float candidateCost = pathCost[currentNode] + Vector2.Distance(currentPoint, neighborPoint) + straightnessPenalty;
				if (candidateCost + MovementEpsilon >= pathCost[neighborNode])
				{
					continue;
				}

				previousNode[neighborNode] = currentNode;
				pathCost[neighborNode] = candidateCost;
				estimatedCost[neighborNode] = candidateCost + Vector2.Distance(neighborPoint, targetPosition);
			}
		}

		if (previousNode[1] < 0)
		{
			return false;
		}

		reversePath.Clear();
		for (int node = 1; node > 0; node = previousNode[node])
		{
			reversePath.Add(node);
		}

		for (int i = reversePath.Count - 1; i >= 0; i--)
		{
			path.Add(navigationNodes[reversePath[i]]);
		}

		return path.Count > 0;
	}

	private static int FindBestOpenPathNode(float[] estimatedCost, bool[] visited)
	{
		int bestNode = -1;
		float bestCost = float.PositiveInfinity;

		for (int i = 0; i < estimatedCost.Length; i++)
		{
			if (visited[i] || estimatedCost[i] >= bestCost)
			{
				continue;
			}

			bestNode = i;
			bestCost = estimatedCost[i];
		}

		return bestNode;
	}

	private void AddObstacleNavigationNodes(List<Bounds> obstacles, List<Vector2> nodes)
	{
		AddObstacleEdgeNodes(obstacles, nodes);
		AddObstacleGapNodes(obstacles, nodes);
	}

	private void AddObstacleEdgeNodes(List<Bounds> obstacles, List<Vector2> nodes)
	{
		float cornerPadding = Mathf.Max(0.01f, pathCornerPadding);

		for (int i = 0; i < obstacles.Count; i++)
		{
			Bounds bounds = obstacles[i];

			AddNavigationNode(nodes, new Vector2(bounds.min.x - cornerPadding, bounds.min.y - cornerPadding), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.min.x - cornerPadding, bounds.max.y + cornerPadding), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.max.x + cornerPadding, bounds.min.y - cornerPadding), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.max.x + cornerPadding, bounds.max.y + cornerPadding), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.min.x - cornerPadding, bounds.center.y), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.max.x + cornerPadding, bounds.center.y), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.center.x, bounds.min.y - cornerPadding), obstacles);
			AddNavigationNode(nodes, new Vector2(bounds.center.x, bounds.max.y + cornerPadding), obstacles);
		}
	}

	private void AddObstacleGapNodes(List<Bounds> obstacles, List<Vector2> nodes)
	{
		for (int i = 0; i < obstacles.Count; i++)
		{
			for (int j = i + 1; j < obstacles.Count; j++)
			{
				AddHorizontalGapNode(obstacles[i], obstacles[j], nodes, obstacles);
				AddVerticalGapNode(obstacles[i], obstacles[j], nodes, obstacles);
			}
		}
	}

	private void AddHorizontalGapNode(Bounds first, Bounds second, List<Vector2> nodes, List<Bounds> obstacles)
	{
		Bounds left = first.center.x <= second.center.x ? first : second;
		Bounds right = first.center.x <= second.center.x ? second : first;
		float gapPadding = Mathf.Max(0.005f, Mathf.Min(0.04f, pathCornerPadding * 0.5f));
		float gapMin = left.max.x + gapPadding;
		float gapMax = right.min.x - gapPadding;

		if (gapMin > gapMax)
		{
			return;
		}

		float yMin = Mathf.Max(left.min.y, right.min.y);
		float yMax = Mathf.Min(left.max.y, right.max.y);
		float y = yMin <= yMax
			? (yMin + yMax) * 0.5f
			: (left.center.y + right.center.y) * 0.5f;

		AddNavigationNode(nodes, new Vector2((gapMin + gapMax) * 0.5f, y), obstacles);
	}

	private void AddVerticalGapNode(Bounds first, Bounds second, List<Vector2> nodes, List<Bounds> obstacles)
	{
		Bounds lower = first.center.y <= second.center.y ? first : second;
		Bounds upper = first.center.y <= second.center.y ? second : first;
		float gapPadding = Mathf.Max(0.005f, Mathf.Min(0.04f, pathCornerPadding * 0.5f));
		float gapMin = lower.max.y + gapPadding;
		float gapMax = upper.min.y - gapPadding;

		if (gapMin > gapMax)
		{
			return;
		}

		float xMin = Mathf.Max(lower.min.x, upper.min.x);
		float xMax = Mathf.Min(lower.max.x, upper.max.x);
		float x = xMin <= xMax
			? (xMin + xMax) * 0.5f
			: (lower.center.x + upper.center.x) * 0.5f;

		AddNavigationNode(nodes, new Vector2(x, (gapMin + gapMax) * 0.5f), obstacles);
	}

	private void AddNavigationNode(List<Vector2> nodes, Vector2 candidate, List<Bounds> obstacles)
	{
		if (!IsNavigationPointUsable(candidate, obstacles))
		{
			return;
		}

		float mergeDistanceSquared = PathNodeMergeDistance * PathNodeMergeDistance;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (Vector2.SqrMagnitude(nodes[i] - candidate) <= mergeDistanceSquared)
			{
				return;
			}
		}

		nodes.Add(candidate);
	}

	private bool IsNavigationPointUsable(Vector2 point, List<Bounds> obstacles)
	{
		return IsPointWalkable(point) && !IsMovementPointBlocked(point, obstacles);
	}

	private bool IsMovementPointBlocked(Vector2 point)
	{
		CollectMovementObstacleBounds(navigationObstacleBounds);
		return IsMovementPointBlocked(point, navigationObstacleBounds);
	}

	private bool IsMovementSegmentBlocked(Vector2 startPosition, Vector2 endPosition)
	{
		if (!avoidSolidObstacleFootprints)
		{
			return false;
		}

		CollectMovementObstacleBounds(navigationObstacleBounds);
		return IsSegmentBlockedByObstacle(startPosition, endPosition, navigationObstacleBounds);
	}

	private void CollectMovementObstacleBounds(List<Bounds> bounds)
	{
		bounds.Clear();
		UpdateVisualOffset(Camera.main);

		if (!avoidSolidObstacleFootprints)
		{
			return;
		}

#if UNITY_2023_1_OR_NEWER
		YSortSolidObstacle2D[] obstacles = FindObjectsByType<YSortSolidObstacle2D>(FindObjectsInactive.Exclude);
#else
		YSortSolidObstacle2D[] obstacles = FindObjectsOfType<YSortSolidObstacle2D>();
#endif

		Vector3 logicalOffset = new Vector3(currentVisualOffset.x, currentVisualOffset.y, 0f);

		for (int i = 0; i < obstacles.Length; i++)
		{
			YSortSolidObstacle2D obstacle = obstacles[i];

			if (obstacle == null || !obstacle.TryGetMovementBounds(movementObstaclePadding, out Bounds obstacleBounds))
			{
				continue;
			}

			obstacleBounds.center -= logicalOffset;
			bounds.Add(obstacleBounds);
		}
	}

	private bool IsNavigationSegmentClear(Vector2 startPosition, Vector2 endPosition, List<Bounds> obstacles)
	{
		return IsWalkableSegment(startPosition, endPosition) &&
			!IsSegmentBlockedByObstacle(startPosition, endPosition, obstacles);
	}

	private bool IsWalkableSegment(Vector2 startPosition, Vector2 endPosition)
	{
		float distance = Vector2.Distance(startPosition, endPosition);
		int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.03f, pathProbeStep)));

		for (int i = 1; i <= steps; i++)
		{
			Vector2 samplePoint = Vector2.Lerp(startPosition, endPosition, i / (float)steps);
			if (!IsPointWalkable(samplePoint))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsMovementPointBlocked(Vector2 point, List<Bounds> obstacles)
	{
		for (int i = 0; i < obstacles.Count; i++)
		{
			if (BoundsContainsPoint2D(obstacles[i], point))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsSegmentBlockedByObstacle(Vector2 startPosition, Vector2 endPosition, List<Bounds> obstacles)
	{
		for (int i = 0; i < obstacles.Count; i++)
		{
			Bounds bounds = obstacles[i];

			if (BoundsContainsPoint2D(bounds, endPosition))
			{
				return true;
			}

			if (BoundsContainsPoint2D(bounds, startPosition))
			{
				continue;
			}

			if (SegmentIntersectsBounds2D(startPosition, endPosition, bounds))
			{
				return true;
			}
		}

		return false;
	}

	private static bool SegmentIntersectsBounds2D(Vector2 startPosition, Vector2 endPosition, Bounds bounds)
	{
		float tMin = 0f;
		float tMax = 1f;
		Vector2 delta = endPosition - startPosition;

		return ClipSegmentAxis(startPosition.x, delta.x, bounds.min.x, bounds.max.x, ref tMin, ref tMax) &&
			ClipSegmentAxis(startPosition.y, delta.y, bounds.min.y, bounds.max.y, ref tMin, ref tMax);
	}

	private static bool ClipSegmentAxis(float start, float delta, float min, float max, ref float tMin, ref float tMax)
	{
		if (Mathf.Abs(delta) <= MovementEpsilon)
		{
			return start >= min && start <= max;
		}

		float inverseDelta = 1f / delta;
		float axisMin = (min - start) * inverseDelta;
		float axisMax = (max - start) * inverseDelta;

		if (axisMin > axisMax)
		{
			float swap = axisMin;
			axisMin = axisMax;
			axisMax = swap;
		}

		tMin = Mathf.Max(tMin, axisMin);
		tMax = Mathf.Min(tMax, axisMax);
		return tMin <= tMax;
	}

	private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
	{
		Vector2 segment = segmentEnd - segmentStart;
		float lengthSquared = segment.sqrMagnitude;

		if (lengthSquared <= MovementEpsilon)
		{
			return Vector2.Distance(point, segmentStart);
		}

		float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
		Vector2 closestPoint = segmentStart + segment * t;
		return Vector2.Distance(point, closestPoint);
	}

	private static bool BoundsContainsPoint2D(Bounds bounds, Vector2 point)
	{
		return point.x >= bounds.min.x &&
			point.x <= bounds.max.x &&
			point.y >= bounds.min.y &&
			point.y <= bounds.max.y;
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
