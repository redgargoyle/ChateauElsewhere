using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private const string Chapter1Title = "Chapter 1";
    private const string DebugCanvasName = "Canvas_ChapterDebug";
    private const string SkipChapter2ButtonName = "Button_SkipToChapter2";
    private const string SkipChapter2ButtonLabelName = "Text_SkipToChapter2";

    [Header("Chapter")]
    [SerializeField] private string currentChapterId = Chapter1Id;
    [SerializeField] private string displayedTitle = Chapter1Title;
    [SerializeField] private ChapterPhase currentPhase = ChapterPhase.NotStarted;

    [Header("Debug Tools")]
    [SerializeField] private bool autoStartChapter1 = true;
    [SerializeField] private bool skipIntro;
    [SerializeField] private bool debugFastMode;
    [SerializeField] private bool showSkipToChapter2Button = true;
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

    private Coroutine chapterRoutine;
    private Coroutine chapterCompleteRoutine;
    private bool chapterStarted;
    private Canvas debugCanvas;
    private Button skipChapter2Button;
    private TMP_Text skipChapter2ButtonLabel;

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
        if (chapterCompleteRoutine != null)
        {
            return;
        }

        chapterCompleteRoutine = StartCoroutine(CompleteChapterRoutine(nextChapterId));
    }

    public void SetChapterPlayerInputEnabled(bool enabled)
    {
        SetPlayerInputEnabled(enabled);
    }

    [ContextMenu("Skip To Chapter 2 For Testing")]
    public void SkipToChapter2ForTesting()
    {
        ResolveReferences();

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

        chapter2Controller = ResolveChapter2Controller(true);

        if (chapter2Controller != null)
        {
            chapter2Controller.BeginChapter2(this);
        }
        else
        {
            Debug.LogWarning("Skip to Chapter 2 requested, but Chapter2Controller could not be resolved.", this);
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
        SetPhase(ChapterPhase.Complete);

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
        if (!showSkipToChapter2Button)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find(DebugCanvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(DebugCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        debugCanvas = canvasObject.GetComponent<Canvas>();
        debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        debugCanvas.sortingOrder = 9400;
        EnsureEventSystem();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;

        Transform existing = root.Find(SkipChapter2ButtonName);
        skipChapter2Button = existing != null ? existing.GetComponent<Button>() : null;

        if (skipChapter2Button == null)
        {
            GameObject buttonObject = new GameObject(SkipChapter2ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(root, false);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -18f);
            buttonRect.sizeDelta = new Vector2(220f, 40f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.05f, 0.05f, 0.06f, 0.82f);

            skipChapter2Button = buttonObject.GetComponent<Button>();
            ColorBlock colors = skipChapter2Button.colors;
            colors.normalColor = new Color(0.05f, 0.05f, 0.06f, 0.82f);
            colors.highlightedColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            colors.pressedColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            colors.selectedColor = colors.highlightedColor;
            skipChapter2Button.colors = colors;
        }

        RectTransform rect = skipChapter2Button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(220f, 40f);

        skipChapter2ButtonLabel = FindOrCreateDebugButtonLabel(skipChapter2Button.transform);
        skipChapter2ButtonLabel.text = "Skip to Chapter 2";
        skipChapter2Button.onClick.RemoveAllListeners();
        skipChapter2Button.onClick.AddListener(SkipToChapter2ForTesting);
    }

    private void UpdateDebugSkipButtonVisibility()
    {
        if (!showSkipToChapter2Button)
        {
            if (skipChapter2Button != null)
            {
                skipChapter2Button.gameObject.SetActive(false);
            }

            return;
        }

        if (skipChapter2Button == null)
        {
            EnsureDebugSkipButton();
        }

        if (skipChapter2Button == null)
        {
            return;
        }

        bool shouldShow = !string.Equals(currentChapterId, Chapter2Id, System.StringComparison.OrdinalIgnoreCase);
        skipChapter2Button.gameObject.SetActive(shouldShow);
    }

    private static TMP_Text FindOrCreateDebugButtonLabel(Transform buttonRoot)
    {
        Transform existing = buttonRoot.Find(SkipChapter2ButtonLabelName);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label != null)
        {
            return label;
        }

        GameObject labelObject = new GameObject(SkipChapter2ButtonLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRoot, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        label = labelObject.GetComponent<TMP_Text>();
        label.fontSize = 17f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
