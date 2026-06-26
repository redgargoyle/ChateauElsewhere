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
        AwaitFoundReply,
        AwaitPreferencePrompt,
        AwaitMealReply,
        AwaitSmokeReply,
        AwaitSpiritsReply,
        AwaitSendToDiningPrompt,
        AwaitExitToDiningCompletion
    }

    private sealed class HiddenGuestConversationSpec
    {
        public readonly int GuestNumber;
        public readonly string DisplayName;
        public readonly string HideAnchorId;
        public readonly string FoundStartLineId;
        public readonly string FoundStartText;
        public readonly string ButlerFoundLineId;
        public readonly string ButlerFoundText;
        public readonly string FoundReplyLineId;
        public readonly string FoundReplyText;
        public readonly string MealReplyLineId;
        public readonly string MealReplyText;
        public readonly string FixedMealPreference;
        public readonly string SmokeReplyLineId;
        public readonly string SmokeReplyText;
        public readonly string FixedSmokingPreference;
        public readonly string SpiritsReplyLineId;
        public readonly string SpiritsReplyText;
        public readonly string FixedSpiritBottle;
        public readonly string ExitToDiningLineId;
        public readonly string ExitToDiningText;

        public HiddenGuestConversationSpec(
            int guestNumber,
            string displayName,
            string hideAnchorId,
            string foundStartLineId,
            string foundStartText,
            string butlerFoundLineId,
            string butlerFoundText,
            string foundReplyLineId,
            string foundReplyText,
            string mealReplyLineId,
            string mealReplyText,
            string fixedMealPreference,
            string smokeReplyLineId,
            string smokeReplyText,
            string fixedSmokingPreference,
            string spiritsReplyLineId,
            string spiritsReplyText,
            string fixedSpiritBottle,
            string exitToDiningLineId,
            string exitToDiningText)
        {
            GuestNumber = guestNumber;
            DisplayName = displayName;
            HideAnchorId = hideAnchorId;
            FoundStartLineId = foundStartLineId;
            FoundStartText = foundStartText;
            ButlerFoundLineId = butlerFoundLineId;
            ButlerFoundText = butlerFoundText;
            FoundReplyLineId = foundReplyLineId;
            FoundReplyText = foundReplyText;
            MealReplyLineId = mealReplyLineId;
            MealReplyText = mealReplyText;
            FixedMealPreference = fixedMealPreference;
            SmokeReplyLineId = smokeReplyLineId;
            SmokeReplyText = smokeReplyText;
            FixedSmokingPreference = fixedSmokingPreference;
            SpiritsReplyLineId = spiritsReplyLineId;
            SpiritsReplyText = spiritsReplyText;
            FixedSpiritBottle = fixedSpiritBottle;
            ExitToDiningLineId = exitToDiningLineId;
            ExitToDiningText = exitToDiningText;
        }
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
    [SerializeField] private float guestExitSeconds = 0.85f;
    [SerializeField] private float guestExitDistance = 0.75f;

    private const string ButlerSpeakerName = "Butler";
    private const string MealPlinkPreference = "fresh monte genellion de plink";
    private const string MealThymePreference = "thyme with Lillums";
    private const string SmokeCigarPreference = "cigar";
    private const string SmokePipePreference = "pipe";
    private const string SmokeNonePreference = "none, thank you";
    private const string ButlerMealAskLineId = "SUB_CH02_BUTLER_MEAL_ASK_001";
    private const string ButlerMealAskText = "For supper, shall I put you down for the fresh monte genellion de plink, or thyme with Lillums?";
    private const string ButlerSmokeAskLineId = "SUB_CH02_BUTLER_SMOKE_ASK_001";
    private const string ButlerSmokeAskText = "After dinner, shall I prepare a cigar, a pipe, or no smoke at all?";
    private const string ButlerSpiritsAskLineId = "SUB_CH02_BUTLER_SPIRITS_ASK_001";
    private const string ButlerSpiritsAskText = "And shall I see that your bottle of spirits is waiting at the table?";

    private static readonly HiddenGuestConversationSpec[] HiddenGuestConversationSpecs =
    {
        new HiddenGuestConversationSpec(
            1,
            "Miss Isolde Wren",
            "Ch2_Hide_Guest01",
            "CH2_G01_FOUND_START",
            "Announce yourself before I die of manners.",
            "SUB_CH02_BUTLER_FOUND_G01",
            "I have found you, Miss Isolde Wren. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G01_FOUND_REPLY",
            "You may record whatever prevents further surprises.",
            "CH2_G01_MEAL_PLINK",
            "The fresh monte genellion de plink. If one must face horrors, one should do it properly fed.",
            MealPlinkPreference,
            "CH2_G01_SMOKE_PIPE",
            "A pipe. Slower nerves make better decisions.",
            SmokePipePreference,
            "CH2_G01_SPIRITS_REPLY",
            "See that it is not shy.",
            "Miss Isolde Wren's bottle of spirits",
            "CH2_G01_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            2,
            "Professor Lucien Vale",
            "Ch2_Hide_Guest02",
            "CH2_G02_FOUND_START",
            "Please tell me you are real before you come any closer.",
            "SUB_CH02_BUTLER_FOUND_G02",
            "I have found you, Professor Lucien Vale. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G02_FOUND_REPLY",
            "At seven? After that thing? Yes. Yes, ordinary questions may save us.",
            "CH2_G02_MEAL_THYME",
            "Thyme with Lillums, please. Something gentle. Something with leaves.",
            MealThymePreference,
            "CH2_G02_SMOKE_CIGAR",
            "A cigar, though I may only hold it for courage.",
            SmokeCigarPreference,
            "CH2_G02_SPIRITS_REPLY",
            "Thank you. I may ask it several questions.",
            "Professor Lucien Vale's bottle of spirits",
            "CH2_G02_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            3,
            "Mister Florian Knell",
            "Ch2_Hide_Guest03",
            "CH2_G03_FOUND_START",
            "If this is a party game, I withdraw my admiration.",
            "SUB_CH02_BUTLER_FOUND_G03",
            "I have found you, Mister Florian Knell. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G03_FOUND_REPLY",
            "Splendid. Nothing steadies the soul like being menued after a monster.",
            "CH2_G03_MEAL_PLINK",
            "Fresh monte genellion de plink. It sounds impossible, and I am in an impossible mood.",
            MealPlinkPreference,
            "CH2_G03_SMOKE_NONE",
            "No smoke. The monster already supplied quite enough atmosphere.",
            SmokeNonePreference,
            "CH2_G03_SPIRITS_REPLY",
            "Make it visible. I may need to toast survival several times.",
            "Mister Florian Knell's bottle of spirits",
            "CH2_G03_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            4,
            "Countess Elowen Dusk",
            "Ch2_Hide_Guest04",
            "CH2_G04_FOUND_START",
            "If you are here to say dinner is canceled, lie more elegantly.",
            "SUB_CH02_BUTLER_FOUND_G04",
            "I have found you, Countess Elowen Dusk. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G04_FOUND_REPLY",
            "Good. A schedule is a flimsy shield, but it is a shield.",
            "CH2_G04_MEAL_THYME",
            "Thyme with Lillums. Quiet food. Sensible food. Food unlikely to chase me.",
            MealThymePreference,
            "CH2_G04_SMOKE_PIPE",
            "A pipe. It gives the hands something to do besides tremble.",
            SmokePipePreference,
            "CH2_G04_SPIRITS_REPLY",
            "Good. I distrust a dinner table without witnesses.",
            "Countess Elowen Dusk's bottle of spirits",
            "CH2_G04_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            5,
            "Baron Hector Glass",
            "Ch2_Hide_Guest05",
            "CH2_G05_FOUND_START",
            "I was not hiding. I was choosing a defensible position.",
            "SUB_CH02_BUTLER_FOUND_G05",
            "I have found you, Baron Hector Glass. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G05_FOUND_REPLY",
            "Proceed. The more ordinary the ritual, the less power we give the extraordinary.",
            "CH2_G05_MEAL_PLINK",
            "Fresh monte genellion de plink. Something substantial. I dislike fleeing on an empty stomach.",
            MealPlinkPreference,
            "CH2_G05_SMOKE_CIGAR",
            "A cigar. For victory, or for pretending.",
            SmokeCigarPreference,
            "CH2_G05_SPIRITS_REPLY",
            "Place it where I can reach it without turning my back.",
            "Baron Hector Glass's bottle of spirits",
            "CH2_G05_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            6,
            "Lady Sabine Marrow",
            "Ch2_Hide_Guest06",
            "CH2_G06_FOUND_START",
            "Is it gone, or has it merely become quiet?",
            "SUB_CH02_BUTLER_FOUND_G06",
            "I have found you, Lady Sabine Marrow. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G06_FOUND_REPLY",
            "Yes. Please. Ask me anything that has only two answers.",
            "CH2_G06_MEAL_THYME",
            "Thyme with Lillums. That sounds almost medicinal. I accept.",
            MealThymePreference,
            "CH2_G06_SMOKE_NONE",
            "No smoke. The room has already burned itself into my memory.",
            SmokeNonePreference,
            "CH2_G06_SPIRITS_REPLY",
            "Good. Tell it I am counting on its courage.",
            "Lady Sabine Marrow's bottle of spirits",
            "CH2_G06_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            7,
            "Lord Ambrose Veil",
            "Ch2_Hide_Guest07",
            "CH2_G07_FOUND_START",
            "I knew the house was awake. I did not know it had pets.",
            "SUB_CH02_BUTLER_FOUND_G07",
            "I have found you, Lord Ambrose Veil. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G07_FOUND_REPLY",
            "Record quickly. The walls have begun pretending not to listen.",
            "CH2_G07_MEAL_PLINK",
            "Fresh monte genellion de plink. It sounds like a spell, and we may need one.",
            MealPlinkPreference,
            "CH2_G07_SMOKE_PIPE",
            "A pipe. Smoke curls like warnings when the air is honest.",
            SmokePipePreference,
            "CH2_G07_SPIRITS_REPLY",
            "Then pour generously. The chateau has had enough of my nerves.",
            "Lord Ambrose Veil's bottle of spirits",
            "CH2_G07_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
        new HiddenGuestConversationSpec(
            8,
            "Madame Coralie Thread",
            "Ch2_Hide_Guest08",
            "CH2_G08_FOUND_START",
            "Speak plainly. Is the room safe, or merely occupied?",
            "SUB_CH02_BUTLER_FOUND_G08",
            "I have found you, Madame Coralie Thread. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            "CH2_G08_FOUND_REPLY",
            "You may. I admire a household that continues taking orders after an omen.",
            "CH2_G08_MEAL_THYME",
            "Thyme with Lillums. Quiet, green, and unlikely to announce itself on nine legs.",
            MealThymePreference,
            "CH2_G08_SMOKE_CIGAR",
            "A cigar. I intend to leave evidence that I remained composed.",
            SmokeCigarPreference,
            "CH2_G08_SPIRITS_REPLY",
            "Good. It may be the most trustworthy guest here.",
            "Madame Coralie Thread's bottle of spirits",
            "CH2_G08_EXIT_TO_DINING",
            "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
    };

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

            ApplyConversationSpecToGuest(guest);
            guest.found = false;
            guest.foundOrder = 0;
            guest.mealPreference = string.Empty;
            guest.smokingPreference = string.Empty;
            guest.spiritBottle = string.Empty;

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

            ApplyConversationSpecToGuest(guest);
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

            GuestSearchEntry guest = new GuestSearchEntry
            {
                guestId = actorState.ActorId,
                displayName = GetCanonicalGuestDisplayName(actorState),
                actorState = actorState
            };

            ApplyConversationSpecToGuest(guest);
            guests.Add(guest);
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
        HashSet<RoomAnchor> assignedAnchors = new HashSet<RoomAnchor>();
        guests.Sort(CompareGuestIdentity);

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            ApplyConversationSpecToGuest(guest);

            if (guest.hideAnchor != null)
            {
                assignedAnchors.Add(guest.hideAnchor);
                continue;
            }

            HiddenGuestConversationSpec spec = GetConversationSpec(guest);

            if (spec != null && TryFindAnchorById(hideAnchors, spec.HideAnchorId, out RoomAnchor specAnchor))
            {
                guest.hideAnchor = specAnchor;
                assignedAnchors.Add(specAnchor);
                continue;
            }

            while (anchorIndex < hideAnchors.Length && assignedAnchors.Contains(hideAnchors[anchorIndex]))
            {
                anchorIndex++;
            }

            if (anchorIndex >= hideAnchors.Length)
            {
                Debug.LogWarning($"Chapter 2 guest search has no hide anchor for guest '{guest.guestId}'.", this);
                continue;
            }

            guest.hideAnchor = hideAnchors[anchorIndex];
            assignedAnchors.Add(guest.hideAnchor);
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

        ApplyConversationSpecToGuest(guest);

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

        ShowButlerFoundLine(guest);
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
            guest.actorState.ResetAnimatorToAuthoredState();
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

        HiddenGuestConversationSpec spec = GetConversationSpec(guest);

        if (spec != null)
        {
            guest.mealPreference = spec.FixedMealPreference;
            guest.smokingPreference = spec.FixedSmokingPreference;
            guest.spiritBottle = spec.FixedSpiritBottle;
            return;
        }

        if (string.IsNullOrWhiteSpace(guest.mealPreference))
        {
            guest.mealPreference = MealPlinkPreference;
        }

        if (string.IsNullOrWhiteSpace(guest.smokingPreference))
        {
            guest.smokingPreference = SmokeNonePreference;
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

    private void ShowButlerFoundLine(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitFoundReply);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.ButlerFoundLineId,
            ButlerSpeakerName,
            spec.ButlerFoundText,
            () => ShowGuestFoundReply(guest));
    }

    private void ShowGuestFoundReply(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitPreferencePrompt);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.FoundReplyLineId,
            spec.DisplayName,
            spec.FoundReplyText,
            () => ShowPreferenceChoices(guest));
    }

    private void ShowButlerMealAsk(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitMealReply);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            ButlerMealAskLineId,
            ButlerSpeakerName,
            ButlerMealAskText,
            () => ShowGuestMealReply(guest));
    }

    private void ShowGuestMealReply(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        guest.mealPreference = spec.FixedMealPreference;
        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitPreferencePrompt);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.MealReplyLineId,
            spec.DisplayName,
            spec.MealReplyText,
            () => ShowPreferenceChoices(guest));
    }

    private void ShowButlerSmokeAsk(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitSmokeReply);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            ButlerSmokeAskLineId,
            ButlerSpeakerName,
            ButlerSmokeAskText,
            () => ShowGuestSmokeReply(guest));
    }

    private void ShowGuestSmokeReply(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        guest.smokingPreference = spec.FixedSmokingPreference;
        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitPreferencePrompt);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.SmokeReplyLineId,
            spec.DisplayName,
            spec.SmokeReplyText,
            () => ShowPreferenceChoices(guest));
    }

    private void ShowButlerSpiritsAsk(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitSpiritsReply);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            ButlerSpiritsAskLineId,
            ButlerSpeakerName,
            ButlerSpiritsAskText,
            () => ShowGuestSpiritsReply(guest));
    }

    private void ShowGuestSpiritsReply(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        guest.spiritBottle = spec.FixedSpiritBottle;
        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitPreferencePrompt);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.SpiritsReplyLineId,
            spec.DisplayName,
            spec.SpiritsReplyText,
            () => ShowPreferenceChoices(guest));
    }

    private void ShowGuestExitToDining(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null || !TryGetConversationSpec(guest, out HiddenGuestConversationSpec spec))
        {
            return;
        }

        SetActiveConversationResumeStep(guest, GuestConversationResumeStep.AwaitExitToDiningCompletion);
        chapter2Controller.ShowGuestConversationLineWithVoice(
            spec.ExitToDiningLineId,
            spec.DisplayName,
            spec.ExitToDiningText,
            () => FinishGuestConversation(guest));
    }

    private void ShowPreferenceChoices(GuestSearchEntry guest)
    {
        if (!IsActiveConversationGuest(guest) || chapter2Controller == null)
        {
            return;
        }

        SetActiveConversationResumeStep(guest, AreAllPreferencesRecorded(guest)
            ? GuestConversationResumeStep.AwaitSendToDiningPrompt
            : GuestConversationResumeStep.AwaitPreferencePrompt);

        if (AreAllPreferencesRecorded(guest))
        {
            chapter2Controller.ShowGuestConversation(
                ButlerSpeakerName,
                string.Empty,
                "Comfort and send to Dining Room",
                () => ShowGuestExitToDining(guest));
            return;
        }

        List<string> choiceLabels = new List<string>(3);
        List<Action> choiceCallbacks = new List<Action>(3);

        if (string.IsNullOrWhiteSpace(guest.mealPreference))
        {
            choiceLabels.Add("Ask supper preference");
            choiceCallbacks.Add(() => ShowButlerMealAsk(guest));
        }

        if (string.IsNullOrWhiteSpace(guest.spiritBottle))
        {
            choiceLabels.Add("Ask drink preference");
            choiceCallbacks.Add(() => ShowButlerSpiritsAsk(guest));
        }

        if (string.IsNullOrWhiteSpace(guest.smokingPreference))
        {
            choiceLabels.Add("Ask smoke preference");
            choiceCallbacks.Add(() => ShowButlerSmokeAsk(guest));
        }

        chapter2Controller.ShowGuestConversation(
            ButlerSpeakerName,
            string.Empty,
            choiceLabels.Count > 0 ? choiceLabels[0] : null,
            choiceCallbacks.Count > 0 ? choiceCallbacks[0] : null,
            choiceLabels.Count > 1 ? choiceLabels[1] : null,
            choiceCallbacks.Count > 1 ? choiceCallbacks[1] : null,
            choiceLabels.Count > 2 ? choiceLabels[2] : null,
            choiceCallbacks.Count > 2 ? choiceCallbacks[2] : null);
    }

    private static bool AreAllPreferencesRecorded(GuestSearchEntry guest)
    {
        return guest != null &&
            !string.IsNullOrWhiteSpace(guest.mealPreference) &&
            !string.IsNullOrWhiteSpace(guest.spiritBottle) &&
            !string.IsNullOrWhiteSpace(guest.smokingPreference);
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
            case GuestConversationResumeStep.AwaitFoundReply:
                ShowGuestFoundReply(guest);
                return true;

            case GuestConversationResumeStep.AwaitPreferencePrompt:
                ShowPreferenceChoices(guest);
                return true;

            case GuestConversationResumeStep.AwaitMealReply:
                ShowGuestMealReply(guest);
                return true;

            case GuestConversationResumeStep.AwaitSmokeReply:
                ShowGuestSmokeReply(guest);
                return true;

            case GuestConversationResumeStep.AwaitSpiritsReply:
                ShowGuestSpiritsReply(guest);
                return true;

            case GuestConversationResumeStep.AwaitSendToDiningPrompt:
                ShowPreferenceChoices(guest);
                return true;

            case GuestConversationResumeStep.AwaitExitToDiningCompletion:
                FinishGuestConversation(guest);
                return true;
        }

        return false;
    }

    private static bool TryGetConversationSpec(GuestSearchEntry guest, out HiddenGuestConversationSpec spec)
    {
        spec = GetConversationSpec(guest);
        return spec != null;
    }

    private static HiddenGuestConversationSpec GetConversationSpec(GuestSearchEntry guest)
    {
        if (TryGetGuestIdentityNumber(guest, out int guestNumber) &&
            guestNumber >= 1 &&
            guestNumber <= HiddenGuestConversationSpecs.Length)
        {
            return HiddenGuestConversationSpecs[guestNumber - 1];
        }

        return null;
    }

    private static void ApplyConversationSpecToGuest(GuestSearchEntry guest)
    {
        HiddenGuestConversationSpec spec = GetConversationSpec(guest);

        if (guest == null || spec == null)
        {
            return;
        }

        guest.displayName = spec.DisplayName;

        if (string.IsNullOrWhiteSpace(guest.guestId))
        {
            guest.guestId = $"Guest{spec.GuestNumber:00}";
        }
    }

    private static bool TryGetGuestIdentityNumber(GuestSearchEntry guest, out int guestNumber)
    {
        guestNumber = 0;

        if (guest == null)
        {
            return false;
        }

        if (TryGetGuestIdentityNumber(guest.guestId, out guestNumber) ||
            TryGetGuestIdentityNumber(guest.displayName, out guestNumber))
        {
            return true;
        }

        if (guest.actorState != null)
        {
            if (TryGetGuestIdentityNumber(guest.actorState.ActorId, out guestNumber))
            {
                return true;
            }

            string objectName = guest.actorState.gameObject != null ? guest.actorState.gameObject.name : null;

            if (TryGetGuestIdentityNumber(objectName, out guestNumber))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetGuestIdentityNumber(string value, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (TryGetKnownGuestIdentityNumber(value, out guestNumber))
        {
            return true;
        }

        string cleanValue = NormalizeIdentityToken(value);

        for (int i = 1; i <= HiddenGuestConversationSpecs.Length; i++)
        {
            if (cleanValue.Contains($"guest{i:00}") ||
                cleanValue.Contains($"guest{i}") ||
                cleanValue.Contains($"g{i:00}"))
            {
                guestNumber = i;
                return true;
            }
        }

        if (TryExtractTrailingGuestNumber(value, out guestNumber) &&
            guestNumber >= 1 &&
            guestNumber <= HiddenGuestConversationSpecs.Length)
        {
            return true;
        }

        guestNumber = 0;
        return false;
    }

    private static bool TryGetKnownGuestIdentityNumber(string value, out int guestNumber)
    {
        guestNumber = 0;
        string cleanValue = NormalizeIdentityToken(value);

        if (string.IsNullOrEmpty(cleanValue))
        {
            return false;
        }

        if (cleanValue.Contains("ladysabinemarrow"))
        {
            guestNumber = 6;
            return true;
        }

        if (cleanValue.Contains("missisoldewren") ||
            cleanValue == "ava" ||
            cleanValue == "lady" ||
            cleanValue.StartsWith("ladywalk", StringComparison.Ordinal))
        {
            guestNumber = 1;
            return true;
        }

        if (cleanValue.Contains("professorlucienvale") ||
            cleanValue.Contains("butlerguest") ||
            cleanValue == "marcus")
        {
            guestNumber = 2;
            return true;
        }

        if (cleanValue.Contains("misterflorianknell") ||
            cleanValue.Contains("florianknell") ||
            cleanValue.Contains("guest3idle"))
        {
            guestNumber = 3;
            return true;
        }

        if (cleanValue.Contains("countesselowendusk") ||
            cleanValue.Contains("elowendusk"))
        {
            guestNumber = 4;
            return true;
        }

        if (cleanValue.Contains("baronhectorglass") ||
            cleanValue.Contains("hectorglass"))
        {
            guestNumber = 5;
            return true;
        }

        if (cleanValue.Contains("lordambroseveil") ||
            cleanValue.Contains("ambroseveil") ||
            cleanValue.Contains("guestpair02man"))
        {
            guestNumber = 7;
            return true;
        }

        if (cleanValue.Contains("madamecoraliethread") ||
            cleanValue.Contains("coraliethread"))
        {
            guestNumber = 8;
            return true;
        }

        return false;
    }

    private static string NormalizeIdentityToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (char.IsLetterOrDigit(character))
            {
                buffer[count] = char.ToLowerInvariant(character);
                count++;
            }
        }

        return new string(buffer, 0, count);
    }

    private static bool TryFindAnchorById(RoomAnchor[] anchors, string anchorId, out RoomAnchor foundAnchor)
    {
        foundAnchor = null;

        if (anchors == null || string.IsNullOrWhiteSpace(anchorId))
        {
            return false;
        }

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor != null &&
                (SameId(anchor.name, anchorId) || SameId(anchor.AnchorId, anchorId)))
            {
                foundAnchor = anchor;
                return true;
            }
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
        List<GuestSearchEntry> orderedGuests = new List<GuestSearchEntry>();

        if (guests != null)
        {
            for (int i = 0; i < guests.Count; i++)
            {
                if (guests[i] != null)
                {
                    orderedGuests.Add(guests[i]);
                }
            }
        }

        orderedGuests.Sort(CompareGuestIdentity);
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
        if (TryGetGuestIdentityNumber(guest, out int identityNumber))
        {
            return identityNumber;
        }

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

        HiddenGuestConversationSpec spec = GetConversationSpec(guest);

        if (spec != null)
        {
            return spec.DisplayName;
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

    private static string GetCanonicalGuestDisplayName(ActorRoomState actorState)
    {
        if (actorState == null)
        {
            return "Guest";
        }

        if (TryExtractTrailingGuestNumber(actorState.ActorId, out int actorIdNumber) &&
            actorIdNumber >= 1 &&
            actorIdNumber <= Chapter2PanicRoster.DisplayNames.Length)
        {
            return Chapter2PanicRoster.DisplayNames[actorIdNumber - 1];
        }

        string objectName = actorState.gameObject != null ? actorState.gameObject.name : string.Empty;

        if (TryExtractTrailingGuestNumber(objectName, out int objectNameNumber) &&
            objectNameNumber >= 1 &&
            objectNameNumber <= Chapter2PanicRoster.DisplayNames.Length)
        {
            return Chapter2PanicRoster.DisplayNames[objectNameNumber - 1];
        }

        return string.IsNullOrWhiteSpace(objectName) ? "Guest" : objectName.Trim();
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
        bool hasLeftNumber = TryGetGuestIdentityNumber(left, out int leftNumber);
        bool hasRightNumber = TryGetGuestIdentityNumber(right, out int rightNumber);

        if (hasLeftNumber && hasRightNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (hasLeftNumber)
        {
            return -1;
        }

        if (hasRightNumber)
        {
            return 1;
        }

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
