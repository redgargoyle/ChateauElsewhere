using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Chapter2MonsterStingerController : MonoBehaviour
{
    [SerializeField] private GameObject monsterObject;
    [SerializeField] private string monsterObjectName = "Ch2_Monster";
    [SerializeField] private Transform runStart;
    [SerializeField] private Transform runTarget;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private string drawingRoomId = "Drawing Room";
    [SerializeField] private AudioSource violinAudioSource;
    [SerializeField] private AudioClip violinAudioClip;
    [SerializeField] private string fallbackViolinClipName = "violinscreech";
    [SerializeField] private bool loopViolinAudio;
    [SerializeField] private bool forceMonsterToFront = true;
    [SerializeField] private string monsterSortingLayerName = "People";
    [SerializeField] private int monsterSortingOrder = 9999;
    [SerializeField] private int monsterOverlaySortingOrder = 10000;
    [SerializeField] private float runSeconds = 1.0f;
    [SerializeField] private float freezeSeconds = 2.5f;
    [SerializeField] private float maxVisibleSeconds = 7f;
    [SerializeField] private int cyclesBeforeComplete = 1;
    [SerializeField] private bool createPlaceholderMonsterIfMissing = true;

    private Coroutine stingerRoutine;
    private bool isRunning;
    private bool subscribedToRoomChanges;
    private float visibleElapsedSeconds;

    public bool IsRunning => isRunning;

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

        ApplyMonsterRoomVisibility();
        PlayViolinAudioIfVisible(true);

        int cycleCount = cyclesBeforeComplete > 0 ? 1 : 0;

        for (int i = 0; i < cycleCount && HasVisibleTimeRemaining(); i++)
        {
            if (monsterObject != null && runStart != null)
            {
                monsterObject.transform.position = runStart.position;
            }

            ApplyMonsterRoomVisibility();
            PlayViolinAudioIfVisible();

            yield return MoveMonsterToFreezeTarget();

            StopViolinAudio();

            if (monsterObject != null && runTarget != null)
            {
                monsterObject.transform.position = runTarget.position;
                ApplyMonsterRoomVisibility();
            }

            if (freezeSeconds > 0f && HasVisibleTimeRemaining())
            {
                yield return WaitForFreezeSeconds();
            }
        }

        StopViolinAudio();
        HideMonster();
        UnsubscribeFromRoomChanges();
        isRunning = false;
        stingerRoutine = null;
    }

    private IEnumerator MoveMonsterToFreezeTarget()
    {
        float duration = Mathf.Max(0f, runSeconds);

        if (monsterObject == null || runStart == null || runTarget == null)
        {
            yield return WaitForStingerSeconds(duration);
            yield break;
        }

        Vector3 startPosition = runStart.position;
        Vector3 targetPosition = runTarget.position;

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

    private IEnumerator WaitForFreezeSeconds()
    {
        yield return WaitForStingerSeconds(freezeSeconds);
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
