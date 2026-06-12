using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Chapter3AbominationDinnerPhase
{
    NotStarted,
    SeatedIdle,
    DinnerServedCovered,
    EatingActive,
    MealFinishedIdle,
    Complete
}

[DisallowMultipleComponent]
public sealed class Chapter3AbominationDinnerController : MonoBehaviour
{
    private const string DefaultDiningRoomId = "Dining Room";
    private const string DefaultPendingFlag = "chapter_03_dinner_pending";
    private const string DefaultCompositeRootName = "DiningRoom_Demo_Ambience";

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private Chapter2Controller chapter2Controller;
    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private AbominationFullFrameAnimator fullFrameAnimator;
    [SerializeField] private DiningRoomAmbienceDirector legacyAmbienceDirector;

    [Header("Chapter")]
    [SerializeField] private string pendingFlag = DefaultPendingFlag;
    [SerializeField] private string diningRoomId = DefaultDiningRoomId;
    [SerializeField] private bool autoStartWhenPendingFlagDetected = true;
    [SerializeField] private Chapter3AbominationDinnerPhase currentPhase = Chapter3AbominationDinnerPhase.NotStarted;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float seatedIdleHoldSeconds = 2f;
    [SerializeField, Min(0f)] private float coveredDinnerHoldSeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float eatingDurationSeconds = 60f;
    [SerializeField, Min(0f)] private float finishedIdleHoldSeconds = 2f;

    [Header("Scene Visibility")]
    [SerializeField] private bool movePlayerToDiningRoomOnStart = true;
    [SerializeField] private bool hideNormalDiningRoomActorsDuringAbomination = true;
    [SerializeField] private bool hideNormalDiningRoomTableDuringAbomination = true;
    [SerializeField] private bool keepCompositeVisibleOnComplete = true;
    [SerializeField] private GameObject normalDiningRoomPeopleGroup;
    [SerializeField] private GameObject normalDiningRoomTableGroup;
    [SerializeField] private GameObject abominationCompositeRoot;

    private readonly List<GameObjectActiveSnapshot> hiddenGroups = new List<GameObjectActiveSnapshot>();
    private readonly List<GuestVisualSnapshot> hiddenGuestVisuals = new List<GuestVisualSnapshot>();
    private Coroutine dinnerRoutine;
    private bool warnedMissingAnimator;
    private bool warnedMissingGuestSetup;

    private sealed class GameObjectActiveSnapshot
    {
        public GameObject Target;
        public bool WasActive;
    }

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

    public Chapter3AbominationDinnerPhase CurrentPhase => currentPhase;
    public string PendingFlag => string.IsNullOrWhiteSpace(pendingFlag) ? DefaultPendingFlag : pendingFlag.Trim();
    public float EatingDurationSeconds => Mathf.Max(0.1f, eatingDurationSeconds);

    private void Reset()
    {
        ResolveReferences(false);
    }

    private void Start()
    {
        ResolveReferences(false);

        if (autoStartWhenPendingFlagDetected &&
            currentPhase == Chapter3AbominationDinnerPhase.NotStarted &&
            chapterManager != null &&
            SameId(chapterManager.CurrentChapterId, PendingFlag))
        {
            BeginAbominationDinner(chapterManager);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(pendingFlag))
        {
            pendingFlag = DefaultPendingFlag;
        }

        if (string.IsNullOrWhiteSpace(diningRoomId))
        {
            diningRoomId = DefaultDiningRoomId;
        }

        seatedIdleHoldSeconds = Mathf.Max(0f, seatedIdleHoldSeconds);
        coveredDinnerHoldSeconds = Mathf.Max(0f, coveredDinnerHoldSeconds);
        eatingDurationSeconds = Mathf.Max(0.1f, eatingDurationSeconds);
        finishedIdleHoldSeconds = Mathf.Max(0f, finishedIdleHoldSeconds);
    }

    public void BeginAbominationDinner(ChapterManager manager = null)
    {
        chapterManager = manager != null ? manager : chapterManager;
        ResolveReferences(true);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        RestoreNormalDiningRoomVisuals();

        if (!Application.isPlaying)
        {
            PrepareAbominationScene();
            EnterSeatedIdle();
            Debug.LogWarning("Chapter 3 Abomination dinner was prepared in edit mode. Enter Play Mode to run the timed sequence.", this);
            return;
        }

        dinnerRoutine = StartCoroutine(RunAbominationDinnerSequence());
    }

