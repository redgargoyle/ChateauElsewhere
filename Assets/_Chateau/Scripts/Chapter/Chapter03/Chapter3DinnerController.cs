using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Chapter3DinnerPhase
{
    NotStarted,
    BuildLayeredDinner,
    SeatedIdle,
    DinnerServedCovered,
    EatingActive,
    MealFinishedIdle,
    Complete
}

[DisallowMultipleComponent]
public sealed class Chapter3DinnerController : MonoBehaviour
{
    private const string LogPrefix = "[Ch3Dining]";
    private const string DebugHudCanvasName = "Ch3_Dining_DebugHUD";

    [Header("References")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2InteractionHUD interactionHUD;
    [SerializeField] private Chapter3LayeredDinnerBuilder dinnerBuilder;
    [SerializeField] private Chapter3DiningTableForegroundOccluder tableForegroundOccluder;

    [Header("Room")]
    [SerializeField] private string diningRoomId = "Dining Room";

    [Header("Timing")]
    [SerializeField] private float seatedIdleSeconds = 1f;
    [SerializeField] private float coveredDinnerHoldSeconds = 1.5f;
    [SerializeField] private float eatingDurationSeconds = 60f;
    [SerializeField, Range(0.05f, 0.95f)] private float foodHalfwayNormalizedTime = 0.5f;

    [Header("Debug")]
    [SerializeField] private Chapter3DinnerPhase currentPhase;
    [SerializeField] private bool debugFastMode;
    [SerializeField] private bool showDebugHud = true;

    private readonly List<Chapter3GuestVisualSuppressor> suppressors = new List<Chapter3GuestVisualSuppressor>();
    private readonly List<Chapter3LayeredSeatAnimator> seatAnimators = new List<Chapter3LayeredSeatAnimator>();
    private Coroutine routine;
    private Coroutine eatingWatchdogRoutine;
    private Canvas debugCanvas;
    private Text debugText;

    public Chapter3DinnerPhase CurrentPhase => currentPhase;
    public int SeatAnimatorCount => seatAnimators.Count;
    public int TotalSeatActions => GetTotalSeatActions();
    public int TotalSpriteFramesApplied => GetTotalSpriteFramesApplied();

    private void OnValidate()
    {
        seatedIdleSeconds = Mathf.Max(0f, seatedIdleSeconds);
        coveredDinnerHoldSeconds = Mathf.Max(0f, coveredDinnerHoldSeconds);
        eatingDurationSeconds = Mathf.Max(0f, eatingDurationSeconds);
    }

    private void LateUpdate()
    {
        if (currentPhase == Chapter3DinnerPhase.NotStarted)
        {
            return;
        }

        for (int i = 0; i < suppressors.Count; i++)
        {
            if (suppressors[i] != null)
            {
                suppressors[i].Suppress();
            }
        }

        tableForegroundOccluder?.EnsureOccluder();
    }

    private void OnDisable()
    {
        StopRoutine();
        StopSeatEating();
        tableForegroundOccluder?.HideOccluder();
    }

    public void BeginChapter3Dinner(ChapterManager manager = null)
    {
        if (manager != null)
        {
            chapterManager = manager;
        }

        ResolveReferences();
        StopRoutine();
        routine = StartCoroutine(RunDinnerRoutine());
    }

    public void FinishDinnerImmediately()
    {
        ResolveReferences();
        StopRoutine();
        FinishDinner(false);
    }

    [ContextMenu("Debug Start Chapter 3 Dinner")]
    private void DebugStartChapter3Dinner()
    {
        BeginChapter3Dinner(chapterManager);
    }

    [ContextMenu("Debug Jump To Eating Active")]
    public void DebugJumpToEatingActive()
    {
        ResolveReferences();
        StopRoutine();

        if (!PrepareLayeredDinner())
        {
            SetPlayerInputEnabled(true);
            return;
        }

        SetPhase(Chapter3DinnerPhase.EatingActive);
        dinnerBuilder.ShowDinner(true);
        dinnerBuilder.FoodState?.ShowFull();
        UpdateObjective("The guests begin to eat.");
        BeginSeatEating(true);
        StartEatingWatchdog();
        SetPlayerInputEnabled(true);
    }

    [ContextMenu("Debug Finish Dinner Immediately")]
    private void DebugFinishDinnerImmediately()
    {
        FinishDinnerImmediately();
    }

    private IEnumerator RunDinnerRoutine()
    {
        ResolveReferences();
        SetPlayerInputEnabled(false);

        if (!PrepareLayeredDinner())
        {
            SetPlayerInputEnabled(true);
            routine = null;
            yield break;
        }

        SetPhase(Chapter3DinnerPhase.SeatedIdle);
        dinnerBuilder.ShowDinner(true);
        dinnerBuilder.FoodState?.HideAll();
        PlayAllSeatsIdle();
        UpdateObjective("The guests take their seats.");

        if (GetMaybeFastDuration(seatedIdleSeconds) > 0f)
        {
            yield return new WaitForSeconds(GetMaybeFastDuration(seatedIdleSeconds));
        }

        SetPhase(Chapter3DinnerPhase.DinnerServedCovered);
        dinnerBuilder.FoodState?.ShowCovered();
        UpdateObjective("Dinner is served.");

        if (GetMaybeFastDuration(coveredDinnerHoldSeconds) > 0f)
        {
            yield return new WaitForSeconds(GetMaybeFastDuration(coveredDinnerHoldSeconds));
        }

        SetPhase(Chapter3DinnerPhase.EatingActive);
        dinnerBuilder.FoodState?.ShowFull();
        UpdateObjective("The guests begin to eat.");
        BeginSeatEating(false);
        StartEatingWatchdog();

        float eatingDuration = GetEatingDuration();
        float firstLeg = eatingDuration * foodHalfwayNormalizedTime;
        float secondLeg = Mathf.Max(0f, eatingDuration - firstLeg);

        if (firstLeg > 0f)
        {
            yield return new WaitForSeconds(firstLeg);
        }

        dinnerBuilder.FoodState?.ShowHalf();

        if (secondLeg > 0f)
        {
            yield return new WaitForSeconds(secondLeg);
        }

        dinnerBuilder.FoodState?.ShowEmpty();
        FinishDinner(false);
        routine = null;
    }

    private bool PrepareLayeredDinner()
    {
        SetPhase(Chapter3DinnerPhase.BuildLayeredDinner);
        MoveToDiningRoom();

        if (dinnerBuilder == null)
        {
            dinnerBuilder = gameObject.AddComponent<Chapter3LayeredDinnerBuilder>();
        }

        if (!dinnerBuilder.BuildOrRefresh())
        {
            Debug.LogError($"{LogPrefix} Could not build layered Chapter 3 dinner. Missing registered full-canvas art is the likely cause.", this);
            UpdateDebugHud();
            return false;
        }

        SeatStoryGuests();
        SuppressStoryGuestVisuals();
        EnsureTableForegroundOccluder();
        RefreshSeatAnimators();
        EnsureDebugHud();
        UpdateDebugHud();
        return true;
    }

    private void EnsureTableForegroundOccluder()
    {
        if (tableForegroundOccluder == null)
        {
            tableForegroundOccluder = GetComponent<Chapter3DiningTableForegroundOccluder>();
        }

        if (tableForegroundOccluder == null)
        {
            tableForegroundOccluder = gameObject.AddComponent<Chapter3DiningTableForegroundOccluder>();
        }

        tableForegroundOccluder.EnsureOccluder();
    }

    private void SeatStoryGuests()
    {
        if (guestSearch == null)
        {
            Debug.LogWarning($"{LogPrefix} No Chapter2GuestSearchController found; layered dinner will still render but old guest actors cannot be staged.", this);
            return;
        }

        guestSearch.AutoDiscoverGuestsIfNeeded();
        guestSearch.SeatGuestsInDiningRoom();
    }

    private void SuppressStoryGuestVisuals()
    {
        suppressors.Clear();
        List<ActorRoomState> actors = GetGuestActors();

        for (int i = 0; i < actors.Count; i++)
        {
            ActorRoomState actor = actors[i];

            if (actor == null)
            {
                continue;
            }

            actor.SetSeated(true);
            actor.SetInteractable(false);
            actor.SetVisibleByChapterState(true);
            actor.SetAvailableInCurrentChapter(true);
            actor.ApplyState();

            Chapter3GuestVisualSuppressor suppressor = actor.GetComponent<Chapter3GuestVisualSuppressor>();

            if (suppressor == null)
            {
                suppressor = actor.gameObject.AddComponent<Chapter3GuestVisualSuppressor>();
            }

            suppressor.Initialize(actor);
            suppressor.Suppress();
            suppressors.Add(suppressor);
        }
    }

    private List<ActorRoomState> GetGuestActors()
    {
        if (guestSearch != null)
        {
            return guestSearch.GetGuestActorsInIdentityOrder();
        }

        ActorRoomState[] allActors = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        List<ActorRoomState> actors = new List<ActorRoomState>();

        for (int i = 0; i < allActors.Length; i++)
        {
            ActorRoomState actor = allActors[i];

            if (actor != null &&
                (ContainsGuest(actor.ActorId) || ContainsGuest(actor.gameObject.name)))
            {
                actors.Add(actor);
            }
        }

        return actors;
    }

    private void RefreshSeatAnimators()
    {
        seatAnimators.Clear();

        if (dinnerBuilder == null)
        {
            return;
        }

        IReadOnlyList<Chapter3LayeredSeatAnimator> builderAnimators = dinnerBuilder.SeatAnimators;

        for (int i = 0; i < builderAnimators.Count; i++)
        {
            if (builderAnimators[i] != null)
            {
                seatAnimators.Add(builderAnimators[i]);
            }
        }
    }

    private void PlayAllSeatsIdle()
    {
        for (int i = 0; i < seatAnimators.Count; i++)
        {
            if (seatAnimators[i] != null)
            {
                seatAnimators[i].PlayIdle();
            }
        }
    }

    private void BeginSeatEating(bool immediate)
    {
        for (int i = 0; i < seatAnimators.Count; i++)
        {
            if (seatAnimators[i] == null)
            {
                continue;
            }

            float initialDelay = immediate
                ? 0f
                : i * 0.17f + Random.Range(0f, 0.15f);
            seatAnimators[i].BeginEating(initialDelay);
        }
    }

    private void StopSeatEating()
    {
        for (int i = 0; i < seatAnimators.Count; i++)
        {
            if (seatAnimators[i] != null)
            {
                seatAnimators[i].StopEatingAndIdle();
            }
        }
    }

    private void FinishDinner(bool markComplete)
    {
        StopEatingWatchdog();
        StopSeatEating();

        if (dinnerBuilder != null)
        {
            dinnerBuilder.ShowDinner(true);
            dinnerBuilder.FoodState?.ShowEmpty();
        }

        UpdateObjective("The meal is finished.");
        SetPhase(markComplete ? Chapter3DinnerPhase.Complete : Chapter3DinnerPhase.MealFinishedIdle);
        SetPlayerInputEnabled(true);
        UpdateDebugHud();
    }

    private void StartEatingWatchdog()
    {
        StopEatingWatchdog();
        eatingWatchdogRoutine = StartCoroutine(EatingWatchdog());
    }

    private IEnumerator EatingWatchdog()
    {
        int startActions = GetTotalSeatActions();
        yield return new WaitForSeconds(2f);

        if (currentPhase == Chapter3DinnerPhase.EatingActive && GetTotalSeatActions() <= startActions)
        {
            Debug.LogError("[Ch3Dining][ERROR] EatingActive started but no seat animator has played a visible action.", this);
        }
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopEatingWatchdog();
    }

    private void StopEatingWatchdog()
    {
        if (eatingWatchdogRoutine != null)
        {
            StopCoroutine(eatingWatchdogRoutine);
            eatingWatchdogRoutine = null;
        }
    }

    private void MoveToDiningRoom()
    {
        if (navigationManager == null)
        {
            return;
        }

        bool moved = navigationManager.DebugTeleportToRoom(diningRoomId);

        if (!moved)
        {
            moved = navigationManager.MoveToRoom(diningRoomId);
        }

        if (!moved)
        {
            Debug.LogWarning($"{LogPrefix} Could not move to '{diningRoomId}'.", this);
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
        interactionHUD.ClearStatus();
        interactionHUD.SetObjective(objective);
    }

    private void SetPhase(Chapter3DinnerPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        currentPhase = nextPhase;
        Debug.Log($"{LogPrefix} Phase changed: {currentPhase}", this);
        UpdateDebugHud();
    }

    private int GetTotalSeatActions()
    {
        int total = 0;

        for (int i = 0; i < seatAnimators.Count; i++)
        {
            if (seatAnimators[i] != null)
            {
                total += seatAnimators[i].DebugActions;
            }
        }

        return total;
    }

    private int GetTotalSpriteFramesApplied()
    {
        int total = 0;

        for (int i = 0; i < seatAnimators.Count; i++)
        {
            if (seatAnimators[i] != null)
            {
                total += seatAnimators[i].DebugFramesApplied;
            }
        }

        return total;
    }

    private void EnsureDebugHud()
    {
        if (!showDebugHud)
        {
            return;
        }

        if (debugCanvas == null)
        {
            GameObject canvasObject = GameObject.Find(DebugHudCanvasName);

            if (canvasObject == null)
            {
                canvasObject = new GameObject(DebugHudCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            debugCanvas = canvasObject.GetComponent<Canvas>();
            debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            debugCanvas.sortingOrder = 20000;
        }

        if (debugText == null)
        {
            Transform existing = debugCanvas.transform.Find("Text");

            if (existing == null)
            {
                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                existing = textObject.transform;
                existing.SetParent(debugCanvas.transform, false);
            }

            debugText = existing.GetComponent<Text>();
            RectTransform rect = debugText.transform as RectTransform;

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(12f, -64f);
                rect.sizeDelta = new Vector2(470f, 130f);
            }

            debugText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            debugText.fontSize = 14;
            debugText.alignment = TextAnchor.UpperLeft;
            debugText.color = Color.white;
            debugText.raycastTarget = false;
        }

        debugCanvas.gameObject.SetActive(showDebugHud);
        UpdateDebugHud();
    }

    private void UpdateDebugHud()
    {
        if (!showDebugHud || debugText == null)
        {
            return;
        }

        bool manifestLoaded = dinnerBuilder != null && dinnerBuilder.HasValidManifest;
        string foodState = dinnerBuilder != null && dinnerBuilder.FoodState != null
            ? dinnerBuilder.FoodState.CurrentState
            : "Missing";
        debugText.text =
            $"Ch3 phase: {currentPhase}\n" +
            $"manifest: {manifestLoaded} seats: {seatAnimators.Count}\n" +
            $"actions: {GetTotalSeatActions()} frames: {GetTotalSpriteFramesApplied()}\n" +
            $"food: {foodState}";
    }

    private float GetMaybeFastDuration(float duration)
    {
        return debugFastMode ? Mathf.Min(duration, 0.2f) : duration;
    }

    private float GetEatingDuration()
    {
        return debugFastMode ? Mathf.Min(15f, Mathf.Max(0f, eatingDurationSeconds)) : Mathf.Max(0f, eatingDurationSeconds);
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (chapterManager != null)
        {
            chapterManager.SetChapterPlayerInputEnabled(enabled);
        }
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

        if (dinnerBuilder == null)
        {
            dinnerBuilder = GetComponent<Chapter3LayeredDinnerBuilder>();
        }

        if (dinnerBuilder == null)
        {
            dinnerBuilder = FindAnyObjectByType<Chapter3LayeredDinnerBuilder>(FindObjectsInactive.Include);
        }

        if (dinnerBuilder == null)
        {
            dinnerBuilder = gameObject.AddComponent<Chapter3LayeredDinnerBuilder>();
        }

        if (tableForegroundOccluder == null)
        {
            tableForegroundOccluder = GetComponent<Chapter3DiningTableForegroundOccluder>();
        }
    }

    private static bool ContainsGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
