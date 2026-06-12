using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum ChapterPhase
{
    NotStarted,
    BlackScreen,
    ShowActTitle,
    FadeIntoRoom,
    ArrivalSequence,
    FreeInteraction,
    Complete
}

[DisallowMultipleComponent]
[RequireComponent(typeof(ChapterClock))]
[RequireComponent(typeof(ChapterEventScheduler))]
[RequireComponent(typeof(ChapterIntroUI))]
public class ChapterManager : MonoBehaviour
{
    public const string Chapter1Id = "chapter_01_arrivals";
    public const string Chapter2Id = "chapter_02_guest_search";
    public const string Chapter3PendingId = "chapter_03_dinner_pending";
    private const string Chapter1Title = "Chapter 1";
    private const string DebugCanvasName = "Canvas_ChapterDebug";
    private const string SkipChapter2ButtonName = "Button_SkipToChapter2";

    [Header("Chapter")]
    [SerializeField] private string currentChapterId = Chapter1Id;
    [SerializeField] private string displayedTitle = Chapter1Title;
    [SerializeField] private ChapterPhase currentPhase = ChapterPhase.NotStarted;

    [Header("Timing")]
    [SerializeField] private float chapter1CompletionFadeOutDelaySeconds = 2f;

    [Header("Debug Tools")]
    [SerializeField] private bool autoStartChapter1 = true;
    [SerializeField] private bool skipIntro;
    [SerializeField] private bool debugFastMode;
    [SerializeField] private bool triggerNextGuest;
    [SerializeField] private bool printChapterState;

    [Header("References")]
    [SerializeField] private PointClickPlayerMovement playerInput;
    [SerializeField] private GameObject playerButlerReference;
    [SerializeField] private ActorRoomState butlerActorState;
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private ChapterEventScheduler eventScheduler;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private Chapter1ArrivalController chapter1ArrivalController;
    [SerializeField] private Chapter2Controller chapter2Controller;
    [SerializeField] private Chapter3AbominationDinnerController chapter3AbominationDinnerController;
    [SerializeField] private Chapter3DinnerController chapter3DinnerController;

    private Coroutine chapterRoutine;
    private Coroutine chapterCompleteRoutine;
    private bool chapterStarted;
    private bool searchedForLegacyDebugSkipButton;
    private Button skipChapter2Button;

    public string CurrentChapterId => currentChapterId;
    public string DisplayedTitle => displayedTitle;
    public ChapterPhase CurrentPhase => currentPhase;
    public bool DebugFastMode => debugFastMode;
    public ChapterClock Clock => chapterClock;
    public ChapterEventScheduler EventScheduler => eventScheduler;
    public GameObject PlayerButlerReference => playerButlerReference;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapChapterManagerForGameplay()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (!string.Equals(activeScene.name, "Gameplay", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("ChapterManager_Runtime");
        managerObject.AddComponent<ChapterClock>();
        managerObject.AddComponent<ChapterEventScheduler>();
        managerObject.AddComponent<ChapterIntroUI>();
        managerObject.AddComponent<Chapter1ArrivalController>();
        managerObject.AddComponent<Chapter2Controller>();
        managerObject.AddComponent<Chapter3AbominationDinnerController>();
        managerObject.AddComponent<ChapterManager>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureDebugSkipButton();

        if (autoStartChapter1)
        {
            if (chapter1ArrivalController != null)
            {
                chapter1ArrivalController.PrepareGuestsForChapterStart();
            }

            if (introUI != null)
            {
                introUI.ShowBlack();
            }

            SetPlayerInputEnabled(false);
        }
    }

    private void Start()
    {
        ResolveReferences();
        EnsureDebugSkipButton();
        UpdateDebugSkipButtonVisibility();

        if (autoStartChapter1)
        {
            StartChapter1();
        }
    }

    private void Update()
    {
        UpdateDebugSkipButtonVisibility();

        if (triggerNextGuest)
        {
            triggerNextGuest = false;
            TriggerNextGuest();
        }

        if (printChapterState)
        {
            printChapterState = false;
            PrintChapterState();
        }
    }

    [ContextMenu("Start Chapter 1")]
    public void StartChapter1()
    {
        if (chapterStarted)
        {
            return;
        }

        ResolveReferences();
        ValidateRequiredReferences();

        currentChapterId = Chapter1Id;
        displayedTitle = Chapter1Title;
        chapterStarted = true;
        Debug.Log("Chapter 1 started", this);

        if (chapterRoutine != null)
        {
            StopCoroutine(chapterRoutine);
        }

        chapterRoutine = StartCoroutine(RunChapter1Routine());
    }

    [ContextMenu("Trigger Next Guest")]
    public void TriggerNextGuest()
    {
        ResolveReferences();

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.TriggerNextGuest();
        }
    }

