using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
[RequireComponent(typeof(Chapter1ArrivalController))]
public class ChapterManager : MonoBehaviour
{
    public const string Chapter1Id = "chapter_01_arrivals";

    [Header("Chapter")]
    [SerializeField] private string currentChapterId = Chapter1Id;
    [SerializeField] private string displayedTitle = "Act 1";
    [SerializeField] private ChapterPhase currentPhase = ChapterPhase.NotStarted;

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

    private Coroutine chapterRoutine;
    private bool chapterStarted;

    public string CurrentChapterId => currentChapterId;
    public string DisplayedTitle => displayedTitle;
    public ChapterPhase CurrentPhase => currentPhase;
    public bool DebugFastMode => debugFastMode;
    public ChapterClock Clock => chapterClock;
    public ChapterEventScheduler EventScheduler => eventScheduler;

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
        managerObject.AddComponent<ChapterManager>();
    }

    private void Awake()
    {
        ResolveReferences();

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

        ResolveReferences();
        ValidateRequiredReferences();

        currentChapterId = Chapter1Id;
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

    private IEnumerator RunChapter1Routine()
    {
        SetPlayerInputEnabled(false);

        if (chapterClock != null)
        {
            chapterClock.StopClock();
            chapterClock.ResetClock();
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

        Debug.Log("Act title shown", this);

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

    private void SetPlayerInputEnabled(bool enabled)
    {
        ResolvePlayerReference();

        if (playerInput != null)
        {
            playerInput.enabled = enabled;
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
        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
        }

        if (playerButlerReference == null && playerInput != null)
        {
            playerButlerReference = playerInput.gameObject;
        }

        if (playerButlerReference == null)
        {
            GameObject namedPlayer = GameObject.Find("Player");

            if (namedPlayer != null)
            {
                playerButlerReference = namedPlayer;
                playerInput = namedPlayer.GetComponent<PointClickPlayerMovement>();
            }
        }
    }
}
