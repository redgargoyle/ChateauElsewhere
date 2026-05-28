using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class Chapter1ArrivalController : MonoBehaviour
{
    private sealed class GuestRuntimeState
    {
        public GuestArrivalConfig Config;
        public GuestArrivalState State;
        public bool Handled;
        public bool CoatTaken;
        public bool Seated;
        public NPCWaypointMover Mover;
        public ActorRoomState ActorState;
    }

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private ChapterEventScheduler eventScheduler;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private GameObject playerButlerReference;
    [SerializeField] private CoatCloset coatCloset;

    [Header("Rooms")]
    [SerializeField] private string entryRoomId = "Grand Entrance Hall";
    [SerializeField] private string drawingRoomId = "Drawing Room";

    [Header("Required Waypoints")]
    [SerializeField] private Transform frontDoorArrivalPoint;
    [SerializeField] private Transform butlerDoorSpot;
    [SerializeField] private Transform closetPoint;
    [SerializeField] private Transform drawingRoomEntryPoint;
    [SerializeField] private Transform drawingRoomSeat01;
    [SerializeField] private Transform drawingRoomSeat02;
    [SerializeField] private Transform drawingRoomSeat03;

    [Header("Guests")]
    [SerializeField] private List<GuestArrivalConfig> guests = new List<GuestArrivalConfig>();

    [Header("Fallback Interaction")]
    [SerializeField] private bool autoHandleGreetingAndCoat = true;
    [SerializeField] private float fallbackGreetingDelaySeconds = 0.75f;
    [SerializeField] private float fallbackCoatDelaySeconds = 0.35f;
    [SerializeField] private float debugFastGuestIntervalSeconds = 1f;

    private readonly List<GuestRuntimeState> guestStates = new List<GuestRuntimeState>();
    private int currentGuestIndex = -1;
    private bool sequenceActive;
    private bool guestFlowActive;
    private bool butlerCarryingCoat;
    private string carriedCoatId;

    public int CurrentGuestIndex => currentGuestIndex;
    public bool ButlerCarryingCoat => butlerCarryingCoat;
    public string CarriedCoatId => carriedCoatId;

    private void Awake()
    {
        ResolveReferences(false);
    }

    public void BeginChapter1(ChapterManager manager)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences();
        ValidateRequiredReferences();
        ResetGuestStates();

        sequenceActive = true;
        guestFlowActive = false;
        currentGuestIndex = -1;
        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;

        if (guestStates.Count == 0)
        {
            Debug.LogWarning("Chapter1ArrivalController guest list is empty. Arrival sequence cannot start until guests are assigned.", this);
            NotifySequenceComplete();
            return;
        }

        ScheduleGuestArrival(0, 0f);
    }

    public void PrepareGuestsForChapterStart()
    {
        ResolveReferences(false);
        ResetGuestStates();
    }

    public void TriggerNextGuest()
    {
        ResolveReferences();

        if (!sequenceActive)
        {
            Debug.LogWarning("Chapter 1 next guest debug trigger ignored because the arrival sequence is not active.", this);
            return;
        }

        if (guestFlowActive)
        {
            Debug.Log("Chapter 1 next guest debug trigger queued: current guest is still in the door flow.", this);
            return;
        }

        int nextGuestIndex = FindNextWaitingGuestIndex();

        if (nextGuestIndex < 0)
        {
            Debug.Log("Chapter 1 next guest debug trigger found no waiting guests.", this);
            return;
        }

        StartGuestArrival(nextGuestIndex);
    }

    public string BuildDebugState()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("current guest index: ");
        builder.Append(currentGuestIndex);
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
            builder.Append(" (");
            builder.Append(guestState.Config.GuestDisplayName);
            builder.Append("): ");
            builder.Append(guestState.State);
            builder.Append(", room=");
            builder.Append(actorState != null ? actorState.CurrentRoomId : "none");
            builder.Append(", available=");
            builder.Append(actorState != null && actorState.IsAvailableInCurrentChapter);
            builder.Append(", visibleByChapter=");
            builder.Append(actorState != null && actorState.IsVisibleByChapterState);
            builder.Append(", interactable=");
            builder.Append(actorState != null && actorState.IsInteractable);
            builder.Append(", seated=");
            builder.Append(guestState.Seated);
            builder.Append(", handled=");
            builder.Append(guestState.Handled);
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
            Debug.LogWarning("Chapter1ArrivalController missing required field: closet reference.", this);
        }

        if (frontDoorArrivalPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: frontDoorArrivalPoint.", this);
        }

        if (butlerDoorSpot == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: butlerDoorSpot.", this);
        }

        if (closetPoint == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: closetPoint.", this);
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

        if (guests == null || guests.Count == 0)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: guest list.", this);
            return;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            GuestArrivalConfig guest = guests[i];

            if (guest == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest list element {i} is missing.", this);
                continue;
            }

            if (guest.GuestObject == null && guest.ActorState == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest '{guest.GuestId}' missing required field: guest GameObject/reference.", this);
            }

            if (guest.GetFrontDoorArrivalPoint(frontDoorArrivalPoint) == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest '{guest.GuestId}' missing required field: front door waypoint.", this);
            }

            if (guest.GetDrawingRoomEntryPoint(drawingRoomEntryPoint) == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest '{guest.GuestId}' missing required field: drawing room entry waypoint.", this);
            }

            if (guest.GetAssignedSeat(GetDefaultSeatForGuest(i)) == null)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest '{guest.GuestId}' missing required field: seat waypoint.", this);
            }

            if (guest.AmbientLines == null || guest.AmbientLines.Count == 0)
            {
                Debug.LogWarning($"Chapter1ArrivalController guest '{guest.GuestId}' should have at least one ambient line.", this);
            }
        }
    }

    private void ResetGuestStates()
    {
        guestStates.Clear();

        if (guests == null)
        {
            return;
        }

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

            GuestRuntimeState runtimeState = new GuestRuntimeState
            {
                Config = config,
                State = GuestArrivalState.Hidden,
                Handled = false,
                CoatTaken = false,
                Seated = false,
                Mover = mover,
                ActorState = actorState
            };

            if (actorState != null)
            {
                actorState.SetCurrentRoom(entryRoomId);
                actorState.SetAvailableInCurrentChapter(false);
                actorState.SetVisibleByChapterState(false);
                actorState.SetInteractable(false);
                actorState.SetSeated(false);
            }

            guestStates.Add(runtimeState);
        }
    }

    private void ScheduleGuestArrival(int guestIndex, float delaySeconds)
    {
        if (eventScheduler == null)
        {
            StartGuestArrival(guestIndex);
            return;
        }

        string eventId = $"chapter_01_arrivals_guest_{guestIndex + 1:00}";
        eventScheduler.ScheduleOneShot(eventId, delaySeconds, () => StartGuestArrival(guestIndex));
    }

    private void StartGuestArrival(int guestIndex)
    {
        if (!sequenceActive ||
            guestFlowActive ||
            guestIndex < 0 ||
            guestIndex >= guestStates.Count)
        {
            return;
        }

        GuestRuntimeState guestState = guestStates[guestIndex];

        if (guestState == null ||
            guestState.Handled ||
            guestState.State != GuestArrivalState.WaitingTurn && guestState.State != GuestArrivalState.Hidden)
        {
            return;
        }

        StartCoroutine(RunGuestArrival(guestIndex));
    }

    private IEnumerator RunGuestArrival(int guestIndex)
    {
        guestFlowActive = true;
        currentGuestIndex = guestIndex;

        GuestRuntimeState guestState = guestStates[guestIndex];
        GuestArrivalConfig config = guestState.Config;
        ActorRoomState actorState = guestState.ActorState;

        SetGuestState(guestState, GuestArrivalState.Arriving);

        EnsureGuestHiddenBeforeArrival(guestState);

        Transform arrivalPoint = config.GetFrontDoorArrivalPoint(frontDoorArrivalPoint);
        PlaceGuestAt(guestState, arrivalPoint, "frontDoorArrivalPoint");

        if (actorState != null)
        {
            actorState.SetCurrentRoom(entryRoomId);
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetInteractable(true);
        }

        SetGuestState(guestState, GuestArrivalState.AwaitingGreeting);
        Debug.Log($"Guest arrival started: {config.GuestDisplayName}", this);

        if (!autoHandleGreetingAndCoat)
        {
            Debug.Log("Guest greeting is waiting for a future player interaction hook. Fallback auto-handle is disabled.", this);
            guestFlowActive = false;
            yield break;
        }

        yield return new WaitForSeconds(GetFallbackDelay(fallbackGreetingDelaySeconds));

        // TODO: Replace this fallback with the final clickable guest dialogue/action when the story interaction UX is ready.
        Debug.Log($"Butler greets {config.GuestDisplayName}.", this);
        LogGuestLine(config, config.GreetingLine);
        SetGuestState(guestState, GuestArrivalState.GreetingComplete);

        yield return new WaitForSeconds(GetFallbackDelay(fallbackCoatDelaySeconds));

        TakeGuestCoat(guestState);
        StoreCarriedCoatInCloset();

        if (actorState != null)
        {
            actorState.SetInteractable(false);
        }

        SetGuestState(guestState, GuestArrivalState.MovingToDrawingRoom);
        Transform entryPoint = config.GetDrawingRoomEntryPoint(drawingRoomEntryPoint);
        yield return MoveGuestTo(guestState, entryPoint, "drawingRoomEntryPoint");

        Transform assignedSeat = config.GetAssignedSeat(GetDefaultSeatForGuest(guestIndex));
        yield return MoveGuestTo(guestState, assignedSeat, "assignedSeat");

        if (actorState != null)
        {
            actorState.SetCurrentRoom(drawingRoomId);
            actorState.SetInteractable(false);
            actorState.SetSeated(true);
        }

        guestState.Seated = true;
        SetGuestState(guestState, GuestArrivalState.Seated);
        DisableGuestMovement(guestState);
        StartAmbientConversation(guestState);
        guestState.Handled = true;
        SetGuestState(guestState, GuestArrivalState.Handled);

        Debug.Log($"Guest handled: {config.GuestDisplayName}", this);
        guestFlowActive = false;

        int nextGuestIndex = guestIndex + 1;

        if (nextGuestIndex < guestStates.Count)
        {
            SetGuestState(guestStates[nextGuestIndex], GuestArrivalState.WaitingTurn);
            ScheduleGuestArrival(nextGuestIndex, GetArrivalInterval(config));
        }
        else
        {
            NotifySequenceComplete();
        }
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

        GameObject guestObject = guestState.Config.ResolveGuestObject();

        if (guestObject != null)
        {
            guestObject.transform.position = target.position;
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
        mover.MoveTo(target);

        while (mover != null && mover.IsMoving)
        {
            yield return null;
        }
    }

    private void TakeGuestCoat(GuestRuntimeState guestState)
    {
        if (guestState == null || guestState.Config == null)
        {
            return;
        }

        string coatId = guestState.Config.CoatId;
        butlerCarryingCoat = true;
        carriedCoatId = coatId;
        guestState.CoatTaken = true;
        SetGuestState(guestState, GuestArrivalState.CoatTaken);
        Debug.Log($"Coat taken from guest: {coatId}", this);
    }

    private void StoreCarriedCoatInCloset()
    {
        if (!butlerCarryingCoat)
        {
            return;
        }

        if (coatCloset == null)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: closet reference. Butler will keep carrying the coat.", this);
            return;
        }

        // TODO: Replace this fallback with the final closet Hang Coat interaction when that UX is ready.
        Debug.Log("Closet Hang Coat action fallback: storing carried coat immediately.", this);
        coatCloset.StoreCoat(carriedCoatId);
        butlerCarryingCoat = false;
        carriedCoatId = string.Empty;
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

        // TODO: Replace this Debug.Log bark with the final speech bubble/dialogue system when it exists.
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

        // TODO: Replace this Debug.Log bark with the final speech bubble/dialogue system when it exists.
        Debug.Log($"{config.GuestDisplayName}: {line}", this);
    }

    private void SetGuestState(GuestRuntimeState guestState, GuestArrivalState nextState)
    {
        if (guestState == null || guestState.State == nextState)
        {
            return;
        }

        guestState.State = nextState;
        string guestId = guestState.Config != null ? guestState.Config.GuestId : "unknown_guest";
        Debug.Log($"Chapter 1 guest state changed: {guestId} -> {nextState}", this);
    }

    private void NotifySequenceComplete()
    {
        sequenceActive = false;
        guestFlowActive = false;

        if (chapterManager != null)
        {
            chapterManager.NotifyArrivalSequenceComplete();
        }
    }

    private int FindNextWaitingGuestIndex()
    {
        for (int i = 0; i < guestStates.Count; i++)
        {
            GuestRuntimeState guestState = guestStates[i];

            if (guestState != null && !guestState.Handled)
            {
                return i;
            }
        }

        return -1;
    }

    private Transform GetDefaultSeatForGuest(int index)
    {
        switch (index)
        {
            case 0:
                return drawingRoomSeat01;
            case 1:
                return drawingRoomSeat02;
            case 2:
                return drawingRoomSeat03;
            default:
                return drawingRoomSeat03;
        }
    }

    private float GetArrivalInterval(GuestArrivalConfig config)
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return Mathf.Max(0f, debugFastGuestIntervalSeconds);
        }

        if (config == null)
        {
            return 15f;
        }

        return config.ArrivalIntervalSeconds;
    }

    private float GetFallbackDelay(float seconds)
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return 0.05f;
        }

        return Mathf.Max(0f, seconds);
    }

    private void ResolveReferences()
    {
        ResolveReferences(true);
    }

    private void ResolveReferences(bool createFallbackCloset)
    {
        if (chapterManager == null)
        {
            chapterManager = GetComponent<ChapterManager>();
        }

        if (eventScheduler == null)
        {
            eventScheduler = GetComponent<ChapterEventScheduler>();
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
        }

        if (playerButlerReference == null && playerMovement != null)
        {
            playerButlerReference = playerMovement.gameObject;
        }

        if (coatCloset == null)
        {
            coatCloset = FindAnyObjectByType<CoatCloset>(FindObjectsInactive.Include);
        }

        if (coatCloset == null && createFallbackCloset)
        {
            Debug.LogWarning("Chapter1ArrivalController missing required field: closet reference. Created runtime fallback CoatCloset_Runtime.", this);
            GameObject closetObject = new GameObject("CoatCloset_Runtime");
            coatCloset = closetObject.AddComponent<CoatCloset>();
        }
    }
}
