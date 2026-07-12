using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChapterIntroUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private Image fadeImage;
    [SerializeField] private TMP_Text titleText;

    [Header("Title")]
    [SerializeField] private string defaultTitle = "Chapter 1";
    [SerializeField] private float titleHoldSeconds = 2f;
    [SerializeField] private float fadeFromBlackSeconds = 1.5f;

    public string DefaultTitle => defaultTitle;
    public float TitleHoldSeconds => titleHoldSeconds;
    public float FadeFromBlackSeconds => fadeFromBlackSeconds;

    private void Awake()
    {
        HideOverlay();
    }

    public void ShowBlack()
    {
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
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            SetFadeAlpha(Mathf.Lerp(1f, 0f, progress));
            SetTitleAlpha(Mathf.Lerp(1f, 0f, progress));
            yield return null;
        }

        SetFadeAlpha(0f);
        SetTitleAlpha(0f);
        HideOverlay();
    }

    public IEnumerator FadeToBlack(float duration)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(false);
        }

        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            SetFadeAlpha(1f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            SetFadeAlpha(Mathf.Lerp(0f, 1f, progress));
            yield return null;
        }

        SetFadeAlpha(1f);
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
        if (canvas == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: canvas.", this);
        }

        if (overlayRoot == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: overlayRoot.", this);
        }

        if (fadeImage == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: fadeImage.", this);
        }

        if (titleText == null)
        {
            Debug.LogWarning("ChapterIntroUI missing required field: titleText.", this);
        }

        if (canvas != null && overlayRoot != null && overlayRoot.parent != canvas.transform)
        {
            Debug.LogWarning("ChapterIntroUI requires overlayRoot to be a direct child of its serialized canvas.", this);
        }

        if (overlayRoot != null && fadeImage != null && fadeImage.transform.parent != overlayRoot)
        {
            Debug.LogWarning("ChapterIntroUI requires fadeImage to be a direct child of overlayRoot.", this);
        }

        if (overlayRoot != null && titleText != null && titleText.transform.parent != overlayRoot)
        {
            Debug.LogWarning("ChapterIntroUI requires titleText to be a direct child of overlayRoot.", this);
        }
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
}
