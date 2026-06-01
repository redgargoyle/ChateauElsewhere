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
    [SerializeField] private float diningRoomFadeDelayGameMinutes = 5f;

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
        if (!ShouldWatchDinnerTime())
        {
            return;
        }

        TrySeatDinnerGuestsAtDinnerTime();

        if (dinnerSeatingHandled && IsCurrentRoom(diningRoomId))
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
        if (allGuestsFoundHandled)
        {
            return;
        }

        allGuestsFoundHandled = true;
        SetPhase(Chapter2Phase.DiningRoomObjective);
        StartChapter2Clock();
        TrySeatDinnerGuestsAtDinnerTime();

        SetPlayerInputEnabled(true);
        UpdateDiningRoomObjective();

        if (dinnerSeatingHandled && IsCurrentRoom(diningRoomId))
        {
            StartDiningRoomCompletionRoutine();
        }
    }

    private void TrySeatDinnerGuestsAtDinnerTime()
    {
        if (dinnerSeatingHandled || !HasReachedDinnerTime())
        {
            return;
        }

        allGuestsFoundHandled = true;
        dinnerSeatingHandled = true;
        SeatGuestsInDiningRoom();
        SetPhase(Chapter2Phase.DiningRoomObjective);
        SetPlayerInputEnabled(true);
        UpdateDiningRoomObjective();
    }

    private void UpdateDiningRoomObjective()
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearPrimaryAction();
        interactionHUD.ClearStatus();

        if (dinnerSeatingHandled)
        {
            interactionHUD.SetObjective("The clock strikes 7:00. Go to the Dining Room.");
            return;
        }

        interactionHUD.SetObjective("All guests found. Go to the Dining Room before 7:00 PM.");
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
        StartChapter2Clock();
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
    }

    private void SeatGuestsInDiningRoom()
    {
        if (guestSearch == null)
        {
            ResolveReferences();
        }

        if (guestSearch != null)
        {
            guestSearch.SeatGuestsInDiningRoom();
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
        StartChapter2Clock();

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("Dinner is served.");
        }

        if (chapterClock != null)
        {
            while (!HasReachedDiningRoomFadeTime())
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(GetFallbackDiningRoomFadeSeconds());
        }

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

    private bool ShouldWatchDinnerTime()
    {
        return currentPhase == Chapter2Phase.GuestSearch ||
            currentPhase == Chapter2Phase.DiningRoomObjective;
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

    private void StartChapter2Clock()
    {
        if (chapterClock != null && !chapterClock.IsRunning)
        {
            chapterClock.StartClock();
        }
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

    private float GetFallbackDiningRoomFadeSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return Mathf.Max(0f, diningRoomFadeDelayGameMinutes);
    }

    private bool HasReachedDinnerTime()
    {
        if (chapterClock == null)
        {
            return true;
        }

        return chapterClock.HasReachedTime(dinnerHour, dinnerMinute);
    }

    private bool HasReachedDiningRoomFadeTime()
    {
        if (chapterClock == null)
        {
            return true;
        }

        return chapterClock.CurrentTotalMinutes >= GetDiningRoomFadeTotalMinutes();
    }

    private int GetDiningRoomFadeTotalMinutes()
    {
        return GetDinnerTotalMinutes() + Mathf.CeilToInt(Mathf.Max(0f, diningRoomFadeDelayGameMinutes));
    }

    private int GetDinnerTotalMinutes()
    {
        return ChapterClock.ToTotalMinutes(dinnerHour, dinnerMinute);
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
}
