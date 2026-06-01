using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Chapter2MonsterStingerController : MonoBehaviour
{
    private const float TimingEpsilon = 0.0001f;

    [SerializeField] private GameObject monsterObject;
    [SerializeField] private string monsterObjectName = "Ch2_Monster";
    [SerializeField] private Transform runStart;
    [SerializeField] private Transform runTarget;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private AudioSource violinAudioSource;
    [SerializeField] private AudioClip violinAudioClip;
    [SerializeField] private string fallbackViolinClipName = "violinscreech";
    [SerializeField] private bool loopViolinAudio = true;
    [SerializeField] private bool forceMonsterToFront = true;
    [SerializeField] private string monsterSortingLayerName = "People";
    [SerializeField] private int monsterSortingOrder = 9999;
    [SerializeField] private int monsterOverlaySortingOrder = 10000;
    [SerializeField, Min(0f)] private float minimumRunSeconds = 1f;
    [SerializeField, Min(0f)] private float maximumRunSeconds = 2f;
    [SerializeField, Min(0f)] private float minimumFreezeSeconds = 1f;
    [SerializeField, Min(0f)] private float maximumFreezeSeconds = 2f;
    [SerializeField, Min(1)] private int minimumCyclesBeforeComplete = 2;
    [SerializeField, Min(1)] private int maximumCyclesBeforeComplete = 3;
    [SerializeField, Min(0.1f)] private float fallbackRunRightDistance = 4f;
    [SerializeField] private float maxVisibleSeconds = 7f;
    [SerializeField] private bool createPlaceholderMonsterIfMissing = true;

    private Coroutine stingerRoutine;
    private bool isRunning;
    private bool subscribedToRoomChanges;
    private float visibleElapsedSeconds;

    public bool IsRunning => isRunning;

    private struct StingerCycleTiming
    {
        public StingerCycleTiming(float runSeconds, float freezeSeconds)
        {
            RunSeconds = runSeconds;
            FreezeSeconds = freezeSeconds;
        }

        public float RunSeconds;
        public float FreezeSeconds;
    }

    public Coroutine BeginStinger()
    {
        if (stingerRoutine != null)
        {
            return stingerRoutine;
        }

        stingerRoutine = StartCoroutine(PlayStinger());
        return stingerRoutine;
    }

    public void StopStinger()
    {
        if (stingerRoutine != null)
        {
            StopCoroutine(stingerRoutine);
            stingerRoutine = null;
        }

        if (violinAudioSource != null)
        {
            violinAudioSource.Stop();
        }

        HideMonster();
        UnsubscribeFromRoomChanges();
        isRunning = false;
    }

    private void OnDisable()
    {
        StopStinger();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRoomChanges();
    }

    public IEnumerator PlayStinger()
    {
        if (isRunning)
        {
            yield break;
        }

        isRunning = true;
        visibleElapsedSeconds = 0f;
        ResolveReferences();
        SubscribeToRoomChanges();

        if (monsterObject != null && runStart != null)
        {
            monsterObject.transform.position = runStart.position;
        }

        StingerCycleTiming[] cycleTimings = BuildCycleTimings();

        for (int i = 0; i < cycleTimings.Length && HasVisibleTimeRemaining(); i++)
        {
            if (monsterObject != null && runStart != null)
            {
                monsterObject.transform.position = runStart.position;
            }

            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible(true);

            yield return MoveMonsterToFreezeTarget(cycleTimings[i].RunSeconds);

            StopViolinAudio();

            if (monsterObject != null)
            {
                monsterObject.transform.position = GetRunTargetPosition(runStart != null ? runStart.position : monsterObject.transform.position);
                ApplyMonsterRoomVisibility();
            }

            if (cycleTimings[i].FreezeSeconds > 0f && HasVisibleTimeRemaining())
            {
                yield return WaitForFreezeSeconds(cycleTimings[i].FreezeSeconds);
            }
        }

        StopViolinAudio();
        HideMonster();
        UnsubscribeFromRoomChanges();
        isRunning = false;
        stingerRoutine = null;
    }

    private IEnumerator MoveMonsterToFreezeTarget(float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (monsterObject == null || runStart == null)
        {
            yield return WaitForStingerSeconds(duration);
            yield break;
        }

        Vector3 startPosition = runStart.position;
        Vector3 targetPosition = GetRunTargetPosition(startPosition);

        if (duration <= 0f)
        {
            monsterObject.transform.position = targetPosition;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration && HasVisibleTimeRemaining())
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            monsterObject.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            TickVisibleElapsed();
            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible();
            yield return null;
        }

        monsterObject.transform.position = targetPosition;
    }

    private IEnumerator WaitForFreezeSeconds(float duration)
    {
        yield return WaitForStingerSeconds(duration);
    }

    private StingerCycleTiming[] BuildCycleTimings()
    {
        int cycleCount = GetRandomCycleCount();
        StingerCycleTiming[] timings = new StingerCycleTiming[cycleCount];
        float totalSeconds = 0f;

        for (int i = 0; i < timings.Length; i++)
        {
            float runDuration = GetRandomDuration(minimumRunSeconds, maximumRunSeconds);
            float freezeDuration = GetRandomDuration(minimumFreezeSeconds, maximumFreezeSeconds);
            timings[i] = new StingerCycleTiming(runDuration, freezeDuration);
            totalSeconds += runDuration + freezeDuration;
        }

        TrimCycleTimingsToVisibleBudget(timings, totalSeconds);
        return timings;
    }

    private int GetRandomCycleCount()
    {
        int minimumCycles = Mathf.Max(1, Mathf.Min(minimumCyclesBeforeComplete, maximumCyclesBeforeComplete));
        int maximumCycles = Mathf.Max(minimumCycles, Mathf.Max(minimumCyclesBeforeComplete, maximumCyclesBeforeComplete));
        return Random.Range(minimumCycles, maximumCycles + 1);
    }

    private static float GetRandomDuration(float minimumSeconds, float maximumSeconds)
    {
        float minimum = Mathf.Max(0f, Mathf.Min(minimumSeconds, maximumSeconds));
        float maximum = Mathf.Max(minimum, Mathf.Max(minimumSeconds, maximumSeconds));
        return Mathf.Approximately(minimum, maximum) ? minimum : Random.Range(minimum, maximum);
    }

    private void TrimCycleTimingsToVisibleBudget(StingerCycleTiming[] timings, float totalSeconds)
    {
        float visibleBudget = Mathf.Max(0f, maxVisibleSeconds);

        if (visibleBudget <= 0f || totalSeconds <= visibleBudget || timings == null || timings.Length == 0)
        {
            return;
        }

        float minimumRun = Mathf.Max(0f, Mathf.Min(minimumRunSeconds, maximumRunSeconds));
        float minimumFreeze = Mathf.Max(0f, Mathf.Min(minimumFreezeSeconds, maximumFreezeSeconds));
        float minimumTotal = timings.Length * (minimumRun + minimumFreeze);

        if (minimumTotal > visibleBudget)
        {
            return;
        }

        float excessSeconds = totalSeconds - visibleBudget;

        for (int i = timings.Length - 1; i >= 0 && excessSeconds > TimingEpsilon; i--)
        {
            StingerCycleTiming timing = timings[i];
            float reducibleSeconds = Mathf.Max(0f, timing.FreezeSeconds - minimumFreeze);
            float reductionSeconds = Mathf.Min(reducibleSeconds, excessSeconds);
            timing.FreezeSeconds -= reductionSeconds;
            excessSeconds -= reductionSeconds;
            timings[i] = timing;
        }

        for (int i = timings.Length - 1; i >= 0 && excessSeconds > TimingEpsilon; i--)
        {
            StingerCycleTiming timing = timings[i];
            float reducibleSeconds = Mathf.Max(0f, timing.RunSeconds - minimumRun);
            float reductionSeconds = Mathf.Min(reducibleSeconds, excessSeconds);
            timing.RunSeconds -= reductionSeconds;
            excessSeconds -= reductionSeconds;
            timings[i] = timing;
        }
    }

    private Vector3 GetRunTargetPosition(Vector3 startPosition)
    {
        Vector3 targetPosition = runTarget != null ? runTarget.position : startPosition + Vector3.right * fallbackRunRightDistance;

        if (targetPosition.x <= startPosition.x)
        {
            float rightDistance = Mathf.Abs(targetPosition.x - startPosition.x);

            if (rightDistance < 0.1f)
            {
                rightDistance = fallbackRunRightDistance;
            }

            targetPosition.x = startPosition.x + rightDistance;
        }

        return targetPosition;
    }

    private IEnumerator WaitForStingerSeconds(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && HasVisibleTimeRemaining())
        {
            elapsed += Time.deltaTime;
            TickVisibleElapsed();
            ApplyMonsterRoomVisibility();
            yield return null;
        }
    }

    private void ResolveReferences()
    {
        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (runStart == null)
        {
            runStart = FindRoomAnchor("Ch2_MonsterRunStart");
        }

        if (runTarget == null)
        {
            runTarget = FindRoomAnchor("Ch2_MonsterFreezeTarget");
        }

        if (monsterObject == null)
        {
            monsterObject = FindSceneMonsterObject(monsterObjectName);
        }

        if (monsterObject == null && createPlaceholderMonsterIfMissing)
        {
            monsterObject = CreatePlaceholderMonster();
        }

        ResolveViolinAudioSource();

        if (runStart == null)
        {
            Debug.LogWarning("Chapter 2 monster stinger missing RoomAnchor Ch2_MonsterRunStart.", this);
        }

        if (runTarget == null)
        {
            Debug.LogWarning("Chapter 2 monster stinger missing RoomAnchor Ch2_MonsterFreezeTarget.", this);
        }

        if (monsterObject == null)
        {
            Debug.LogWarning("Chapter 2 monster stinger missing monster object.", this);
        }

        if (violinAudioSource == null)
        {
            Debug.LogWarning("Chapter 2 monster stinger has no violin AudioSource assigned.", this);
        }
        else if (violinAudioSource.clip == null)
        {
            Debug.LogWarning($"Chapter 2 monster stinger could not find violin audio clip '{fallbackViolinClipName}'.", this);
        }
    }

    private void ResolveViolinAudioSource()
    {
        if (violinAudioSource == null)
        {
            violinAudioSource = GetComponent<AudioSource>();
        }

        if (violinAudioSource == null)
        {
            violinAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (violinAudioClip == null)
        {
            violinAudioClip = FindViolinClip();
        }

        if (violinAudioSource.clip == null && violinAudioClip != null)
        {
            violinAudioSource.clip = violinAudioClip;
        }

        violinAudioSource.playOnAwake = false;
        violinAudioSource.loop = loopViolinAudio;
        violinAudioSource.spatialBlend = 0f;
    }

    private AudioClip FindViolinClip()
    {
        if (string.IsNullOrWhiteSpace(fallbackViolinClipName))
        {
            return null;
        }

        AudioClip clip = Resources.Load<AudioClip>(fallbackViolinClipName);

        if (clip != null)
        {
            return clip;
        }

        clip = Resources.Load<AudioClip>($"Audio/{fallbackViolinClipName}");

        if (clip != null)
        {
            return clip;
        }

#if UNITY_EDITOR
        string[] matches = AssetDatabase.FindAssets($"{fallbackViolinClipName} t:AudioClip", new[] { "Assets/Audio" });

        for (int i = 0; i < matches.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(matches[i]);
            AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

            if (editorClip != null &&
                string.Equals(editorClip.name, fallbackViolinClipName, System.StringComparison.OrdinalIgnoreCase))
            {
                return editorClip;
            }
        }
#endif

        return null;
    }

    private static Transform FindRoomAnchor(string anchorName)
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (string.Equals(anchor.AnchorId, anchorName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(anchor.name, anchorName, System.StringComparison.OrdinalIgnoreCase))
            {
                return anchor.transform;
            }
        }

        return null;
    }

    private static GameObject FindSceneMonsterObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null &&
                string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private GameObject CreatePlaceholderMonster()
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        placeholder.name = "Chapter2_MonsterPlaceholder_Runtime";
        placeholder.transform.SetParent(transform, true);
        placeholder.transform.localScale = new Vector3(0.65f, 1.45f, 0.65f);

        Renderer placeholderRenderer = placeholder.GetComponent<Renderer>();

        if (placeholderRenderer != null)
        {
            placeholderRenderer.material.color = new Color(0.06f, 0.04f, 0.05f, 1f);
        }

        Collider placeholderCollider = placeholder.GetComponent<Collider>();

        if (placeholderCollider != null)
        {
            Destroy(placeholderCollider);
        }

        if (runStart != null)
        {
            placeholder.transform.position = runStart.position;
        }

        placeholder.SetActive(false);
        return placeholder;
    }

    private void SubscribeToRoomChanges()
    {
        if (navigationManager == null || subscribedToRoomChanges)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.AddListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnsubscribeFromRoomChanges()
    {
        if (navigationManager == null || !subscribedToRoomChanges)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        ApplyMonsterRoomVisibility();
    }

    private void ApplyMonsterRoomVisibility()
    {
        if (monsterObject == null)
        {
            return;
        }

        bool shouldShow = CanShowMonster();

        if (monsterObject != gameObject)
        {
            monsterObject.SetActive(shouldShow);
        }

        if (shouldShow)
        {
            BringMonsterToFront();
        }

        if (!shouldShow)
        {
            StopViolinAudio();
        }
    }

    private bool CanShowMonster()
    {
        return isRunning && HasVisibleTimeRemaining() && IsDrawingRoomCurrent();
    }

    private bool IsDrawingRoomCurrent()
    {
        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return true;
        }

        return string.Equals(navigationManager.CurrentRoom, drawingRoomId, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool HasVisibleTimeRemaining()
    {
        return visibleElapsedSeconds < Mathf.Max(0f, maxVisibleSeconds);
    }

    private void TickVisibleElapsed()
    {
        visibleElapsedSeconds += Time.deltaTime;
    }

    private void HideMonster()
    {
        if (monsterObject != null && monsterObject != gameObject)
        {
            monsterObject.SetActive(false);
        }
    }

    private void BringMonsterToFront()
    {
        if (!forceMonsterToFront || monsterObject == null)
        {
            return;
        }

        monsterObject.transform.SetAsLastSibling();
        EnsureMonsterOverlayCanvas();

        Renderer[] renderers = monsterObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];

            if (targetRenderer == null)
            {
                continue;
            }

            if (HasSortingLayer(monsterSortingLayerName))
            {
                targetRenderer.sortingLayerName = monsterSortingLayerName;
            }

            targetRenderer.sortingOrder = monsterSortingOrder;
        }
    }

    private void EnsureMonsterOverlayCanvas()
    {
        Canvas monsterCanvas = monsterObject.GetComponent<Canvas>();

        if (monsterCanvas == null)
        {
            monsterCanvas = monsterObject.AddComponent<Canvas>();
        }

        monsterCanvas.overrideSorting = true;
        monsterCanvas.sortingOrder = monsterOverlaySortingOrder;

        if (HasSortingLayer(monsterSortingLayerName))
        {
            monsterCanvas.sortingLayerName = monsterSortingLayerName;
        }
    }

    private static bool HasSortingLayer(string sortingLayerName)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return false;
        }

        SortingLayer[] layers = SortingLayer.layers;

        for (int i = 0; i < layers.Length; i++)
        {
            if (string.Equals(layers[i].name, sortingLayerName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void PlayViolinAudioIfVisible(bool restart = false)
    {
        if (!CanShowMonster() || violinAudioSource == null || violinAudioSource.clip == null)
        {
            return;
        }

        if (restart)
        {
            violinAudioSource.Stop();
        }

        if (!violinAudioSource.isPlaying)
        {
            violinAudioSource.Play();
        }
    }

    private void StopViolinAudio()
    {
        if (violinAudioSource != null)
        {
            violinAudioSource.Stop();
        }
    }
}
