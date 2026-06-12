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

    [Header("Chapter")]
    [SerializeField] private string diningRoomId = "Dining Room";
    [SerializeField] private string pendingFlag = ChapterManager.Chapter3PendingId;
    [SerializeField] private Chapter3DinnerPhase currentPhase = Chapter3DinnerPhase.NotStarted;

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
        HideChapter2DiningStill();
        EnsureDiningRoomIsActive();
        SeatGuestsUsingExistingController();
        ResolveGuestPerformances();
        PrepareGuestsForSeatedIdle();
        HideAllFood();
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
                guestPerformances[i].PrepareSeatedIdle();
            }
        }
    }

    private void BeginGuestEating()
    {
        for (int i = 0; i < guestPerformances.Count; i++)
        {
            if (guestPerformances[i] != null)
            {
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
                guestPerformances[i].StopEatingAndIdle();
            }
        }
    }

    private void HideChapter2DiningStill()
    {
        if (chapter2Controller == null)
        {
            return;
        }

        chapter2Controller.HideDiningTableIdleScene();
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
