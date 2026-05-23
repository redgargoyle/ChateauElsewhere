using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NightManager : MonoBehaviour
{
    public static NightManager Active { get; private set; }

    [Header("References")]
    [SerializeField] private TMP_Text nightIntroText;
    [SerializeField] private CanvasGroup nightIntroGroup;
    [SerializeField] private NightTimer nightTimer;
    [SerializeField] private WinSequence winSequence;
    [SerializeField] private PowerManager powerManager;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Canvas targetCanvas;

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private float holdDuration = 2f;

    [Header("Progress")]
    [SerializeField] private string nightPrefsKey = "Night";
    [SerializeField] private int firstNight = 1;
    [SerializeField] private bool startNightOnStart = true;

    private RectTransform runtimeUiRoot;
    private int currentNight;
    private Coroutine introRoutine;
    private bool subscribedToTimer;
    private bool nightEnding;

    public string NightPrefsKey => nightPrefsKey;
    public int CurrentNight => currentNight;

    private void Awake()
    {
        Active = this;
        ResolveReferences();
        currentNight = Mathf.Max(firstNight, PlayerPrefs.GetInt(nightPrefsKey, firstNight));
        PrepareInitialState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToTimer();
    }

    private void Start()
    {
        ResolveReferences();

        if (startNightOnStart)
        {
            StartNight();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();

        if (Active == this)
        {
            Active = null;
        }
    }

    public void StartNight()
    {
        ResolveReferences();
        SubscribeToTimer();

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
        }

        currentNight = Mathf.Max(firstNight, PlayerPrefs.GetInt(nightPrefsKey, firstNight));
        nightEnding = false;
        winSequence?.ResetSequence();
        nightTimer?.ResetTimer();
        powerManager?.ResetPower(false);
        introRoutine = StartCoroutine(PlayNightIntro());
    }

    public void AbortNight()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        nightEnding = false;
        nightTimer?.StopNight(true);
        powerManager?.StopPowerDrain();
        winSequence?.ResetSequence();
        HideNightIntro();
        DisableBlackBackground();
    }

    public static void StopActiveNight()
    {
        Active?.AbortNight();
    }

    public void AdvanceNight()
    {
        currentNight = Mathf.Max(firstNight, currentNight + 1);
        PlayerPrefs.SetInt(nightPrefsKey, currentNight);
        PlayerPrefs.Save();
        StartNight();
    }

    public void EnableBlackBackground()
    {
        ResolveReferences();

        if (panelBackground == null)
        {
            return;
        }

        panelBackground.gameObject.SetActive(true);
        panelBackground.color = Color.black;
        panelBackground.raycastTarget = true;
        panelBackground.transform.SetAsLastSibling();
    }

    public void DisableBlackBackground()
    {
        if (panelBackground == null)
        {
            return;
        }

        panelBackground.color = Color.black;
        panelBackground.gameObject.SetActive(false);
    }

    private void SubscribeToTimer()
    {
        if (nightTimer == null || subscribedToTimer)
        {
            return;
        }

        nightTimer.OnNightEnd += OnNightEnd;
        subscribedToTimer = true;
    }

    private void UnsubscribeFromTimer()
    {
        if (nightTimer == null || !subscribedToTimer)
        {
            return;
        }

        nightTimer.OnNightEnd -= OnNightEnd;
        subscribedToTimer = false;
    }

    private void OnNightEnd()
    {
        if (nightEnding)
        {
            return;
        }

        nightEnding = true;
        powerManager?.StopPowerDrain();

        if (winSequence != null)
        {
            winSequence.PlayWinSequence();
        }
        else
        {
            AdvanceNight();
        }
    }

    private IEnumerator PlayNightIntro()
    {
        EnableBlackBackground();

        if (nightIntroText != null)
        {
            nightIntroText.text = $"Night {currentNight}";
        }

        if (nightIntroGroup != null)
        {
            nightIntroGroup.gameObject.SetActive(true);
            nightIntroGroup.alpha = 1f;
            nightIntroGroup.interactable = false;
            nightIntroGroup.blocksRaycasts = false;
            nightIntroGroup.transform.SetAsLastSibling();
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdDuration));
        yield return FadeNightIntro(1f, 0f);
        HideNightIntro();
        DisableBlackBackground();
        powerManager?.StartPowerDrain();
        nightTimer?.BeginNight();
        introRoutine = null;
    }

    private IEnumerator FadeNightIntro(float start, float end)
    {
        if (nightIntroGroup == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        nightIntroGroup.alpha = start;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            nightIntroGroup.alpha = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        nightIntroGroup.alpha = end;
    }

    private void PrepareInitialState()
    {
        HideNightIntro();
        DisableBlackBackground();
        nightTimer?.StopNight(true);
        winSequence?.ResetSequence();
    }

    private void HideNightIntro()
    {
        if (nightIntroGroup == null)
        {
            return;
        }

        nightIntroGroup.alpha = 0f;
        nightIntroGroup.interactable = false;
        nightIntroGroup.blocksRaycasts = false;
        nightIntroGroup.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (nightTimer == null)
        {
            nightTimer = GetComponent<NightTimer>();
        }

        if (winSequence == null)
        {
            winSequence = GetComponent<WinSequence>();
        }

        if (powerManager == null)
        {
            powerManager = GetComponent<PowerManager>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindTargetCanvas();
        }

        RectTransform uiRoot = EnsureUiRoot();
        panelBackground = EnsurePanelBackground(uiRoot);
        nightIntroGroup = EnsureNightIntroGroup(uiRoot);
        nightIntroText = EnsureText("Text_Night", nightIntroGroup.transform as RectTransform, "Night 1", 48f, TextAlignmentOptions.Center);
        TMP_Text clockText = EnsureClockText(uiRoot);
        CanvasGroup winFadeGroup = EnsureWinFadeGroup(uiRoot);
        TMP_Text winText = EnsureText("Text_6AM", winFadeGroup.transform as RectTransform, "6 AM", 76f, TextAlignmentOptions.Center);

        nightTimer?.Configure(this, clockText);
        winSequence?.Configure(this, winFadeGroup, winText);
    }

    private Canvas FindTargetCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "Canvas_Background" || canvas.name == "Canvas_NightManager")
            {
                return canvas;
            }
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private RectTransform EnsureUiRoot()
    {
        RectTransform ownRect = transform as RectTransform;

        if (ownRect != null && ownRect.GetComponentInParent<Canvas>() != null)
        {
            StretchToParent(ownRect);
            return ownRect;
        }

        if (runtimeUiRoot != null)
        {
            return runtimeUiRoot;
        }

        if (targetCanvas == null)
        {
            return null;
        }

        GameObject rootObject = new GameObject("NightManagerRuntimeUI", typeof(RectTransform));
        runtimeUiRoot = rootObject.GetComponent<RectTransform>();
        runtimeUiRoot.SetParent(targetCanvas.transform, false);
        StretchToParent(runtimeUiRoot);
        return runtimeUiRoot;
    }

    private Image EnsurePanelBackground(RectTransform uiRoot)
    {
        Image image = FindNamedComponent<Image>("Panel_BG_Black");

        if (image == null && uiRoot != null)
        {
            GameObject panel = CreateUiObject("Panel_BG_Black", uiRoot, typeof(CanvasRenderer), typeof(Image));
            image = panel.GetComponent<Image>();
        }

        if (image != null)
        {
            if (uiRoot != null && image.rectTransform.parent != uiRoot)
            {
                image.rectTransform.SetParent(uiRoot, false);
            }

            StretchToParent(image.rectTransform);
            image.color = Color.black;
            image.raycastTarget = true;
        }

        return image;
    }

    private CanvasGroup EnsureNightIntroGroup(RectTransform uiRoot)
    {
        CanvasGroup group = FindNamedComponent<CanvasGroup>("Panel_Nightintro");
        GameObject panelObject = group != null ? group.gameObject : FindChildObject("Panel_Nightintro");

        if (panelObject == null && uiRoot != null)
        {
            panelObject = CreateUiObject("Panel_Nightintro", uiRoot, typeof(CanvasRenderer), typeof(Image));
            Image image = panelObject.GetComponent<Image>();

            if (image != null)
            {
                image.color = Color.clear;
                image.raycastTarget = false;
            }
        }

        if (panelObject == null)
        {
            return null;
        }

        group = panelObject.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = panelObject.AddComponent<CanvasGroup>();
        }

        RectTransform panelRect = panelObject.transform as RectTransform;

        if (uiRoot != null && panelRect != null && panelRect.parent != uiRoot)
        {
            panelRect.SetParent(uiRoot, false);
        }

        StretchToParent(panelRect);
        return group;
    }

    private CanvasGroup EnsureWinFadeGroup(RectTransform uiRoot)
    {
        CanvasGroup group = FindNamedComponent<CanvasGroup>("Panel_WinFade");
        GameObject panelObject = group != null ? group.gameObject : FindChildObject("Panel_WinFade");

        if (panelObject == null && uiRoot != null)
        {
            panelObject = CreateUiObject("Panel_WinFade", uiRoot, typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            Image image = panelObject.GetComponent<Image>();

            if (image != null)
            {
                image.color = Color.black;
                image.raycastTarget = true;
            }
        }

        if (panelObject == null)
        {
            return null;
        }

        group = panelObject.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = panelObject.AddComponent<CanvasGroup>();
        }

        RectTransform panelRect = panelObject.transform as RectTransform;

        if (uiRoot != null && panelRect != null && panelRect.parent != uiRoot)
        {
            panelRect.SetParent(uiRoot, false);
        }

        StretchToParent(panelRect);
        return group;
    }

    private TMP_Text EnsureClockText(RectTransform uiRoot)
    {
        TMP_Text clockText = FindNamedComponent<TMP_Text>("Text_ClockHUD");

        if (clockText == null && uiRoot != null)
        {
            clockText = CreateText("Text_ClockHUD", uiRoot, "12:00 AM", 28f, TextAlignmentOptions.TopRight);
            RectTransform clockRect = clockText.rectTransform;
            clockRect.anchorMin = new Vector2(1f, 1f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.pivot = new Vector2(1f, 1f);
            clockRect.anchoredPosition = new Vector2(-24f, -20f);
            clockRect.sizeDelta = new Vector2(220f, 48f);
        }

        if (clockText != null)
        {
            clockText.raycastTarget = false;
        }

        return clockText;
    }

    private TMP_Text EnsureText(string objectName, RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        TMP_Text tmpText = FindNamedComponent<TMP_Text>(objectName);

        if (tmpText == null && parent != null)
        {
            tmpText = CreateText(objectName, parent, text, fontSize, alignment);
            StretchToParent(tmpText.rectTransform);
        }

        if (tmpText != null)
        {
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.alignment = alignment;
            tmpText.color = Color.white;
            tmpText.raycastTarget = false;
        }

        return tmpText;
    }

    private TMP_Text CreateText(string objectName, RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.alignment = alignment;
        tmpText.color = Color.white;
        return tmpText;
    }

    private GameObject CreateUiObject(string objectName, RectTransform parent, params System.Type[] components)
    {
        GameObject uiObject = new GameObject(objectName, components);
        RectTransform rectTransform = uiObject.transform as RectTransform;

        if (parent != null)
        {
            rectTransform.SetParent(parent, false);
        }

        return uiObject;
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component.name == objectName)
            {
                return component;
            }
        }

        if (targetCanvas != null)
        {
            components = targetCanvas.GetComponentsInChildren<T>(true);

            foreach (T component in components)
            {
                if (component.name == objectName)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private GameObject FindChildObject(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        if (targetCanvas != null)
        {
            children = targetCanvas.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.name == objectName)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private void StretchToParent(RectTransform rectTransform)
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
