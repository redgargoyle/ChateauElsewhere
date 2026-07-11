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
public class Chapter2Controller : Chateau.Architecture.ChapterControllerBase
{
    private const string DefaultClockStrikeClipResourcePath = "Audio/SFX/06_heavy_wooden_case_clock_gong_seed1442486_tangoflux_raw_44k1";
    private const float DefaultClockStrikeVolume = 0.4f;

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private Chapter2MonsterStingerController monsterStinger;
    [SerializeField] private Chapter2GuestPanicController guestPanic;
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
    [SerializeField] private float clockStrikeCloseUpSeconds = 5f;
    [SerializeField, Min(0.1f)] private float monsterStingerTimeoutSeconds = 14f;
    [SerializeField] private AudioSource clockStrikeAudioSource;
    [SerializeField] private AudioClip clockStrikeClip;
    [SerializeField] private string clockStrikeClipResourcePath = DefaultClockStrikeClipResourcePath;
    [SerializeField, Range(0f, 1f)] private float clockStrikeVolume = DefaultClockStrikeVolume;

    [Header("Speech")]
    [SerializeField] private float speechLineSeconds = 1.75f;
    [SerializeField]
    private string[] openingSpeechLines =
    {
        "Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—"
    };

    [Header("Subtitles")]
    [SerializeField] private bool enableSubtitles = true;
    [SerializeField] private bool subtitleDebugMode;
    [SerializeField] private SubtitleService subtitleService;
    [SerializeField] private DialogueSpeechService speechService;

    private Coroutine fadeInRoutine;
    private Coroutine openingSpeechRoutine;
    private Coroutine monsterStingerRoutine;
    private Coroutine diningObjectiveTransitionRoutine;
    private Coroutine diningRoomCompletionRoutine;
    private Coroutine dialogueVoiceChoiceRoutine;
    private AudioClip runtimeClockStrikeClip;
    private bool allGuestsFoundHandled;
    private bool dinnerSeatingHandled;
    private bool debugTeleportToDrawingRoomOnStart;
    private bool subscribedToRoomChanges;

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
        diningObjectiveTransitionRoutine = null;
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
        if (guestSearch != null && guestSearch.HasPendingGuestExitsToDining)
        {
            Debug.Log("[Ch2GuestExit] all guests found transition ignored until pending guest exits complete.", this);
            return;
        }

