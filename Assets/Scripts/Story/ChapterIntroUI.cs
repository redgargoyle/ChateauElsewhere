using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChapterIntroUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image fadeImage;
    [SerializeField] private TMP_Text titleText;

    [Header("Title")]
    [SerializeField] private string defaultTitle = "Act 1";
    [SerializeField] private float titleHoldSeconds = 2f;
    [SerializeField] private float fadeFromBlackSeconds = 1.5f;
    [SerializeField] private float titleFontSize = 72f;
    [SerializeField] private Color titleColor = Color.white;

    [Header("Canvas Layer")]
    [SerializeField] private bool useDedicatedOverlayCanvas = true;
    [SerializeField] private string overlayCanvasObjectName = "Canvas_ChapterIntroOverlay";
    [SerializeField] private int overlaySortingOrder = 12000;

    [Header("Fallback Creation")]
    [SerializeField] private bool createRuntimeFallbackIfMissing = true;
    [SerializeField] private string overlayObjectName = "ChapterIntroUI_Runtime";
    [SerializeField] private string fadeObjectName = "Image_ChapterIntroFade";
    [SerializeField] private string titleObjectName = "Text_ChapterIntroTitle";

    private RectTransform overlayRoot;
    private bool warnedMissingFadeImage;
    private bool warnedMissingTitleText;

    public string DefaultTitle => defaultTitle;
    public float TitleHoldSeconds => titleHoldSeconds;
    public float FadeFromBlackSeconds => fadeFromBlackSeconds;

    private void Awake()
    {
        EnsureUI();
        HideOverlay();
    }

    public void ShowBlack()
    {
        EnsureUI();

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
        }

        SetFadeAlpha(1f);

        if (titleText != null)
        {
            titleText.gameObject.SetActive(false);
        }
    }

    public void ShowTitle(string title)
    {
        EnsureUI();

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
        }

        if (titleText == null)
        {
            return;
        }

        titleText.text = string.IsNullOrWhiteSpace(title) ? defaultTitle : title.Trim();
        titleText.alpha = 1f;
        titleText.gameObject.SetActive(true);
        titleText.transform.SetAsLastSibling();
    }

    public IEnumerator FadeFromBlack(float duration)
    {
        EnsureUI();

        if (fadeImage == null)
        {
            yield break;
        }

        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            SetFadeAlpha(0f);
            HideOverlay();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            SetFadeAlpha(Mathf.Lerp(1f, 0f, progress));
            SetTitleAlpha(Mathf.Lerp(1f, 0f, progress));
            yield return null;
        }

        SetFadeAlpha(0f);
        SetTitleAlpha(0f);
        HideOverlay();
    }

    public void HideOverlay()
    {
        if (titleText != null)
        {
            titleText.gameObject.SetActive(false);
        }

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(false);
        }
    }

    public void ValidateRequiredReferences()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: fadeImage.", this);
        }

        if (titleText == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: titleText.", this);
        }
    }

    private void EnsureUI()
    {
        EnsureCanvasLayer();

        if (canvas == null)
        {
            return;
        }

        if (overlayRoot == null)
        {
            overlayRoot = FindOverlayRoot();
        }

        if (overlayRoot == null && createRuntimeFallbackIfMissing)
        {
            overlayRoot = CreateOverlayRoot(canvas.transform);
        }

        ConfigureOverlayRoot();

        if (fadeImage == null)
        {
            fadeImage = FindNamedChild<Image>(fadeObjectName);

            if (fadeImage == null && createRuntimeFallbackIfMissing && overlayRoot != null)
            {
                if (!warnedMissingFadeImage)
                {
                    Debug.LogWarning("ChapterIntroUI missing required field: fadeImage. Created runtime fallback Image_ChapterIntroFade.", this);
                    warnedMissingFadeImage = true;
                }

                fadeImage = CreateFadeImage(overlayRoot);
            }
        }

        MoveComponentToOverlay(fadeImage);

        if (titleText == null)
        {
            titleText = FindNamedChild<TMP_Text>(titleObjectName);

            if (titleText == null && createRuntimeFallbackIfMissing && overlayRoot != null)
            {
                if (!warnedMissingTitleText)
                {
                    Debug.LogWarning("ChapterIntroUI missing required field: titleText. Created runtime fallback Text_ChapterIntroTitle.", this);
                    warnedMissingTitleText = true;
                }

                titleText = CreateTitleText(overlayRoot);
            }
        }

        MoveComponentToOverlay(titleText);

        ConfigureFadeImage();
        ConfigureTitleText();
    }

    private void EnsureCanvasLayer()
    {
        if (useDedicatedOverlayCanvas)
        {
            canvas = GetOrCreateIntroCanvas();
        }
        else if (canvas == null)
        {
            canvas = PostProcessSafeCanvasUtility.GetOrCreateCanvas();
        }

        if (canvas == null)
        {
            return;
        }

        ConfigureIntroCanvas(canvas);
    }

    private Canvas GetOrCreateIntroCanvas()
    {
        string canvasName = string.IsNullOrWhiteSpace(overlayCanvasObjectName)
            ? "Canvas_ChapterIntroOverlay"
            : overlayCanvasObjectName.Trim();

        GameObject canvasObject = GameObject.Find(canvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                canvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        return canvasObject.GetComponent<Canvas>();
    }

    private void ConfigureIntroCanvas(Canvas introCanvas)
    {
        if (introCanvas == null)
        {
            return;
        }

        GameObject canvasObject = introCanvas.gameObject;
        canvasObject.SetActive(true);

        int uiLayer = LayerMask.NameToLayer("UI");

        if (uiLayer >= 0)
        {
            SetLayerRecursively(canvasObject, uiLayer);
        }

        introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        introCanvas.overrideSorting = true;
        introCanvas.sortingOrder = overlaySortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1366f, 768f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        StretchToParent(canvasObject.transform as RectTransform);
    }

    private RectTransform FindOverlayRoot()
    {
        if (canvas == null || string.IsNullOrWhiteSpace(overlayObjectName))
        {
            return null;
        }

        RectTransform[] rectTransforms = canvas.GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];

            if (rectTransform != null && rectTransform.name == overlayObjectName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private RectTransform CreateOverlayRoot(Transform parent)
    {
        GameObject overlayObject = new GameObject(overlayObjectName, typeof(RectTransform));
        overlayObject.transform.SetParent(parent, false);

        RectTransform rectTransform = overlayObject.transform as RectTransform;
        StretchToParent(rectTransform);
        return rectTransform;
    }

    private void ConfigureOverlayRoot()
    {
        if (overlayRoot == null || canvas == null)
        {
            return;
        }

        if (overlayRoot.transform.parent != canvas.transform)
        {
            overlayRoot.transform.SetParent(canvas.transform, false);
        }

        StretchToParent(overlayRoot);
        overlayRoot.SetAsLastSibling();
    }

    private Image CreateFadeImage(RectTransform parent)
    {
        GameObject imageObject = new GameObject(fadeObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        return image;
    }

    private TMP_Text CreateTitleText(RectTransform parent)
    {
        GameObject textObject = new GameObject(titleObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TMP_Text>();
    }

    private T FindNamedChild<T>(string objectName) where T : Component
    {
        if (overlayRoot == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] components = overlayRoot.GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];

            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private void MoveComponentToOverlay(Component component)
    {
        if (component == null || overlayRoot == null)
        {
            return;
        }

        if (component.transform.parent != overlayRoot)
        {
            component.transform.SetParent(overlayRoot, false);
        }
    }

    private void ConfigureFadeImage()
    {
        if (fadeImage == null)
        {
            return;
        }

        RectTransform rectTransform = fadeImage.transform as RectTransform;
        StretchToParent(rectTransform);
        fadeImage.color = new Color(0f, 0f, 0f, fadeImage.color.a);
        fadeImage.raycastTarget = true;
    }

    private void ConfigureTitleText()
    {
        if (titleText == null)
        {
            return;
        }

        RectTransform rectTransform = titleText.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(900f, 180f);
            rectTransform.localScale = Vector3.one;
        }

        titleText.text = string.IsNullOrWhiteSpace(titleText.text) ? defaultTitle : titleText.text;
        titleText.fontSize = titleFontSize;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeImage.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.gameObject.SetActive(color.a > 0.001f);
    }

    private void SetTitleAlpha(float alpha)
    {
        if (titleText == null)
        {
            return;
        }

        titleText.alpha = Mathf.Clamp01(alpha);
    }

    private static void StretchToParent(RectTransform rectTransform)
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

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;

        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