    public void BeginChapter3Dinner(ChapterManager manager = null)
    {
        BeginAbominationDinner(manager);
    }

    [ContextMenu("Debug Start Abomination Dinner")]
    public void DebugStartAbominationDinner()
    {
        BeginAbominationDinner(chapterManager);
    }

    [ContextMenu("Debug Skip To Eating")]
    public void DebugSkipToEating()
    {
        ResolveReferences(true);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        PrepareAbominationScene();
        EnterEatingActive();
        SetPlayerInputEnabled(true);
    }

    [ContextMenu("Debug Finish Abomination Dinner")]
    public void DebugFinishAbominationDinner()
    {
        ResolveReferences(true);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        PrepareAbominationScene();
        EnterMealFinishedIdle();
        SetPlayerInputEnabled(true);
    }

    [ContextMenu("Debug Reset Abomination Dinner")]
    public void DebugResetAbominationDinner()
    {
        ResolveReferences(false);

        if (dinnerRoutine != null)
        {
            StopCoroutine(dinnerRoutine);
            dinnerRoutine = null;
        }

        fullFrameAnimator?.StopPlayback();

        if (abominationCompositeRoot != null)
        {
            abominationCompositeRoot.SetActive(false);
        }

        RestoreNormalDiningRoomVisuals();
        SetPhase(Chapter3AbominationDinnerPhase.NotStarted);
        UpdateObjective(string.Empty);
        SetPlayerInputEnabled(true);
    }

