using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter1ArrivalController : Chateau.Architecture.ChapterControllerBase
{
    private sealed class GuestRuntimeState
    {
        public GuestArrivalConfig Config;
        public GuestArrivalState State;
        public int GuestIndex;
        public int GroupIndex;
        public bool WaitingOutside;
        public bool EnteredEntranceHall;
        public bool Annoyed;
        public bool CoatOffered;
        public bool CoatTaken;
        public bool CoatStored;
        public bool MovingToDrawingRoom;
        public bool Seated;
        public bool Handled;
        public float QueuedAtGameMinute;
        public Transform Seat;
        public GameObject GuestObject;
        public Chapter1CoatPickup CoatPickup;
        public NPCWaypointMover Mover;
        public ActorRoomState ActorState;
        public RoomProjectedEntity Projection;
        public GuestFootstepAudio Footsteps;
        public GuestScaleParticipant ScaleParticipant;
    }

    private sealed class GuestGroupRuntimeState
    {
        public int GroupIndex;
        public int ArrivalHour;
        public int ArrivalMinute;
        public bool EmptyRing;
        public bool QueuedOutside;
        public bool EnteredEntranceHall;
        public bool MovingToDrawingRoom;
        public bool Complete;
        public float QueuedAtGameMinute;
        public readonly List<GuestRuntimeState> Guests = new List<GuestRuntimeState>();
    }

    private sealed class RendererSortingState
    {
        public string LayerName;
        public int Order;
    }

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private ChapterEventScheduler eventScheduler;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private GameObject playerButlerReference;
    [SerializeField] private CoatCloset coatCloset;
    [SerializeField] private DoorbellSystem doorbellSystem;
    [SerializeField] private ChapterTimeSettingsUI timeSettingsUI;
    [SerializeField] private Chapter1InteractionHUD interactionHUD;
    [SerializeField] private Chapter1SceneAction frontDoorSceneAction;
    [SerializeField] private GuestRoomScaleApplier guestRoomScaleApplier;

    [Header("Rooms")]
    [SerializeField] private string entryRoomId = "Grand Entrance Hall";
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private RoomContentGroup entryRoomContent;
    [SerializeField] private RoomContentGroup drawingRoomContent;

    [Header("Required Anchors")]
    [SerializeField] private Transform frontDoorArrivalPoint;
    [SerializeField] private Transform guestEntranceSpawnPlacemark;
    [SerializeField] private Transform entranceHallGuestAnchor;
    [SerializeField, FormerlySerializedAs("butlerDoorSpot")] private Transform drawingRoomSideButlerSpot;
    [SerializeField] private Transform closetPoint;
    [SerializeField] private Transform drawingRoomEntryPoint;
    [SerializeField] private Transform drawingRoomDoorTarget;
    [SerializeField] private Transform[] drawingRoomGuestPoints = Array.Empty<Transform>();

    [Header("Clock Timeline")]
    [SerializeField, Range(0, 23)] private int firstArrivalHour = 18;
    [SerializeField, Range(0, 59)] private int firstArrivalMinute = 0;
    [SerializeField, Min(1)] private int guestGroupCount = 4;
    [SerializeField, Min(1)] private int guestsPerArrivalGroup = 2;
    [SerializeField, Range(0, 23)] private int emptyDoorbellHour = 18;
    [SerializeField, Range(0, 59)] private int emptyDoorbellMinute = 4;
    [SerializeField, Min(1f)] private float chapter1SecondsPerGameMinute = 35f;

    [Header("Guests")]
    [SerializeField] private List<GuestArrivalConfig> guests = new List<GuestArrivalConfig>();
    [SerializeField] private bool useExistingSceneGuestsFirst = true;
    [SerializeField] private float entranceGuestSpacing = 95f;
    [SerializeField] private float guestMoveSpeed = 180f;
    [SerializeField] private float worldEntranceGuestSpacing = 0.65f;
    [SerializeField] private float worldDrawingRoomSeatSpacing = 0.75f;
    [SerializeField] private float worldGuestMoveSpeed = 2.2f;

    [Header("Guest Footsteps")]
    [SerializeField] private GuestFootstepCatalog guestFootstepCatalog;
    [SerializeField] private bool playGuestFootsteps = true;

    [Header("Entrance Sorting")]
    [SerializeField] private string entranceGuestSortingLayerName = "People";
    [SerializeField] private int entranceGuestSortingOrderBase = 9000;
    [SerializeField] private int entranceGuestSortingOrderGroupStep = 100;
    [SerializeField] private int entranceGuestSortingOrderSlotStep = 10;

    [Header("Interactions")]
    [SerializeField] private bool snapGuestsIntoEntranceForFirstVisualPass = true;
    [SerializeField] private bool autoStoreCoatIfClosetMissing = false;
    [SerializeField] private float coatOffsetX = 34f;
    [SerializeField] private float coatOffsetY = 42f;

    [Header("Subtitles")]
    [SerializeField] private bool enableSubtitles = true;
    [SerializeField] private bool subtitleDebugMode;
    [SerializeField] private SubtitleService subtitleService;
    [SerializeField] private DialogueSpeechService speechService;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private readonly List<GuestRuntimeState> guestStates = new List<GuestRuntimeState>();
    private readonly List<GuestGroupRuntimeState> guestGroups = new List<GuestGroupRuntimeState>();
    private readonly List<GuestGroupRuntimeState> pendingGuestGroups = new List<GuestGroupRuntimeState>();
    private readonly List<GuestGroupRuntimeState> activeEntranceGroups = new List<GuestGroupRuntimeState>();
    private readonly HashSet<GameObject> runtimeGeneratedGuestObjects = new HashSet<GameObject>();
    private readonly Dictionary<Renderer, RendererSortingState> authoredGuestRendererSorting = new Dictionary<Renderer, RendererSortingState>();
    private readonly Dictionary<string, Sprite> guestCoatSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private int currentGuestIndex = -1;
    private bool sequenceActive;
    private bool chapterCompletionRequested;
    private bool finalEmptyDoorbellOccurred;
    private bool emptyDoorbellWaitingForAnswer;
    private bool butlerCarryingCoat;
    private string carriedCoatId = string.Empty;
    private GuestRuntimeState carriedCoatGuest;
    private GuestRuntimeState pendingCoatPickupGuest;
    private bool guestScaleConfigurationErrorLogged;
    private Chapter1CoatPickup pendingCoatPickup;
    private bool hasPendingClosetApproachDestination;
    private Vector2 pendingClosetApproachDestination;
    private GameObject carriedCoatVisual;
    private Sprite runtimeGuestSprite;
    private bool subscribedToRoomChanges;
    private bool hasWorldDoorCenterPosition;
    private Vector3 worldDoorCenterPosition;
    private bool hasFrontDoorAnswerSpot;
    private Vector2 frontDoorAnswerSpot;
    private Coroutine guestRoomVisibilityRefreshRoutine;
    private const float CoatPickupReadyScreenDistance = 90f;
    private const float ClosetStorageReadyScreenDistance = 90f;
    private const float FrontDoorReadyScreenDistance = 90f;
    private const float FrontDoorApproachSampleRadius = 160f;
    private const float EntranceWaitDepthStepMultiplier = 0.32f;
    private const float EntranceWaitSlotSpacingMultiplier = 1.3f;
    private const float EntranceWaitGroupSideStepMultiplier = -0.32f;
    private const int EntranceBanisterSafeWalkingSortingOrder = 1599;
    private const string GuestCoatResourceFolder = "Chapter1/GuestCoats";
    private const string GuestInterruptedLineText = "You inturrupted me.";
    private static readonly Vector3 WorldCoatOffset = new Vector3(0.25f, 0.45f, 0f);
    private static readonly Vector3 ButlerCarriedCoatOffset = new Vector3(0.43f, 1.08f, 0f);
    private static readonly Vector3 AssignedCoatFallbackScale = new Vector3(0.4f, 0.4f, 1f);
    private static readonly Vector2 WorldCoatColliderSize = new Vector2(0.35f, 0.25f);
    private static readonly string[][] ChapterGuestNameAliases =
    {
        new[] { "Guest1", "Guest 1", "Guest01", "Miss Isolde Wren", "Lady" },
        new[] { "Guest2", "Guest 2", "Guest02", "Professor Lucien Vale", "Butler Guest" },
        new[] { "Guest3", "Guest 3", "Guest03", "Mister Florian Knell" },
        new[] { "Guest4", "Guest 4", "Guest04", "Countess Elowen Dusk" },
        new[] { "Guest5", "Guest 5", "Guest05", "Baron Hector Glass" },
        new[] { "Guest6", "Guest 6", "Guest06", "Lady Sabine Marrow" },
        new[] { "Guest7", "Guest 7", "Guest07", "Lord Ambrose Veil" },
        new[] { "Guest8", "Guest 8", "Guest08", "Madame Coralie Thread" }
    };
    private static readonly string[] ChapterGuestDisplayNames =
    {
        "Miss Isolde Wren",
        "Professor Lucien Vale",
        "Mister Florian Knell",
        "Countess Elowen Dusk",
        "Baron Hector Glass",
        "Lady Sabine Marrow",
        "Lord Ambrose Veil",
        "Madame Coralie Thread"
    };
    public int CurrentGuestIndex => currentGuestIndex;
    public bool ButlerCarryingCoat => butlerCarryingCoat;
    public string CarriedCoatId => carriedCoatId;
    public bool IsFrontDoorActionAvailable => IsFrontDoorActionAvailableNow();

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (chapterManager == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized ChapterManager.", this);
        }

        if (chapterClock == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized ChapterClock.", this);
        }

        if (eventScheduler == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized ChapterEventScheduler.", this);
        }

        if (cameraManager == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized CameraManager.", this);
        }

        if (navigationManager == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized RoomNavigationManager.", this);
        }

        if (playerMovement == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized Player movement owner.", this);
        }

        if (playerMovement != null &&
            playerButlerReference != null &&
            playerButlerReference != playerMovement.gameObject)
        {
            report.AddError("Chapter1ArrivalController Player movement and Butler root must identify the same actor.", this);
        }

        if (frontDoorSceneAction == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized front-door action.", this);
        }
        else
        {
            if (!frontDoorSceneAction.enabled ||
                !frontDoorSceneAction.IsConfiguredFor(Chapter1SceneActionType.FrontDoor, this))
            {
                report.AddError("Chapter1ArrivalController requires its enabled front-door action to target this controller.", this);
            }

            BoxCollider2D frontDoorCollider = frontDoorSceneAction.GetComponent<BoxCollider2D>();

            if (frontDoorCollider == null || !frontDoorCollider.enabled || !frontDoorCollider.isTrigger)
            {
                report.AddError("Chapter1ArrivalController front-door action requires its enabled authored trigger collider.", this);
            }
        }

        if (doorbellSystem == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized DoorbellSystem.", this);
        }
        else
        {
            if (!doorbellSystem.IsConfiguredFor(gameObject, chapterClock))
            {
                report.AddError("Chapter1ArrivalController requires its same-owner doorbell to use the serialized ChapterClock.", this);
            }

            doorbellSystem.ValidateConfiguration(report);
        }

        if (coatCloset == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized Entrance coat closet.", this);
        }

        if (closetPoint == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized Entrance closet approach point.", this);
        }
        else if (coatCloset != null && closetPoint != coatCloset.transform)
        {
            report.AddError("Chapter1ArrivalController coat closet and approach point must share the authored Entrance hanger.", this);
        }

        if (guestFootstepCatalog == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized guest footstep catalog.", this);
        }

        if (entryRoomContent == null || !SameRoom(entryRoomContent.RoomName, entryRoomId))
        {
            report.AddError("Chapter1ArrivalController requires its serialized Entrance room-content owner.", this);
        }

        if (drawingRoomContent == null || !SameRoom(drawingRoomContent.RoomName, drawingRoomId))
        {
            report.AddError("Chapter1ArrivalController requires its serialized Drawing Room content owner.", this);
        }

        if (frontDoorArrivalPoint == null ||
            guestEntranceSpawnPlacemark == null ||
            entranceHallGuestAnchor == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized Entrance guest anchors.", this);
        }

        if (drawingRoomEntryPoint == null || drawingRoomDoorTarget == null)
        {
            report.AddError("Chapter1ArrivalController requires its serialized Drawing Room transition anchors.", this);
        }

        if (entryRoomContent != null &&
            ((guestEntranceSpawnPlacemark != null && !guestEntranceSpawnPlacemark.IsChildOf(entryRoomContent.transform)) ||
             (drawingRoomDoorTarget != null && !drawingRoomDoorTarget.IsChildOf(entryRoomContent.transform))))
        {
            report.AddError("Chapter1ArrivalController Entrance placemark and Drawing Room door target must belong to the Entrance room owner.", this);
        }

        if (drawingRoomGuestPoints == null || drawingRoomGuestPoints.Length != 8)
        {
            report.AddError("Chapter1ArrivalController requires exactly eight serialized Drawing Room guest points.", this);
        }
        else
        {
            HashSet<Transform> uniqueGuestPoints = new HashSet<Transform>();

            for (int i = 0; i < drawingRoomGuestPoints.Length; i++)
            {
                Transform guestPoint = drawingRoomGuestPoints[i];

                if (guestPoint == null ||
                    !uniqueGuestPoints.Add(guestPoint) ||
                    (drawingRoomContent != null && !guestPoint.IsChildOf(drawingRoomContent.transform)))
                {
                    report.AddError("Chapter1ArrivalController Drawing Room guest points must be unique authored children of the Drawing Room owner.", this);
                    break;
                }

                RoomAnchor guestPointAnchor = guestPoint.GetComponent<RoomAnchor>();
                string expectedAnchorId = $"DrawingRoomGuestPoint_{i + 1:00}";

                if (guestPointAnchor == null ||
                    !string.Equals(guestPointAnchor.AnchorId, expectedAnchorId, StringComparison.Ordinal) ||
                    !SameRoom(guestPointAnchor.RoomId, drawingRoomId))
                {
                    report.AddError(
                        $"Chapter1ArrivalController Drawing Room guest point slot {i + 1} must reference ordered RoomAnchor '{expectedAnchorId}' in room '{drawingRoomId}'.",
                        this);
                    break;
                }
            }
        }
    }

    public bool CanTakeCoat(string coatId)
    {
        if (butlerCarryingCoat)
        {
            return false;
        }

        GuestRuntimeState guestState = FindGuestByCoat(coatId);
        return guestState != null &&
            guestState.CoatOffered &&
            !guestState.CoatTaken &&
            !guestState.CoatStored;
    }

    private void Awake()
    {
        ResolveReferences(false);
    }

    private void OnEnable()
    {
        if (sequenceActive && !chapterCompletionRequested)
        {
            SubscribeToRoomChanges();
        }
    }

    private void OnDisable()
    {
        CancelPendingCoatPickup();
        CancelPendingClosetStorage();
        StopAllGuestFootsteps();

        if (guestRoomVisibilityRefreshRoutine != null)
        {
            StopCoroutine(guestRoomVisibilityRefreshRoutine);
            guestRoomVisibilityRefreshRoutine = null;
        }

        UnsubscribeFromRoomChanges();
    }

    public void BeginChapter1(ChapterManager manager)
    {
        if (!AcceptsManagerCommandFrom(manager))
        {
            return;
        }

        ResolveReferences();
        ValidateRequiredReferences();
        ResetChapterRuntime();
        EnsureRuntimeInteractionSystems();
        SubscribeToRoomChanges();

        sequenceActive = true;
        chapterClock?.SetSecondsPerGameMinute(chapter1SecondsPerGameMinute);
        ScheduleArrivalTimeline();
        RefreshInteractionState();
        Debug.Log("Chapter 1 entrance hall sequence armed at 5:59 PM.", this);
    }

    private bool AcceptsManagerCommandFrom(ChapterManager manager)
    {
        if (chapterManager == null)
        {
            Debug.LogError("Chapter1ArrivalController rejected a command because its serialized ChapterManager is missing.", this);
            return false;
        }

        if (manager != null && manager != chapterManager)
        {
            Debug.LogError("Chapter1ArrivalController rejected a command from a different ChapterManager.", this);
            return false;
        }

        return true;
    }

    public void PrepareGuestsForChapterStart()
    {
        ResolveReferences(true);
        EnsureRuntimeInteractionSystems();
        ResetGuestStates(false);
        SetChapterSceneGuestsActive(false);
    }

    public void PrepareGuestsForChapter2Skip()
    {
        ResolveReferences(true);

        StopAllCoroutines();
        guestRoomVisibilityRefreshRoutine = null;
        UnsubscribeFromRoomChanges();
        DisableAllChapter1CoatPickupsForChapter2Skip();

        sequenceActive = false;
        chapterCompletionRequested = true;
        finalEmptyDoorbellOccurred = true;
        emptyDoorbellWaitingForAnswer = false;
        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
        carriedCoatGuest = null;
        currentGuestIndex = -1;

        if (carriedCoatVisual != null)
        {
            carriedCoatVisual.SetActive(false);
            carriedCoatVisual = null;
        }

        CancelPendingCoatPickup();
        CancelPendingClosetStorage();
        doorbellSystem?.StopRinging();
        pendingGuestGroups.Clear();
        activeEntranceGroups.Clear();
        guestGroups.Clear();

        ResetGuestStates(true);
        coatCloset?.ClearStoredCoats();

        StageRequiredGuestsInDrawingRoomForChapter2();
    }

    public void HideGuestCoatsForChapter2Skip()
    {
        for (int i = 0; i < guestStates.Count; i++)
        {
            HideGuestCoatVisualsForChapter2Skip(guestStates[i]);
        }

        HideAllGuestCoatVisualsForChapter2Skip();
    }

    public void RefreshChapter2SkipGuestVisibilityAfterRoomChange()
    {
        ResolveReferences(true);

        ResetGuestStates(true);
        int requiredGuestCount = GetRequiredGuestCountForCurrentRun();
        int stagedGuestCount = StageRequiredGuestsInDrawingRoomForChapter2();
        LogChapter2SkipGuestVisibility(requiredGuestCount, stagedGuestCount);
    }

    [ContextMenu("Trigger Next Guest Group")]
    public void TriggerNextGuest()
    {
        ResolveReferences();

        if (!sequenceActive)
        {
            Debug.LogWarning("Chapter 1 next guest debug trigger ignored because the arrival sequence is not active.", this);
            return;
        }

        GuestGroupRuntimeState nextGroup = FindNextUnqueuedGuestGroup();

        if (nextGroup == null)
        {
            Debug.Log("Chapter 1 next guest debug trigger found no unqueued guest group.", this);
            return;
        }

        QueueGuestGroupOutside(nextGroup);
    }

    public void AnswerFrontDoor()
    {
        ResolveReferences();
        Debug.Log("Front door action received.", this);

        if (!sequenceActive && !HasDoorAnswerWaiting())
        {
            Debug.Log("Front door clicked, but Chapter 1 arrival sequence is not active.", this);
            return;
        }

        if (pendingGuestGroups.Count == 0)
        {
            if (emptyDoorbellWaitingForAnswer || IsDoorbellRinging())
            {
                emptyDoorbellWaitingForAnswer = false;
                doorbellSystem?.StopRinging();
                Debug.Log("The butler answers the door. No one is there.", this);
                ShowSubtitleLine("SUB_CH01_BUTLER_EMPTY_DOOR_001");
                RefreshInteractionState();
                CheckChapterCompletionGate();
                return;
            }

            Debug.Log("Front door clicked, but no guests are waiting outside.", this);
            return;
        }

        List<GuestGroupRuntimeState> groupsToAdmit = new List<GuestGroupRuntimeState>(pendingGuestGroups);
        pendingGuestGroups.Clear();
        doorbellSystem?.StopRinging();
        StartCoroutine(AdmitQueuedGuestGroups(groupsToAdmit));
        RefreshInteractionState();
    }

    public bool TryGetFrontDoorApproachDestination(PointClickPlayerMovement movement, out Vector2 destination)
    {
        destination = Vector2.zero;
        ResolveReferences();

        if (movement == null)
        {
            return false;
        }

        if (hasFrontDoorAnswerSpot)
        {
            destination = frontDoorAnswerSpot;
            return true;
        }

        Transform target = GetFrontDoorInteractionTransform();

        if (target == null)
        {
            return false;
        }

        if (movement.TryFindClosestReachableDestinationToWorldPoint(target.position, out destination))
        {
            RememberFrontDoorAnswerSpot(destination);
            return true;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return false;
        }

        Vector2 doorScreenPosition = mainCamera.WorldToScreenPoint(target.position);
        Vector2 playerScreenPosition = Vector2.zero;

        if (!movement.TryGetScreenPointFromLogicalPosition(movement.LogicalPosition, out playerScreenPosition) &&
            playerButlerReference != null)
        {
            playerScreenPosition = mainCamera.WorldToScreenPoint(playerButlerReference.transform.position);
        }

        bool foundDestination = false;
        float bestScore = float.MaxValue;
        Vector2 bestDestination = Vector2.zero;

        TryConsiderFrontDoorApproachSample(movement, doorScreenPosition, playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);

        for (int radiusStep = 1; radiusStep <= 3; radiusStep++)
        {
            float radius = FrontDoorApproachSampleRadius * radiusStep / 3f;
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(0f, -radius), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(-radius, -radius), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(radius, -radius), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(-radius, 0f), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(radius, 0f), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
            TryConsiderFrontDoorApproachSample(movement, doorScreenPosition + new Vector2(0f, radius), playerScreenPosition, doorScreenPosition, ref foundDestination, ref bestScore, ref bestDestination);
        }

        if (!foundDestination)
        {
            return false;
        }

        destination = bestDestination;
        RememberFrontDoorAnswerSpot(destination);
        return true;
    }

    public bool IsButlerCloseToFrontDoor(PointClickPlayerMovement movement = null)
    {
        ResolveReferences();

        PointClickPlayerMovement effectiveMovement = movement != null ? movement : playerMovement;

        if (effectiveMovement == null || !TryGetFrontDoorApproachDestination(effectiveMovement, out Vector2 answerSpot))
        {
            return false;
        }

        Vector2 butlerScreenPosition;

        if (!effectiveMovement.TryGetScreenPointFromLogicalPosition(effectiveMovement.LogicalPosition, out butlerScreenPosition))
        {
            return false;
        }

        if (!effectiveMovement.TryGetScreenPointFromLogicalPosition(answerSpot, out Vector2 answerSpotScreenPosition))
        {
            return false;
        }

        return Vector2.Distance(butlerScreenPosition, answerSpotScreenPosition) <= FrontDoorReadyScreenDistance;
    }

    private Transform GetFrontDoorInteractionTransform()
    {
        if (frontDoorArrivalPoint != null)
        {
            return frontDoorArrivalPoint;
        }

        return frontDoorSceneAction != null ? frontDoorSceneAction.transform : null;
    }

    private static void TryConsiderFrontDoorApproachSample(
        PointClickPlayerMovement movement,
        Vector2 sampleScreenPosition,
        Vector2 playerScreenPosition,
        Vector2 doorScreenPosition,
        ref bool foundDestination,
        ref float bestScore,
        ref Vector2 bestDestination)
    {
        if (!movement.TryEvaluateMovementAtScreenPoint(sampleScreenPosition, true, out PointClickPlayerMovement.MovementTargetQuery query) ||
            !query.HasReachableDestination)
        {
            return;
        }

        if (!movement.TryGetScreenPointFromLogicalPosition(query.Destination, out Vector2 destinationScreenPosition))
        {
            return;
        }

        float doorDistance = Vector2.Distance(destinationScreenPosition, doorScreenPosition);
        float playerDistance = Vector2.Distance(destinationScreenPosition, playerScreenPosition);
        float score = doorDistance * 10f + playerDistance * 0.01f + (query.ExactPointWalkable ? 0f : 25f);

        if (foundDestination && score >= bestScore)
        {
            return;
        }

        foundDestination = true;
        bestScore = score;
        bestDestination = query.Destination;
    }

    private void RememberFrontDoorAnswerSpot(Vector2 answerSpot)
    {
        frontDoorAnswerSpot = answerSpot;
        hasFrontDoorAnswerSpot = true;
    }

    public void HandleCoatClicked(Chapter1CoatPickup coatPickup)
    {
        if (coatPickup == null)
        {
            return;
        }

        if (butlerCarryingCoat)
        {
            Debug.Log($"[Chapter1] Butler already holding coat {carriedCoatId}.", this);
            ShowSubtitleLine("SUB_CH01_BUTLER_ONE_COAT_001");
            return;
        }

        GuestRuntimeState guestState = FindGuestByCoat(coatPickup.CoatId);

        if (guestState == null || !guestState.CoatOffered || guestState.CoatTaken || guestState.CoatStored)
        {
            Debug.Log("That coat is not ready to be taken.", this);
            return;
        }

        if (!IsButlerCloseToCoat(coatPickup))
        {
            WalkButlerToCoat(guestState, coatPickup);
            return;
        }

        TakeGuestCoat(guestState);
    }

    private void TakeGuestCoat(GuestRuntimeState guestState)
    {
        if (guestState == null || butlerCarryingCoat)
        {
            if (butlerCarryingCoat)
            {
                Debug.Log($"[Chapter1] Butler already holding coat {carriedCoatId}.", this);
            }

            return;
        }

        DialogueSpeechService.SpeechInterruption speechInterruption = CancelSpeechForCoatPickup();

        guestState.CoatTaken = true;
        butlerCarryingCoat = true;
        carriedCoatId = guestState.Config.CoatId;
        carriedCoatGuest = guestState;
        SetGuestState(guestState, GuestArrivalState.CoatTaken);

        if (speechInterruption.HadActiveSpeech &&
            TryFindGuestForSpeechInterruption(speechInterruption, out GuestRuntimeState interruptedGuest))
        {
            QueueGuestLine(interruptedGuest, "INTERRUPTED", GuestInterruptedLineText);
        }
        else if (!speechInterruption.HadAnySpeech)
        {
            QueueButlerLine("SUB_CH01_BUTLER_TAKE_COAT_001");
            QueueGuestLine(guestState, "COAT_HANDOFF", null);
        }

        if (guestState.CoatPickup != null)
        {
            TransferCoatVisualToButler(guestState);
            DisableCoatPickupInteraction(guestState.CoatPickup);
        }

        RefreshGuestScalingNow();
        Debug.Log($"Coat taken from guest: {carriedCoatId}", this);
        RefreshInteractionState();
        CheckActiveGroupsReadyForDrawingRoom();
    }

    private void TransferCoatVisualToButler(GuestRuntimeState guestState)
    {
        ResolveReferences();

        if (guestState == null || guestState.CoatPickup == null)
        {
            return;
        }

        Transform butlerTransform = playerButlerReference != null
            ? playerButlerReference.transform
            : playerMovement != null
                ? playerMovement.transform
                : null;

        if (butlerTransform == null)
        {
            return;
        }

        GameObject coatObject = guestState.CoatPickup.gameObject;
        coatObject.SetActive(true);
        coatObject.transform.SetParent(butlerTransform, false);
        coatObject.transform.localPosition = GetCoatOffsetWithSpritePivot(coatObject, ButlerCarriedCoatOffset);
        coatObject.transform.localRotation = Quaternion.identity;
        BringCoatRenderersAboveButler(coatObject, butlerTransform);
        carriedCoatVisual = coatObject;

        Debug.Log($"[Chapter1] Coat transferred to butler from guest {guestState.Config.GuestId}.", this);
    }

    private static void BringCoatRenderersAboveButler(GameObject coatObject, Transform butlerTransform)
    {
        if (coatObject == null || butlerTransform == null)
        {
            return;
        }

        SpriteRenderer butlerRenderer = butlerTransform.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer[] coatRenderers = coatObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < coatRenderers.Length; i++)
        {
            SpriteRenderer coatRenderer = coatRenderers[i];

            if (coatRenderer == null)
            {
                continue;
            }

            coatRenderer.enabled = true;

            if (butlerRenderer != null)
            {
                coatRenderer.sortingLayerID = butlerRenderer.sortingLayerID;
                coatRenderer.sortingOrder = butlerRenderer.sortingOrder + 1;
            }
        }
    }

    private void DisableCoatPickupInteraction(Chapter1CoatPickup coatPickup)
    {
        if (coatPickup == null)
        {
            return;
        }

        Collider2D collider = coatPickup.GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.enabled = false;
        }

        coatPickup.enabled = false;
    }

    private void WalkButlerToCoat(GuestRuntimeState guestState, Chapter1CoatPickup coatPickup)
    {
        if (guestState == null || coatPickup == null)
        {
            return;
        }

        ResolveReferences();
        CancelPendingCoatPickup();

        Camera mainCamera = Camera.main;

        if (playerMovement == null || mainCamera == null)
        {
            Debug.LogWarning("Coat clicked, but the butler cannot walk to it because the player movement reference is missing.", this);
            return;
        }

        Vector2 coatScreenPosition = TryGetGuestFeetScreenPosition(guestState, out Vector2 guestFeetScreenPosition)
            ? guestFeetScreenPosition
            : mainCamera.WorldToScreenPoint(coatPickup.transform.position);

        if (!playerMovement.TryEvaluateMovementAtScreenPoint(coatScreenPosition, true, out PointClickPlayerMovement.MovementTargetQuery movementQuery) ||
            !movementQuery.HasReachableDestination)
        {
            Debug.LogWarning($"Coat clicked for guest {guestState.Config.GuestId}, but the butler could not find a reachable coat pickup spot.", this);
            return;
        }

        if (!playerMovement.TrySetDestination(movementQuery.Destination))
        {
            Debug.LogWarning($"Coat clicked for guest {guestState.Config.GuestId}, but the butler could not walk to the selected coat pickup spot.", this);
            return;
        }

        pendingCoatPickupGuest = guestState;
        pendingCoatPickup = coatPickup;

        Debug.Log($"[Chapter1] Butler walking to coat for guest {guestState.Config.GuestId}.", this);

        if (!playerMovement.HasDestination)
        {
            CompletePendingCoatPickup();
            return;
        }

        playerMovement.MovementStopped += HandleCoatPickupMovementStopped;
    }

    private void HandleCoatPickupMovementStopped()
    {
        CompletePendingCoatPickup();
    }

    private void CompletePendingCoatPickup()
    {
        GuestRuntimeState guestState = pendingCoatPickupGuest;
        Chapter1CoatPickup coatPickup = pendingCoatPickup;
        CancelPendingCoatPickup();

        if (guestState == null || coatPickup == null)
        {
            return;
        }

        if (butlerCarryingCoat)
        {
            Debug.Log($"[Chapter1] Butler already holding coat {carriedCoatId}.", this);
            ShowSubtitleLine("SUB_CH01_BUTLER_ONE_COAT_001");
            return;
        }

        if (guestState.CoatTaken || guestState.CoatStored || !IsButlerCloseToCoat(coatPickup))
        {
            return;
        }

        Debug.Log($"[Chapter1] Butler reached coat for guest {guestState.Config.GuestId}.", this);
        TakeGuestCoat(guestState);
    }

    private void CancelPendingCoatPickup()
    {
        if (playerMovement != null)
        {
            playerMovement.MovementStopped -= HandleCoatPickupMovementStopped;
        }

        pendingCoatPickupGuest = null;
        pendingCoatPickup = null;
    }

    private bool IsButlerCloseToCoat(Chapter1CoatPickup coatPickup)
    {
        ResolveReferences();

        if (coatPickup == null || playerMovement == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return false;
        }

        GuestRuntimeState guestState = FindGuestByCoat(coatPickup.CoatId);
        Vector2 coatScreenPosition = TryGetGuestFeetScreenPosition(guestState, out Vector2 guestFeetScreenPosition)
            ? guestFeetScreenPosition
            : mainCamera.WorldToScreenPoint(coatPickup.transform.position);

        if (!playerMovement.TryGetScreenPointFromLogicalPosition(playerMovement.LogicalPosition, out Vector2 butlerScreenPosition))
        {
            if (playerButlerReference == null)
            {
                return false;
            }

            butlerScreenPosition = mainCamera.WorldToScreenPoint(playerButlerReference.transform.position);
        }

        return Vector2.Distance(butlerScreenPosition, coatScreenPosition) <= CoatPickupReadyScreenDistance;
    }

    private bool TryGetGuestFeetScreenPosition(GuestRuntimeState guestState, out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (guestState == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return false;
        }

        RoomProjectedEntity projection = ResolveGuestProjection(guestState);

        if (projection != null && projection.IsProjectionActive)
        {
            screenPosition = mainCamera.WorldToScreenPoint(projection.transform.position);
            return true;
        }

        if (TryGetVisibleFeetWorldPoint(guestState.GuestObject, true, out Vector3 feetWorldPoint))
        {
            screenPosition = mainCamera.WorldToScreenPoint(feetWorldPoint);
            return true;
        }

        if (guestState.GuestObject != null)
        {
            screenPosition = mainCamera.WorldToScreenPoint(guestState.GuestObject.transform.position);
            return true;
        }

        return false;
    }

    private static bool TryGetVisibleFeetWorldPoint(GameObject root, bool ignoreCoatRenderers, out Vector3 feetWorldPoint)
    {
        return TryGetGuestFeetWorldPoint(root, ignoreCoatRenderers, false, out feetWorldPoint);
    }

    private static bool TryGetGuestFeetWorldPoint(
        GameObject root,
        bool ignoreCoatRenderers,
        bool includeInactiveRenderers,
        out Vector3 feetWorldPoint)
    {
        feetWorldPoint = Vector3.zero;

        if (root == null)
        {
            return false;
        }

        Bounds combinedBounds = default;
        bool hasBounds = false;

        AccumulateGuestRendererBounds(root, ignoreCoatRenderers, includeInactiveRenderers, ref combinedBounds, ref hasBounds);

        if (!hasBounds && ignoreCoatRenderers)
        {
            AccumulateGuestRendererBounds(root, false, includeInactiveRenderers, ref combinedBounds, ref hasBounds);
        }

        AccumulateGuestGraphicBounds(root, includeInactiveRenderers, ref combinedBounds, ref hasBounds);

        if (!hasBounds)
        {
            return false;
        }

        feetWorldPoint = new Vector3(combinedBounds.center.x, combinedBounds.min.y, combinedBounds.center.z);
        return true;
    }

    private static void AccumulateVisibleRendererBounds(
        GameObject root,
        bool ignoreCoatRenderers,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        AccumulateGuestRendererBounds(root, ignoreCoatRenderers, false, ref combinedBounds, ref hasBounds);
    }

    private static void AccumulateGuestRendererBounds(
        GameObject root,
        bool ignoreCoatRenderers,
        bool includeInactiveRenderers,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                (ignoreCoatRenderers && IsCoatVisualTransform(renderer.transform)))
            {
                continue;
            }

            if (!includeInactiveRenderers &&
                (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
            {
                continue;
            }

            IncludeBounds(renderer.bounds, ref combinedBounds, ref hasBounds);
        }
    }

    private static void AccumulateVisibleGraphicBounds(
        GameObject root,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        AccumulateGuestGraphicBounds(root, false, ref combinedBounds, ref hasBounds);
    }

    private static void AccumulateGuestGraphicBounds(
        GameObject root,
        bool includeInactiveRenderers,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        Vector3[] corners = new Vector3[4];

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                graphic.rectTransform == null)
            {
                continue;
            }

            if (!includeInactiveRenderers &&
                (!graphic.enabled || !graphic.gameObject.activeInHierarchy))
            {
                continue;
            }

            graphic.rectTransform.GetWorldCorners(corners);
            Bounds graphicBounds = new Bounds(corners[0], Vector3.zero);

            for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                graphicBounds.Encapsulate(corners[cornerIndex]);
            }

            IncludeBounds(graphicBounds, ref combinedBounds, ref hasBounds);
        }
    }

    private static void IncludeBounds(Bounds bounds, ref Bounds combinedBounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            combinedBounds = bounds;
            hasBounds = true;
            return;
        }

        combinedBounds.Encapsulate(bounds);
    }

    public void HandleClosetClicked()
    {
        ResolveReferences();

        if (!butlerCarryingCoat)
        {
            Debug.Log("Closet clicked, but the butler is not carrying a coat.", this);
            ShowSubtitleLine("SUB_CH01_BUTLER_NO_COAT_001");
            return;
        }

        if (!IsButlerCloseToCloset())
        {
            WalkButlerToCloset();
            return;
        }

        StoreCarriedCoatInCloset();
    }

    private void WalkButlerToCloset()
    {
        ResolveReferences();
        CancelPendingClosetStorage();

        if (playerMovement == null)
        {
            Debug.LogWarning("Closet clicked, but the butler cannot walk to it because the player movement reference is missing.", this);
            return;
        }

        if (!TryGetClosetApproachDestination(out Vector2 destination))
        {
            Debug.LogWarning("Closet clicked, but no reachable coat-hanger approach point could be found.", this);
            return;
        }

        if (!playerMovement.TrySetDestination(destination, true))
        {
            Debug.LogWarning("Closet clicked, but the butler could not walk to the selected coat-hanger approach point.", this);
            return;
        }

        pendingClosetApproachDestination = destination;
        hasPendingClosetApproachDestination = true;

        if (!playerMovement.HasDestination)
        {
            CompletePendingClosetStorage();
            return;
        }

        playerMovement.MovementStopped += HandleClosetStorageMovementStopped;
    }

    private void HandleClosetStorageMovementStopped()
    {
        CompletePendingClosetStorage();
    }

    private void CompletePendingClosetStorage()
    {
        bool hasDestination = hasPendingClosetApproachDestination;
        Vector2 destination = pendingClosetApproachDestination;
        CancelPendingClosetStorage();

        if (!butlerCarryingCoat ||
            !(hasDestination
                ? IsButlerCloseToCloset(destination)
                : IsButlerCloseToCloset()))
        {
            return;
        }

        StoreCarriedCoatInCloset();
    }

    private void CancelPendingClosetStorage()
    {
        if (playerMovement != null)
        {
            playerMovement.MovementStopped -= HandleClosetStorageMovementStopped;
        }

        hasPendingClosetApproachDestination = false;
        pendingClosetApproachDestination = Vector2.zero;
    }

    private bool IsButlerCloseToCloset()
    {
        ResolveReferences();

        if (playerMovement == null || !TryGetClosetApproachDestination(out Vector2 destination))
        {
            return false;
        }

        return IsButlerCloseToCloset(destination);
    }

    private bool IsButlerCloseToCloset(Vector2 destination)
    {
        if (playerMovement == null ||
            !playerMovement.TryGetScreenPointFromLogicalPosition(playerMovement.LogicalPosition, out Vector2 butlerScreenPosition) ||
            !playerMovement.TryGetScreenPointFromLogicalPosition(destination, out Vector2 closetScreenPosition))
        {
            return false;
        }

        return Vector2.Distance(butlerScreenPosition, closetScreenPosition) <= ClosetStorageReadyScreenDistance;
    }

    private bool TryGetClosetApproachDestination(out Vector2 destination)
    {
        destination = Vector2.zero;

        if (playerMovement == null)
        {
            return false;
        }

        if (TryGetClosetApproachScreenPosition(out Vector2 screenPosition) &&
            playerMovement.TryEvaluateMovementAtScreenPoint(screenPosition, true, out PointClickPlayerMovement.MovementTargetQuery movementQuery) &&
            movementQuery.HasReachableDestination)
        {
            destination = movementQuery.Destination;
            return true;
        }

        Transform target = closetPoint != null
            ? closetPoint
            : coatCloset != null ? coatCloset.transform : null;

        return target != null &&
            playerMovement.TryFindClosestReachableDestinationToWorldPoint(target.position, out destination);
    }

    private bool TryGetClosetApproachScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return false;
        }

        GameObject closetObject = coatCloset != null
            ? coatCloset.gameObject
            : closetPoint != null ? closetPoint.gameObject : null;

        if (TryGetVisibleFeetWorldPoint(closetObject, false, out Vector3 feetWorldPoint))
        {
            screenPosition = mainCamera.WorldToScreenPoint(feetWorldPoint);
            return true;
        }

        Transform target = closetPoint != null
            ? closetPoint
            : coatCloset != null ? coatCloset.transform : null;

        if (target == null)
        {
            return false;
        }

        screenPosition = mainCamera.WorldToScreenPoint(target.position);
        return true;
    }

    private void StoreCarriedCoatInCloset()
    {
        if (!butlerCarryingCoat)
        {
            return;
        }

        if (coatCloset == null)
        {
            if (!autoStoreCoatIfClosetMissing)
            {
                Debug.LogWarning("Chapter1ArrivalController missing required field: closet reference. Coat cannot be stored.", this);
                return;
            }

            Debug.LogWarning("Chapter1ArrivalController missing closet; auto-storing coat because autoStoreCoatIfClosetMissing is enabled.", this);
        }

        if (coatCloset != null)
        {
            coatCloset.StoreCoat(carriedCoatId);
            Debug.Log($"[Chapter1] Coat {carriedCoatId} stored in wardrobe.", this);
        }

        if (carriedCoatGuest != null)
        {
            carriedCoatGuest.CoatStored = true;
            carriedCoatGuest.Handled = false;
        }

        if (carriedCoatVisual != null)
        {
            carriedCoatVisual.SetActive(false);
            carriedCoatVisual = null;
        }

        RefreshGuestScalingNow();
        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
        carriedCoatGuest = null;
        RefreshInteractionState();
        CheckActiveGroupsReadyForDrawingRoom();
        CheckChapterCompletionGate();
    }

    public string BuildShortHudState(string timeLabel)
    {
        int outsideGuests = CountGuestsWaitingOutside();
        int entranceGuests = CountGuestsInEntranceHall();
        int drawingRoomGuests = CountGuestsInDrawingRoom();
        string coatState = butlerCarryingCoat ? $"Carrying: {carriedCoatId}" : "Hands free";
        return $"{timeLabel}\nOutside: {outsideGuests}  Hall: {entranceGuests}  Drawing Room: {drawingRoomGuests}\n{coatState}";
    }

    public string BuildDebugState()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("current guest index: ");
        builder.Append(currentGuestIndex);
        builder.AppendLine();
        builder.Append("pending guest groups: ");
        builder.Append(pendingGuestGroups.Count);
        builder.AppendLine();
        builder.Append("active guests in entrance hall: ");
        builder.Append(CountGuestsInEntranceHall());
        builder.AppendLine();
        builder.Append("guests in drawing room: ");
        builder.Append(CountGuestsInDrawingRoom());
        builder.AppendLine();
        builder.Append("doorbell is ringing: ");
        builder.Append(doorbellSystem != null && doorbellSystem.IsRinging);
        builder.Append(", intensity=");
        builder.Append(doorbellSystem != null ? doorbellSystem.CurrentIntensityLevel : 0);
        builder.AppendLine();

        builder.Append("guest state for each guest:");
        builder.AppendLine();

        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState guestState = guestStates[i];

            if (guestState == null || guestState.Config == null)
            {
                builder.Append(" - ");
                builder.Append(i);
                builder.AppendLine(": missing config");
                continue;
            }

            ActorRoomState actorState = guestState.ActorState;
            builder.Append(" - ");
            builder.Append(guestState.Config.GuestId);
            builder.Append(" group=");
            builder.Append(guestState.GroupIndex + 1);
            builder.Append(": ");
            builder.Append(guestState.State);
            builder.Append(", room=");
            builder.Append(actorState != null ? actorState.CurrentRoomId : "none");
            builder.Append(", outside=");
            builder.Append(guestState.WaitingOutside);
            builder.Append(", coatTaken=");
            builder.Append(guestState.CoatTaken);
            builder.Append(", coatStored=");
            builder.Append(guestState.CoatStored);
            builder.Append(", annoyed=");
            builder.Append(guestState.Annoyed);
            builder.Append(", seated=");
            builder.Append(guestState.Seated);
            builder.AppendLine();
        }

        builder.Append("whether player/Butler is carrying a coat: ");
        builder.Append(butlerCarryingCoat);

        if (!string.IsNullOrWhiteSpace(carriedCoatId))
        {
            builder.Append(" (");
            builder.Append(carriedCoatId);
            builder.Append(")");
        }

        builder.AppendLine();
        builder.Append("closet stored coat count: ");
        builder.Append(coatCloset != null ? coatCloset.StoredCoatCount : 0);
        builder.AppendLine();
        builder.Append("chapterOneComplete: ");
        builder.Append(CanCompleteChapterOne());
        return builder.ToString();
    }

    public void ValidateRequiredReferences()
    {
        ResolveReferences(false);

        Chateau.Architecture.ValidationReport configurationReport = new Chateau.Architecture.ValidationReport();
        ValidateConfiguration(configurationReport);

        for (int i = 0; i < configurationReport.Messages.Count; i++)
        {
            Chateau.Architecture.ValidationMessage message = configurationReport.Messages[i];

            if (message.Severity == Chateau.Architecture.ValidationSeverity.Error)
            {
                Debug.LogWarning(
                    $"Chapter1 startup configuration: {message.Message}",
                    message.Context != null ? message.Context : this);
            }
        }

        if (playerMovement == null && playerButlerReference == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: player/butler reference.", this);
        }

        if (coatCloset == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: serialized Entrance coat closet.", this);
        }

        if (frontDoorArrivalPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: frontDoorArrivalPoint.", this);
        }

        if (drawingRoomEntryPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: drawingRoomEntryPoint.", this);
        }

        if (guestRoomScaleApplier == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: guestRoomScaleApplier.", this);
        }
        else if (guestRoomScaleApplier.Calibration == null)
        {
            Debug.LogWarning("Chapter1ArrivalController guestRoomScaleApplier is missing its serialized calibration.", this);
        }
        else if (guestRoomScaleApplier.Calibration.ButlerScaleSource == null)
        {
            Debug.LogWarning("GuestRoomScaleCalibration is missing its serialized Butler scale source.", this);
        }

        if (interactionHUD == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: interactionHUD.", this);
        }

        int sceneGuestCandidateCount = useExistingSceneGuestsFirst ? FindSceneGuestCandidates().Count : 0;
        int configuredGuestObjectCount = CountConfiguredGuestObjects();
        int requestedGuestCount = GetRequestedGuestCount();

        if (configuredGuestObjectCount == 0 && sceneGuestCandidateCount == 0 && guests == null)
        {
            Debug.LogWarning("Chapter1ArrivalController guest list is incomplete. Runtime placeholder guests will be created for testing.", this);
        }

        if (configuredGuestObjectCount + sceneGuestCandidateCount < requestedGuestCount)
        {
            Debug.LogWarning($"Chapter1ArrivalController needs {requestedGuestCount} guests for Chapter 1. Missing guests will be created at runtime.", this);
        }

        if (guests != null)
        {
            for (int i = 0; i < guests.Count; i++)
            {
                GuestArrivalConfig config = guests[i];

                if (config != null && config.ResolveGuestObject() == null)
                {
                    Debug.LogWarning($"Chapter1ArrivalController missing required field: guest GameObject/reference for guest entry {i}.", this);
                }
            }
        }
    }

    private void ResetChapterRuntime()
    {
        chapterCompletionRequested = false;
        finalEmptyDoorbellOccurred = false;
        emptyDoorbellWaitingForAnswer = false;
        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
        carriedCoatGuest = null;

        if (carriedCoatVisual != null)
        {
            carriedCoatVisual.SetActive(false);
        }

        carriedCoatVisual = null;
        CancelPendingCoatPickup();
        CancelPendingClosetStorage();
        currentGuestIndex = -1;
        hasWorldDoorCenterPosition = false;
        worldDoorCenterPosition = Vector3.zero;
        hasFrontDoorAnswerSpot = false;
        frontDoorAnswerSpot = Vector2.zero;
        pendingGuestGroups.Clear();
        activeEntranceGroups.Clear();
        guestGroups.Clear();
        authoredGuestRendererSorting.Clear();

        if (coatCloset != null)
        {
            coatCloset.ClearStoredCoats();
        }

        ResetGuestStates(true);
        SetChapterSceneGuestsActive(false);
        BuildGuestGroups();
    }

    private void ResetGuestStates(bool createFallbacks)
    {
        StopAllGuestFootsteps();
        guestStates.Clear();
        EnsureGuestConfigs(createFallbacks);

        for (int i = 0; i < guests.Count; i++)
        {
            GuestArrivalConfig config = guests[i];

            if (config == null)
            {
                continue;
            }

            ActorRoomState actorState = config.ResolveActorState();
            GameObject guestObject = config.ResolveGuestObject();
            NPCWaypointMover mover = guestObject != null ? guestObject.GetComponent<NPCWaypointMover>() : null;
            RoomProjectedEntity projection = ResolveGuestProjection(guestObject, actorState);
            GuestFootstepAudio footsteps = ConfigureGuestFootsteps(guestObject, i + 1);

            if (mover == null && guestObject != null)
            {
                mover = guestObject.AddComponent<NPCWaypointMover>();
            }

            if (mover != null)
            {
                mover.MoveSpeed = GetMoveSpeedForGuestObject(guestObject);
                mover.enabled = true;
            }

            GuestRuntimeState runtimeState = new GuestRuntimeState
            {
                Config = config,
                State = GuestArrivalState.Hidden,
                GuestIndex = i,
                GroupIndex = i / Mathf.Max(1, guestsPerArrivalGroup),
                GuestObject = guestObject,
                Handled = false,
                CoatTaken = false,
                CoatStored = false,
                Seated = false,
                Mover = mover,
                ActorState = actorState,
                Projection = projection,
                Footsteps = footsteps,
                Seat = ResolveSeatForGuest(i)
            };

            config.SetAssignedSeat(runtimeState.Seat);

            if (actorState != null)
            {
                actorState.enabled = true;
                actorState.SetCurrentRoom(entryRoomId);
                actorState.SetAvailableInCurrentChapter(false);
                actorState.SetVisibleByChapterState(false);
                actorState.SetInteractable(false);
                actorState.SetSeated(false);
            }

            runtimeState.ScaleParticipant = EnsureGuestScaleParticipant(runtimeState, entryRoomId, CharacterPose.Standing);

            if (runtimeState.CoatPickup != null)
            {
                runtimeState.CoatPickup.gameObject.SetActive(false);
            }

            guestStates.Add(runtimeState);
        }

        RefreshGuestScalingNow();
    }

    private void BuildGuestGroups()
    {
        int requiredGuestCount = GetRequiredGuestCountForCurrentRun();
        int activeGuestGroupCount = Mathf.Max(1, guestGroupCount);

        for (int groupIndex = 0; groupIndex < activeGuestGroupCount; groupIndex++)
        {
            int arrivalMinuteOffset = firstArrivalMinute + groupIndex;
            GuestGroupRuntimeState group = new GuestGroupRuntimeState
            {
                GroupIndex = groupIndex,
                ArrivalHour = firstArrivalHour + arrivalMinuteOffset / 60,
                ArrivalMinute = arrivalMinuteOffset % 60,
                EmptyRing = false
            };

            for (int guestOffset = 0; guestOffset < guestsPerArrivalGroup; guestOffset++)
            {
                int guestIndex = groupIndex * guestsPerArrivalGroup + guestOffset;

                if (guestIndex >= guestStates.Count || guestIndex >= requiredGuestCount)
                {
                    continue;
                }

                GuestRuntimeState guest = guestStates[guestIndex];
                guest.GroupIndex = groupIndex;
                group.Guests.Add(guest);
            }

            guestGroups.Add(group);
        }

        guestGroups.Add(new GuestGroupRuntimeState
        {
            GroupIndex = guestGroupCount,
            ArrivalHour = emptyDoorbellHour,
            ArrivalMinute = emptyDoorbellMinute,
            EmptyRing = true
        });
    }

    private void ScheduleArrivalTimeline()
    {
        if (eventScheduler != null)
        {
            eventScheduler.Clear();
        }

        for (int i = 0; i < guestGroups.Count; i++)
        {
            GuestGroupRuntimeState group = guestGroups[i];
            string eventId = group.EmptyRing
                ? "chapter_01_empty_6_04_doorbell"
                : $"chapter_01_guest_group_{group.GroupIndex + 1:00}";

            if (eventScheduler != null)
            {
                eventScheduler.ScheduleOneShotAtClockTime(eventId, group.ArrivalHour, group.ArrivalMinute, () => HandleScheduledDoorbell(group));
            }
            else
            {
                StartCoroutine(FallbackScheduleAtClockTime(group));
            }
        }
    }

    private IEnumerator FallbackScheduleAtClockTime(GuestGroupRuntimeState group)
    {
        while (chapterClock != null && !chapterClock.HasReachedTime(group.ArrivalHour, group.ArrivalMinute))
        {
            yield return null;
        }

        HandleScheduledDoorbell(group);
    }

    private void HandleScheduledDoorbell(GuestGroupRuntimeState group)
    {
        if (!sequenceActive || group == null)
        {
            return;
        }

        if (group.EmptyRing)
        {
            if (finalEmptyDoorbellOccurred)
            {
                return;
            }

            finalEmptyDoorbellOccurred = true;
            emptyDoorbellWaitingForAnswer = pendingGuestGroups.Count == 0;
            float queuedMinute = GetOldestPendingQueuedGameMinute();
            doorbellSystem?.StartRinging(
                pendingGuestGroups.Count > 0 ? queuedMinute : chapterClock != null ? chapterClock.ElapsedGameMinutes : 0f,
                pendingGuestGroups.Count > 0,
                pendingGuestGroups.Count == 0);
            Debug.Log("6:04 doorbell event fired. No new guests arrive.", this);
            RefreshInteractionState();
            return;
        }

        QueueGuestGroupOutside(group);
    }

    private void QueueGuestGroupOutside(GuestGroupRuntimeState group)
    {
        if (group == null || group.QueuedOutside || group.EnteredEntranceHall)
        {
            return;
        }

        group.QueuedOutside = true;
        group.QueuedAtGameMinute = chapterClock != null ? chapterClock.ElapsedGameMinutes : 0f;
        pendingGuestGroups.Add(group);

        for (int i = 0; i < group.Guests.Count; i++)
        {
            GuestRuntimeState guest = group.Guests[i];
            guest.WaitingOutside = true;
            guest.QueuedAtGameMinute = group.QueuedAtGameMinute;
            SetGuestState(guest, GuestArrivalState.WaitingTurn);
        }

        RefreshDoorbellRinging();
        RefreshInteractionState();
        Debug.Log($"Guest group {group.GroupIndex + 1} queued outside at {ChapterClock.FormatTime(group.ArrivalHour, group.ArrivalMinute)}.", this);
    }

    private IEnumerator AdmitQueuedGuestGroups(List<GuestGroupRuntimeState> groupsToAdmit)
    {
        if (groupsToAdmit == null || groupsToAdmit.Count == 0)
        {
            yield break;
        }

        int totalGuestBatchCount = CountGuestsInGroups(groupsToAdmit);
        int entranceGuestSlotCount = Mathf.Max(GetRequestedGuestCount(), totalGuestBatchCount);

        int startedGuestCount = 0;
        bool queuedWelcome = false;

        for (int i = 0; i < groupsToAdmit.Count; i++)
        {
            GuestGroupRuntimeState group = groupsToAdmit[i];

            if (group == null || group.EnteredEntranceHall)
            {
                continue;
            }

            group.EnteredEntranceHall = true;
            group.QueuedOutside = false;
            activeEntranceGroups.Add(group);

            for (int guestIndex = 0; guestIndex < group.Guests.Count; guestIndex++)
            {
                GuestRuntimeState guest = group.Guests[guestIndex];
                guest.WaitingOutside = false;
                guest.EnteredEntranceHall = true;
                guest.Annoyed = WasGuestWaitingLongEnoughToBeAnnoyed(guest);
                currentGuestIndex = guest.GuestIndex;

                if (!queuedWelcome)
                {
                    QueueButlerLine("SUB_CH01_BUTLER_WELCOME_001");
                    queuedWelcome = true;
                }

                StartCoroutine(AdmitGuestToEntranceHall(guest, guest.GuestIndex, entranceGuestSlotCount));
                startedGuestCount++;
            }
        }

        if (startedGuestCount > 0)
        {
            yield return null;
        }

        RefreshInteractionState();
        CheckActiveGroupsReadyForDrawingRoom();
    }

    private IEnumerator AdmitGuestToEntranceHall(GuestRuntimeState guest, int indexInDoorBatch, int batchCount)
    {
        if (guest == null)
        {
            yield break;
        }

        EnsureGuestHiddenBeforeArrival(guest);
        bool useWorldSafePlacement = IsWorldSpaceGuestObject(guest.GuestObject);
        PlaceGuestAtDoorArrival(guest, indexInDoorBatch, batchCount);

        if (guest.ActorState != null)
        {
            guest.ActorState.SetCurrentRoom(entryRoomId);
            guest.ActorState.SetAvailableInCurrentChapter(true);
            guest.ActorState.SetInteractable(false);
        }

        EnsureGuestScaleParticipant(guest, entryRoomId, CharacterPose.Standing);
        RefreshGuestScalingNow();
        PrepareGuestCoatForArrival(guest);
        SetGuestState(guest, GuestArrivalState.Arriving);
        ForceGuestVisibleForDoorFlow(guest);
        LogGuestLine(guest.Config, guest.Config.GreetingLine);
        QueueGuestLine(guest, "GREETING", GetGuestGreetingLine(guest));

        if (guest.Annoyed)
        {
            Debug.Log($"{guest.Config.GuestDisplayName}: {GetAnnoyedLine(guest.GuestIndex)}", this);
            QueueGuestLine(guest, "ANNOYED", GetAnnoyedLine(guest.GuestIndex));
        }

        Transform waitSpot;

        if (useWorldSafePlacement)
        {
            waitSpot = CreateRuntimeAnchor(
                $"EntranceWait_{guest.Config.GuestId}",
                GetWorldEntranceWaitPosition(guest, indexInDoorBatch, batchCount),
                null);
        }
        else
        {
            waitSpot = CreateRuntimeAnchor(
                $"EntranceWait_{guest.Config.GuestId}",
                GetEntranceWaitPosition(guest, indexInDoorBatch, batchCount),
                frontDoorArrivalPoint);
        }

        Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} moving to entrance wait spot.", this);
        yield return MoveGuestTo(guest, waitSpot, "entrance waiting spot");

        if (guest.CoatTaken || guest.MovingToDrawingRoom || guest.Seated)
        {
            yield break;
        }

        ForceGuestVisibleForDoorFlow(guest);
        EnsureGuestScaleParticipant(guest, entryRoomId, CharacterPose.Standing);
        RefreshGuestScalingNow();
        OfferGuestCoat(guest);
        Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} reached entrance wait spot.", this);
    }

    private void PrepareGuestCoatForArrival(GuestRuntimeState guest)
    {
        if (guest == null || guest.CoatPickup != null)
        {
            return;
        }

        guest.CoatPickup = CreateCoatPickup(guest);
        DisableCoatPickupInteraction(guest.CoatPickup);
    }

    private void OfferGuestCoat(GuestRuntimeState guest)
    {
        if (guest == null)
        {
            return;
        }

        if (!guest.CoatOffered)
        {
            guest.CoatOffered = true;

            if (guest.CoatPickup == null)
            {
                guest.CoatPickup = CreateCoatPickup(guest);
            }
        }

        SetGuestState(guest, GuestArrivalState.GreetingComplete);
        SetGuestState(guest, GuestArrivalState.CoatOffered);
        RefreshCoatPickupVisibilityForCurrentRoom(guest);
    }

    private Chapter1CoatPickup CreateCoatPickup(GuestRuntimeState guest)
    {
        GameObject coatObject = FindVisibleGuestCoatObject(guest);
        bool usingAuthoredCoatObject = coatObject != null;

        if (coatObject == null)
        {
            coatObject = new GameObject($"Coat_{guest.Config.GuestId}");
        }

        coatObject.SetActive(true);
        bool useWorldSpaceCoat = IsWorldSpaceGuestObject(guest.GuestObject);

        if (!usingAuthoredCoatObject && useWorldSpaceCoat && guest.GuestObject != null)
        {
            coatObject.transform.SetParent(guest.GuestObject.transform, false);
            coatObject.transform.localPosition = WorldCoatOffset;
            coatObject.transform.localRotation = Quaternion.identity;
        }
        else if (!usingAuthoredCoatObject)
        {
            Transform parent = guest.GuestObject != null && guest.GuestObject.transform.parent != null
                ? guest.GuestObject.transform.parent
                : transform;
            coatObject.transform.SetParent(parent, true);
            coatObject.transform.position = GetCoatPosition(guest);
        }

        if (!usingAuthoredCoatObject)
        {
            coatObject.transform.localScale = Vector3.one;
        }

        ApplyAssignedCoatSprite(guest, coatObject, usingAuthoredCoatObject);
        Debug.Log($"[Chapter1] Coat attached to guest {guest.Config.GuestId}.", this);

        BoxCollider2D collider = coatObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = coatObject.AddComponent<BoxCollider2D>();
        }

        collider.size = GetCoatClickColliderSize(coatObject, out Vector2 colliderOffset);
        collider.offset = colliderOffset;
        collider.isTrigger = true;

        Chapter1CoatPickup pickup = coatObject.GetComponent<Chapter1CoatPickup>();

        if (pickup == null)
        {
            pickup = coatObject.AddComponent<Chapter1CoatPickup>();
        }

        pickup.Initialize(this, guest.Config.GuestId, guest.Config.CoatId);
        RefreshCoatPickupVisibilityForCurrentRoom(guest);
        return pickup;
    }

    private GameObject FindVisibleGuestCoatObject(GuestRuntimeState guest)
    {
        if (guest == null || guest.GuestObject == null)
        {
            return null;
        }

        Transform[] children = guest.GuestObject.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null ||
                child == guest.GuestObject.transform ||
                !HasCoatVisualName(child))
            {
                continue;
            }

            if (child.GetComponent<SpriteRenderer>() != null ||
                child.GetComponent<Renderer>() != null ||
                child.GetComponent<Graphic>() != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void ApplyAssignedCoatSprite(GuestRuntimeState guest, GameObject coatObject, bool preserveAuthoredVisualSize)
    {
        if (guest == null || coatObject == null)
        {
            return;
        }

        Sprite assignedSprite = ResolveGuestCoatSprite(guest);

        if (assignedSprite == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = coatObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = coatObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = coatObject.AddComponent<SpriteRenderer>();
        }

        Sprite previousSprite = spriteRenderer.sprite;
        bool spriteChanged = previousSprite != assignedSprite;
        Vector3 previousScale = spriteRenderer.transform.localScale;
        Vector2 previousSize = previousSprite != null ? previousSprite.bounds.size : Vector2.zero;

        spriteRenderer.sprite = assignedSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.enabled = true;
        ConfigureAssignedCoatSorting(guest, spriteRenderer);

        if (spriteChanged && preserveAuthoredVisualSize && previousSize.x > 0f && previousSize.y > 0f)
        {
            Vector2 assignedSize = assignedSprite.bounds.size;

            if (assignedSize.x > 0f && assignedSize.y > 0f)
            {
                Vector3 assignedScale = new Vector3(
                    previousScale.x * previousSize.x / assignedSize.x,
                    previousScale.y * previousSize.y / assignedSize.y,
                    previousScale.z);
                Vector2 previousPivot = GetSpritePivotNormalized(previousSprite);
                Vector2 assignedPivot = GetSpritePivotNormalized(assignedSprite);
                Vector3 pivotOffsetDelta = new Vector3(
                    assignedPivot.x * assignedSize.x * assignedScale.x - previousPivot.x * previousSize.x * previousScale.x,
                    assignedPivot.y * assignedSize.y * assignedScale.y - previousPivot.y * previousSize.y * previousScale.y,
                    0f);

                spriteRenderer.transform.localScale = assignedScale;
                spriteRenderer.transform.localPosition += pivotOffsetDelta;
            }
        }
        else if (!preserveAuthoredVisualSize)
        {
            spriteRenderer.transform.localScale = AssignedCoatFallbackScale;

            if (spriteChanged)
            {
                spriteRenderer.transform.localPosition += GetSpritePivotOffset(spriteRenderer);
            }
        }
    }

    private static Vector2 GetSpritePivotNormalized(Sprite sprite)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);
    }

    private static Vector3 GetCoatOffsetWithSpritePivot(GameObject coatObject, Vector3 baseOffset)
    {
        SpriteRenderer spriteRenderer = coatObject != null ? coatObject.GetComponent<SpriteRenderer>() : null;

        if (spriteRenderer == null && coatObject != null)
        {
            spriteRenderer = coatObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer == null || spriteRenderer.sprite == null || spriteRenderer.transform != coatObject.transform)
        {
            return baseOffset;
        }

        return baseOffset + GetSpritePivotOffset(spriteRenderer);
    }

    private static Vector3 GetSpritePivotOffset(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return Vector3.zero;
        }

        Vector2 pivot = GetSpritePivotNormalized(spriteRenderer.sprite);
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        Vector3 spriteScale = spriteRenderer.transform.localScale;

        return new Vector3(
            pivot.x * spriteSize.x * spriteScale.x,
            pivot.y * spriteSize.y * spriteScale.y,
            0f);
    }

    private void ConfigureAssignedCoatSorting(GuestRuntimeState guest, SpriteRenderer coatRenderer)
    {
        if (coatRenderer == null)
        {
            return;
        }

        RoomProjectedEntity projection = ResolveGuestProjection(guest);

        if (projection != null && projection.IsProjectionActive)
        {
            int coatSortingOffset = projection.VisualProfile != null ? projection.VisualProfile.CoatSortingOffset : 1;
            coatRenderer.sortingLayerName = projection.GetSortingLayerName();
            coatRenderer.sortingOrder = projection.GetSortingOrder(coatSortingOffset);
            coatRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            CacheConfiguredCoatSorting(coatRenderer);
            return;
        }

        SpriteRenderer guestRenderer = guest != null ? FindCharacterSpriteRenderer(guest.GuestObject) : null;

        if (guestRenderer != null)
        {
            coatRenderer.sortingLayerID = guestRenderer.sortingLayerID;
            coatRenderer.sortingOrder = guestRenderer.sortingOrder + 1;
            CacheConfiguredCoatSorting(coatRenderer);
            return;
        }

        coatRenderer.sortingLayerName = "People";
        coatRenderer.sortingOrder = 9000 + (guest != null ? guest.GuestIndex : 0) + 1;
        CacheConfiguredCoatSorting(coatRenderer);
    }

    private void CacheConfiguredCoatSorting(Renderer coatRenderer)
    {
        if (coatRenderer == null)
        {
            return;
        }

        authoredGuestRendererSorting[coatRenderer] = new RendererSortingState
        {
            LayerName = coatRenderer.sortingLayerName,
            Order = coatRenderer.sortingOrder
        };

        RemoveDestroyedGuestSortingCacheEntries();
    }

    private Sprite ResolveGuestCoatSprite(GuestRuntimeState guest)
    {
        if (guest == null)
        {
            return null;
        }

        int guestNumber;
        Sprite sprite;

        if (guest.Config != null)
        {
            if (TryResolveGuestNumberFromText(guest.Config.GuestId, out guestNumber) &&
                TryLoadGuestCoatSprite(guestNumber, out sprite))
            {
                return sprite;
            }

            if (TryResolveGuestNumberFromText(guest.Config.CoatId, out guestNumber) &&
                TryLoadGuestCoatSprite(guestNumber, out sprite))
            {
                return sprite;
            }

            if (TryResolveNamedGuestCoatNumber(guest.Config.GuestDisplayName, out guestNumber) &&
                TryLoadGuestCoatSprite(guestNumber, out sprite))
            {
                return sprite;
            }
        }

        string guestObjectName = guest.GuestObject != null ? guest.GuestObject.name : null;

        if (TryResolveGuestNumberFromText(guestObjectName, out guestNumber) &&
            TryLoadGuestCoatSprite(guestNumber, out sprite))
        {
            return sprite;
        }

        if (TryResolveNamedGuestCoatNumber(guestObjectName, out guestNumber) &&
            TryLoadGuestCoatSprite(guestNumber, out sprite))
        {
            return sprite;
        }

        if (TryLoadGuestCoatSprite(guest.GuestIndex + 1, out sprite))
        {
            return sprite;
        }

        return null;
    }

    private bool TryLoadGuestCoatSprite(int guestNumber, out Sprite sprite)
    {
        sprite = null;

        if (guestNumber < 1 || guestNumber > ChapterGuestNameAliases.Length)
        {
            return false;
        }

        return TryLoadGuestCoatSprite($"guest{guestNumber}_coat", out sprite);
    }

    private bool TryLoadGuestCoatSprite(string resourceName, out Sprite sprite)
    {
        sprite = null;

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return false;
        }

        string cleanResourceName = resourceName.Trim();

        if (guestCoatSpriteCache.TryGetValue(cleanResourceName, out sprite))
        {
            return sprite != null;
        }

        string resourcePath = $"{GuestCoatResourceFolder}/{cleanResourceName}";
        sprite = Resources.Load<Sprite>(resourcePath);

        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
        }

        guestCoatSpriteCache[cleanResourceName] = sprite;
        return sprite != null;
    }

    private static bool TryResolveGuestNumberFromText(string value, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedValue = NormalizeObjectName(value);
        int guestIndex = normalizedValue.IndexOf("guest", StringComparison.OrdinalIgnoreCase);

        if (guestIndex < 0)
        {
            return false;
        }

        int digitStart = guestIndex + "guest".Length;

        while (digitStart < normalizedValue.Length && normalizedValue[digitStart] == '_')
        {
            digitStart++;
        }

        if (digitStart >= normalizedValue.Length || !char.IsDigit(normalizedValue[digitStart]))
        {
            return false;
        }

        int digitEnd = digitStart;

        while (digitEnd < normalizedValue.Length && char.IsDigit(normalizedValue[digitEnd]))
        {
            digitEnd++;
        }

        return int.TryParse(normalizedValue.Substring(digitStart, digitEnd - digitStart), out guestNumber);
    }

    private static bool TryResolveNamedGuestCoatNumber(string value, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedValue = NormalizeObjectName(value).Replace("_", string.Empty);

        if (normalizedValue.Contains("missisoldewren"))
        {
            guestNumber = 1;
            return true;
        }

        if (normalizedValue.Contains("professorlucienvale"))
        {
            guestNumber = 2;
            return true;
        }

        if (normalizedValue.Contains("misterflorianknell"))
        {
            guestNumber = 3;
            return true;
        }

        if (normalizedValue.Contains("countesselowendusk"))
        {
            guestNumber = 4;
            return true;
        }

        if (normalizedValue.Contains("baronhectorglass"))
        {
            guestNumber = 5;
            return true;
        }

        if (normalizedValue.Contains("ladysabinemarrow"))
        {
            guestNumber = 6;
            return true;
        }

        if (normalizedValue.Contains("lordambroseveil"))
        {
            guestNumber = 7;
            return true;
        }

        if (normalizedValue.Contains("madamecoraliethread"))
        {
            guestNumber = 8;
            return true;
        }

        return false;
    }

    private Vector2 GetCoatClickColliderSize(GameObject coatObject, out Vector2 colliderOffset)
    {
        colliderOffset = Vector2.zero;
        SpriteRenderer spriteRenderer = coatObject != null ? coatObject.GetComponent<SpriteRenderer>() : null;

        if (spriteRenderer != null)
        {
            Bounds localBounds = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds : default;

            if (localBounds.size.x > 0f && localBounds.size.y > 0f)
            {
                colliderOffset = localBounds.center;
                return localBounds.size;
            }
        }

        return WorldCoatColliderSize;
    }

    private void CheckActiveGroupsReadyForDrawingRoom()
    {
        for (int i = activeEntranceGroups.Count - 1; i >= 0; i--)
        {
            GuestGroupRuntimeState group = activeEntranceGroups[i];

            if (group == null || group.Complete || group.MovingToDrawingRoom)
            {
                continue;
            }

            if (CanMoveEntranceGroupToDrawingRoom(group))
            {
                StartCoroutine(MoveEntranceGroupToDrawingRoom(group));
            }
        }
    }

    private bool CanMoveEntranceGroupToDrawingRoom(GuestGroupRuntimeState group)
    {
        if (group == null || group.Guests.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < group.Guests.Count; i++)
        {
            if (!CanMoveGuestToDrawingRoom(group.Guests[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanMoveGuestToDrawingRoom(GuestRuntimeState guest)
    {
        return guest != null &&
            guest.CoatStored &&
            !guest.MovingToDrawingRoom &&
            !guest.Seated;
    }

    private IEnumerator MoveEntranceGroupToDrawingRoom(GuestGroupRuntimeState group)
    {
        if (!CanMoveEntranceGroupToDrawingRoom(group))
        {
            yield break;
        }

        group.MovingToDrawingRoom = true;
        yield return SpeakButlerLine("SUB_CH01_BUTLER_THIS_WAY_001");

        for (int i = 0; i < group.Guests.Count; i++)
        {
            QueueGuestLine(group.Guests[i], "TO_DRAWING_ROOM", null);
            StartCoroutine(MoveGuestToDrawingRoom(group.Guests[i], group));
        }

        yield return null;
    }

    private IEnumerator MoveGuestToDrawingRoom(GuestRuntimeState guest, GuestGroupRuntimeState group)
    {
        if (!CanMoveGuestToDrawingRoom(guest))
        {
            yield break;
        }

        guest.MovingToDrawingRoom = true;
        Transform drawingRoomEntry = ResolveDrawingRoomEntryPointForGuest(guest, group);

        SetGuestState(guest, GuestArrivalState.MovingToDrawingRoom);
        RestoreGuestAuthoredSorting(guest);
        ApplyEntranceBanisterSafeWalkingSorting(guest);
        Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} moving to drawing room door.", this);
        BeginGuestMoveTo(guest, drawingRoomEntry, "drawingRoomEntryPoint");

        while (guest.Mover != null && guest.Mover.IsMoving)
        {
            if (ShouldFinishDrawingRoomMoveOffscreen(guest))
            {
                break;
            }

            yield return null;
        }

        CompleteGuestDrawingRoomArrival(guest, group);
    }

    private bool ShouldFinishDrawingRoomMoveOffscreen(GuestRuntimeState guest)
    {
        if (guest == null || guest.GuestObject == null)
        {
            return true;
        }

        if (!guest.GuestObject.activeInHierarchy)
        {
            return true;
        }

        return navigationManager != null &&
            !string.IsNullOrWhiteSpace(navigationManager.CurrentRoom) &&
            !SameRoom(navigationManager.CurrentRoom, entryRoomId);
    }

    private void CompleteGuestDrawingRoomArrival(GuestRuntimeState guest, GuestGroupRuntimeState group)
    {
        if (guest == null || (guest.Seated && !guest.MovingToDrawingRoom))
        {
            return;
        }

        Transform drawingRoomSpot = ResolveDrawingRoomSpotForGuest(guest);

        RestoreGuestAuthoredSorting(guest);
        DisableGuestMovement(guest);
        MoveGuestObjectToRoomContent(guest, drawingRoomId);
        SetGuestVisibleAfterDrawingRoomExit(guest, true);
        PlaceGuestAt(guest, drawingRoomSpot, "drawing room waiting spot");

        if (guest.ActorState != null)
        {
            guest.ActorState.enabled = true;
            guest.ActorState.SetCurrentRoom(drawingRoomId);
            guest.ActorState.SetAvailableInCurrentChapter(true);
            guest.ActorState.SetInteractable(false);
            ApplyDrawingRoomWaitingPose(guest);
            guest.ActorState.SetVisibleByChapterState(true);
            guest.ActorState.ApplyState();
        }

        EnsureGuestScaleParticipant(guest, drawingRoomId, CharacterPose.Seated);
        RefreshGuestScalingNow();
        ApplyDrawingRoomGuestDepthSorting(guest, drawingRoomSpot);

        guest.MovingToDrawingRoom = false;
        guest.Seated = true;
        guest.Handled = true;
        SetGuestState(guest, GuestArrivalState.Seated);
        SetGuestState(guest, GuestArrivalState.Handled);
        Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} entered the drawing room and is waiting there.", this);

        TryCompleteEntranceGroup(group);
        RefreshInteractionState();
        CheckChapterCompletionGate();
    }

    private GuestFootstepAudio ConfigureGuestFootsteps(GameObject guestObject, int guestNumber)
    {
        if (!playGuestFootsteps || guestObject == null)
        {
            return null;
        }

        GuestFootstepAudio footsteps = guestObject.GetComponent<GuestFootstepAudio>();

        if (footsteps == null)
        {
            footsteps = guestObject.AddComponent<GuestFootstepAudio>();
        }

        if (guestFootstepCatalog != null &&
            guestFootstepCatalog.TryGetFootstepVariantsForGuest(
                guestNumber,
                out AudioClip[] clips,
                out float volume,
                out float cutoffFrequency,
                out float resonanceQ,
                out float stepInterval,
                out float stepJitter))
        {
            footsteps.Configure(clips, volume, cutoffFrequency, resonanceQ, stepInterval, stepJitter);
        }

        return footsteps;
    }

    private GuestFootstepAudio ResolveGuestFootsteps(GuestRuntimeState guestState)
    {
        if (!playGuestFootsteps || guestState == null || guestState.GuestObject == null)
        {
            return null;
        }

        if (guestState.Footsteps == null)
        {
            guestState.Footsteps = ConfigureGuestFootsteps(guestState.GuestObject, guestState.GuestIndex + 1);
        }

        return guestState.Footsteps;
    }

    private void StartGuestFootsteps(GuestRuntimeState guestState)
    {
        GuestFootstepAudio footsteps = ResolveGuestFootsteps(guestState);
        footsteps?.PlayWalking();
    }

    private void StopGuestFootsteps(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return;
        }

        if (guestState.Footsteps == null && guestState.GuestObject != null)
        {
            guestState.Footsteps = guestState.GuestObject.GetComponent<GuestFootstepAudio>();
        }

        guestState.Footsteps?.StopWalking();
    }

    private void StopAllGuestFootsteps()
    {
        for (int i = 0; i < guestStates.Count; i++)
        {
            StopGuestFootsteps(guestStates[i]);
        }
    }

    private Transform ResolveDrawingRoomEntryPointForGuest(GuestRuntimeState guest, GuestGroupRuntimeState group)
    {
        if (guest == null)
        {
            return drawingRoomEntryPoint;
        }

        if (!IsWorldSpaceGuestObject(guest.GuestObject))
        {
            return guest.Config.GetDrawingRoomEntryPoint(drawingRoomEntryPoint);
        }

        GetDrawingRoomGroupSlot(guest, group, out int slotInGroup, out int groupSize);
        return CreateRuntimeAnchor(
            $"DrawingRoomEntry_{guest.Config.GuestId}",
            GetWorldDrawingRoomEntryPosition(guest, slotInGroup, groupSize),
            null);
    }

    private void GetDrawingRoomGroupSlot(
        GuestRuntimeState guest,
        GuestGroupRuntimeState group,
        out int slotInGroup,
        out int groupSize)
    {
        groupSize = group != null && group.Guests.Count > 0
            ? group.Guests.Count
            : Mathf.Max(1, guestsPerArrivalGroup);
        slotInGroup = group != null ? group.Guests.IndexOf(guest) : -1;

        if (slotInGroup < 0)
        {
            slotInGroup = guest != null ? guest.GuestIndex % groupSize : 0;
        }
    }

    private void TryCompleteEntranceGroup(GuestGroupRuntimeState group)
    {
        if (group == null || group.Complete)
        {
            return;
        }

        for (int i = 0; i < group.Guests.Count; i++)
        {
            GuestRuntimeState guest = group.Guests[i];

            if (guest == null || !guest.Seated)
            {
                return;
            }
        }

        group.Complete = true;
        group.MovingToDrawingRoom = false;
        activeEntranceGroups.Remove(group);
        Debug.Log($"Guest group {group.GroupIndex + 1} entered the drawing room.", this);
        TryFastForwardNextDoorbellIfEntranceClear();
    }

    private void CompleteOffscreenDrawingRoomMoves(string currentRoomName)
    {
        if (SameRoom(currentRoomName, entryRoomId))
        {
            return;
        }

        for (int groupIndex = activeEntranceGroups.Count - 1; groupIndex >= 0; groupIndex--)
        {
            GuestGroupRuntimeState group = activeEntranceGroups[groupIndex];

            if (group == null)
            {
                continue;
            }

            for (int guestIndex = 0; guestIndex < group.Guests.Count; guestIndex++)
            {
                GuestRuntimeState guest = group.Guests[guestIndex];

                if (guest != null && guest.MovingToDrawingRoom)
                {
                    CompleteGuestDrawingRoomArrival(guest, group);
                }
            }
        }
    }

    private int StageRequiredGuestsInDrawingRoomForChapter2()
    {
        int requiredGuestCount = GetRequiredGuestCountForCurrentRun();

        if (guestStates.Count < requiredGuestCount)
        {
            Debug.LogWarning(
                $"Chapter 2 skip expected {requiredGuestCount} guest state(s), but only {guestStates.Count} existed. Rebuilding guest states before staging.",
                this);
            ResetGuestStates(true);
        }

        int stagedGuestCount = Mathf.Min(requiredGuestCount, guestStates.Count);

        for (int i = 0; i < stagedGuestCount; i++)
        {
            StageGuestInDrawingRoomForChapter2(guestStates[i]);
        }

        RefreshInteractionState();
        RefreshAllGuestRoomVisibility();
        HideGuestCoatsForChapter2Skip();
        Debug.Log($"Chapter 2 staged {stagedGuestCount}/{requiredGuestCount} guest(s) in the Drawing Room with coats stored.", this);

        if (stagedGuestCount < requiredGuestCount)
        {
            Debug.LogWarning(
                $"Chapter 2 skip could only stage {stagedGuestCount}/{requiredGuestCount} required guest(s). Check scene guest configuration and fallback guest creation.",
                this);
        }

        return stagedGuestCount;
    }

    private void StageGuestInDrawingRoomForChapter2(GuestRuntimeState guest)
    {
        if (guest == null)
        {
            return;
        }

        Transform drawingRoomSpot = ResolveDrawingRoomSpotForGuest(guest);

        RestoreGuestAuthoredSorting(guest);
        MoveGuestObjectToRoomContent(guest, drawingRoomId);
        SetGuestVisibleAfterDrawingRoomExit(guest, true);
        PlaceGuestAt(guest, drawingRoomSpot, "drawing room waiting spot");
        DisableGuestMovement(guest);

        if (guest.CoatPickup != null)
        {
            DisableChapter1CoatPickupForChapter2Skip(guest.CoatPickup);
        }

        if (guest.ActorState != null)
        {
            guest.ActorState.enabled = true;
            guest.ActorState.SetCurrentRoom(drawingRoomId);
            guest.ActorState.SetAvailableInCurrentChapter(true);
            guest.ActorState.SetInteractable(false);
            ApplyDrawingRoomWaitingPose(guest);
            guest.ActorState.SetVisibleByChapterState(true);
            guest.ActorState.ApplyState();
        }

        EnsureGuestScaleParticipant(guest, drawingRoomId, CharacterPose.Seated);
        RefreshGuestScalingNow();
        HideGuestCoatVisualsForChapter2Skip(guest);
        ApplyDrawingRoomGuestDepthSorting(guest, drawingRoomSpot);

        guest.WaitingOutside = false;
        guest.EnteredEntranceHall = true;
        guest.Annoyed = false;
        guest.CoatOffered = true;
        guest.CoatTaken = true;
        guest.MovingToDrawingRoom = false;
        StoreGuestCoatForChapter2Skip(guest);
        guest.CoatStored = true;
        guest.Seated = true;
        guest.Handled = true;
        SetGuestState(guest, GuestArrivalState.Seated);
        SetGuestState(guest, GuestArrivalState.Handled);
    }

    private void ApplyDrawingRoomWaitingPose(GuestRuntimeState guest)
    {
        if (guest == null || guest.ActorState == null)
        {
            return;
        }

        guest.ActorState.SetSeated(!ShouldUseStandingDrawingRoomPose(guest));
    }

    private bool ShouldUseStandingDrawingRoomPose(GuestRuntimeState guest)
    {
        return guest != null &&
            (guest.GuestIndex == 2 ||
            guest.GuestIndex == 4 ||
            guest.GuestIndex == 6);
    }

    private void LogChapter2SkipGuestVisibility(int expectedGuestCount, int stagedGuestCount)
    {
        int visibleGuestCount = 0;
        int drawingRoomGuestCount = 0;

        for (int i = 0; i < expectedGuestCount && i < guestStates.Count; i++)
        {
            GuestRuntimeState guest = guestStates[i];
            ActorRoomState actorState = guest != null ? guest.ActorState : null;

            if (actorState == null)
            {
                continue;
            }

            if (SameRoom(actorState.CurrentRoomId, drawingRoomId))
            {
                drawingRoomGuestCount++;
            }

            if (actorState.IsVisibleInCurrentRoom)
            {
                visibleGuestCount++;
            }
        }

        string message = $"Chapter 2 skip guest visibility verified after room change: " +
            $"{visibleGuestCount}/{expectedGuestCount} visible, {drawingRoomGuestCount}/{expectedGuestCount} assigned to Drawing Room, " +
            $"{stagedGuestCount} force-staged.";

        if (expectedGuestCount <= 0 || visibleGuestCount == 0)
        {
            Debug.LogWarning(message, this);
            return;
        }

        Debug.Log(message, this);
    }

    private void HideGuestCoatVisualsForChapter2Skip(GuestRuntimeState guest)
    {
        if (guest == null || guest.GuestObject == null)
        {
            return;
        }

        Transform[] children = guest.GuestObject.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null ||
                child == guest.GuestObject.transform ||
                child.name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            HideCoatVisualObjectForChapter2Skip(child.gameObject);
        }
    }

    private void HideAllGuestCoatVisualsForChapter2Skip()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (!IsChapter2SkipCoatVisualObject(candidate))
            {
                continue;
            }

            HideCoatVisualObjectForChapter2Skip(candidate);
        }
    }

    private static bool IsChapter2SkipCoatVisualObject(GameObject candidate)
    {
        if (candidate == null || !candidate.scene.IsValid())
        {
            return false;
        }

        if (candidate.GetComponent<Chapter1CoatPickup>() != null)
        {
            return true;
        }

        string objectName = candidate.name;

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        return objectName.IndexOf("coatcutout", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.StartsWith("Coat_", StringComparison.OrdinalIgnoreCase);
    }

    private void HideCoatVisualObjectForChapter2Skip(GameObject coatObject)
    {
        if (coatObject == null)
        {
            return;
        }

        Chapter1CoatPickup coatPickup = coatObject.GetComponent<Chapter1CoatPickup>();

        if (coatPickup != null)
        {
            coatPickup.enabled = false;
        }

        Collider2D[] colliders = coatObject.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        SetCoatPickupRenderersVisible(coatObject, false);
        coatObject.SetActive(false);
    }

    private void StoreGuestCoatForChapter2Skip(GuestRuntimeState guest)
    {
        if (guest == null || guest.Config == null || coatCloset == null)
        {
            return;
        }

        string coatId = guest.Config.CoatId;

        if (!coatCloset.ContainsCoat(coatId))
        {
            coatCloset.StoreCoat(coatId);
        }
    }

    private void DisableAllChapter1CoatPickupsForChapter2Skip()
    {
        Chapter1CoatPickup[] coatPickups = FindObjectsByType<Chapter1CoatPickup>(FindObjectsInactive.Include);

        for (int i = 0; i < coatPickups.Length; i++)
        {
            DisableChapter1CoatPickupForChapter2Skip(coatPickups[i]);
        }
    }

    private void DisableChapter1CoatPickupForChapter2Skip(Chapter1CoatPickup coatPickup)
    {
        if (coatPickup == null)
        {
            return;
        }

        HideCoatVisualObjectForChapter2Skip(coatPickup.gameObject);
    }

    private Transform ResolveDrawingRoomSpotForGuest(GuestRuntimeState guest)
    {
        if (guest == null)
        {
            return drawingRoomEntryPoint;
        }

        Transform editableGuestPoint = GetDrawingRoomGuestPoint(guest.GuestIndex);

        if (editableGuestPoint != null)
        {
            return editableGuestPoint;
        }

        return guest.Seat != null ? guest.Seat : ResolveSeatForGuest(guest.GuestIndex);
    }

    private void CheckChapterCompletionGate()
    {
        if (!sequenceActive || chapterCompletionRequested)
        {
            return;
        }

        if (!CanCompleteChapterOne())
        {
            return;
        }

        if (navigationManager == null || !SameRoom(navigationManager.CurrentRoom, drawingRoomId))
        {
            Debug.Log("Chapter 1 completion ready. Player must enter the drawing room.", this);
            return;
        }

        chapterCompletionRequested = true;
        sequenceActive = false;
        StageRequiredGuestsInDrawingRoomForChapter2();
        UnsubscribeFromRoomChanges();
        chapterManager?.CompleteChapterAndTriggerNextChapter("chapter_02_pending");
    }

    private bool CanCompleteChapterOne()
    {
        if (!finalEmptyDoorbellOccurred)
        {
            return false;
        }

        int requiredGuestCount = GetRequiredGuestCountForCurrentRun();

        if (requiredGuestCount <= 0 || guestStates.Count < requiredGuestCount)
        {
            return false;
        }

        for (int i = 0; i < requiredGuestCount; i++)
        {
            GuestRuntimeState guest = guestStates[i];

            if (guest == null || !guest.CoatStored || !guest.Seated)
            {
                return false;
            }
        }

        return pendingGuestGroups.Count == 0 && !butlerCarryingCoat;
    }

    private void EnsureGuestHiddenBeforeArrival(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.ActorState == null)
        {
            return;
        }

        guestState.ActorState.SetAvailableInCurrentChapter(false);
        guestState.ActorState.SetVisibleByChapterState(false);
        guestState.ActorState.SetInteractable(false);
    }

    private void SetChapterSceneGuestsActive(bool active)
    {
        if (active)
        {
            SetNamedSceneGuestsActive(GetChapterSceneGuestNames(), true);
            return;
        }

        SetChapterSceneGuestAliasesActive(false);
    }

    private bool IsChapterSceneGuest(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return false;
        }

        return MatchesAnyChapterGuestAlias(guestObject);
    }

    private bool ShouldPreserveAuthoredEntrancePosition(GameObject guestObject)
    {
        return IsChapterSceneGuest(guestObject) && !runtimeGeneratedGuestObjects.Contains(guestObject);
    }

    private string[] GetChapterSceneGuestNames()
    {
        string[] guestNames = new string[ChapterGuestNameAliases.Length];

        for (int i = 0; i < ChapterGuestNameAliases.Length; i++)
        {
            guestNames[i] = FindExistingSceneGuestName(ChapterGuestNameAliases[i]) ?? GetChapterGuestDisplayName(i);
        }

        return guestNames;
    }

    private string GetChapterGuestDisplayName(int index)
    {
        if (index >= 0 && index < ChapterGuestDisplayNames.Length)
        {
            return ChapterGuestDisplayNames[index];
        }

        return $"Guest {index + 1}";
    }

    private string GetChapterGuestObjectName(int index)
    {
        if (index >= 0 && index < ChapterGuestNameAliases.Length && ChapterGuestNameAliases[index].Length > 1)
        {
            return ChapterGuestNameAliases[index][1];
        }

        return $"Guest {index + 1}";
    }

    private string FindExistingSceneGuestName(string[] guestNameAliases)
    {
        if (guestNameAliases == null)
        {
            return null;
        }

        for (int i = 0; i < guestNameAliases.Length; i++)
        {
            if (FindSceneObjectByExactName(guestNameAliases[i]) != null)
            {
                return guestNameAliases[i];
            }
        }

        return null;
    }

    private bool IsUnselectedChapterSceneGuestAlias(GameObject guestObject)
    {
        if (!IsChapterSceneGuest(guestObject))
        {
            return false;
        }

        return !MatchesSceneGuestName(guestObject, GetChapterSceneGuestNames());
    }

    private bool MatchesAnyChapterGuestAlias(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return false;
        }

        for (int i = 0; i < ChapterGuestNameAliases.Length; i++)
        {
            if (MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSceneGuestName(GameObject guestObject, string[] guestNames)
    {
        if (guestObject == null || guestNames == null)
        {
            return false;
        }

        string cleanName = guestObject.name.Trim();

        for (int i = 0; i < guestNames.Length; i++)
        {
            if (string.Equals(cleanName, guestNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetNamedSceneGuestsActive(string[] guestNames, bool active)
    {
        for (int i = 0; i < guestNames.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(guestNames[i]);

            if (guestObject != null)
            {
                guestObject.SetActive(active);
            }
        }
    }

    private void SetChapterSceneGuestAliasesActive(bool active)
    {
        HashSet<GameObject> handledGuests = new HashSet<GameObject>();

        for (int i = 0; i < ChapterGuestNameAliases.Length; i++)
        {
            string[] aliases = ChapterGuestNameAliases[i];

            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                GameObject guestObject = FindSceneObjectByExactName(aliases[aliasIndex]);

                if (guestObject == null || handledGuests.Contains(guestObject))
                {
                    continue;
                }

                guestObject.SetActive(active);
                handledGuests.Add(guestObject);
            }
        }
    }

    private void ActivateAuthoredChapterGuestObject(GuestRuntimeState guestState, int index, int batchCount)
    {
        GameObject guestObject = guestState != null ? guestState.GuestObject : null;

        if (guestObject == null)
        {
            return;
        }

        DisableAmbientWalkers(guestObject);
        guestObject.SetActive(true);

        if (guestObject.transform is RectTransform)
        {
            PlaceChapterSceneGuestAtEntrance(guestState, index, batchCount);
        }

        DisableAmbientWalkers(guestObject);

        ActorRoomState actorState = guestState != null ? guestState.ActorState : null;

        if (actorState == null)
        {
            actorState = guestObject.GetComponent<ActorRoomState>();
        }

        if (actorState != null)
        {
            actorState.enabled = true;
            actorState.SetCurrentRoom(entryRoomId);
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetInteractable(false);
            actorState.ApplyState();
        }

        if (actorState == null)
        {
            ForceRenderersAndCollidersOn(guestObject);
        }

        ApplyEntranceHallGuestSorting(guestState);
        Debug.Log($"Scene guest activated: {guestObject.name}", this);
    }

    private void PlaceChapterSceneGuestAtEntrance(GuestRuntimeState guestState, int index, int batchCount)
    {
        GameObject guestObject = guestState != null ? guestState.GuestObject : null;

        if (guestObject == null)
        {
            return;
        }

        RectTransform rectTransform = guestObject.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = GetEntranceWaitAnchoredPosition(guestState, index, batchCount);
            return;
        }

        guestObject.transform.position = GetEntranceWaitPosition(guestState, index, batchCount);
    }

    private void PlaceGuestAt(GuestRuntimeState guestState, Transform target, string fieldName)
    {
        if (guestState == null)
        {
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"Chapter1ArrivalController missing required field: {fieldName}.", this);
            return;
        }

        string targetRoomId = GetRoomForTransform(target);
        PreserveGuestAuthoredScale(guestState);

        if (TryPlaceProjectedGuestAtTarget(guestState, target))
        {
            SyncGuestScaleParticipantCurrentRoom(guestState, targetRoomId);
            return;
        }

        if (guestState.GuestObject != null &&
            guestState.GuestObject.transform is RectTransform rectTransform &&
            TryGetAnchoredPositionForGuestTarget(guestState, target, out Vector2 anchoredPosition))
        {
            rectTransform.anchoredPosition = anchoredPosition;
            SyncGuestScaleParticipantCurrentRoom(guestState, targetRoomId);
            return;
        }

        if (guestState.GuestObject != null &&
            TryGetWorldPositionForGuestTarget(guestState.GuestObject.transform, target, out Vector3 worldPosition))
        {
            guestState.GuestObject.transform.position = worldPosition;
            BindGuestToRoomStagePoint(guestState, target);
            SyncGuestScaleParticipantCurrentRoom(guestState, targetRoomId);
            return;
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.PlaceAt(target);
            SyncGuestScaleParticipantCurrentRoom(guestState, targetRoomId);
            return;
        }

        if (guestState.GuestObject != null)
        {
            Vector3 targetPosition = target.position;
            targetPosition.z = guestState.GuestObject.transform.position.z;
            guestState.GuestObject.transform.position = targetPosition;
            BindGuestToRoomStagePoint(guestState, target);
            SyncGuestScaleParticipantCurrentRoom(guestState, targetRoomId);
        }
    }

    private void PlaceGuestAtPosition(GuestRuntimeState guestState, Vector3 position)
    {
        if (guestState == null)
        {
            return;
        }

        PreserveGuestAuthoredScale(guestState);

        if (guestState.GuestObject != null)
        {
            Vector3 targetPosition = position;
            targetPosition.z = guestState.GuestObject.transform.position.z;
            guestState.GuestObject.transform.position = targetPosition;
            ClearGuestRoomStagePointBinding(guestState);
            return;
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.transform.position = position;
            guestState.ActorState.ClearRoomStagePointBinding();
        }
    }

    private void PlaceGuestAtDoorArrival(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)
    {
        if (guestState == null)
        {
            return;
        }

        Transform profileTarget = GetWorldDoorArrivalTarget(guestState);
        Vector2 roomLocalPairOffset = GetDoorArrivalPairSlotOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            entranceGuestSpacing * 0.55f);

        if (TryPlaceProjectedGuestFeetAtTarget(guestState, profileTarget, roomLocalPairOffset))
        {
            return;
        }

        Vector3 feetPosition = GetWorldDoorArrivalPosition(guestState, fallbackIndex, fallbackCount);
        PlaceGuestFeetAtPosition(guestState, feetPosition, profileTarget);
    }

    private bool TryPlaceProjectedGuestFeetAtTarget(GuestRuntimeState guestState, Transform target, Vector2 roomLocalPairOffset)
    {
        if (target == null)
        {
            return false;
        }

        RoomProjectedEntity projection = ResolveGuestProjection(guestState);

        if (projection == null)
        {
            return false;
        }

        projection.UseProfileFromRoomTarget(target);

        if (!projection.HasUsableProfile ||
            !projection.TryGetRoomLocalFootPointForTarget(target, out Vector2 footPoint))
        {
            return false;
        }

        projection.SetRoomLocalFootPoint(footPoint + roomLocalPairOffset);
        ClearGuestRoomStagePointBinding(guestState);
        return true;
    }

    private void PlaceGuestFeetAtPosition(GuestRuntimeState guestState, Vector3 feetPosition, Transform profileTarget)
    {
        if (guestState == null)
        {
            return;
        }

        PreserveGuestAuthoredScale(guestState);

        if (TryPlaceProjectedGuestFeetAtPosition(guestState, feetPosition, profileTarget))
        {
            return;
        }

        if (guestState.GuestObject != null)
        {
            Transform guestTransform = guestState.GuestObject.transform;
            Vector3 targetPosition = feetPosition;

            if (TryGetGuestFeetWorldPoint(guestState.GuestObject, true, true, out Vector3 currentFeetPosition))
            {
                Vector3 feetOffset = currentFeetPosition - guestTransform.position;
                targetPosition.x -= feetOffset.x;
                targetPosition.y -= feetOffset.y;
            }

            targetPosition.z = guestTransform.position.z;
            guestTransform.position = targetPosition;
            ClearGuestRoomStagePointBinding(guestState);
            return;
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.transform.position = feetPosition;
            guestState.ActorState.ClearRoomStagePointBinding();
        }
    }

    private bool TryPlaceProjectedGuestFeetAtPosition(GuestRuntimeState guestState, Vector3 feetPosition, Transform profileTarget)
    {
        RoomProjectedEntity projection = ResolveGuestProjection(guestState);

        if (projection == null)
        {
            return false;
        }

        if (profileTarget != null)
        {
            projection.UseProfileFromRoomTarget(profileTarget);
        }

        if (!projection.HasUsableProfile)
        {
            return false;
        }

        if (cameraManager == null ||
            !cameraManager.TryGetActiveRoomStageLocalPoint(feetPosition, out Vector2 roomLocalFootPoint))
        {
            return false;
        }

        projection.SetRoomLocalFootPoint(roomLocalFootPoint);
        ClearGuestRoomStagePointBinding(guestState);
        return projection.IsProjectionActive;
    }

    private void BindGuestToRoomStagePoint(GuestRuntimeState guestState, Transform target)
    {
        if (guestState == null ||
            guestState.ActorState == null ||
            !IsWorldSpaceGuestObject(guestState.GuestObject) ||
            HasActiveProjection(guestState))
        {
            return;
        }

        PreserveGuestAuthoredScale(guestState);
        guestState.ActorState.BindToRoomStagePoint(target);
    }

    private void PreserveGuestAuthoredScale(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return;
        }

        PreserveGuestAuthoredScale(guestState.GuestObject, guestState.ActorState);
    }

    private void PreserveGuestAuthoredScale(GameObject guestObject, ActorRoomState actorState)
    {
        if (guestObject != null && guestObject != playerButlerReference)
        {
            PointClickPlayerMovement[] pointClickMovements = guestObject.GetComponentsInChildren<PointClickPlayerMovement>(true);

            for (int i = 0; i < pointClickMovements.Length; i++)
            {
                if (pointClickMovements[i] != null)
                {
                    pointClickMovements[i].SetPerspectiveScaleEnabled(false);
                }
            }
        }

        if (actorState != null)
        {
            actorState.SetScaleWithRoomStageMotion(true);
        }
    }

    private void ClearGuestRoomStagePointBinding(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.ActorState == null)
        {
            return;
        }

        guestState.ActorState.ClearRoomStagePointBinding();
    }

    private void ForceGuestVisibleForDoorFlow(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return;
        }

        if (guestState.GuestObject != null && !guestState.GuestObject.activeSelf)
        {
            guestState.GuestObject.SetActive(true);
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.enabled = true;
            guestState.ActorState.SetCurrentRoom(entryRoomId);
            guestState.ActorState.SetAvailableInCurrentChapter(true);
            guestState.ActorState.SetVisibleByChapterState(true);
            guestState.ActorState.SetInteractable(false);
            guestState.ActorState.ApplyState();
        }

        if (guestState.GuestObject == null)
        {
            return;
        }

        if (IsChapterSceneGuest(guestState.GuestObject))
        {
            DisableAmbientWalkers(guestState.GuestObject);
        }

        if (guestState.ActorState == null)
        {
            ForceRenderersAndCollidersOn(guestState.GuestObject);
        }

        ApplyEntranceHallGuestSorting(guestState);
        RefreshCoatPickupVisibilityForCurrentRoom(guestState);
    }

    private void RefreshAllGuestRoomVisibility()
    {
        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState guestState = guestStates[i];

            if (guestState == null)
            {
                continue;
            }

            if (guestState.ActorState != null)
            {
                guestState.ActorState.enabled = true;
                guestState.ActorState.ApplyState();
            }

            RefreshCoatPickupVisibilityForCurrentRoom(guestState);
        }
    }

    private void RefreshCoatPickupVisibilityForCurrentRoom(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.CoatPickup == null)
        {
            return;
        }

        bool coatIsWaitingOnGuest = guestState.CoatOffered && !guestState.CoatTaken && !guestState.CoatStored;
        bool coatShouldBeVisibleAndClickable = coatIsWaitingOnGuest && IsGuestVisibleInCurrentRoom(guestState);

        if (coatIsWaitingOnGuest)
        {
            SetCoatPickupRenderersVisible(guestState.CoatPickup.gameObject, coatShouldBeVisibleAndClickable);
        }

        Collider2D collider = guestState.CoatPickup.GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.enabled = coatShouldBeVisibleAndClickable;
        }

        guestState.CoatPickup.enabled = coatShouldBeVisibleAndClickable;
    }

    private bool IsGuestVisibleInCurrentRoom(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return false;
        }

        if (guestState.ActorState != null)
        {
            return guestState.ActorState.IsVisibleInCurrentRoom;
        }

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return true;
        }

        if (guestState.Seated)
        {
            return SameRoom(navigationManager.CurrentRoom, drawingRoomId);
        }

        return guestState.EnteredEntranceHall && SameRoom(navigationManager.CurrentRoom, entryRoomId);
    }

    private static void SetCoatPickupRenderersVisible(GameObject coatObject, bool visible)
    {
        if (coatObject == null)
        {
            return;
        }

        Renderer[] renderers = coatObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }

        Graphic[] graphics = coatObject.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].enabled = visible;
            }
        }
    }

    private void ForceRenderersAndCollidersOn(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].enabled = true;
            }
        }

        Collider[] colliders3D = target.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders3D.Length; i++)
        {
            if (colliders3D[i] != null)
            {
                colliders3D[i].enabled = true;
            }
        }

        Collider2D[] colliders2D = target.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
            {
                colliders2D[i].enabled = true;
            }
        }

        CanvasGroup[] canvasGroups = target.GetComponentsInChildren<CanvasGroup>(true);

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].alpha = 1f;
            }
        }
    }

    private IEnumerator MoveGuestTo(GuestRuntimeState guestState, Transform target, string fieldName)
    {
        if (guestState == null)
        {
            yield break;
        }

        if (target == null)
        {
            Debug.LogWarning($"Chapter1ArrivalController missing required field: {fieldName}.", this);
            yield break;
        }

        NPCWaypointMover mover = guestState.Mover;

        if (mover == null)
        {
            PlaceGuestAt(guestState, target, fieldName);
            EnsureGuestScaleParticipant(guestState, ResolveGuestScaleRoomId(guestState), ResolveGuestScalePose(guestState));
            RefreshGuestScalingNow();
            yield break;
        }

        mover.enabled = true;
        mover.MoveSpeed = GetMoveSpeedForGuestObject(guestState.GuestObject);
        StartGuestFootsteps(guestState);
        mover.MoveTo(target);

        while (mover != null && mover.IsMoving)
        {
            yield return null;
        }

        StopGuestFootsteps(guestState);
        BindGuestToRoomStagePoint(guestState, target);
        EnsureGuestScaleParticipant(guestState, ResolveGuestScaleRoomId(guestState), ResolveGuestScalePose(guestState));
        RefreshGuestScalingNow();
    }

    private void BeginGuestMoveTo(GuestRuntimeState guestState, Transform target, string fieldName)
    {
        if (guestState == null)
        {
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"Chapter1ArrivalController missing required field: {fieldName}.", this);
            return;
        }

        NPCWaypointMover mover = guestState.Mover;

        if (mover == null)
        {
            PlaceGuestAt(guestState, target, fieldName);
            return;
        }

        mover.enabled = true;
        mover.MoveSpeed = GetMoveSpeedForGuestObject(guestState.GuestObject);
        StartGuestFootsteps(guestState);
        mover.MoveTo(target);
    }

    private void DisableGuestMovement(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return;
        }

        StopGuestFootsteps(guestState);

        if (guestState.Mover == null)
        {
            return;
        }

        guestState.Mover.StopMoving();
        guestState.Mover.SetAmbientWalkerEnabled(false);
        guestState.Mover.enabled = false;
    }

    private static void SetGuestVisibleAfterDrawingRoomExit(GuestRuntimeState guestState, bool visible)
    {
        if (guestState == null || guestState.GuestObject == null)
        {
            return;
        }

        guestState.GuestObject.SetActive(visible);
    }

    private void StartAmbientConversation(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.Config == null)
        {
            return;
        }

        SetGuestState(guestState, GuestArrivalState.AmbientIdle);

        if (guestState.Config.AmbientLines == null || guestState.Config.AmbientLines.Count == 0)
        {
            Debug.LogWarning($"Guest '{guestState.Config.GuestId}' has no ambient lines. TODO: add final seated bark text.", this);
            return;
        }

        Debug.Log($"{guestState.Config.GuestDisplayName} ambient: {guestState.Config.AmbientLines[0]}", this);
        ShowGuestSubtitle(guestState, "AMBIENT", guestState.Config.AmbientLines[0]);
    }

    private string GetGuestGreetingLine(GuestRuntimeState guestState)
    {
        if (guestState != null &&
            guestState.Config != null &&
            !string.IsNullOrWhiteSpace(guestState.Config.GreetingLine))
        {
            return guestState.Config.GreetingLine.Trim();
        }

        return GetDefaultGreeting(guestState != null ? guestState.GuestIndex : 0);
    }

    private void ShowGuestSubtitle(GuestRuntimeState guestState, string lineKind, string text)
    {
        if (guestState == null || guestState.Config == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        QueueGuestLine(guestState, lineKind, text);
    }

    private void QueueGuestLine(GuestRuntimeState guestState, string lineKind, string text)
    {
        if (guestState == null || guestState.Config == null)
        {
            return;
        }

        string lineId = GetChapter1GuestLineId(guestState.GuestIndex, lineKind);
        DialogueSpeechService service = ResolveSpeechService();
        service?.BeginSpeakLine(lineId, guestState.Config.GuestDisplayName, text, false, false);
    }

    private void QueueButlerLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId))
        {
            return;
        }

        DialogueSpeechService service = ResolveSpeechService();
        service?.BeginSpeakLine(lineId, "Butler", null, false, false);
    }

    private DialogueSpeechService.SpeechInterruption CancelSpeechForCoatPickup()
    {
        DialogueSpeechService service = ResolveSpeechService();
        return service != null ? service.CancelQueuedSpeech() : default;
    }

    private bool TryFindGuestForSpeechInterruption(
        DialogueSpeechService.SpeechInterruption interruption,
        out GuestRuntimeState guestState)
    {
        guestState = null;

        if (TryResolveGuestNumberFromSpeechLineId(interruption.LineId, out int guestNumber) &&
            TryFindGuestByNumber(guestNumber, out guestState))
        {
            return true;
        }

        if (TryFindGuestBySpeakerName(interruption.SpeakerId, out guestState) ||
            TryFindGuestBySpeakerName(interruption.SpeakerDisplayName, out guestState))
        {
            return true;
        }

        return false;
    }

    private bool TryFindGuestByNumber(int guestNumber, out GuestRuntimeState guestState)
    {
        guestState = null;
        int guestIndex = guestNumber - 1;

        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState candidate = guestStates[i];

            if (candidate != null && candidate.GuestIndex == guestIndex)
            {
                guestState = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindGuestBySpeakerName(string speakerName, out GuestRuntimeState guestState)
    {
        guestState = null;

        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return false;
        }

        if (TryResolveGuestNumberFromText(speakerName, out int guestNumber) &&
            TryFindGuestByNumber(guestNumber, out guestState))
        {
            return true;
        }

        if (TryResolveNamedGuestCoatNumber(speakerName, out guestNumber) &&
            TryFindGuestByNumber(guestNumber, out guestState))
        {
            return true;
        }

        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState candidate = guestStates[i];

            if (candidate != null &&
                candidate.Config != null &&
                string.Equals(candidate.Config.GuestDisplayName, speakerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                guestState = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveGuestNumberFromSpeechLineId(string lineId, out int guestNumber)
    {
        return TryResolveGuestNumberAfterPrefix(lineId, "CH1_G", out guestNumber) ||
            TryResolveGuestNumberAfterPrefix(lineId, "CH2_G", out guestNumber) ||
            TryResolveGuestNumberAfterPrefix(lineId, "SUB_CH01_G", out guestNumber) ||
            TryResolveGuestNumberAfterPrefix(lineId, "SUB_CH02_G", out guestNumber);
    }

    private static bool TryResolveGuestNumberAfterPrefix(string value, string prefix, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int digitStart = prefix.Length;
        int digitEnd = digitStart;

        while (digitEnd < value.Length && char.IsDigit(value[digitEnd]))
        {
            digitEnd++;
        }

        return digitEnd > digitStart &&
            int.TryParse(value.Substring(digitStart, digitEnd - digitStart), out guestNumber) &&
            guestNumber > 0;
    }

    private IEnumerator SpeakButlerLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId))
        {
            yield break;
        }

        DialogueSpeechService service = ResolveSpeechService();

        if (service != null)
        {
            yield return service.SpeakLine(lineId, "Butler", null, false, false);
        }
    }

    private static string GetChapter1GuestSubtitleLineId(int guestIndex, string lineKind)
    {
        int guestNumber = Mathf.Clamp(guestIndex + 1, 1, 99);
        string cleanLineKind = string.IsNullOrWhiteSpace(lineKind) ? "GREETING" : lineKind.Trim().ToUpperInvariant();
        return $"SUB_CH01_G{guestNumber:00}_{cleanLineKind}_001";
    }

    private static string GetChapter1GuestLineId(int guestIndex, string lineKind)
    {
        int guestNumber = Mathf.Clamp(guestIndex + 1, 1, 99);
        string cleanLineKind = string.IsNullOrWhiteSpace(lineKind) ? "GREETING" : lineKind.Trim().ToUpperInvariant();

        switch (cleanLineKind)
        {
            case "ENTRY":
            case "GREETING":
                return $"CH1_G{guestNumber:00}_ENTRY";
            case "DELAYED":
            case "ANNOYED":
                return $"CH1_G{guestNumber:00}_DELAYED";
            case "COAT":
            case "COAT_HANDOFF":
                return $"CH1_G{guestNumber:00}_COAT_HANDOFF";
            case "INTERRUPTED":
            case "INTERRUPTION":
                return $"CH1_G{guestNumber:00}_INTERRUPTED";
            case "TO_DRAWING":
            case "TO_DRAWING_ROOM":
                return $"CH1_G{guestNumber:00}_TO_DRAWING_ROOM";
            case "AMBIENT":
                return GetChapter1GuestSubtitleLineId(guestIndex, "AMBIENT");
            default:
                return $"CH1_G{guestNumber:00}_{cleanLineKind}";
        }
    }

    private void ShowSubtitleLine(string lineId)
    {
        DialogueSpeechService service = ResolveSpeechService();
        service?.BeginSpeakLine(lineId, null, null, false, false);
    }

    private void ShowSubtitleLine(string lineId, string speaker, string text, bool requireAdvance)
    {
        DialogueSpeechService service = ResolveSpeechService();
        service?.BeginSpeakLine(lineId, speaker, text, false, false);
    }

    private DialogueSpeechService ResolveSpeechService()
    {
        if (!enableSubtitles || !Application.isPlaying)
        {
            return null;
        }

        return speechService;
    }

    private SubtitleService ResolveSubtitleService()
    {
        if (!enableSubtitles || !Application.isPlaying)
        {
            return null;
        }

        subtitleService?.SetDebugMode(subtitleDebugMode);
        return subtitleService;
    }

    private void LogGuestLine(GuestArrivalConfig config, string line)
    {
        if (config == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            Debug.LogWarning($"Guest '{config.GuestId}' has no greetingLine assigned.", this);
            return;
        }

        Debug.Log($"{config.GuestDisplayName}: {line}", this);
    }

    private void SetGuestState(GuestRuntimeState guestState, GuestArrivalState nextState)
    {
        if (guestState == null || guestState.State == nextState)
        {
            return;
        }

        guestState.State = nextState;

        if (verboseLogs)
        {
            string guestId = guestState.Config != null ? guestState.Config.GuestId : "unknown_guest";
            Debug.Log($"Chapter 1 guest state changed: {guestId} -> {nextState}", this);
        }
    }

    private void RefreshDoorbellRinging()
    {
        if (doorbellSystem == null)
        {
            return;
        }

        if (pendingGuestGroups.Count == 0)
        {
            if (!emptyDoorbellWaitingForAnswer)
            {
                doorbellSystem.StopRinging();
            }

            return;
        }

        doorbellSystem.RefreshQueueState(GetOldestPendingQueuedGameMinute(), true, false);
    }

    private void RefreshInteractionState()
    {
        if (interactionHUD != null)
        {
            interactionHUD.SetHangCoatAvailable(butlerCarryingCoat);
        }

        if (frontDoorSceneAction != null)
        {
            frontDoorSceneAction.SetAvailable(IsFrontDoorActionAvailableNow());
        }
    }

    private bool IsFrontDoorActionAvailableNow()
    {
        return HasDoorAnswerWaiting();
    }

    private bool HasDoorAnswerWaiting()
    {
        return pendingGuestGroups.Count > 0 ||
            emptyDoorbellWaitingForAnswer ||
            IsDoorbellRinging();
    }

    private bool IsDoorbellRinging()
    {
        return doorbellSystem != null && doorbellSystem.IsRinging;
    }

    private bool IsPlayerInEntryRoom()
    {
        return navigationManager == null ||
            string.IsNullOrWhiteSpace(navigationManager.CurrentRoom) ||
            SameRoom(navigationManager.CurrentRoom, entryRoomId);
    }

    private void EnsureRuntimeInteractionSystems()
    {
        ResolveReferences();

        doorbellSystem?.Initialize(chapterClock);
        timeSettingsUI?.Initialize(chapterClock);

        if (interactionHUD != null)
        {
            interactionHUD.Initialize(this);
        }

        ConfigureFrontDoorAction();
    }

    private void ConfigureFrontDoorAction()
    {
        if (frontDoorSceneAction == null)
        {
            return;
        }

        frontDoorSceneAction.gameObject.SetActive(true);
        frontDoorSceneAction.Initialize(Chapter1SceneActionType.FrontDoor, this);
        frontDoorSceneAction.SetAvailable(true);
    }

    private void EnsureGuestConfigs(bool createFallbacks)
    {
        guests.RemoveAll(guest => guest == null);
        int namedSceneGuestCount = EnsureNamedSceneGuestsConfigured();
        int adoptedSceneGuestCount = AdoptExistingSceneGuests();
        int runtimeGuestCount = EnsureRequiredGuestConfigs(createFallbacks);
        int totalSceneGuestCount = namedSceneGuestCount + adoptedSceneGuestCount;

        if (totalSceneGuestCount > 0)
        {
            Debug.Log($"Chapter 1 using {totalSceneGuestCount} existing scene guest object(s) for the arrival sequence.", this);
        }

        if (runtimeGuestCount > 0)
        {
            Debug.Log($"Chapter 1 created {runtimeGuestCount} runtime guest object(s) to reach the required eight guests.", this);
        }
    }

    private int EnsureNamedSceneGuestsConfigured()
    {
        int addedCount = 0;
        int insertIndex = 0;
        string[] guestNames = GetChapterSceneGuestNames();

        for (int i = 0; i < guestNames.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(guestNames[i]);

            if (guestObject == null)
            {
                continue;
            }

            int existingIndex = FindGuestConfigIndexForObject(guestObject);

            if (existingIndex >= 0)
            {
                PrepareSceneGuestObject(guestObject, insertIndex);
                string displayName = GetChapterGuestDisplayName(insertIndex);
                guests[existingIndex].ConfigureRuntime(
                    MakeGuestId(guestObject.name, insertIndex),
                    displayName,
                    guestObject,
                    frontDoorArrivalPoint,
                    drawingRoomEntryPoint,
                    ResolveSeatForGuest(insertIndex),
                    GetDefaultGreeting(insertIndex),
                    new[] { GetDefaultAmbientLine(insertIndex) },
                    $"{MakeGuestId(guestObject.name, insertIndex)}_coat");
                MoveGuestConfig(existingIndex, insertIndex);
                insertIndex++;
                continue;
            }

            PrepareSceneGuestObject(guestObject, insertIndex);

            GuestArrivalConfig config = new GuestArrivalConfig();
            string newDisplayName = GetChapterGuestDisplayName(insertIndex);
            config.ConfigureRuntime(
                MakeGuestId(guestObject.name, insertIndex),
                newDisplayName,
                guestObject,
                frontDoorArrivalPoint,
                drawingRoomEntryPoint,
                ResolveSeatForGuest(insertIndex),
                GetDefaultGreeting(insertIndex),
                new[] { GetDefaultAmbientLine(insertIndex) },
                $"{MakeGuestId(guestObject.name, insertIndex)}_coat");

            guests.Insert(Mathf.Min(insertIndex, guests.Count), config);
            insertIndex++;
            addedCount++;
        }

        return addedCount;
    }

    private int EnsureRequiredGuestConfigs(bool createFallbacks)
    {
        int requestedGuestCount = GetRequestedGuestCount();

        if (!createFallbacks)
        {
            return 0;
        }

        int createdCount = 0;

        for (int i = 0; i < requestedGuestCount; i++)
        {
            GameObject configuredObject = i < guests.Count && guests[i] != null
                ? guests[i].ResolveGuestObject()
                : null;

            if (configuredObject != null && MatchesSceneGuestName(configuredObject, ChapterGuestNameAliases[i]))
            {
                continue;
            }

            GameObject guestObject = FindChapterGuestObjectByIndex(i);

            if (guestObject == null)
            {
                guestObject = CreateRuntimeGuestObject(i);
                createdCount++;
            }

            if (guestObject == null)
            {
                continue;
            }

            PrepareSceneGuestObject(guestObject, i);

            GuestArrivalConfig config = new GuestArrivalConfig();
            string guestId = MakeGuestId(guestObject.name, i);
            string displayName = GetChapterGuestDisplayName(i);
            config.ConfigureRuntime(
                guestId,
                displayName,
                guestObject,
                frontDoorArrivalPoint,
                drawingRoomEntryPoint,
                ResolveSeatForGuest(i),
                GetDefaultGreeting(i),
                new[] { GetDefaultAmbientLine(i) },
                $"{guestId}_coat");

            if (i < guests.Count)
            {
                guests[i] = config;
            }
            else
            {
                guests.Add(config);
            }
        }

        return createdCount;
    }

    private GameObject FindChapterGuestObjectByIndex(int index)
    {
        if (index < 0 || index >= ChapterGuestNameAliases.Length)
        {
            return null;
        }

        string[] aliases = ChapterGuestNameAliases[index];

        for (int i = 0; i < aliases.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(aliases[i]);

            if (guestObject != null)
            {
                return guestObject;
            }
        }

        return null;
    }

    private GameObject CreateRuntimeGuestObject(int index)
    {
        string guestName = GetChapterGuestObjectName(index);
        GameObject template = FindRuntimeGuestTemplate();
        Vector3 startPosition = GetWorldEntranceWaitPosition(index % Mathf.Max(1, guestsPerArrivalGroup), Mathf.Max(1, guestsPerArrivalGroup));
        GameObject guestObject;

        if (template != null)
        {
            guestObject = Instantiate(template, startPosition, template.transform.rotation, template.transform.parent);
            guestObject.transform.localScale = template.transform.localScale;
            guestObject.name = guestName;
        }
        else
        {
            guestObject = new GameObject(guestName);
            guestObject.transform.position = startPosition;
            SpriteRenderer renderer = CreateRuntimeVisual(guestObject.transform, "Visual_Guest", GetRuntimeGuestSprite(), 0.03f);
            renderer.sortingLayerName = "People";
            renderer.sortingOrder = 9000 + index;

            BoxCollider2D collider = guestObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.55f, 0.9f);
            collider.isTrigger = true;
        }

        runtimeGeneratedGuestObjects.Add(guestObject);
        guestObject.SetActive(false);
        return guestObject;
    }

    private GameObject FindRuntimeGuestTemplate()
    {
        for (int i = 1; i < ChapterGuestNameAliases.Length; i++)
        {
            GameObject guestObject = FindChapterGuestObjectByIndex(i);

            if (guestObject != null && !runtimeGeneratedGuestObjects.Contains(guestObject))
            {
                return guestObject;
            }
        }

        if (playerButlerReference != null)
        {
            return playerButlerReference;
        }

        GameObject firstGuestObject = FindChapterGuestObjectByIndex(0);

        if (firstGuestObject != null && !runtimeGeneratedGuestObjects.Contains(firstGuestObject))
        {
            return firstGuestObject;
        }

        return null;
    }

    private int AdoptExistingSceneGuests()
    {
        if (!useExistingSceneGuestsFirst)
        {
            return 0;
        }

        List<GameObject> candidates = FindSceneGuestCandidates();
        int adoptedCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];

            if (guests.Count >= GetRequestedGuestCount())
            {
                break;
            }

            if (candidate == null ||
                HasGuestConfigForObject(candidate) ||
                IsUnselectedChapterSceneGuestAlias(candidate) ||
                !IsChapterSceneGuest(candidate))
            {
                continue;
            }

            GuestArrivalConfig config = new GuestArrivalConfig();
            int nextIndex = guests.Count;
            PrepareSceneGuestObject(candidate, nextIndex);
            string displayName = GetChapterGuestDisplayName(nextIndex);
            config.ConfigureRuntime(
                MakeGuestId(candidate.name, nextIndex),
                displayName,
                candidate,
                frontDoorArrivalPoint,
                drawingRoomEntryPoint,
                ResolveSeatForGuest(nextIndex),
                GetDefaultGreeting(nextIndex),
                new[] { GetDefaultAmbientLine(nextIndex) },
                $"{MakeGuestId(candidate.name, nextIndex)}_coat");
            guests.Add(config);
            adoptedCount++;
        }

        return adoptedCount;
    }

    private List<GameObject> FindSceneGuestCandidates()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> candidates = new List<GameObject>();
        HashSet<GameObject> uniqueCandidates = new HashSet<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = GetSceneGuestCandidateRoot(allObjects[i]);

            if (candidate == null || uniqueCandidates.Contains(candidate))
            {
                continue;
            }

            uniqueCandidates.Add(candidate);
            candidates.Add(candidate);
        }

        candidates.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        return candidates;
    }

    private GameObject GetSceneGuestCandidateRoot(GameObject candidate)
    {
        if (!IsSceneGuestCandidate(candidate))
        {
            return null;
        }

        Transform current = candidate.transform.parent;

        while (current != null)
        {
            if (IsSceneGuestCandidate(current.gameObject))
            {
                return null;
            }

            current = current.parent;
        }

        return candidate;
    }

    private bool IsSceneGuestCandidate(GameObject candidate)
    {
        if (candidate == null ||
            candidate == gameObject ||
            candidate == playerButlerReference ||
            !candidate.scene.IsValid() ||
            !candidate.scene.isLoaded)
        {
            return false;
        }

        string cleanName = candidate.name.Trim();
        string normalizedName = NormalizeObjectName(cleanName);
        bool hasGuestName = normalizedName.Contains("guest");

        if (!hasGuestName ||
            normalizedName.Contains("guestarrival") ||
            normalizedName.Contains("clicktarget") ||
            normalizedName.Contains("visual") ||
            normalizedName.Contains("coat") ||
            normalizedName.Contains("anchor") ||
            normalizedName.Contains("chapter"))
        {
            return false;
        }

        return candidate.GetComponentInChildren<SpriteRenderer>(true) != null ||
            candidate.GetComponentInChildren<Animator>(true) != null;
    }

    private bool HasGuestConfigForObject(GameObject guestObject)
    {
        return FindGuestConfigIndexForObject(guestObject) >= 0;
    }

    private int FindGuestConfigIndexForObject(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return -1;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null && guests[i].ResolveGuestObject() == guestObject)
            {
                return i;
            }
        }

        return -1;
    }

    private void MoveGuestConfig(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 ||
            fromIndex >= guests.Count ||
            toIndex < 0 ||
            toIndex >= guests.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        GuestArrivalConfig config = guests[fromIndex];
        guests.RemoveAt(fromIndex);
        guests.Insert(Mathf.Min(toIndex, guests.Count), config);
    }

    private GameObject FindSceneObjectByExactName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (candidate == null ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded ||
                !string.Equals(candidate.name.Trim(), objectName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private int CountConfiguredGuestObjects()
    {
        int count = 0;

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null && guests[i].ResolveGuestObject() != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetRequiredGuestCountForCurrentRun()
    {
        return GetRequestedGuestCount();
    }

    private int GetRequestedGuestCount()
    {
        return Mathf.Min(
            ChapterGuestNameAliases.Length,
            Mathf.Max(1, guestGroupCount) * Mathf.Max(1, guestsPerArrivalGroup));
    }

    private void PrepareSceneGuestObject(GameObject guestObject, int index)
    {
        if (guestObject == null)
        {
            return;
        }

        DisablePlayerOnlyComponents(guestObject);
        Vector3 authoredGuestScale = guestObject.transform.localScale;
        DisableAmbientWalkers(guestObject);
        ConfigureGuestPhysicsForScriptedMovement(guestObject);
        ConfigureGuestAnimatorForIndex(guestObject, index);

        ActorRoomState actorState = guestObject.GetComponent<ActorRoomState>();

        if (actorState == null)
        {
            actorState = guestObject.AddComponent<ActorRoomState>();
        }

        actorState.SetActorId(MakeGuestId(guestObject.name, index));
        actorState.SetScaleWithRoomStageMotion(true);
        guestObject.transform.localScale = authoredGuestScale;
        RoomProjectedEntity projection = ResolveGuestProjection(guestObject, actorState);
        ConfigureGuestFootsteps(guestObject, index + 1);

        bool preserveAuthoredSorting = ShouldPreserveAuthoredGuestSorting(guestObject);
        SpriteRenderer[] renderers = guestObject.GetComponentsInChildren<SpriteRenderer>(true);

        if (projection != null && projection.IsProjectionActive)
        {
            CacheGuestAuthoredSorting(guestObject);
            projection.RefreshVisualTargets();
            projection.ApplyProjection();
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (preserveAuthoredSorting)
            {
                continue;
            }

            renderers[i].sortingLayerName = "People";
            renderers[i].sortingOrder = 9000 + index;
        }

        CacheGuestAuthoredSorting(guestObject);
    }

    private bool ShouldPreserveAuthoredGuestSorting(GameObject guestObject)
    {
        return guestObject != null &&
            guestObject.scene.IsValid() &&
            guestObject.scene.isLoaded &&
            !runtimeGeneratedGuestObjects.Contains(guestObject);
    }

    private void CacheGuestAuthoredSorting(GameObject guestObject)
    {
        Renderer[] renderers = GetGuestRenderers(guestObject);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || authoredGuestRendererSorting.ContainsKey(renderer))
            {
                continue;
            }

            authoredGuestRendererSorting.Add(renderer, new RendererSortingState
            {
                LayerName = renderer.sortingLayerName,
                Order = renderer.sortingOrder
            });
        }

        RemoveDestroyedGuestSortingCacheEntries();
    }

    private void ApplyEntranceHallGuestSorting(GuestRuntimeState guestState)
    {
        if (guestState == null ||
            guestState.GuestObject == null ||
            guestState.MovingToDrawingRoom ||
            guestState.Seated ||
            HasActiveProjection(guestState))
        {
            return;
        }

        if (guestState.ActorState != null &&
            !SameRoom(guestState.ActorState.CurrentRoomId, entryRoomId))
        {
            return;
        }

        CacheGuestAuthoredSorting(guestState.GuestObject);

        Renderer[] renderers = GetGuestRenderers(guestState);

        if (renderers.Length == 0)
        {
            return;
        }

        int groupIndex = Mathf.Max(0, guestState.GroupIndex);
        int groupSize = Mathf.Max(1, guestsPerArrivalGroup);
        int slotIndex = Mathf.Max(0, guestState.GuestIndex) % groupSize;
        int sortingOrder = entranceGuestSortingOrderBase +
            groupIndex * Mathf.Max(1, entranceGuestSortingOrderGroupStep) +
            slotIndex * Mathf.Max(1, entranceGuestSortingOrderSlotStep);
        int authoredReferenceOrder = GetGuestRendererReferenceSortingOrder(guestState, renderers);
        string sortingLayerName = ResolveSortingLayerName(entranceGuestSortingLayerName);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            int localOffset = GetCachedSortingOrder(renderer) - authoredReferenceOrder;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder + localOffset;
        }
    }

    private void RestoreGuestAuthoredSorting(GuestRuntimeState guestState)
    {
        Renderer[] renderers = GetGuestRenderers(guestState);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                !authoredGuestRendererSorting.TryGetValue(renderer, out RendererSortingState originalSorting))
            {
                continue;
            }

            renderer.sortingLayerName = originalSorting.LayerName;
            renderer.sortingOrder = originalSorting.Order;
        }

        RemoveDestroyedGuestSortingCacheEntries();
    }

    private void ApplyDrawingRoomGuestDepthSorting(GuestRuntimeState guestState, Transform drawingRoomSpot)
    {
        if (guestState == null ||
            guestState.GuestObject == null ||
            drawingRoomSpot == null ||
            HasActiveProjection(guestState) ||
            !TryGetRoomLocalFootPoint(drawingRoomSpot, out Vector2 roomLocalFootPoint) ||
            !TryGetPerspectiveProfileForTarget(drawingRoomSpot, out RoomPerspectiveProfile profile))
        {
            return;
        }

        CacheGuestAuthoredSorting(guestState.GuestObject);
        Renderer[] renderers = GetGuestRenderers(guestState);

        if (renderers.Length == 0)
        {
            return;
        }

        int depthSortingOrder = profile.GetSortingOrder(roomLocalFootPoint);
        int referenceOrder = GetGuestRendererReferenceSortingOrder(guestState, renderers);
        string sortingLayerName = profile.SortingLayerName;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            int localOffset = GetCachedSortingOrder(renderer) - referenceOrder;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = depthSortingOrder + localOffset;
        }
    }

    private static bool TryGetPerspectiveProfileForTarget(Transform target, out RoomPerspectiveProfile profile)
    {
        profile = null;

        if (target == null)
        {
            return false;
        }

        RoomContentGroup roomContent = target.GetComponentInParent<RoomContentGroup>(true);
        return roomContent != null && roomContent.TryGetPerspectiveProfile(out profile);
    }

    private static bool TryGetRoomLocalFootPoint(Transform target, out Vector2 roomLocalFootPoint)
    {
        roomLocalFootPoint = Vector2.zero;

        if (target == null)
        {
            return false;
        }

        RoomContentGroup roomContent = target.GetComponentInParent<RoomContentGroup>(true);

        if (roomContent != null)
        {
            Vector3 localPoint = roomContent.transform.InverseTransformPoint(target.position);
            roomLocalFootPoint = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        if (target is RectTransform targetRectTransform)
        {
            roomLocalFootPoint = targetRectTransform.anchoredPosition;
            return true;
        }

        roomLocalFootPoint = new Vector2(target.localPosition.x, target.localPosition.y);
        return true;
    }

    private void ApplyEntranceBanisterSafeWalkingSorting(GuestRuntimeState guestState)
    {
        if (guestState == null ||
            guestState.GuestObject == null ||
            HasActiveProjection(guestState))
        {
            return;
        }

        CacheGuestAuthoredSorting(guestState.GuestObject);
        Renderer[] renderers = GetGuestRenderers(guestState);

        if (renderers.Length == 0)
        {
            return;
        }

        int referenceOrder = GetGuestRendererReferenceSortingOrder(guestState, renderers);

        if (referenceOrder <= EntranceBanisterSafeWalkingSortingOrder)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            int localOffset = GetCachedSortingOrder(renderer) - referenceOrder;
            renderer.sortingOrder = EntranceBanisterSafeWalkingSortingOrder + localOffset;
        }
    }

    private int GetGuestRendererReferenceSortingOrder(GuestRuntimeState guestState, Renderer[] renderers)
    {
        SpriteRenderer characterRenderer = guestState != null ? FindCharacterSpriteRenderer(guestState.GuestObject) : null;

        if (characterRenderer != null)
        {
            return GetCachedSortingOrder(characterRenderer);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                return GetCachedSortingOrder(renderers[i]);
            }
        }

        return 0;
    }

    private int GetCachedSortingOrder(Renderer renderer)
    {
        return renderer != null &&
            authoredGuestRendererSorting.TryGetValue(renderer, out RendererSortingState originalSorting)
                ? originalSorting.Order
                : renderer != null
                    ? renderer.sortingOrder
                    : 0;
    }

    private static Renderer[] GetGuestRenderers(GuestRuntimeState guestState)
    {
        return GetGuestRenderers(guestState != null ? guestState.GuestObject : null);
    }

    private static Renderer[] GetGuestRenderers(GameObject guestObject)
    {
        return guestObject != null
            ? guestObject.GetComponentsInChildren<Renderer>(true)
            : Array.Empty<Renderer>();
    }

    private void RemoveDestroyedGuestSortingCacheEntries()
    {
        if (authoredGuestRendererSorting.Count == 0)
        {
            return;
        }

        List<Renderer> destroyedRenderers = null;

        foreach (Renderer renderer in authoredGuestRendererSorting.Keys)
        {
            if (renderer != null)
            {
                continue;
            }

            if (destroyedRenderers == null)
            {
                destroyedRenderers = new List<Renderer>();
            }

            destroyedRenderers.Add(renderer);
        }

        if (destroyedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < destroyedRenderers.Count; i++)
        {
            authoredGuestRendererSorting.Remove(destroyedRenderers[i]);
        }
    }

    private static string ResolveSortingLayerName(string sortingLayerName)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return "Default";
        }

        return string.Equals(sortingLayerName, "Default", StringComparison.OrdinalIgnoreCase) ||
            SortingLayer.NameToID(sortingLayerName) != 0
                ? sortingLayerName
                : "Default";
    }

    private void DisableAmbientWalkers(GameObject guestObject)
    {
        RoomPersonWalker2D[] walkers = guestObject.GetComponentsInChildren<RoomPersonWalker2D>(true);

        for (int i = 0; i < walkers.Length; i++)
        {
            if (walkers[i] != null)
            {
                walkers[i].enabled = false;
            }
        }
    }

    private void DisablePlayerOnlyComponents(GameObject guestObject)
    {
        if (guestObject == null || guestObject == playerButlerReference)
        {
            return;
        }

        PointClickPlayerMovement[] pointClickMovements = guestObject.GetComponentsInChildren<PointClickPlayerMovement>(true);

        for (int i = 0; i < pointClickMovements.Length; i++)
        {
            if (pointClickMovements[i] != null)
            {
                pointClickMovements[i].SetPerspectiveScaleEnabled(false);
                pointClickMovements[i].SetPlayerSortingEnabled(false);
                pointClickMovements[i].enabled = false;
            }
        }

        PlayerMovement[] legacyMovements = guestObject.GetComponentsInChildren<PlayerMovement>(true);

        for (int i = 0; i < legacyMovements.Length; i++)
        {
            if (legacyMovements[i] != null)
            {
                legacyMovements[i].enabled = false;
            }
        }

        CharacterController2D[] legacyControllers = guestObject.GetComponentsInChildren<CharacterController2D>(true);

        for (int i = 0; i < legacyControllers.Length; i++)
        {
            if (legacyControllers[i] != null)
            {
                legacyControllers[i].enabled = false;
            }
        }
    }

    private void ConfigureGuestPhysicsForScriptedMovement(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return;
        }

        Rigidbody2D[] bodies = guestObject.GetComponentsInChildren<Rigidbody2D>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];

            if (body == null)
            {
                continue;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.freezeRotation = true;
        }
    }

    private void ConfigureGuestAnimatorForIndex(GameObject guestObject, int index)
    {
        if (ShouldUseAuthoredLadyGuestAnimation(guestObject, index) ||
            ShouldUseAuthoredButlerGuestAnimation(guestObject, index) ||
            ShouldUseAuthoredMisterFlorianGuestAnimation(guestObject, index) ||
            ShouldUseAuthoredCountessGuestAnimation(guestObject, index) ||
            ShouldUseAuthoredLaterGuestAnimation(guestObject, index))
        {
            return;
        }

        EnsureGuestAnimatorUsesButlerController(guestObject);
    }

    private bool ShouldUseAuthoredLadyGuestAnimation(GameObject guestObject, int index)
    {
        return index == 0 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[0]);
    }

    private bool ShouldUseAuthoredButlerGuestAnimation(GameObject guestObject, int index)
    {
        return index == 1 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[1]);
    }

    private bool ShouldUseAuthoredMisterFlorianGuestAnimation(GameObject guestObject, int index)
    {
        return index == 2 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[2]);
    }

    private bool ShouldUseAuthoredCountessGuestAnimation(GameObject guestObject, int index)
    {
        return index == 3 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[3]);
    }

    private bool ShouldUseAuthoredLaterGuestAnimation(GameObject guestObject, int index)
    {
        return index >= 4 && index <= 7 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[index]);
    }

    private void EnsureGuestAnimatorUsesButlerController(GameObject guestObject)
    {
        Animator sourceAnimator = playerButlerReference != null
            ? playerButlerReference.GetComponentInChildren<Animator>(true)
            : null;
        SpriteRenderer sourceRenderer = playerButlerReference != null
            ? playerButlerReference.GetComponentInChildren<SpriteRenderer>(true)
            : null;

        if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        Animator guestAnimator = guestObject.GetComponentInChildren<Animator>(true);

        if (guestAnimator == null)
        {
            guestAnimator = guestObject.AddComponent<Animator>();
        }

        guestAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;

        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            return;
        }

        SpriteRenderer guestRenderer = FindCharacterSpriteRenderer(guestObject);

        if (guestRenderer != null)
        {
            guestRenderer.sprite = sourceRenderer.sprite;
        }
    }

    private static SpriteRenderer FindCharacterSpriteRenderer(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return null;
        }

        SpriteRenderer rootRenderer = guestObject.GetComponent<SpriteRenderer>();

        if (rootRenderer != null)
        {
            return rootRenderer;
        }

        SpriteRenderer[] renderers = guestObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer != null && !IsCoatVisualTransform(renderer.transform))
            {
                return renderer;
            }
        }

        return null;
    }

    private static bool IsCoatVisualTransform(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (HasCoatVisualName(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCoatVisualName(Transform target)
    {
        return target != null && target.name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string MakeGuestId(string objectName, int index)
    {
        string normalizedName = NormalizeObjectName(objectName);
        return string.IsNullOrWhiteSpace(normalizedName)
            ? $"guest_{index + 1:00}"
            : normalizedName;
    }

    private static string NormalizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
    }

    private Transform ResolveSeatForGuest(int index)
    {
        return GetDrawingRoomGuestPoint(index);
    }

    private Transform GetDrawingRoomGuestPoint(int guestIndex)
    {
        if (drawingRoomGuestPoints == null ||
            guestIndex < 0 ||
            guestIndex >= drawingRoomGuestPoints.Length)
        {
            return null;
        }

        return drawingRoomGuestPoints[guestIndex];
    }

    private Transform CreateRuntimeAnchor(string objectName, Vector3 position, Transform siblingAnchor)
    {
        GameObject anchorObject = GameObject.Find(objectName);

        if (anchorObject == null)
        {
            anchorObject = new GameObject(objectName);
            Transform parent = siblingAnchor != null && siblingAnchor.parent != null ? siblingAnchor.parent : transform;
            anchorObject.transform.SetParent(parent, true);
        }

        anchorObject.transform.position = position;
        return anchorObject.transform;
    }

    private bool IsWorldSpaceGuestObject(GameObject guestObject)
    {
        return guestObject != null && !(guestObject.transform is RectTransform);
    }

    private bool TryPlaceProjectedGuestAtTarget(GuestRuntimeState guestState, Transform target)
    {
        RoomProjectedEntity projection = ResolveGuestProjection(guestState);

        if (projection == null)
        {
            return false;
        }

        projection.UseProfileFromRoomTarget(target);

        if (!projection.HasUsableProfile)
        {
            return false;
        }

        return projection.TrySetRoomLocalFootPointFromTarget(target);
    }

    private bool HasActiveProjection(GuestRuntimeState guestState)
    {
        RoomProjectedEntity projection = ResolveGuestProjection(guestState);
        return projection != null && projection.IsProjectionActive;
    }

    private bool HasActiveProjection(GameObject guestObject)
    {
        RoomProjectedEntity projection = ResolveGuestProjection(guestObject, guestObject != null ? guestObject.GetComponent<ActorRoomState>() : null);
        return projection != null && projection.IsProjectionActive;
    }

    private RoomProjectedEntity ResolveGuestProjection(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return null;
        }

        if (guestState.Projection == null)
        {
            guestState.Projection = ResolveGuestProjection(guestState.GuestObject, guestState.ActorState);
        }

        return guestState.Projection;
    }

    private static RoomProjectedEntity ResolveGuestProjection(GameObject guestObject, ActorRoomState actorState)
    {
        if (actorState != null && actorState.Projection != null)
        {
            return actorState.Projection;
        }

        return guestObject != null ? guestObject.GetComponentInChildren<RoomProjectedEntity>(true) : null;
    }

    private GuestScaleParticipant EnsureGuestScaleParticipant(
        GuestRuntimeState guestState,
        string roomId,
        CharacterPose pose)
    {
        if (guestState == null || guestState.GuestObject == null)
        {
            return null;
        }

        GuestRoomScaleApplier applier = ResolveConfiguredGuestScaleApplier();

        if (applier == null)
        {
            return null;
        }

        GuestScaleParticipant participant = GuestRoomScaleApplier.EnsureParticipantForGuestObject(
            guestState.GuestObject,
            guestState.Config != null ? guestState.Config.GuestId : guestState.GuestObject.name,
            roomId,
            pose,
            true);

        if (participant == null)
        {
            return null;
        }

        participant.SetCurrentRoomId(roomId);
        participant.SetIsButler(false);
        participant.ResolveScaleRoot();
        participant.CaptureBaseScale(false);
        guestState.ScaleParticipant = participant;
        applier.RefreshParticipantNow(participant);
        return participant;
    }

    private void SyncGuestScaleParticipantCurrentRoom(GuestRuntimeState guestState, string roomId)
    {
        if (guestState == null || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        GuestScaleParticipant participant = guestState.ScaleParticipant;

        if (participant == null && guestState.GuestObject != null)
        {
            participant = guestState.GuestObject.GetComponent<GuestScaleParticipant>();
        }

        participant?.SetCurrentRoomId(roomId);
    }

    private GuestRoomScaleApplier ResolveConfiguredGuestScaleApplier()
    {
        if (guestRoomScaleApplier == null)
        {
            LogGuestScaleConfigurationError("Chapter1ArrivalController requires its serialized GuestRoomScaleApplier reference.");
            return null;
        }

        GuestRoomScaleCalibration calibration = guestRoomScaleApplier.Calibration;

        if (calibration == null)
        {
            LogGuestScaleConfigurationError("The serialized GuestRoomScaleApplier requires its GuestRoomScaleCalibration reference.");
            return null;
        }

        ResolveReferences(false);
        PointClickPlayerMovement butler = playerMovement;

        if (butler != null)
        {
            calibration.InitializeMissingRoomsFromButler(butler);
            calibration.SetButlerScaleSource(butler);
        }

        return guestRoomScaleApplier;
    }

    private void LogGuestScaleConfigurationError(string message)
    {
        if (guestScaleConfigurationErrorLogged)
        {
            return;
        }

        guestScaleConfigurationErrorLogged = true;
        Debug.LogError(message, this);
    }

    private void RefreshGuestScalingNow()
    {
        GuestRoomScaleApplier applier = ResolveConfiguredGuestScaleApplier();
        applier?.RefreshAllNow();
    }

    private string ResolveGuestScaleRoomId(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return entryRoomId;
        }

        if (guestState.ActorState != null && !string.IsNullOrWhiteSpace(guestState.ActorState.CurrentRoomId))
        {
            return guestState.ActorState.CurrentRoomId;
        }

        if (guestState.Seated || guestState.MovingToDrawingRoom)
        {
            return drawingRoomId;
        }

        return entryRoomId;
    }

    private CharacterPose ResolveGuestScalePose(GuestRuntimeState guestState)
    {
        if (guestState == null)
        {
            return CharacterPose.Standing;
        }

        if (SameRoom(ResolveGuestScaleRoomId(guestState), drawingRoomId) &&
            !ShouldUseStandingDrawingRoomPose(guestState))
        {
            return CharacterPose.Seated;
        }

        return guestState.Seated ? CharacterPose.Seated : CharacterPose.Standing;
    }

    private float GetMoveSpeedForGuestObject(GameObject guestObject)
    {
        return HasActiveProjection(guestObject)
            ? Mathf.Max(0.01f, guestMoveSpeed)
            : IsWorldSpaceGuestObject(guestObject)
            ? Mathf.Max(0.01f, worldGuestMoveSpeed)
            : Mathf.Max(0.01f, guestMoveSpeed);
    }

    private Vector3 GetEntranceWaitPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)
    {
        Vector3 basePosition = GetEntranceWaitBasePosition();
        Vector2 offset = GetEntranceGroupOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            entranceGuestSpacing,
            GetEntranceWaitBaseYOffset(entranceGuestSpacing));
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetEntranceWaitPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetEntranceWaitBasePosition();
        Vector2 offset = GetEntranceGroupOffset(
            null,
            indexInBatch,
            batchCount,
            entranceGuestSpacing,
            GetEntranceWaitBaseYOffset(entranceGuestSpacing));
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetEntranceWaitBasePosition()
    {
        Transform anchor = entranceHallGuestAnchor;

        if (anchor != null)
        {
            if (TryGetEntranceHallGuestAnchorWorldPosition(null, out Vector3 anchorPosition))
            {
                return anchorPosition;
            }

            return anchor.position;
        }

        Vector3 basePosition = frontDoorArrivalPoint != null
            ? frontDoorArrivalPoint.position
            : transform.position;

        if (!snapGuestsIntoEntranceForFirstVisualPass)
        {
            return basePosition;
        }

        if (playerButlerReference != null)
        {
            return playerButlerReference.transform.position;
        }

        return basePosition;
    }

    private Vector3 GetWorldDoorArrivalPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)
    {
        Vector3 basePosition = GetWorldDoorArrivalBasePosition(guestState);
        Vector2 pairOffset = GetDoorArrivalPairSlotOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            worldEntranceGuestSpacing * 0.55f);
        return basePosition + new Vector3(pairOffset.x, pairOffset.y, 0f);
    }

    private Vector3 GetWorldDoorArrivalPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetWorldDoorArrivalBasePosition(null);
        Vector2 pairOffset = GetDoorArrivalPairSlotOffset(
            null,
            indexInBatch,
            batchCount,
            worldEntranceGuestSpacing * 0.55f);
        return basePosition + new Vector3(pairOffset.x, pairOffset.y, 0f);
    }

    private Vector2 GetDoorArrivalPairSlotOffset(
        GuestRuntimeState guestState,
        int fallbackIndex,
        int fallbackCount,
        float spacing)
    {
        GetEntranceGroupSlot(guestState, fallbackIndex, fallbackCount, out int slotInGroup, out _, out int groupSize);
        float centeredSlot = slotInGroup - (groupSize - 1) * 0.5f;
        return new Vector2(centeredSlot * spacing, 0f);
    }

    private Vector3 GetWorldEntranceWaitPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)
    {
        Vector3 basePosition = GetWorldEntranceCenterPosition(guestState);
        Vector2 offset = GetWorldEntranceGroupOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            worldEntranceGuestSpacing,
            GetWorldEntranceWaitBaseYOffset());
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetWorldEntranceWaitPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetWorldEntranceCenterPosition();
        Vector2 offset = GetWorldEntranceGroupOffset(
            null,
            indexInBatch,
            batchCount,
            worldEntranceGuestSpacing,
            GetWorldEntranceWaitBaseYOffset());
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetWorldDoorArrivalBasePosition(GuestRuntimeState guestState)
    {
        Transform doorAnchor = GetWorldDoorArrivalTarget(guestState);

        if (TryGetWorldPositionForGuestTarget(GetWorldPlacementDepthReference(guestState), doorAnchor, out Vector3 doorPosition))
        {
            return doorPosition;
        }

        if (TryGetWorldFrontDoorAnswerSpot(out Vector3 answerSpotPosition))
        {
            return answerSpotPosition;
        }

        return GetWorldEntranceCenterPosition(guestState);
    }

    private Transform GetWorldDoorArrivalTarget(GuestRuntimeState guestState)
    {
        if (guestEntranceSpawnPlacemark != null)
        {
            return guestEntranceSpawnPlacemark;
        }

        return guestState != null && guestState.Config != null
            ? guestState.Config.GetFrontDoorArrivalPoint(frontDoorArrivalPoint)
            : frontDoorArrivalPoint;
    }

    private bool TryGetWorldFrontDoorAnswerSpot(out Vector3 answerSpotPosition)
    {
        answerSpotPosition = Vector3.zero;

        if (!hasFrontDoorAnswerSpot)
        {
            return false;
        }

        if (playerMovement == null)
        {
            ResolveReferences();
        }

        if (playerMovement == null ||
            !playerMovement.TryGetWorldPointFromLogicalPosition(frontDoorAnswerSpot, out Vector2 worldPoint))
        {
            return false;
        }

        Transform depthReference = GetWorldPlacementDepthReference(null);
        answerSpotPosition = new Vector3(
            worldPoint.x,
            worldPoint.y,
            depthReference != null ? depthReference.position.z : transform.position.z);
        return true;
    }

    private Vector3 GetWorldDrawingRoomEntryPosition(GuestRuntimeState guestState, int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetWorldDrawingRoomEntryBasePosition(guestState);
        Vector2 offset = GetWorldGuestGridOffset(indexInBatch, batchCount, worldDrawingRoomSeatSpacing);
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetWorldDrawingRoomEntryBasePosition(GuestRuntimeState guestState)
    {
        Transform depthReference = GetWorldPlacementDepthReference(guestState);

        if (TryGetGrandEntranceDrawingRoomGuestTargetPosition(depthReference, out Vector3 editableTargetPosition))
        {
            return editableTargetPosition;
        }

        Transform entryAnchor = guestState != null && guestState.Config != null
            ? guestState.Config.GetDrawingRoomEntryPoint(drawingRoomEntryPoint)
            : drawingRoomEntryPoint;
        return GetWorldVisibleAnchorPosition(guestState, entryAnchor, GetWorldEntranceCenterPosition(guestState));
    }

    private Vector3 GetWorldVisibleAnchorPosition(GuestRuntimeState guestState, Transform anchor, Vector3 fallbackPosition)
    {
        if (TryGetWorldPositionForGuestTarget(GetWorldPlacementDepthReference(guestState), anchor, out Vector3 worldPosition))
        {
            return worldPosition;
        }

        return fallbackPosition;
    }

    private Transform GetWorldPlacementDepthReference(GuestRuntimeState guestState)
    {
        if (guestState != null && guestState.GuestObject != null)
        {
            return guestState.GuestObject.transform;
        }

        if (playerButlerReference != null)
        {
            return playerButlerReference.transform;
        }

        return transform;
    }

    private bool TryGetGrandEntranceDrawingRoomGuestTargetPosition(Transform depthReference, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        return drawingRoomDoorTarget != null &&
            TryGetWorldPositionForGuestTarget(depthReference, drawingRoomDoorTarget, out worldPosition);
    }

    private Vector2 GetWorldGuestGridOffset(int index, int count, float spacing)
    {
        int columns = Mathf.Max(1, Mathf.Min(4, count));
        int column = index % columns;
        int row = index / columns;
        float centeredColumn = column - (columns - 1) * 0.5f;
        return new Vector2(centeredColumn * spacing, -row * spacing * 0.8f);
    }

    private Vector2 GetWorldEntranceGroupOffset(
        GuestRuntimeState guestState,
        int fallbackIndex,
        int fallbackCount,
        float spacing,
        float baseY)
    {
        GetEntranceGroupSlot(guestState, fallbackIndex, fallbackCount, out int slotInGroup, out int groupIndex, out int groupSize);
        float centeredSlot = slotInGroup - (groupSize - 1) * 0.5f;
        return new Vector2(
            centeredSlot * spacing * EntranceWaitSlotSpacingMultiplier +
                groupIndex * spacing * EntranceWaitGroupSideStepMultiplier,
            baseY - groupIndex * spacing * EntranceWaitDepthStepMultiplier);
    }

    private Vector3 GetWorldEntranceCenterPosition()
    {
        return GetWorldEntranceCenterPosition(null);
    }

    private Vector3 GetWorldEntranceCenterPosition(GuestRuntimeState guestState)
    {
        if (TryGetEntranceHallGuestAnchorWorldPosition(guestState, out Vector3 anchorPosition))
        {
            return anchorPosition;
        }

        if (hasWorldDoorCenterPosition)
        {
            return worldDoorCenterPosition;
        }

        if (TryGetAverageAuthoredChapterGuestPosition(out Vector3 averagePosition))
        {
            worldDoorCenterPosition = averagePosition;
            hasWorldDoorCenterPosition = true;
            return averagePosition;
        }

        if (playerButlerReference != null)
        {
            worldDoorCenterPosition = playerButlerReference.transform.position + new Vector3(0f, 1.15f, 0f);
            hasWorldDoorCenterPosition = true;
            return worldDoorCenterPosition;
        }

        worldDoorCenterPosition = transform.position;
        hasWorldDoorCenterPosition = true;
        return worldDoorCenterPosition;
    }

    private bool TryGetEntranceHallGuestAnchorWorldPosition(GuestRuntimeState guestState, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        Transform anchor = entranceHallGuestAnchor;

        if (anchor == null)
        {
            return false;
        }

        if (TryGetWorldPositionForGuestTarget(GetWorldPlacementDepthReference(guestState), anchor, out worldPosition))
        {
            return true;
        }

        if (anchor.GetComponentInParent<RoomContentGroup>(true) == null && !(anchor is RectTransform))
        {
            worldPosition = anchor.position;
            return true;
        }

        return false;
    }

    private bool TryGetAverageAuthoredChapterGuestPosition(out Vector3 averagePosition)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < ChapterGuestNameAliases.Length; i++)
        {
            GameObject guestObject = FindChapterGuestObjectByIndex(i);

            if (guestObject == null || runtimeGeneratedGuestObjects.Contains(guestObject))
            {
                continue;
            }

            sum += guestObject.transform.position;
            count++;
        }

        if (count == 0)
        {
            averagePosition = Vector3.zero;
            return false;
        }

        averagePosition = sum / count;
        return true;
    }

    private Vector2 GetEntranceWaitAnchoredPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)
    {
        Vector2 basePosition = GetEntranceWaitBaseAnchoredPosition();
        return basePosition + GetEntranceGroupOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            entranceGuestSpacing,
            GetEntranceWaitBaseYOffset(entranceGuestSpacing));
    }

    private Vector2 GetEntranceWaitAnchoredPosition(int indexInBatch, int batchCount)
    {
        Vector2 basePosition = GetEntranceWaitBaseAnchoredPosition();
        float centeredIndex = indexInBatch - (batchCount - 1) * 0.5f;
        Vector2 offset = new Vector2(
            centeredIndex * entranceGuestSpacing,
            GetEntranceWaitBaseYOffset(entranceGuestSpacing));
        return basePosition + offset;
    }

    private Vector2 GetEntranceWaitBaseAnchoredPosition()
    {
        Transform anchor = entranceHallGuestAnchor;

        if (anchor != null)
        {
            return new Vector2(anchor.localPosition.x, anchor.localPosition.y);
        }

        return frontDoorArrivalPoint != null
            ? new Vector2(frontDoorArrivalPoint.localPosition.x, frontDoorArrivalPoint.localPosition.y)
            : Vector2.zero;
    }

    private float GetEntranceWaitBaseYOffset(float spacing)
    {
        if (entranceHallGuestAnchor != null)
        {
            return 0f;
        }

        return snapGuestsIntoEntranceForFirstVisualPass ? spacing * 0.65f : -spacing * 0.55f;
    }

    private float GetWorldEntranceWaitBaseYOffset()
    {
        return entranceHallGuestAnchor != null ? 0f : -worldEntranceGuestSpacing * 1.5f;
    }

    private Vector2 GetEntranceGroupOffset(
        GuestRuntimeState guestState,
        int fallbackIndex,
        int fallbackCount,
        float spacing)
    {
        return GetEntranceGroupOffset(
            guestState,
            fallbackIndex,
            fallbackCount,
            spacing,
            GetEntranceWaitBaseYOffset(spacing));
    }

    private Vector2 GetEntranceGroupOffset(
        GuestRuntimeState guestState,
        int fallbackIndex,
        int fallbackCount,
        float spacing,
        float baseY)
    {
        GetEntranceGroupSlot(guestState, fallbackIndex, fallbackCount, out int slotInGroup, out int groupIndex, out int groupSize);
        float centeredSlot = slotInGroup - (groupSize - 1) * 0.5f;
        return new Vector2(
            centeredSlot * spacing * EntranceWaitSlotSpacingMultiplier +
                groupIndex * spacing * EntranceWaitGroupSideStepMultiplier,
            baseY - groupIndex * spacing * EntranceWaitDepthStepMultiplier);
    }

    private void GetEntranceGroupSlot(
        GuestRuntimeState guestState,
        int fallbackIndex,
        int fallbackCount,
        out int slotInGroup,
        out int groupIndex,
        out int groupSize)
    {
        groupSize = Mathf.Max(1, guestsPerArrivalGroup);
        int safeFallbackIndex = Mathf.Clamp(fallbackIndex, 0, Mathf.Max(0, fallbackCount - 1));
        int guestIndex = guestState != null ? Mathf.Max(0, guestState.GuestIndex) : safeFallbackIndex;
        slotInGroup = guestIndex % groupSize;
        groupIndex = guestState != null ? Mathf.Max(0, guestState.GroupIndex) : guestIndex / groupSize;
    }

    private bool TryGetAnchoredPositionForGuestTarget(GuestRuntimeState guestState, Transform target, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;

        if (target == null)
        {
            return false;
        }

        RectTransform guestRectTransform = guestState != null && guestState.GuestObject != null
            ? guestState.GuestObject.transform as RectTransform
            : null;
        RectTransform guestParentRect = guestRectTransform != null ? guestRectTransform.parent as RectTransform : null;

        if (guestParentRect != null &&
            TryGetTargetScreenPosition(target, out Vector2 screenPosition))
        {
            Camera guestCanvasCamera = GetCanvasCamera(guestParentRect.GetComponentInParent<Canvas>(true));

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                guestParentRect,
                screenPosition,
                guestCanvasCamera,
                out anchoredPosition))
            {
                return true;
            }
        }

        if (target is RectTransform targetRectTransform)
        {
            anchoredPosition = targetRectTransform.anchoredPosition;
            return true;
        }

        anchoredPosition = new Vector2(target.localPosition.x, target.localPosition.y);
        return true;
    }

    private bool TryGetWorldPositionForGuestTarget(Transform guestTransform, Transform target, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (target == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;
        Transform depthReference = guestTransform != null ? guestTransform : GetWorldPlacementDepthReference(null);

        if (mainCamera == null)
        {
            return false;
        }

        float depth = GetWorldPlacementDepth(depthReference, mainCamera);

        if (TryGetActiveRoomStageWorldPositionForGuestTarget(target, depth, out worldPosition))
        {
            worldPosition.z = depthReference.position.z;
            return true;
        }

        if (!TryGetTargetScreenPosition(target, out Vector2 screenPosition))
        {
            return false;
        }

        worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = depthReference.position.z;
        return true;
    }

    private bool TryGetActiveRoomStageWorldPositionForGuestTarget(Transform target, float depth, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (target == null)
        {
            return false;
        }

        RoomContentGroup roomContentGroup = target.GetComponentInParent<RoomContentGroup>(true);
        RectTransform roomStage = roomContentGroup != null ? roomContentGroup.transform as RectTransform : null;

        if (cameraManager == null ||
            navigationManager == null ||
            roomStage == null ||
            !SameRoom(roomContentGroup.RoomName, navigationManager.CurrentRoom))
        {
            return false;
        }

        Vector3 localPoint = roomStage.InverseTransformPoint(target.position);
        return cameraManager.TryGetActiveRoomStageWorldPoint(
            new Vector2(localPoint.x, localPoint.y),
            depth,
            out worldPosition);
    }

    private float GetWorldPlacementDepth(Transform depthReference, Camera mainCamera)
    {
        if (mainCamera == null)
        {
            return 10f;
        }

        Transform safeDepthReference = depthReference != null ? depthReference : transform;
        float depth = safeDepthReference.position.z - mainCamera.transform.position.z;

        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(depth);
        }

        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
        }

        return depth <= 0.01f ? 10f : depth;
    }

    private bool TryGetTargetScreenPosition(Transform target, out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (target == null)
        {
            return false;
        }

        Canvas targetCanvas = target.GetComponentInParent<Canvas>(true);

        if (targetCanvas != null)
        {
            screenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(targetCanvas), target.position);
            return true;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return false;
        }

        screenPosition = mainCamera.WorldToScreenPoint(target.position);
        return true;
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private Vector3 GetCoatPosition(GuestRuntimeState guest)
    {
        Vector3 basePosition = guest != null && guest.GuestObject != null ? guest.GuestObject.transform.position : transform.position;

        if (guest != null && IsWorldSpaceGuestObject(guest.GuestObject))
        {
            return basePosition + WorldCoatOffset;
        }

        return basePosition + new Vector3(coatOffsetX, coatOffsetY, 0f);
    }

    private GuestGroupRuntimeState FindNextUnqueuedGuestGroup()
    {
        for (int i = 0; i < guestGroups.Count; i++)
        {
            GuestGroupRuntimeState group = guestGroups[i];

            if (group != null && !group.EmptyRing && !group.QueuedOutside && !group.EnteredEntranceHall)
            {
                return group;
            }
        }

        return null;
    }

    private void TryFastForwardNextDoorbellIfEntranceClear()
    {
        if (!sequenceActive ||
            chapterCompletionRequested ||
            butlerCarryingCoat ||
            pendingGuestGroups.Count > 0 ||
            activeEntranceGroups.Count > 0 ||
            emptyDoorbellWaitingForAnswer)
        {
            return;
        }

        GuestGroupRuntimeState nextGroup = FindNextDoorbellGroupForFastForward();

        if (nextGroup == null)
        {
            return;
        }

        Debug.Log(
            $"[Chapter1] Entrance is clear; fast-forwarding doorbell for {(nextGroup.EmptyRing ? "empty final ring" : $"group {nextGroup.GroupIndex + 1}")}.",
            this);
        HandleScheduledDoorbell(nextGroup);
    }

    private GuestGroupRuntimeState FindNextDoorbellGroupForFastForward()
    {
        for (int i = 0; i < guestGroups.Count; i++)
        {
            GuestGroupRuntimeState group = guestGroups[i];

            if (group == null)
            {
                continue;
            }

            if (group.EmptyRing)
            {
                if (!finalEmptyDoorbellOccurred)
                {
                    return group;
                }

                continue;
            }

            if (!group.QueuedOutside && !group.EnteredEntranceHall)
            {
                return group;
            }
        }

        return null;
    }

    private GuestRuntimeState FindGuestByCoat(string coatId)
    {
        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState guest = guestStates[i];

            if (guest != null &&
                guest.Config != null &&
                string.Equals(guest.Config.CoatId, coatId, StringComparison.OrdinalIgnoreCase))
            {
                return guest;
            }
        }

        return null;
    }

    private float GetOldestPendingQueuedGameMinute()
    {
        if (pendingGuestGroups.Count == 0)
        {
            return chapterClock != null ? chapterClock.ElapsedGameMinutes : 0f;
        }

        float oldest = float.PositiveInfinity;

        for (int i = 0; i < pendingGuestGroups.Count; i++)
        {
            oldest = Mathf.Min(oldest, pendingGuestGroups[i].QueuedAtGameMinute);
        }

        return float.IsPositiveInfinity(oldest) ? 0f : oldest;
    }

    private bool WasGuestWaitingLongEnoughToBeAnnoyed(GuestRuntimeState guest)
    {
        if (guest == null || chapterClock == null)
        {
            return false;
        }

        return chapterClock.ElapsedGameMinutes - guest.QueuedAtGameMinute >= 1f;
    }

    private int CountGuestsWaitingOutside()
    {
        int count = 0;

        for (int i = 0; i < guestStates.Count; i++)
        {
            if (guestStates[i].WaitingOutside)
            {
                count++;
            }
        }

        return count;
    }

    private int CountGuestsInEntranceHall()
    {
        int count = 0;

        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState guest = guestStates[i];

            if (guest.EnteredEntranceHall && !guest.Seated)
            {
                count++;
            }
        }

        return count;
    }

    private int CountGuestsInDrawingRoom()
    {
        int count = 0;

        for (int i = 0; i < guestStates.Count; i++)
        {
            if (guestStates[i].Seated)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountGuestsInGroups(List<GuestGroupRuntimeState> groups)
    {
        int count = 0;

        if (groups == null)
        {
            return count;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
            {
                count += groups[i].Guests.Count;
            }
        }

        return count;
    }

    private string GetDefaultGreeting(int index)
    {
        switch (Mathf.Clamp(index, 0, 7))
        {
            case 0: return "Good evening. I trust the house remembers its manners better than the weather does.";
            case 1: return "Thank you. The drive was longer in the dark than I care to admit.";
            case 2: return "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?";
            case 3: return "Good evening, Butler. The road up here has the cheerful shape of a warning.";
            case 4: return "Good evening. I hope the evening has not started without us.";
            case 5: return "Thank you. I nearly mistook the bell pull for a funeral cord.";
            case 6: return "Lovely to see you. The chateau looks almost awake tonight.";
            default: return "Good evening, Butler. I see the house has chosen its most severe face.";
        }
    }

    private string GetDefaultAmbientLine(int index)
    {
        switch (Mathf.Clamp(index, 0, 7))
        {
            case 0: return "This house is colder than I expected.";
            case 1: return "The host is late, isn't he?";
            case 2: return "Did you hear something upstairs?";
            case 3: return "The drawing room should be warmer.";
            case 4: return "This house is colder than I expected.";
            case 5: return "The host is late, isn't he?";
            case 6: return "Did you hear something upstairs?";
            default: return "The drawing room should be warmer.";
        }
    }

    private string GetAnnoyedLine(int index)
    {
        switch (Mathf.Clamp(index, 0, 7))
        {
            case 0: return "We were beginning to wonder if anyone was home.";
            case 1: return "It is rather cold out there.";
            case 2: return "We have been waiting at the door for some time.";
            case 3: return "At last. I had begun composing my obituary in the frost.";
            case 4: return "We were beginning to wonder if anyone was home.";
            case 5: return "It is rather cold out there, and colder still when one is expected.";
            case 6: return "We have been waiting at the door for some time. The house was listening with us.";
            default: return "At last. A closed door should not feel so pleased with itself.";
        }
    }

    private Sprite GetRuntimeGuestSprite()
    {
        if (runtimeGuestSprite == null)
        {
            runtimeGuestSprite = CreateSolidSprite("RuntimeGuestSprite", new Color(0.18f, 0.24f, 0.36f, 1f), 48, 96, new Vector2(0.5f, 0.08f), 2f);
        }

        return runtimeGuestSprite;
    }

    private SpriteRenderer CreateRuntimeVisual(Transform parent, string objectName, Sprite sprite, float visualScale)
    {
        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(parent, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one * visualScale;

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        return renderer;
    }

    private static Sprite CreateSolidSprite(string spriteName, Color color, int width, int height, Vector2 pivot, float pixelsPerUnit)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x / (float)(width - 1)) * 2f - 1f;
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float mask = nx * nx * 0.72f + ny * ny;
                texture.SetPixel(x, y, mask <= 1f ? color : clear);
            }
        }

        texture.Apply();
        texture.name = spriteName;
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, Mathf.Max(1f, pixelsPerUnit));
    }

    private static void HideRuntimePlaceholderRenderers(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].enabled = false;
            }
        }
    }

    private void ResolveReferences()
    {
        ResolveReferences(true);
    }

    private void ResolveReferences(bool createFallbacks)
    {
        playerButlerReference = playerMovement != null ? playerMovement.gameObject : null;

        ResolveStoryHelpers(createFallbacks);
    }

    private void ResolveStoryHelpers(bool createFallbacks)
    {
        if (timeSettingsUI == null)
        {
            timeSettingsUI = FindAnyObjectByType<ChapterTimeSettingsUI>(FindObjectsInactive.Include);
        }

        if (timeSettingsUI == null && createFallbacks)
        {
            timeSettingsUI = gameObject.AddComponent<ChapterTimeSettingsUI>();
        }
    }

    private string GetRoomForTransform(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        RoomContentGroup room = target.GetComponentInParent<RoomContentGroup>(true);
        return room != null ? room.RoomName : string.Empty;
    }

    private void MoveGuestObjectToRoomContent(GuestRuntimeState guest, string roomId)
    {
        if (guest == null || guest.GuestObject == null)
        {
            return;
        }

        SyncGuestScaleParticipantCurrentRoom(guest, roomId);

        if (guest.ActorState != null || IsChapterSceneGuest(guest.GuestObject))
        {
            return;
        }

        RoomContentGroup roomContent = FindRoomContentGroup(roomId);

        if (roomContent == null)
        {
            return;
        }

        Transform guestTransform = guest.GuestObject.transform;

        if (guestTransform == null || guestTransform.IsChildOf(roomContent.transform))
        {
            return;
        }

        guestTransform.SetParent(roomContent.transform, true);
    }

    private RoomContentGroup FindRoomContentGroup(string roomId)
    {
        if (SameRoom(roomId, entryRoomId))
        {
            return entryRoomContent;
        }

        if (SameRoom(roomId, drawingRoomId))
        {
            return drawingRoomContent;
        }

        return null;
    }

    private void SubscribeToRoomChanges()
    {
        if (subscribedToRoomChanges)
        {
            return;
        }

        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.AddListener(HandleRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnsubscribeFromRoomChanges()
    {
        if (!subscribedToRoomChanges || navigationManager == null)
        {
            subscribedToRoomChanges = false;
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleRoomChanged(string roomName)
    {
        if (!sequenceActive || chapterCompletionRequested)
        {
            return;
        }

        CompleteOffscreenDrawingRoomMoves(roomName);

        if (chapterCompletionRequested)
        {
            return;
        }

        RefreshInteractionState();
        RefreshAllGuestRoomVisibility();
        QueueDeferredGuestRoomVisibilityRefresh();

        if (SameRoom(roomName, drawingRoomId))
        {
            CheckChapterCompletionGate();
        }
    }

    private void QueueDeferredGuestRoomVisibilityRefresh()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (guestRoomVisibilityRefreshRoutine != null)
        {
            StopCoroutine(guestRoomVisibilityRefreshRoutine);
        }

        guestRoomVisibilityRefreshRoutine = StartCoroutine(RefreshGuestRoomVisibilityAfterRoomListeners());
    }

    private IEnumerator RefreshGuestRoomVisibilityAfterRoomListeners()
    {
        yield return null;
        guestRoomVisibilityRefreshRoutine = null;
        RefreshAllGuestRoomVisibility();
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] normalized = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            normalized[count] = char.ToLowerInvariant(character);
            count++;
        }

        return new string(normalized, 0, count);
    }
}
