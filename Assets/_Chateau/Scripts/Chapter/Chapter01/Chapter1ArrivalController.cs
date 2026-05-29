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

    [Header("Interactions")]
    [SerializeField] private bool createRuntimeHud = true;
    [SerializeField] private bool createRuntimeClickTargets = true;
    [SerializeField] private bool placeButlerAtDoorSpotOnStart = true;
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
    private int currentGuestIndex = -1;
    private bool sequenceActive;
    private bool finalEmptyDoorbellOccurred;
    private bool emptyDoorbellWaitingForAnswer;
    private bool butlerCarryingCoat;
    private string carriedCoatId = string.Empty;
    private Chapter1SceneAction frontDoorSceneAction;
    private GuestRuntimeState carriedCoatGuest;
    private Sprite runtimeCoatSprite;
    private bool subscribedToRoomChanges;

    private const float RuntimeCoatVisualScale = 0.03f;
    private static readonly string[] FirstDoorSceneGuestNames = { "Guest 1", "Guest 2" };

    public int CurrentGuestIndex => currentGuestIndex;
    public bool ButlerCarryingCoat => butlerCarryingCoat;
    public string CarriedCoatId => carriedCoatId;

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
        SetFirstDoorSceneGuestsActive(false);
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
        Debug.Log("Answer Door action received.", this);

        if (!sequenceActive)
        {
            Debug.Log("Front door clicked, but Chapter 1 arrival sequence is not active.", this);
            return;
        }

        ShowFirstDoorSceneGuestsImmediately();

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
            Debug.Log($"The butler is already carrying coat '{carriedCoatId}'. Place it in the closet first.", this);
            return;
        }

        GuestRuntimeState guestState = FindGuestByCoat(coatPickup.CoatId);

        if (guestState == null || !guestState.CoatOffered || guestState.CoatTaken || guestState.CoatStored)
        {
            Debug.Log("That coat is not ready to be taken.", this);
            return;
        }

        guestState.CoatTaken = true;
        butlerCarryingCoat = true;
        carriedCoatId = guestState.Config.CoatId;
        carriedCoatGuest = guestState;
        SetGuestState(guestState, GuestArrivalState.CoatTaken);

        if (guestState.CoatPickup != null)
        {
            guestState.CoatPickup.gameObject.SetActive(false);
        }

        Debug.Log($"Coat taken from guest: {carriedCoatId}", this);
        RefreshInteractionState();
    }

    public void HandleClosetClicked()
    {
        ResolveReferences();

        if (!butlerCarryingCoat)
        {
            Debug.Log("Closet clicked, but the butler is not carrying a coat.", this);
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
        }

        if (carriedCoatGuest != null)
        {
            carriedCoatGuest.CoatStored = true;
            carriedCoatGuest.Handled = false;
        }

        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
        carriedCoatGuest = null;
        RefreshInteractionState();
        CheckActiveGroupsReadyForDrawingRoom();
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

        if (configuredGuestObjectCount == 0 && sceneGuestCandidateCount == 0 && guests == null)
        {
            Debug.LogWarning("Chapter1ArrivalController guest list is incomplete. Runtime placeholder guests will be created for testing.", this);
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
        currentGuestIndex = -1;
        pendingGuestGroups.Clear();
        activeEntranceGroups.Clear();
        guestGroups.Clear();

        if (coatCloset != null)
        {
            coatCloset.ClearStoredCoats();
        }

        ResetGuestStates(true);
        SetFirstDoorSceneGuestsActive(false);
        BuildGuestGroups();
        MoveButlerToStart();
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
                mover.MoveSpeed = guestMoveSpeed;
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
        int activeGuestGroupCount = requiredGuestCount > 0
            ? Mathf.CeilToInt(requiredGuestCount / (float)Mathf.Max(1, guestsPerArrivalGroup))
            : guestGroupCount;

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
        Transform arrivalPoint = guest.Config.GetFrontDoorArrivalPoint(frontDoorArrivalPoint);
        PlaceGuestAt(guest, arrivalPoint, "frontDoorArrivalPoint");

        if (guest.ActorState != null)
        {
            guest.ActorState.SetCurrentRoom(entryRoomId);
            guest.ActorState.SetAvailableInCurrentChapter(true);
            guest.ActorState.SetVisibleByChapterState(true);
            guest.ActorState.SetInteractable(false);
        }

        SetGuestState(guest, GuestArrivalState.Arriving);
        Transform waitSpot = CreateRuntimeAnchor($"EntranceWait_{guest.Config.GuestId}", GetEntranceWaitPosition(indexInDoorBatch, batchCount), frontDoorArrivalPoint);

        if (snapGuestsIntoEntranceForFirstVisualPass)
        {
            PlaceGuestAt(guest, waitSpot, "entrance waiting spot");
            ForceGuestVisibleForDoorFlow(guest);
            yield return null;
        }
        else
        {
            yield return MoveGuestTo(guest, waitSpot, "entrance waiting spot");
            ForceGuestVisibleForDoorFlow(guest);
        }

        SetGuestState(guest, GuestArrivalState.AwaitingGreeting);
        LogGuestLine(guest.Config, guest.Config.GreetingLine);

        if (guest.Annoyed)
        {
            Debug.Log($"{guest.Config.GuestDisplayName}: {GetAnnoyedLine(guest.GuestIndex)}", this);
        }

        OfferGuestCoat(guest);
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
        Debug.Log($"Coat offered by {guest.Config.GuestDisplayName}: {guest.Config.CoatId}", this);
    }

    private Chapter1CoatPickup CreateCoatPickup(GuestRuntimeState guest)
    {
        GameObject coatObject = new GameObject($"Coat_{guest.Config.GuestId}");
        Transform parent = guest.GuestObject != null && guest.GuestObject.transform.parent != null
            ? guest.GuestObject.transform.parent
            : transform;
        coatObject.transform.SetParent(parent, true);
        coatObject.transform.position = GetCoatPosition(guest);
        coatObject.transform.localScale = Vector3.one;
        SpriteRenderer renderer = CreateRuntimeVisual(coatObject.transform, "Visual_Coat", GetRuntimeCoatSprite(), RuntimeCoatVisualScale);
        renderer.sortingLayerName = "People";
        renderer.sortingOrder = 9500 + guest.GuestIndex;

        BoxCollider2D collider = coatObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(90f, 70f);
        collider.isTrigger = true;

        Chapter1CoatPickup pickup = coatObject.AddComponent<Chapter1CoatPickup>();
        pickup.Initialize(this, guest.Config.GuestId, guest.Config.CoatId);
        return pickup;
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
            SetGuestState(guest, GuestArrivalState.MovingToDrawingRoom);
            yield return MoveGuestTo(guest, guest.Config.GetDrawingRoomEntryPoint(drawingRoomEntryPoint), "drawingRoomEntryPoint");
            yield return MoveGuestTo(guest, guest.Seat, "assignedSeat");

            if (guest.ActorState != null)
            {
                guest.ActorState.SetCurrentRoom(drawingRoomId);
                guest.ActorState.SetInteractable(false);
                guest.ActorState.SetSeated(true);
            }

            guest.Seated = true;
            guest.Handled = true;
            SetGuestState(guest, GuestArrivalState.Seated);
            DisableGuestMovement(guest);
            StartAmbientConversation(guest);
            SetGuestState(guest, GuestArrivalState.Handled);
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
        if (snapGuestsIntoEntranceForFirstVisualPass && IsFirstDoorSceneGuest(guestState?.GuestObject))
        {
            return;
        }

        if (guestState == null || guestState.ActorState == null)
        {
            return;
        }

        guestState.ActorState.SetAvailableInCurrentChapter(false);
        guestState.ActorState.SetVisibleByChapterState(false);
        guestState.ActorState.SetInteractable(false);
    }

    private void SetFirstDoorSceneGuestsActive(bool active)
    {
        for (int i = 0; i < FirstDoorSceneGuestNames.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(FirstDoorSceneGuestNames[i]);

            if (guestObject != null)
            {
                guestObject.SetActive(active);
            }
        }
    }

    private void ShowFirstDoorSceneGuestsImmediately()
    {
        for (int i = 0; i < FirstDoorSceneGuestNames.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(FirstDoorSceneGuestNames[i]);

            if (guestObject == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController could not find scene guest '{FirstDoorSceneGuestNames[i]}'.", this);
                continue;
            }

            guestObject.SetActive(true);
            guestObject.transform.position = GetEntranceWaitPosition(i, FirstDoorSceneGuestNames.Length);

            ActorRoomState actorState = guestObject.GetComponent<ActorRoomState>();

            if (actorState != null)
            {
                actorState.enabled = false;
            }

            ForceRenderersAndCollidersOn(guestObject);
            Debug.Log($"Scene guest activated: {guestObject.name}", this);
        }
    }

    private bool IsFirstDoorSceneGuest(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return false;
        }

        for (int i = 0; i < FirstDoorSceneGuestNames.Length; i++)
        {
            if (string.Equals(guestObject.name.Trim(), FirstDoorSceneGuestNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        mover.MoveSpeed = guestMoveSpeed;
        mover.MoveTo(target);

        while (mover != null && mover.IsMoving)
        {
            yield return null;
        }
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
        bool doorAnswerAvailable = pendingGuestGroups.Count > 0 || emptyDoorbellWaitingForAnswer;

        if (interactionHUD != null)
        {
            interactionHUD.SetDoorAnswerAvailable(doorAnswerAvailable);
            interactionHUD.SetHangCoatAvailable(butlerCarryingCoat);
        }

        if (frontDoorSceneAction != null)
        {
            frontDoorSceneAction.SetAvailable(doorAnswerAvailable);
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

        if (createRuntimeClickTargets)
        {
            EnsureSceneActionTargets();
        }
    }

    private void EnsureSceneActionTargets()
    {
        CreateClickTarget("Chapter1_ClickTarget_FrontDoor", frontDoorArrivalPoint, Chapter1SceneActionType.FrontDoor);
        CreateClickTarget("Chapter1_ClickTarget_CoatCloset", closetPoint != null ? closetPoint : coatCloset != null ? coatCloset.transform : null, Chapter1SceneActionType.CoatCloset);
        CreateClickTarget("Chapter1_ClickTarget_GrandfatherClock", grandfatherClock != null ? grandfatherClock.transform : null, Chapter1SceneActionType.GrandfatherClock);
        CreateClickTarget("Chapter1_ClickTarget_DrawingRoomExit", drawingRoomEntryPoint, Chapter1SceneActionType.DrawingRoomExit);
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
            frontDoorSceneAction.SetAvailable(pendingGuestGroups.Count > 0 || emptyDoorbellWaitingForAnswer);
        }
    }

    private void MoveButlerToStart()
    {
        if (!placeButlerAtDoorSpotOnStart || playerMovement == null || butlerDoorSpot == null)
        {
            return;
        }

        playerMovement.TryWarpTo(butlerDoorSpot.position, true);
    }

    private void EnsureGuestConfigs(bool createFallbacks)
    {
        guests.RemoveAll(guest => guest == null);
        int namedSceneGuestCount = EnsureNamedSceneGuestsConfigured();
        int adoptedSceneGuestCount = AdoptExistingSceneGuests();
        int totalSceneGuestCount = namedSceneGuestCount + adoptedSceneGuestCount;

        if (totalSceneGuestCount > 0)
        {
            Debug.Log($"Chapter 1 using {totalSceneGuestCount} existing scene guest object(s) for the arrival sequence.", this);
        }
    }

    private int EnsureNamedSceneGuestsConfigured()
    {
        int addedCount = 0;
        int insertIndex = 0;

        for (int i = 0; i < FirstDoorSceneGuestNames.Length; i++)
        {
            GameObject guestObject = FindSceneObjectByExactName(FirstDoorSceneGuestNames[i]);

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

            if (candidate == null || HasGuestConfigForObject(candidate))
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
        int requestedGuestCount = guestGroupCount * guestsPerArrivalGroup;

        if (guestStates.Count > 0 && guestStates.Count < requestedGuestCount)
        {
            return guestStates.Count;
        }

        return requestedGuestCount;
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
            HideRuntimePlaceholderRenderers(coatCloset.gameObject);
        }

        if (coatCloset != null && SameRoom(GetRoomForTransform(coatCloset.transform), entryRoomId))
        {
            return;
        }

        GameObject closetObject = GameObject.Find("CoatCloset_EntranceHall_Runtime");

        if (closetObject == null)
        {
            closetObject = new GameObject("CoatCloset_EntranceHall_Runtime");
            Transform parent = frontDoorArrivalPoint != null && frontDoorArrivalPoint.parent != null ? frontDoorArrivalPoint.parent : transform;
            closetObject.transform.SetParent(parent, true);
            Vector3 fallbackPosition = butlerDoorSpot != null
                ? butlerDoorSpot.position + new Vector3(-180f, 0f, 0f)
                : transform.position;
            closetObject.transform.position = closetPoint != null ? closetPoint.position : fallbackPosition;

            BoxCollider2D collider = closetObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(8f, 8f);
            collider.isTrigger = true;
        }

        HideRuntimePlaceholderRenderers(closetObject);

        coatCloset = closetObject.GetComponent<CoatCloset>();

        if (coatCloset == null)
        {
            coatCloset = closetObject.AddComponent<CoatCloset>();
        }

        if (closetPoint == null)
        {
            closetPoint = closetObject.transform;
        }
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

    private Vector3 GetCoatPosition(GuestRuntimeState guest)
    {
        Vector3 basePosition = guest != null && guest.GuestObject != null ? guest.GuestObject.transform.position : transform.position;
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
            string.Equals(target.name, "CoatCloset_EntranceHall_Runtime", StringComparison.OrdinalIgnoreCase);
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
