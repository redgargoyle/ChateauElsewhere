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

    [Header("Rooms")]
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private string diningRoomId = "Dining Room";

    [Header("State")]
    [SerializeField] private Chapter2Phase currentPhase = Chapter2Phase.NotStarted;
    [SerializeField] private bool debugFastMode;

    private Coroutine fadeInRoutine;

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

    public void HandleAddressGuestsPrompt()
    {
        if (currentPhase != Chapter2Phase.AwaitingAddressPrompt)
        {
            return;
        }

        if (interactionHUD != null)
        {
            interactionHUD.ClearPrimaryAction();
        }

        SetPhase(Chapter2Phase.ButlerSpeech);
        Debug.Log("Butler begins addressing the guests.", this);
    }

    private IEnumerator FadeInDrawingRoomRoutine()
    {
        introUI.ShowBlack();
        yield return null;
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
    }

    private void InitializeInteractionHUD()
    {
        if (interactionHUD != null)
        {
            interactionHUD.Initialize(this, chapterClock);
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

    private void SetChapter2Clock()
    {
        if (chapterClock == null)
        {
            return;
        }

        chapterClock.StopClock();
        chapterClock.SetStartTime(18, 5);
    }

    private float GetFadeSeconds()
    {
        if (debugFastMode || (chapterManager != null && chapterManager.DebugFastMode))
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 1.5f;
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