    [ContextMenu("Print Chapter State")]
    public void PrintChapterState()
    {
        ResolveReferences();

        string arrivalState = chapter1ArrivalController != null
            ? chapter1ArrivalController.BuildDebugState()
            : "Chapter1ArrivalController missing.";

        Debug.Log(
            $"Chapter State\n" +
            $"current chapter id: {currentChapterId}\n" +
            $"current phase: {currentPhase}\n" +
            $"elapsed chapter time: {(chapterClock != null ? chapterClock.ElapsedSeconds : 0f):0.00}\n" +
            arrivalState,
            this);
    }

    public void SetPhase(ChapterPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"Chapter phase changed: {currentPhase}", this);
    }

    public void NotifyArrivalSequenceComplete()
    {
        if (currentPhase == ChapterPhase.ArrivalSequence)
        {
            SetPhase(ChapterPhase.FreeInteraction);
        }
    }

    public void CompleteChapter()
    {
        SetPhase(ChapterPhase.Complete);

        if (chapterClock != null)
        {
            chapterClock.StopClock();
        }
    }

    public void CompleteChapterAndTriggerNextChapter(string nextChapterId)
    {
        string cleanNextChapterId = NormalizeNextChapterId(nextChapterId);

        if (IsDuplicateChapter2Request(cleanNextChapterId))
        {
            Debug.Log("Chapter 2 request ignored because Chapter 2 is already active.", this);
            return;
        }

        if (IsDuplicateChapter3Request(cleanNextChapterId))
        {
            Debug.Log("Chapter 3 request ignored because Chapter 3 dinner is already active.", this);
            return;
        }

        if (chapterCompleteRoutine != null)
        {
            return;
        }

        chapterCompleteRoutine = StartCoroutine(CompleteChapterRoutine(cleanNextChapterId));
    }

    public void SetChapterPlayerInputEnabled(bool enabled)
    {
        SetPlayerInputEnabled(enabled);
    }

    [ContextMenu("Skip To Chapter 2 For Testing")]
    public void SkipToChapter2ForTesting()
    {
        ResolveReferences();

        StopChapterCoroutines();

        if (eventScheduler != null)
        {
            eventScheduler.Clear();
        }

        if (chapterClock != null)
        {
            chapterClock.StopClock();
        }

        SetPlayerInputEnabled(false);
        currentChapterId = Chapter2Id;
        chapterStarted = true;
        SetPhase(ChapterPhase.Complete);
        UpdateDebugSkipButtonVisibility();

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.PrepareGuestsForChapter2Skip();
        }

        chapter2Controller = ResolveChapter2Controller(true);

        if (chapter2Controller != null)
        {
            chapter2Controller.DebugResetForChapter2Skip(this);
            chapter2Controller.BeginChapter2(this);
            chapter1ArrivalController?.RefreshChapter2SkipGuestVisibilityAfterRoomChange();
        }
        else
        {
            Debug.LogWarning("Skip to Chapter 2 requested, but Chapter2Controller could not be resolved.", this);
        }
    }

    [ContextMenu("Skip To Chapter 3 For Testing")]
    public void SkipToChapter3ForTesting()
    {
        ResolveReferences();
        StopChapterCoroutines();

        if (eventScheduler != null)
        {
            eventScheduler.Clear();
        }

        if (chapterClock != null)
        {
            chapterClock.StopClock();
        }

        if (introUI != null)
        {
            introUI.HideOverlay();
        }

        SetPlayerInputEnabled(false);
        currentChapterId = Chapter3PendingId;
        displayedTitle = "Chapter 3";
        chapterStarted = true;
        SetPhase(ChapterPhase.Complete);
        UpdateDebugSkipButtonVisibility();

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.PrepareGuestsForChapter2Skip();
            chapter1ArrivalController.HideGuestCoatsForChapter2Skip();
        }

        chapter2Controller = ResolveChapter2Controller(true);

        if (chapter2Controller != null)
        {
            chapter2Controller.DebugSkipToChapter3ForTesting(this);
            chapter3AbominationDinnerController = ResolveChapter3AbominationDinnerController(true);

            if (chapter3AbominationDinnerController != null)
            {
                chapter3AbominationDinnerController.BeginAbominationDinner(this);
            }
            else
            {
                chapter3DinnerController = ResolveChapter3DinnerController(true);

                if (chapter3DinnerController != null)
                {
                    chapter3DinnerController.BeginChapter3Dinner(this);
                }
                else
                {
                    Debug.LogWarning("Skip to Chapter 3 requested, but no Chapter 3 dinner controller could be resolved.", this);
                    SetPlayerInputEnabled(true);
                }
            }
        }
        else
        {
            Debug.LogWarning("Skip to Chapter 3 requested, but Chapter2Controller could not be resolved.", this);
            SetPlayerInputEnabled(true);
        }
    }

    private void StopChapterCoroutines()
    {
        if (chapterRoutine != null)
        {
            StopCoroutine(chapterRoutine);
            chapterRoutine = null;
        }

        if (chapterCompleteRoutine != null)
        {
            StopCoroutine(chapterCompleteRoutine);
            chapterCompleteRoutine = null;
        }
    }

    private IEnumerator RunChapter1Routine()
    {
        SetPlayerInputEnabled(false);

        if (chapterClock != null)
        {
            chapterClock.StopClock();
            chapterClock.ResetClock();
            chapterClock.SetStartTime(17, 59);
        }

        if (eventScheduler != null)
        {
            eventScheduler.Clear();
        }

        SetPhase(ChapterPhase.BlackScreen);

        if (introUI != null)
        {
            introUI.ShowBlack();
        }

        Debug.Log("Chapter intro started", this);
        yield return null;

        SetPhase(ChapterPhase.ShowActTitle);

        if (introUI != null)
        {
            introUI.ShowTitle(displayedTitle);
        }

        Debug.Log("Chapter title shown", this);

        if (!skipIntro)
        {
            yield return new WaitForSeconds(GetIntroTitleHoldSeconds());
        }

        SetPhase(ChapterPhase.FadeIntoRoom);

        float fadeSeconds = skipIntro ? 0f : GetIntroFadeSeconds();

        if (introUI != null)
        {
            yield return introUI.FadeFromBlack(fadeSeconds);
        }

        Debug.Log("Chapter fade complete", this);
        SetPlayerInputEnabled(true);
        Debug.Log("Player input enabled", this);

        if (chapterClock != null)
        {
            chapterClock.ResetClock();
            chapterClock.StartClock();
        }

        SetPhase(ChapterPhase.ArrivalSequence);

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.BeginChapter1(this);
        }
        else
        {
            SetPhase(ChapterPhase.FreeInteraction);
        }
    }

    private float GetIntroTitleHoldSeconds()
    {
        if (debugFastMode)
        {
            return 0.15f;
        }

        return introUI != null ? introUI.TitleHoldSeconds : 2f;
    }

    private float GetIntroFadeSeconds()
    {
        if (debugFastMode)
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 1.5f;
    }

    private IEnumerator CompleteChapterRoutine(string nextChapterId)
    {
        SetPlayerInputEnabled(false);
        bool delayBeforeFadeOut = IsCurrentChapter(Chapter1Id);
        SetPhase(ChapterPhase.Complete);

        if (delayBeforeFadeOut)
        {
            float delaySeconds = Mathf.Max(0f, chapter1CompletionFadeOutDelaySeconds);

            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }
        }

        if (introUI != null)
        {
            yield return introUI.FadeToBlack(GetIntroFadeSeconds());
        }

        if (chapterClock != null)
        {
            chapterClock.StopClock();
        }

        string cleanNextChapterId = NormalizeNextChapterId(nextChapterId);

        if (IsChapter2Request(cleanNextChapterId))
        {
            currentChapterId = Chapter2Id;
            chapterCompleteRoutine = null;
            chapter2Controller = ResolveChapter2Controller(true);

            if (chapter2Controller != null)
            {
                chapter2Controller.BeginChapter2(this);
            }
            else
            {
                Debug.LogWarning("Chapter 2 requested, but Chapter2Controller could not be resolved.", this);
            }

            yield break;
        }

        if (IsChapter3Request(cleanNextChapterId))
        {
            currentChapterId = Chapter3PendingId;
            displayedTitle = "Chapter 3";
            chapterCompleteRoutine = null;
            chapter3AbominationDinnerController = ResolveChapter3AbominationDinnerController(true);

            if (chapter3AbominationDinnerController != null)
            {
                chapter3AbominationDinnerController.BeginAbominationDinner(this);
            }
            else
            {
                chapter3DinnerController = ResolveChapter3DinnerController(true);

                if (chapter3DinnerController != null)
                {
                    chapter3DinnerController.BeginChapter3Dinner(this);
                }
                else
                {
                    Debug.LogWarning("Chapter 3 dinner requested, but no Chapter 3 dinner controller could be resolved.", this);
                    SetPlayerInputEnabled(true);
                }
            }

            yield break;
        }

        Debug.Log($"Next chapter requested: {cleanNextChapterId}", this);
        chapterCompleteRoutine = null;
    }

    private static string NormalizeNextChapterId(string nextChapterId)
    {
        return string.IsNullOrWhiteSpace(nextChapterId) ? "chapter_02_pending" : nextChapterId.Trim();
    }

    private static bool IsChapter2Request(string nextChapterId)
    {
        return string.Equals(nextChapterId, Chapter2Id, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nextChapterId, "chapter_02_pending", System.StringComparison.OrdinalIgnoreCase) ||
            nextChapterId.StartsWith("chapter_02", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChapter3Request(string nextChapterId)
    {
        return string.Equals(nextChapterId, Chapter3PendingId, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nextChapterId, "chapter_03_pending", System.StringComparison.OrdinalIgnoreCase) ||
            nextChapterId.StartsWith("chapter_03", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCurrentChapter(string chapterId)
    {
        return !string.IsNullOrWhiteSpace(chapterId) &&
            string.Equals(currentChapterId, chapterId, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDuplicateChapter2Request(string nextChapterId)
    {
        if (!IsChapter2Request(nextChapterId))
        {
            return false;
        }

        ResolveChapter2Controller(false);

        return string.Equals(currentChapterId, Chapter2Id, System.StringComparison.OrdinalIgnoreCase) ||
            (chapter2Controller != null && chapter2Controller.CurrentPhase != Chapter2Phase.NotStarted);
    }

    private bool IsDuplicateChapter3Request(string nextChapterId)
    {
        if (!IsChapter3Request(nextChapterId))
        {
            return false;
        }

        ResolveChapter3AbominationDinnerController(false);
        ResolveChapter3DinnerController(false);

        return string.Equals(currentChapterId, Chapter3PendingId, System.StringComparison.OrdinalIgnoreCase) &&
            ((chapter3AbominationDinnerController != null &&
                chapter3AbominationDinnerController.CurrentPhase != Chapter3AbominationDinnerPhase.NotStarted) ||
                (chapter3DinnerController != null &&
                    chapter3DinnerController.CurrentPhase != Chapter3DinnerPhase.NotStarted));
    }

    private Chapter2Controller ResolveChapter2Controller(bool createIfMissing)
    {
        if (chapter2Controller == null)
        {
            chapter2Controller = GetComponent<Chapter2Controller>();
        }

        if (chapter2Controller == null)
        {
            chapter2Controller = FindAnyObjectByType<Chapter2Controller>(FindObjectsInactive.Include);
        }

        if (chapter2Controller == null && createIfMissing)
        {
            chapter2Controller = gameObject.AddComponent<Chapter2Controller>();
        }

        return chapter2Controller;
    }

    private Chapter3DinnerController ResolveChapter3DinnerController(bool createIfMissing)
    {
        if (chapter3DinnerController == null)
        {
            chapter3DinnerController = GetComponent<Chapter3DinnerController>();
        }

        if (chapter3DinnerController == null)
        {
            chapter3DinnerController = FindAnyObjectByType<Chapter3DinnerController>(FindObjectsInactive.Include);
        }

        if (chapter3DinnerController == null && createIfMissing)
        {
            chapter3DinnerController = gameObject.AddComponent<Chapter3DinnerController>();
        }

        return chapter3DinnerController;
    }

    private Chapter3AbominationDinnerController ResolveChapter3AbominationDinnerController(bool createIfMissing)
    {
        if (chapter3AbominationDinnerController == null)
        {
            chapter3AbominationDinnerController = GetComponent<Chapter3AbominationDinnerController>();
        }

        if (chapter3AbominationDinnerController == null)
        {
            chapter3AbominationDinnerController = FindAnyObjectByType<Chapter3AbominationDinnerController>(FindObjectsInactive.Include);
        }

        if (chapter3AbominationDinnerController == null && createIfMissing)
        {
            chapter3AbominationDinnerController = gameObject.AddComponent<Chapter3AbominationDinnerController>();
        }

        return chapter3AbominationDinnerController;
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        ResolvePlayerReference();

        if (playerInput != null)
        {
            playerInput.enabled = true;
            playerInput.SetInputEnabled(enabled);
        }

        if (playerButlerReference != null)
        {
            PlayerMovement legacyMovement = playerButlerReference.GetComponent<PlayerMovement>();
            CharacterController2D legacyController = playerButlerReference.GetComponent<CharacterController2D>();

            if (legacyMovement != null)
            {
                legacyMovement.enabled = false;
            }

            if (legacyController != null)
            {
                legacyController.enabled = false;
            }
        }
    }

    private void EnsureDebugSkipButton()
    {
        HideLegacyDebugSkipButton();
    }

    private void UpdateDebugSkipButtonVisibility()
    {
        HideLegacyDebugSkipButton();
    }

    private void HideLegacyDebugSkipButton()
    {
        if (skipChapter2Button == null && !searchedForLegacyDebugSkipButton)
        {
            searchedForLegacyDebugSkipButton = true;
            GameObject canvasObject = GameObject.Find(DebugCanvasName);
            Transform root = canvasObject != null ? canvasObject.transform : null;
            Transform existing = root != null ? root.Find(SkipChapter2ButtonName) : null;
            skipChapter2Button = existing != null ? existing.GetComponent<Button>() : null;
        }

        if (skipChapter2Button != null)
        {
            skipChapter2Button.gameObject.SetActive(false);
        }
    }

    private void ValidateRequiredReferences()
    {
        if (playerInput == null && playerButlerReference == null)
        {
            Debug.LogWarning("ChapterManager missing required field: player/butler reference.", this);
        }

        if (introUI == null)
        {
            Debug.LogWarning("ChapterManager missing required field: intro UI/fade references.", this);
        }
        else
        {
            introUI.ValidateRequiredReferences();
        }

        if (chapter1ArrivalController == null)
        {
            Debug.LogWarning("ChapterManager missing required field: Chapter1ArrivalController.", this);
        }
        else
        {
            chapter1ArrivalController.ValidateRequiredReferences();
        }
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = GetComponent<ChapterClock>();
        }

        if (eventScheduler == null)
        {
            eventScheduler = GetComponent<ChapterEventScheduler>();
        }

        if (introUI == null)
        {
            introUI = GetComponent<ChapterIntroUI>();
        }

        if (chapter1ArrivalController == null)
        {
            chapter1ArrivalController = GetComponent<Chapter1ArrivalController>();
        }

        if (chapter1ArrivalController == null)
        {
            chapter1ArrivalController = FindAnyObjectByType<Chapter1ArrivalController>(FindObjectsInactive.Include);
        }

        ResolveChapter2Controller(false);
        ResolveChapter3AbominationDinnerController(false);
        ResolveChapter3DinnerController(false);

        ResolvePlayerReference();

        if (butlerActorState == null && playerButlerReference != null)
        {
            butlerActorState = playerButlerReference.GetComponent<ActorRoomState>();
        }

        if (butlerActorState != null)
        {
            butlerActorState.SetAvailableInCurrentChapter(true);
            butlerActorState.SetVisibleByChapterState(true);
            butlerActorState.SetInteractable(true);
        }
    }

    private void ResolvePlayerReference()
    {
        if (playerButlerReference == null || IsGuestObject(playerButlerReference))
        {
            GameObject namedPlayer = GameObject.Find("Player");

            if (namedPlayer != null)
            {
                playerButlerReference = namedPlayer;
                playerInput = namedPlayer.GetComponent<PointClickPlayerMovement>();
            }
        }

        if (playerInput == null && playerButlerReference != null)
        {
            playerInput = playerButlerReference.GetComponent<PointClickPlayerMovement>();
        }

        if (playerInput == null)
        {
            playerInput = FindPlayerInput();
        }

        if ((playerButlerReference == null || IsGuestObject(playerButlerReference)) && playerInput != null)
        {
            playerButlerReference = playerInput.gameObject;
        }
    }

    private static PointClickPlayerMovement FindPlayerInput()
    {
        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                !IsGuestObject(candidate.gameObject) &&
                string.Equals(candidate.gameObject.name, "Player", System.StringComparison.OrdinalIgnoreCase))
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
            target.name.IndexOf("Guest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
