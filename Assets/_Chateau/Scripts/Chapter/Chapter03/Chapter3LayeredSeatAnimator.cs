using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter3LayeredSeatAnimator : MonoBehaviour
{
    [SerializeField] private int seatIndex;
    [SerializeField] private Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet seatSet;

    [SerializeField] private Image baseImage;
    [SerializeField] private SpriteRenderer baseSpriteRenderer;

    [SerializeField] private Image overlayImage;
    [SerializeField] private SpriteRenderer overlaySpriteRenderer;

    [SerializeField] private float idleFrameRate = 2f;
    [SerializeField] private float eatFrameRate = 8f;
    [SerializeField] private float minActionDelay = 0.35f;
    [SerializeField] private float maxActionDelay = 1.25f;

    private Coroutine eatingRoutine;
    private Coroutine idleRoutine;
    private int debugActions;
    private int debugFramesApplied;

    public int DebugActions => debugActions;
    public int DebugFramesApplied => debugFramesApplied;

    private void OnDisable()
    {
        StopRoutines();
        ClearOverlay();
    }

    public void Configure(
        int index,
        Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet set,
        Image baseImageReference,
        SpriteRenderer baseRendererReference,
        Image overlayImageReference,
        SpriteRenderer overlayRendererReference)
    {
        seatIndex = index;
        seatSet = set;
        baseImage = baseImageReference;
        baseSpriteRenderer = baseRendererReference;
        overlayImage = overlayImageReference;
        overlaySpriteRenderer = overlayRendererReference;
        ClearOverlay();
        ApplyBaseSprite(GetIdleSprite());
    }

    public void PlayIdle()
    {
        StopEatingRoutine();
        StopIdleRoutine();
        ClearOverlay();

        if (CountValidFrames(seatSet?.idleFrames) > 1)
        {
            idleRoutine = StartCoroutine(IdleLoop());
            return;
        }

        ApplyBaseSprite(GetIdleSprite());
    }

    public void BeginEating(float initialDelay)
    {
        StopEatingRoutine();
        StopIdleRoutine();
        ClearOverlay();
        eatingRoutine = StartCoroutine(EatingLoop(Mathf.Max(0f, initialDelay)));
    }

    public void StopEatingAndIdle()
    {
        StopEatingRoutine();
        PlayIdle();
    }

    public IEnumerator PlayEatOnce()
    {
        debugActions++;
        StopIdleRoutine();
        ClearOverlay();

        Sprite idle = GetIdleSprite();
        Sprite eatA = GetFrameOrFallback(seatSet?.eatFrames, 0, idle);
        Sprite eatB = GetFrameOrFallback(seatSet?.eatFrames, 1, eatA);
        Sprite[] sequence = { idle, eatA, eatB, eatA, idle };

        yield return PlayBaseSequence(sequence, eatFrameRate);
        ClearOverlay();
        ApplyBaseSprite(idle);
    }

    public IEnumerator PlayTalkOnce()
    {
        debugActions++;
        StopIdleRoutine();
        ClearOverlay();

        Sprite[] frames = CountValidFrames(seatSet?.talkFrames) > 0
            ? seatSet.talkFrames
            : BuildIdleEatAlternation();

        yield return PlayBaseSequence(frames, eatFrameRate);
        ApplyBaseSprite(GetIdleSprite());
    }

    public IEnumerator PlayHeadMoveOnce()
    {
        debugActions++;
        StopIdleRoutine();
        ClearOverlay();

        Sprite[] frames = CountValidFrames(seatSet?.headFrames) > 0
            ? seatSet.headFrames
            : CountValidFrames(seatSet?.talkFrames) > 0 ? seatSet.talkFrames : seatSet?.idleFrames;

        yield return PlayBaseSequence(frames, Mathf.Max(1f, idleFrameRate));
        ApplyBaseSprite(GetIdleSprite());
    }

    public IEnumerator PlayUtensilOnce()
    {
        Sprite[] overlayFrames = CountValidFrames(seatSet?.utensilFrames) > 0
            ? seatSet.utensilFrames
            : seatSet?.handOverlayFrames;

        if (CountValidFrames(overlayFrames) == 0)
        {
            yield return PlayEatOnce();
            yield break;
        }

        debugActions++;
        StopIdleRoutine();
        ApplyBaseSprite(GetIdleSprite());

        yield return PlayOverlaySequence(overlayFrames, eatFrameRate);
        ClearOverlay();
        ApplyBaseSprite(GetIdleSprite());
    }

    private IEnumerator IdleLoop()
    {
        int frameIndex = 0;
        float frameSeconds = GetFrameSeconds(idleFrameRate);

        while (true)
        {
            ApplyBaseSprite(GetFrameOrFallback(seatSet?.idleFrames, frameIndex, GetIdleSprite()));
            frameIndex++;
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private IEnumerator EatingLoop(float initialDelay)
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        yield return PlayEatOnce();

        while (true)
        {
            float delay = Random.Range(Mathf.Max(0f, minActionDelay), Mathf.Max(minActionDelay, maxActionDelay));

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            int action = Random.Range(0, 5);

            switch (action)
            {
                case 0:
                    yield return PlayEatOnce();
                    break;
                case 1:
                    yield return PlayUtensilOnce();
                    break;
                case 2:
                    yield return PlayHeadMoveOnce();
                    break;
                case 3:
                    yield return PlayTalkOnce();
                    break;
                default:
                    ApplyBaseSprite(GetIdleSprite());
                    yield return new WaitForSeconds(Random.Range(0.12f, 0.28f));
                    break;
            }
        }
    }

    private IEnumerator PlayBaseSequence(Sprite[] frames, float frameRate)
    {
        Sprite fallback = GetIdleSprite();
        int frameCount = Mathf.Max(1, frames != null ? frames.Length : 0);
        float frameSeconds = GetFrameSeconds(frameRate);

        for (int i = 0; i < frameCount; i++)
        {
            ApplyBaseSprite(GetFrameOrFallback(frames, i, fallback));
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private IEnumerator PlayOverlaySequence(Sprite[] frames, float frameRate)
    {
        int frameCount = Mathf.Max(1, frames != null ? frames.Length : 0);
        float frameSeconds = GetFrameSeconds(frameRate);

        for (int i = 0; i < frameCount; i++)
        {
            ApplyOverlaySprite(GetFrameOrFallback(frames, i, null));
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private Sprite[] BuildIdleEatAlternation()
    {
        Sprite idle = GetIdleSprite();
        Sprite eatA = GetFrameOrFallback(seatSet?.eatFrames, 0, idle);
        Sprite eatB = GetFrameOrFallback(seatSet?.eatFrames, 1, eatA);
        return new[] { idle, eatA, idle, eatB, idle };
    }

    private Sprite GetIdleSprite()
    {
        return GetFrameOrFallback(seatSet?.idleFrames, 0, null);
    }

    private void ApplyBaseSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        if (baseImage != null)
        {
            baseImage.sprite = sprite;
            baseImage.enabled = true;
            debugFramesApplied++;
        }

        if (baseSpriteRenderer != null)
        {
            baseSpriteRenderer.sprite = sprite;
            baseSpriteRenderer.enabled = true;
            debugFramesApplied++;
        }
    }

    private void ApplyOverlaySprite(Sprite sprite)
    {
        if (overlayImage != null)
        {
            overlayImage.sprite = sprite;
            overlayImage.enabled = sprite != null;
            debugFramesApplied++;
        }

        if (overlaySpriteRenderer != null)
        {
            overlaySpriteRenderer.sprite = sprite;
            overlaySpriteRenderer.enabled = sprite != null;
            debugFramesApplied++;
        }
    }

    private void ClearOverlay()
    {
        if (overlayImage != null)
        {
            overlayImage.sprite = null;
            overlayImage.enabled = false;
        }

        if (overlaySpriteRenderer != null)
        {
            overlaySpriteRenderer.sprite = null;
            overlaySpriteRenderer.enabled = false;
        }
    }

    private void StopRoutines()
    {
        StopEatingRoutine();
        StopIdleRoutine();
    }

    private void StopEatingRoutine()
    {
        if (eatingRoutine != null)
        {
            StopCoroutine(eatingRoutine);
            eatingRoutine = null;
        }
    }

    private void StopIdleRoutine()
    {
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }
    }

    private float GetFrameSeconds(float rate)
    {
        return 1f / Mathf.Max(1f, rate);
    }

    private static Sprite GetFrameOrFallback(Sprite[] frames, int index, Sprite fallback)
    {
        if (frames == null || frames.Length == 0)
        {
            return fallback;
        }

        int safeIndex = Mathf.Clamp(index, 0, frames.Length - 1);

        if (frames[safeIndex] != null)
        {
            return frames[safeIndex];
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return frames[i];
            }
        }

        return fallback;
    }

    private static int CountValidFrames(Sprite[] frames)
    {
        if (frames == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                count++;
            }
        }

        return count;
    }
}
