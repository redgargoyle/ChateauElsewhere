using System;
using System.Collections.Generic;
using Chateau.World.Navigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CanonicalPassage = Chateau.World.Rooms.Passages.Passage;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
public class DoorTriggerNavigation : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const string DiagnosticPrefix = "[Ch2ClickDiag]";

    public enum NavigationTriggerKind
    {
        Door,
        Stairway
    }

    public enum StairwayDirection
    {
        Auto,
        Up,
        Down
    }

    private const string DefaultDoorOpenSoundCatalogResourcePath = "Audio/DoorOpenSoundCatalog";
    private const string DefaultStairwaySoundCatalogResourcePath = "Audio/StairwaySoundCatalog";
    private const float ApproachTriggerDistanceWeight = 10f;
    private const float ApproachPlayerDistanceWeight = 0.01f;
    private const float ApproachExactPointPenalty = 25f;
    private const float DuplicateApproachSampleDistance = 1f;
    private const float ApproachSampleMinimumOffset = 36f;

    public static event Action<DoorTriggerNavigation> HoveredTriggerChanged;
    public static DoorTriggerNavigation HoveredTrigger { get; private set; }

    [Header("Door Route")]
    [SerializeField] private string sourceRoom;
    [SerializeField] private string doorName;
    [SerializeField] private string destinationRoom;
    [SerializeField] private bool requirePlayerInSourceRoom = true;
    [SerializeField] private bool useCameraSequence = true;
    [SerializeField] private NavigationTriggerKind triggerKind = NavigationTriggerKind.Door;
    [SerializeField] private StairwayDirection stairwayDirection = StairwayDirection.Auto;

    [Header("References")]
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private CanonicalPassage canonicalPassage;
    [SerializeField] private Image image;
    [SerializeField] private AudioSource doorOpenAudioSource;
    [SerializeField] private Transform player;

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool bringToFront = true;
    [SerializeField] private bool useBottomScreenEdgeInteraction;
    [SerializeField, Min(1f)] private float bottomScreenEdgeActivationPixels = 28f;
    [SerializeField] private bool disableGraphicRaycastForScreenEdgeInteraction = true;

    [Header("Player Proximity")]
    [SerializeField] private bool requirePlayerProximity = true;
    [SerializeField] private bool walkPlayerToTriggerWhenFar = true;
    [SerializeField] private bool autoActivateAfterApproach = true;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private float maxPlayerScreenDistance = 145f;

    [Header("Audio")]
    [SerializeField] private bool playDoorOpenSound = true;
    [SerializeField] private string doorOpenAudioObjectName = "Audio_DoorOpen";
    [SerializeField] private DoorOpenSoundCatalog doorOpenSoundCatalog;
    [SerializeField] private string doorOpenSoundCatalogResourcePath = DefaultDoorOpenSoundCatalogResourcePath;
    [SerializeField] private DoorOpenSoundCatalog stairwaySoundCatalog;
    [SerializeField] private string stairwaySoundCatalogResourcePath = DefaultStairwaySoundCatalogResourcePath;

    public string SourceRoom => GetEffectiveSourceRoom();
    public string DoorName => Clean(doorName);
    public string DestinationRoom => Clean(destinationRoom);
    public bool UsesCameraSequence => useCameraSequence;
    public bool IsStairway => GetEffectiveTriggerKind() == NavigationTriggerKind.Stairway;
    public string InteractionLabel => IsStairway ? "Stairway" : "Door";

    private RectTransform rectTransform;
    private readonly Vector3[] triggerWorldCorners = new Vector3[4];
    private readonly List<Vector2> triggerScreenSamples = new List<Vector2>(16);
    private PointClickPlayerMovement pendingApproachPlayer;
    private int lastPointerActivationFrame = -1;
    private static readonly List<DoorTriggerNavigation> activeTriggers = new List<DoorTriggerNavigation>();
    private static DoorTriggerNavigation fallbackHoveredTrigger;
    private static DoorTriggerNavigation pendingApproachTrigger;
    private static int lastFallbackUpdateFrame = -1;
    private static AudioSource activeNavigationAudioSource;
    private static int lastDoorOpenClipIndex = -1;
    private static int lastStairwayClipIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        HoveredTriggerChanged = null;
        HoveredTrigger = null;
        activeTriggers.Clear();
        fallbackHoveredTrigger = null;
        pendingApproachTrigger = null;
        lastFallbackUpdateFrame = -1;
        activeNavigationAudioSource = null;
        lastDoorOpenClipIndex = -1;
        lastStairwayClipIndex = -1;
    }

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        FillSourceRoomFromHierarchy();
        ConfigureImage();
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveDoorOpenAudioSource();
        ResolveDoorOpenSoundCatalog();
        ConfigureImage();
        BringToFrontIfNeeded();
    }

    private void OnEnable()
    {
        RegisterActiveTrigger(this);
        BringToFrontIfNeeded();
    }

    private void OnValidate()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        FillSourceRoomFromHierarchy();
        maxPlayerScreenDistance = Mathf.Max(1f, maxPlayerScreenDistance);

        ConfigureImage();
    }

    private void OnTransformParentChanged()
    {
        FillSourceRoomFromHierarchy();
    }

    private void OnDisable()
    {
        UnregisterActiveTrigger(this);

        if (HoveredTrigger == this)
        {
            SetHoveredTrigger(null);
        }

        CancelPendingPlayerApproach();
        NavigationCursorController.ClearDoorHover(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (RuntimeSettingsMenu.BlocksGameInput || IsPointerOverAvailableGuestAction(eventData) || IsPlayerInputLocked())
        {
            ClearActiveDoorHover(this);
            return;
        }

        SetActiveDoorHover(this, Vector2.zero, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoveredTrigger == this)
        {
            ClearActiveDoorHover(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (RuntimeSettingsMenu.BlocksGameInput)
        {
            ClearActiveDoorHover(this);
            return;
        }

        if (IsPointerOverAvailableGuestAction(eventData))
        {
            ClearActiveDoorHover(this);
            return;
        }

        if (IsPlayerInputLocked())
        {
            ClearActiveDoorHover(this);
            return;
        }

        ActivateDoor(eventData.position);
    }

    public static bool IsPointerOverActiveTrigger(Vector2 screenPosition)
    {
        if (RuntimeSettingsMenu.BlocksGameInput || !IsPointerInsideScreenBounds(screenPosition))
        {
            return false;
        }

        return FindTopmostTriggerAtScreenPoint(screenPosition) != null;
    }

    public void ActivateDoor()
    {
        ActivateDoor(null);
    }

    private void ActivateDoor(Vector2? preferredScreenPosition)
    {
        if (Application.isPlaying && lastPointerActivationFrame == Time.frameCount)
        {
            return;
        }

        lastPointerActivationFrame = Time.frameCount;
        ActivateDoor(true, preferredScreenPosition);
    }

    private void Update()
    {
        UpdateFallbackPointerHoverAndClick();
    }

    private void ActivateDoor(bool allowPlayerApproach, Vector2? preferredScreenPosition)
    {
        ResolveReferences();

        if (IsPlayerInputLocked())
        {
            CancelPendingPlayerApproach();
            ClearActiveDoorHover(this);
            return;
        }

        if (navigationManager == null)
        {
            Debug.LogWarning($"Door trigger '{name}' could not find a RoomNavigationManager.", this);
            return;
        }

        if (!IsPlayerCloseEnough())
        {
            if (allowPlayerApproach && TryStartPlayerApproach(preferredScreenPosition))
            {
                return;
            }

            Debug.Log($"Move closer to the {InteractionLabel.ToLowerInvariant()} before using it.", this);
            return;
        }

        CancelPendingPlayerApproach();

        if (canonicalPassage != null)
        {
            bool soundStarted = TryPlayNavigationSoundNow();
            INavigationService navigationService = navigationManager;
            bool didNavigate = navigationService.TryTraverse(canonicalPassage);
            StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);
            return;
        }

        if (useCameraSequence)
        {
            // The trigger does not load rooms itself. It asks the navigation
            // manager for the next room in the sequence, and the manager changes
            // the single current-room state that every room system reacts to.
            bool soundStarted = TryPlayNavigationSoundNow();
            bool didNavigate = navigationManager.OpenDoorFromCurrentRoom(string.Empty, DoorName, false);
            StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(destinationRoom))
        {
            // Manual room objects and Inspector fields are the source of truth.
            bool soundStarted = TryPlayNavigationSoundNow();
            bool didNavigate = navigationManager.MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom);
            StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);
            return;
        }

        Debug.LogWarning($"Door trigger '{name}' has no destination room.", this);
    }

    private bool TryStartPlayerApproach(Vector2? preferredScreenPosition)
    {
        if (!walkPlayerToTriggerWhenFar)
        {
            LogApproachFailure("automatic approach is disabled");
            return false;
        }

        ResolvePlayerReference();
        PointClickPlayerMovement playerMovement = player != null ? player.GetComponent<PointClickPlayerMovement>() : null;
        if (playerMovement == null)
        {
            LogApproachFailure("no PointClickPlayerMovement was found on the player");
            return false;
        }

        if (!playerMovement.InputEnabled)
        {
            LogApproachFailure("player input is disabled");
            return false;
        }

        CancelAnyPendingApproach();

        if (!TryFindTraversalApproachDestination(playerMovement, out Vector2 approachDestination, preferredScreenPosition))
        {
            LogApproachFailure("no reachable walkable point could be found near the trigger");
            return false;
        }

        if (!playerMovement.TrySetDestination(approachDestination))
        {
            LogApproachFailure("the selected approach point was rejected by the player movement boundary");
            return false;
        }

        if (!playerMovement.HasDestination)
        {
            if (!IsPlayerCloseEnough())
            {
                LogApproachFailure("the player is already at the closest reachable point but still not close enough");
                return false;
            }

            ActivateDoor(false, null);
            return true;
        }

        pendingApproachTrigger = this;
        pendingApproachPlayer = playerMovement;
        pendingApproachPlayer.MovementStopped += HandlePlayerApproachStopped;
        return true;
    }

    private bool TryFindTraversalApproachDestination(
        PointClickPlayerMovement playerMovement,
        out Vector2 destination,
        Vector2? preferredScreenPosition)
    {
        if (canonicalPassage == null || !canonicalPassage.UsesAuthoredApproach)
        {
            return TryFindBestApproachDestination(
                playerMovement,
                true,
                out destination,
                preferredScreenPosition);
        }

        return TryFindCanonicalApproachDestination(playerMovement, out destination);
    }

    private bool TryFindCanonicalApproachDestination(
        PointClickPlayerMovement playerMovement,
        out Vector2 destination)
    {
        destination = Vector2.zero;
        INavigationService navigationService = navigationManager;

        if (navigationService == null || !navigationService.CanTraverse(canonicalPassage))
        {
            return false;
        }

        Vector2 authoredDestination = canonicalPassage.ApproachAnchor.LogicalPosition;

        if (!playerMovement.TryGetScreenPointFromLogicalPosition(authoredDestination, out Vector2 destinationScreenPoint) ||
            !TryGetTriggerScreenBounds(out Vector2 min, out Vector2 max))
        {
            return false;
        }

        Vector2 closestTriggerPoint = GetClosestApproachPointInTriggerBounds(destinationScreenPoint, min, max);

        if (Vector2.Distance(destinationScreenPoint, closestTriggerPoint) > Mathf.Max(1f, maxPlayerScreenDistance))
        {
            return false;
        }

        destination = authoredDestination;
        return true;
    }

    public bool TryFindArrivalDestination(PointClickPlayerMovement playerMovement, out Vector2 destination)
    {
        ResolveReferences();

        if (TryFindBestApproachDestination(playerMovement, false, out destination))
        {
            return true;
        }

        return TryFindClosestReachableArrivalDestination(playerMovement, out destination);
    }

    private bool TryFindClosestReachableArrivalDestination(PointClickPlayerMovement playerMovement, out Vector2 destination)
    {
        destination = Vector2.zero;

        if (playerMovement == null || rectTransform == null)
        {
            return false;
        }

        Camera canvasCamera = GetCanvasCamera();
        rectTransform.GetWorldCorners(triggerWorldCorners);

        bool foundDestination = false;
        float bestScore = float.MaxValue;
        Vector2 bestDestination = Vector2.zero;

        TryScoreArrivalWorldPoint(playerMovement, (triggerWorldCorners[0] + triggerWorldCorners[3]) * 0.5f, canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, Vector3.Lerp(triggerWorldCorners[0], triggerWorldCorners[3], 0.25f), canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, Vector3.Lerp(triggerWorldCorners[0], triggerWorldCorners[3], 0.75f), canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, (triggerWorldCorners[1] + triggerWorldCorners[2]) * 0.5f, canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, (triggerWorldCorners[0] + triggerWorldCorners[2]) * 0.5f, canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, triggerWorldCorners[0], canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);
        TryScoreArrivalWorldPoint(playerMovement, triggerWorldCorners[3], canvasCamera, ref foundDestination, ref bestScore, ref bestDestination);

        if (!foundDestination)
        {
            return false;
        }

        destination = bestDestination;
        return true;
    }

    private void TryScoreArrivalWorldPoint(
        PointClickPlayerMovement playerMovement,
        Vector3 triggerWorldPoint,
        Camera canvasCamera,
        ref bool foundDestination,
        ref float bestScore,
        ref Vector2 bestDestination)
    {
        if (!playerMovement.TryFindClosestReachableDestinationToWorldPointTowardRoomCenter(triggerWorldPoint, out Vector2 candidateDestination) ||
            !playerMovement.TryGetScreenPointFromLogicalPosition(candidateDestination, out Vector2 candidateScreenPoint))
        {
            return;
        }

        Vector2 triggerScreenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, triggerWorldPoint);
        float score = Vector2.SqrMagnitude(candidateScreenPoint - triggerScreenPoint);

        if (foundDestination && score >= bestScore)
        {
            return;
        }

        foundDestination = true;
        bestScore = score;
        bestDestination = candidateDestination;
    }

    private bool TryFindBestApproachDestination(
        PointClickPlayerMovement playerMovement,
        bool requireMovement,
        out Vector2 destination,
        Vector2? preferredScreenPosition = null)
    {
        destination = Vector2.zero;

        if (playerMovement == null || !TryGetTriggerScreenBounds(out Vector2 min, out Vector2 max))
        {
            return false;
        }

        Vector2 playerScreenPosition = GetPlayerScreenPosition();
        CollectTriggerApproachSamples(playerScreenPosition, min, max, preferredScreenPosition);

        bool foundDestination = false;
        float bestScore = float.MaxValue;
        Vector2 bestDestination = Vector2.zero;

        for (int i = 0; i < triggerScreenSamples.Count; i++)
        {
            Vector2 samplePoint = triggerScreenSamples[i];
            if (!playerMovement.TryEvaluateMovementAtScreenPoint(samplePoint, true, out PointClickPlayerMovement.MovementTargetQuery movementQuery) ||
                !movementQuery.HasReachableDestination ||
                (requireMovement && !movementQuery.WouldMove))
            {
                continue;
            }

            if (!playerMovement.TryGetScreenPointFromLogicalPosition(movementQuery.Destination, out Vector2 destinationScreenPoint))
            {
                continue;
            }

            Vector2 closestTriggerPoint = GetClosestApproachPointInTriggerBounds(destinationScreenPoint, min, max);
            float triggerDistance = Vector2.Distance(destinationScreenPoint, closestTriggerPoint);
            float playerDistance = Vector2.Distance(playerScreenPosition, destinationScreenPoint);
            float score = triggerDistance * ApproachTriggerDistanceWeight +
                playerDistance * ApproachPlayerDistanceWeight +
                (movementQuery.ExactPointWalkable ? 0f : ApproachExactPointPenalty);

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestDestination = movementQuery.Destination;
            foundDestination = true;

            if (preferredScreenPosition.HasValue &&
                i == 0 &&
                movementQuery.ExactPointWalkable &&
                triggerDistance <= 1f)
            {
                destination = bestDestination;
                return true;
            }
        }

        if (!foundDestination)
        {
            return false;
        }

        destination = bestDestination;
        return true;
    }

    private void LogApproachFailure(string reason)
    {
        Debug.Log($"Door trigger '{name}' could not start player approach because {reason}.", this);
    }

    private void HandlePlayerApproachStopped()
    {
        CancelPendingPlayerApproach();

        if (autoActivateAfterApproach && isActiveAndEnabled && IsPlayerCloseEnough())
        {
            ActivateDoor(false, null);
            return;
        }

        if (autoActivateAfterApproach && isActiveAndEnabled)
        {
            float distance = GetPlayerScreenDistanceToTrigger();
            LogApproachFailure(
                $"the player reached the closest approach point but is still {distance:0.#} screen pixels from the {InteractionLabel.ToLowerInvariant()} (limit {Mathf.Max(1f, maxPlayerScreenDistance):0.#})");
        }
    }

    private bool IsPlayerCloseEnough()
    {
        if (!requirePlayerProximity)
        {
            return true;
        }

        ResolvePlayerReference();

        if (player == null)
        {
            Debug.LogWarning($"Door trigger '{name}' requires player proximity but could not find a player transform.", this);
            return true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null || rectTransform == null)
        {
            return true;
        }

        return GetPlayerScreenDistanceToTrigger() <= Mathf.Max(1f, maxPlayerScreenDistance);
    }

    private float GetPlayerScreenDistanceToTrigger()
    {
        Vector2 playerScreenPosition = GetPlayerScreenPosition();
        Vector2 triggerScreenPosition = GetClosestTriggerScreenPoint(playerScreenPosition);
        return Vector2.Distance(playerScreenPosition, triggerScreenPosition);
    }

    private Vector2 GetPlayerScreenPosition()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || player == null)
        {
            return Vector2.zero;
        }

        PointClickPlayerMovement playerMovement = player.GetComponent<PointClickPlayerMovement>();

        if (playerMovement != null &&
            playerMovement.TryGetScreenPointFromLogicalPosition(playerMovement.LogicalPosition, out Vector2 feetScreenPosition))
        {
            return feetScreenPosition;
        }

        return RectTransformUtility.WorldToScreenPoint(mainCamera, player.position);
    }

    private Vector2 GetClosestTriggerScreenPoint(Vector2 screenPosition)
    {
        if (!TryGetTriggerScreenBounds(out Vector2 min, out Vector2 max))
        {
            return screenPosition;
        }

        return GetClosestApproachPointInTriggerBounds(screenPosition, min, max);
    }

    private bool TryGetTriggerScreenBounds(out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        ResolveReferences();
        if (rectTransform == null)
        {
            return false;
        }

        Camera canvasCamera = GetCanvasCamera();
        rectTransform.GetWorldCorners(triggerWorldCorners);

        Vector2 firstCorner = RectTransformUtility.WorldToScreenPoint(canvasCamera, triggerWorldCorners[0]);
        min = firstCorner;
        max = firstCorner;

        for (int i = 1; i < triggerWorldCorners.Length; i++)
        {
            Vector2 corner = RectTransformUtility.WorldToScreenPoint(canvasCamera, triggerWorldCorners[i]);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }

        return true;
    }

    private void CollectTriggerApproachSamples(
        Vector2 playerScreenPosition,
        Vector2 min,
        Vector2 max,
        Vector2? preferredScreenPosition)
    {
        triggerScreenSamples.Clear();

        float centerX = (min.x + max.x) * 0.5f;
        float centerY = (min.y + max.y) * 0.5f;
        float lowerY = min.y;
        float upperY = max.y;
        float leftX = min.x;
        float rightX = max.x;

        if (preferredScreenPosition.HasValue)
        {
            AddUniqueApproachSample(GetClosestApproachPointInTriggerBounds(preferredScreenPosition.Value, min, max));
        }

        AddUniqueApproachSample(GetClosestApproachPointInTriggerBounds(playerScreenPosition, min, max));
        AddUniqueApproachSample(new Vector2(centerX, lowerY));
        AddUniqueApproachSample(new Vector2(Mathf.Lerp(leftX, rightX, 0.25f), lowerY));
        AddUniqueApproachSample(new Vector2(Mathf.Lerp(leftX, rightX, 0.75f), lowerY));
        AddUniqueApproachSample(new Vector2(leftX, lowerY));
        AddUniqueApproachSample(new Vector2(rightX, lowerY));
        AddUniqueApproachSample(new Vector2(centerX, centerY));
        AddUniqueApproachSample(new Vector2(leftX, centerY));
        AddUniqueApproachSample(new Vector2(rightX, centerY));
        AddUniqueApproachSample(new Vector2(centerX, upperY));
        AddUniqueApproachSample(new Vector2(leftX, upperY));
        AddUniqueApproachSample(new Vector2(rightX, upperY));

        float width = Mathf.Max(1f, rightX - leftX);
        float height = Mathf.Max(1f, upperY - lowerY);
        float offset = Mathf.Max(ApproachSampleMinimumOffset, Mathf.Min(width, height) * 0.35f);

        AddDoorEdgeApproachSamples(leftX, rightX, centerX, lowerY, -offset);
        AddDoorEdgeApproachSamples(leftX, rightX, centerX, lowerY, -offset * 2f);
        AddDoorEdgeApproachSamples(leftX, rightX, centerX, upperY, offset);
        AddDoorEdgeApproachSamples(leftX, rightX, centerX, upperY, offset * 2f);
        AddUniqueApproachSample(new Vector2(leftX - offset, centerY));
        AddUniqueApproachSample(new Vector2(leftX - offset * 2f, centerY));
        AddUniqueApproachSample(new Vector2(rightX + offset, centerY));
        AddUniqueApproachSample(new Vector2(rightX + offset * 2f, centerY));
    }

    private void AddDoorEdgeApproachSamples(float leftX, float rightX, float centerX, float edgeY, float yOffset)
    {
        float sampleY = edgeY + yOffset;
        AddUniqueApproachSample(new Vector2(centerX, sampleY));
        AddUniqueApproachSample(new Vector2(Mathf.Lerp(leftX, rightX, 0.25f), sampleY));
        AddUniqueApproachSample(new Vector2(Mathf.Lerp(leftX, rightX, 0.75f), sampleY));
    }

    private void AddUniqueApproachSample(Vector2 sample)
    {
        for (int i = 0; i < triggerScreenSamples.Count; i++)
        {
            if (Vector2.Distance(triggerScreenSamples[i], sample) <= DuplicateApproachSampleDistance)
            {
                return;
            }
        }

        triggerScreenSamples.Add(sample);
    }

    private static Vector2 GetClosestApproachPointInTriggerBounds(Vector2 screenPosition, Vector2 min, Vector2 max)
    {
        return new Vector2(
            Mathf.Clamp(screenPosition.x, min.x, max.x),
            min.y);
    }

    private void ResolvePlayerReference()
    {
        if (IsUsablePlayerTransform(player))
        {
            return;
        }

        player = null;

        string cleanPlayerObjectName = Clean(playerObjectName);

        if (!string.IsNullOrEmpty(cleanPlayerObjectName))
        {
            GameObject playerObject = GameObject.Find(cleanPlayerObjectName);

            if (TryGetUsablePlayerMovement(playerObject, out PointClickPlayerMovement namedPlayerMovement))
            {
                player = namedPlayerMovement.transform;
                return;
            }
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                string.Equals(candidate.gameObject.name, cleanPlayerObjectName, StringComparison.OrdinalIgnoreCase) &&
                IsUsablePlayerMovement(candidate))
            {
                player = candidate.transform;
                return;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (IsUsablePlayerMovement(candidate))
            {
                player = candidate.transform;
                return;
            }
        }
    }

    private static bool TryGetUsablePlayerMovement(GameObject candidateObject, out PointClickPlayerMovement playerMovement)
    {
        playerMovement = candidateObject != null ? candidateObject.GetComponent<PointClickPlayerMovement>() : null;
        return IsUsablePlayerMovement(playerMovement);
    }

    private static bool IsUsablePlayerTransform(Transform candidate)
    {
        return candidate != null &&
            TryGetUsablePlayerMovement(candidate.gameObject, out _);
    }

    private static bool IsUsablePlayerMovement(PointClickPlayerMovement candidate)
    {
        if (candidate == null ||
            !candidate.enabled ||
            !candidate.gameObject.activeInHierarchy ||
            IsLikelyChapterGuest(candidate.gameObject))
        {
            return false;
        }

        return true;
    }

    private static bool IsLikelyChapterGuest(GameObject candidateObject)
    {
        if (candidateObject == null)
        {
            return false;
        }

        string candidateName = candidateObject.name.Trim();
        return candidateName.StartsWith("Guest", StringComparison.OrdinalIgnoreCase);
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private bool ContainsScreenPoint(Vector2 screenPosition)
    {
        ResolveReferences();

        if (useBottomScreenEdgeInteraction)
        {
            return IsCurrentRoomSourceForScreenEdgeInteraction() &&
                IsBottomScreenEdgePoint(screenPosition);
        }

        return rectTransform != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, GetCanvasCamera());
    }

    private bool IsBottomScreenEdgePoint(Vector2 screenPosition)
    {
        float edgePixels = Mathf.Max(1f, bottomScreenEdgeActivationPixels);
        return screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= edgePixels;
    }

    private bool IsCurrentRoomSourceForScreenEdgeInteraction()
    {
        if (!requirePlayerInSourceRoom)
        {
            return true;
        }

        ResolveReferences();

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return true;
        }

        string cleanSourceRoom = SourceRoom;
        return string.IsNullOrWhiteSpace(cleanSourceRoom) ||
            string.Equals(cleanSourceRoom, navigationManager.CurrentRoom, StringComparison.OrdinalIgnoreCase);
    }

    private static void RegisterActiveTrigger(DoorTriggerNavigation trigger)
    {
        if (trigger != null && !activeTriggers.Contains(trigger))
        {
            activeTriggers.Add(trigger);
        }
    }

    private static void UnregisterActiveTrigger(DoorTriggerNavigation trigger)
    {
        activeTriggers.Remove(trigger);

        if (fallbackHoveredTrigger == trigger)
        {
            ClearActiveDoorHover(trigger);
        }

        if (pendingApproachTrigger == trigger)
        {
            pendingApproachTrigger = null;
        }
    }

    private static void UpdateFallbackPointerHoverAndClick()
    {
        if (!Application.isPlaying || lastFallbackUpdateFrame == Time.frameCount)
        {
            return;
        }

        lastFallbackUpdateFrame = Time.frameCount;

        if (RuntimeSettingsMenu.BlocksGameInput)
        {
            ClearActiveDoorHover(fallbackHoveredTrigger);
            return;
        }

        if (!TryGetPointerPosition(out Vector2 screenPosition))
        {
            ClearActiveDoorHover(fallbackHoveredTrigger);
            return;
        }

        if (Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(screenPosition))
        {
            ClearActiveDoorHover(fallbackHoveredTrigger);
            return;
        }

        DoorTriggerNavigation triggerUnderPointer = FindTopmostTriggerAtScreenPoint(screenPosition);
        if (triggerUnderPointer != null && triggerUnderPointer.IsPlayerInputLocked())
        {
            triggerUnderPointer = null;
        }

        SetActiveDoorHover(triggerUnderPointer, screenPosition, true);

        bool primaryPointerDown = TryGetPrimaryPointerDown();

        if (triggerUnderPointer != null && primaryPointerDown)
        {
            triggerUnderPointer.LogDoorFallbackDiagnostic("hover/click", screenPosition, true, true);
            triggerUnderPointer.ActivateDoor(screenPosition);
        }
    }

    private static DoorTriggerNavigation FindTopmostTriggerAtScreenPoint(Vector2 screenPosition)
    {
        DoorTriggerNavigation bestTrigger = null;

        for (int i = 0; i < activeTriggers.Count; i++)
        {
            DoorTriggerNavigation candidate = activeTriggers[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (candidate.ContainsScreenPoint(screenPosition))
            {
                bestTrigger = candidate;
            }
        }

        return bestTrigger;
    }

    private static bool IsPointerOverAvailableGuestAction(PointerEventData eventData)
    {
        return eventData != null &&
            Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(eventData.position);
    }

    private bool IsPlayerInputLocked()
    {
        ResolvePlayerReference();
        PointClickPlayerMovement playerMovement = player != null ? player.GetComponent<PointClickPlayerMovement>() : null;
        return playerMovement != null && !playerMovement.InputEnabled;
    }

    private static bool TryGetPointerPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            return IsPointerInsideScreenBounds(screenPosition);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            screenPosition = Input.mousePosition;
            return IsPointerInsideScreenBounds(screenPosition);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static bool IsPointerInsideScreenBounds(Vector2 screenPosition)
    {
        return Screen.width > 0 &&
            Screen.height > 0 &&
            screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height;
    }

    private static bool TryGetPrimaryPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return Input.GetMouseButtonDown(0);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static void CancelAnyPendingApproach()
    {
        if (pendingApproachTrigger != null)
        {
            pendingApproachTrigger.CancelPendingPlayerApproach();
        }
    }

    private void CancelPendingPlayerApproach()
    {
        if (pendingApproachPlayer != null)
        {
            pendingApproachPlayer.MovementStopped -= HandlePlayerApproachStopped;
            pendingApproachPlayer = null;
        }

        if (pendingApproachTrigger == this)
        {
            pendingApproachTrigger = null;
        }
    }

    private static void SetActiveDoorHover(DoorTriggerNavigation trigger, Vector2 screenPosition, bool fromFallback)
    {
        if (fallbackHoveredTrigger == trigger)
        {
            return;
        }

        DoorTriggerNavigation previousTrigger = fallbackHoveredTrigger;
        ClearActiveDoorHover(fallbackHoveredTrigger);
        fallbackHoveredTrigger = trigger;

        if (trigger == null)
        {
            SetHoveredTrigger(null);
            if (fromFallback)
            {
                LogDoorFallbackHoverChange(previousTrigger, null, screenPosition);
            }
            return;
        }

        SetHoveredTrigger(trigger);
        NavigationCursorController.SetDoorHover(trigger, trigger.GetNavigationCursorIcon(), true);

        if (fromFallback)
        {
            LogDoorFallbackHoverChange(previousTrigger, trigger, screenPosition);
        }
    }

    private static void ClearActiveDoorHover(DoorTriggerNavigation trigger)
    {
        if (trigger == null)
        {
            return;
        }

        if (fallbackHoveredTrigger == trigger)
        {
            fallbackHoveredTrigger = null;
        }

        if (HoveredTrigger == trigger)
        {
            SetHoveredTrigger(null);
        }

        NavigationCursorController.SetDoorHover(trigger, false);
    }

    private static void LogDoorFallbackHoverChange(DoorTriggerNavigation previousTrigger, DoorTriggerNavigation nextTrigger, Vector2 screenPosition)
    {
        DoorTriggerNavigation logTrigger = nextTrigger != null ? nextTrigger : previousTrigger;

        if (logTrigger == null)
        {
            return;
        }

        string previousName = previousTrigger != null ? previousTrigger.name : "<none>";
        string nextName = nextTrigger != null ? nextTrigger.name : "<none>";
        logTrigger.LogDoorFallbackDiagnostic(
            $"hover-change previous={previousName} next={nextName}",
            screenPosition,
            false,
            nextTrigger != null);
    }

    private void LogDoorFallbackDiagnostic(string eventName, Vector2 screenPosition, bool activating, bool setCursorHover)
    {
        Debug.Log(
            $"{DiagnosticPrefix} DoorFallback {eventName} frame={Time.frameCount} " +
            $"trigger={name} currentRoom={GetCurrentRoomForDiagnostic()} sourceRoom={SourceRoom} " +
            $"screen={FormatDiagnosticVector(screenPosition)} activating={activating} setCursorHover={setCursorHover}",
            this);
    }

    private string GetCurrentRoomForDiagnostic()
    {
        return navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom)
            ? "<none>"
            : navigationManager.CurrentRoom;
    }

    private static string FormatDiagnosticVector(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    public void RefreshInferredSourceRoom()
    {
        FillSourceRoomFromHierarchy();
    }

    private NavigationCursorController.HoverIcon GetNavigationCursorIcon()
    {
        if (!IsStairway)
        {
            return NavigationCursorController.HoverIcon.Door;
        }

        switch (GetEffectiveStairwayDirection())
        {
            case StairwayDirection.Up:
                return NavigationCursorController.HoverIcon.StairsUp;
            case StairwayDirection.Down:
                return NavigationCursorController.HoverIcon.StairsDown;
            default:
                return NavigationCursorController.HoverIcon.Stairway;
        }
    }

    private StairwayDirection GetEffectiveStairwayDirection()
    {
        if (stairwayDirection != StairwayDirection.Auto)
        {
            return stairwayDirection;
        }

        if (ContainsNavigationToken(name, "Down") ||
            ContainsNavigationToken(DoorName, "Down") ||
            ContainsNavigationToken(DestinationRoom, "Downstairs") ||
            ContainsNavigationToken(DestinationRoom, "Lower"))
        {
            return StairwayDirection.Down;
        }

        if (ContainsNavigationToken(name, "Up") ||
            ContainsNavigationToken(DoorName, "Up") ||
            ContainsNavigationToken(DestinationRoom, "Upstairs") ||
            ContainsNavigationToken(DestinationRoom, "Upper"))
        {
            return StairwayDirection.Up;
        }

        if (ContainsNavigationToken(SourceRoom, "Upper") &&
            !ContainsNavigationToken(DestinationRoom, "Upper"))
        {
            return StairwayDirection.Down;
        }

        if (!ContainsNavigationToken(SourceRoom, "Upper") &&
            ContainsNavigationToken(DestinationRoom, "Upper"))
        {
            return StairwayDirection.Up;
        }

        return StairwayDirection.Auto;
    }

    private static bool ContainsNavigationToken(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private NavigationTriggerKind GetEffectiveTriggerKind()
    {
        if (triggerKind == NavigationTriggerKind.Stairway)
        {
            return NavigationTriggerKind.Stairway;
        }

        if (name.StartsWith("StairwayTrigger_", StringComparison.OrdinalIgnoreCase) ||
            DoorName.IndexOf("Stairway", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return NavigationTriggerKind.Stairway;
        }

        return NavigationTriggerKind.Door;
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void ConfigureImage()
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = ShouldUseGraphicRaycast();

        if (makeInvisibleAtRuntime && Application.isPlaying)
        {
            image.color = runtimeColor;
        }
    }

    private bool ShouldUseGraphicRaycast()
    {
        return !useBottomScreenEdgeInteraction || !disableGraphicRaycastForScreenEdgeInteraction;
    }

    private bool TryPlayNavigationSoundNow()
    {
        if (!playDoorOpenSound)
        {
            return false;
        }

        ResolveDoorOpenAudioSource();

        if (doorOpenAudioSource == null)
        {
            return false;
        }

        StopCurrentNavigationSound();
        activeNavigationAudioSource = doorOpenAudioSource;
        doorOpenAudioSource.Stop();
        GameAudioSettings.EnsureBinding(
            doorOpenAudioSource,
            GameAudioChannel.GameSounds,
            GameAudioSettings.GetCurrentOrBoundBaseVolume(doorOpenAudioSource, GameAudioChannel.GameSounds));

        if (TryGetNavigationClip(out AudioClip randomClip))
        {
            if (GameAudioSettings.TryPlayOneShot(doorOpenAudioSource, randomClip))
            {
                return true;
            }

            activeNavigationAudioSource = null;
            return false;
        }

        if (!IsStairway && doorOpenAudioSource.clip != null)
        {
            if (GameAudioSettings.TryPlayOneShot(doorOpenAudioSource, doorOpenAudioSource.clip))
            {
                return true;
            }

            activeNavigationAudioSource = null;
            return false;
        }

        activeNavigationAudioSource = null;
        return false;
    }

    private void StopNavigationSoundIfNavigationFailed(bool soundStarted, bool didNavigate)
    {
        if (soundStarted && !didNavigate && activeNavigationAudioSource == doorOpenAudioSource)
        {
            StopCurrentNavigationSound();
        }
    }

    private static void StopCurrentNavigationSound()
    {
        if (activeNavigationAudioSource != null)
        {
            activeNavigationAudioSource.Stop();
            activeNavigationAudioSource = null;
        }
    }

    private void ResolveDoorOpenAudioSource()
    {
        if (doorOpenAudioSource != null)
        {
            return;
        }

        GameObject audioObject = GameObject.Find(doorOpenAudioObjectName);

        if (audioObject != null)
        {
            doorOpenAudioSource = audioObject.GetComponent<AudioSource>();
        }
    }

    private bool TryGetNavigationClip(out AudioClip clip)
    {
        clip = null;

        if (IsStairway)
        {
            ResolveStairwaySoundCatalog();
            return stairwaySoundCatalog != null && stairwaySoundCatalog.TryGetRandomClip(ref lastStairwayClipIndex, out clip);
        }

        ResolveDoorOpenSoundCatalog();

        return doorOpenSoundCatalog != null && doorOpenSoundCatalog.TryGetRandomClip(ref lastDoorOpenClipIndex, out clip);
    }

    private void ResolveDoorOpenSoundCatalog()
    {
        if (doorOpenSoundCatalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(doorOpenSoundCatalogResourcePath)
                ? DefaultDoorOpenSoundCatalogResourcePath
                : doorOpenSoundCatalogResourcePath.Trim();

            doorOpenSoundCatalog = Resources.Load<DoorOpenSoundCatalog>(resourcePath);
        }
    }

    private void ResolveStairwaySoundCatalog()
    {
        if (stairwaySoundCatalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(stairwaySoundCatalogResourcePath)
                ? DefaultStairwaySoundCatalogResourcePath
                : stairwaySoundCatalogResourcePath.Trim();

            stairwaySoundCatalog = Resources.Load<DoorOpenSoundCatalog>(resourcePath);
        }
    }

    private void BringToFrontIfNeeded()
    {
        if (!Application.isPlaying || !bringToFront || transform.parent == null)
        {
            return;
        }

        transform.SetAsLastSibling();
    }

    private static void SetHoveredTrigger(DoorTriggerNavigation trigger)
    {
        if (HoveredTrigger == trigger)
        {
            return;
        }

        HoveredTrigger = trigger;
        HoveredTriggerChanged?.Invoke(trigger);
    }

    private string GetEffectiveSourceRoom()
    {
        string hierarchySourceRoom = InferSourceRoomFromHierarchy(transform);

        if (!string.IsNullOrWhiteSpace(hierarchySourceRoom))
        {
            return hierarchySourceRoom;
        }

        return Clean(sourceRoom);
    }

    private void FillSourceRoomFromHierarchy()
    {
        sourceRoom = InferSourceRoomFromHierarchy(transform);
    }

    private static string InferSourceRoomFromHierarchy(Transform current)
    {
        while (current != null)
        {
            string parsedRoomName = ParseRoomNameFromObject(current.name);

            if (!string.IsNullOrEmpty(parsedRoomName))
            {
                return parsedRoomName;
            }

            current = current.parent;
        }

        return string.Empty;
    }

    private static string ParseRoomNameFromObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }
        else if (cleanName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Room_".Length);
        }
        else
        {
            return string.Empty;
        }

        return cleanName.Replace('_', ' ').Trim();
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
