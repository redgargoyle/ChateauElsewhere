using UnityEngine;
using UnityEngine.UI;

public enum AbominationFullFramePhase
{
    None,
    SeatedIdle,
    CoveredDinner,
    Eating,
    FinishedIdle
}

[DisallowMultipleComponent]
public sealed class AbominationFullFrameAnimator : MonoBehaviour
{
    [Header("Animator Clip Playback")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip seatedIdleClip;
    [SerializeField] private AnimationClip coveredDinnerClip;
    [SerializeField] private AnimationClip eatingLoopClip;
    [SerializeField] private AnimationClip finishedIdleClip;

    [Header("Manual Frame Targets")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image image;
    [SerializeField] private RawImage rawImage;
    [SerializeField] private RawImage crossFadeImage;

    [Header("Sprite Frames")]
    [SerializeField] private Sprite[] seatedIdleFrames;
    [SerializeField] private Sprite[] coveredDinnerFrames;
    [SerializeField] private Sprite[] eatingFrames;
    [SerializeField] private Sprite[] finishedIdleFrames;

    [Header("Texture Frames")]
    [SerializeField] private Texture2D[] seatedIdleTextures;
    [SerializeField] private Texture2D[] coveredDinnerTextures;
    [SerializeField] private Texture2D[] eatingTextures;
    [SerializeField] private Texture2D[] finishedIdleTextures;

    [Header("Playback")]
    [SerializeField, Min(0.1f)] private float seatedIdleFps = 6f;
    [SerializeField, Min(0.1f)] private float coveredDinnerFps = 6f;
    [SerializeField, Min(0.1f)] private float eatingFps = 8f;
    [SerializeField, Min(0.1f)] private float finishedIdleFps = 6f;
    [SerializeField] private bool loopSeatedIdle = true;
    [SerializeField] private bool loopCoveredDinner = true;
    [SerializeField] private bool loopEating = true;
    [SerializeField] private bool loopFinishedIdle = true;
    [SerializeField] private bool useUnscaledTime;

    private AbominationFullFramePhase currentPhase = AbominationFullFramePhase.None;
    private Sprite[] activeSpriteFrames;
    private Texture2D[] activeTextureFrames;
    private float activeFps = 1f;
    private bool activeLoop = true;
    private bool manualPlaybackActive;
    private int frameIndex;
    private float frameTimer;
    private bool warnedMissingSeatedIdle;
    private bool warnedMissingCoveredDinner;
    private bool warnedMissingEating;
    private bool warnedMissingFinishedIdle;

    public AbominationFullFramePhase CurrentPhase => currentPhase;
    public bool IsPlaying => manualPlaybackActive || (animator != null && animator.enabled && animator.speed > 0f);
    public bool HasAnyAssignedFrames => HasAnyManualFrames() || HasAnyAnimatorClip();

    private void Reset()
    {
        ResolveTargets();
    }

    private void Awake()
    {
        ResolveTargets();
    }

    private void OnValidate()
    {
        seatedIdleFps = Mathf.Max(0.1f, seatedIdleFps);
        coveredDinnerFps = Mathf.Max(0.1f, coveredDinnerFps);
        eatingFps = Mathf.Max(0.1f, eatingFps);
        finishedIdleFps = Mathf.Max(0.1f, finishedIdleFps);
        ResolveTargets();
    }

    private void Update()
    {
        if (!manualPlaybackActive)
        {
            return;
        }

        int frameCount = GetActiveFrameCount();
        if (frameCount <= 1)
        {
            return;
        }

        float frameSeconds = 1f / Mathf.Max(0.1f, activeFps);
        frameTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        while (frameTimer >= frameSeconds)
        {
            frameTimer -= frameSeconds;
            AdvanceFrame(frameCount);
        }
    }

    public void PlaySeatedIdle()
    {
        PlayPhase(
            AbominationFullFramePhase.SeatedIdle,
            seatedIdleClip,
            seatedIdleFrames,
            seatedIdleTextures,
            seatedIdleFps,
            loopSeatedIdle);
    }

    public void PlayCoveredDinner()
    {
        PlayPhase(
            AbominationFullFramePhase.CoveredDinner,
            coveredDinnerClip,
            coveredDinnerFrames,
            coveredDinnerTextures,
            coveredDinnerFps,
            loopCoveredDinner);
    }

    public void PlayEatingLoop()
    {
        PlayPhase(
            AbominationFullFramePhase.Eating,
            eatingLoopClip,
            eatingFrames,
            eatingTextures,
            eatingFps,
            loopEating);
    }

    public void PlayFinishedIdle()
    {
        PlayPhase(
            AbominationFullFramePhase.FinishedIdle,
            finishedIdleClip,
            finishedIdleFrames,
            finishedIdleTextures,
            finishedIdleFps,
            loopFinishedIdle);
    }

    public void StopPlayback()
    {
        manualPlaybackActive = false;
        frameTimer = 0f;

        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    public void ShowFirstFrameOfCurrentPhase()
    {
        if (currentPhase == AbominationFullFramePhase.None)
        {
            return;
        }

        frameIndex = 0;
        frameTimer = 0f;
        ApplyFrame(frameIndex);
    }

    private void PlayPhase(
        AbominationFullFramePhase phase,
        AnimationClip clip,
        Sprite[] spriteFrames,
        Texture2D[] textureFrames,
        float fps,
        bool loop)
    {
        ResolveTargets();

        currentPhase = phase;
        activeSpriteFrames = spriteFrames;
        activeTextureFrames = textureFrames;
        activeFps = Mathf.Max(0.1f, fps);
        activeLoop = loop;
        frameIndex = 0;
        frameTimer = 0f;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        bool hasManualFrames = HasValidSprites(spriteFrames) || HasValidTextures(textureFrames);
        bool playedAnimatorClip = !hasManualFrames && TryPlayAnimatorClip(clip);
        manualPlaybackActive = hasManualFrames;

        if (hasManualFrames)
        {
            ApplyFrame(frameIndex);
            return;
        }

        if (!playedAnimatorClip)
        {
            WarnMissingPhaseOnce(phase);
        }

        SetTargetsVisible(true);
    }

    private bool TryPlayAnimatorClip(AnimationClip clip)
    {
        if (animator == null || clip == null)
        {
            return false;
        }

        animator.enabled = true;
        animator.speed = 1f;
        animator.Play(clip.name, 0, 0f);
        return true;
    }

    private void AdvanceFrame(int frameCount)
    {
        if (frameCount <= 0)
        {
            manualPlaybackActive = false;
            return;
        }

        int nextFrame = frameIndex + 1;

        if (nextFrame >= frameCount)
        {
            if (!activeLoop)
            {
                manualPlaybackActive = false;
                return;
            }

            nextFrame = 0;
        }

        frameIndex = nextFrame;
        ApplyFrame(frameIndex);
    }

    private void ApplyFrame(int requestedIndex)
    {
        SetTargetsVisible(true);

        Sprite sprite = FindSpriteAtOrNear(activeSpriteFrames, requestedIndex);
        if (sprite != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.enabled = true;
            }

            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = true;
                image.color = WithAlpha(image.color, 1f);
            }
        }

        Texture2D texture = FindTextureAtOrNear(activeTextureFrames, requestedIndex);
        if (texture != null && rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.enabled = true;
            rawImage.color = WithAlpha(rawImage.color, 1f);
        }

        if (crossFadeImage != null)
        {
            crossFadeImage.texture = null;
            crossFadeImage.color = WithAlpha(crossFadeImage.color, 0f);
            crossFadeImage.enabled = true;
        }
    }

    private int GetActiveFrameCount()
    {
        int spriteCount = HasValidSprites(activeSpriteFrames) ? activeSpriteFrames.Length : 0;
        int textureCount = HasValidTextures(activeTextureFrames) ? activeTextureFrames.Length : 0;
        return Mathf.Max(spriteCount, textureCount);
    }

    private bool HasAnyManualFrames()
    {
        return HasValidSprites(seatedIdleFrames) ||
            HasValidSprites(coveredDinnerFrames) ||
            HasValidSprites(eatingFrames) ||
            HasValidSprites(finishedIdleFrames) ||
            HasValidTextures(seatedIdleTextures) ||
            HasValidTextures(coveredDinnerTextures) ||
            HasValidTextures(eatingTextures) ||
            HasValidTextures(finishedIdleTextures);
    }

    private bool HasAnyAnimatorClip()
    {
        return seatedIdleClip != null ||
            coveredDinnerClip != null ||
            eatingLoopClip != null ||
            finishedIdleClip != null;
    }

    private static bool HasValidSprites(Sprite[] frames)
    {
        if (frames == null)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValidTextures(Texture2D[] frames)
    {
        if (frames == null)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Sprite FindSpriteAtOrNear(Sprite[] frames, int requestedIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        int index = NormalizeIndex(requestedIndex, frames.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            int candidate = NormalizeIndex(index + i, frames.Length);
            if (frames[candidate] != null)
            {
                return frames[candidate];
            }
        }

        return null;
    }

    private static Texture2D FindTextureAtOrNear(Texture2D[] frames, int requestedIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        int index = NormalizeIndex(requestedIndex, frames.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            int candidate = NormalizeIndex(index + i, frames.Length);
            if (frames[candidate] != null)
            {
                return frames[candidate];
            }
        }

        return null;
    }

    private static int NormalizeIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int normalized = index % count;
        return normalized < 0 ? normalized + count : normalized;
    }

    private void ResolveTargets()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (image == null)
        {
            image = GetComponentInChildren<Image>(true);
        }

        if (rawImage == null)
        {
            rawImage = GetComponentInChildren<RawImage>(true);
        }

        if (crossFadeImage == null && rawImage != null)
        {
            RawImage[] rawImages = GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (rawImages[i] != null && rawImages[i] != rawImage)
                {
                    crossFadeImage = rawImages[i];
                    break;
                }
            }
        }

        ConfigureImage(image);
        ConfigureRawImage(rawImage);
        ConfigureRawImage(crossFadeImage);
    }

