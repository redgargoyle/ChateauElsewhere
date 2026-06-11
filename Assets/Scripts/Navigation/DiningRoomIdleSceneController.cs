using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DiningRoomIdleSceneController : MonoBehaviour
{
    [SerializeField] private RawImage currentImage;
    [SerializeField] private RawImage nextImage;
    [SerializeField] private Texture[] idleFrames = new Texture[0];
    [SerializeField] private float secondsPerFrame = 2.25f;
    [SerializeField] private float crossFadeSeconds = 0.75f;
    [SerializeField] private bool pingPong = true;
    [SerializeField] private bool startAtRandomFrame;
    [SerializeField] private bool createImagesIfMissing = true;
    [SerializeField] private string imageObjectName = "DiningRoomIdleScene";
    [SerializeField] private bool sendImagesBehindRoomContent = true;

    private Coroutine playbackRoutine;
    private int frameIndex;
    private int frameDirection = 1;

    private void OnEnable()
    {
        EnsureImageLayers();
        ConfigureImageLayers();

        if (!HasFrames() || currentImage == null)
        {
            return;
        }

        frameIndex = startAtRandomFrame ? Random.Range(0, idleFrames.Length) : 0;
        frameIndex = FindValidFrameIndex(frameIndex);
        frameDirection = frameIndex >= idleFrames.Length - 1 ? -1 : 1;

        SetFrameImmediate(frameIndex);
        playbackRoutine = StartCoroutine(PlayIdleLoop());
    }

    private void OnDisable()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
    }

    private void OnValidate()
    {
        secondsPerFrame = Mathf.Max(0.1f, secondsPerFrame);
        crossFadeSeconds = Mathf.Max(0f, crossFadeSeconds);
    }

    private IEnumerator PlayIdleLoop()
    {
        while (isActiveAndEnabled && HasFrames())
        {
            yield return HoldCurrentFrame();

            int nextFrameIndex = GetNextFrameIndex();

            if (nextFrameIndex == frameIndex)
            {
                continue;
            }

            yield return CrossFadeToFrame(nextFrameIndex);
        }
    }

    private IEnumerator HoldCurrentFrame()
    {
        float elapsed = 0f;

        while (elapsed < secondsPerFrame)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator CrossFadeToFrame(int nextFrameIndex)
    {
        if (currentImage == null || nextImage == null || !IsValidFrameIndex(nextFrameIndex))
        {
            yield break;
        }

        Texture nextTexture = idleFrames[nextFrameIndex];

        if (nextTexture == null)
        {
            yield break;
        }

        nextImage.texture = nextTexture;
        nextImage.gameObject.SetActive(true);
        SetImageAlpha(nextImage, 0f);
        SendLayersBehindRoomContent();

        if (crossFadeSeconds <= 0f)
        {
            CompleteFrameSwap(nextFrameIndex);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < crossFadeSeconds)
        {
            float t = Mathf.Clamp01(elapsed / crossFadeSeconds);
            SetImageAlpha(currentImage, 1f);
            SetImageAlpha(nextImage, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        CompleteFrameSwap(nextFrameIndex);
    }

    private void CompleteFrameSwap(int nextFrameIndex)
    {
        SetImageAlpha(nextImage, 1f);
        SetImageAlpha(currentImage, 0f);

        RawImage previousImage = currentImage;
        currentImage = nextImage;
        nextImage = previousImage;
        frameIndex = nextFrameIndex;

        if (nextImage != null)
        {
            nextImage.gameObject.SetActive(false);
        }

        SendLayersBehindRoomContent();
    }

    private void SetFrameImmediate(int index)
    {
        if (currentImage == null || !IsValidFrameIndex(index))
        {
            return;
        }

        currentImage.texture = idleFrames[index];
        currentImage.gameObject.SetActive(true);
        SetImageAlpha(currentImage, 1f);

        if (nextImage != null)
        {
            nextImage.gameObject.SetActive(false);
            SetImageAlpha(nextImage, 0f);
        }

        SendLayersBehindRoomContent();
    }

    private int GetNextFrameIndex()
    {
        if (idleFrames == null || idleFrames.Length <= 1)
        {
            return frameIndex;
        }

        int nextFrameIndex = frameIndex + frameDirection;
        int attempts = 0;

        while (attempts < idleFrames.Length)
        {
            if (pingPong)
            {
                if (nextFrameIndex >= idleFrames.Length)
                {
                    frameDirection = -1;
                    nextFrameIndex = idleFrames.Length - 2;
                }
                else if (nextFrameIndex < 0)
                {
                    frameDirection = 1;
                    nextFrameIndex = 1;
                }
            }
            else
            {
                if (nextFrameIndex >= idleFrames.Length)
                {
                    nextFrameIndex = 0;
                }
                else if (nextFrameIndex < 0)
                {
                    nextFrameIndex = idleFrames.Length - 1;
                }
            }

            if (IsValidFrameIndex(nextFrameIndex) && idleFrames[nextFrameIndex] != null)
            {
                return nextFrameIndex;
            }

            nextFrameIndex += frameDirection;
            attempts++;
        }

        return frameIndex;
    }

    private int FindValidFrameIndex(int preferredIndex)
    {
        if (!HasFrames())
        {
            return 0;
        }

        int clampedIndex = Mathf.Clamp(preferredIndex, 0, idleFrames.Length - 1);

        if (idleFrames[clampedIndex] != null)
        {
            return clampedIndex;
        }

        for (int i = 0; i < idleFrames.Length; i++)
        {
            if (idleFrames[i] != null)
            {
                return i;
            }
        }

        return 0;
    }

    private bool HasFrames()
    {
        return idleFrames != null && idleFrames.Length > 0;
    }

    private bool IsValidFrameIndex(int index)
    {
        return idleFrames != null && index >= 0 && index < idleFrames.Length;
    }

    private void EnsureImageLayers()
    {
        if (!createImagesIfMissing)
        {
            return;
        }

        if (currentImage == null)
        {
            currentImage = CreateImageLayer("Current");
        }

        if (nextImage == null)
        {
            nextImage = CreateImageLayer("Next");
        }
    }

    private RawImage CreateImageLayer(string suffix)
    {
        string rootName = string.IsNullOrWhiteSpace(imageObjectName) ? "DiningRoomIdleScene" : imageObjectName.Trim();
        GameObject imageObject = new GameObject($"{rootName}_{suffix}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(transform, false);

        RawImage image = imageObject.GetComponent<RawImage>();
        ConfigureImageLayer(image);
        return image;
    }

    private void ConfigureImageLayers()
    {
        ConfigureImageLayer(currentImage);
        ConfigureImageLayer(nextImage);
        SendLayersBehindRoomContent();
    }

    private void ConfigureImageLayer(RawImage image)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.maskable = false;

        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.localRotation = Quaternion.identity;
        imageRect.localScale = Vector3.one;
        imageRect.anchoredPosition = Vector2.zero;
    }

    private void SendLayersBehindRoomContent()
    {
        if (!sendImagesBehindRoomContent)
        {
            return;
        }

        int firstAnimatedSiblingIndex = GetFirstAnimatedSiblingIndex();

        if (currentImage != null)
        {
            currentImage.transform.SetSiblingIndex(firstAnimatedSiblingIndex);
        }

        if (nextImage != null)
        {
            nextImage.transform.SetSiblingIndex(firstAnimatedSiblingIndex + 1);
        }
    }

    private int GetFirstAnimatedSiblingIndex()
    {
        int cameraBackgroundSiblingIndex = -1;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RawImage siblingImage = child.GetComponent<RawImage>();

            if (siblingImage == null ||
                siblingImage == currentImage ||
                siblingImage == nextImage)
            {
                continue;
            }

            cameraBackgroundSiblingIndex = i;
            break;
        }

        return cameraBackgroundSiblingIndex >= 0 ? cameraBackgroundSiblingIndex + 1 : 0;
    }

    private void SetImageAlpha(RawImage image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
