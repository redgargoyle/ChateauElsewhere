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
	private const int MinPathSegmentProbeSamples = 4;
	private const int MaxPathSegmentProbeSamples = 192;
	private const int PathNodeRadialSamples = 16;
	private const int PathNodeRadialRings = 4;
	private const int GridRouteMaxNodeCount = 8192;
	private const int GridRouteClosestSpecialConnections = 8;
	private const float GridRouteSpecialConnectionMultiplier = 2.5f;
	private const int ClickProjectionSearchRings = 5;
	private const int ClickProjectionSearchSamplesPerRing = 16;
	private const float ClickProjectionMinWorldDistance = 16f;
	private const float ClickProjectionMaxWorldDistance = 48f;

	[SerializeField] private string walkableFloorName = "PlayerBoundary_Entrance";
	[SerializeField] private Collider2D walkableFloor;
	[SerializeField] private bool useCurrentRoomBoundary = true;
	[SerializeField] private string roomBoundaryNamePrefix = "PlayerBoundary";
	[SerializeField] private string roomBoundaryBlockerNamePrefix = "PlayerBlocker";
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
	[SerializeField] private bool useRoomPerspectiveProfileScale = true;
	[SerializeField] private bool useButlerRoomScaleOverrides = true;
	[SerializeField, HideInInspector] private string editorSelectedButlerScaleRoomId = string.Empty;
	[SerializeField, HideInInspector] private List<ButlerRoomScaleOverride> butlerRoomScaleOverrides = new List<ButlerRoomScaleOverride>();
	[SerializeField] private bool applyPerspectiveScale = true;
	[SerializeField] private bool applyPlayerSorting = true;
	[SerializeField] private float runningAnimationSpeed = 40f;
	[SerializeField] private bool disablePlatformMovement = true;
	[SerializeField] private bool sortPlayerByVisibleFeet = true;
	[SerializeField] private float playerSortingYOffset;

	private Rigidbody2D body;
	private Animator animator;
	private SpriteRenderer spriteRenderer;
	private SpriteRenderer[] spriteRenderers;
	private PlayerFootstepAudio footstepAudio;
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
	private float authoredPerspectiveScaleReference = 1f;
	private RoomPerspectiveProfile currentRoomPerspectiveProfile;
	private string currentRoomPerspectiveProfileRoom;
	private CharacterWalkDirection walkDirection = CharacterWalkDirection.Right;
	private CharacterAnimatorDriver.ParameterCache animatorParameters;
	private string currentWalkableBoundaryRoom;
	private string roomStageVisualReferenceRoom;
	private bool hasRoomStageVisualReference;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;
	private bool inputEnabled = true;
	private bool hasAuthoredLocalScale;
	private bool hasAuthoredRendererSorting;
	private Vector3 authoredLocalScale = Vector3.one;
	private AuthoredRendererSorting[] authoredRendererSorting = Array.Empty<AuthoredRendererSorting>();
	private int currentSortingOrder;
	private int movementPathIndex;
	private readonly List<Vector2> movementPath = new List<Vector2>();
	private readonly List<Vector2> movementQueryPath = new List<Vector2>();
	private readonly List<Vector2> pathWorldNodes = new List<Vector2>();
	private readonly List<float> pathNodeDistances = new List<float>();
	private readonly List<int> pathPreviousNodeIndices = new List<int>();
	private readonly List<bool> pathVisitedNodes = new List<bool>();
	private readonly List<Vector2> polygonRouteLocalNodes = new List<Vector2>();
	private readonly List<PolygonRouteConnection> polygonRouteConnections = new List<PolygonRouteConnection>();
	private readonly List<int> polygonRouteStartConnections = new List<int>();
	private readonly List<int> polygonRouteTargetConnections = new List<int>();
	private readonly List<int> polygonRouteIndices = new List<int>();
	private readonly List<Collider2D> walkableBlockers = new List<Collider2D>();
	private readonly List<Vector2> gridRouteWorldNodes = new List<Vector2>();
	private readonly List<int> gridRouteCellIndices = new List<int>();
	private readonly List<int> gridRouteOpenNodes = new List<int>();
	private readonly List<Vector2> gridRouteSmoothedPath = new List<Vector2>();
	private PolygonCollider2D cachedPolygonRouteCollider;
	private int cachedPolygonRouteShapeHash;
	private bool polygonRouteGraphValid;
	private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

	public event Action ArrivedAtDestination;
	public event Action MovementStopped;
	public Vector2 LogicalPosition => logicalPosition;
	public bool HasDestination => hasDestination;
	public int CurrentSortingOrder => currentSortingOrder;
	public bool InputEnabled => inputEnabled;
	public bool AppliesPerspectiveScale => applyPerspectiveScale;
	public bool AppliesPlayerSorting => applyPlayerSorting;
	public bool UsesButlerRoomScaleOverrides => useButlerRoomScaleOverrides;
	public string EditorSelectedButlerScaleRoomId => editorSelectedButlerScaleRoomId;
	public string CurrentButlerScaleRoomId => GetCurrentButlerScaleRoomId();

	public readonly struct ButlerRoomScaleOverrideData
	{
		public ButlerRoomScaleOverrideData(
			string roomId,
			bool hasFront,
			float frontFootY,
			float frontScale,
			bool hasBack,
			float backFootY,
			float backScale)
		{
			RoomId = CleanRoomName(roomId);
			HasFront = hasFront;
			FrontFootY = frontFootY;
			FrontScale = SanitizeButlerScale(frontScale);
			HasBack = hasBack;
			BackFootY = backFootY;
			BackScale = SanitizeButlerScale(backScale);
		}

		public string RoomId { get; }
		public bool HasFront { get; }
		public float FrontFootY { get; }
		public float FrontScale { get; }
		public bool HasBack { get; }
		public float BackFootY { get; }
		public float BackScale { get; }
		public bool IsComplete => HasFront && HasBack;
	}

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

	public void RefreshPerspectiveScaleNow(bool refreshLogicalPositionFromTransform = false)
	{
		CacheReferences();
		CaptureAuthoredLocalScaleIfNeeded();

		if (refreshLogicalPositionFromTransform || !Application.isPlaying)
		{
			UpdateVisualOffset(Camera.main);
			logicalPosition = WalkableWorldToLogicalPoint(GetCurrentVisibleMovementWorldPoint());
		}

		ApplyPerspectiveScale();
	}

	public bool UsesPerspectiveProfile(RoomPerspectiveProfile profile)
	{
		return profile != null &&
			TryGetCurrentRoomPerspectiveProfile(out RoomPerspectiveProfile currentProfile) &&
			currentProfile == profile;
	}

	public void SetEditorSelectedButlerScaleRoomId(string roomId)
	{
		editorSelectedButlerScaleRoomId = CleanRoomName(roomId);
	}

	public bool TryGetButlerRoomScaleOverride(string roomId, out ButlerRoomScaleOverrideData data)
	{
		data = default;
		int existingIndex = GetButlerScaleOverrideIndex(roomId);

		if (existingIndex < 0)
		{
			return false;
		}

		data = butlerRoomScaleOverrides[existingIndex].ToData();
		return true;
	}

	public bool HasCompleteButlerRoomScaleOverride(string roomId)
	{
		return TryGetButlerRoomScaleOverride(roomId, out ButlerRoomScaleOverrideData data) && data.IsComplete;
	}

	public float GetButlerScaleForRoomAtY(string roomId, float roomLocalFootY, float fallbackScale)
	{
		if (!TryGetButlerRoomScaleOverride(roomId, out ButlerRoomScaleOverrideData data) || !data.IsComplete)
		{
			return SanitizeButlerScale(fallbackScale);
		}

		if (Mathf.Approximately(data.FrontFootY, data.BackFootY))
		{
			return data.FrontScale;
		}

		float depth = Mathf.Clamp01(Mathf.InverseLerp(data.FrontFootY, data.BackFootY, roomLocalFootY));
		return SanitizeButlerScale(Mathf.Lerp(data.FrontScale, data.BackScale, depth));
	}

	public void SetButlerFrontScaleForRoom(string roomId, float roomLocalFootY, float scale, bool applyImmediately = true)
	{
		ButlerRoomScaleOverride roomScale = GetOrCreateButlerScaleOverride(roomId);

		if (roomScale == null)
		{
			return;
		}

		roomScale.SetFront(roomLocalFootY, scale);
		useButlerRoomScaleOverrides = true;
		SetEditorSelectedButlerScaleRoomId(roomScale.RoomId);

		if (applyImmediately)
		{
			RefreshPerspectiveScaleNow();
		}
	}

	public void SetButlerBackScaleForRoom(string roomId, float roomLocalFootY, float scale, bool applyImmediately = true)
	{
		ButlerRoomScaleOverride roomScale = GetOrCreateButlerScaleOverride(roomId);

		if (roomScale == null)
		{
			return;
		}

		roomScale.SetBack(roomLocalFootY, scale);
		useButlerRoomScaleOverrides = true;
		SetEditorSelectedButlerScaleRoomId(roomScale.RoomId);

		if (applyImmediately)
		{
			RefreshPerspectiveScaleNow();
		}
	}

	public bool RemoveButlerScaleOverrideForRoom(string roomId, bool applyImmediately = true)
	{
		int existingIndex = GetButlerScaleOverrideIndex(roomId);

		if (existingIndex < 0)
		{
			return false;
		}

		butlerRoomScaleOverrides.RemoveAt(existingIndex);

		if (applyImmediately)
		{
			RefreshPerspectiveScaleNow();
		}

		return true;
	}

	public void GetButlerScaleOverrideRoomIds(List<string> results)
	{
		if (results == null || butlerRoomScaleOverrides == null)
		{
			return;
		}

		for (int i = 0; i < butlerRoomScaleOverrides.Count; i++)
		{
			string roomId = butlerRoomScaleOverrides[i].RoomId;

			if (!string.IsNullOrWhiteSpace(roomId) && !ContainsRoomName(results, roomId))
			{
				results.Add(roomId);
			}
		}
	}

	public bool TryGetCurrentButlerRoomLocalFootPoint(out Vector2 footPoint)
	{
		CacheReferences();
		UpdateVisualOffset(Camera.main);

		Vector2 logicalFootPoint = WalkableWorldToLogicalPoint(GetCurrentVisibleMovementWorldPoint());

		if (TryGetRoomStageLocalPoint(logicalFootPoint, out footPoint))
		{
			return true;
		}

		footPoint = logicalFootPoint;
		return true;
	}

	public float CaptureCurrentButlerScaleMultiplier(bool removeRoomStageScale = true)
	{
		CaptureAuthoredLocalScaleIfNeeded();
		UpdateVisualOffset(Camera.main);

		float xScale = Mathf.Abs(authoredLocalScale.x) > 0.0001f
			? transform.localScale.x / authoredLocalScale.x
			: 1f;
		float yScale = Mathf.Abs(authoredLocalScale.y) > 0.0001f
			? transform.localScale.y / authoredLocalScale.y
			: xScale;
		float scale = Mathf.Abs(yScale) > 0.0001f ? (xScale + yScale) * 0.5f : xScale;

		if (removeRoomStageScale)
		{
			scale /= Mathf.Max(0.0001f, currentRoomStageScaleRatio);
		}

		return SanitizeButlerScale(scale);
	}

	public void ApplyButlerScalePreview(float scale, bool includeRoomStageScale = true)
	{
		CaptureAuthoredLocalScaleIfNeeded();

		float finalScale = SanitizeButlerScale(scale);

		if (includeRoomStageScale)
		{
			UpdateVisualOffset(Camera.main);
			finalScale *= currentRoomStageScaleRatio;
		}

		transform.localScale = new Vector3(
			authoredLocalScale.x * finalScale,
			authoredLocalScale.y * finalScale,
			authoredLocalScale.z);
	}

	public bool InitializeButlerScaleOverrideForRoomFromCurrentPerspective(string roomId, bool applyImmediately = true)
	{
		if (!TryGetExistingPerspectiveEndpointsForRoom(roomId, out float frontY, out float frontScale, out float backY, out float backScale))
		{
			return false;
		}

		SetButlerFrontScaleForRoom(roomId, frontY, frontScale, false);
		SetButlerBackScaleForRoom(roomId, backY, backScale, applyImmediately);
		return true;
	}

	public void SetPerspectiveScaleEnabled(bool value, bool restoreAuthoredScale = true)
	{
		CaptureAuthoredLocalScaleIfNeeded();
		applyPerspectiveScale = value;

		if (!applyPerspectiveScale && restoreAuthoredScale)
		{
			RestoreAuthoredLocalScale();
		}
	}

	public void SetPlayerSortingEnabled(bool value, bool restoreAuthoredSorting = true)
	{
		CacheReferences();
		CaptureAuthoredRendererSortingIfNeeded();
		applyPlayerSorting = value;

		if (!applyPlayerSorting && restoreAuthoredSorting)
		{
			RestoreAuthoredRendererSorting();
		}
	}

	public readonly struct MovementTargetQuery
	{
		public MovementTargetQuery(
			Vector2 screenPosition,
			Vector2 requestedLogicalPosition,
			Vector2 destination,
			bool exactPointWalkable,
			bool hasReachableDestination,
			bool usesProjectedDestination,
			bool wouldMove)
		{
			ScreenPosition = screenPosition;
			RequestedLogicalPosition = requestedLogicalPosition;
			Destination = destination;
			ExactPointWalkable = exactPointWalkable;
			HasReachableDestination = hasReachableDestination;
			UsesProjectedDestination = usesProjectedDestination;
			WouldMove = wouldMove;
		}

		public Vector2 ScreenPosition { get; }
		public Vector2 RequestedLogicalPosition { get; }
		public Vector2 Destination { get; }
		public bool ExactPointWalkable { get; }
		public bool HasReachableDestination { get; }
		public bool UsesProjectedDestination { get; }
		public bool CanShowWalkCursor => HasReachableDestination && (ExactPointWalkable || UsesProjectedDestination);
		public bool WouldMove { get; }
	}

	private void Awake()
	{
		CacheReferences();
		CaptureAuthoredLocalScaleIfNeeded();
		CaptureAuthoredRendererSortingIfNeeded();
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
		UpdateFootstepAudio();
	}

	private void OnDisable()
	{
		NavigationCursorController.ClearWalkHover(this);
		footstepAudio?.SetWalking(false);
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

		ApplyPerspectiveScale();
		ApplyVisualPosition();
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

		if (footstepAudio == null)
			footstepAudio = GetComponent<PlayerFootstepAudio>();

		if (footstepAudio == null && Application.isPlaying)
			footstepAudio = gameObject.AddComponent<PlayerFootstepAudio>();

		if (cameraManager == null)
			cameraManager = FindAnyObjectByType<CameraManager>();

		if (navigationManager == null)
			navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
	}

	private void CacheAnimatorParameters()
	{
		animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
	}

	private void CaptureAuthoredLocalScaleIfNeeded()
	{
		if (hasAuthoredLocalScale)
		{
			return;
		}

		authoredLocalScale = transform.localScale;
		authoredPerspectiveScaleReference = GetPerspectiveScaleForY(GetCurrentVisibleMovementWorldPoint().y);
		hasAuthoredLocalScale = true;
	}

	private void RestoreAuthoredLocalScale()
	{
		if (!hasAuthoredLocalScale)
		{
			return;
		}

		transform.localScale = authoredLocalScale;
	}

	private void CaptureAuthoredRendererSortingIfNeeded()
	{
		if (hasAuthoredRendererSorting)
		{
			return;
		}

		if (spriteRenderers == null || spriteRenderers.Length == 0)
		{
			CacheReferences();
		}

		if (spriteRenderers == null || spriteRenderers.Length == 0)
		{
			authoredRendererSorting = Array.Empty<AuthoredRendererSorting>();
			hasAuthoredRendererSorting = true;
			return;
		}

		List<AuthoredRendererSorting> capturedSorting = new List<AuthoredRendererSorting>(spriteRenderers.Length);

		for (int i = 0; i < spriteRenderers.Length; i++)
		{
			SpriteRenderer targetRenderer = spriteRenderers[i];

			if (targetRenderer == null)
			{
				continue;
			}

			capturedSorting.Add(new AuthoredRendererSorting(targetRenderer));
		}

		authoredRendererSorting = capturedSorting.Count > 0 ? capturedSorting.ToArray() : Array.Empty<AuthoredRendererSorting>();
		hasAuthoredRendererSorting = true;
	}

	private void RestoreAuthoredRendererSorting()
	{
		if (!hasAuthoredRendererSorting)
		{
			return;
		}

		for (int i = 0; i < authoredRendererSorting.Length; i++)
		{
			AuthoredRendererSorting sorting = authoredRendererSorting[i];
			SpriteRenderer targetRenderer = sorting.Renderer;

			if (targetRenderer == null)
			{
				continue;
			}

			targetRenderer.sortingLayerID = sorting.SortingLayerId;
			targetRenderer.sortingOrder = sorting.SortingOrder;
			targetRenderer.spriteSortPoint = sorting.SpriteSortPoint;

			if (targetRenderer == spriteRenderer)
			{
				currentSortingOrder = sorting.SortingOrder;
			}
		}
	}

	private void InitializeVisualStateFromTransform()
	{
		UpdateVisualOffset(Camera.main);
		logicalPosition = WalkableWorldToLogicalPoint(GetCurrentVisibleMovementWorldPoint());
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

		RefreshWalkableBlockersForCurrentRoom(currentRoom, roomBoundary);
		walkableFloor = roomBoundary;
		currentWalkableBoundaryRoom = cleanRoom;
		InvalidatePolygonRouteGraph();
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

		UpdateVisualOffset(Camera.main);
		Vector2 startPosition = ClampToWalkableArea(WalkableWorldToLogicalPoint(GetCurrentVisibleMovementWorldPoint()));
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
		ApplyPerspectiveScale();
		ApplyVisualPosition();
		ApplyPlayerSorting();
	}

	private bool TryGetFloorClick(out Vector2 clickPosition, out Vector2 screenPosition, out bool pointerOverUi)
	{
		clickPosition = Vector2.zero;
		screenPosition = Vector2.zero;
		pointerOverUi = false;

		if (RuntimeSettingsMenu.BlocksGameInput)
			return false;

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

		if (!movementQuery.CanShowWalkCursor || !movementQuery.WouldMove)
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

	public bool TryGetWorldPointFromLogicalPosition(Vector2 logicalPoint, out Vector2 worldPoint)
	{
		worldPoint = Vector2.zero;

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);
		worldPoint = LogicalToWalkableWorldPoint(logicalPoint);
		return true;
	}

	public bool TryGetLogicalPositionFromWorldPoint(
		Vector2 worldPoint,
		bool clampToWalkableArea,
		Vector2 preferredWorldPoint,
		out Vector2 logicalPoint)
	{
		logicalPoint = Vector2.zero;

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);

		Vector2 candidateLogicalPoint = WalkableWorldToLogicalPoint(worldPoint);

		if (!IsPointWalkable(candidateLogicalPoint))
		{
			if (!clampToWalkableArea)
				return false;

			Vector2 preferredLogicalPoint = WalkableWorldToLogicalPoint(preferredWorldPoint);
			candidateLogicalPoint = ClampToWalkableArea(candidateLogicalPoint, preferredLogicalPoint);
		}

		if (clampToWalkableArea && !IsPointWalkable(candidateLogicalPoint))
		{
			Vector2 preferredLogicalPoint = WalkableWorldToLogicalPoint(preferredWorldPoint);
			candidateLogicalPoint = ClampToWalkableArea(candidateLogicalPoint, preferredLogicalPoint);
		}

		if (!IsPointWalkable(candidateLogicalPoint))
			return false;

		logicalPoint = candidateLogicalPoint;
		return true;
	}

	public Vector2 MoveLogicalPointToward(Vector2 currentPosition, Vector2 targetPosition, float maxDistance)
	{
		return MoveLogicalPositionToward(currentPosition, targetPosition, maxDistance);
	}

	public bool TryBuildReachableWorldPath(
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint,
		bool clampToWalkableArea,
		List<Vector2> worldPath)
	{
		if (worldPath == null)
			return false;

		worldPath.Clear();

		if (!isReady)
			return false;

		RefreshWalkableFloorForCurrentRoom();
		UpdateVisualOffset(Camera.main);

		Vector2 startLogicalPoint = WalkableWorldToLogicalPoint(startWorldPoint);
		Vector2 targetLogicalPoint = WalkableWorldToLogicalPoint(targetWorldPoint);
		Vector2 destinationLogicalPoint = targetLogicalPoint;
		bool exactPointWalkable = IsPointWalkable(targetLogicalPoint);

		if (!exactPointWalkable && clampToWalkableArea)
		{
			destinationLogicalPoint = ClampToWalkableArea(targetLogicalPoint, startLogicalPoint);
		}

		if (clampToWalkableArea && !IsPointWalkable(destinationLogicalPoint))
		{
			destinationLogicalPoint = ClampToWalkableArea(destinationLogicalPoint, startLogicalPoint);
		}

		if (!TryBuildMovementPath(startLogicalPoint, destinationLogicalPoint, movementQueryPath) ||
			movementQueryPath.Count == 0)
		{
			return false;
		}

		for (int i = 0; i < movementQueryPath.Count; i++)
		{
			worldPath.Add(LogicalToWalkableWorldPoint(movementQueryPath[i]));
		}

		return worldPath.Count > 0;
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
		bool usesProjectedDestination = false;

		if (!exactPointWalkable && clampToWalkableArea)
		{
			destinationPosition = ClampToWalkableArea(targetPosition, logicalPosition);
			usesProjectedDestination = Vector2.Distance(destinationPosition, targetPosition) > stopDistance;
		}
		else if (!exactPointWalkable &&
			TryProjectClickToNearbyWalkableDestination(targetPosition, logicalPosition, out Vector2 projectedDestination))
		{
			destinationPosition = projectedDestination;
			usesProjectedDestination = true;
		}

		if (clampToWalkableArea && !IsPointWalkable(destinationPosition))
		{
			destinationPosition = ClampToWalkableArea(destinationPosition, logicalPosition);
			usesProjectedDestination = Vector2.Distance(destinationPosition, targetPosition) > stopDistance;
		}

		bool hasReachableDestination = TryBuildMovementPath(logicalPosition, destinationPosition, movementQueryPath);
		if (!hasReachableDestination &&
			TryFindReachableDestinationNear(targetPosition, out Vector2 reachableDestination))
		{
			destinationPosition = reachableDestination;
			usesProjectedDestination = true;
			hasReachableDestination = true;
		}

		bool wouldMove = hasReachableDestination &&
			Vector2.Distance(logicalPosition, destinationPosition) > stopDistance;

		movementQuery = new MovementTargetQuery(
			screenPosition,
			targetPosition,
			destinationPosition,
			exactPointWalkable,
			hasReachableDestination,
			usesProjectedDestination,
			wouldMove);
		return true;
	}

	private bool TryProjectClickToNearbyWalkableDestination(
		Vector2 requestedLogicalPoint,
		Vector2 preferredLogicalPoint,
		out Vector2 destination)
	{
		destination = requestedLogicalPoint;

		if (walkableFloor == null)
		{
			return true;
		}

		UpdateVisualOffset(Camera.main);
		Vector2 requestedWorldPoint = LogicalToWalkableWorldPoint(requestedLogicalPoint);
		Vector2 preferredWorldPoint = LogicalToWalkableWorldPoint(preferredLogicalPoint);
		float maxDistance = GetClickProjectionMaxWorldDistance();

		if (!TryFindProjectedWalkableWorldPointNearClick(
			requestedWorldPoint,
			preferredWorldPoint,
			maxDistance,
			out Vector2 projectedWorldPoint))
		{
			return false;
		}

		Vector2 projectedLogicalPoint = WalkableWorldToLogicalPoint(projectedWorldPoint);
		if (!IsPointWalkable(projectedLogicalPoint))
		{
			return false;
		}

		destination = projectedLogicalPoint;
		return true;
	}

	private bool TryFindProjectedWalkableWorldPointNearClick(
		Vector2 requestedWorldPoint,
		Vector2 preferredWorldPoint,
		float maxDistance,
		out Vector2 projectedWorldPoint)
	{
		projectedWorldPoint = requestedWorldPoint;

		if (walkableFloor == null)
		{
			return true;
		}

		if (IsWalkableWorldPoint(requestedWorldPoint))
		{
			return true;
		}

		bool foundPoint = false;
		float bestSqrDistance = maxDistance * maxDistance;
		Vector2 bestPoint = Vector2.zero;
		Vector2 closestPoint = walkableFloor.ClosestPoint(requestedWorldPoint);

		TryAcceptProjectedWalkableWorldPoint(
			closestPoint,
			requestedWorldPoint,
			ref foundPoint,
			ref bestSqrDistance,
			ref bestPoint);

		Vector2 insetDirection = preferredWorldPoint - closestPoint;
		if (insetDirection.sqrMagnitude <= MovementEpsilon || !IsWalkableWorldPoint(preferredWorldPoint))
		{
			insetDirection = (Vector2)walkableFloor.bounds.center - closestPoint;
		}

		if (insetDirection.sqrMagnitude > MovementEpsilon)
		{
			insetDirection.Normalize();

			for (int ring = 1; ring <= ClickProjectionSearchRings; ring++)
			{
				float radius = maxDistance * ring / ClickProjectionSearchRings;
				Vector2 insetPoint = closestPoint + insetDirection * radius;
				TryAcceptProjectedWalkableWorldPoint(
					insetPoint,
					requestedWorldPoint,
					ref foundPoint,
					ref bestSqrDistance,
					ref bestPoint);
			}
		}

		for (int ring = 1; ring <= ClickProjectionSearchRings; ring++)
		{
			float radius = maxDistance * ring / ClickProjectionSearchRings;

			for (int sample = 0; sample < ClickProjectionSearchSamplesPerRing; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / ClickProjectionSearchSamplesPerRing;
				Vector2 samplePoint = closestPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
				TryAcceptProjectedWalkableWorldPoint(
					samplePoint,
					requestedWorldPoint,
					ref foundPoint,
					ref bestSqrDistance,
					ref bestPoint);
			}
		}

		if (!foundPoint)
		{
			return false;
		}

		projectedWorldPoint = bestPoint;
		return true;
	}

	private void TryAcceptProjectedWalkableWorldPoint(
		Vector2 candidateWorldPoint,
		Vector2 requestedWorldPoint,
		ref bool foundPoint,
		ref float bestSqrDistance,
		ref Vector2 bestPoint)
	{
		if (!IsWalkableWorldPoint(candidateWorldPoint))
		{
			return;
		}

		float sqrDistance = (candidateWorldPoint - requestedWorldPoint).sqrMagnitude;
		if (sqrDistance > bestSqrDistance)
		{
			return;
		}

		foundPoint = true;
		bestSqrDistance = sqrDistance;
		bestPoint = candidateWorldPoint;
	}

	private bool TryFindReachableDestinationNear(Vector2 requestedLogicalPoint, out Vector2 destination)
	{
		destination = requestedLogicalPoint;

		if (walkableFloor == null)
		{
			return true;
		}

		UpdateVisualOffset(Camera.main);
		float maxDistance = GetClickProjectionMaxWorldDistance();
		Vector2 requestedWorldPoint = LogicalToWalkableWorldPoint(requestedLogicalPoint);
		Vector2 currentWorldPoint = LogicalToWalkableWorldPoint(logicalPosition);

		if (TryProjectClickToNearbyWalkableDestination(requestedLogicalPoint, logicalPosition, out Vector2 projectedDestination) &&
			TryBuildMovementPath(logicalPosition, projectedDestination, movementQueryPath))
		{
			destination = projectedDestination;
			return true;
		}

		for (int ring = 1; ring <= ClickProjectionSearchRings; ring++)
		{
			float radius = maxDistance * ring / ClickProjectionSearchRings;

			for (int sample = 0; sample < ClickProjectionSearchSamplesPerRing; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / ClickProjectionSearchSamplesPerRing;
				Vector2 candidateWorldPoint = requestedWorldPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

				if (!IsWalkableWorldPoint(candidateWorldPoint))
				{
					continue;
				}

				if (Vector2.Distance(requestedWorldPoint, candidateWorldPoint) > maxDistance)
				{
					continue;
				}

				Vector2 candidateLogicalPoint = WalkableWorldToLogicalPoint(candidateWorldPoint);
				if (TryBuildMovementPath(logicalPosition, candidateLogicalPoint, movementQueryPath))
				{
					destination = candidateLogicalPoint;
					return true;
				}
			}
		}

		Vector2 towardCurrent = currentWorldPoint - requestedWorldPoint;
		if (towardCurrent.sqrMagnitude > MovementEpsilon)
		{
			Vector2 candidateWorldPoint = requestedWorldPoint + towardCurrent.normalized * Mathf.Min(maxDistance, towardCurrent.magnitude);
			if (IsWalkableWorldPoint(candidateWorldPoint))
			{
				Vector2 candidateLogicalPoint = WalkableWorldToLogicalPoint(candidateWorldPoint);
				if (TryBuildMovementPath(logicalPosition, candidateLogicalPoint, movementQueryPath))
				{
					destination = candidateLogicalPoint;
					return true;
				}
			}
		}

		return false;
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
		if (RuntimeSettingsMenu.BlocksGameInput)
		{
			NavigationCursorController.ClearWalkHover(this);
			return;
		}

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
			movementQuery.CanShowWalkCursor);
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
		ApplyPerspectiveScale();
		ApplyVisualPosition();
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

		UpdateFootstepAudio();
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
		if (!applyPerspectiveScale)
		{
			return;
		}

		UpdateVisualOffset(Camera.main);
		float scale = TryEvaluateButlerRoomScaleOverride(out float calibratedScale)
			? calibratedScale
			: CalculateExistingPerspectiveScale();
		scale *= currentRoomStageScaleRatio;
		transform.localScale = new Vector3(
			authoredLocalScale.x * scale,
			authoredLocalScale.y * scale,
			authoredLocalScale.z);
	}

	private float CalculateExistingPerspectiveScale()
	{
		TryGetPerspectiveScaleForY(logicalPosition.y, out float depthScale, out bool usesRoomProfileScale);
		float fallbackRelativeScale = depthScale / Mathf.Max(0.0001f, authoredPerspectiveScaleReference);
		return usesRoomProfileScale ? depthScale : fallbackRelativeScale;
	}

	private bool TryEvaluateButlerRoomScaleOverride(out float calibratedScale)
	{
		calibratedScale = 1f;

		if (!useButlerRoomScaleOverrides)
		{
			return false;
		}

		string roomId = GetCurrentButlerScaleRoomId();

		if (!HasCompleteButlerRoomScaleOverride(roomId))
		{
			return false;
		}

		float roomLocalFootY = logicalPosition.y;

		if (TryGetCurrentButlerRoomLocalFootPoint(out Vector2 roomLocalFootPoint))
		{
			roomLocalFootY = roomLocalFootPoint.y;
		}

		calibratedScale = GetButlerScaleForRoomAtY(roomId, roomLocalFootY, CalculateExistingPerspectiveScale());
		return true;
	}

	private float GetPerspectiveScaleForY(float y)
	{
		TryGetPerspectiveScaleForY(y, out float scale, out _);
		return scale;
	}

	private bool TryGetPerspectiveScaleForY(float y, out float scale, out bool usesRoomProfileScale)
	{
		if (TryGetRoomPerspectiveScaleForY(y, out scale))
		{
			usesRoomProfileScale = true;
			return true;
		}

		usesRoomProfileScale = false;
		scale = GetFallbackPerspectiveScaleForY(y);
		return true;
	}

	private float GetFallbackPerspectiveScaleForY(float y)
	{
		float depth = Mathf.InverseLerp(nearY, farY, y);
		return Mathf.Max(0.0001f, Mathf.Lerp(nearScale, farScale, depth));
	}

	private bool TryGetRoomPerspectiveScaleForY(float y, out float scale)
	{
		scale = 1f;

		if (!TryGetCurrentRoomPerspectiveProfile(out RoomPerspectiveProfile profile))
		{
			return false;
		}

		Vector2 logicalPoint = new Vector2(logicalPosition.x, y);

		if (TryGetRoomStageLocalPoint(logicalPoint, out Vector2 roomLocalPoint))
		{
			scale = profile.GetScale(roomLocalPoint);
			return true;
		}

		scale = profile.GetScale(new Vector2(0f, y));
		return true;
	}

	private bool TryGetRoomStageLocalPoint(Vector2 logicalPoint, out Vector2 roomLocalPoint)
	{
		roomLocalPoint = Vector2.zero;

		if (cameraManager == null)
		{
			cameraManager = FindAnyObjectByType<CameraManager>();
		}

		if (cameraManager == null)
		{
			return false;
		}

		Vector2 worldPoint = LogicalToWalkableWorldPoint(logicalPoint);
		return cameraManager.TryGetActiveRoomStageLocalPoint(worldPoint, out roomLocalPoint);
	}

	private bool TryGetCurrentRoomPerspectiveProfile(out RoomPerspectiveProfile profile)
	{
		profile = null;

		if (!useRoomPerspectiveProfileScale)
		{
			return false;
		}

		if (navigationManager == null)
		{
			navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
		}

		string currentRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;

		if (currentRoomPerspectiveProfile != null &&
			SameRoomName(currentRoomPerspectiveProfileRoom, currentRoom))
		{
			profile = currentRoomPerspectiveProfile;
			return true;
		}

		if (!string.IsNullOrWhiteSpace(currentRoom) &&
			TryFindRoomContentForRoom(currentRoom, out RoomContentGroup roomContent) &&
			roomContent.TryGetPerspectiveProfile(out profile))
		{
			currentRoomPerspectiveProfile = profile;
			currentRoomPerspectiveProfileRoom = roomContent.RoomName;
			return true;
		}

		RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);
		if (parentRoom != null && parentRoom.TryGetPerspectiveProfile(out profile))
		{
			currentRoomPerspectiveProfile = profile;
			currentRoomPerspectiveProfileRoom = parentRoom.RoomName;
			return true;
		}

		currentRoomPerspectiveProfile = null;
		currentRoomPerspectiveProfileRoom = string.Empty;
		return false;
	}

	private string GetCurrentButlerScaleRoomId()
	{
		if (!Application.isPlaying && !string.IsNullOrWhiteSpace(editorSelectedButlerScaleRoomId))
		{
			return CleanRoomName(editorSelectedButlerScaleRoomId);
		}

		CacheReferences();

		string currentRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;

		if (!string.IsNullOrWhiteSpace(currentRoom))
		{
			return CleanRoomName(currentRoom);
		}

		if (!string.IsNullOrWhiteSpace(currentRoomPerspectiveProfileRoom))
		{
			return CleanRoomName(currentRoomPerspectiveProfileRoom);
		}

		RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);

		if (parentRoom != null && !string.IsNullOrWhiteSpace(parentRoom.RoomName))
		{
			return CleanRoomName(parentRoom.RoomName);
		}

		return CleanRoomName(editorSelectedButlerScaleRoomId);
	}

	private ButlerRoomScaleOverride GetOrCreateButlerScaleOverride(string roomId)
	{
		string cleanRoomId = CleanRoomName(roomId);

		if (string.IsNullOrWhiteSpace(cleanRoomId))
		{
			return null;
		}

		if (butlerRoomScaleOverrides == null)
		{
			butlerRoomScaleOverrides = new List<ButlerRoomScaleOverride>();
		}

		int existingIndex = GetButlerScaleOverrideIndex(cleanRoomId);

		if (existingIndex >= 0)
		{
			return butlerRoomScaleOverrides[existingIndex];
		}

		ButlerRoomScaleOverride roomScale = new ButlerRoomScaleOverride(cleanRoomId);
		butlerRoomScaleOverrides.Add(roomScale);
		return roomScale;
	}

	private int GetButlerScaleOverrideIndex(string roomId)
	{
		if (butlerRoomScaleOverrides == null || string.IsNullOrWhiteSpace(roomId))
		{
			return -1;
		}

		string cleanRoomId = CleanRoomName(roomId);

		for (int i = 0; i < butlerRoomScaleOverrides.Count; i++)
		{
			ButlerRoomScaleOverride roomScale = butlerRoomScaleOverrides[i];

			if (roomScale != null && roomScale.Matches(cleanRoomId))
			{
				return i;
			}
		}

		return -1;
	}

	private bool TryGetExistingPerspectiveEndpointsForRoom(
		string roomId,
		out float frontY,
		out float frontScale,
		out float backY,
		out float backScale)
	{
		frontY = nearY;
		backY = farY;
		CaptureAuthoredLocalScaleIfNeeded();

		if (!string.IsNullOrWhiteSpace(roomId) &&
			TryFindRoomContentForRoom(roomId, out RoomContentGroup roomContent) &&
			roomContent.TryGetPerspectiveProfile(out RoomPerspectiveProfile profile))
		{
			frontY = profile.NearFootY;
			backY = profile.FarFootY;
			frontScale = SanitizeButlerScale(profile.GetScale(new Vector2(0f, frontY)));
			backScale = SanitizeButlerScale(profile.GetScale(new Vector2(0f, backY)));
			return true;
		}

		float safeReferenceScale = Mathf.Max(0.0001f, authoredPerspectiveScaleReference);
		frontScale = SanitizeButlerScale(GetFallbackPerspectiveScaleForY(frontY) / safeReferenceScale);
		backScale = SanitizeButlerScale(GetFallbackPerspectiveScaleForY(backY) / safeReferenceScale);
		return true;
	}

	private void UpdateAnimator()
	{
		animatorParameters.ApplyMovement(animator, isWalking, walkDirection, runningAnimationSpeed);
		ApplySpriteMirror();
	}

	private void UpdateFootstepAudio()
	{
		if (footstepAudio == null)
		{
			CacheReferences();
		}

		footstepAudio?.SetWalking(isWalking && isReady && enabled && gameObject.activeInHierarchy);
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
		float feetOffsetY = GetVisibleFeetOffsetY();
		Vector3 visualPosition = new Vector3(visualPoint.x, visualPoint.y - feetOffsetY, transform.position.z);

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

	private Vector2 GetCurrentVisibleMovementWorldPoint()
	{
		Vector2 movementPoint = transform.position;

		if (TryGetVisibleFeetY(out float feetY))
		{
			movementPoint.y = feetY;
		}

		return movementPoint;
	}

	private float GetVisibleFeetOffsetY()
	{
		return TryGetVisibleFeetY(out float feetY) ? feetY - transform.position.y : 0f;
	}

	private bool TryGetVisibleFeetY(out float feetY)
	{
		feetY = transform.position.y;

		if (spriteRenderers == null || spriteRenderers.Length == 0)
		{
			CacheReferences();
		}

		if (spriteRenderers == null || spriteRenderers.Length == 0)
		{
			return false;
		}

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

		if (!foundRendererBounds)
		{
			return false;
		}

		feetY = lowestVisibleY;
		return true;
	}

	private bool IsPointWalkable(Vector2 point)
	{
		UpdateVisualOffset(Camera.main);
		return IsWalkableWorldPoint(LogicalToWalkableWorldPoint(point));
	}

	private bool IsWalkableWorldPoint(Vector2 worldPoint)
	{
		if (walkableFloor != null && !walkableFloor.OverlapPoint(worldPoint))
		{
			return false;
		}

		for (int i = 0; i < walkableBlockers.Count; i++)
		{
			Collider2D blocker = walkableBlockers[i];
			if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (blocker.OverlapPoint(worldPoint))
			{
				return false;
			}
		}

		return true;
	}

	private bool TryBuildMovementPath(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		path.Clear();

		if (!IsPointWalkable(targetPosition))
		{
			return false;
		}

		if (!IsPointWalkable(startPosition))
		{
			startPosition = ClampToWalkableArea(startPosition, targetPosition);
		}

		if (!IsPointWalkable(startPosition))
		{
			return false;
		}

		if (TryBuildMovementPathBetweenWalkable(startPosition, targetPosition, path))
		{
			return path.Count > 0;
		}

		if (TryBuildMovementPathFromNearbyStart(startPosition, targetPosition, path))
		{
			return path.Count > 0;
		}

		return false;
	}

	private bool TryBuildMovementPathBetweenWalkable(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		path.Clear();

		if (IsWalkableLogicalSegment(startPosition, targetPosition))
		{
			path.Add(targetPosition);
			return true;
		}

		if (TryBuildPolygonMovementPath(startPosition, targetPosition, path))
		{
			return path.Count > 0;
		}

		if (TryBuildGridMovementPath(startPosition, targetPosition, path))
		{
			return path.Count > 0;
		}

		return false;
	}

	private bool TryBuildMovementPathFromNearbyStart(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		if (walkableFloor == null)
		{
			return false;
		}

		UpdateVisualOffset(Camera.main);
		Vector2 startWorldPoint = LogicalToWalkableWorldPoint(startPosition);
		float maxDistance = GetClickProjectionMaxWorldDistance() * 0.5f;

		for (int ring = 1; ring <= ClickProjectionSearchRings; ring++)
		{
			float radius = maxDistance * ring / ClickProjectionSearchRings;

			for (int sample = 0; sample < ClickProjectionSearchSamplesPerRing; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / ClickProjectionSearchSamplesPerRing;
				Vector2 candidateWorldPoint = startWorldPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

				if (!IsWalkableWorldPoint(candidateWorldPoint) ||
					!IsWalkableWorldSegment(startWorldPoint, candidateWorldPoint))
				{
					continue;
				}

				Vector2 candidateStartPosition = WalkableWorldToLogicalPoint(candidateWorldPoint);
				if (!TryBuildMovementPathBetweenWalkable(candidateStartPosition, targetPosition, path))
				{
					continue;
				}

				if (Vector2.Distance(startPosition, candidateStartPosition) > stopDistance * 0.25f)
				{
					path.Insert(0, candidateStartPosition);
				}

				return path.Count > 0;
			}
		}

		return false;
	}

	private bool IsWalkableLogicalSegment(Vector2 startPosition, Vector2 targetPosition)
	{
		UpdateVisualOffset(Camera.main);
		Vector2 startWorldPoint = LogicalToWalkableWorldPoint(startPosition);
		Vector2 targetWorldPoint = LogicalToWalkableWorldPoint(targetPosition);
		return IsWalkableWorldSegment(startWorldPoint, targetWorldPoint);
	}

	private bool IsWalkableWorldSegment(Vector2 startWorldPoint, Vector2 targetWorldPoint)
	{
		if (walkableFloor == null)
		{
			return true;
		}

		if (!IsWalkableWorldPoint(startWorldPoint) || !IsWalkableWorldPoint(targetWorldPoint))
		{
			return false;
		}

		float distance = Vector2.Distance(startWorldPoint, targetWorldPoint);
		if (distance <= MovementEpsilon)
		{
			return true;
		}

		float spacing = GetPathSegmentProbeSpacing();
		int samples = Mathf.Clamp(Mathf.CeilToInt(distance / spacing), MinPathSegmentProbeSamples, MaxPathSegmentProbeSamples);

		for (int i = 1; i < samples; i++)
		{
			Vector2 samplePoint = Vector2.Lerp(startWorldPoint, targetWorldPoint, i / (float)samples);
			if (!IsWalkableWorldPoint(samplePoint))
			{
				return false;
			}
		}

		return true;
	}

	private bool TryBuildPolygonMovementPath(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		PolygonCollider2D polygon = walkableFloor as PolygonCollider2D;
		if (polygon == null || polygon.pathCount == 0)
		{
			return false;
		}

		Vector2 startWorldPoint = LogicalToWalkableWorldPoint(startPosition);
		Vector2 targetWorldPoint = LogicalToWalkableWorldPoint(targetPosition);

		if (!EnsurePolygonRouteGraph(polygon))
		{
			return false;
		}

		return TryBuildCachedPolygonRoute(polygon, startWorldPoint, targetWorldPoint, targetPosition, path);
	}

	private bool EnsurePolygonRouteGraph(PolygonCollider2D polygon)
	{
		if (polygon == null || polygon.pathCount == 0)
		{
			InvalidatePolygonRouteGraph();
			return false;
		}

		int shapeHash = ComputePolygonRouteShapeHash(polygon);
		if (polygonRouteGraphValid &&
			cachedPolygonRouteCollider == polygon &&
			cachedPolygonRouteShapeHash == shapeHash)
		{
			return true;
		}

		BuildPolygonRouteGraph(polygon, shapeHash);
		return polygonRouteGraphValid;
	}

	private void BuildPolygonRouteGraph(PolygonCollider2D polygon, int shapeHash)
	{
		polygonRouteLocalNodes.Clear();
		polygonRouteConnections.Clear();
		pathWorldNodes.Clear();

		CollectPolygonPathNodes(polygon, pathWorldNodes);
		CollectPolygonBlockerPathNodes(pathWorldNodes);

		for (int i = 0; i < pathWorldNodes.Count; i++)
		{
			polygonRouteLocalNodes.Add(PolygonWorldPointToLocal(polygon, pathWorldNodes[i]));
		}

		for (int currentIndex = 0; currentIndex < pathWorldNodes.Count; currentIndex++)
		{
			for (int nextIndex = currentIndex + 1; nextIndex < pathWorldNodes.Count; nextIndex++)
			{
				if (!IsWalkableWorldSegment(pathWorldNodes[currentIndex], pathWorldNodes[nextIndex]))
				{
					continue;
				}

				polygonRouteConnections.Add(new PolygonRouteConnection(currentIndex, nextIndex));
				polygonRouteConnections.Add(new PolygonRouteConnection(nextIndex, currentIndex));
			}
		}

		cachedPolygonRouteCollider = polygon;
		cachedPolygonRouteShapeHash = shapeHash;
		polygonRouteGraphValid = polygonRouteLocalNodes.Count > 0;
	}

	private void InvalidatePolygonRouteGraph()
	{
		cachedPolygonRouteCollider = null;
		cachedPolygonRouteShapeHash = 0;
		polygonRouteGraphValid = false;
		polygonRouteLocalNodes.Clear();
		polygonRouteConnections.Clear();
		polygonRouteStartConnections.Clear();
		polygonRouteTargetConnections.Clear();
		polygonRouteIndices.Clear();
	}

	private int ComputePolygonRouteShapeHash(PolygonCollider2D polygon)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + polygon.pathCount;
			hash = hash * 31 + polygon.offset.GetHashCode();
			hash = hash * 31 + polygon.transform.localPosition.GetHashCode();
			hash = hash * 31 + polygon.transform.localRotation.GetHashCode();
			hash = hash * 31 + polygon.transform.localScale.GetHashCode();

			for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
			{
				Vector2[] localPath = polygon.GetPath(pathIndex);
				hash = hash * 31 + (localPath != null ? localPath.Length : 0);

				if (localPath == null)
				{
					continue;
				}

				for (int pointIndex = 0; pointIndex < localPath.Length; pointIndex++)
				{
					hash = hash * 31 + localPath[pointIndex].GetHashCode();
				}
			}

			for (int i = 0; i < walkableBlockers.Count; i++)
			{
				PolygonCollider2D blocker = walkableBlockers[i] as PolygonCollider2D;
				if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
				{
					continue;
				}

				hash = hash * 31 + blocker.pathCount;
				hash = hash * 31 + blocker.offset.GetHashCode();
				hash = hash * 31 + blocker.transform.localPosition.GetHashCode();
				hash = hash * 31 + blocker.transform.localRotation.GetHashCode();
				hash = hash * 31 + blocker.transform.localScale.GetHashCode();

				for (int pathIndex = 0; pathIndex < blocker.pathCount; pathIndex++)
				{
					Vector2[] localPath = blocker.GetPath(pathIndex);
					hash = hash * 31 + (localPath != null ? localPath.Length : 0);

					if (localPath == null)
					{
						continue;
					}

					for (int pointIndex = 0; pointIndex < localPath.Length; pointIndex++)
					{
						hash = hash * 31 + localPath[pointIndex].GetHashCode();
					}
				}
			}

			return hash;
		}
	}

	private bool TryBuildCachedPolygonRoute(
		PolygonCollider2D polygon,
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint,
		Vector2 targetPosition,
		List<Vector2> path)
	{
		path.Clear();

		if (IsWalkableWorldSegment(startWorldPoint, targetWorldPoint))
		{
			path.Add(targetPosition);
			return true;
		}

		CollectDynamicPolygonRouteConnections(polygon, startWorldPoint, targetWorldPoint);

		if (polygonRouteStartConnections.Count == 0 || polygonRouteTargetConnections.Count == 0)
		{
			return false;
		}

		int routeNodeCount = polygonRouteLocalNodes.Count + 2;
		PreparePathScratch(routeNodeCount);
		pathNodeDistances[0] = 0f;

		for (int iteration = 0; iteration < routeNodeCount; iteration++)
		{
			int currentIndex = FindNearestUnvisitedPathNode(routeNodeCount);
			if (currentIndex < 0)
			{
				break;
			}

			if (currentIndex == 1)
			{
				break;
			}

			pathVisitedNodes[currentIndex] = true;

			if (currentIndex == 0)
			{
				for (int i = 0; i < polygonRouteStartConnections.Count; i++)
				{
					TryRelaxPolygonRouteNode(
						polygon,
						currentIndex,
						polygonRouteStartConnections[i] + 2,
						startWorldPoint,
						targetWorldPoint);
				}

				continue;
			}

			int staticNodeIndex = currentIndex - 2;
			if (staticNodeIndex < 0 || staticNodeIndex >= polygonRouteLocalNodes.Count)
			{
				continue;
			}

			if (polygonRouteTargetConnections.Contains(staticNodeIndex))
			{
				TryRelaxPolygonRouteNode(polygon, currentIndex, 1, startWorldPoint, targetWorldPoint);
			}

			for (int i = 0; i < polygonRouteConnections.Count; i++)
			{
				PolygonRouteConnection connection = polygonRouteConnections[i];
				if (connection.From != staticNodeIndex)
				{
					continue;
				}

				TryRelaxPolygonRouteNode(
					polygon,
					currentIndex,
					connection.To + 2,
					startWorldPoint,
					targetWorldPoint);
			}
		}

		if (pathPreviousNodeIndices[1] < 0)
		{
			return false;
		}

		return BuildCachedPolygonRouteResult(polygon, startWorldPoint, targetWorldPoint, targetPosition, path);
	}

	private void CollectDynamicPolygonRouteConnections(
		PolygonCollider2D polygon,
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint)
	{
		polygonRouteStartConnections.Clear();
		polygonRouteTargetConnections.Clear();

		for (int nodeIndex = 0; nodeIndex < polygonRouteLocalNodes.Count; nodeIndex++)
		{
			Vector2 nodeWorldPoint = GetPolygonRouteNodeWorldPoint(polygon, nodeIndex);

			if (IsWalkableWorldSegment(startWorldPoint, nodeWorldPoint))
			{
				polygonRouteStartConnections.Add(nodeIndex);
			}

			if (IsWalkableWorldSegment(targetWorldPoint, nodeWorldPoint))
			{
				polygonRouteTargetConnections.Add(nodeIndex);
			}
		}
	}

	private bool TryRelaxPolygonRouteNode(
		PolygonCollider2D polygon,
		int currentIndex,
		int nextIndex,
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint)
	{
		if (nextIndex < 0 ||
			nextIndex >= pathVisitedNodes.Count ||
			pathVisitedNodes[nextIndex])
		{
			return false;
		}

		Vector2 currentWorldPoint = GetPolygonRouteWorldPoint(polygon, currentIndex, startWorldPoint, targetWorldPoint);
		Vector2 nextWorldPoint = GetPolygonRouteWorldPoint(polygon, nextIndex, startWorldPoint, targetWorldPoint);
		float nextDistance = pathNodeDistances[currentIndex] + Vector2.Distance(currentWorldPoint, nextWorldPoint);

		if (nextDistance >= pathNodeDistances[nextIndex])
		{
			return false;
		}

		pathNodeDistances[nextIndex] = nextDistance;
		pathPreviousNodeIndices[nextIndex] = currentIndex;
		return true;
	}

	private bool BuildCachedPolygonRouteResult(
		PolygonCollider2D polygon,
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint,
		Vector2 targetPosition,
		List<Vector2> path)
	{
		polygonRouteIndices.Clear();

		int routeIndex = 1;
		while (routeIndex >= 0)
		{
			polygonRouteIndices.Add(routeIndex);

			if (routeIndex == 0)
			{
				break;
			}

			routeIndex = pathPreviousNodeIndices[routeIndex];
		}

		if (polygonRouteIndices.Count == 0 || polygonRouteIndices[polygonRouteIndices.Count - 1] != 0)
		{
			polygonRouteIndices.Clear();
			return false;
		}

		polygonRouteIndices.Reverse();

		for (int i = 1; i < polygonRouteIndices.Count; i++)
		{
			int nodeIndex = polygonRouteIndices[i];
			Vector2 logicalPoint = nodeIndex == 1
				? targetPosition
				: WalkableWorldToLogicalPoint(GetPolygonRouteWorldPoint(polygon, nodeIndex, startWorldPoint, targetWorldPoint));

			if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], logicalPoint) > stopDistance * 0.5f)
			{
				path.Add(logicalPoint);
			}
		}

		return path.Count > 0;
	}

	private Vector2 GetPolygonRouteWorldPoint(
		PolygonCollider2D polygon,
		int routeIndex,
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint)
	{
		if (routeIndex == 0)
		{
			return startWorldPoint;
		}

		if (routeIndex == 1)
		{
			return targetWorldPoint;
		}

		return GetPolygonRouteNodeWorldPoint(polygon, routeIndex - 2);
	}

	private Vector2 GetPolygonRouteNodeWorldPoint(PolygonCollider2D polygon, int nodeIndex)
	{
		return PolygonLocalPointToWorld(polygon, polygonRouteLocalNodes[nodeIndex]);
	}

	private bool TryBuildGridMovementPath(Vector2 startPosition, Vector2 targetPosition, List<Vector2> path)
	{
		if (walkableFloor == null)
		{
			path.Add(targetPosition);
			return true;
		}

		Vector2 startWorldPoint = LogicalToWalkableWorldPoint(startPosition);
		Vector2 targetWorldPoint = LogicalToWalkableWorldPoint(targetPosition);
		float spacing = GetGridRouteSpacing(out int columns, out int rows);

		if (columns <= 0 || rows <= 0)
		{
			return false;
		}

		BuildGridRouteNodes(startWorldPoint, targetWorldPoint, spacing, columns, rows, gridRouteWorldNodes, gridRouteCellIndices);

		if (gridRouteWorldNodes.Count <= 2)
		{
			return false;
		}

		int nodeCount = gridRouteWorldNodes.Count;
		float[] gScores = new float[nodeCount];
		float[] fScores = new float[nodeCount];
		int[] previous = new int[nodeCount];
		bool[] closed = new bool[nodeCount];
		bool[] open = new bool[nodeCount];

		for (int i = 0; i < nodeCount; i++)
		{
			gScores[i] = float.PositiveInfinity;
			fScores[i] = float.PositiveInfinity;
			previous[i] = -1;
		}

		gridRouteOpenNodes.Clear();
		gScores[0] = 0f;
		fScores[0] = Vector2.Distance(startWorldPoint, targetWorldPoint);
		open[0] = true;
		gridRouteOpenNodes.Add(0);

		int targetIndex = 1;
		int[] cellNodeIndices = BuildGridRouteCellIndex(gridRouteCellIndices, columns, rows);
		float specialConnectionDistance = spacing * GridRouteSpecialConnectionMultiplier;

		while (gridRouteOpenNodes.Count > 0)
		{
			int currentIndex = PopLowestGridRouteOpenNode(gridRouteOpenNodes, fScores);
			open[currentIndex] = false;

			if (currentIndex == targetIndex)
			{
				return BuildGridRouteResult(previous, targetIndex, targetPosition, path);
			}

			closed[currentIndex] = true;

			if (currentIndex == 0)
			{
				ExploreGridRouteSpecialConnections(
					currentIndex,
					startWorldPoint,
					spacing,
					specialConnectionDistance,
					gScores,
					fScores,
					previous,
					closed,
					open,
					gridRouteOpenNodes);
				continue;
			}

			int currentCellIndex = gridRouteCellIndices[currentIndex];
			if (currentCellIndex < 0)
			{
				continue;
			}

			int cellX = currentCellIndex % columns;
			int cellY = currentCellIndex / columns;

			for (int yOffset = -1; yOffset <= 1; yOffset++)
			{
				for (int xOffset = -1; xOffset <= 1; xOffset++)
				{
					if (xOffset == 0 && yOffset == 0)
					{
						continue;
					}

					int nextX = cellX + xOffset;
					int nextY = cellY + yOffset;

					if (nextX < 0 || nextX >= columns || nextY < 0 || nextY >= rows)
					{
						continue;
					}

					int nextIndex = cellNodeIndices[nextY * columns + nextX];
					if (nextIndex < 0)
					{
						continue;
					}

					TryRelaxGridRouteNode(currentIndex, nextIndex, targetWorldPoint, gScores, fScores, previous, closed, open, gridRouteOpenNodes);
				}
			}

			if (Vector2.Distance(gridRouteWorldNodes[currentIndex], targetWorldPoint) <= specialConnectionDistance)
			{
				TryRelaxGridRouteNode(currentIndex, targetIndex, targetWorldPoint, gScores, fScores, previous, closed, open, gridRouteOpenNodes);
			}
		}

		return false;
	}

	private void BuildGridRouteNodes(
		Vector2 startWorldPoint,
		Vector2 targetWorldPoint,
		float spacing,
		int columns,
		int rows,
		List<Vector2> nodes,
		List<int> cellIndices)
	{
		nodes.Clear();
		cellIndices.Clear();
		nodes.Add(startWorldPoint);
		cellIndices.Add(-1);
		nodes.Add(targetWorldPoint);
		cellIndices.Add(-1);

		Bounds bounds = walkableFloor.bounds;
		Vector2 origin = bounds.min;

		for (int y = 0; y < rows; y++)
		{
			for (int x = 0; x < columns; x++)
			{
				Vector2 candidate = origin + new Vector2(x * spacing, y * spacing);

				if (!IsWalkableWorldPoint(candidate))
				{
					continue;
				}

				nodes.Add(candidate);
				cellIndices.Add(y * columns + x);
			}
		}
	}

	private int[] BuildGridRouteCellIndex(List<int> cellIndices, int columns, int rows)
	{
		int[] cellNodeIndices = new int[columns * rows];

		for (int i = 0; i < cellNodeIndices.Length; i++)
		{
			cellNodeIndices[i] = -1;
		}

		for (int i = 2; i < cellIndices.Count; i++)
		{
			int cellIndex = cellIndices[i];
			if (cellIndex >= 0 && cellIndex < cellNodeIndices.Length)
			{
				cellNodeIndices[cellIndex] = i;
			}
		}

		return cellNodeIndices;
	}

	private void ExploreGridRouteSpecialConnections(
		int currentIndex,
		Vector2 sourceWorldPoint,
		float spacing,
		float specialConnectionDistance,
		float[] gScores,
		float[] fScores,
		int[] previous,
		bool[] closed,
		bool[] open,
		List<int> openNodes)
	{
		bool foundNearbyConnection = false;

		for (int i = 2; i < gridRouteWorldNodes.Count; i++)
		{
			if (Vector2.Distance(sourceWorldPoint, gridRouteWorldNodes[i]) > specialConnectionDistance)
			{
				continue;
			}

			if (TryRelaxGridRouteNode(currentIndex, i, gridRouteWorldNodes[1], gScores, fScores, previous, closed, open, openNodes))
			{
				foundNearbyConnection = true;
			}
		}

		if (foundNearbyConnection)
		{
			return;
		}

		int[] closestIndices = new int[GridRouteClosestSpecialConnections];
		float[] closestDistances = new float[GridRouteClosestSpecialConnections];

		for (int i = 0; i < closestIndices.Length; i++)
		{
			closestIndices[i] = -1;
			closestDistances[i] = float.PositiveInfinity;
		}

		float maxDistance = spacing * GridRouteClosestSpecialConnections;

		for (int i = 2; i < gridRouteWorldNodes.Count; i++)
		{
			float distance = Vector2.Distance(sourceWorldPoint, gridRouteWorldNodes[i]);
			if (distance > maxDistance || !IsWalkableWorldSegment(sourceWorldPoint, gridRouteWorldNodes[i]))
			{
				continue;
			}

			for (int slot = 0; slot < closestIndices.Length; slot++)
			{
				if (distance >= closestDistances[slot])
				{
					continue;
				}

				for (int shift = closestIndices.Length - 1; shift > slot; shift--)
				{
					closestIndices[shift] = closestIndices[shift - 1];
					closestDistances[shift] = closestDistances[shift - 1];
				}

				closestIndices[slot] = i;
				closestDistances[slot] = distance;
				break;
			}
		}

		for (int i = 0; i < closestIndices.Length; i++)
		{
			if (closestIndices[i] >= 0)
			{
				TryRelaxGridRouteNode(currentIndex, closestIndices[i], gridRouteWorldNodes[1], gScores, fScores, previous, closed, open, openNodes);
			}
		}
	}

	private bool TryRelaxGridRouteNode(
		int currentIndex,
		int nextIndex,
		Vector2 targetWorldPoint,
		float[] gScores,
		float[] fScores,
		int[] previous,
		bool[] closed,
		bool[] open,
		List<int> openNodes)
	{
		if (closed[nextIndex] || !IsWalkableWorldSegment(gridRouteWorldNodes[currentIndex], gridRouteWorldNodes[nextIndex]))
		{
			return false;
		}

		float nextScore = gScores[currentIndex] + Vector2.Distance(gridRouteWorldNodes[currentIndex], gridRouteWorldNodes[nextIndex]);
		if (nextScore >= gScores[nextIndex])
		{
			return false;
		}

		previous[nextIndex] = currentIndex;
		gScores[nextIndex] = nextScore;
		fScores[nextIndex] = nextScore + Vector2.Distance(gridRouteWorldNodes[nextIndex], targetWorldPoint);

		if (!open[nextIndex])
		{
			open[nextIndex] = true;
			openNodes.Add(nextIndex);
		}

		return true;
	}

	private int PopLowestGridRouteOpenNode(List<int> openNodes, float[] fScores)
	{
		int bestOpenListIndex = 0;
		float bestScore = fScores[openNodes[0]];

		for (int i = 1; i < openNodes.Count; i++)
		{
			float score = fScores[openNodes[i]];
			if (score >= bestScore)
			{
				continue;
			}

			bestOpenListIndex = i;
			bestScore = score;
		}

		int nodeIndex = openNodes[bestOpenListIndex];
		int lastIndex = openNodes.Count - 1;
		openNodes[bestOpenListIndex] = openNodes[lastIndex];
		openNodes.RemoveAt(lastIndex);
		return nodeIndex;
	}

	private bool BuildGridRouteResult(int[] previous, int targetIndex, Vector2 targetPosition, List<Vector2> path)
	{
		gridRouteSmoothedPath.Clear();

		int routeIndex = targetIndex;
		while (routeIndex >= 0)
		{
			gridRouteSmoothedPath.Add(gridRouteWorldNodes[routeIndex]);

			if (routeIndex == 0)
			{
				break;
			}

			routeIndex = previous[routeIndex];
		}

		if (gridRouteSmoothedPath.Count < 2 || gridRouteSmoothedPath[gridRouteSmoothedPath.Count - 1] != gridRouteWorldNodes[0])
		{
			gridRouteSmoothedPath.Clear();
			return false;
		}

		gridRouteSmoothedPath.Reverse();
		SmoothGridRouteWorldPath(gridRouteSmoothedPath);

		for (int i = 1; i < gridRouteSmoothedPath.Count; i++)
		{
			Vector2 logicalPoint = i == gridRouteSmoothedPath.Count - 1
				? targetPosition
				: WalkableWorldToLogicalPoint(gridRouteSmoothedPath[i]);

			if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], logicalPoint) > stopDistance * 0.5f)
			{
				path.Add(logicalPoint);
			}
		}

		return path.Count > 0;
	}

	private void SmoothGridRouteWorldPath(List<Vector2> route)
	{
		if (route.Count <= 2)
		{
			return;
		}

		int anchorIndex = 0;
		int testIndex = 2;

		while (testIndex < route.Count)
		{
			if (IsWalkableWorldSegment(route[anchorIndex], route[testIndex]))
			{
				route.RemoveAt(testIndex - 1);
				continue;
			}

			anchorIndex++;
			testIndex = anchorIndex + 2;
		}
	}

	private void CollectPolygonPathNodes(
		PolygonCollider2D polygon,
		List<Vector2> nodes)
	{
		nodes.Clear();

		for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
		{
			Vector2[] localPath = polygon.GetPath(pathIndex);
			Vector2 pathCenter = GetPolygonPathWorldCenter(polygon, localPath);

			for (int i = 0; i < localPath.Length; i++)
			{
				Vector2 vertexWorldPoint = PolygonLocalPointToWorld(polygon, localPath[i]);

				if (!TryFindWalkablePathNodeNear(vertexWorldPoint, pathCenter, out Vector2 node))
				{
					continue;
				}

				AddDistinctPathNode(nodes, node);
			}
		}
	}

	private void CollectPolygonBlockerPathNodes(List<Vector2> nodes)
	{
		for (int blockerIndex = 0; blockerIndex < walkableBlockers.Count; blockerIndex++)
		{
			PolygonCollider2D blocker = walkableBlockers[blockerIndex] as PolygonCollider2D;
			if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
			{
				continue;
			}

			for (int pathIndex = 0; pathIndex < blocker.pathCount; pathIndex++)
			{
				Vector2[] localPath = blocker.GetPath(pathIndex);
				Vector2 blockerCenter = GetPolygonPathWorldCenter(blocker, localPath);

				for (int i = 0; i < localPath.Length; i++)
				{
					Vector2 vertexWorldPoint = PolygonLocalPointToWorld(blocker, localPath[i]);
					Vector2 awayFromBlocker = vertexWorldPoint - blockerCenter;

					if (awayFromBlocker.sqrMagnitude <= MovementEpsilon)
					{
						awayFromBlocker = vertexWorldPoint - (Vector2)blocker.bounds.center;
					}

					Vector2 preferredWalkablePoint = vertexWorldPoint + awayFromBlocker;
					if (!TryFindWalkablePathNodeNear(vertexWorldPoint, preferredWalkablePoint, out Vector2 node))
					{
						continue;
					}

					AddDistinctPathNode(nodes, node);
				}
			}
		}
	}

	private void PreparePathScratch(int nodeCount)
	{
		pathNodeDistances.Clear();
		pathPreviousNodeIndices.Clear();
		pathVisitedNodes.Clear();

		for (int i = 0; i < nodeCount; i++)
		{
			pathNodeDistances.Add(float.PositiveInfinity);
			pathPreviousNodeIndices.Add(-1);
			pathVisitedNodes.Add(false);
		}
	}

	private int FindNearestUnvisitedPathNode(int nodeCount)
	{
		int bestIndex = -1;
		float bestDistance = float.PositiveInfinity;

		for (int i = 0; i < nodeCount; i++)
		{
			if (pathVisitedNodes[i] || pathNodeDistances[i] >= bestDistance)
			{
				continue;
			}

			bestIndex = i;
			bestDistance = pathNodeDistances[i];
		}

		return bestIndex;
	}

	private bool TryFindWalkablePathNodeNear(Vector2 vertexWorldPoint, Vector2 preferredInsideWorldPoint, out Vector2 node)
	{
		node = Vector2.zero;
		float inset = GetPathNodeInsetDistance();
		Vector2 preferredDirection = preferredInsideWorldPoint - vertexWorldPoint;

		if (TryNudgePathNodeInside(vertexWorldPoint, preferredDirection, inset, out node))
		{
			return true;
		}

		Vector2 boundsDirection = (Vector2)walkableFloor.bounds.center - vertexWorldPoint;
		if (TryNudgePathNodeInside(vertexWorldPoint, boundsDirection, inset, out node))
		{
			return true;
		}

		for (int ring = 1; ring <= PathNodeRadialRings; ring++)
		{
			float radius = inset * ring;

			for (int sample = 0; sample < PathNodeRadialSamples; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / PathNodeRadialSamples;
				Vector2 candidate = vertexWorldPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

				if (IsWalkableWorldPoint(candidate))
				{
					node = candidate;
					return true;
				}
			}
		}

		if (IsWalkableWorldPoint(vertexWorldPoint))
		{
			node = vertexWorldPoint;
			return true;
		}

		return false;
	}

	private bool TryNudgePathNodeInside(Vector2 vertexWorldPoint, Vector2 direction, float inset, out Vector2 node)
	{
		node = Vector2.zero;

		if (direction.sqrMagnitude <= MovementEpsilon)
		{
			return false;
		}

		direction.Normalize();

		for (int i = 1; i <= PathNodeRadialRings; i++)
		{
			Vector2 candidate = vertexWorldPoint + direction * (inset * i);
			if (IsWalkableWorldPoint(candidate))
			{
				node = candidate;
				return true;
			}
		}

		return false;
	}

	private void AddDistinctPathNode(List<Vector2> nodes, Vector2 node)
	{
		float minDistance = Mathf.Max(GetPathNodeInsetDistance() * 0.5f, MovementEpsilon * 8f);
		float minSqrDistance = minDistance * minDistance;

		for (int i = 0; i < nodes.Count; i++)
		{
			if ((nodes[i] - node).sqrMagnitude <= minSqrDistance)
			{
				return;
			}
		}

		nodes.Add(node);
	}

	private Vector2 GetPolygonPathWorldCenter(PolygonCollider2D polygon, Vector2[] localPath)
	{
		if (localPath == null || localPath.Length == 0)
		{
			return walkableFloor != null ? (Vector2)walkableFloor.bounds.center : Vector2.zero;
		}

		Vector2 center = Vector2.zero;

		for (int i = 0; i < localPath.Length; i++)
		{
			center += PolygonLocalPointToWorld(polygon, localPath[i]);
		}

		return center / localPath.Length;
	}

	private static Vector2 PolygonLocalPointToWorld(PolygonCollider2D polygon, Vector2 localPoint)
	{
		return polygon.transform.TransformPoint(localPoint + polygon.offset);
	}

	private static Vector2 PolygonWorldPointToLocal(PolygonCollider2D polygon, Vector2 worldPoint)
	{
		return (Vector2)polygon.transform.InverseTransformPoint(worldPoint) - polygon.offset;
	}

	private float GetPathSegmentProbeSpacing()
	{
		if (walkableFloor == null)
		{
			return WalkableInsetStep;
		}

		Bounds bounds = walkableFloor.bounds;
		float shortestSize = Mathf.Min(bounds.size.x, bounds.size.y);
		return Mathf.Max(WalkableInsetStep, shortestSize / 160f);
	}

	private float GetGridRouteSpacing(out int columns, out int rows)
	{
		columns = 0;
		rows = 0;

		if (walkableFloor == null)
		{
			return WalkableInsetStep;
		}

		Bounds bounds = walkableFloor.bounds;
		float shortestSize = Mathf.Max(WalkableInsetStep, Mathf.Min(bounds.size.x, bounds.size.y));
		float spacing = Mathf.Max(WalkableInsetStep, shortestSize / 48f);

		for (int attempt = 0; attempt < 16; attempt++)
		{
			columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / spacing) + 1);
			rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / spacing) + 1);

			if (columns * rows <= GridRouteMaxNodeCount)
			{
				return spacing;
			}

			spacing *= 1.18f;
		}

		columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / spacing) + 1);
		rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / spacing) + 1);
		return spacing;
	}

	private float GetClickProjectionMaxWorldDistance()
	{
		if (walkableFloor == null)
		{
			return ClickProjectionMinWorldDistance;
		}

		Bounds bounds = walkableFloor.bounds;
		float shortestSize = Mathf.Min(bounds.size.x, bounds.size.y);
		return Mathf.Clamp(
			shortestSize / 16f,
			ClickProjectionMinWorldDistance,
			ClickProjectionMaxWorldDistance);
	}

	private float GetPathNodeInsetDistance()
	{
		if (walkableFloor == null)
		{
			return WalkableInsetStep;
		}

		Bounds bounds = walkableFloor.bounds;
		float shortestSize = Mathf.Min(bounds.size.x, bounds.size.y);
		return Mathf.Max(WalkableInsetStep, shortestSize / 400f);
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
		if (IsWalkableWorldPoint(worldPoint))
			return point;

		Vector2 closestPoint = walkableFloor.ClosestPoint(worldPoint);
		if (IsWalkableWorldPoint(closestPoint))
			return WalkableWorldToLogicalPoint(closestPoint);

		Vector2 preferredWorldPoint = LogicalToWalkableWorldPoint(preferredInsidePoint);
		Vector2 insetDirection = preferredWorldPoint - closestPoint;
		if (insetDirection.sqrMagnitude <= MovementEpsilon || !IsWalkableWorldPoint(preferredWorldPoint))
			insetDirection = (Vector2)walkableFloor.bounds.center - closestPoint;

		if (insetDirection.sqrMagnitude <= MovementEpsilon)
			return WalkableWorldToLogicalPoint(closestPoint);

		insetDirection.Normalize();

		for (int i = 1; i <= WalkableInsetAttempts; i++)
		{
			Vector2 insetPoint = closestPoint + insetDirection * (WalkableInsetStep * i);
			if (IsWalkableWorldPoint(insetPoint))
				return WalkableWorldToLogicalPoint(insetPoint);
		}

		for (int i = 1; i <= WalkableInsetAttempts; i++)
		{
			float radius = WalkableInsetStep * i;

			for (int sample = 0; sample < WalkableInsetRadialSamples; sample++)
			{
				float angle = (Mathf.PI * 2f * sample) / WalkableInsetRadialSamples;
				Vector2 radialPoint = closestPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

				if (IsWalkableWorldPoint(radialPoint))
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

		if (IsWalkableWorldPoint(targetWorldPoint))
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
		if (!IsWalkableWorldPoint(candidateWorldPoint))
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

	private bool TryFindRoomContentForRoom(string roomName, out RoomContentGroup roomContent)
	{
		roomContent = null;

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

			roomContent = room;
			return true;
		}

		return false;
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

	private void RefreshWalkableBlockersForCurrentRoom(string roomName, Collider2D roomBoundary)
	{
		walkableBlockers.Clear();

		if (string.IsNullOrWhiteSpace(roomName) ||
			string.IsNullOrWhiteSpace(roomBoundaryBlockerNamePrefix) ||
			!TryFindRoomContentForRoom(roomName, out RoomContentGroup room))
		{
			return;
		}

		string blockerPrefixKey = NormalizeBoundaryName(roomBoundaryBlockerNamePrefix);
		Collider2D[] colliders = room.GetComponentsInChildren<Collider2D>(true);

		for (int i = 0; i < colliders.Length; i++)
		{
			Collider2D candidate = colliders[i];
			if (candidate == null || candidate == roomBoundary || !candidate.enabled)
			{
				continue;
			}

			string candidateKey = NormalizeBoundaryName(candidate.name);
			if (!candidateKey.StartsWith(blockerPrefixKey, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			walkableBlockers.Add(candidate);
		}
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

	private static float SanitizeButlerScale(float scale)
	{
		return Mathf.Max(0.001f, float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : scale);
	}

	private static bool ContainsRoomName(List<string> rooms, string roomId)
	{
		if (rooms == null || string.IsNullOrWhiteSpace(roomId))
		{
			return false;
		}

		for (int i = 0; i < rooms.Count; i++)
		{
			if (SameRoomName(rooms[i], roomId))
			{
				return true;
			}
		}

		return false;
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
		if (!applyPlayerSorting)
		{
			return;
		}

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

	private readonly struct PolygonRouteConnection
	{
		public PolygonRouteConnection(int from, int to)
		{
			From = from;
			To = to;
		}

		public int From { get; }
		public int To { get; }
	}

	private readonly struct AuthoredRendererSorting
	{
		public AuthoredRendererSorting(SpriteRenderer renderer)
		{
			Renderer = renderer;
			SortingLayerId = renderer.sortingLayerID;
			SortingOrder = renderer.sortingOrder;
			SpriteSortPoint = renderer.spriteSortPoint;
		}

		public SpriteRenderer Renderer { get; }
		public int SortingLayerId { get; }
		public int SortingOrder { get; }
		public SpriteSortPoint SpriteSortPoint { get; }
	}

	[Serializable]
	private sealed class ButlerRoomScaleOverride
	{
		[SerializeField] private string roomId;
		[SerializeField] private bool hasFront;
		[SerializeField] private float frontFootY;
		[SerializeField] private float frontScale = 1f;
		[SerializeField] private bool hasBack;
		[SerializeField] private float backFootY;
		[SerializeField] private float backScale = 1f;

		private ButlerRoomScaleOverride()
		{
			roomId = string.Empty;
			frontScale = 1f;
			backScale = 1f;
		}

		public ButlerRoomScaleOverride(string roomId)
		{
			this.roomId = CleanRoomName(roomId);
			frontScale = 1f;
			backScale = 1f;
		}

		public string RoomId => CleanRoomName(roomId);

		public void SetFront(float roomLocalFootY, float scale)
		{
			roomId = CleanRoomName(roomId);
			hasFront = true;
			frontFootY = roomLocalFootY;
			frontScale = SanitizeButlerScale(scale);
		}

		public void SetBack(float roomLocalFootY, float scale)
		{
			roomId = CleanRoomName(roomId);
			hasBack = true;
			backFootY = roomLocalFootY;
			backScale = SanitizeButlerScale(scale);
		}

		public ButlerRoomScaleOverrideData ToData()
		{
			return new ButlerRoomScaleOverrideData(
				roomId,
				hasFront,
				frontFootY,
				frontScale,
				hasBack,
				backFootY,
				backScale);
		}

		public bool Matches(string otherRoomId)
		{
			return SameRoomName(roomId, otherRoomId);
		}
	}

}
