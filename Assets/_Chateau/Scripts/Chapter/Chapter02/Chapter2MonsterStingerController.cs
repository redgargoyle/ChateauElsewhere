using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Chapter2MonsterStingerController : MonoBehaviour
{
    private const int RunFreezeCycleCount = 3;
    private static readonly int[] MonsterRunStutterFrameOrder = { 0, 3, 1, 5, 2, 6, 4, 7 };

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
    [SerializeField] private Image monsterImage;
    [SerializeField] private SpriteRenderer monsterSpriteRenderer;
    [SerializeField] private Sprite[] monsterRunSprites = new Sprite[0];
    [SerializeField] private string monsterRunSpritesResourcePath = "Chapter2/Monster/ArmSwing";
    [SerializeField, Min(1f)] private float monsterRunAnimationFramesPerSecond = 6f;
    [SerializeField] private bool animateMonsterRunSprites = true;
    [SerializeField] private bool useMonsterRunStutterFrameOrder = true;
    [SerializeField] private bool shakeMonsterWhileRunning = true;
    [SerializeField, Min(0f)] private float monsterRunShakePixels = 1.75f;
    [SerializeField, Min(0f)] private float monsterRunWorldShakeUnits = 0.015f;
    [SerializeField, Min(0.1f)] private float monsterRunShakeFrequency = 20f;
    [SerializeField, Range(0f, 1f)] private float monsterRunVerticalShakeScale = 0.2f;
    [SerializeField] private bool holdDifferentMonsterPoseOnFreeze = true;
    [SerializeField, Min(1)] private int monsterFreezePoseStep = 3;
    [SerializeField] private bool twitchMonsterPoseWhileFrozen = true;
    [SerializeField, Min(0.1f)] private float monsterFreezeTwitchFramesPerSecond = 2f;
    [SerializeField, Min(0f)] private float minimumRunSeconds = 1f;
    [SerializeField, Min(0f)] private float maximumRunSeconds = 2f;
    [SerializeField, Min(0f)] private float minimumFreezeSeconds = 1f;
    [SerializeField, Min(0f)] private float maximumFreezeSeconds = 2f;
    [SerializeField, Range(0.1f, 1f)] private float runSegmentDistanceScale = 0.65f;
    [SerializeField, Min(0.1f)] private float fallbackRunRightDistance = 4f;
    [SerializeField] private float maxVisibleSeconds = 12f;
    [SerializeField] private bool createPlaceholderMonsterIfMissing = true;

    private Coroutine stingerRoutine;
    private bool isRunning;
    private bool subscribedToRoomChanges;
    private float visibleElapsedSeconds;
    private float monsterRunAnimationElapsedSeconds;
    private int currentMonsterRunFrameIndex = -1;
    private int nextMonsterFreezeFrameIndex;
    private Sprite originalMonsterSprite;
    private bool hasOriginalMonsterSprite;

    public bool IsRunning => isRunning || stingerRoutine != null;

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
        monsterRunAnimationElapsedSeconds = 0f;
        ResolveReferences();
        SubscribeToRoomChanges();

        if (monsterObject != null && runStart != null)
        {
            monsterObject.transform.position = runStart.position;
        }

        ResetMonsterRunAnimation();

        StingerCycleTiming[] cycleTimings = BuildCycleTimings();

        for (int i = 0; i < cycleTimings.Length && HasVisibleTimeRemaining(); i++)
        {
            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible(true);

            yield return MoveMonsterToNextFreezeTarget(cycleTimings[i].RunSeconds);

            ApplyMonsterRoomVisibility();

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

    private IEnumerator MoveMonsterToNextFreezeTarget(float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (monsterObject == null)
        {
            yield return WaitForStingerSeconds(duration);
            yield break;
        }

        Vector3 startPosition = monsterObject.transform.position;
        Vector3 targetPosition = GetForwardRunTargetPosition(startPosition);

        if (duration <= 0f)
        {
            monsterObject.transform.position = targetPosition;
            ApplyNextMonsterFreezePose();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration && HasVisibleTimeRemaining())
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            monsterRunAnimationElapsedSeconds += deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 basePosition = Vector3.Lerp(startPosition, targetPosition, progress);
            UpdateMonsterRunAnimation(monsterRunAnimationElapsedSeconds);
            monsterObject.transform.position = basePosition + GetMonsterRunShakeOffset(monsterRunAnimationElapsedSeconds);
            TickVisibleElapsed();
            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible();
            yield return null;
        }

        monsterObject.transform.position = targetPosition;
        ApplyNextMonsterFreezePose();
    }

    private IEnumerator WaitForFreezeSeconds(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && HasVisibleTimeRemaining())
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            monsterRunAnimationElapsedSeconds += deltaTime;
            UpdateMonsterFreezeAnimation(monsterRunAnimationElapsedSeconds);
            TickVisibleElapsed();
            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible();
            yield return null;
        }
    }

    private StingerCycleTiming[] BuildCycleTimings()
    {
        StingerCycleTiming[] timings = new StingerCycleTiming[RunFreezeCycleCount];

        for (int i = 0; i < timings.Length; i++)
        {
            float runDuration = GetRandomDuration(minimumRunSeconds, maximumRunSeconds);
            float freezeDuration = GetRandomDuration(minimumFreezeSeconds, maximumFreezeSeconds);
            timings[i] = new StingerCycleTiming(runDuration, freezeDuration);
        }

        return timings;
    }

    private static float GetRandomDuration(float minimumSeconds, float maximumSeconds)
    {
        float minimum = Mathf.Max(0f, Mathf.Min(minimumSeconds, maximumSeconds));
        float maximum = Mathf.Max(minimum, Mathf.Max(minimumSeconds, maximumSeconds));
        return Mathf.Approximately(minimum, maximum) ? minimum : Random.Range(minimum, maximum);
    }

    private Vector3 GetForwardRunTargetPosition(Vector3 startPosition)
    {
        return startPosition + Vector3.right * GetRunSegmentDistance(startPosition);
    }

    private float GetRunSegmentDistance(Vector3 startPosition)
    {
        float rightDistance = fallbackRunRightDistance;

        if (runStart != null && runTarget != null)
        {
            rightDistance = Mathf.Abs(runTarget.position.x - runStart.position.x);
        }
        else if (runTarget != null)
        {
            rightDistance = Mathf.Abs(runTarget.position.x - startPosition.x);
        }

        return Mathf.Max(0.1f, rightDistance * runSegmentDistanceScale);
    }

    private IEnumerator WaitForStingerSeconds(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && HasVisibleTimeRemaining())
        {
            elapsed += Time.deltaTime;
            TickVisibleElapsed();
            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible();
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

        ResolveMonsterVisuals();
        LoadMonsterRunSpritesIfNeeded();
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

    private void ResolveMonsterVisuals()
    {
        if (monsterObject == null)
        {
            return;
        }

        Transform monsterTransform = monsterObject.transform;

        if (monsterImage == null || !monsterImage.transform.IsChildOf(monsterTransform))
        {
            monsterImage = monsterObject.GetComponentInChildren<Image>(true);
        }

        if (monsterSpriteRenderer == null || !monsterSpriteRenderer.transform.IsChildOf(monsterTransform))
        {
            monsterSpriteRenderer = monsterObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        CaptureOriginalMonsterSprite();
    }

    private void CaptureOriginalMonsterSprite()
    {
        if (hasOriginalMonsterSprite)
        {
            return;
        }

        Sprite sprite = GetActiveMonsterSprite();

        if (sprite == null)
        {
            return;
        }

        originalMonsterSprite = sprite;
        hasOriginalMonsterSprite = true;
    }

    private Sprite GetActiveMonsterSprite()
    {
        if (monsterImage != null && monsterImage.sprite != null)
        {
            return monsterImage.sprite;
        }

        if (monsterSpriteRenderer != null)
        {
            return monsterSpriteRenderer.sprite;
        }

        return null;
    }

    private void LoadMonsterRunSpritesIfNeeded()
    {
        if (monsterRunSprites != null && monsterRunSprites.Length > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(monsterRunSpritesResourcePath))
        {
            return;
        }

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(monsterRunSpritesResourcePath);

        if (loadedSprites == null || loadedSprites.Length == 0)
        {
            return;
        }

        System.Array.Sort(loadedSprites, CompareSpritesByName);
        monsterRunSprites = loadedSprites;
    }

    private static int CompareSpritesByName(Sprite left, Sprite right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.Compare(leftName, rightName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ResetMonsterRunAnimation()
    {
        currentMonsterRunFrameIndex = -1;
        nextMonsterFreezeFrameIndex = 0;
        ApplyMonsterRunSpriteFrame(0);
    }

    private void UpdateMonsterRunAnimation(float elapsedSeconds)
    {
        if (!CanAnimateMonsterRunSprites())
        {
            return;
        }

        float frameRate = Mathf.Max(1f, monsterRunAnimationFramesPerSecond);
        int cycleFrameIndex = Mathf.FloorToInt(elapsedSeconds * frameRate) % monsterRunSprites.Length;
        int frameIndex = GetMonsterRunFrameIndex(cycleFrameIndex);
        ApplyMonsterRunSpriteFrame(frameIndex);
    }

    private void UpdateMonsterFreezeAnimation(float elapsedSeconds)
    {
        if (!twitchMonsterPoseWhileFrozen || !CanAnimateMonsterRunSprites())
        {
            return;
        }

        float frameRate = Mathf.Max(0.1f, monsterFreezeTwitchFramesPerSecond);
        int cycleFrameIndex = Mathf.FloorToInt(elapsedSeconds * frameRate) % monsterRunSprites.Length;
        int frameIndex = GetMonsterRunFrameIndex(cycleFrameIndex);
        ApplyMonsterRunSpriteFrame(frameIndex);
    }

    private int GetMonsterRunFrameIndex(int cycleFrameIndex)
    {
        if (!useMonsterRunStutterFrameOrder || monsterRunSprites == null || monsterRunSprites.Length != MonsterRunStutterFrameOrder.Length)
        {
            return cycleFrameIndex;
        }

        return MonsterRunStutterFrameOrder[cycleFrameIndex];
    }

    private void ApplyNextMonsterFreezePose()
    {
        if (!holdDifferentMonsterPoseOnFreeze || !CanAnimateMonsterRunSprites())
        {
            return;
        }

        int frameStep = Mathf.Max(1, monsterFreezePoseStep);
        nextMonsterFreezeFrameIndex = (nextMonsterFreezeFrameIndex + frameStep) % monsterRunSprites.Length;
        ApplyMonsterRunSpriteFrame(nextMonsterFreezeFrameIndex);
    }

    private bool CanAnimateMonsterRunSprites()
    {
        return animateMonsterRunSprites &&
            monsterRunSprites != null &&
            monsterRunSprites.Length > 0 &&
            (monsterImage != null || monsterSpriteRenderer != null);
    }

    private void ApplyMonsterRunSpriteFrame(int frameIndex)
    {
        if (!CanAnimateMonsterRunSprites())
        {
            return;
        }

        int spriteIndex = Mathf.Clamp(frameIndex, 0, monsterRunSprites.Length - 1);

        if (spriteIndex == currentMonsterRunFrameIndex)
        {
            return;
        }

        Sprite sprite = monsterRunSprites[spriteIndex];

        if (sprite == null)
        {
            return;
        }

        if (monsterImage != null)
        {
            monsterImage.sprite = sprite;
        }

        if (monsterSpriteRenderer != null)
        {
            monsterSpriteRenderer.sprite = sprite;
        }

        currentMonsterRunFrameIndex = spriteIndex;
    }

    private Vector3 GetMonsterRunShakeOffset(float elapsedSeconds)
    {
        if (!shakeMonsterWhileRunning || monsterObject == null)
        {
            return Vector3.zero;
        }

        float shakeAmplitude = monsterObject.transform is RectTransform ? monsterRunShakePixels : monsterRunWorldShakeUnits;

        if (shakeAmplitude <= 0f)
        {
            return Vector3.zero;
        }

        float frequency = Mathf.Max(0.1f, monsterRunShakeFrequency);
        float phase = elapsedSeconds * frequency;
        float x = Mathf.Sin(phase) * shakeAmplitude;
        float y = Mathf.Sin(phase * 1.73f + 0.4f) * shakeAmplitude * monsterRunVerticalShakeScale;
        return new Vector3(x, y, 0f);
    }

    private void RestoreOriginalMonsterSprite()
    {
        currentMonsterRunFrameIndex = -1;

        if (!hasOriginalMonsterSprite || originalMonsterSprite == null)
        {
            return;
        }

        if (monsterImage != null)
        {
            monsterImage.sprite = originalMonsterSprite;
        }

        if (monsterSpriteRenderer != null)
        {
            monsterSpriteRenderer.sprite = originalMonsterSprite;
        }
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
        RestoreOriginalMonsterSprite();

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