    private void SetTargetsVisible(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }

        if (image != null)
        {
            image.enabled = visible;
            image.gameObject.SetActive(visible);
        }

        if (rawImage != null)
        {
            rawImage.enabled = visible;
            rawImage.gameObject.SetActive(visible);
        }

        if (crossFadeImage != null)
        {
            crossFadeImage.enabled = visible;
            crossFadeImage.gameObject.SetActive(visible);
        }
    }

    private void WarnMissingPhaseOnce(AbominationFullFramePhase phase)
    {
        switch (phase)
        {
            case AbominationFullFramePhase.SeatedIdle:
                if (warnedMissingSeatedIdle)
                {
                    return;
                }

                warnedMissingSeatedIdle = true;
                break;
            case AbominationFullFramePhase.CoveredDinner:
                if (warnedMissingCoveredDinner)
                {
                    return;
                }

                warnedMissingCoveredDinner = true;
                break;
            case AbominationFullFramePhase.Eating:
                if (warnedMissingEating)
                {
                    return;
                }

                warnedMissingEating = true;
                break;
            case AbominationFullFramePhase.FinishedIdle:
                if (warnedMissingFinishedIdle)
                {
                    return;
                }

                warnedMissingFinishedIdle = true;
                break;
        }

        Debug.LogWarning($"Abomination full-frame animator has no clip or frame sequence assigned for {phase}.", this);
    }

    private static void ConfigureImage(Image targetImage)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.raycastTarget = false;
        targetImage.preserveAspect = true;
    }

    private static void ConfigureRawImage(RawImage targetImage)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.raycastTarget = false;
        targetImage.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
