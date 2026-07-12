using System.Collections;
using UnityEngine;

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
public class ChapterManager : Chateau.Architecture.GameServiceBase
{
    public const string Chapter1Id = "chapter_01_arrivals";
    public const string Chapter2Id = "chapter_02_guest_search";
    public const string Chapter3PendingId = "chapter_03_dinner_pending";
    private const string Chapter1Title = "Chapter 1";
    private const string Chapter2Title = "Chapter 2";
    private const string Chapter3Title = "Chapter 3";

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
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private ChapterEventScheduler eventScheduler;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private Chapter1ArrivalController chapter1ArrivalController;
    [SerializeField] private Chapter2Controller chapter2Controller;
    [SerializeField] private DialogueSpeechService speechService;
    [SerializeField] private SubtitleService subtitleService;

    private Coroutine chapterRoutine;
    private Coroutine chapterCompleteRoutine;
    private bool chapterStarted;

    public string CurrentChapterId => currentChapterId;
    public string DisplayedTitle => displayedTitle;
    public ChapterPhase CurrentPhase => currentPhase;
    public bool DebugFastMode => debugFastMode;
    public ChapterClock Clock => chapterClock;
    public ChapterEventScheduler EventScheduler => eventScheduler;
    public GameObject PlayerButlerReference => playerInput != null ? playerInput.gameObject : null;

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (playerInput == null)
        {
            report.AddError("ChapterManager requires its serialized PointClickPlayerMovement.", this);
        }

        if (chapterClock == null)
        {
            report.AddError("ChapterManager requires its serialized ChapterClock.", this);
        }

        if (eventScheduler == null)
        {
            report.AddError("ChapterManager requires its serialized ChapterEventScheduler.", this);
        }

        if (introUI == null)
        {
            report.AddError("ChapterManager requires its serialized ChapterIntroUI.", this);
        }

        if (chapter1ArrivalController == null)
        {
            report.AddError("ChapterManager requires its serialized Chapter1ArrivalController.", this);
        }

        if (chapter2Controller == null)
        {
            report.AddError("ChapterManager requires its serialized Chapter2Controller.", this);
        }

        if (speechService == null)
        {
            report.AddError("ChapterManager requires its serialized DialogueSpeechService.", this);
        }

        if (subtitleService == null)
        {
            report.AddError("ChapterManager requires its serialized SubtitleService.", this);
        }
    }

    private void Awake()
    {
        GameplayRuntimeState.ResetForGameplayStart();

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
        if (autoStartChapter1)
        {
            StartChapter1();
        }
    }

    private void Update()
    {
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
        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.TriggerNextGuest();
        }
    }

    [ContextMenu("Print Chapter State")]
    public void PrintChapterState()
    {
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
        StopActiveDialogueForDebugTransition();

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

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.PrepareGuestsForChapter2Skip();
        }

        if (chapter2Controller != null)
        {
            chapter2Controller.DebugResetForChapter2Skip(this);
            chapter2Controller.BeginChapter2(this);
            chapter1ArrivalController?.RefreshChapter2SkipGuestVisibilityAfterRoomChange();
        }
        else
        {
            Debug.LogWarning("Skip to Chapter 2 requested, but the serialized Chapter2Controller is not configured.", this);
        }
    }

    [ContextMenu("Skip To Chapter 3 For Testing")]
    public void SkipToChapter3ForTesting()
    {
        StopActiveDialogueForDebugTransition();
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
        displayedTitle = Chapter3Title;
        chapterStarted = true;
        SetPhase(ChapterPhase.Complete);

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.PrepareGuestsForChapter2Skip();
            chapter1ArrivalController.HideGuestCoatsForChapter2Skip();
        }

        if (chapter2Controller != null)
        {
            chapter2Controller.DebugSkipToChapter3ForTesting(this);
        }
        else
        {
            Debug.LogWarning("Skip to Chapter 3 requested, but the serialized Chapter2Controller is not configured.", this);
            SetPlayerInputEnabled(true);
        }
    }

    [ContextMenu("Skip To 7:00 PM For Testing")]
    public void SkipToSevenPMForTesting()
    {
        StopActiveDialogueForDebugTransition();
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
        currentChapterId = Chapter2Id;
        displayedTitle = Chapter2Title;
        chapterStarted = true;
        SetPhase(ChapterPhase.Complete);

        if (chapter1ArrivalController != null)
        {
            chapter1ArrivalController.PrepareGuestsForChapter2Skip();
            chapter1ArrivalController.HideGuestCoatsForChapter2Skip();
        }

        if (chapter2Controller != null)
        {
            chapter2Controller.DebugSkipToSevenPMForTesting(this);
        }
        else
        {
            Debug.LogWarning("Skip to 7:00 PM requested, but the serialized Chapter2Controller is not configured.", this);
            SetPlayerInputEnabled(true);
        }
    }

    public void StopActiveDialogueForDebugTransition()
    {
        speechService?.CancelQueuedSpeech();
        subtitleService?.ClearAll();
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
            yield return new WaitForSecondsRealtime(GetIntroTitleHoldSeconds());
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
                yield return new WaitForSecondsRealtime(delaySeconds);
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
            displayedTitle = GetChapterTitle(cleanNextChapterId);
            chapterCompleteRoutine = null;

            if (chapter2Controller != null)
            {
                chapter2Controller.BeginChapter2(this);
            }
            else
            {
                Debug.LogWarning("Chapter 2 requested, but the serialized Chapter2Controller is not configured.", this);
            }

            yield break;
        }

        currentChapterId = cleanNextChapterId;
        displayedTitle = GetChapterTitle(cleanNextChapterId);
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
            (!string.IsNullOrWhiteSpace(nextChapterId) && nextChapterId.StartsWith("chapter_02", System.StringComparison.OrdinalIgnoreCase));
    }

    private static string GetChapterTitle(string chapterId)
    {
        if (IsChapter2Request(chapterId))
        {
            return Chapter2Title;
        }

        if (string.Equals(chapterId, Chapter3PendingId, System.StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(chapterId) && chapterId.StartsWith("chapter_03", System.StringComparison.OrdinalIgnoreCase)))
        {
            return Chapter3Title;
        }

        return string.IsNullOrWhiteSpace(chapterId) ? Chapter1Title : chapterId.Trim();
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

        return string.Equals(currentChapterId, Chapter2Id, System.StringComparison.OrdinalIgnoreCase) ||
            (chapter2Controller != null && chapter2Controller.CurrentPhase != Chapter2Phase.NotStarted);
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        playerInput?.SetInputEnabled(enabled);
    }

    private void ValidateRequiredReferences()
    {
        if (playerInput == null)
        {
            Debug.LogWarning("ChapterManager missing required field: player input.", this);
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
}
