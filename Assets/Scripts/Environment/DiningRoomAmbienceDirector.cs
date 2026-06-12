using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DiningRoomAmbienceDirector : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Texture2D[] frames;
    [SerializeField, Min(0.1f)] private float holdSeconds = 2.6f;
    [SerializeField, Min(0.01f)] private float crossFadeSeconds = 0.7f;
    [SerializeField] private bool pingPong = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Targets")]
    [SerializeField] private RawImage currentImage;
    [SerializeField] private RawImage nextImage;
    [SerializeField] private bool hideWhenNoFrames = true;

    private int frameIndex;
    private int direction = 1;
    private float timer;
    private bool crossFading;

    private void Reset()
    {
        ResolveTargets();
    }

    private void Awake()
    {
        ResolveTargets();
        ApplyInitialFrame();
    }

    private void OnEnable()
    {
        ResolveTargets();
        ApplyInitialFrame();
    }

    private void OnValidate()
    {
        holdSeconds = Mathf.Max(0.1f, holdSeconds);
        crossFadeSeconds = Mathf.Max(0.01f, crossFadeSeconds);
        ResolveTargets();
        ApplyInitialFrame();
    }

    private void Update()
    {
        if (!HasFrames())
        {
            SetTargetsVisible(!hideWhenNoFrames);
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        timer += Mathf.Max(0f, deltaTime);

        if (crossFading)
        {
            float fade01 = Mathf.Clamp01(timer / crossFadeSeconds);
            SetAlpha(currentImage, 1f - fade01);
            SetAlpha(nextImage, fade01);

            if (fade01 >= 1f)
            {
                CompleteCrossFade();
            }

            return;
        }

        if (timer >= holdSeconds)
        {
            BeginCrossFade();
        }
    }

    public void SetFrames(Texture2D[] value)
    {
        frames = value;
        ApplyInitialFrame();
    }

    private void ResolveTargets()
    {
        if (currentImage == null)
        {
            currentImage = GetComponentInChildren<RawImage>(true);
        }

        if (nextImage == null && currentImage != null)
        {
            RawImage[] images = GetComponentsInChildren<RawImage>(true);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != currentImage)
                {
                    nextImage = images[i];
                    break;
                }
            }
        }

        ConfigureImage(currentImage);
        ConfigureImage(nextImage);
    }

    private void ApplyInitialFrame()
    {
        if (!HasFrames())
        {
            SetTargetsVisible(!hideWhenNoFrames);
            return;
        }

        frameIndex = FindValidFrameIndex(frameIndex, 1);
        direction = direction == 0 ? 1 : direction;
        timer = 0f;
        crossFading = false;

        if (currentImage != null)
        {
            currentImage.texture = frames[frameIndex];
            SetAlpha(currentImage, 1f);
            currentImage.gameObject.SetActive(true);
        }

        if (nextImage != null)
        {
            nextImage.texture = null;
            SetAlpha(nextImage, 0f);
            nextImage.gameObject.SetActive(true);
        }
    }

    private void BeginCrossFade()
    {
        int nextFrameIndex = GetNextFrameIndex();

        if (nextFrameIndex < 0 || nextFrameIndex == frameIndex)
        {
            timer = 0f;
            return;
        }

        if (nextImage == null)
        {
            frameIndex = nextFrameIndex;
            if (currentImage != null)
            {
                currentImage.texture = frames[frameIndex];
            }

            timer = 0f;
            return;
        }

        nextImage.texture = frames[nextFrameIndex];
        nextImage.gameObject.SetActive(true);
        SetAlpha(nextImage, 0f);
        timer = 0f;
        crossFading = true;
        frameIndex = nextFrameIndex;
    }

    private void CompleteCrossFade()
    {
        if (currentImage != null && nextImage != null)
        {
            currentImage.texture = nextImage.texture;
            SetAlpha(currentImage, 1f);
            SetAlpha(nextImage, 0f);
        }

        timer = 0f;
        crossFading = false;
    }

    private int GetNextFrameIndex()
    {
        if (frames == null || frames.Length <= 1)
        {
            return frameIndex;
        }

        if (frameIndex < 0 || frameIndex >= frames.Length || frames[frameIndex] == null)
        {
            return FindValidFrameIndex(frameIndex, direction);
        }

        if (CountValidFrames() <= 1)
        {
            return frameIndex;
        }

        if (!pingPong)
        {
            return FindValidFrameIndex(frameIndex + 1, 1);
        }

        direction = direction == 0 ? 1 : (direction > 0 ? 1 : -1);
        int nextIndex = frameIndex + direction;

        for (int guard = 0; guard < frames.Length * 2; guard++)
        {
            if (nextIndex >= frames.Length)
            {
                direction = -1;
                nextIndex = frames.Length - 2;
            }
            else if (nextIndex < 0)
            {
                direction = 1;
                nextIndex = 1;
            }

            if (nextIndex >= 0 && nextIndex < frames.Length && frames[nextIndex] != null)
            {
                return nextIndex;
            }

            nextIndex += direction;
        }

        return frameIndex;
    }

    private bool HasFrames()
    {
        return FindValidFrameIndex(0, 1) >= 0;
    }

    private int CountValidFrames()
    {
        if (frames == null || frames.Length == 0)
        {
            return 0;
        }

        int validCount = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                validCount++;
            }
        }

        return validCount;
    }

    private int FindValidFrameIndex(int startIndex, int step)
    {
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        int count = frames.Length;
        int stepDirection = step == 0 ? 1 : (step > 0 ? 1 : -1);
        int index = NormalizeFrameIndex(startIndex, count);

        for (int i = 0; i < count; i++)
        {
            if (frames[index] != null)
            {
                return index;
            }

            index = NormalizeFrameIndex(index + stepDirection, count);
        }

        return -1;
    }

    private static int NormalizeFrameIndex(int index, int count)
    {
        int normalized = index % count;
        return normalized < 0 ? normalized + count : normalized;
    }

    private void SetTargetsVisible(bool visible)
    {
        if (currentImage != null)
        {
            currentImage.gameObject.SetActive(visible);
        }

        if (nextImage != null)
        {
            nextImage.gameObject.SetActive(visible);
        }
    }

    private static void ConfigureImage(RawImage image)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private static void SetAlpha(RawImage image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
