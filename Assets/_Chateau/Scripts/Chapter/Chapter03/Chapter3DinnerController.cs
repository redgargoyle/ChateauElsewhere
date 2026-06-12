using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Chapter3DinnerPhase
{
    NotStarted,
    SeatedIdle,
    DinnerServedCovered,
    EatingActive,
    MealFinishedIdle,
    Complete
}

[DisallowMultipleComponent]
public sealed class Chapter3DinnerController : MonoBehaviour
{
    private const string DefaultDiningRoomId = "Dining Room";
    private const string DefaultPendingFlag = "chapter_03_dinner_pending";

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private Chapter2Controller chapter2Controller;
    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private DiningRoomAmbienceDirector diningRoomAmbienceDirector;

    [Header("Chapter")]
    [SerializeField] private string diningRoomId = DefaultDiningRoomId;
    [SerializeField] private string pendingFlag = DefaultPendingFlag;
    [SerializeField] private Chapter3DinnerPhase currentPhase = Chapter3DinnerPhase.NotStarted;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float coveredDinnerHoldSeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float eatingDurationSeconds = 60f;
    [SerializeField, Range(0f, 1f)] private float foodHalfwayNormalizedTime = 0.5f;
    [SerializeField] private bool autoStartSequence = true;
    [SerializeField] private bool movePlayerToDiningRoomOnStart = true;
    [SerializeField] private bool useAnimatedDiningRoomPresentation = true;
    [SerializeField] private bool hideGuestActorVisualsWhenPresentationActive = true;
    [SerializeField] private bool keepAnimatedPresentationAfterMeal;

    [Header("Food Visuals")]
    [SerializeField] private GameObject coveredDinnerGroup;
    [SerializeField] private GameObject fullFoodGroup;
    [SerializeField] private GameObject halfFoodGroup;
    [SerializeField] private GameObject emptyFoodGroup;

    [Header("Guests")]
    [SerializeField] private List<GuestDiningPerformance> guestPerformances = new List<GuestDiningPerformance>();

    private readonly DiningFoodVisualState foodVisuals = new DiningFoodVisualState();
    private Coroutine dinnerRoutine;
    private bool warnedMissingFoodSetup;
    private bool warnedMissingGuestSetup;
    private bool warnedMissingPresentationSetup;
    private bool loggedEnabledAnimatedPresentation;
    private bool animatedPresentationActive;
    private readonly List<GuestVisualSnapshot> hiddenGuestVisuals = new List<GuestVisualSnapshot>();

    private sealed class GuestVisualSnapshot
    {
        public ActorRoomState Actor;
        public Renderer[] Renderers;
        public bool[] RendererStates;
        public Graphic[] Graphics;
        public bool[] GraphicStates;
        public bool[] GraphicRaycastStates;
        public CanvasGroup[] CanvasGroups;
        public float[] CanvasGroupAlphas;
        public bool[] CanvasGroupInteractableStates;
        public bool[] CanvasGroupBlocksRaycastsStates;
    }

