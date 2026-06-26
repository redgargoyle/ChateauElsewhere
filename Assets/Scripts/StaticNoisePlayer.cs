using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StaticNoisePlayer : MonoBehaviour
{
    public RawImage targetImage;
    public StaticSet set;
    public bool playOnEnable = true;
    public bool hideWhenStopped = true;
    public bool stretchToScreen = true;
    public CanvasGroup canvasGroup;
    public AudioSource staticAudio;

    private Coroutine playCoroutine;
    private Color baseColor = Color.white;
    private float staticAudioBaseVolume = -1f;

    public bool CanPlay
    {
        get { return set != null && set.IsValid(); }
    }

    private void Reset()
    {
        targetImage = GetComponentInChildren<RawImage>();
        canvasGroup = GetComponent<CanvasGroup>();
        staticAudio = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponentInChildren<RawImage>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (staticAudio == null)
        {
            staticAudio = GetComponent<AudioSource>();
        }

        if (staticAudio != null && staticAudioBaseVolume < 0f)
        {
            staticAudioBaseVolume = staticAudio.volume;
        }

        if (targetImage != null)
        {
            baseColor = targetImage.color;
        }

        ApplyResponsiveLayout();
        DisableRaycasts();

        if (hideWhenStopped)
        {
            SetVisible(false);
        }
    }

    private void OnEnable()
    {
        if (playOnEnable && set != null && set.IsValid())
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (set == null || !set.IsValid())
        {
            return;
        }

        if (targetImage == null)
        {
            targetImage = GetComponentInChildren<RawImage>(true);
        }

        if (targetImage == null)
        {
            Debug.LogWarning("StaticNoisePlayer needs a RawImage target assigned.", this);
            return;
        }

        ApplyResponsiveLayout();
        Stop();
        SetVisible(true);

        if (staticAudio != null)
        {
            if (staticAudioBaseVolume < 0f)
            {
                staticAudioBaseVolume = staticAudio.volume;
            }

            GameAudioSettings.EnsureBinding(staticAudio, GameAudioChannel.GameSounds, staticAudioBaseVolume);
            GameAudioSettings.TryPlay(staticAudio);
        }

        playCoroutine = StartCoroutine(Co_Play());
    }

    public void PlaySet(StaticSet staticSet)
    {
        set = staticSet;
        Play();
    }

    public void Stop()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (staticAudio != null)
        {
            staticAudio.Stop();
        }

        ResetIntensity();

        if (hideWhenStopped)
        {
            SetVisible(false);
        }
    }

    private IEnumerator Co_Play()
    {
        while (set != null && set.IsValid())
        {
            for (int i = 0; i < set.groups.Length; i++)
            {
                StaticFrameGroup group = set.groups[i];

                if (group == null || group.frames == null)
                {
                    continue;
                }

                int length = group.frames.Length;

                if (length == 0)
                {
                    continue;
                }

                int[] order = BuildOrder(length, group.shuffle);

                for (int f = 0; f < order.Length; f++)
                {
                    Texture2D tex = group.frames[order[f]];

                    if (tex != null && targetImage != null)
                    {
                        targetImage.texture = tex;
                        SetIntensity(group.GetIntensity());
                    }

                    yield return new WaitForSeconds(group.frameDuration);
                }
            }

            if (!set.loop)
            {
                break;
            }
        }

        playCoroutine = null;

        if (hideWhenStopped)
        {
            SetVisible(false);
        }
    }

    private int[] BuildOrder(int length, bool shuffle)
    {
        int[] order = new int[length];

        for (int i = 0; i < length; i++)
        {
            order[i] = i;
        }

        if (shuffle)
        {
            for (int i = 0; i < order.Length; i++)
            {
                int randomIndex = Random.Range(i, order.Length);
                int current = order[i];
                order[i] = order[randomIndex];
                order[randomIndex] = current;
            }
        }

        return order;
    }

    private void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = intensity;
        }

        Color color = baseColor;
        color.a = intensity;
        targetImage.color = color;
    }

    private void ResetIntensity()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = baseColor.a;
        }

        if (targetImage != null)
        {
            targetImage.color = baseColor;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? baseColor.a : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (targetImage != null)
        {
            targetImage.enabled = visible;
        }
    }

    private void DisableRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!stretchToScreen)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        Stretch(panelRect);

        if (targetImage != null)
        {
            Stretch(targetImage.rectTransform);
        }
    }

    private void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }
}