        BeginDiningRoomObjective();
    }

    public void DebugSkipToChapter3ForTesting(ChapterManager manager)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences();
        StopChapter2Coroutines();
        SetPlayerInputEnabled(false);
        SetDinnerClockAndStop();
        InitializeInteractionHUD();

        if (introUI != null)
        {
            introUI.HideOverlay();
        }

        MoveToDiningRoomForDebugSkip();

        if (guestSearch != null)
        {
            guestSearch.Initialize(this);
            guestSearch.DebugStageAllGuestsFoundForChapter3Skip();
        }

        allGuestsFoundHandled = true;
        dinnerSeatingHandled = true;

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
            interactionHUD.ClearClockStrike();
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("Dinner is served.");
        }

        ClearSubtitles();
        UpdateFoundGuestsHud();
        SetPhase(Chapter2Phase.Complete);
        SetPlayerInputEnabled(true);
    }

    public void DebugSkipToSevenPMForTesting(ChapterManager manager)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences();
        StopChapter2Coroutines();
        SetPlayerInputEnabled(false);
        SetDinnerClockAndStop();
        InitializeInteractionHUD();

        if (monsterStinger != null)
        {
            monsterStinger.StopStinger();
        }

        if (guestPanic != null)
        {
            guestPanic.StopPanic();
        }

        if (clockStrikeAudioSource != null)
        {
            clockStrikeAudioSource.Stop();
        }

        if (introUI != null)
        {
            introUI.HideOverlay();
        }

        if (guestSearch != null)
        {
            guestSearch.Initialize(this);
            guestSearch.DebugStageAllGuestsFoundForSevenPMSkip();
        }

        allGuestsFoundHandled = false;
        dinnerSeatingHandled = false;

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
            interactionHUD.ClearClockStrike();
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective(string.Empty);
        }

        ClearSubtitles();
        UpdateFoundGuestsHud();
        BeginDiningRoomObjective();
    }

    public void DebugResetForChapter2Skip(ChapterManager manager)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences();
        StopChapter2Coroutines();

        if (monsterStinger != null)
        {
            monsterStinger.StopStinger();
        }

        if (guestPanic != null)
        {
            guestPanic.StopPanic();
        }

        if (clockStrikeAudioSource != null)
        {
            clockStrikeAudioSource.Stop();
        }

        allGuestsFoundHandled = false;
        dinnerSeatingHandled = false;
        currentPhase = Chapter2Phase.NotStarted;
        debugTeleportToDrawingRoomOnStart = true;

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
            interactionHUD.ClearClockStrike();
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective(string.Empty);
        }
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
        interactionHUD.SetDialogueChoicesInteractable(true);
    }

    public void ShowGuestConversationWithSubtitle(
        string subtitleLineId,
        string speaker,
        string line,
        string firstChoice,
        System.Action firstCallback,
        string secondChoice = null,
        System.Action secondCallback = null,
        string thirdChoice = null,
        System.Action thirdCallback = null)
    {
        ShowGuestConversation(
            speaker,
            line,
            firstChoice,
            firstCallback,
            secondChoice,
            secondCallback,
            thirdChoice,
            thirdCallback);

        HoldDialogueChoicesForSpeech(subtitleLineId, speaker, line);
        LogSubtitleLineShown(subtitleLineId, speaker, line);
    }

    public void ShowGuestConversationLineWithVoice(
        string subtitleLineId,
        string speaker,
        string line,
        System.Action onComplete = null)
    {
        ShowGuestConversation(
            speaker,
            line,
            null,
            null);

        HoldDialogueChoicesForSpeech(subtitleLineId, speaker, line, onComplete);
        LogSubtitleLineShown(subtitleLineId, speaker, line);
    }

    public void ClearGuestConversation()
    {
        StopDialogueChoiceHold();

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
        }

        ClearSubtitles();
    }

    private void BeginDiningRoomObjective()
    {
        if (allGuestsFoundHandled)
        {
            return;
        }

        allGuestsFoundHandled = true;
        diningObjectiveTransitionRoutine = StartCoroutine(RunDiningObjectiveTransitionRoutine());
    }

    private IEnumerator RunDiningObjectiveTransitionRoutine()
    {
        SetPlayerInputEnabled(false);
        SetDinnerClockAndStop();
        PrepareGuestsForDiningTransfer();
        UpdateFoundGuestsHud();

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective(string.Empty);
            interactionHUD.ShowClockStrike(chapterClock != null ? chapterClock.CurrentTimeLabel : "7:00 PM");
        }

        ClearSubtitles();
        PlayClockStrikeDing();
        yield return new WaitForSeconds(GetClockStrikeCloseUpSeconds());

        if (interactionHUD != null)
        {
            interactionHUD.ClearClockStrike();
        }

        diningObjectiveTransitionRoutine = null;
        SetPhase(Chapter2Phase.DiningRoomObjective);
        SetPlayerInputEnabled(true);
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
        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective(string.Empty);
            interactionHUD.SetDialogue("Butler", string.Empty);
            interactionHUD.SetDialogueChoices(null, null);
        }

        if (openingSpeechLines != null)
        {
            for (int i = 0; i < openingSpeechLines.Length; i++)
            {
                string line = openingSpeechLines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                const string openingSpeechLineId = "SUB_CH02_BUTLER_ADDRESS_GUESTS_001";
                Debug.Log($"Butler: {line}", this);
                yield return SpeakLineInDialoguePanel(openingSpeechLineId, "Butler", line, false, true);
            }
        }

        openingSpeechRoutine = null;

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
            interactionHUD.ClearPrimaryAction();
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective("A terrible sound cuts through the room...");
        }

        ClearSubtitles();
        SetPhase(Chapter2Phase.MonsterStinger);
    }

    private IEnumerator FadeInDrawingRoomRoutine()
    {
        introUI.ShowBlack();
        yield return null;

        introUI.ShowTitle(chapterTitle);
        yield return new WaitForSecondsRealtime(GetChapterTitleHoldSeconds());

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

        RegisterRoomChangeHandler();

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

        if (monsterStinger == null)
        {
            monsterStinger = GetComponent<Chapter2MonsterStingerController>();
        }

        if (guestPanic == null)
        {
            guestPanic = GetComponent<Chapter2GuestPanicController>();
        }

        if (guestSearch == null)
        {
            guestSearch = GetComponent<Chapter2GuestSearchController>();
        }
    }

    private void InitializeInteractionHUD()
    {
        if (interactionHUD != null)
        {
            interactionHUD.Initialize(this, chapterClock);
        }
    }

    private void StopChapter2Coroutines()
    {
        if (fadeInRoutine != null)
        {
            StopCoroutine(fadeInRoutine);
            fadeInRoutine = null;
        }

        if (openingSpeechRoutine != null)
        {
            StopCoroutine(openingSpeechRoutine);
            openingSpeechRoutine = null;
        }

        if (monsterStingerRoutine != null)
        {
            StopCoroutine(monsterStingerRoutine);
            monsterStingerRoutine = null;
        }

        if (guestPanic != null)
        {
            guestPanic.StopPanic();
        }

        ClearSubtitles();

        if (diningObjectiveTransitionRoutine != null)
        {
            StopCoroutine(diningObjectiveTransitionRoutine);
            diningObjectiveTransitionRoutine = null;
        }

        if (diningRoomCompletionRoutine != null)
        {
            StopCoroutine(diningRoomCompletionRoutine);
            diningRoomCompletionRoutine = null;
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

        if (guestPanic == null)
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
            monsterStinger.BeginStinger();

            if (guestPanic != null)
            {
                guestPanic.BeginPanic();
            }

            float elapsedSeconds = 0f;
            float timeoutSeconds = GetMonsterStingerTimeoutSeconds();

            while (monsterStinger.IsRunning && elapsedSeconds < timeoutSeconds)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            if (monsterStinger.IsRunning)
            {
                Debug.LogWarning($"Chapter 2 monster stinger timed out after {timeoutSeconds:0.##} seconds; continuing to guest search.", this);
                monsterStinger.StopStinger();
            }
        }
        else
        {
            Debug.LogWarning("Chapter 2 monster stinger requested, but Chapter2MonsterStingerController is missing.", this);
        }

        if (guestPanic != null)
        {
            yield return guestPanic.RunExitToDoorsThenRestoreRoutine();
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
            debugTeleportToDrawingRoomOnStart = false;
            return;
        }

        if (debugTeleportToDrawingRoomOnStart &&
            navigationManager.DebugTeleportToRoom(drawingRoomId))
        {
            debugTeleportToDrawingRoomOnStart = false;
            return;
        }

        debugTeleportToDrawingRoomOnStart = false;
        navigationManager.MoveToRoom(drawingRoomId);
    }

    private void MoveToDiningRoomForDebugSkip()
    {
        if (navigationManager == null)
        {
            return;
        }

        if (IsCurrentRoom(diningRoomId))
        {
            return;
        }

        if (!navigationManager.DebugTeleportToRoom(diningRoomId))
        {
            navigationManager.MoveToRoom(diningRoomId);
        }
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

    private IEnumerator WaitForDialogueReadOrSkip(string line, float fallbackSeconds, float unskippableSeconds = 0f)
    {
        float duration = GetDialogueReadSeconds(line, fallbackSeconds);
        float protectedDuration = Mathf.Max(0f, unskippableSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && elapsed >= protectedDuration)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float GetDialogueReadSeconds(string line, float fallbackSeconds)
    {
        float fallback = Mathf.Max(0f, fallbackSeconds);
        float readableSeconds = 1.25f + Mathf.Max(0, string.IsNullOrEmpty(line) ? 0 : line.Length) / 24f;
        float maxSeconds = Mathf.Max(fallback, 6f);
        return Mathf.Clamp(Mathf.Max(fallback, readableSeconds), fallback, maxSeconds);
    }

    private float GetDiningRoomRevealSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return Mathf.Max(0f, diningRoomRevealSeconds);
    }

    private float GetClockStrikeCloseUpSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return Mathf.Max(0f, clockStrikeCloseUpSeconds);
    }

    private float GetMonsterStingerTimeoutSeconds()
    {
        return Mathf.Max(0.1f, monsterStingerTimeoutSeconds);
    }

    private void PlayClockStrikeDing()
    {
        if (clockStrikeAudioSource == null)
        {
            clockStrikeAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (clockStrikeAudioSource == null)
        {
            return;
        }

        clockStrikeAudioSource.clip = ResolveClockStrikeClip();

        clockStrikeAudioSource.playOnAwake = false;
        clockStrikeAudioSource.loop = false;
        clockStrikeAudioSource.spatialBlend = 0f;
        float baseVolume = ResolveClockStrikeVolume();
        clockStrikeAudioSource.volume = baseVolume;
        GameAudioSettings.EnsureBinding(clockStrikeAudioSource, GameAudioChannel.GameSounds, baseVolume);
        clockStrikeAudioSource.Stop();
        GameAudioSettings.TryPlay(clockStrikeAudioSource);
    }

    private AudioClip ResolveClockStrikeClip()
    {
        if (clockStrikeClip != null)
        {
            return clockStrikeClip;
        }

        string resourcePath = string.IsNullOrWhiteSpace(clockStrikeClipResourcePath)
            ? DefaultClockStrikeClipResourcePath
            : clockStrikeClipResourcePath.Trim();

        clockStrikeClip = Resources.Load<AudioClip>(resourcePath);

        if (clockStrikeClip != null)
        {
            return clockStrikeClip;
        }

        return runtimeClockStrikeClip != null
            ? runtimeClockStrikeClip
            : CreateRuntimeClockStrikeClip();
    }

    private float ResolveClockStrikeVolume()
    {
        return clockStrikeVolume > 0f ? Mathf.Clamp01(clockStrikeVolume) : DefaultClockStrikeVolume;
    }

    private AudioClip CreateRuntimeClockStrikeClip()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 1.25f;
        int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Exp(-3.6f * time);
            float bell = Mathf.Sin(2f * Mathf.PI * 784f * time) * 0.55f;
            float lowBell = Mathf.Sin(2f * Mathf.PI * 392f * time) * 0.35f;
            data[i] = (bell + lowBell) * envelope;
        }

        runtimeClockStrikeClip = AudioClip.Create("RuntimeChapter2ClockStrikeDing", samples, 1, sampleRate, false);
        runtimeClockStrikeClip.SetData(data, 0);
        return runtimeClockStrikeClip;
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

        interactionHUD.ClearPrimaryAction();
        interactionHUD.ClearStatus();
        interactionHUD.SetObjective(string.Empty);
        ShowGuestConversation("Butler", string.Empty, "Address Guests", HandleAddressGuestsPrompt);
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

    public void ShowSubtitleLine(string lineId, string speaker, string text, bool requireAdvance)
    {
        DialogueSpeechService service = ResolveSpeechService();
        service?.BeginSpeakLine(lineId, speaker, text, false, false);
    }

    public void ClearSubtitles()
    {
        speechService?.StopCurrentSpeech();
        DialogueSpeechService.StopAnyCurrentSpeech();
        subtitleService?.ClearAll();
    }

    private SubtitleService ResolveSubtitleService()
    {
        if (!enableSubtitles || !Application.isPlaying)
        {
            return null;
        }

        if (subtitleService == null)
        {
            subtitleService = SubtitleService.FindOrCreate();
        }

        subtitleService?.SetDebugMode(subtitleDebugMode);
        return subtitleService;
    }

    private DialogueSpeechService ResolveSpeechService()
    {
        if (!enableSubtitles || !Application.isPlaying)
        {
            return null;
        }

        if (speechService == null)
        {
            speechService = DialogueSpeechService.FindOrCreate();
        }

        return speechService;
    }

    private IEnumerator SpeakLine(string lineId, string speaker, string text, bool allowOverlap = false, bool blockInput = false)
    {
        DialogueSpeechService service = ResolveSpeechService();

        if (service != null)
        {
            yield return service.SpeakLine(lineId, speaker, text, allowOverlap, blockInput);
            yield break;
        }

        yield return WaitForDialogueReadOrSkip(text, GetSpeechLineSeconds());
    }

    private IEnumerator SpeakLineInDialoguePanel(string lineId, string speaker, string text, bool allowOverlap = false, bool blockInput = false)
    {
        if (interactionHUD == null)
        {
            ResolveReferences();
            InitializeInteractionHUD();
        }

        DialogueSpeechService service = ResolveSpeechService();

        if (service != null)
        {
            if (interactionHUD != null)
            {
                interactionHUD.SetDialogueSkipAction(service.SkipCurrentSpeech);
            }

            yield return service.SpeakLine(
                lineId,
                speaker,
                text,
                allowOverlap,
                blockInput,
                showSubtitleOverlay: false,
                onSpeechLineStarted: SetDialoguePanelSpeechLine);

            if (interactionHUD != null)
            {
                interactionHUD.SetDialogueSkipAction(null);
            }

            yield break;
        }

        SetDialoguePanelSpeechLine(speaker, text);
        yield return WaitForDialogueReadOrSkip(text, GetSpeechLineSeconds());
    }

    private void LogSubtitleLineShown(string lineId, string speaker, string text)
    {
        if (!subtitleDebugMode || string.IsNullOrWhiteSpace(lineId))
        {
            return;
        }

        string cleanSpeaker = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker.Trim();
        Debug.Log($"[Subtitle] {lineId.Trim()}: {cleanSpeaker}: {text}", this);
    }

    private void HoldDialogueChoicesForSpeech(string lineId, string speaker, string text, System.Action onComplete = null)
    {
        StopDialogueChoiceHold();

        if (interactionHUD == null)
        {
            return;
        }

        DialogueSpeechService service = ResolveSpeechService();

        if (service == null)
        {
            interactionHUD.SetDialogueChoicesInteractable(true);
            onComplete?.Invoke();
            return;
        }

        interactionHUD.SetDialogueChoicesInteractable(false);
        interactionHUD.SetDialogueSkipAction(service.SkipCurrentSpeech);
        dialogueVoiceChoiceRoutine = service.BeginSpeakLine(
            lineId,
            speaker,
            text,
            false,
            false,
            () =>
            {
                if (interactionHUD != null)
                {
                    interactionHUD.SetDialogueSkipAction(null);
                    interactionHUD.SetDialogueChoicesInteractable(true);
                }

                dialogueVoiceChoiceRoutine = null;
                onComplete?.Invoke();
            },
            showSubtitleOverlay: false,
            onSpeechLineStarted: SetDialoguePanelSpeechLine);
    }

    private void SetDialoguePanelSpeechLine(string speaker, string text)
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.SetDialogue(speaker, text);
    }

    private void StopDialogueChoiceHold()
    {
        if (dialogueVoiceChoiceRoutine != null || speechService != null)
        {
            speechService?.StopCurrentSpeech();
            DialogueSpeechService.StopAnyCurrentSpeech();
            dialogueVoiceChoiceRoutine = null;
        }

        interactionHUD?.SetDialogueSkipAction(null);
        interactionHUD?.SetDialogueChoicesInteractable(true);
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
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void OnDestroy()
    {
        UnregisterRoomChangeHandler();
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        StopDialogueChoiceHold();

        if (interactionHUD != null)
        {
            interactionHUD.ClearDialogue();
        }

        ClearSubtitles();

        if (currentPhase == Chapter2Phase.GuestSearch)
        {
            SetGuestConversationInputEnabled(true);
        }
    }
}