    public Chapter3DinnerPhase CurrentPhase => currentPhase;
    public string PendingFlag => string.IsNullOrWhiteSpace(pendingFlag) ? DefaultPendingFlag : pendingFlag.Trim();

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(diningRoomId))
        {
            diningRoomId = DefaultDiningRoomId;
        }

        if (string.IsNullOrWhiteSpace(pendingFlag))
        {
            pendingFlag = DefaultPendingFlag;
        }

        coveredDinnerHoldSeconds = Mathf.Max(0f, coveredDinnerHoldSeconds);
        eatingDurationSeconds = Mathf.Max(0.1f, eatingDurationSeconds);
        foodHalfwayNormalizedTime = Mathf.Clamp01(foodHalfwayNormalizedTime);
        ConfigureFoodVisuals();
    }

    public void BeginChapter3Dinner(ChapterManager manager = null)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences(true);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        RestoreSuppressedGuestVisuals();
        animatedPresentationActive = false;

        if (!Application.isPlaying)
        {
            EnterSeatedIdle();

            if (autoStartSequence)
            {
                EnterDinnerServedCovered();
            }

            Debug.LogWarning("Chapter 3 dinner was prepared in edit mode. Enter Play Mode to run the timed dinner sequence.", this);
            return;
        }

        dinnerRoutine = StartCoroutine(RunDinnerSequence());
    }

    [ContextMenu("Debug Start Chapter 3 Dinner")]
    public void DebugStartChapter3Dinner()
    {
        BeginChapter3Dinner(chapterManager);
    }

    [ContextMenu("Debug Finish Dinner Immediately")]
    public void DebugFinishDinnerImmediately()
    {
        ResolveReferences(true);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        EnterMealFinishedIdle();
        SetPlayerInputEnabled(true);
    }

    private IEnumerator RunDinnerSequence()
    {
        SetPlayerInputEnabled(false);
        EnterSeatedIdle();

        if (introUI != null)
        {
            yield return introUI.FadeFromBlack(GetFadeFromBlackSeconds());
        }

        SetPlayerInputEnabled(true);

        if (!autoStartSequence)
        {
            dinnerRoutine = null;
            yield break;
        }

        EnterDinnerServedCovered();

        float coveredHold = GetCoveredDinnerHoldSeconds();

        if (coveredHold > 0f)
        {
            yield return new WaitForSeconds(coveredHold);
        }

        EnterEatingActive();

        float duration = GetEatingDurationSeconds();
        float halfTime = duration * Mathf.Clamp01(foodHalfwayNormalizedTime);
        bool switchedToHalf = halfTime <= 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (!switchedToHalf && elapsed >= halfTime)
            {
                ShowHalfFoodIfAssigned();
                switchedToHalf = true;
            }

            yield return null;
        }

        EnterMealFinishedIdle();
        dinnerRoutine = null;
    }

    private void EnterSeatedIdle()
    {
        SetPhase(Chapter3DinnerPhase.SeatedIdle);
        ResolveReferences(true);
        ConfigureFoodVisuals();
        EnsurePlayerInDiningRoom();
        StageGuestsAtDiningTable();
        RefreshGuestPerformancesFromActors();

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].PrepareSeatedIdle();
            }
        }

        DeactivateAnimatedDiningRoomPresentation();
        ApplyGuestVisualPresentationState();
        ShowNoFood();
        UpdateObjective("Guests gather at the table.");
    }

    private void EnterDinnerServedCovered()
    {
        SetPhase(Chapter3DinnerPhase.DinnerServedCovered);

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].PrepareSeatedIdle();
            }
        }

        ApplyGuestVisualPresentationState();
        ShowCoveredDinner();
        UpdateObjective("Dinner is served.");
    }

    private void EnterEatingActive()
    {
        SetPhase(Chapter3DinnerPhase.EatingActive);
        ShowFullFood();
        ActivateAnimatedDiningRoomPresentationIfNeeded();

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].BeginEating();
            }
        }

        ApplyGuestVisualPresentationState();
        UpdateObjective("The guests begin dinner.");
    }

    private void EnterMealFinishedIdle()
    {
        SetPhase(Chapter3DinnerPhase.MealFinishedIdle);

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].StopEatingAndIdle();
            }
        }

        if (!keepAnimatedPresentationAfterMeal)
        {
            DeactivateAnimatedDiningRoomPresentation();
        }

        ApplyGuestVisualPresentationState();
        ShowEmptyFoodOrHide();
        UpdateObjective("The meal is finished.");
    }

    private void StageGuestsAtDiningTable()
    {
        if (guestSearch == null)
        {
            WarnMissingGuestSetupOnce("Chapter 3 dinner could not find Chapter2GuestSearchController, so guests could not be seated.");
            return;
        }

        if (chapter2Controller != null)
        {
            guestSearch.Initialize(chapter2Controller);
        }

        guestSearch.AutoDiscoverGuestsIfNeeded();
        guestSearch.AutoAssignHideAnchorsIfNeeded();

        if (guestSearch.FoundGuestCount <= 0)
        {
            guestSearch.DebugStageAllGuestsFoundForChapter3Skip();
        }
        else
        {
            guestSearch.SeatGuestsInDiningRoom();
        }
    }

    private void RefreshGuestPerformancesFromActors()
    {
        if (guestPerformances == null)
        {
            guestPerformances = new List<GuestDiningPerformance>();
        }

        RemoveNullGuestPerformances();

        HashSet<GuestDiningPerformance> knownPerformances = new HashSet<GuestDiningPerformance>();

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                knownPerformances.Add(guestPerformances[i]);
            }
        }

        List<ActorRoomState> actors = GetDiningGuestActors();

        for (int i = 0; i < actors.Count; i++)
        {
            ActorRoomState actor = actors[i];

            if (actor == null || actor.gameObject == null)
            {
                continue;
            }

            actor.SetCurrentRoom(diningRoomId);
            actor.SetAvailableInCurrentChapter(true);
            actor.SetVisibleByChapterState(true);
            actor.SetInteractable(false);
            actor.SetSeated(true);
            actor.ApplyState();

            GuestDiningPerformance performance = actor.GetComponent<GuestDiningPerformance>();

            if (performance == null)
            {
                performance = actor.gameObject.AddComponent<GuestDiningPerformance>();
            }

            performance.Configure(actor);

            if (knownPerformances.Add(performance))
            {
                guestPerformances.Add(performance);
            }
        }

        if (guestPerformances.Count == 0)
        {
            GuestDiningPerformance[] discoveredPerformances = FindObjectsByType<GuestDiningPerformance>(FindObjectsInactive.Include);

            for (int i = 0; i < discoveredPerformances.Length; i++)
            {
                if (discoveredPerformances[i] != null && knownPerformances.Add(discoveredPerformances[i]))
                {
                    guestPerformances.Add(discoveredPerformances[i]);
                }
            }
        }

        if (guestPerformances.Count == 0)
        {
            WarnMissingGuestSetupOnce("Chapter 3 dinner found no GuestDiningPerformance components or guest actors.");
        }
    }

    private List<ActorRoomState> GetDiningGuestActors()
    {
        List<ActorRoomState> actors = new List<ActorRoomState>();

        if (guestSearch == null)
        {
            return actors;
        }

        actors = guestSearch.GetFoundActorsInOrder();

        if (actors.Count == 0)
        {
            actors = guestSearch.GetGuestActorsInIdentityOrder();
        }

        return actors;
    }

    private void RemoveNullGuestPerformances()
    {
        for (int i = guestPerformances.Count - 1; i >= 0; i--)
        {
            if (guestPerformances[i] == null)
            {
                guestPerformances.RemoveAt(i);
            }
        }
    }

    private void EnsurePlayerInDiningRoom()
    {
        if (!movePlayerToDiningRoomOnStart || navigationManager == null || string.IsNullOrWhiteSpace(diningRoomId))
        {
            return;
        }

        if (SameRoom(navigationManager.CurrentRoom, diningRoomId))
        {
            return;
        }

        if (!navigationManager.DebugTeleportToRoom(diningRoomId))
        {
            navigationManager.MoveToRoom(diningRoomId);
        }
    }

    private void ActivateAnimatedDiningRoomPresentationIfNeeded()
    {
        if (!useAnimatedDiningRoomPresentation)
        {
            DeactivateAnimatedDiningRoomPresentation();
            return;
        }

        if (diningRoomAmbienceDirector == null)
        {
            diningRoomAmbienceDirector = FindAnyObjectByType<DiningRoomAmbienceDirector>(FindObjectsInactive.Include);
        }

        if (diningRoomAmbienceDirector == null)
        {
            WarnMissingPresentationSetupOnce("Chapter 3 dinner has no DiningRoomAmbienceDirector assigned, so the animated dining-room frame layer cannot play.");
            return;
        }

        if (!diningRoomAmbienceDirector.HasUsableFrames())
        {
            WarnMissingPresentationSetupOnce("Chapter 3 dining-room animation has no usable frames assigned.");
            return;
        }

        Transform presentationTransform = diningRoomAmbienceDirector.transform;
        if (presentationTransform != null)
        {
            presentationTransform.SetSiblingIndex(0);
        }

        GameObject presentationObject = diningRoomAmbienceDirector.gameObject;
        if (presentationObject != null && !presentationObject.activeSelf)
        {
            presentationObject.SetActive(true);
        }

        diningRoomAmbienceDirector.enabled = true;
        diningRoomAmbienceDirector.SetImagesVisible(true);
        diningRoomAmbienceDirector.RestartLoop();
        animatedPresentationActive = true;

        if (!loggedEnabledAnimatedPresentation)
        {
            loggedEnabledAnimatedPresentation = true;
            Debug.Log("Chapter 3 enabled the animated dining-room presentation layer and will hide duplicate live guest sprites while it plays.", this);
        }
    }

    private void DeactivateAnimatedDiningRoomPresentation()
    {
        animatedPresentationActive = false;

        if (diningRoomAmbienceDirector != null)
        {
            diningRoomAmbienceDirector.SetImagesVisible(false);
            diningRoomAmbienceDirector.enabled = false;

            if (diningRoomAmbienceDirector.gameObject != null && diningRoomAmbienceDirector.gameObject.activeSelf)
            {
                diningRoomAmbienceDirector.gameObject.SetActive(false);
            }
        }

        RestoreSuppressedGuestVisuals();
    }

    private void ApplyGuestVisualPresentationState()
    {
        if (animatedPresentationActive && hideGuestActorVisualsWhenPresentationActive)
        {
            SuppressGuestActorVisuals();
            return;
        }

        RestoreSuppressedGuestVisuals();
    }

    private void SuppressGuestActorVisuals()
    {
        List<ActorRoomState> actors = GetDiningGuestActorsFromPerformances();

        for (int i = 0; i < actors.Count; i++)
        {
            ActorRoomState actor = actors[i];

            if (actor == null)
            {
                continue;
            }

            GuestVisualSnapshot snapshot = GetOrCreateGuestVisualSnapshot(actor);
            ApplySuppressedGuestVisualSnapshot(snapshot);
        }
    }

    private List<ActorRoomState> GetDiningGuestActorsFromPerformances()
    {
        List<ActorRoomState> actors = new List<ActorRoomState>();
        HashSet<ActorRoomState> knownActors = new HashSet<ActorRoomState>();

        if (guestPerformances != null)
        {
            for (int i = 0; i < guestPerformances.Count; i++)
            {
                GuestDiningPerformance performance = guestPerformances[i];
                ActorRoomState actor = performance != null ? performance.ActorState : null;

                if (actor != null && knownActors.Add(actor))
                {
                    actors.Add(actor);
                }
            }
        }

        List<ActorRoomState> searchedActors = GetDiningGuestActors();

        for (int i = 0; i < searchedActors.Count; i++)
        {
            ActorRoomState actor = searchedActors[i];

            if (actor != null && knownActors.Add(actor))
            {
                actors.Add(actor);
            }
        }

        return actors;
    }

    private GuestVisualSnapshot GetOrCreateGuestVisualSnapshot(ActorRoomState actor)
    {
        for (int i = 0; i < hiddenGuestVisuals.Count; i++)
        {
            if (hiddenGuestVisuals[i] != null && hiddenGuestVisuals[i].Actor == actor)
            {
                return hiddenGuestVisuals[i];
            }
        }

        GuestVisualSnapshot snapshot = CaptureGuestVisualSnapshot(actor);
        hiddenGuestVisuals.Add(snapshot);
        return snapshot;
    }

    private GuestVisualSnapshot CaptureGuestVisualSnapshot(ActorRoomState actor)
    {
        GameObject root = actor != null ? actor.gameObject : null;
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        Graphic[] graphics = root != null ? root.GetComponentsInChildren<Graphic>(true) : Array.Empty<Graphic>();
        CanvasGroup[] canvasGroups = root != null ? root.GetComponentsInChildren<CanvasGroup>(true) : Array.Empty<CanvasGroup>();

        GuestVisualSnapshot snapshot = new GuestVisualSnapshot
        {
            Actor = actor,
            Renderers = renderers,
            RendererStates = new bool[renderers.Length],
            Graphics = graphics,
            GraphicStates = new bool[graphics.Length],
            GraphicRaycastStates = new bool[graphics.Length],
            CanvasGroups = canvasGroups,
            CanvasGroupAlphas = new float[canvasGroups.Length],
            CanvasGroupInteractableStates = new bool[canvasGroups.Length],
            CanvasGroupBlocksRaycastsStates = new bool[canvasGroups.Length]
        };

        for (int i = 0; i < renderers.Length; i++)
        {
            snapshot.RendererStates[i] = renderers[i] != null && renderers[i].enabled;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            snapshot.GraphicStates[i] = graphics[i].enabled;
            snapshot.GraphicRaycastStates[i] = graphics[i].raycastTarget;
        }

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            if (canvasGroups[i] == null)
            {
                continue;
            }

            snapshot.CanvasGroupAlphas[i] = canvasGroups[i].alpha;
            snapshot.CanvasGroupInteractableStates[i] = canvasGroups[i].interactable;
            snapshot.CanvasGroupBlocksRaycastsStates[i] = canvasGroups[i].blocksRaycasts;
        }

        return snapshot;
    }

    private static void ApplySuppressedGuestVisualSnapshot(GuestVisualSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.Renderers.Length; i++)
        {
            if (snapshot.Renderers[i] != null)
            {
                snapshot.Renderers[i].enabled = false;
            }
        }

        for (int i = 0; i < snapshot.Graphics.Length; i++)
        {
            if (snapshot.Graphics[i] != null)
            {
                snapshot.Graphics[i].enabled = false;
                snapshot.Graphics[i].raycastTarget = false;
            }
        }

        for (int i = 0; i < snapshot.CanvasGroups.Length; i++)
        {
            if (snapshot.CanvasGroups[i] == null)
            {
                continue;
            }

            snapshot.CanvasGroups[i].alpha = 0f;
            snapshot.CanvasGroups[i].interactable = false;
            snapshot.CanvasGroups[i].blocksRaycasts = false;
        }
    }

    private void RestoreSuppressedGuestVisuals()
    {
        for (int i = 0; i < hiddenGuestVisuals.Count; i++)
        {
            RestoreGuestVisualSnapshot(hiddenGuestVisuals[i]);
        }

        hiddenGuestVisuals.Clear();
    }

    private static void RestoreGuestVisualSnapshot(GuestVisualSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.Renderers.Length; i++)
        {
            if (snapshot.Renderers[i] != null && i < snapshot.RendererStates.Length)
            {
                snapshot.Renderers[i].enabled = snapshot.RendererStates[i];
            }
        }

        for (int i = 0; i < snapshot.Graphics.Length; i++)
        {
            if (snapshot.Graphics[i] == null || i >= snapshot.GraphicStates.Length)
            {
                continue;
            }

            snapshot.Graphics[i].enabled = snapshot.GraphicStates[i];
            snapshot.Graphics[i].raycastTarget = i < snapshot.GraphicRaycastStates.Length && snapshot.GraphicRaycastStates[i];
        }

        for (int i = 0; i < snapshot.CanvasGroups.Length; i++)
        {
            if (snapshot.CanvasGroups[i] == null || i >= snapshot.CanvasGroupAlphas.Length)
            {
                continue;
            }

            snapshot.CanvasGroups[i].alpha = snapshot.CanvasGroupAlphas[i];
            snapshot.CanvasGroups[i].interactable = i < snapshot.CanvasGroupInteractableStates.Length && snapshot.CanvasGroupInteractableStates[i];
            snapshot.CanvasGroups[i].blocksRaycasts = i < snapshot.CanvasGroupBlocksRaycastsStates.Length && snapshot.CanvasGroupBlocksRaycastsStates[i];
        }
    }

    private void ShowNoFood()
    {
        ConfigureFoodVisuals();
        WarnMissingFoodSetupOnce();
        foodVisuals.HideAll();
    }

    private void ShowCoveredDinner()
    {
        ConfigureFoodVisuals();
        WarnMissingFoodSetupOnce();

        if (foodVisuals.HasCovered)
        {
            foodVisuals.ShowCovered();
            return;
        }

        foodVisuals.HideAll();
    }

    private void ShowFullFood()
    {
        ConfigureFoodVisuals();
        WarnMissingFoodSetupOnce();

        if (foodVisuals.HasFull)
        {
            foodVisuals.ShowFull();
            return;
        }

        foodVisuals.HideAll();
    }

    private void ShowHalfFoodIfAssigned()
    {
        ConfigureFoodVisuals();

        if (foodVisuals.HasHalf)
        {
            foodVisuals.ShowHalf();
        }
    }

    private void ShowEmptyFoodOrHide()
    {
        ConfigureFoodVisuals();
        WarnMissingFoodSetupOnce();

        if (foodVisuals.HasEmpty)
        {
            foodVisuals.ShowEmpty();
            return;
        }

        foodVisuals.HideAll();
    }

    private void ConfigureFoodVisuals()
    {
        foodVisuals.Configure(coveredDinnerGroup, fullFoodGroup, halfFoodGroup, emptyFoodGroup);
    }

    private void UpdateObjective(string objective)
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearDialogue();
        interactionHUD.ClearClockStrike();
        interactionHUD.ClearPrimaryAction();
        interactionHUD.SetFoundGuests(new List<string>(), 0, 0);
        interactionHUD.SetStatus("Chapter 3");
        interactionHUD.SetObjective(objective);
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (chapterManager != null)
        {
            chapterManager.SetChapterPlayerInputEnabled(enabled);
        }
    }

    private void ResolveReferences(bool createIfMissing)
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

        if (guestSearch == null)
        {
            guestSearch = GetComponent<Chapter2GuestSearchController>();
        }

        if (guestSearch == null)
        {
            guestSearch = FindAnyObjectByType<Chapter2GuestSearchController>(FindObjectsInactive.Include);
        }

        if (guestSearch == null && createIfMissing)
        {
            guestSearch = gameObject.AddComponent<Chapter2GuestSearchController>();
        }

        if (interactionHUD == null)
        {
            interactionHUD = GetComponent<Chapter2InteractionHUD>();
        }

        if (interactionHUD == null)
        {
            interactionHUD = FindAnyObjectByType<Chapter2InteractionHUD>(FindObjectsInactive.Include);
        }

        if (interactionHUD == null && createIfMissing)
        {
            interactionHUD = gameObject.AddComponent<Chapter2InteractionHUD>();
        }

        if (diningRoomAmbienceDirector == null)
        {
            diningRoomAmbienceDirector = FindAnyObjectByType<DiningRoomAmbienceDirector>(FindObjectsInactive.Include);
        }

        ConfigureFoodVisuals();
    }

    private void SetPhase(Chapter3DinnerPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"Chapter 3 dinner phase changed: {currentPhase}", this);
    }

    private float GetCoveredDinnerHoldSeconds()
    {
        return chapterManager != null && chapterManager.DebugFastMode
            ? Mathf.Min(0.15f, coveredDinnerHoldSeconds)
            : Mathf.Max(0f, coveredDinnerHoldSeconds);
    }

    private float GetEatingDurationSeconds()
    {
        return chapterManager != null && chapterManager.DebugFastMode
            ? Mathf.Min(1f, eatingDurationSeconds)
            : Mathf.Max(0.1f, eatingDurationSeconds);
    }

    private float GetFadeFromBlackSeconds()
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 0f;
    }

    private void WarnMissingFoodSetupOnce()
    {
        if (warnedMissingFoodSetup || foodVisuals.HasAnyAssigned)
        {
            return;
        }

        warnedMissingFoodSetup = true;
        Debug.LogWarning(
            "Chapter 3 dinner has no food visual groups assigned. Covered/full/half/empty food beats will be skipped until props are assigned.",
            this);
    }

    private void WarnMissingGuestSetupOnce(string message)
    {
        if (warnedMissingGuestSetup)
        {
            return;
        }

        warnedMissingGuestSetup = true;
        Debug.LogWarning(message, this);
    }

    private void WarnMissingPresentationSetupOnce(string message)
    {
        if (warnedMissingPresentationSetup)
        {
            return;
        }

        warnedMissingPresentationSetup = true;
        Debug.LogWarning(message, this);
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }
}
