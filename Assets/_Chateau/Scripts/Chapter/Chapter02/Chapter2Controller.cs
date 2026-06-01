using System.Collections;
using UnityEngine;

public enum Chapter2Phase
{
    NotStarted,
    FadeInDrawingRoom,
    AwaitingAddressPrompt,
    ButlerSpeech,
    MonsterStinger,
    GuestSearch,
    DiningRoomObjective,
    DiningRoomReveal,
    Complete
}

[DisallowMultipleComponent]
public class Chapter2Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private Chapter2MonsterStingerController monsterStinger;
    [SerializeField] private Chapter2GuestSearchController guestSearch;

    [Header("Rooms")]
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private string diningRoomId = "Dining Room";

    [Header("Title")]
    [SerializeField] private string chapterTitle = "Chapter 2";

    [Header("State")]
    [SerializeField] private Chapter2Phase currentPhase = Chapter2Phase.NotStarted;
    [SerializeField] private bool debugFastMode;
    [SerializeField, Range(0, 23)] private int dinnerHour = 19;
    [SerializeField, Range(0, 59)] private int dinnerMinute;
    [SerializeField] private float diningRoomRevealSeconds = 5f;

    [Header("Speech")]
    [SerializeField] private float speechLineSeconds = 1.75f;
    [SerializeField]
    private string[] openingSpeechLines =
    {
        "Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—"
    };

    private Coroutine fadeInRoutine;
    private Coroutine openingSpeechRoutine;
    private Coroutine monsterStingerRoutine;
    private Coroutine diningRoomCompletionRoutine;
    private bool allGuestsFoundHandled;
    private bool dinnerSeatingHandled;

    public Chapter2Phase CurrentPhase => currentPhase;
    public string DrawingRoomId => drawingRoomId;
    public string DiningRoomId => diningRoomId;
    public bool IsGuestSearchActive => currentPhase == Chapter2Phase.GuestSearch;

    public void BeginChapter2(ChapterManager manager)
    {
        if (currentPhase != Chapter2Phase.NotStarted)
        {
            Debug.Log("Chapter 2 start ignored because it is already active.", this);
            return;
        }

        chapterManager = manager;
        allGuestsFoundHandled = false;
        dinnerSeatingHandled = false;
        diningRoomCompletionRoutine = null;
        ResolveReferences();
        SetPhase(Chapter2Phase.FadeInDrawingRoom);
        SetPlayerInputEnabled(false);
        MoveToDrawingRoom();
        SetChapter2Clock();
        InitializeInteractionHUD();

        if (introUI != null)
        {
            if (fadeInRoutine != null)
            {
                StopCoroutine(fadeInRoutine);
            }

            fadeInRoutine = StartCoroutine(FadeInDrawingRoomRoutine());
        }
        else
        {
            SetPhase(Chapter2Phase.AwaitingAddressPrompt);
        }

        Debug.Log("Chapter 2 started", this);
    }

    private void Update()
    {
        if (currentPhase == Chapter2Phase.DiningRoomObjective && IsCurrentRoom(diningRoomId))
        {
            StartDiningRoomCompletionRoutine();
        }
    }

    public void HandleAddressGuestsPrompt()
    {
        if (currentPhase != Chapter2Phase.AwaitingAddressPrompt)
        {
            return;
        }

        SetPlayerInputEnabled(false);

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
        }

        SetPhase(Chapter2Phase.ButlerSpeech);
        Debug.Log("Butler begins addressing the guests.", this);

        if (openingSpeechRoutine != null)
        {
            StopCoroutine(openingSpeechRoutine);
        }

        openingSpeechRoutine = StartCoroutine(RunOpeningSpeechRoutine());
    }

    public void HandleAllGuestsFound()
    {
        BeginDiningRoomObjective();
    }

    public void HandleGuestSearchProgressChanged()
    {
        UpdateFoundGuestsHud();
    }

    public void SetGuestConversationInputEnabled(bool enabled)
    {
        SetPlayerInputEnabled(enabled);
    }

    public void ShowGuestConversation(
        string speaker,
        string line,
        string firstChoice,
        System.Action firstCallback,
        string secondChoice = null,
        System.Action secondCallback = null,
        string thirdChoice = null,
        System.Action thirdCallback = null)
    {
        if (interactionHUD == null)
        {
            ResolveReferences();
            InitializeInteractionHUD();
        }

        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.SetDialogue(speaker, line);
        interactionHUD.SetDialogueChoices(
            firstChoice,
            firstCallback,
            secondChoice,
            secondCallback,
            thirdChoice,
            thirdCallback);
    }

    public void ClearGuestConversation()
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearDialogue();
    }

    private void BeginDiningRoomObjective()
    {
        if (allGuestsFoundHandled)
        {
            return;
        }

        allGuestsFoundHandled = true;
        SetDinnerClockAndStop();
        PrepareGuestsForDiningTransfer();
        SetPhase(Chapter2Phase.DiningRoomObjective);
        SetPlayerInputEnabled(true);
        UpdateFoundGuestsHud();
        UpdateDiningRoomObjective();

        if (IsCurrentRoom(diningRoomId))
        {
            StartDiningRoomCompletionRoutine();
        }
    }

    private void UpdateDiningRoomObjective()
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearPrimaryAction();
        interactionHUD.ClearStatus();
        interactionHUD.SetObjective("The clock strikes 7:00. Go to the Dining Room.");
    }

    private IEnumerator RunOpeningSpeechRoutine()
    {
        if (openingSpeechLines != null)
        {
            for (int i = 0; i < openingSpeechLines.Length; i++)
            {
                string line = openingSpeechLines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (interactionHUD != null)
                {
                    interactionHUD.SetObjective(line);
                    interactionHUD.SetStatus(line);
                }

                Debug.Log($"Butler: {line}", this);
                yield return new WaitForSeconds(GetSpeechLineSeconds());
            }
        }

        openingSpeechRoutine = null;

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("A terrible sound cuts through the room...");
        }

        SetPhase(Chapter2Phase.MonsterStinger);
    }

    private IEnumerator FadeInDrawingRoomRoutine()
    {
        introUI.ShowBlack();
        yield return null;

        introUI.ShowTitle(chapterTitle);
        yield return new WaitForSeconds(GetChapterTitleHoldSeconds());

        yield return introUI.FadeFromBlack(GetFadeSeconds());

        fadeInRoutine = null;
        SetPhase(Chapter2Phase.AwaitingAddressPrompt);
    }

    private void ResolveReferences()
    {
        if (chapterManager == null)
        {
            chapterManager = GetComponent<ChapterManager>();
        }

        if (chapterManager == null)
        {
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (introUI == null)
        {
            introUI = GetComponent<ChapterIntroUI>();
        }

        if (introUI == null)
        {
            introUI = FindAnyObjectByType<ChapterIntroUI>(FindObjectsInactive.Include);
        }

        if (chapterClock == null && chapterManager != null)
        {
            chapterClock = chapterManager.Clock;
        }

        if (chapterClock == null)
        {
            chapterClock = GetComponent<ChapterClock>();
        }

        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (playerMovement == null && chapterManager != null && chapterManager.PlayerButlerReference != null)
        {
            playerMovement = chapterManager.PlayerButlerReference.GetComponent<PointClickPlayerMovement>();
        }

        if (playerMovement == null)
        {
            GameObject playerObject = GameObject.Find("Player");

            if (playerObject != null)
            {
                playerMovement = playerObject.GetComponent<PointClickPlayerMovement>();
            }
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
        }

        if (interactionHUD == null)
        {
            interactionHUD = GetComponent<Chapter2InteractionHUD>();
        }

        if (interactionHUD == null)
        {
            interactionHUD = FindAnyObjectByType<Chapter2InteractionHUD>(FindObjectsInactive.Include);
        }

        if (interactionHUD == null)
        {
            interactionHUD = gameObject.AddComponent<Chapter2InteractionHUD>();
        }

        if (monsterStinger == null)
        {
            monsterStinger = GetComponent<Chapter2MonsterStingerController>();
        }

        if (monsterStinger == null)
        {
            monsterStinger = gameObject.AddComponent<Chapter2MonsterStingerController>();
        }

        if (guestSearch == null)
        {
            guestSearch = GetComponent<Chapter2GuestSearchController>();
        }

        if (guestSearch == null)
        {
            guestSearch = gameObject.AddComponent<Chapter2GuestSearchController>();
        }
    }

    private void InitializeInteractionHUD()
    {
        if (interactionHUD != null)
        {
            interactionHUD.Initialize(this, chapterClock);
        }
    }

    private void StartMonsterStingerRoutine()
    {
        if (monsterStingerRoutine != null)
        {
            return;
        }

        if (monsterStinger == null)
        {
            ResolveReferences();
        }

        SetPlayerInputEnabled(false);

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("A terrible sound cuts through the room...");
        }

        monsterStingerRoutine = StartCoroutine(RunMonsterStingerRoutine());
    }

    private IEnumerator RunMonsterStingerRoutine()
    {
        if (monsterStinger != null)
        {
            yield return monsterStinger.PlayStinger();
        }
        else
        {
            Debug.LogWarning("Chapter 2 monster stinger requested, but Chapter2MonsterStingerController is missing.", this);
        }

        monsterStingerRoutine = null;
        StartGuestSearch();
        SetPhase(Chapter2Phase.GuestSearch);
        SetPlayerInputEnabled(true);

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("Find the guests. Tell them dinner will be served at 7:00 PM sharp.");
        }
    }

    private void StartGuestSearch()
    {
        if (guestSearch == null)
        {
            ResolveReferences();
        }

        if (guestSearch == null)
        {
            Debug.LogWarning("Chapter 2 guest search requested, but Chapter2GuestSearchController is missing.", this);
            return;
        }

        guestSearch.Initialize(this);
        guestSearch.BeginSearch();
        UpdateFoundGuestsHud();
    }

    private void SeatGuestsInDiningRoom()
    {
        if (dinnerSeatingHandled)
        {
            return;
        }

        if (guestSearch == null)
        {
            ResolveReferences();
        }

        if (guestSearch != null)
        {
            guestSearch.SeatGuestsInDiningRoom();
        }

        dinnerSeatingHandled = true;
    }

    private void PrepareGuestsForDiningTransfer()
    {
        if (guestSearch == null)
        {
            ResolveReferences();
        }

        if (guestSearch != null)
        {
            guestSearch.PrepareGuestsForDiningTransfer();
        }
    }

    private void StartDiningRoomCompletionRoutine()
    {
        if (diningRoomCompletionRoutine != null)
        {
            return;
        }

        diningRoomCompletionRoutine = StartCoroutine(RunDiningRoomCompletionRoutine());
    }

    private IEnumerator RunDiningRoomCompletionRoutine()
    {
        SetPhase(Chapter2Phase.DiningRoomReveal);
        SeatGuestsInDiningRoom();

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("Dinner is served.");
        }

        yield return new WaitForSeconds(GetDiningRoomRevealSeconds());

        diningRoomCompletionRoutine = null;
        SetPhase(Chapter2Phase.Complete);

        if (chapterManager != null)
        {
            chapterManager.CompleteChapterAndTriggerNextChapter("chapter_03_dinner_pending");
        }
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (chapterManager != null)
        {
            chapterManager.SetChapterPlayerInputEnabled(enabled);
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.SetInputEnabled(enabled);
        }
    }

    private void MoveToDrawingRoom()
    {
        if (navigationManager == null)
        {
            return;
        }

        if (string.Equals(navigationManager.CurrentRoom, drawingRoomId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        navigationManager.MoveToRoom(drawingRoomId);
    }

    private bool IsCurrentRoom(string roomId)
    {
        return navigationManager != null &&
            !string.IsNullOrWhiteSpace(roomId) &&
            string.Equals(navigationManager.CurrentRoom, roomId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetChapter2Clock()
    {
        if (chapterClock == null)
        {
            return;
        }

        chapterClock.StopClock();
        chapterClock.SetStartTime(18, 5);
    }

    private void SetDinnerClockAndStop()
    {
        if (chapterClock == null)
        {
            return;
        }

        chapterClock.StopClock();
        chapterClock.SetStartTime(dinnerHour, dinnerMinute);
    }

    private float GetFadeSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 1.5f;
    }

    private float GetChapterTitleHoldSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return introUI != null ? introUI.TitleHoldSeconds : 2f;
    }

    private float GetSpeechLineSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return Mathf.Max(0f, speechLineSeconds);
    }

    private float GetDiningRoomRevealSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return Mathf.Max(0f, diningRoomRevealSeconds);
    }

    private void SetPhase(Chapter2Phase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"Chapter 2 phase changed: {currentPhase}", this);

        if (currentPhase == Chapter2Phase.AwaitingAddressPrompt)
        {
            ShowAddressGuestsPrompt();
        }
        else if (currentPhase == Chapter2Phase.MonsterStinger)
        {
            StartMonsterStingerRoutine();
        }
    }

    private void ShowAddressGuestsPrompt()
    {
        if (interactionHUD == null)
        {
            ResolveReferences();
            InitializeInteractionHUD();
        }

        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.SetObjective("Address the guests.");
        interactionHUD.SetPrimaryAction("Address Guests", HandleAddressGuestsPrompt);
    }

    private void UpdateFoundGuestsHud()
    {
        if (interactionHUD == null || guestSearch == null)
        {
            return;
        }

        interactionHUD.SetFoundGuests(
            guestSearch.GetFoundGuestDisplayNamesInOrder(),
            guestSearch.FoundGuestCount,
            guestSearch.GuestCount);
    }
}
