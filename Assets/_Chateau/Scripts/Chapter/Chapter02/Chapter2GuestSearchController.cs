using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter2GuestSearchController : MonoBehaviour
{
    private const string PersistentActorRootName = "ChapterActors_Runtime";
    private const string DiagnosticPrefix = "[Ch2ClickDiag]";
    private const string GuestClickDiagnosticPrefix = "[Chapter2GuestClick]";
    private const string ClickTargetName = "Ch2_ClickTarget";
    private const float ClickTargetWidthPadding = 1.15f;
    private const float ClickTargetHeightPadding = 1.15f;
    private static readonly Vector2 MinimumClickTargetSize = new Vector2(1f, 2f);
    private static readonly Vector2 FallbackClickTargetOffset = new Vector2(0f, 1f);
    private static readonly Vector2 FallbackClickTargetSize = new Vector2(1f, 2f);

    private enum GuestConversationResumeStep
    {
        None,
        AwaitMealPromptChoice,
        AwaitMealPreferenceChoice,
        AwaitSmokingPreferenceChoice,
        AwaitFinishConfirmation
    }

    [Serializable]
    public class GuestSearchEntry
    {
        public string guestId;
        public string displayName;
        public ActorRoomState actorState;
        public RoomAnchor hideAnchor;
        public string mealPreference;
        public string smokingPreference;
        public string spiritBottle;
        public bool found;
        public int foundOrder;
    }

    [SerializeField] private List<GuestSearchEntry> guests = new List<GuestSearchEntry>();
    [SerializeField] private string hideAnchorPrefix = "Ch2_Hide_";
    [SerializeField] private string diningSeatPrefix = "Ch2_DiningSeat_";
    [SerializeField] private int foundOrderCounter;
    [SerializeField] private List<string> foundGuestIdsInOrder = new List<string>();

    [Header("Conversation")]
    [SerializeField] private string firstMealOption = "fresh monte genellion de plink";
    [SerializeField] private string secondMealOption = "thyme with Lillums";
    [SerializeField] private string cigarPreference = "cigar";
    [SerializeField] private string pipePreference = "pipe";
    [SerializeField] private string noSmokingPreference = "none, thank you";
    [SerializeField] private float guestExitSeconds = 0.85f;
    [SerializeField] private float guestExitDistance = 0.75f;

    private Chapter2Controller chapter2Controller;
    private RoomNavigationManager navigationManager;
    private GuestSearchEntry activeConversationGuest;
    private GuestConversationResumeStep activeConversationResumeStep;
    private Coroutine roomResumeRoutine;
    private bool subscribedToRoomChanges;
    private readonly HashSet<string> guestsWithFallbackClickBounds = new HashSet<string>();

    public string DiningSeatPrefix => diningSeatPrefix;
    public int GuestCount => CountGuests();
    public int FoundGuestCount => CountFoundGuests();

    public bool AllGuestsFound
    {
        get
        {
            if (guests == null || guests.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < guests.Count; i++)
            {
                if (guests[i] == null || !guests[i].found)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void Initialize(Chapter2Controller controller)
    {
        chapter2Controller = controller;
        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
    }

    private void OnDisable()
    {
        if (roomResumeRoutine != null)
        {
            StopCoroutine(roomResumeRoutine);
            roomResumeRoutine = null;
        }

        UnregisterRoomChangeHandler();
    }

    private void ResolveRoomNavigation()
    {
        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void RegisterRoomChangeHandler()
    {
        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        navigationManager.OnCurrentRoomChanged.AddListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnregisterRoomChangeHandler()
    {
        if (!subscribedToRoomChanges || navigationManager == null)
        {
            subscribedToRoomChanges = false;
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        if (roomResumeRoutine != null)
        {
            StopCoroutine(roomResumeRoutine);
        }

        roomResumeRoutine = StartCoroutine(ResumeActiveConversationAfterRoomChange(roomName));
    }

    private IEnumerator ResumeActiveConversationAfterRoomChange(string roomName)
    {
        yield return null;
        roomResumeRoutine = null;
        TryResumeActiveConversationForRoom(roomName);
    }

    public void BeginSearch()
    {
        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
        AutoDiscoverGuestsIfNeeded();
        AutoAssignHideAnchorsIfNeeded();
        activeConversationGuest = null;
        activeConversationResumeStep = GuestConversationResumeStep.None;
        foundOrderCounter = 0;

        if (foundGuestIdsInOrder == null)
        {
            foundGuestIdsInOrder = new List<string>();
        }

        foundGuestIdsInOrder.Clear();

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            guest.found = false;
            guest.foundOrder = 0;

            if (guest.actorState == null)
            {
                Debug.LogWarning($"Chapter 2 guest search missing ActorRoomState for guest '{guest.guestId}'.", this);
                continue;
            }

            if (guest.hideAnchor == null)
            {
                Debug.LogWarning($"Chapter 2 guest search missing hide anchor for guest '{guest.guestId}'.", this);
                continue;
            }

            EnsureGuestUsesPersistentActorRoot(guest);
            guest.actorState.enabled = true;
            guest.actorState.PlaceAt(guest.hideAnchor.transform);
            guest.actorState.SetCurrentRoom(guest.hideAnchor.RoomId);
            guest.actorState.SetAvailableInCurrentChapter(true);
            guest.actorState.SetVisibleByChapterState(true);
            guest.actorState.SetInteractable(true);
            guest.actorState.SetSeated(false);
            EnsureGuestFindAction(guest);
            guest.actorState.ApplyState();

            if (Application.isPlaying)
            {
                Physics2D.SyncTransforms();
            }
        }
    }

    public void DebugStageAllGuestsFoundForChapter3Skip()
    {
        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
        AutoDiscoverGuestsIfNeeded();
        AutoAssignHideAnchorsIfNeeded();
        activeConversationGuest = null;
        activeConversationResumeStep = GuestConversationResumeStep.None;
        foundOrderCounter = 0;

        if (foundGuestIdsInOrder == null)
        {
            foundGuestIdsInOrder = new List<string>();
        }

        foundGuestIdsInOrder.Clear();

        if (guests == null)
        {
            return;
        }

        guests.Sort(CompareGuestIdentity);

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            foundOrderCounter++;
            guest.found = true;
            guest.foundOrder = foundOrderCounter;
            foundGuestIdsInOrder.Add(GetGuestIdForOrderList(guest));
            FillDefaultPreferences(guest);
            EnsureGuestUsesPersistentActorRoot(guest);
            DisableGuestFindAction(guest);

            if (guest.actorState == null)
            {
                Debug.LogWarning($"Chapter 3 skip missing ActorRoomState for guest '{GetGuestIdForOrderList(guest)}'.", this);
                continue;
            }

            guest.actorState.enabled = true;
        }

        SeatGuestsInDiningRoom(GetFoundGuestsInOrder());
    }

    public void AutoDiscoverGuestsIfNeeded()
    {
        if (guests == null)
        {
            guests = new List<GuestSearchEntry>();
        }

        if (guests.Count > 0)
        {
            return;
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actorState = actorStates[i];

            if (!IsLikelyChapterGuest(actorState))
            {
                continue;
            }

            guests.Add(new GuestSearchEntry
            {
                guestId = actorState.ActorId,
                displayName = actorState.gameObject.name,
                actorState = actorState
            });
        }
    }

    public void AutoAssignHideAnchorsIfNeeded()
    {
        if (guests == null || guests.Count == 0)
        {
            return;
        }

        RoomAnchor[] hideAnchors = FindHideAnchors();
        int anchorIndex = 0;
        guests.Sort(CompareGuestIdentity);

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null || guest.hideAnchor != null)
            {
                continue;
            }

            if (anchorIndex >= hideAnchors.Length)
            {
                Debug.LogWarning($"Chapter 2 guest search has no hide anchor for guest '{guest.guestId}'.", this);
                continue;
            }

            guest.hideAnchor = hideAnchors[anchorIndex];
            anchorIndex++;
        }
    }

    public bool MarkGuestFound(string guestId)
    {
        GuestSearchEntry guest = FindGuest(guestId);

        if (guest == null)
        {
            return false;
        }

        if (guest.found)
        {
            return true;
        }

        if (activeConversationGuest == guest)
        {
            activeConversationGuest = null;
            activeConversationResumeStep = GuestConversationResumeStep.None;
        }

        foundOrderCounter++;
        guest.found = true;
        guest.foundOrder = foundOrderCounter;

        if (foundGuestIdsInOrder == null)
        {
            foundGuestIdsInOrder = new List<string>();
        }

        foundGuestIdsInOrder.Add(GetGuestIdForOrderList(guest));
        FillDefaultPreferences(guest);

        if (guest.actorState != null)
        {
            guest.actorState.SetInteractable(false);
            guest.actorState.ApplyState();
        }

        DisableGuestFindAction(guest);
        LogGuestFound(guest);
        SendGuestToDiningRoomAfterConversation(guest);

        if (chapter2Controller != null)
        {
            chapter2Controller.HandleGuestSearchProgressChanged();
        }

        if (AllGuestsFound && chapter2Controller != null)
        {
            chapter2Controller.HandleAllGuestsFound();
        }

        return true;
    }

    public bool TryStartGuestConversation(string guestId)
    {
        GuestSearchEntry guest = FindGuest(guestId);

        if (guest == null)
        {
            LogTryStartGuestConversationDiagnostic("rejected", guestId, null, "guest-not-found");
            return false;
        }

        if (guest.found)
        {
            LogTryStartGuestConversationDiagnostic("rejected", guestId, guest, "guest-already-found");
            return false;
        }

        if (chapter2Controller != null && !chapter2Controller.IsGuestSearchActive)
        {
            LogTryStartGuestConversationDiagnostic("rejected", guestId, guest, "search-inactive");
            return false;
        }

        if (activeConversationGuest != null && activeConversationGuest != guest)
        {
            LogTryStartGuestConversationDiagnostic("rejected", guestId, guest, "active-conversation-blocks-guest");
            return false;
        }

        LogTryStartGuestConversationDiagnostic("accepted", guestId, guest, "accepted");
        activeConversationGuest = guest;

        if (chapter2Controller != null)
        {
            chapter2Controller.SetGuestConversationInputEnabled(false);
        }

        if (TryShowActiveConversationResumeState(guest))
        {
            return true;
        }

        ShowDinnerAnnouncement(guest);
        return true;
    }

    private void LogTryStartGuestConversationDiagnostic(string result, string requestedGuestId, GuestSearchEntry guest, string reason)
    {
        ActorRoomState actorState = guest != null ? guest.actorState : null;
        string actorCurrentRoom = actorState != null && !string.IsNullOrWhiteSpace(actorState.CurrentRoomId)
            ? actorState.CurrentRoomId
            : "<none>";
        string activeConversation = activeConversationGuest != null
            ? $"{GetGuestIdForOrderList(activeConversationGuest)}/{GetGuestDisplayName(activeConversationGuest)}"
            : "<none>";
        string phase = chapter2Controller != null
            ? chapter2Controller.CurrentPhase.ToString()
            : "<none>";
        bool isGuestSearchActive = chapter2Controller != null && chapter2Controller.IsGuestSearchActive;
        string actorVisible = actorState != null ? actorState.IsVisibleInCurrentRoom.ToString() : "<none>";
        string actorInteractable = actorState != null ? actorState.IsInteractable.ToString() : "<none>";
        string guestFound = guest != null ? guest.found.ToString() : "<none>";

        Debug.Log(
            $"{DiagnosticPrefix} GuestSearch TryStartGuestConversation {result} frame={Time.frameCount} " +
            $"requestedGuest={FormatDiagnosticValue(requestedGuestId)} reason={reason} " +
            $"phase={phase} isGuestSearchActive={isGuestSearchActive} " +
            $"activeConversationGuest={activeConversation} guestFound={guestFound} " +
            $"actorCurrentRoom={actorCurrentRoom} actorVisibleInCurrentRoom={actorVisible} " +
            $"actorInteractable={actorInteractable}",
            this);
    }

    public List<string> GetFoundGuestIdsInOrder()
    {
        if (foundGuestIdsInOrder != null && foundGuestIdsInOrder.Count > 0)
        {
            return new List<string>(foundGuestIdsInOrder);
        }

        List<GuestSearchEntry> foundGuests = GetFoundGuestsInOrder();
        List<string> foundGuestIds = new List<string>(foundGuests.Count);

        for (int i = 0; i < foundGuests.Count; i++)
        {
            string guestId = foundGuests[i].guestId;
            foundGuestIds.Add(string.IsNullOrWhiteSpace(guestId) ? foundGuests[i].displayName : guestId);
        }

        return foundGuestIds;
    }

    public List<string> GetFoundGuestDisplayNamesInOrder()
    {
        List<GuestSearchEntry> foundGuests = GetFoundGuestsInOrder();
        List<string> displayNames = new List<string>(foundGuests.Count);

        for (int i = 0; i < foundGuests.Count; i++)
        {
            displayNames.Add(GetGuestDisplayName(foundGuests[i]));
        }

        return displayNames;
    }

    public List<ActorRoomState> GetFoundActorsInOrder()
    {
        List<GuestSearchEntry> foundGuests = GetFoundGuestsInOrder();
        List<ActorRoomState> foundActors = new List<ActorRoomState>(foundGuests.Count);

        for (int i = 0; i < foundGuests.Count; i++)
        {
            if (foundGuests[i].actorState != null)
            {
                foundActors.Add(foundGuests[i].actorState);
            }
        }

        return foundActors;
    }

    public List<ActorRoomState> GetGuestActorsInIdentityOrder()
    {
        AutoDiscoverGuestsIfNeeded();

        List<ActorRoomState> orderedActors = new List<ActorRoomState>();

        if (guests == null)
        {
            return orderedActors;
        }

        guests.Sort(CompareGuestIdentity);

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest != null && guest.actorState != null)
            {
                orderedActors.Add(guest.actorState);
            }
        }

        return orderedActors;
    }

    public void SeatFoundGuestsInDiningRoom()
    {
        SeatGuestsInDiningRoom(GetFoundGuestsInOrder());
    }

    public void SeatGuestsInDiningRoom()
    {
        SeatGuestsInDiningRoom(GetGuestsInDiningSeatOrder());
    }

    public void PrepareGuestsForDiningTransfer()
    {
        if (guests == null)
        {
            return;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null || guest.actorState == null)
            {
                continue;
            }

            EnsureGuestUsesPersistentActorRoot(guest);
            DisableGuestFindAction(guest);
            HideGuestForDiningRoomTransfer(guest);
        }
    }

    private void SeatGuestsInDiningRoom(List<GuestSearchEntry> guestsToSeat)
    {
        RoomAnchor[] diningSeats = FindDiningSeatAnchors();

        for (int i = 0; i < guestsToSeat.Count; i++)
        {
            GuestSearchEntry guest = guestsToSeat[i];

            if (guest == null || guest.actorState == null)
            {
                continue;
            }

            if (i >= diningSeats.Length || diningSeats[i] == null)
            {
                Debug.LogWarning($"Chapter 2 guest search has no dining seat for guest '{GetGuestIdForOrderList(guest)}'.", this);
                continue;
            }

            RoomAnchor diningSeat = diningSeats[i];
            EnsureGuestUsesPersistentActorRoot(guest);
            DisableGuestFindAction(guest);

            guest.actorState.enabled = true;
            HideGuestForDiningRoomTransfer(guest);
            guest.actorState.SetCurrentRoom(diningSeat.RoomId);
            guest.actorState.PlaceAt(diningSeat.transform);
            guest.actorState.SetAvailableInCurrentChapter(true);
            guest.actorState.SetVisibleByChapterState(true);
            guest.actorState.SetInteractable(false);
            guest.actorState.SetSeated(true);
            guest.actorState.ApplyState();
        }
    }

    private void HideGuestForDiningRoomTransfer(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null)
        {
            return;
        }

        guest.actorState.SetInteractable(false);
        guest.actorState.SetVisibleByChapterState(false);
        guest.actorState.ApplyState();
    }

    private void EnsureGuestFindAction(GuestSearchEntry guest)
    {
        if (!Application.isPlaying || guest == null || guest.actorState == null || guest.actorState.gameObject == null)
        {
            return;
        }

        GameObject actorObject = guest.actorState.gameObject;
        Chapter2GuestFindAction clickTargetAction = EnsureRuntimeClickTarget(actorObject, GetGuestIdForOrderList(guest));

        if (clickTargetAction == null)
        {
            return;
        }

        DisableCompetingGuestFindActions(actorObject, clickTargetAction);
    }

    private Chapter2GuestFindAction EnsureRuntimeClickTarget(GameObject actorObject, string guestId)
    {
        if (actorObject == null)
        {
            return null;
        }

        Transform targetTransform = FindClickTargetTransform(actorObject.transform);
        bool createdTarget = false;

        if (targetTransform == null)
        {
            GameObject targetObject = new GameObject(ClickTargetName);
            targetTransform = targetObject.transform;
            createdTarget = true;
        }

        targetTransform.name = ClickTargetName;
        targetTransform.SetParent(actorObject.transform, false);
        targetTransform.localPosition = Vector3.zero;
        targetTransform.localRotation = Quaternion.identity;
        targetTransform.localScale = Vector3.one;
        targetTransform.gameObject.SetActive(true);

        BoxCollider2D clickCollider = targetTransform.GetComponent<BoxCollider2D>();

        if (clickCollider == null)
        {
            clickCollider = targetTransform.gameObject.AddComponent<BoxCollider2D>();
            createdTarget = true;
        }

        clickCollider.isTrigger = true;

        Chapter2GuestFindAction findAction = targetTransform.GetComponent<Chapter2GuestFindAction>();

        if (findAction == null)
        {
            findAction = targetTransform.gameObject.AddComponent<Chapter2GuestFindAction>();
        }

        findAction.Initialize(guestId, this);
        ResizeRuntimeClickTarget(actorObject, targetTransform, clickCollider, guestId);

        if (createdTarget)
        {
            Debug.Log($"{GuestClickDiagnosticPrefix} Created {ClickTargetName} for guest '{FormatDiagnosticValue(guestId)}' on actor '{actorObject.name}'.", this);
        }

        return findAction;
    }

    private void ResizeRuntimeClickTarget(GameObject actorObject, Transform targetTransform, BoxCollider2D clickCollider, string guestId)
    {
        Vector2 nextOffset = FallbackClickTargetOffset;
        Vector2 nextSize = FallbackClickTargetSize;
        bool usedRendererBounds = TryGetGuestRendererBounds(actorObject, targetTransform, out Bounds rendererBounds);

        if (usedRendererBounds)
        {
            nextOffset = targetTransform.InverseTransformPoint(rendererBounds.center);
            nextSize = GetLocalBoundsSize(targetTransform, rendererBounds);
            nextSize = new Vector2(
                Mathf.Max(MinimumClickTargetSize.x, nextSize.x * ClickTargetWidthPadding),
                Mathf.Max(MinimumClickTargetSize.y, nextSize.y * ClickTargetHeightPadding));
        }
        else
        {
            LogFallbackClickBoundsOnce(actorObject, guestId);
        }

        bool changed =
            !Approximately(clickCollider.offset, nextOffset) ||
            !Approximately(clickCollider.size, nextSize);

        clickCollider.offset = nextOffset;
        clickCollider.size = nextSize;

        if (changed)
        {
            Debug.Log(
                $"{GuestClickDiagnosticPrefix} Resized {ClickTargetName} for guest '{FormatDiagnosticValue(guestId)}' " +
                $"offset={FormatDiagnosticVector(nextOffset)} size={FormatDiagnosticVector(nextSize)} " +
                $"source={(usedRendererBounds ? "renderer-bounds" : "fallback")}.",
                this);
        }
    }

    private static Transform FindClickTargetTransform(Transform actorTransform)
    {
        if (actorTransform == null)
        {
            return null;
        }

        Transform[] childTransforms = actorTransform.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform childTransform = childTransforms[i];

            if (childTransform != null &&
                childTransform != actorTransform &&
                childTransform.name == ClickTargetName)
            {
                return childTransform;
            }
        }

        return null;
    }

    private static bool TryGetGuestRendererBounds(GameObject actorObject, Transform clickTarget, out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (actorObject == null)
        {
            return false;
        }

        HashSet<Renderer> visitedRenderers = new HashSet<Renderer>();
        SpriteRenderer[] spriteRenderers = actorObject.GetComponentsInChildren<SpriteRenderer>(true);
        Renderer[] renderers = actorObject.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            hasBounds |= TryEncapsulateRendererBounds(spriteRenderers[i], clickTarget, visitedRenderers, ref combinedBounds, hasBounds);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            hasBounds |= TryEncapsulateRendererBounds(renderers[i], clickTarget, visitedRenderers, ref combinedBounds, hasBounds);
        }

        return hasBounds && IsUsableBounds(combinedBounds);
    }

    private static bool TryEncapsulateRendererBounds(Renderer renderer, Transform clickTarget, HashSet<Renderer> visitedRenderers, ref Bounds combinedBounds, bool hasBounds)
    {
        if (renderer == null ||
            visitedRenderers.Contains(renderer) ||
            IsUnderClickTarget(renderer.transform, clickTarget))
        {
            return false;
        }

        visitedRenderers.Add(renderer);
        Bounds rendererBounds = renderer.bounds;

        if (!IsUsableBounds(rendererBounds))
        {
            return false;
        }

        if (!hasBounds)
        {
            combinedBounds = rendererBounds;
        }
        else
        {
            combinedBounds.Encapsulate(rendererBounds);
        }

        return true;
    }

    private static bool IsUnderClickTarget(Transform candidate, Transform clickTarget)
    {
        return candidate != null && clickTarget != null &&
            (candidate == clickTarget || candidate.IsChildOf(clickTarget));
    }

    private static bool IsUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return size.x > 0.001f && size.y > 0.001f;
    }

    private static Vector2 GetLocalBoundsSize(Transform targetTransform, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] worldCorners =
        {
            new Vector3(min.x, min.y, worldBounds.center.z),
            new Vector3(min.x, max.y, worldBounds.center.z),
            new Vector3(max.x, min.y, worldBounds.center.z),
            new Vector3(max.x, max.y, worldBounds.center.z)
        };

        Vector2 localMin = targetTransform.InverseTransformPoint(worldCorners[0]);
        Vector2 localMax = localMin;

        for (int i = 1; i < worldCorners.Length; i++)
        {
            Vector2 localPoint = targetTransform.InverseTransformPoint(worldCorners[i]);
            localMin = Vector2.Min(localMin, localPoint);
            localMax = Vector2.Max(localMax, localPoint);
        }

        return localMax - localMin;
    }

    private void LogFallbackClickBoundsOnce(GameObject actorObject, string guestId)
    {
        string key = !string.IsNullOrWhiteSpace(guestId)
            ? guestId.Trim()
            : actorObject != null ? actorObject.name : string.Empty;

        if (!guestsWithFallbackClickBounds.Add(key))
        {
            return;
        }

        Debug.LogWarning(
            $"{GuestClickDiagnosticPrefix} Using fallback {ClickTargetName} bounds for guest '{FormatDiagnosticValue(guestId)}'.",
            this);
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);
    }

    private static string FormatDiagnosticVector(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    private static void DisableCompetingGuestFindActions(GameObject actorObject, Chapter2GuestFindAction activeAction)
    {
        if (actorObject == null || activeAction == null)
        {
            return;
        }

        Chapter2GuestFindAction[] findActions = actorObject.GetComponentsInChildren<Chapter2GuestFindAction>(true);

        for (int i = 0; i < findActions.Length; i++)
        {
            Chapter2GuestFindAction findAction = findActions[i];

            if (findAction == null || findAction == activeAction)
            {
                continue;
            }

            findAction.SetAvailable(false);
            findAction.enabled = false;
        }
    }

    private void FillDefaultPreferences(GuestSearchEntry guest)
    {
        if (guest == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(guest.mealPreference))
        {
            guest.mealPreference = guest.foundOrder % 2 == 1
                ? firstMealOption
                : secondMealOption;
        }

        if (string.IsNullOrWhiteSpace(guest.smokingPreference))
        {
            int smokingIndex = (guest.foundOrder - 1) % 3;

            if (smokingIndex == 0)
            {
                guest.smokingPreference = pipePreference;
            }
            else if (smokingIndex == 1)
            {
                guest.smokingPreference = cigarPreference;
            }
            else
            {
                guest.smokingPreference = noSmokingPreference;
            }
        }

        if (string.IsNullOrWhiteSpace(guest.spiritBottle))
        {
            guest.spiritBottle = $"{GetGuestDisplayName(guest)}'s bottle of spirits";
        }
    }

    private void DisableGuestFindAction(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null || guest.actorState.gameObject == null)
        {
            return;
        }

        Chapter2GuestFindAction[] findActions = guest.actorState.gameObject.GetComponentsInChildren<Chapter2GuestFindAction>(true);

        for (int i = 0; i < findActions.Length; i++)
        {
            Chapter2GuestFindAction findAction = findActions[i];

            if (findAction == null)
            {
                continue;
            }

            findAction.SetAvailable(false);
            findAction.enabled = false;
        }
    }

    private void LogGuestFound(GuestSearchEntry guest)
    {
        string guestName = GetGuestDisplayName(guest);

        Debug.Log(
            $"Butler to {guestName}: Dinner will be served at 7:00 PM sharp...\n" +
            $"Meal preference: {guest.mealPreference}\n" +
            $"Smoking preference: {guest.smokingPreference}\n" +
            $"Spirits: {guest.spiritBottle}",
            this);
    }

    private void SendGuestToDiningRoomAfterConversation(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null)
        {
            return;
        }

        if (Application.isPlaying && isActiveAndEnabled)
        {
            StartCoroutine(RunGuestExitToDiningRoomRoutine(guest));
            return;
        }

        StageGuestForDiningRoomReveal(guest);
    }

    private IEnumerator RunGuestExitToDiningRoomRoutine(GuestSearchEntry guest)
    {
        ActorRoomState actorState = guest != null ? guest.actorState : null;

        if (actorState == null || actorState.gameObject == null)
        {
            yield break;
        }

        Transform actorTransform = actorState.gameObject.transform;
        Vector3 startPosition = actorTransform.position;
        Vector3 exitPosition = startPosition + new Vector3(guestExitDistance, 0f, 0f);
        float duration = Mathf.Max(0.01f, guestExitSeconds);
        float elapsed = 0f;

        while (elapsed < duration && actorState != null && actorState.IsVisibleInCurrentRoom)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actorTransform.position = Vector3.Lerp(startPosition, exitPosition, t);
            yield return null;
        }

        StageGuestForDiningRoomReveal(guest);
    }

    private void StageGuestForDiningRoomReveal(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null)
        {
            return;
        }

        string targetRoom = chapter2Controller != null && !string.IsNullOrWhiteSpace(chapter2Controller.DiningRoomId)
            ? chapter2Controller.DiningRoomId
            : "Dining Room";

        guest.actorState.SetInteractable(false);
        guest.actorState.SetCurrentRoom(targetRoom);
        guest.actorState.SetAvailableInCurrentChapter(true);
        guest.actorState.SetVisibleByChapterState(false);
        guest.actorState.SetSeated(true);
        guest.actorState.ApplyState();
    }

    private void ShowDinnerAnnouncement(GuestSearchEntry guest)
    {
        if (chapter2Controller == null)
        {
            MarkGuestFound(GetGuestIdForOrderList(guest));
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitMealPromptChoice);
        string guestName = GetGuestDisplayName(guest);
        string line = $"I have found you, {guestName}. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?";
        chapter2Controller.ShowGuestConversationWithSubtitle(
            GetChapter2GuestSubtitleLineId("SUB_CH02_BUTLER_FOUND_G", guest),
            "Butler",
            line,
            "Ask meal preference",
            () => ShowMealPreferenceQuestion(guest));
    }

    private void ShowMealPreferenceQuestion(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitMealPreferenceChoice);
        const string line = "For supper, shall I put you down for the fresh monte genellion de plink, or thyme with Lillums?";
        chapter2Controller.ShowGuestConversationWithSubtitle(
            "SUB_CH02_BUTLER_MEAL_ASK_001",
            "Butler",
            line,
            firstMealOption,
            () => ChooseMealPreference(guest, firstMealOption),
            secondMealOption,
            () => ChooseMealPreference(guest, secondMealOption));
    }

    private void ChooseMealPreference(GuestSearchEntry guest, string preference)
    {
        if (!IsActiveConversationGuest(guest))
        {
            return;
        }

        guest.mealPreference = preference;
        ShowSmokingPreferenceQuestion(guest);
    }

    private void ShowSmokingPreferenceQuestion(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitSmokingPreferenceChoice);
        const string line = "After dinner, shall I prepare a cigar, a pipe, or no smoke at all?";
        chapter2Controller.ShowGuestConversationWithSubtitle(
            "SUB_CH02_BUTLER_SMOKE_ASK_001",
            "Butler",
            line,
            "Cigar",
            () => ChooseSmokingPreference(guest, cigarPreference),
            "Pipe",
            () => ChooseSmokingPreference(guest, pipePreference),
            "No smoke",
            () => ChooseSmokingPreference(guest, noSmokingPreference));
    }

    private void ChooseSmokingPreference(GuestSearchEntry guest, string preference)
    {
        if (!IsActiveConversationGuest(guest))
        {
            return;
        }

        guest.smokingPreference = preference;
        guest.spiritBottle = $"{GetGuestDisplayName(guest)}'s bottle of spirits";
        ShowConversationComplete(guest);
    }

    private void ShowConversationComplete(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        string guestName = GetGuestDisplayName(guest);
        const string line = "Very good. I shall present myself in the Dining Room and recover what dignity remains to us.";
        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitFinishConfirmation);
        chapter2Controller.ShowGuestConversationWithSubtitle(
            GetChapter2GuestSubtitleLineId("SUB_CH02_G", guest, "_FINAL_ACK_001"),
            guestName,
            line,
            "Very good",
            () => FinishGuestConversation(guest));
    }

    private void FinishGuestConversation(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest))
        {
            return;
        }

        activeConversationGuest = null;
        activeConversationResumeStep = GuestConversationResumeStep.None;

        if (chapter2Controller != null)
        {
            chapter2Controller.ClearGuestConversation();
            chapter2Controller.SetGuestConversationInputEnabled(true);
        }

        MarkGuestFound(GetGuestIdForOrderList(guest));
    }

    private bool IsActiveConversationGuest(GuestSearchEntry guest)
    {
        return guest != null && activeConversationGuest == guest && !guest.found;
    }

    private void SetActiveConversationResumeStep(GuestSearchEntry guest, GuestConversationResumeStep resumeStep)
    {
        if (!IsActiveConversationGuest(guest))
        {
            return;
        }

        activeConversationResumeStep = resumeStep;
    }

    private bool TryResumeActiveConversationForRoom(string roomName)
    {
        if (activeConversationGuest == null ||
            activeConversationResumeStep == GuestConversationResumeStep.None ||
            chapter2Controller == null ||
            !chapter2Controller.IsGuestSearchActive ||
            activeConversationGuest.found)
        {
            return false;
        }

        ActorRoomState actorState = activeConversationGuest.actorState;

        if (actorState == null ||
            !actorState.IsAvailableInCurrentChapter ||
            !actorState.IsVisibleByChapterState ||
            !SameRoom(actorState.CurrentRoomId, roomName))
        {
            return false;
        }

        chapter2Controller.SetGuestConversationInputEnabled(false);
        return TryShowActiveConversationResumeState(activeConversationGuest);
    }

    private bool TryShowActiveConversationResumeState(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) ||
            activeConversationResumeStep == GuestConversationResumeStep.None ||
            chapter2Controller == null)
        {
            return false;
        }

        switch (activeConversationResumeStep)
        {
            case GuestConversationResumeStep.AwaitMealPromptChoice:
                chapter2Controller.ShowGuestConversation(
                    "Butler",
                    string.Empty,
                    "Ask meal preference",
                    () => ShowMealPreferenceQuestion(guest));
                return true;

            case GuestConversationResumeStep.AwaitMealPreferenceChoice:
                chapter2Controller.ShowGuestConversation(
                    "Butler",
                    string.Empty,
                    firstMealOption,
                    () => ChooseMealPreference(guest, firstMealOption),
                    secondMealOption,
                    () => ChooseMealPreference(guest, secondMealOption));
                return true;

            case GuestConversationResumeStep.AwaitSmokingPreferenceChoice:
                chapter2Controller.ShowGuestConversation(
                    "Butler",
                    string.Empty,
                    "Cigar",
                    () => ChooseSmokingPreference(guest, cigarPreference),
                    "Pipe",
                    () => ChooseSmokingPreference(guest, pipePreference),
                    "No smoke",
                    () => ChooseSmokingPreference(guest, noSmokingPreference));
                return true;

            case GuestConversationResumeStep.AwaitFinishConfirmation:
                chapter2Controller.ShowGuestConversation(
                    GetGuestDisplayName(guest),
                    string.Empty,
                    "Very good",
                    () => FinishGuestConversation(guest));
                return true;
        }

        return false;
    }

    private void EnsureGuestUsesPersistentActorRoot(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null || guest.actorState.gameObject == null)
        {
            return;
        }

        GameObject actorObject = guest.actorState.gameObject;
        Transform actorTransform = actorObject.transform;

        if (actorTransform.GetComponentInParent<RoomContentGroup>(true) == null)
        {
            return;
        }

        RectTransform rectTransform = actorTransform as RectTransform;

        if (rectTransform != null && actorTransform.GetComponentInParent<Canvas>(true) != null)
        {
            Debug.LogWarning($"Chapter 2 guest search left UI guest '{actorObject.name}' under its Canvas instead of reparenting out of RoomContentGroup.", this);
            return;
        }

        Transform persistentActorRoot = GetOrCreatePersistentActorRoot();
        actorTransform.SetParent(persistentActorRoot, true);
    }

    private Transform GetOrCreatePersistentActorRoot()
    {
        GameObject rootObject = GameObject.Find(PersistentActorRootName);

        if (rootObject == null)
        {
            rootObject = new GameObject(PersistentActorRootName);
        }

        if (rootObject.transform.GetComponentInParent<RoomContentGroup>(true) != null)
        {
            rootObject.transform.SetParent(null, true);
        }

        return rootObject.transform;
    }

    private GuestSearchEntry FindGuest(string guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId) || guests == null)
        {
            return null;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            if (SameId(guest.guestId, guestId) ||
                SameId(guest.displayName, guestId) ||
                (guest.actorState != null && SameId(guest.actorState.ActorId, guestId)) ||
                (guest.actorState != null && SameId(guest.actorState.gameObject.name, guestId)))
            {
                return guest;
            }
        }

        return null;
    }

    private List<GuestSearchEntry> GetFoundGuestsInOrder()
    {
        List<GuestSearchEntry> foundGuests = new List<GuestSearchEntry>();

        if (guests == null)
        {
            return foundGuests;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null && guests[i].found)
            {
                foundGuests.Add(guests[i]);
            }
        }

        foundGuests.Sort(CompareFoundOrder);
        return foundGuests;
    }

    private List<GuestSearchEntry> GetGuestsInDiningSeatOrder()
    {
        List<GuestSearchEntry> orderedGuests = GetFoundGuestsInOrder();

        if (guests == null)
        {
            return orderedGuests;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest != null && !orderedGuests.Contains(guest))
            {
                orderedGuests.Add(guest);
            }
        }

        return orderedGuests;
    }

    private RoomAnchor[] FindHideAnchors()
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);
        List<RoomAnchor> hideAnchors = new List<RoomAnchor>();

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (StartsWithPrefix(anchor.name, hideAnchorPrefix) ||
                StartsWithPrefix(anchor.AnchorId, hideAnchorPrefix))
            {
                hideAnchors.Add(anchor);
            }
        }

        hideAnchors.Sort(CompareAnchorName);
        return hideAnchors.ToArray();
    }

    private RoomAnchor[] FindDiningSeatAnchors()
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);
        List<RoomAnchor> diningSeats = new List<RoomAnchor>();

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (StartsWithPrefix(anchor.name, diningSeatPrefix) ||
                StartsWithPrefix(anchor.AnchorId, diningSeatPrefix))
            {
                diningSeats.Add(anchor);
            }
        }

        diningSeats.Sort(CompareAnchorName);
        return diningSeats.ToArray();
    }

    private static string GetGuestIdForOrderList(GuestSearchEntry guest)
    {
        if (guest == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(guest.guestId))
        {
            return guest.guestId.Trim();
        }

        if (guest.actorState != null && !string.IsNullOrWhiteSpace(guest.actorState.ActorId))
        {
            return guest.actorState.ActorId.Trim();
        }

        return GetGuestDisplayName(guest);
    }

    private string GetChapter2GuestSubtitleLineId(string prefix, GuestSearchEntry guest, string suffix = "")
    {
        int guestNumber = Mathf.Clamp(GetChapter2GuestSubtitleNumber(guest), 1, 99);
        return $"{prefix}{guestNumber:00}{suffix}";
    }

    private int GetChapter2GuestSubtitleNumber(GuestSearchEntry guest)
    {
        if (guest != null && guests != null)
        {
            int rosterIndex = guests.IndexOf(guest);

            if (rosterIndex >= 0)
            {
                return rosterIndex + 1;
            }
        }

        if (TryExtractTrailingGuestNumber(guest != null ? guest.guestId : null, out int guestIdNumber))
        {
            return guestIdNumber;
        }

        if (guest != null && guest.actorState != null && TryExtractTrailingGuestNumber(guest.actorState.ActorId, out int actorIdNumber))
        {
            return actorIdNumber;
        }

        return guest != null && guest.foundOrder > 0 ? guest.foundOrder : 1;
    }

    private static bool TryExtractTrailingGuestNumber(string value, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string cleanValue = value.Trim();
        int end = cleanValue.Length - 1;

        while (end >= 0 && char.IsDigit(cleanValue[end]))
        {
            end--;
        }

        int start = end + 1;

        if (start >= cleanValue.Length)
        {
            return false;
        }

        return int.TryParse(cleanValue.Substring(start), out guestNumber);
    }

    private static string GetGuestDisplayName(GuestSearchEntry guest)
    {
        if (guest == null)
        {
            return "Guest";
        }

        if (!string.IsNullOrWhiteSpace(guest.displayName))
        {
            return guest.displayName.Trim();
        }

        if (guest.actorState != null && guest.actorState.gameObject != null)
        {
            return guest.actorState.gameObject.name;
        }

        return string.IsNullOrWhiteSpace(guest.guestId) ? "Guest" : guest.guestId.Trim();
    }

    private static bool IsLikelyChapterGuest(ActorRoomState actorState)
    {
        if (actorState == null)
        {
            return false;
        }

        string actorId = actorState.ActorId;
        string objectName = actorState.gameObject != null ? actorState.gameObject.name : string.Empty;

        if (ContainsAny(actorId, "Player", "Monster") ||
            ContainsAny(objectName, "Player", "Monster"))
        {
            return false;
        }

        return ContainsAny(actorId, "Guest") || ContainsAny(objectName, "Guest");
    }

    private static int CompareAnchorName(RoomAnchor left, RoomAnchor right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareFoundOrder(GuestSearchEntry left, GuestSearchEntry right)
    {
        int leftOrder = left != null ? left.foundOrder : int.MaxValue;
        int rightOrder = right != null ? right.foundOrder : int.MaxValue;
        return leftOrder.CompareTo(rightOrder);
    }

    private static int CompareGuestIdentity(GuestSearchEntry left, GuestSearchEntry right)
    {
        return string.Compare(GetGuestIdForOrderList(left), GetGuestIdForOrderList(right), StringComparison.OrdinalIgnoreCase);
    }

    private int CountGuests()
    {
        if (guests == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountFoundGuests()
    {
        if (guests == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null && guests[i].found)
            {
                count++;
            }
        }

        return count;
    }

    private static bool StartsWithPrefix(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(prefix) &&
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameRoom(string left, string right)
    {
        string cleanLeft = string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim();
        string cleanRight = string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim();
        return string.Equals(cleanLeft, cleanRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDiagnosticValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(fragments[i]) &&
                value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