    private IEnumerator RunAbominationDinnerSequence()
    {
        SetPlayerInputEnabled(false);
        PrepareAbominationScene();
        EnterSeatedIdle();

        if (introUI != null)
        {
            yield return introUI.FadeFromBlack(GetFadeFromBlackSeconds());
        }

        SetPlayerInputEnabled(true);
        yield return WaitIfNeeded(GetHoldSeconds(seatedIdleHoldSeconds, 0.15f));

        EnterDinnerServedCovered();
        yield return WaitIfNeeded(GetHoldSeconds(coveredDinnerHoldSeconds, 0.15f));

        EnterEatingActive();

        float duration = GetHoldSeconds(eatingDurationSeconds, 1f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        EnterMealFinishedIdle();
        yield return WaitIfNeeded(GetHoldSeconds(finishedIdleHoldSeconds, 0.15f));

        EnterComplete();
        dinnerRoutine = null;
    }

    private IEnumerator WaitIfNeeded(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        yield return new WaitForSeconds(seconds);
    }

    private void PrepareAbominationScene()
    {
        ResolveReferences(true);
        EnsurePlayerInDiningRoom();
        StageGuestsForDiningStateOnly();
        DisableLegacyAmbienceDriver();
        ShowCompositeRoot();
        HideNormalDiningRoomVisuals();
    }

    private void EnterSeatedIdle()
    {
        SetPhase(Chapter3AbominationDinnerPhase.SeatedIdle);
        ShowCompositeRoot();
        HideNormalDiningRoomVisuals();
        PlayFullFramePhase(AbominationFullFramePhase.SeatedIdle);
        UpdateObjective("The guests gather for dinner.");
    }

    private void EnterDinnerServedCovered()
    {
        SetPhase(Chapter3AbominationDinnerPhase.DinnerServedCovered);
        ShowCompositeRoot();
        HideNormalDiningRoomVisuals();
        PlayFullFramePhase(AbominationFullFramePhase.CoveredDinner);
        UpdateObjective("Dinner is served.");
    }

    private void EnterEatingActive()
    {
        SetPhase(Chapter3AbominationDinnerPhase.EatingActive);
        ShowCompositeRoot();
        HideNormalDiningRoomVisuals();
        PlayFullFramePhase(AbominationFullFramePhase.Eating);
        UpdateObjective("The guests begin dinner.");
    }

    private void EnterMealFinishedIdle()
    {
        SetPhase(Chapter3AbominationDinnerPhase.MealFinishedIdle);
        ShowCompositeRoot();
        HideNormalDiningRoomVisuals();
        PlayFullFramePhase(AbominationFullFramePhase.FinishedIdle);
        UpdateObjective("The meal is finished.");
    }

    private void EnterComplete()
    {
        SetPhase(Chapter3AbominationDinnerPhase.Complete);
        fullFrameAnimator?.ShowFirstFrameOfCurrentPhase();

        if (!keepCompositeVisibleOnComplete && abominationCompositeRoot != null)
        {
            abominationCompositeRoot.SetActive(false);
            RestoreNormalDiningRoomVisuals();
        }

        SetPlayerInputEnabled(true);
    }

    private void PlayFullFramePhase(AbominationFullFramePhase phase)
    {
        if (fullFrameAnimator == null)
        {
            WarnMissingAnimatorOnce();
            return;
        }

        switch (phase)
        {
            case AbominationFullFramePhase.SeatedIdle:
                fullFrameAnimator.PlaySeatedIdle();
                break;
            case AbominationFullFramePhase.CoveredDinner:
                fullFrameAnimator.PlayCoveredDinner();
                break;
            case AbominationFullFramePhase.Eating:
                fullFrameAnimator.PlayEatingLoop();
                break;
            case AbominationFullFramePhase.FinishedIdle:
                fullFrameAnimator.PlayFinishedIdle();
                break;
        }
    }

    private void StageGuestsForDiningStateOnly()
    {
        if (guestSearch == null)
        {
            WarnMissingGuestSetupOnce("Chapter 3 Abomination dinner could not find Chapter2GuestSearchController, so guest state could not be staged.");
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

    private void ShowCompositeRoot()
    {
        if (abominationCompositeRoot == null && fullFrameAnimator != null)
        {
            abominationCompositeRoot = fullFrameAnimator.gameObject;
        }

        if (abominationCompositeRoot == null)
        {
            return;
        }

        if (!abominationCompositeRoot.activeSelf)
        {
            abominationCompositeRoot.SetActive(true);
        }
    }

    private void HideNormalDiningRoomVisuals()
    {
        if (hideNormalDiningRoomTableDuringAbomination)
        {
            SetGroupActive(normalDiningRoomTableGroup, false);
        }

        if (hideNormalDiningRoomActorsDuringAbomination)
        {
            SetGroupActive(normalDiningRoomPeopleGroup, false);
            SuppressGuestActorVisuals();
        }
    }

    private void RestoreNormalDiningRoomVisuals()
    {
        for (int i = 0; i < hiddenGuestVisuals.Count; i++)
        {
            RestoreGuestVisualSnapshot(hiddenGuestVisuals[i]);
        }

        hiddenGuestVisuals.Clear();

        for (int i = hiddenGroups.Count - 1; i >= 0; i--)
        {
            GameObjectActiveSnapshot snapshot = hiddenGroups[i];
            if (snapshot != null && snapshot.Target != null)
            {
                snapshot.Target.SetActive(snapshot.WasActive);
            }
        }

        hiddenGroups.Clear();
    }

    private void SetGroupActive(GameObject target, bool active)
    {
        if (target == null || target.activeSelf == active)
        {
            return;
        }

        if (FindGroupSnapshot(target) == null)
        {
            hiddenGroups.Add(new GameObjectActiveSnapshot
            {
                Target = target,
                WasActive = target.activeSelf
            });
        }

        target.SetActive(active);
    }

    private GameObjectActiveSnapshot FindGroupSnapshot(GameObject target)
    {
        for (int i = 0; i < hiddenGroups.Count; i++)
        {
            if (hiddenGroups[i] != null && hiddenGroups[i].Target == target)
            {
                return hiddenGroups[i];
            }
        }

        return null;
    }

    private void SuppressGuestActorVisuals()
    {
        List<ActorRoomState> actors = GetDiningGuestActors();

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

    private List<ActorRoomState> GetDiningGuestActors()
    {
        List<ActorRoomState> actors = new List<ActorRoomState>();
        HashSet<ActorRoomState> knownActors = new HashSet<ActorRoomState>();

        if (guestSearch != null)
        {
            AddActorList(guestSearch.GetFoundActorsInOrder(), actors, knownActors);
            AddActorList(guestSearch.GetGuestActorsInIdentityOrder(), actors, knownActors);
        }

        ActorRoomState[] discoveredActors = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        for (int i = 0; i < discoveredActors.Length; i++)
        {
            ActorRoomState actor = discoveredActors[i];
            if (actor != null && IsLikelyGuestActor(actor) && knownActors.Add(actor))
            {
                actors.Add(actor);
            }
        }

        return actors;
    }

    private static void AddActorList(
        List<ActorRoomState> source,
        List<ActorRoomState> actors,
        HashSet<ActorRoomState> knownActors)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ActorRoomState actor = source[i];
            if (actor != null && IsLikelyGuestActor(actor) && knownActors.Add(actor))
            {
                actors.Add(actor);
            }
        }
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

    private static GuestVisualSnapshot CaptureGuestVisualSnapshot(ActorRoomState actor)
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

    private void DisableLegacyAmbienceDriver()
    {
        if (legacyAmbienceDirector == null && abominationCompositeRoot != null)
        {
            legacyAmbienceDirector = abominationCompositeRoot.GetComponent<DiningRoomAmbienceDirector>();
        }

        if (legacyAmbienceDirector != null)
        {
            legacyAmbienceDirector.enabled = false;
            legacyAmbienceDirector.SetImagesVisible(true);
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

    private void UpdateObjective(string objective)
    {
        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearDialogue();
        interactionHUD.ClearClockStrike();
        interactionHUD.ClearPrimaryAction();
        interactionHUD.SetFoundGuests(Array.Empty<string>(), 0, 0);

        if (string.IsNullOrWhiteSpace(objective))
        {
            interactionHUD.ClearStatus();
            interactionHUD.SetObjective(string.Empty);
            return;
        }

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

        if (fullFrameAnimator == null)
        {
            fullFrameAnimator = FindAnyObjectByType<AbominationFullFrameAnimator>(FindObjectsInactive.Include);
        }

        if (abominationCompositeRoot == null && fullFrameAnimator != null)
        {
            abominationCompositeRoot = fullFrameAnimator.gameObject;
        }

        if (abominationCompositeRoot == null)
        {
            abominationCompositeRoot = FindInactiveObjectByName(DefaultCompositeRootName);
        }

        if (fullFrameAnimator == null && abominationCompositeRoot != null)
        {
            fullFrameAnimator = abominationCompositeRoot.GetComponent<AbominationFullFrameAnimator>();
        }

        if (fullFrameAnimator == null && createIfMissing && abominationCompositeRoot != null)
        {
            fullFrameAnimator = abominationCompositeRoot.AddComponent<AbominationFullFrameAnimator>();
        }

        if (legacyAmbienceDirector == null && abominationCompositeRoot != null)
        {
            legacyAmbienceDirector = abominationCompositeRoot.GetComponent<DiningRoomAmbienceDirector>();
        }

        if (legacyAmbienceDirector == null)
        {
            legacyAmbienceDirector = FindAnyObjectByType<DiningRoomAmbienceDirector>(FindObjectsInactive.Include);
        }
    }

    private void SetPhase(Chapter3AbominationDinnerPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"Chapter 3 Abomination dinner phase changed: {currentPhase}", this);
    }

    private float GetHoldSeconds(float value, float debugFastValue)
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return Mathf.Min(Mathf.Max(0f, debugFastValue), Mathf.Max(0f, value));
        }

        return Mathf.Max(0f, value);
    }

    private float GetFadeFromBlackSeconds()
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 0f;
    }

    private void WarnMissingAnimatorOnce()
    {
        if (warnedMissingAnimator)
        {
            return;
        }

        warnedMissingAnimator = true;
        Debug.LogWarning("Chapter 3 Abomination dinner has no AbominationFullFrameAnimator assigned.", this);
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

    private static GameObject FindInactiveObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static bool IsLikelyGuestActor(ActorRoomState actor)
    {
        if (actor == null)
        {
            return false;
        }

        string actorId = actor.ActorId;
        string objectName = actor.gameObject != null ? actor.gameObject.name : string.Empty;

        if (ContainsAny(actorId, "Player", "Butler", "Monster") ||
            ContainsAny(objectName, "Player", "Butler", "Monster"))
        {
            return false;
        }

        return ContainsAny(actorId, "Guest") || ContainsAny(objectName, "Guest");
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

    private static bool SameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
