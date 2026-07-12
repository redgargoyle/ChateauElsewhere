using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter2MonsterStingerController : Chateau.Architecture.ChapterFeatureBase
{
    private const int RunFreezeCycleCount = 3;
    private static readonly int[] MonsterRunStutterFrameOrder = { 0, 3, 1, 5, 2, 6, 4, 7 };

    [SerializeField] private GameObject monsterObject;
    [SerializeField] private Transform runStart;
    [SerializeField] private Transform runTarget;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private AudioSource violinAudioSource;
    [SerializeField] private GameAudioSourceVolume violinAudioVolumeBinding;
    [SerializeField] private AudioClip violinAudioClip;
    [SerializeField] private bool loopViolinAudio = true;
    [SerializeField] private bool forceMonsterToFront = true;
    [SerializeField] private string monsterSortingLayerName = "People";
    [SerializeField] private int monsterSortingOrder = 9999;
    [SerializeField] private int monsterOverlaySortingOrder = 10000;
    [SerializeField] private Canvas monsterOverlayCanvas;
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

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (navigationManager == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized RoomNavigationManager.", this);
        }

        if (runStart == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized run-start anchor.", this);
        }

        if (runTarget == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized run-target anchor.", this);
        }

        if (monsterObject == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized monster object.", this);
            return;
        }

        if (monsterImage == null && monsterSpriteRenderer == null)
        {
            report.AddError("Chapter2MonsterStingerController requires a serialized monster Image or SpriteRenderer.", this);
        }

        if (monsterImage != null &&
            monsterImage.gameObject != monsterObject &&
            !monsterImage.transform.IsChildOf(monsterObject.transform))
        {
            report.AddError("Chapter2MonsterStingerController monster Image must belong to its serialized monster object.", this);
        }

        if (monsterSpriteRenderer != null &&
            monsterSpriteRenderer.gameObject != monsterObject &&
            !monsterSpriteRenderer.transform.IsChildOf(monsterObject.transform))
        {
            report.AddError("Chapter2MonsterStingerController monster SpriteRenderer must belong to its serialized monster object.", this);
        }

        if (monsterOverlayCanvas == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized monster overlay Canvas.", this);
        }
        else
        {
            if (monsterOverlayCanvas.gameObject != monsterObject)
            {
                report.AddError("Chapter2MonsterStingerController overlay Canvas must be owned by its monster object.", this);
            }

            if (monsterOverlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                !monsterOverlayCanvas.overrideSorting ||
                monsterOverlayCanvas.sortingOrder != monsterOverlaySortingOrder ||
                !string.Equals(monsterOverlayCanvas.sortingLayerName, monsterSortingLayerName, System.StringComparison.OrdinalIgnoreCase))
            {
                report.AddError("Chapter2MonsterStingerController overlay Canvas must use its authored sorting layer and order.", this);
            }
        }

        if (violinAudioSource == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized violin AudioSource.", this);
        }
        else
        {
            if (violinAudioSource.gameObject != monsterObject)
            {
                report.AddError("Chapter2MonsterStingerController violin AudioSource must be owned by its monster object.", this);
            }

            if (violinAudioClip != null && violinAudioSource.clip != violinAudioClip)
            {
                report.AddError("Chapter2MonsterStingerController violin source and clip reference must match.", this);
            }

            if (violinAudioSource.playOnAwake || violinAudioSource.loop != loopViolinAudio || !Mathf.Approximately(violinAudioSource.spatialBlend, 0f))
            {
                report.AddError("Chapter2MonsterStingerController violin source must be authored as looping, play-on-awake disabled, 2D audio.", this);
            }
        }

        if (violinAudioClip == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized violin AudioClip.", this);
        }

        if (violinAudioVolumeBinding == null)
        {
            report.AddError("Chapter2MonsterStingerController requires its serialized violin volume binding.", this);
        }
        else
        {
            if (violinAudioVolumeBinding.gameObject != monsterObject ||
                (violinAudioSource != null && violinAudioVolumeBinding.gameObject != violinAudioSource.gameObject))
            {
                report.AddError("Chapter2MonsterStingerController violin source and volume binding must share the monster object.", this);
            }

            if (violinAudioVolumeBinding.Channel != GameAudioChannel.GameSounds ||
                !Mathf.Approximately(violinAudioVolumeBinding.BaseVolume, 1f))
            {
                report.AddError("Chapter2MonsterStingerController violin volume binding must use Game Sounds at base volume 1.", this);
            }
        }
    }

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
        CaptureOriginalMonsterSprite();
        LoadMonsterRunSpritesIfNeeded();
        ConfigureViolinAudioSource();

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
            Debug.LogWarning("Chapter 2 monster stinger has no violin audio clip assigned.", this);
        }
    }

    private void ConfigureViolinAudioSource()
    {
        if (violinAudioSource == null || violinAudioVolumeBinding == null || violinAudioClip == null)
        {
            return;
        }

        violinAudioSource.clip = violinAudioClip;
        violinAudioSource.playOnAwake = false;
        violinAudioSource.loop = loopViolinAudio;
        violinAudioSource.spatialBlend = 0f;
        violinAudioVolumeBinding.Configure(
            violinAudioSource,
            GameAudioChannel.GameSounds,
            violinAudioVolumeBinding.BaseVolume);
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
        Canvas monsterCanvas = monsterOverlayCanvas;

        if (monsterCanvas == null)
        {
            monsterCanvas = monsterObject.GetComponent<Canvas>();
        }

        if (monsterCanvas == null)
        {
            monsterCanvas = monsterObject.AddComponent<Canvas>();
        }

        monsterOverlayCanvas = monsterCanvas;

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
            GameAudioSettings.TryPlay(violinAudioSource);
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
