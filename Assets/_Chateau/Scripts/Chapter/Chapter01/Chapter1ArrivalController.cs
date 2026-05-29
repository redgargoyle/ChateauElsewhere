using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter1ArrivalController : MonoBehaviour
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
        public bool Seated;
        public bool Handled;
        public float QueuedAtGameMinute;
        public Transform Seat;
        public GameObject GuestObject;
        public Chapter1CoatPickup CoatPickup;
        public NPCWaypointMover Mover;
        public ActorRoomState ActorState;
    }

    private sealed class GuestGroupRuntimeState
    {
        public int GroupIndex;
        public int ArrivalHour;
        public int ArrivalMinute;
        public bool EmptyRing;
        public bool QueuedOutside;
        public bool EnteredEntranceHall;
        public bool ExitingToDrawingRoom;
        public bool Complete;
        public float QueuedAtGameMinute;
        public readonly List<GuestRuntimeState> Guests = new List<GuestRuntimeState>();
    }

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private ChapterEventScheduler eventScheduler;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private GameObject playerButlerReference;
    [SerializeField] private CoatCloset coatCloset;
    [SerializeField] private DoorbellSystem doorbellSystem;
    [SerializeField] private GrandfatherClockInteraction grandfatherClock;
    [SerializeField] private ChapterTimeSettingsUI timeSettingsUI;
    [SerializeField] private Chapter1InteractionHUD interactionHUD;

    [Header("Rooms")]
    [SerializeField] private string entryRoomId = "Grand Entrance Hall";
    [SerializeField] private string drawingRoomId = "Drawing Room";

    [Header("Required Anchors")]
    [SerializeField] private Transform frontDoorArrivalPoint;
    [SerializeField] private Transform butlerDoorSpot;
    [SerializeField] private Transform closetPoint;
    [SerializeField] private Transform drawingRoomEntryPoint;
    [SerializeField] private Transform drawingRoomSeat01;
    [SerializeField] private Transform drawingRoomSeat02;
    [SerializeField] private Transform drawingRoomSeat03;

    [Header("Clock Timeline")]
    [SerializeField, Range(0, 23)] private int firstArrivalHour = 18;
    [SerializeField, Range(0, 59)] private int firstArrivalMinute = 0;
    [SerializeField, Min(1)] private int guestGroupCount = 4;
    [SerializeField, Min(1)] private int guestsPerArrivalGroup = 2;
    [SerializeField, Range(0, 23)] private int emptyDoorbellHour = 18;
    [SerializeField, Range(0, 59)] private int emptyDoorbellMinute = 4;

    [Header("Guests")]
    [SerializeField] private List<GuestArrivalConfig> guests = new List<GuestArrivalConfig>();
    [SerializeField] private bool useExistingSceneGuestsFirst = true;
    [SerializeField] private float entranceGuestSpacing = 95f;
    [SerializeField] private float drawingRoomSeatSpacing = 86f;
    [SerializeField] private float guestMoveSpeed = 180f;
    [SerializeField] private float worldEntranceGuestSpacing = 0.65f;
    [SerializeField] private float worldDrawingRoomSeatSpacing = 0.75f;
    [SerializeField] private float worldGuestMoveSpeed = 2.2f;

    [Header("Interactions")]
    [SerializeField] private bool createRuntimeHud = true;
    [SerializeField] private bool createRuntimeClickTargets = true;
    [SerializeField] private bool snapGuestsIntoEntranceForFirstVisualPass = true;
    [SerializeField] private bool autoStoreCoatIfClosetMissing = false;
    [SerializeField] private float coatOffsetX = 34f;
    [SerializeField] private float coatOffsetY = 42f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private readonly List<GuestRuntimeState> guestStates = new List<GuestRuntimeState>();
    private readonly List<GuestGroupRuntimeState> guestGroups = new List<GuestGroupRuntimeState>();
    private readonly List<GuestGroupRuntimeState> pendingGuestGroups = new List<GuestGroupRuntimeState>();
    private readonly List<GuestGroupRuntimeState> activeEntranceGroups = new List<GuestGroupRuntimeState>();
    private readonly List<Transform> runtimeSeatAnchors = new List<Transform>();
    private readonly HashSet<GameObject> runtimeGeneratedGuestObjects = new HashSet<GameObject>();
    private int currentGuestIndex = -1;
    private bool sequenceActive;
    private bool finalEmptyDoorbellOccurred;
    private bool emptyDoorbellWaitingForAnswer;
    private bool butlerCarryingCoat;
    private string carriedCoatId = string.Empty;
    private Chapter1SceneAction frontDoorSceneAction;
    private GuestRuntimeState carriedCoatGuest;
    private GuestRuntimeState pendingCoatPickupGuest;
    private Chapter1CoatPickup pendingCoatPickup;
    private GameObject carriedCoatVisual;
    private Sprite runtimeCoatSprite;
    private Sprite runtimeWardrobeSprite;
    private Sprite runtimeGuestSprite;
    private bool subscribedToRoomChanges;
    private bool hasWorldDoorCenterPosition;
    private Vector3 worldDoorCenterPosition;
    private bool hasEntranceDrawingRoomExitPosition;
    private Vector3 entranceDrawingRoomExitPosition;
    private bool pendingClosetStorage;

    private const string DoorAnswerTriggerName = "Door_answer_trigger";
    private const float CoatPickupReadyScreenDistance = 90f;
    private const float ClosetStorageReadyScreenDistance = 145f;
    private static readonly Vector3 WorldCoatOffset = new Vector3(0.25f, 0.45f, 0f);
    private static readonly Vector3 ButlerCarriedCoatOffset = new Vector3(0.43f, 1.08f, 0f);
    private static readonly Vector2 WorldCoatColliderSize = new Vector2(0.35f, 0.25f);
    private static readonly Vector2 WardrobeColliderSize = new Vector2(0.9f, 1.6f);
    private static readonly string[][] ChapterGuestNameAliases =
    {
        new[] { "Guest1", "Guest 1" },
        new[] { "Guest2", "Guest 2" },
        new[] { "Guest3", "Guest 3" },
        new[] { "Guest4", "Guest 4" },
        new[] { "Guest5", "Guest 5" },
        new[] { "Guest6", "Guest 6" },
        new[] { "Guest7", "Guest 7" },
        new[] { "Guest8", "Guest 8" }
    };
    private static readonly string[] DoorAnswerTriggerNameAliases = { DoorAnswerTriggerName, "Door_Answer_Trigger", "DoorAnswerTrigger", "Door Answer Trigger" };

    public int CurrentGuestIndex => currentGuestIndex;
    public bool ButlerCarryingCoat => butlerCarryingCoat;
    public string CarriedCoatId => carriedCoatId;

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
        SubscribeToRoomChanges();
    }

    private void OnDisable()
    {
        CancelPendingCoatPickup();
        CancelPendingClosetStorage();
        UnsubscribeFromRoomChanges();
    }

    public void BeginChapter1(ChapterManager manager)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences();
        ValidateRequiredReferences();
        ResetChapterRuntime();
        EnsureRuntimeInteractionSystems();
        SubscribeToRoomChanges();

        sequenceActive = true;
        ScheduleArrivalTimeline();
        RefreshInteractionState();
        Debug.Log("Chapter 1 entrance hall sequence armed at 5:59 PM.", this);
    }

    public void PrepareGuestsForChapterStart()
    {
        ResolveReferences(true);
        EnsureRuntimeInteractionSystems();
        ResetGuestStates(false);
        SetChapterSceneGuestsActive(false);
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

        if (!sequenceActive)
        {
            Debug.Log("Front door clicked, but Chapter 1 arrival sequence is not active.", this);
            return;
        }

        if (pendingGuestGroups.Count == 0)
        {
            if (emptyDoorbellWaitingForAnswer)
            {
                emptyDoorbellWaitingForAnswer = false;
                doorbellSystem?.StopRinging();
                Debug.Log("The butler answers the door. No one is there.", this);
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

    public void HandleCoatClicked(Chapter1CoatPickup coatPickup)
    {
        if (coatPickup == null)
        {
            return;
        }

        if (butlerCarryingCoat)
        {
            Debug.Log($"[Chapter1] Butler already holding coat {carriedCoatId}.", this);
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

        guestState.CoatTaken = true;
        butlerCarryingCoat = true;
        carriedCoatId = guestState.Config.CoatId;
        carriedCoatGuest = guestState;
        SetGuestState(guestState, GuestArrivalState.CoatTaken);

        if (guestState.CoatPickup != null)
        {
            TransferCoatVisualToButler(guestState);
            DisableCoatPickupInteraction(guestState.CoatPickup);
        }

        Debug.Log($"Coat taken from guest: {carriedCoatId}", this);
        RefreshInteractionState();
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
        coatObject.transform.localPosition = ButlerCarriedCoatOffset;
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

        Vector2 coatScreenPosition = mainCamera.WorldToScreenPoint(coatPickup.transform.position);

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

        Vector2 coatScreenPosition = mainCamera.WorldToScreenPoint(coatPickup.transform.position);

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

    public void HandleClosetClicked()
    {
        ResolveReferences();

        if (!butlerCarryingCoat)
        {
            Debug.Log("Closet clicked, but the butler is not carrying a coat.", this);
            return;
        }

        if (!IsButlerCloseToCloset())
        {
            WalkButlerToCloset();
            return;
        }

        StoreCarriedCoatInCloset();
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

        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
        carriedCoatGuest = null;
        RefreshInteractionState();
        CheckActiveGroupsReadyForDrawingRoom();
    }

    private void WalkButlerToCloset()
    {
        ResolveReferences();
        CancelPendingClosetStorage();

        Camera mainCamera = Camera.main;
        Transform target = GetClosetInteractionTransform();

        if (playerMovement == null || mainCamera == null || target == null)
        {
            Debug.LogWarning("Wardrobe clicked, but the butler cannot walk to it because a required reference is missing.", this);
            return;
        }

        Vector2 closetScreenPosition = mainCamera.WorldToScreenPoint(target.position);

        if (!playerMovement.TryEvaluateMovementAtScreenPoint(closetScreenPosition, true, out PointClickPlayerMovement.MovementTargetQuery movementQuery) ||
            !movementQuery.HasReachableDestination)
        {
            Debug.LogWarning("Wardrobe clicked, but the butler could not find a reachable wardrobe spot.", this);
            return;
        }

        if (!playerMovement.TrySetDestination(movementQuery.Destination))
        {
            Debug.LogWarning("Wardrobe clicked, but the butler could not walk to the selected wardrobe spot.", this);
            return;
        }

        pendingClosetStorage = true;
        Debug.Log("[Chapter1] Butler walking to wardrobe.", this);

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
        if (!pendingClosetStorage)
        {
            return;
        }

        CancelPendingClosetStorage();

        if (!butlerCarryingCoat || !IsButlerCloseToCloset())
        {
            return;
        }

        Debug.Log("[Chapter1] Butler reached wardrobe.", this);
        StoreCarriedCoatInCloset();
    }

    private void CancelPendingClosetStorage()
    {
        if (playerMovement != null)
        {
            playerMovement.MovementStopped -= HandleClosetStorageMovementStopped;
        }

        pendingClosetStorage = false;
    }

    private bool IsButlerCloseToCloset()
    {
        ResolveReferences();

        if (playerMovement == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;
        Transform target = GetClosetInteractionTransform();

        if (mainCamera == null || target == null)
        {
            return false;
        }

        Vector2 closetScreenPosition = mainCamera.WorldToScreenPoint(target.position);

        if (!playerMovement.TryGetScreenPointFromLogicalPosition(playerMovement.LogicalPosition, out Vector2 butlerScreenPosition))
        {
            if (playerButlerReference == null)
            {
                return false;
            }

            butlerScreenPosition = mainCamera.WorldToScreenPoint(playerButlerReference.transform.position);
        }

        return Vector2.Distance(butlerScreenPosition, closetScreenPosition) <= ClosetStorageReadyScreenDistance;
    }

    private Transform GetClosetInteractionTransform()
    {
        if (coatCloset != null && IsRuntimeEntranceCloset(coatCloset.gameObject))
        {
            return coatCloset.transform;
        }

        if (closetPoint != null && SameRoom(GetRoomForTransform(closetPoint), entryRoomId))
        {
            return closetPoint;
        }

        return coatCloset != null ? coatCloset.transform : null;
    }

    public void TryCompleteChapterFromDrawingRoomExit()
    {
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

        if (playerMovement == null && playerButlerReference == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: player/butler reference.", this);
        }

        if (coatCloset == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: closet reference. A runtime placeholder will be created for testing.", this);
        }

        if (frontDoorArrivalPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: frontDoorArrivalPoint.", this);
        }

        if (butlerDoorSpot == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: butlerDoorSpot.", this);
        }

        if (drawingRoomEntryPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: drawingRoomEntryPoint.", this);
        }

        if (drawingRoomSeat01 == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: drawingRoomSeat01.", this);
        }

        if (drawingRoomSeat02 == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: drawingRoomSeat02.", this);
        }

        if (drawingRoomSeat03 == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: drawingRoomSeat03.", this);
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
        hasEntranceDrawingRoomExitPosition = false;
        entranceDrawingRoomExitPosition = Vector3.zero;
        pendingGuestGroups.Clear();
        activeEntranceGroups.Clear();
        guestGroups.Clear();

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

            if (runtimeState.CoatPickup != null)
            {
                runtimeState.CoatPickup.gameObject.SetActive(false);
            }

            guestStates.Add(runtimeState);
        }
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
        int batchGuestIndex = 0;

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
                yield return AdmitGuestToEntranceHall(guest, batchGuestIndex, totalGuestBatchCount);
                batchGuestIndex++;
            }
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
        bool useAuthoredEntrancePosition = !useWorldSafePlacement &&
            snapGuestsIntoEntranceForFirstVisualPass &&
            ShouldPreserveAuthoredEntrancePosition(guest.GuestObject);

        if (useWorldSafePlacement)
        {
            PlaceGuestAtPosition(guest, GetWorldDoorArrivalPosition(indexInDoorBatch, batchCount));
        }
        else if (useAuthoredEntrancePosition)
        {
            ActivateAuthoredChapterGuestObject(guest.GuestObject, guest.ActorState, indexInDoorBatch, batchCount);
        }
        else
        {
            Transform arrivalPoint = guest.Config.GetFrontDoorArrivalPoint(frontDoorArrivalPoint);
            PlaceGuestAt(guest, arrivalPoint, "frontDoorArrivalPoint");
        }

        if (guest.ActorState != null)
        {
            guest.ActorState.SetCurrentRoom(entryRoomId);
            guest.ActorState.SetAvailableInCurrentChapter(true);
            guest.ActorState.SetVisibleByChapterState(true);
            guest.ActorState.SetInteractable(false);
        }

        SetGuestState(guest, GuestArrivalState.Arriving);
        bool coatOfferedBeforeWaitMovement = false;

        if (useWorldSafePlacement)
        {
            ForceGuestVisibleForDoorFlow(guest);
            Transform waitSpot = CreateRuntimeAnchor(
                $"EntranceWait_{guest.Config.GuestId}",
                GetWorldEntranceWaitPosition(indexInDoorBatch, batchCount),
                null);
            SetGuestState(guest, GuestArrivalState.AwaitingGreeting);
            LogGuestLine(guest.Config, guest.Config.GreetingLine);

            if (guest.Annoyed)
            {
                Debug.Log($"{guest.Config.GuestDisplayName}: {GetAnnoyedLine(guest.GuestIndex)}", this);
            }

            OfferGuestCoat(guest);
            coatOfferedBeforeWaitMovement = true;
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} moving to entrance wait spot.", this);
            yield return MoveGuestTo(guest, waitSpot, "entrance waiting spot");
            ForceGuestVisibleForDoorFlow(guest);
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} reached entrance wait spot.", this);
        }
        else if (useAuthoredEntrancePosition)
        {
            ForceGuestVisibleForDoorFlow(guest);
            yield return null;
        }
        else if (snapGuestsIntoEntranceForFirstVisualPass)
        {
            Transform waitSpot = CreateRuntimeAnchor(
                $"EntranceWait_{guest.Config.GuestId}",
                GetEntranceWaitPosition(indexInDoorBatch, batchCount),
                frontDoorArrivalPoint);
            PlaceGuestAt(guest, waitSpot, "entrance waiting spot");
            ForceGuestVisibleForDoorFlow(guest);
            yield return null;
        }
        else
        {
            Transform waitSpot = CreateRuntimeAnchor(
                $"EntranceWait_{guest.Config.GuestId}",
                GetEntranceWaitPosition(indexInDoorBatch, batchCount),
                frontDoorArrivalPoint);
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} moving to entrance wait spot.", this);
            yield return MoveGuestTo(guest, waitSpot, "entrance waiting spot");
            ForceGuestVisibleForDoorFlow(guest);
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} reached entrance wait spot.", this);
        }

        if (!coatOfferedBeforeWaitMovement)
        {
            SetGuestState(guest, GuestArrivalState.AwaitingGreeting);
            LogGuestLine(guest.Config, guest.Config.GreetingLine);

            if (guest.Annoyed)
            {
                Debug.Log($"{guest.Config.GuestDisplayName}: {GetAnnoyedLine(guest.GuestIndex)}", this);
            }

            OfferGuestCoat(guest);
        }
    }

    private void OfferGuestCoat(GuestRuntimeState guest)
    {
        if (guest == null || guest.CoatOffered)
        {
            return;
        }

        guest.CoatOffered = true;
        SetGuestState(guest, GuestArrivalState.GreetingComplete);
        guest.CoatPickup = CreateCoatPickup(guest);
        SetGuestState(guest, GuestArrivalState.CoatOffered);
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
                child.name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) < 0)
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

            if (group == null || group.ExitingToDrawingRoom || group.Complete)
            {
                continue;
            }

            if (!AreAllGroupCoatsStored(group))
            {
                continue;
            }

            StartCoroutine(MoveGroupToDrawingRoom(group));
        }
    }

    private IEnumerator MoveGroupToDrawingRoom(GuestGroupRuntimeState group)
    {
        group.ExitingToDrawingRoom = true;

        for (int i = 0; i < group.Guests.Count; i++)
        {
            GuestRuntimeState guest = group.Guests[i];
            bool useWorldSafePlacement = IsWorldSpaceGuestObject(guest.GuestObject);
            Transform drawingRoomEntry = useWorldSafePlacement
                ? CreateRuntimeAnchor($"DrawingRoomEntry_{guest.Config.GuestId}", GetWorldDrawingRoomEntryPosition(i, group.Guests.Count), null)
                : guest.Config.GetDrawingRoomEntryPoint(drawingRoomEntryPoint);

            SetGuestState(guest, GuestArrivalState.MovingToDrawingRoom);
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} moving to drawing room door.", this);
            BeginGuestMoveTo(guest, drawingRoomEntry, "drawingRoomEntryPoint");
        }

        while (IsAnyGroupGuestMoving(group))
        {
            yield return null;
        }

        for (int i = 0; i < group.Guests.Count; i++)
        {
            GuestRuntimeState guest = group.Guests[i];

            if (guest.ActorState != null)
            {
                guest.ActorState.SetCurrentRoom(drawingRoomId);
                guest.ActorState.SetInteractable(false);
                guest.ActorState.SetSeated(true);
                guest.ActorState.SetVisibleByChapterState(false);
            }

            guest.Seated = true;
            guest.Handled = true;
            DisableGuestMovement(guest);
            SetGuestVisibleAfterDrawingRoomExit(guest, false);
            SetGuestState(guest, GuestArrivalState.Seated);
            SetGuestState(guest, GuestArrivalState.Handled);
            Debug.Log($"[Chapter1] Guest {guest.Config.GuestId} disappeared at drawing room door.", this);
        }

        group.Complete = true;
        activeEntranceGroups.Remove(group);
        Debug.Log($"Guest group {group.GroupIndex + 1} entered the drawing room.", this);
        RefreshInteractionState();
        CheckChapterCompletionGate();
    }

    private void CheckChapterCompletionGate()
    {
        if (!CanCompleteChapterOne())
        {
            return;
        }

        if (navigationManager == null || !SameRoom(navigationManager.CurrentRoom, drawingRoomId))
        {
            Debug.Log("Chapter 1 completion ready. Player must enter the drawing room.", this);
            return;
        }

        sequenceActive = false;
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

    private bool AreAllGroupCoatsStored(GuestGroupRuntimeState group)
    {
        if (group == null || group.Guests.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < group.Guests.Count; i++)
        {
            if (!group.Guests[i].CoatStored)
            {
                return false;
            }
        }

        return true;
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

    private void ActivateAuthoredChapterGuestObject(GameObject guestObject, ActorRoomState actorState, int index, int batchCount)
    {
        if (guestObject == null)
        {
            return;
        }

        DisableAmbientWalkers(guestObject);
        guestObject.SetActive(true);

        if (guestObject.transform is RectTransform)
        {
            PlaceChapterSceneGuestAtEntrance(guestObject, index, batchCount);
        }

        DisableAmbientWalkers(guestObject);

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

            if (snapGuestsIntoEntranceForFirstVisualPass)
            {
                actorState.enabled = false;
            }
        }

        ForceRenderersAndCollidersOn(guestObject);
        Debug.Log($"Scene guest activated: {guestObject.name}", this);
    }

    private void PlaceChapterSceneGuestAtEntrance(GameObject guestObject, int index, int batchCount)
    {
        if (guestObject == null)
        {
            return;
        }

        RectTransform rectTransform = guestObject.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = GetEntranceWaitAnchoredPosition(index, batchCount);
            return;
        }

        guestObject.transform.position = GetEntranceWaitPosition(index, batchCount);
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

        if (guestState.GuestObject != null &&
            guestState.GuestObject.transform is RectTransform rectTransform &&
            TryGetAnchoredPositionForGuestTarget(guestState, target, out Vector2 anchoredPosition))
        {
            rectTransform.anchoredPosition = anchoredPosition;
            return;
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.PlaceAt(target);
            return;
        }

        if (guestState.GuestObject != null)
        {
            guestState.GuestObject.transform.position = target.position;
        }
    }

    private void PlaceGuestAtPosition(GuestRuntimeState guestState, Vector3 position)
    {
        if (guestState == null)
        {
            return;
        }

        if (guestState.GuestObject != null)
        {
            Vector3 targetPosition = position;
            targetPosition.z = guestState.GuestObject.transform.position.z;
            guestState.GuestObject.transform.position = targetPosition;
            return;
        }

        if (guestState.ActorState != null)
        {
            guestState.ActorState.transform.position = position;
        }
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
            guestState.ActorState.SetCurrentRoom(entryRoomId);
            guestState.ActorState.SetAvailableInCurrentChapter(true);
            guestState.ActorState.SetVisibleByChapterState(true);
            guestState.ActorState.ApplyState();

            if (snapGuestsIntoEntranceForFirstVisualPass)
            {
                guestState.ActorState.enabled = false;
            }
        }

        if (guestState.GuestObject == null)
        {
            return;
        }

        if (IsChapterSceneGuest(guestState.GuestObject))
        {
            DisableAmbientWalkers(guestState.GuestObject);
        }

        ForceRenderersAndCollidersOn(guestState.GuestObject);
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
            yield break;
        }

        mover.enabled = true;
        mover.MoveSpeed = GetMoveSpeedForGuestObject(guestState.GuestObject);
        mover.MoveTo(target);

        while (mover != null && mover.IsMoving)
        {
            yield return null;
        }
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
        mover.MoveTo(target);
    }

    private static bool IsAnyGroupGuestMoving(GuestGroupRuntimeState group)
    {
        if (group == null)
        {
            return false;
        }

        for (int i = 0; i < group.Guests.Count; i++)
        {
            NPCWaypointMover mover = group.Guests[i] != null ? group.Guests[i].Mover : null;

            if (mover != null && mover.IsMoving)
            {
                return true;
            }
        }

        return false;
    }

    private void DisableGuestMovement(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.Mover == null)
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
            frontDoorSceneAction.SetAvailable(true);
        }
    }

    private void EnsureRuntimeInteractionSystems()
    {
        ResolveReferences();

        doorbellSystem?.Initialize(chapterClock);
        grandfatherClock?.Initialize(chapterClock);
        timeSettingsUI?.Initialize(chapterClock);

        if (interactionHUD != null)
        {
            interactionHUD.Initialize(this, chapterClock, grandfatherClock);
        }

        EnsureDoorAnswerTriggerAction(createRuntimeClickTargets);

        if (createRuntimeClickTargets)
        {
            EnsureSceneActionTargets();
        }
    }

    private void EnsureSceneActionTargets()
    {
        RemoveClickTarget("Chapter1_ClickTarget_CoatCloset");
        CreateClickTarget("Chapter1_ClickTarget_GrandfatherClock", grandfatherClock != null ? grandfatherClock.transform : null, Chapter1SceneActionType.GrandfatherClock);
        CreateClickTarget("Chapter1_ClickTarget_DrawingRoomExit", drawingRoomEntryPoint, Chapter1SceneActionType.DrawingRoomExit);
    }

    private void RemoveClickTarget(string objectName)
    {
        GameObject targetObject = GameObject.Find(objectName);

        if (targetObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(targetObject);
        }
        else
        {
            DestroyImmediate(targetObject);
        }
    }

    private void EnsureDoorAnswerTriggerAction(bool createFallback)
    {
        GameObject targetObject = FindDoorAnswerTriggerObject();

        if (targetObject == null && createFallback)
        {
            targetObject = CreateDoorAnswerTriggerFallback();
        }

        if (targetObject == null)
        {
            Debug.LogWarning($"Chapter1ArrivalController could not find '{DoorAnswerTriggerName}' for answering the front door.", this);
            return;
        }

        targetObject.SetActive(true);
        EnsureDoorAnswerTriggerCanReceiveClicks(targetObject);

        Chapter1SceneAction action = targetObject.GetComponent<Chapter1SceneAction>();

        if (action == null)
        {
            action = targetObject.AddComponent<Chapter1SceneAction>();
        }

        action.Initialize(Chapter1SceneActionType.FrontDoor, this, grandfatherClock);
        frontDoorSceneAction = action;
        frontDoorSceneAction.SetAvailable(true);
    }

    private GameObject FindDoorAnswerTriggerObject()
    {
        for (int i = 0; i < DoorAnswerTriggerNameAliases.Length; i++)
        {
            GameObject triggerObject = FindSceneObjectByExactName(DoorAnswerTriggerNameAliases[i]);

            if (triggerObject != null)
            {
                return triggerObject;
            }
        }

        return null;
    }

    private GameObject CreateDoorAnswerTriggerFallback()
    {
        if (frontDoorArrivalPoint == null)
        {
            return null;
        }

        GameObject triggerObject = new GameObject(DoorAnswerTriggerName);
        triggerObject.transform.SetParent(frontDoorArrivalPoint.parent, true);
        triggerObject.transform.position = frontDoorArrivalPoint.position;

        SpriteRenderer renderer = triggerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRuntimeCoatSprite();
        renderer.color = new Color(0.16f, 0.42f, 1f, 0.35f);
        renderer.sortingLayerName = "People";
        renderer.sortingOrder = 6500;

        BoxCollider2D collider = triggerObject.AddComponent<BoxCollider2D>();
        collider.size = GetDoorAnswerTriggerColliderSize(triggerObject);
        collider.isTrigger = true;
        return triggerObject;
    }

    private void EnsureDoorAnswerTriggerCanReceiveClicks(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return;
        }

        Graphic graphic = targetObject.GetComponent<Graphic>();

        if (targetObject.transform is RectTransform && graphic == null)
        {
            Image image = targetObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.42f, 1f, 0.35f);
            image.raycastTarget = true;
        }
        else if (graphic != null)
        {
            graphic.raycastTarget = true;
        }

        if (targetObject.transform is RectTransform)
        {
            return;
        }

        if (targetObject.GetComponent<Collider2D>() == null && targetObject.GetComponent<Collider>() == null)
        {
            BoxCollider2D collider = targetObject.AddComponent<BoxCollider2D>();
            collider.size = GetDoorAnswerTriggerColliderSize(targetObject);
            collider.isTrigger = true;
        }
    }

    private Vector2 GetDoorAnswerTriggerColliderSize(GameObject targetObject)
    {
        SpriteRenderer spriteRenderer = targetObject != null ? targetObject.GetComponent<SpriteRenderer>() : null;

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                return spriteSize;
            }
        }

        return Vector2.one;
    }

    private void CreateClickTarget(string objectName, Transform target, Chapter1SceneActionType actionType)
    {
        if (target == null)
        {
            return;
        }

        GameObject targetObject = GameObject.Find(objectName);

        if (targetObject == null)
        {
            targetObject = new GameObject(objectName);
            targetObject.transform.SetParent(target.parent, true);

            SpriteRenderer renderer = targetObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeCoatSprite();
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.sortingLayerName = "People";
            renderer.sortingOrder = 6000;

            BoxCollider2D collider = targetObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(160f, 160f);
            collider.isTrigger = true;
        }

        targetObject.transform.position = target.position;

        Chapter1SceneAction action = targetObject.GetComponent<Chapter1SceneAction>();

        if (action == null)
        {
            action = targetObject.AddComponent<Chapter1SceneAction>();
        }

        action.Initialize(actionType, this, grandfatherClock);

        if (actionType == Chapter1SceneActionType.FrontDoor)
        {
            frontDoorSceneAction = action;
            frontDoorSceneAction.SetAvailable(true);
        }
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
                guests[existingIndex].ConfigureRuntime(
                    MakeGuestId(guestObject.name, insertIndex),
                    guestObject.name,
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
            config.ConfigureRuntime(
                MakeGuestId(guestObject.name, insertIndex),
                guestObject.name,
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
            config.ConfigureRuntime(
                guestId,
                guestObject.name,
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
        string guestName = GetChapterGuestDisplayName(index);
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
        for (int i = 0; i < ChapterGuestNameAliases.Length; i++)
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
            config.ConfigureRuntime(
                MakeGuestId(candidate.name, nextIndex),
                candidate.name,
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
        DisableAmbientWalkers(guestObject);
        ConfigureGuestPhysicsForScriptedMovement(guestObject);
        EnsureGuestAnimatorUsesButlerController(guestObject);

        ActorRoomState actorState = guestObject.GetComponent<ActorRoomState>();

        if (actorState == null)
        {
            actorState = guestObject.AddComponent<ActorRoomState>();
        }

        actorState.SetActorId(MakeGuestId(guestObject.name, index));

        SpriteRenderer[] renderers = guestObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].sortingLayerName = "People";
            renderers[i].sortingOrder = 9000 + index;
        }
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

    private void EnsureGuestAnimatorUsesButlerController(GameObject guestObject)
    {
        Animator sourceAnimator = playerButlerReference != null
            ? playerButlerReference.GetComponentInChildren<Animator>(true)
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

        if (guestAnimator.runtimeAnimatorController != null)
        {
            return;
        }

        guestAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
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

    private void EnsureRuntimeCloset()
    {
        if (coatCloset != null && IsRuntimeEntranceCloset(coatCloset.gameObject))
        {
            ConfigureRuntimeWardrobeObject(coatCloset.gameObject);
            closetPoint = coatCloset.transform;
            return;
        }

        if (coatCloset != null && SameRoom(GetRoomForTransform(coatCloset.transform), entryRoomId))
        {
            return;
        }

        GameObject closetObject = GameObject.Find("Wardrobe_EntranceHall_Runtime");

        if (closetObject == null)
        {
            closetObject = GameObject.Find("CoatCloset_EntranceHall_Runtime");
        }

        if (closetObject == null)
        {
            closetObject = new GameObject("Wardrobe_EntranceHall_Runtime");
            Transform parent = frontDoorArrivalPoint != null && frontDoorArrivalPoint.parent != null ? frontDoorArrivalPoint.parent : transform;
            closetObject.transform.SetParent(parent, true);
        }

        closetObject.name = "Wardrobe_EntranceHall_Runtime";
        closetObject.transform.position = GetRuntimeWardrobePosition();
        ConfigureRuntimeWardrobeObject(closetObject);

        coatCloset = closetObject.GetComponent<CoatCloset>();

        if (coatCloset == null)
        {
            coatCloset = closetObject.AddComponent<CoatCloset>();
        }

        if (closetPoint == null || !SameRoom(GetRoomForTransform(closetPoint), entryRoomId))
        {
            closetPoint = closetObject.transform;
        }
    }

    private Vector3 GetRuntimeWardrobePosition()
    {
        if (closetPoint != null && SameRoom(GetRoomForTransform(closetPoint), entryRoomId))
        {
            return closetPoint.position;
        }

        return GetWorldEntranceCenterPosition() + new Vector3(-1.75f, -0.55f, 0f);
    }

    private void ConfigureRuntimeWardrobeObject(GameObject wardrobeObject)
    {
        if (wardrobeObject == null)
        {
            return;
        }

        SpriteRenderer renderer = wardrobeObject.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = wardrobeObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = GetRuntimeWardrobeSprite();
        renderer.color = Color.white;
        renderer.sortingLayerName = "People";
        renderer.sortingOrder = 120;
        renderer.enabled = true;

        BoxCollider2D collider = wardrobeObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = wardrobeObject.AddComponent<BoxCollider2D>();
        }

        collider.size = WardrobeColliderSize;
        collider.offset = new Vector2(0f, WardrobeColliderSize.y * 0.5f);
        collider.isTrigger = true;
        collider.enabled = true;

        Chapter1SceneAction action = wardrobeObject.GetComponent<Chapter1SceneAction>();

        if (action == null)
        {
            action = wardrobeObject.AddComponent<Chapter1SceneAction>();
        }

        action.Initialize(Chapter1SceneActionType.CoatCloset, this, grandfatherClock);
        action.SetAvailable(true);
    }

    private Transform ResolveSeatForGuest(int index)
    {
        switch (index)
        {
            case 0:
                if (drawingRoomSeat01 != null) return drawingRoomSeat01;
                break;
            case 1:
                if (drawingRoomSeat02 != null) return drawingRoomSeat02;
                break;
            case 2:
                if (drawingRoomSeat03 != null) return drawingRoomSeat03;
                break;
        }

        while (runtimeSeatAnchors.Count <= index)
        {
            int seatIndex = runtimeSeatAnchors.Count;
            Vector3 basePosition = drawingRoomSeat03 != null
                ? drawingRoomSeat03.position
                : drawingRoomEntryPoint != null ? drawingRoomEntryPoint.position : transform.position;
            int column = seatIndex % 4;
            int row = seatIndex / 4;
            Vector3 position = basePosition + new Vector3((column - 1.5f) * drawingRoomSeatSpacing, -row * drawingRoomSeatSpacing, 0f);
            runtimeSeatAnchors.Add(CreateRuntimeAnchor($"DrawingRoomSeat_Runtime_{seatIndex + 1:00}", position, drawingRoomEntryPoint));
        }

        return runtimeSeatAnchors[index];
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

    private float GetMoveSpeedForGuestObject(GameObject guestObject)
    {
        return IsWorldSpaceGuestObject(guestObject)
            ? Mathf.Max(0.01f, worldGuestMoveSpeed)
            : Mathf.Max(0.01f, guestMoveSpeed);
    }

    private Vector3 GetEntranceWaitPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = frontDoorArrivalPoint != null
            ? frontDoorArrivalPoint.position
            : butlerDoorSpot != null ? butlerDoorSpot.position : transform.position;

        if (snapGuestsIntoEntranceForFirstVisualPass)
        {
            if (playerButlerReference != null)
            {
                basePosition = playerButlerReference.transform.position;
            }
            else if (butlerDoorSpot != null)
            {
                basePosition = butlerDoorSpot.position;
            }
        }

        float centeredIndex = indexInBatch - (batchCount - 1) * 0.5f;
        Vector3 offset = snapGuestsIntoEntranceForFirstVisualPass
            ? new Vector3(centeredIndex * entranceGuestSpacing, entranceGuestSpacing * 0.65f, 0f)
            : new Vector3(centeredIndex * entranceGuestSpacing, -entranceGuestSpacing * 0.55f, 0f);
        return basePosition + offset;
    }

    private Vector3 GetWorldDoorArrivalPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetWorldEntranceCenterPosition();
        Vector2 offset = GetWorldGuestGridOffset(indexInBatch, batchCount, worldEntranceGuestSpacing);
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetWorldEntranceWaitPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetWorldEntranceCenterPosition();
        Vector2 offset = GetWorldGuestGridOffset(indexInBatch, batchCount, worldEntranceGuestSpacing);
        return basePosition + new Vector3(offset.x, offset.y - worldEntranceGuestSpacing * 1.5f, 0f);
    }

    private Vector3 GetWorldDrawingRoomEntryPosition(int indexInBatch, int batchCount)
    {
        Vector3 basePosition = GetEntranceDrawingRoomExitPosition();
        Vector2 offset = GetWorldGuestGridOffset(indexInBatch, batchCount, worldDrawingRoomSeatSpacing);
        return basePosition + new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetEntranceDrawingRoomExitPosition()
    {
        if (hasEntranceDrawingRoomExitPosition)
        {
            return entranceDrawingRoomExitPosition;
        }

        Vector3 basePosition = drawingRoomEntryPoint != null
            ? drawingRoomEntryPoint.position
            : GetWorldEntranceCenterPosition();

        if (TryGetGrandEntranceDrawingRoomDoorX(out float doorX))
        {
            basePosition.x = doorX;
        }
        else
        {
            Vector3 centerPosition = GetWorldEntranceCenterPosition();
            float distanceFromCenter = Mathf.Abs(basePosition.x - centerPosition.x);

            if (distanceFromCenter > 0.01f && basePosition.x > centerPosition.x)
            {
                basePosition.x = centerPosition.x - distanceFromCenter;
            }
        }

        entranceDrawingRoomExitPosition = basePosition;
        hasEntranceDrawingRoomExitPosition = true;
        return entranceDrawingRoomExitPosition;
    }

    private bool TryGetGrandEntranceDrawingRoomDoorX(out float doorX)
    {
        doorX = 0f;
        DoorTriggerNavigation[] doorTriggers = FindObjectsByType<DoorTriggerNavigation>(FindObjectsInactive.Include);

        for (int i = 0; i < doorTriggers.Length; i++)
        {
            DoorTriggerNavigation doorTrigger = doorTriggers[i];

            if (doorTrigger == null ||
                !SameRoom(doorTrigger.SourceRoom, entryRoomId) ||
                !SameRoom(doorTrigger.DestinationRoom, drawingRoomId))
            {
                continue;
            }

            RectTransform rectTransform = doorTrigger.transform as RectTransform;
            doorX = rectTransform != null ? rectTransform.anchoredPosition.x : doorTrigger.transform.position.x;
            return true;
        }

        return false;
    }

    private Vector3 GetWorldDrawingRoomSeatPosition(int guestIndex)
    {
        Vector3 basePosition = GetWorldDrawingRoomCenterPosition();
        int columns = Mathf.Max(1, Mathf.Min(4, guestsPerArrivalGroup * 2));
        int column = guestIndex % columns;
        int row = guestIndex / columns;
        float centeredColumn = column - (columns - 1) * 0.5f;
        return basePosition + new Vector3(
            centeredColumn * worldDrawingRoomSeatSpacing,
            -row * worldDrawingRoomSeatSpacing * 0.8f,
            0f);
    }

    private Vector2 GetWorldGuestGridOffset(int index, int count, float spacing)
    {
        int columns = Mathf.Max(1, Mathf.Min(4, count));
        int column = index % columns;
        int row = index / columns;
        float centeredColumn = column - (columns - 1) * 0.5f;
        return new Vector2(centeredColumn * spacing, -row * spacing * 0.8f);
    }

    private Vector3 GetWorldEntranceCenterPosition()
    {
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

    private Vector3 GetWorldDrawingRoomCenterPosition()
    {
        return GetWorldEntranceCenterPosition() + new Vector3(0f, 0.35f, 0f);
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

    private Vector2 GetEntranceWaitAnchoredPosition(int indexInBatch, int batchCount)
    {
        Vector2 basePosition = frontDoorArrivalPoint != null
            ? new Vector2(frontDoorArrivalPoint.localPosition.x, frontDoorArrivalPoint.localPosition.y)
            : butlerDoorSpot != null
                ? new Vector2(butlerDoorSpot.localPosition.x, butlerDoorSpot.localPosition.y)
                : Vector2.zero;

        float centeredIndex = indexInBatch - (batchCount - 1) * 0.5f;
        Vector2 offset = snapGuestsIntoEntranceForFirstVisualPass
            ? new Vector2(centeredIndex * entranceGuestSpacing, entranceGuestSpacing * 0.65f)
            : new Vector2(centeredIndex * entranceGuestSpacing, -entranceGuestSpacing * 0.55f);
        return basePosition + offset;
    }

    private bool TryGetAnchoredPositionForGuestTarget(GuestRuntimeState guestState, Transform target, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;

        if (target == null)
        {
            return false;
        }

        if (snapGuestsIntoEntranceForFirstVisualPass &&
            IsChapterSceneGuest(guestState?.GuestObject) &&
            target.name.StartsWith("EntranceWait_", StringComparison.OrdinalIgnoreCase))
        {
            int indexInBatch = guestState != null ? guestState.GuestIndex % Mathf.Max(1, guestsPerArrivalGroup) : 0;
            anchoredPosition = GetEntranceWaitAnchoredPosition(indexInBatch, Mathf.Max(1, guestsPerArrivalGroup));
            return true;
        }

        if (target is RectTransform targetRectTransform)
        {
            anchoredPosition = targetRectTransform.anchoredPosition;
            return true;
        }

        anchoredPosition = new Vector2(target.localPosition.x, target.localPosition.y);
        return true;
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
        switch (index % 4)
        {
            case 0: return "Good evening.";
            case 1: return "Thank you.";
            case 2: return "Lovely to see you.";
            default: return "Good evening, Butler.";
        }
    }

    private string GetDefaultAmbientLine(int index)
    {
        switch (index % 4)
        {
            case 0: return "This house is colder than I expected.";
            case 1: return "The host is late, isn't he?";
            case 2: return "Did you hear something upstairs?";
            default: return "The drawing room should be warmer.";
        }
    }

    private string GetAnnoyedLine(int index)
    {
        switch (index % 4)
        {
            case 0: return "We were beginning to wonder if anyone was home.";
            case 1: return "It is rather cold out there.";
            case 2: return "We have been waiting at the door for some time.";
            default: return "At last.";
        }
    }

    private Sprite GetRuntimeCoatSprite()
    {
        if (runtimeCoatSprite == null)
        {
            runtimeCoatSprite = CreateSolidSprite("RuntimeCoatSprite", new Color(0.38f, 0.25f, 0.16f, 1f), 64, 42, new Vector2(0.5f, 0.5f), 2f);
        }

        return runtimeCoatSprite;
    }

    private Sprite GetRuntimeWardrobeSprite()
    {
        if (runtimeWardrobeSprite == null)
        {
            runtimeWardrobeSprite = CreateWardrobeSprite();
        }

        return runtimeWardrobeSprite;
    }

    private Sprite GetRuntimeGuestSprite()
    {
        if (runtimeGuestSprite == null)
        {
            runtimeGuestSprite = CreateSolidSprite("RuntimeGuestSprite", new Color(0.18f, 0.24f, 0.36f, 1f), 48, 96, new Vector2(0.5f, 0.08f), 2f);
        }

        return runtimeGuestSprite;
    }

    private static Sprite CreateWardrobeSprite()
    {
        const int width = 72;
        const int height = 112;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color wood = new Color(0.28f, 0.16f, 0.09f, 1f);
        Color trim = new Color(0.13f, 0.08f, 0.045f, 1f);
        Color panel = new Color(0.40f, 0.24f, 0.13f, 1f);
        Color knob = new Color(0.88f, 0.68f, 0.28f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        for (int y = 6; y < height - 4; y++)
        {
            for (int x = 10; x < width - 10; x++)
            {
                bool border = x < 14 || x >= width - 14 || y < 10 || y >= height - 8;
                bool centerLine = Mathf.Abs(x - width / 2) <= 1;
                bool insetPanel = x > 17 && x < width - 17 && y > 18 && y < height - 18 && !centerLine;
                texture.SetPixel(x, y, border || centerLine ? trim : insetPanel ? panel : wood);
            }
        }

        for (int y = 53; y <= 58; y++)
        {
            for (int x = 31; x <= 40; x++)
            {
                texture.SetPixel(x, y, knob);
            }
        }

        texture.Apply();
        texture.name = "RuntimeWardrobeSprite";
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.05f), 64f);
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

    private static bool IsRuntimeEntranceCloset(GameObject target)
    {
        return target != null &&
            (string.Equals(target.name, "Wardrobe_EntranceHall_Runtime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.name, "CoatCloset_EntranceHall_Runtime", StringComparison.OrdinalIgnoreCase));
    }

    private void ResolveReferences()
    {
        ResolveReferences(true);
    }

    private void ResolveReferences(bool createFallbacks)
    {
        if (chapterManager == null)
        {
            chapterManager = GetComponent<ChapterManager>();
        }

        if (chapterManager == null)
        {
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        }

        if (chapterClock == null && chapterManager != null)
        {
            chapterClock = chapterManager.Clock;
        }

        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (eventScheduler == null && chapterManager != null)
        {
            eventScheduler = chapterManager.EventScheduler;
        }

        if (eventScheduler == null)
        {
            eventScheduler = FindAnyObjectByType<ChapterEventScheduler>(FindObjectsInactive.Include);
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (playerButlerReference == null && chapterManager != null)
        {
            playerButlerReference = chapterManager.PlayerButlerReference;
        }

        if (playerButlerReference == null)
        {
            GameObject namedPlayer = GameObject.Find("Player");

            if (namedPlayer != null)
            {
                playerButlerReference = namedPlayer;
            }
        }

        if (playerMovement == null && playerButlerReference != null)
        {
            playerMovement = playerButlerReference.GetComponent<PointClickPlayerMovement>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindPlayerMovement();
        }

        if (playerButlerReference == null && playerMovement != null)
        {
            playerButlerReference = playerMovement.gameObject;
        }

        ResolveAnchors();
        ResolveStoryHelpers(createFallbacks);

        if (createFallbacks)
        {
            EnsureRuntimeCloset();
        }
    }

    private static PointClickPlayerMovement FindPlayerMovement()
    {
        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                !IsGuestObject(candidate.gameObject) &&
                string.Equals(candidate.gameObject.name, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                !IsGuestObject(candidate.gameObject))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsGuestObject(GameObject target)
    {
        return target != null &&
            target.name.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ResolveStoryHelpers(bool createFallbacks)
    {
        if (doorbellSystem == null)
        {
            doorbellSystem = FindAnyObjectByType<DoorbellSystem>(FindObjectsInactive.Include);
        }

        if (doorbellSystem == null && createFallbacks)
        {
            doorbellSystem = gameObject.AddComponent<DoorbellSystem>();
        }

        if (grandfatherClock == null)
        {
            grandfatherClock = FindAnyObjectByType<GrandfatherClockInteraction>(FindObjectsInactive.Include);
        }

        if (grandfatherClock == null && createFallbacks)
        {
            GameObject clockObject = FindGameObjectByNormalizedName("GrandfatherClock");
            grandfatherClock = clockObject != null
                ? clockObject.AddComponent<GrandfatherClockInteraction>()
                : gameObject.AddComponent<GrandfatherClockInteraction>();
        }

        if (timeSettingsUI == null)
        {
            timeSettingsUI = FindAnyObjectByType<ChapterTimeSettingsUI>(FindObjectsInactive.Include);
        }

        if (timeSettingsUI == null && createFallbacks)
        {
            timeSettingsUI = gameObject.AddComponent<ChapterTimeSettingsUI>();
        }

        if (interactionHUD == null)
        {
            interactionHUD = FindAnyObjectByType<Chapter1InteractionHUD>(FindObjectsInactive.Include);
        }

        if (interactionHUD == null && createFallbacks && createRuntimeHud)
        {
            interactionHUD = gameObject.AddComponent<Chapter1InteractionHUD>();
        }
    }

    private void ResolveAnchors()
    {
        if (frontDoorArrivalPoint == null)
        {
            frontDoorArrivalPoint = FindAnchor("GuestArrival_Door", entryRoomId);
        }

        if (butlerDoorSpot == null)
        {
            butlerDoorSpot = FindAnchor("ButlerGreetingSpot", entryRoomId);
        }

        if (drawingRoomEntryPoint == null)
        {
            drawingRoomEntryPoint = FindAnchor("DrawingRoomEntry", drawingRoomId);
        }

        if (drawingRoomSeat01 == null)
        {
            drawingRoomSeat01 = FindAnchor("Seat_01", drawingRoomId);
        }

        if (drawingRoomSeat02 == null)
        {
            drawingRoomSeat02 = FindAnchor("Seat_02", drawingRoomId);
        }

        if (drawingRoomSeat03 == null)
        {
            drawingRoomSeat03 = FindAnchor("Seat_03", drawingRoomId);
        }

        if (closetPoint == null)
        {
            closetPoint = FindPropAnchor("CoatCloset", "ApproachFront", entryRoomId)
                ?? FindPropAnchor("Closet", "ApproachFront", entryRoomId)
                ?? FindAnchor("ApproachFront", entryRoomId);
        }

        if (coatCloset == null)
        {
            CoatCloset[] closets = FindObjectsByType<CoatCloset>(FindObjectsInactive.Include);

            for (int i = 0; i < closets.Length; i++)
            {
                if (closets[i] != null && SameRoom(GetRoomForTransform(closets[i].transform), entryRoomId))
                {
                    coatCloset = closets[i];
                    break;
                }
            }
        }
    }

    private Transform FindAnchor(string anchorId, string roomId)
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (string.Equals(anchor.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase) &&
                SameRoom(anchor.RoomId, roomId))
            {
                return anchor.transform;
            }
        }

        return null;
    }

    private Transform FindPropAnchor(string propName, string anchorId, string roomId)
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (!string.Equals(anchor.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase) ||
                !SameRoom(anchor.RoomId, roomId) ||
                !IsUnderNamedTransform(anchor.transform, propName))
            {
                continue;
            }

            return anchor.transform;
        }

        return null;
    }

    private static bool IsUnderNamedTransform(Transform target, string normalizedName)
    {
        string cleanNeedle = NormalizeRoomName(normalizedName);
        Transform current = target;

        while (current != null)
        {
            if (NormalizeRoomName(current.name).Contains(cleanNeedle))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static GameObject FindGameObjectByNormalizedName(string normalizedName)
    {
        string cleanNeedle = NormalizeRoomName(normalizedName);
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];

            if (current != null && NormalizeRoomName(current.name).Contains(cleanNeedle))
            {
                return current.gameObject;
            }
        }

        return null;
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

    private void SubscribeToRoomChanges()
    {
        if (subscribedToRoomChanges)
        {
            return;
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
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
        if (SameRoom(roomName, drawingRoomId))
        {
            CheckChapterCompletionGate();
        }
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
