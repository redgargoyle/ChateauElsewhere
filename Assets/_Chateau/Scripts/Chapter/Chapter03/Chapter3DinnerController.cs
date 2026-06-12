using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private const string LogPrefix = "[Chapter3Dinner]";

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ChapterIntroUI introUI;
    [SerializeField] private Chapter2Controller chapter2Controller;
    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private DiningFoodVisualState foodVisualState;
    [SerializeField] private DiningTableIdleSceneController diningTableSequence;

    [Header("Chapter")]
    [SerializeField] private string diningRoomId = "Dining Room";
    [SerializeField] private string pendingFlag = ChapterManager.Chapter3PendingId;
    [SerializeField] private Chapter3DinnerPhase currentPhase = Chapter3DinnerPhase.NotStarted;

    [Header("Dining Animation")]
    [SerializeField] private bool useDiningTableImageSequence = false;
    [SerializeField] private bool guestsInteractableDuringDinner = true;
    [SerializeField] private string diningGuestClickTargetName = "Ch3_DiningGuestClickTarget";
    [SerializeField] private Vector2 diningGuestClickFallbackOffset = new Vector2(0f, 1f);
    [SerializeField] private Vector2 diningGuestClickFallbackSize = new Vector2(1.1f, 2f);

    [Header("Timing")]
    [SerializeField, Min(0f)] private float seatedIdleHoldSeconds = 0.5f;
    [SerializeField, Min(0f)] private float coveredDinnerHoldSeconds = 2.5f;
    [SerializeField, Min(0f)] private float eatingDurationSeconds = 60f;
    [SerializeField, Range(0f, 1f)] private float foodHalfwayNormalizedTime = 0.5f;
    [SerializeField] private bool autoStartSequence = true;
    [SerializeField] private bool fadeFromBlackOnStart = true;

    [Header("Food Groups")]
    [SerializeField] private GameObject coveredDinnerGroup;
    [SerializeField] private GameObject fullFoodGroup;
    [SerializeField] private GameObject halfFoodGroup;
    [SerializeField] private GameObject emptyFoodGroup;
    [SerializeField] private bool autoFindFoodGroupsByName = true;
    [SerializeField] private string coveredDinnerObjectName = "Ch3_CoveredDinnerGroup";
    [SerializeField] private string fullFoodObjectName = "Ch3_FullFoodGroup";
    [SerializeField] private string halfFoodObjectName = "Ch3_HalfFoodGroup";
    [SerializeField] private string emptyFoodObjectName = "Ch3_EmptyFoodGroup";

    [Header("Guest Performances")]
    [SerializeField] private List<GuestDiningPerformance> guestPerformances = new List<GuestDiningPerformance>();
    [SerializeField, Min(0.05f)] private float minGuestActionDelay = 1.5f;
    [SerializeField, Min(0.05f)] private float maxGuestActionDelay = 4.5f;

    private Coroutine dinnerRoutine;
    private bool warnedMissingFoodSetup;
    private bool warnedMissingGuestSearch;
    private bool warnedMissingGuests;
    private bool warnedMissingDiningTableSequence;
    private bool warnedMissingDiningClickTarget;

    public Chapter3DinnerPhase CurrentPhase => currentPhase;
    public string PendingFlag => pendingFlag;
    public float EatingDurationSeconds => eatingDurationSeconds;

    private void OnValidate()
    {
        maxGuestActionDelay = Mathf.Max(minGuestActionDelay, maxGuestActionDelay);
        eatingDurationSeconds = Mathf.Max(0f, eatingDurationSeconds);
        coveredDinnerHoldSeconds = Mathf.Max(0f, coveredDinnerHoldSeconds);
        seatedIdleHoldSeconds = Mathf.Max(0f, seatedIdleHoldSeconds);
    }

    private void OnDisable()
    {
        StopDinnerRoutine();
        StopGuestEating();
        SetDiningGuestClickTargetsAvailable(false);
        diningTableSequence?.Hide();
    }

    public void BeginChapter3Dinner(ChapterManager manager = null)
    {
        if (manager != null)
        {
            chapterManager = manager;
        }

        ResolveReferences();
        StopDinnerRoutine();
        StopGuestEating();

        if (!autoStartSequence)
        {
            PrepareSeatedIdlePhase();
            SetPlayerInputEnabled(true);
            Debug.Log($"{LogPrefix} Chapter 3 staged in seated idle. autoStartSequence is disabled.", this);
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
        ResolveReferences();
        StopDinnerRoutine();
        FinishMeal(false);
    }

    private IEnumerator RunDinnerSequence()
    {
        SetPlayerInputEnabled(false);
        PrepareSeatedIdlePhase();

        if (fadeFromBlackOnStart && introUI != null)
        {
            yield return introUI.FadeFromBlack(GetFadeSeconds());
        }
        else if (introUI != null)
        {
            introUI.HideOverlay();
        }

        SetPlayerInputEnabled(true);

        if (seatedIdleHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(seatedIdleHoldSeconds);
        }

        SetPhase(Chapter3DinnerPhase.DinnerServedCovered);
        UpdateObjective("Dinner is served.");
        ShowFoodCovered();

        if (coveredDinnerHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(coveredDinnerHoldSeconds);
        }

        SetPhase(Chapter3DinnerPhase.EatingActive);
        UpdateObjective("Dinner is served.");
        ShowFoodFull();
        BeginGuestEating();

        yield return RunEatingTimer();
        FinishMeal(false);
        dinnerRoutine = null;
    }

    private void PrepareSeatedIdlePhase()
    {
        ResolveReferences();
        HideChapter2DiningStillIfNeeded();
        diningTableSequence?.Hide();
        EnsureDiningRoomIsActive();
        SeatGuestsUsingExistingController();
        ResolveGuestPerformances();
        PrepareGuestsForSeatedIdle();
        EnsureDiningGuestClickTargets();
        HideAllFood();
        ShowDiningTableSequenceIfNeeded();
        SetPhase(Chapter3DinnerPhase.SeatedIdle);
        UpdateObjective("Dinner is served.");
        Debug.Log($"{LogPrefix} Chapter 3 dinner seated idle is ready.", this);
    }

    private IEnumerator RunEatingTimer()
    {
        float duration = Mathf.Max(0f, eatingDurationSeconds);
        float halfwayTime = duration * Mathf.Clamp01(foodHalfwayNormalizedTime);
        bool showedHalf = false;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (!showedHalf && CanShowHalfFood() && elapsed >= halfwayTime)
            {
                showedHalf = true;
                ShowFoodHalf();
            }

            yield return null;
        }
    }

    private void FinishMeal(bool markComplete)
    {
        StopGuestEating();
        PrepareGuestsForSeatedIdle();
        ShowFoodEmpty();
        ShowDiningTableSequenceIfNeeded();
        UpdateObjective("The meal is finished.");
        SetPhase(Chapter3DinnerPhase.MealFinishedIdle);
        SetPlayerInputEnabled(true);

        if (markComplete)
        {
            SetPhase(Chapter3DinnerPhase.Complete);
        }

        Debug.Log($"{LogPrefix} Chapter 3 dinner meal finished.", this);
    }

    private void SeatGuestsUsingExistingController()
    {
        if (guestSearch == null)
        {
            WarnMissingGuestSearchOnce();
            return;
        }

        guestSearch.AutoDiscoverGuestsIfNeeded();

        if (guestSearch.GuestCount == 0)
        {
            WarnMissingGuestsOnce();
            return;
        }

        if (!guestSearch.AllGuestsFound)
        {
            Debug.LogWarning(
                $"{LogPrefix} Chapter 3 started before all guests were marked found. " +
                "Staging all discovered guests so the dinner sequence can run.",
                this);
            guestSearch.DebugStageAllGuestsFoundForChapter3Skip();
            return;
        }

        guestSearch.SeatGuestsInDiningRoom();
    }

    private void ResolveGuestPerformances()
    {
        if (guestPerformances == null)
        {
            guestPerformances = new List<GuestDiningPerformance>();
        }

        guestPerformances.RemoveAll(performance => performance == null);
        List<ActorRoomState> guestActors = GetGuestActors();

        for (int i = 0; i < guestActors.Count; i++)
        {
            ActorRoomState actor = guestActors[i];

            if (actor == null)
            {
                continue;
            }

            GuestDiningPerformance performance = actor.GetComponent<GuestDiningPerformance>();

            if (performance == null)
            {
                performance = actor.GetComponentInChildren<GuestDiningPerformance>(true);
            }

            if (performance == null)
            {
                performance = actor.gameObject.AddComponent<GuestDiningPerformance>();
            }

            performance.AssignActorStateIfMissing(actor);
            performance.ConfigureActionDelays(minGuestActionDelay, maxGuestActionDelay);

            if (!guestPerformances.Contains(performance))
            {
                guestPerformances.Add(performance);
            }
        }

        if (guestPerformances.Count == 0)
        {
            WarnMissingGuestsOnce();
        }
    }

    private List<ActorRoomState> GetGuestActors()
    {
        if (guestSearch != null)
        {
            return guestSearch.GetGuestActorsInIdentityOrder();
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        List<ActorRoomState> actors = new List<ActorRoomState>();

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actor = actorStates[i];

            if (actor != null && IsLikelyGuestActor(actor))
            {
                actors.Add(actor);
            }
        }

        actors.Sort(CompareActorsById);
        return actors;
    }

    private void PrepareGuestsForSeatedIdle()
    {
        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].SetDinnerInteractable(guestsInteractableDuringDinner);
                guestPerformances[i].PrepareSeatedIdle();
            }
        }

        EnsureDiningGuestClickTargets();
    }

    private void BeginGuestEating()
    {
        if (useDiningTableImageSequence)
        {
            ShowDiningTableSequenceIfNeeded();
            return;
        }

        EnsureDiningGuestClickTargets();

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].SetDinnerInteractable(guestsInteractableDuringDinner);
                guestPerformances[i].BeginEating();
            }
        }
    }

    private void StopGuestEating()
    {
        if (guestPerformances == null)
        {
            return;
        }

        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
                guestPerformances[i].SetDinnerInteractable(guestsInteractableDuringDinner);
                guestPerformances[i].StopEatingAndIdle();
            }
        }
    }

    public void HandleDiningGuestClicked(ActorRoomState actor)
    {
        if (actor == null || !actor.IsInteractable || !actor.IsVisibleInCurrentRoom)
        {
            return;
        }

        ResolveReferences();

        if (interactionHUD == null)
        {
            return;
        }

        interactionHUD.ClearPrimaryAction();
        interactionHUD.ClearStatus();
        interactionHUD.SetObjective("Dinner is served.");
        interactionHUD.SetDialogue(GetGuestDisplayName(actor), GetDiningGuestObservation(actor));
        interactionHUD.SetDialogueChoices("Continue", () => interactionHUD.ClearDialogue());
    }

    private void HideChapter2DiningStillIfNeeded()
    {
        if (useDiningTableImageSequence || chapter2Controller == null)
        {
            return;
        }

        chapter2Controller.HideDiningTableIdleScene();
    }

    private void ShowDiningTableSequenceIfNeeded()
    {
        if (!useDiningTableImageSequence)
        {
            return;
        }

        if (diningTableSequence == null)
        {
            ResolveReferences();
        }

        if (diningTableSequence == null)
        {
            WarnMissingDiningTableSequenceOnce();
            return;
        }

        diningTableSequence.Show(GetGuestActors());
    }

    private void EnsureDiningGuestClickTargets()
    {
        List<ActorRoomState> guestActors = GetGuestActors();

        if (guestActors.Count == 0)
        {
            WarnMissingGuestsOnce();
            return;
        }

        for (int i = 0; i < guestActors.Count; i++)
        {
            ActorRoomState actor = guestActors[i];

            if (actor == null || actor.gameObject == null)
            {
                continue;
            }

            Transform clickTarget = FindOrCreateDiningGuestClickTarget(actor);

            if (clickTarget == null)
            {
                WarnMissingDiningClickTargetOnce(actor);
                continue;
            }

            BoxCollider2D collider = clickTarget.GetComponent<BoxCollider2D>();

            if (collider == null)
            {
                collider = clickTarget.gameObject.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            ResizeDiningGuestClickTarget(actor, clickTarget, collider);

            Chapter3DiningGuestAction action = clickTarget.GetComponent<Chapter3DiningGuestAction>();

            if (action == null)
            {
                action = clickTarget.gameObject.AddComponent<Chapter3DiningGuestAction>();
            }

            action.Initialize(this, actor);
            action.SetAvailable(guestsInteractableDuringDinner);
            clickTarget.gameObject.SetActive(true);

            actor.SetInteractable(guestsInteractableDuringDinner);
            actor.ApplyState();
        }

        if (Application.isPlaying)
        {
            Physics2D.SyncTransforms();
        }
    }

    private void SetDiningGuestClickTargetsAvailable(bool available)
    {
        Chapter3DiningGuestAction[] actions = FindObjectsByType<Chapter3DiningGuestAction>(FindObjectsInactive.Include);

        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] != null)
            {
                actions[i].SetAvailable(available);
            }
        }
    }

    private Transform FindOrCreateDiningGuestClickTarget(ActorRoomState actor)
    {
        if (actor == null || actor.gameObject == null)
        {
            return null;
        }

        Transform actorTransform = actor.gameObject.transform;
        Transform[] childTransforms = actorTransform.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];

            if (child != null &&
                child != actorTransform &&
                string.Equals(child.name, diningGuestClickTargetName, StringComparison.OrdinalIgnoreCase))
            {
                child.SetParent(actorTransform, false);
                return child;
            }
        }

        GameObject targetObject = new GameObject(diningGuestClickTargetName);
        Transform target = targetObject.transform;
        target.SetParent(actorTransform, false);
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        return target;
    }

    private void ResizeDiningGuestClickTarget(ActorRoomState actor, Transform clickTarget, BoxCollider2D collider)
    {
        if (actor == null || clickTarget == null || collider == null)
        {
            return;
        }

        Vector2 nextOffset = diningGuestClickFallbackOffset;
        Vector2 nextSize = diningGuestClickFallbackSize;

        if (TryGetDiningGuestRendererBounds(actor.gameObject, clickTarget, out Bounds bounds))
        {
            nextOffset = clickTarget.InverseTransformPoint(bounds.center);
            nextSize = GetLocalBoundsSize(clickTarget, bounds);
            nextSize = new Vector2(
                Mathf.Max(diningGuestClickFallbackSize.x, nextSize.x * 1.1f),
                Mathf.Max(diningGuestClickFallbackSize.y, nextSize.y * 1.1f));
        }

        collider.offset = nextOffset;
        collider.size = nextSize;
    }

    private static bool TryGetDiningGuestRendererBounds(GameObject actorObject, Transform clickTarget, out Bounds bounds)
    {
        bounds = default;

        if (actorObject == null)
        {
            return false;
        }

        Renderer[] renderers = actorObject.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                renderer.transform == null ||
                IsUnderTransform(renderer.transform, clickTarget) ||
                !IsUsableBounds(renderer.bounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds && IsUsableBounds(bounds);
    }

    private static Vector2 GetLocalBoundsSize(Transform targetTransform, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, worldBounds.center.z),
            new Vector3(min.x, max.y, worldBounds.center.z),
            new Vector3(max.x, min.y, worldBounds.center.z),
            new Vector3(max.x, max.y, worldBounds.center.z)
        };

        Vector2 localMin = targetTransform.InverseTransformPoint(corners[0]);
        Vector2 localMax = localMin;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 localPoint = targetTransform.InverseTransformPoint(corners[i]);
            localMin = Vector2.Min(localMin, localPoint);
            localMax = Vector2.Max(localMax, localPoint);
        }

        return localMax - localMin;
    }

    private static bool IsUnderTransform(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
            (candidate == root || candidate.IsChildOf(root));
    }

    private static bool IsUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return size.x > 0.001f && size.y > 0.001f;
    }

    private void EnsureDiningRoomIsActive()
    {
        if (navigationManager == null || string.IsNullOrWhiteSpace(diningRoomId))
        {
            Debug.LogWarning($"{LogPrefix} Cannot ensure Dining Room because navigation or diningRoomId is missing.", this);
            return;
        }

        if (string.Equals(navigationManager.CurrentRoom, diningRoomId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!navigationManager.MoveToRoom(diningRoomId))
        {
            Debug.LogWarning($"{LogPrefix} Could not move to room '{diningRoomId}'.", this);
        }
    }

    private void ShowFoodCovered()
    {
        ResolveFoodReferences();

        if (foodVisualState != null)
        {
            foodVisualState.ShowCovered();
        }
        else
        {
            SetFoodGroups(coveredDinnerGroup, fullFoodGroup, halfFoodGroup, emptyFoodGroup);
        }

        WarnMissingFoodSetupIfNeeded();
    }

    private void ShowFoodFull()
    {
        ResolveFoodReferences();

        if (foodVisualState != null)
        {
            foodVisualState.ShowFull();
        }
        else
        {
            SetFoodGroups(fullFoodGroup, coveredDinnerGroup, halfFoodGroup, emptyFoodGroup);
        }

        WarnMissingFoodSetupIfNeeded();
    }

    private void ShowFoodHalf()
    {
        ResolveFoodReferences();

        if (foodVisualState != null)
        {
            foodVisualState.ShowHalf();
        }
        else
        {
            SetFoodGroups(halfFoodGroup, coveredDinnerGroup, fullFoodGroup, emptyFoodGroup);
        }
    }

    private void ShowFoodEmpty()
    {
        ResolveFoodReferences();

        if (foodVisualState != null)
        {
            foodVisualState.ShowEmpty();
        }
        else
        {
            SetFoodGroups(emptyFoodGroup, coveredDinnerGroup, fullFoodGroup, halfFoodGroup);
        }

        WarnMissingFoodSetupIfNeeded();
    }

    private void HideAllFood()
    {
        ResolveFoodReferences();

        if (foodVisualState != null)
        {
            foodVisualState.HideAll();
            return;
        }

        SetActiveSafe(coveredDinnerGroup, false);
        SetActiveSafe(fullFoodGroup, false);
        SetActiveSafe(halfFoodGroup, false);
        SetActiveSafe(emptyFoodGroup, false);
    }

    private void SetFoodGroups(GameObject activeGroup, params GameObject[] inactiveGroups)
    {
        SetActiveSafe(activeGroup, true);

        if (inactiveGroups == null)
        {
            return;
        }

        for (int i = 0; i < inactiveGroups.Length; i++)
        {
            SetActiveSafe(inactiveGroups[i], false);
        }
    }

    private void ResolveFoodReferences()
    {
        if (autoFindFoodGroupsByName)
        {
            coveredDinnerGroup = coveredDinnerGroup != null
                ? coveredDinnerGroup
                : FindSceneObjectByName(coveredDinnerObjectName);
            fullFoodGroup = fullFoodGroup != null
                ? fullFoodGroup
                : FindSceneObjectByName(fullFoodObjectName);
            halfFoodGroup = halfFoodGroup != null
                ? halfFoodGroup
                : FindSceneObjectByName(halfFoodObjectName);
            emptyFoodGroup = emptyFoodGroup != null
                ? emptyFoodGroup
                : FindSceneObjectByName(emptyFoodObjectName);
        }

        if (foodVisualState != null)
        {
            foodVisualState.ConfigureIfMissing(
                coveredDinnerGroup,
                fullFoodGroup,
                halfFoodGroup,
                emptyFoodGroup);
        }
    }

    private bool HasAnyFoodReference()
    {
        return coveredDinnerGroup != null ||
            fullFoodGroup != null ||
            halfFoodGroup != null ||
            emptyFoodGroup != null ||
            (foodVisualState != null && foodVisualState.HasAnyFoodReference);
    }

    private bool CanShowHalfFood()
    {
        return halfFoodGroup != null ||
            (foodVisualState != null && foodVisualState.HasHalfFoodReference);
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
        interactionHUD.ClearStatus();
        interactionHUD.SetObjective(objective);
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

        if (chapter2Controller == null)
        {
            chapter2Controller = GetComponent<Chapter2Controller>();
        }

        if (chapter2Controller == null)
        {
            chapter2Controller = FindAnyObjectByType<Chapter2Controller>(FindObjectsInactive.Include);
        }

        if (guestSearch == null)
        {
            guestSearch = GetComponent<Chapter2GuestSearchController>();
        }

        if (guestSearch == null)
        {
            guestSearch = FindAnyObjectByType<Chapter2GuestSearchController>(FindObjectsInactive.Include);
        }

        if (interactionHUD == null)
        {
            interactionHUD = GetComponent<Chapter2InteractionHUD>();
        }

        if (interactionHUD == null)
        {
            interactionHUD = FindAnyObjectByType<Chapter2InteractionHUD>(FindObjectsInactive.Include);
        }

        if (foodVisualState == null)
        {
            foodVisualState = GetComponent<DiningFoodVisualState>();
        }

        if (diningTableSequence == null)
        {
            diningTableSequence = GetComponent<DiningTableIdleSceneController>();
        }

        if (diningTableSequence == null)
        {
            diningTableSequence = FindAnyObjectByType<DiningTableIdleSceneController>(FindObjectsInactive.Include);
        }

        if (diningTableSequence == null && useDiningTableImageSequence)
        {
            diningTableSequence = gameObject.AddComponent<DiningTableIdleSceneController>();
        }

        ResolveFoodReferences();
    }

    private void StopDinnerRoutine()
    {
        if (dinnerRoutine == null)
        {
            return;
        }

        StopCoroutine(dinnerRoutine);
        dinnerRoutine = null;
    }

    private void SetPhase(Chapter3DinnerPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"{LogPrefix} phase changed: {currentPhase}", this);
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (chapterManager != null)
        {
            chapterManager.SetChapterPlayerInputEnabled(enabled);
        }
    }

    private float GetFadeSeconds()
    {
        if (chapterManager != null && chapterManager.DebugFastMode)
        {
            return 0.15f;
        }

        return introUI != null ? introUI.FadeFromBlackSeconds : 1.5f;
    }

    private void WarnMissingFoodSetupIfNeeded()
    {
        if (warnedMissingFoodSetup || HasAnyFoodReference())
        {
            return;
        }

        warnedMissingFoodSetup = true;
        Debug.LogWarning(
            $"{LogPrefix} No Chapter 3 food props are assigned. Assign coveredDinnerGroup, " +
            "fullFoodGroup, halfFoodGroup, and emptyFoodGroup in the Inspector, or name scene " +
            "objects Ch3_CoveredDinnerGroup/Ch3_FullFoodGroup/Ch3_HalfFoodGroup/Ch3_EmptyFoodGroup.",
            this);
    }

    private void WarnMissingGuestSearchOnce()
    {
        if (warnedMissingGuestSearch)
        {
            return;
        }

        warnedMissingGuestSearch = true;
        Debug.LogWarning($"{LogPrefix} Chapter2GuestSearchController is missing; Chapter 3 cannot reuse dining seating.", this);
    }

    private void WarnMissingGuestsOnce()
    {
        if (warnedMissingGuests)
        {
            return;
        }

        warnedMissingGuests = true;
        Debug.LogWarning($"{LogPrefix} No guest ActorRoomState objects were found for Chapter 3 dinner.", this);
    }

    private void WarnMissingDiningTableSequenceOnce()
    {
        if (warnedMissingDiningTableSequence)
        {
            return;
        }

        warnedMissingDiningTableSequence = true;
        Debug.LogWarning($"{LogPrefix} DiningTableIdleSceneController is missing; Chapter 3 cannot show the dining animation sequence.", this);
    }

    private void WarnMissingDiningClickTargetOnce(ActorRoomState actor)
    {
        if (warnedMissingDiningClickTarget)
        {
            return;
        }

        warnedMissingDiningClickTarget = true;
        string actorName = actor != null ? actor.ActorId : "<unknown>";
        Debug.LogWarning($"{LogPrefix} Could not create a dining click target for guest '{actorName}'.", this);
    }

    private static string GetGuestDisplayName(ActorRoomState actor)
    {
        if (actor == null)
        {
            return "Guest";
        }

        string actorId = actor.ActorId;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return actor.name;
        }

        return actorId
            .Replace('_', ' ')
            .Replace("Guest01", "Guest 1")
            .Replace("Guest02", "Guest 2")
            .Replace("Guest03", "Guest 3")
            .Replace("Guest04", "Guest 4")
            .Replace("Guest05", "Guest 5")
            .Replace("Guest06", "Guest 6")
            .Replace("Guest07", "Guest 7")
            .Replace("Guest08", "Guest 8")
            .Trim();
    }

    private static string GetDiningGuestObservation(ActorRoomState actor)
    {
        string actorId = actor != null ? actor.ActorId : string.Empty;
        int lineIndex = Mathf.Abs(actorId.GetHashCode()) % 4;

        switch (lineIndex)
        {
            case 0:
                return "They pause over their plate, listening to the conversation around the table.";
            case 1:
                return "They take a careful bite and glance toward the other guests.";
            case 2:
                return "They murmur something polite, then return to the meal.";
            default:
                return "They sit poised at dinner, alert but trying not to show it.";
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate == null ||
                !candidate.gameObject.scene.IsValid() ||
                !string.Equals(candidate.name, objectName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static bool IsLikelyGuestActor(ActorRoomState actor)
    {
        if (actor == null || actor.gameObject == null)
        {
            return false;
        }

        return ContainsGuest(actor.ActorId) || ContainsGuest(actor.gameObject.name);
    }

    private static bool ContainsGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CompareActorsById(ActorRoomState left, ActorRoomState right)
    {
        string leftId = left != null ? left.ActorId : string.Empty;
        string rightId = right != null ? right.ActorId : string.Empty;
        return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }
}
