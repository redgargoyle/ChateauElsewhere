using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Chapter2MonsterStingerController : MonoBehaviour
{
    [SerializeField] private GameObject monsterObject;
    [SerializeField] private Transform runStart;
    [SerializeField] private Transform runTarget;
    [SerializeField] private AudioSource violinAudioSource;
    [SerializeField] private AudioClip violinAudioClip;
    [SerializeField] private string fallbackViolinClipName = "violinsolo";
    [SerializeField] private bool loopViolinAudio = true;
    [SerializeField] private float runSeconds = 1.0f;
    [SerializeField] private float freezeSeconds = 2.5f;
    [SerializeField] private int cyclesBeforeComplete = 3;
    [SerializeField] private bool createPlaceholderMonsterIfMissing = true;

    private Coroutine stingerRoutine;
    private bool isRunning;

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

        isRunning = false;
    }

    public IEnumerator PlayStinger()
    {
        if (isRunning)
        {
            yield break;
        }

        isRunning = true;
        ResolveReferences();

        int cycleCount = Mathf.Max(0, cyclesBeforeComplete);

        for (int i = 0; i < cycleCount; i++)
        {
            if (monsterObject != null && runStart != null)
            {
                monsterObject.transform.position = runStart.position;
            }

            if (monsterObject != null)
            {
                monsterObject.SetActive(true);
            }

            if (violinAudioSource != null)
            {
                violinAudioSource.Play();
            }

            yield return MoveMonsterToFreezeTarget();

            if (violinAudioSource != null)
            {
                violinAudioSource.Stop();
            }

            if (monsterObject != null && runTarget != null)
            {
                monsterObject.transform.position = runTarget.position;
            }

            if (freezeSeconds > 0f)
            {
                yield return new WaitForSeconds(freezeSeconds);
            }
        }

        if (violinAudioSource != null)
        {
            violinAudioSource.Stop();
        }

        isRunning = false;
        stingerRoutine = null;
    }

    private IEnumerator MoveMonsterToFreezeTarget()
    {
        float duration = Mathf.Max(0f, runSeconds);

        if (monsterObject == null || runStart == null || runTarget == null)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

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

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            monsterObject.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }

        monsterObject.transform.position = targetPosition;
    }

    private void ResolveReferences()
    {
        if (runStart == null)
        {
            runStart = FindRoomAnchor("Ch2_MonsterRunStart");
        }

        if (runTarget == null)
        {
            runTarget = FindRoomAnchor("Ch2_MonsterFreezeTarget");
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
}
