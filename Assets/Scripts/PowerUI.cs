using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUI : MonoBehaviour
{
    private const int UsageBarCount = 5;
    private const float UsageRowY = -16f;

    [Header("References")]
    [SerializeField] private Image powerUIPanel;
    [SerializeField] private PowerManager powerManager;
    [SerializeField] private TMP_Text powerRemainingText;
    [SerializeField] private TMP_Text usageLabel;
    [SerializeField] private Image[] batteryUsageBars;

    [Header("Runtime HUD")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool createRuntimeHud = true;
    [SerializeField] private bool showPowerPercent = true;
    [SerializeField] private bool barsTrackPowerLevel = true;
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 62f);
    [SerializeField] private Vector2 panelOffset = new Vector2(14f, 14f);
    [SerializeField] private Vector2 barSize = new Vector2(13f, 24f);
    [SerializeField] private float barSpacing = 4f;

    [Header("Hide Triggers")]
    [SerializeField] private bool hideWhenKeyPressed = true;
    [SerializeField] private KeyCode hideKey = KeyCode.K;

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Color powerFullColor = new Color(0.24f, 1f, 0.02f, 1f);
    [SerializeField] private Color powerWarningColor = new Color(1f, 0.84f, 0.08f, 1f);
    [SerializeField] private Color powerCriticalColor = new Color(1f, 0.05f, 0.04f, 1f);
    [SerializeField, Range(1, 99)] private int warningPowerThreshold = 50;
    [SerializeField, Range(0, 98)] private int criticalPowerThreshold = 20;
    [SerializeField] private Color inactiveBarColor = new Color(0.08f, 0.08f, 0.08f, 0.8f);
    [SerializeField]
    private Color[] activeBarColors =
    {
        new Color(0.24f, 1f, 0.02f, 1f),
        new Color(0.24f, 1f, 0.02f, 1f),
        new Color(1f, 0.94f, 0.02f, 1f),
        new Color(1f, 0.94f, 0.02f, 1f),
        new Color(1f, 0.04f, 0.02f, 1f)
    };

    private Image[] batteryUsageBackplates;
    private bool hudSuppressed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (powerManager == null)
        {
            return;
        }

        powerManager.OnPowerChanged += OnPowerChanged;
        powerManager.OnUsageChanged += OnUsageChanged;
        powerManager.OnPowerOut += OnPowerOut;
        OnPowerChanged(powerManager.CurrentPowerInteger);
        OnUsageChanged(powerManager.GetUsage());
    }

    private void Update()
    {
        if (hideWhenKeyPressed && !hudSuppressed && Input.GetKeyDown(hideKey))
        {
            HideHud();
        }
    }

    private void OnDisable()
    {
        if (powerManager == null)
        {
            return;
        }

        powerManager.OnPowerChanged -= OnPowerChanged;
        powerManager.OnUsageChanged -= OnUsageChanged;
        powerManager.OnPowerOut -= OnPowerOut;
    }

    private void OnPowerChanged(int power)
    {
        if (powerUIPanel != null && !hudSuppressed && (powerManager == null || !powerManager.IsPowerOut))
        {
            powerUIPanel.gameObject.SetActive(true);
        }

        if (powerRemainingText != null)
        {
            powerRemainingText.text = $"POWER LEFT: {Mathf.Max(0, power)}%";
            powerRemainingText.color = GetPowerTextColor(power);
            powerRemainingText.gameObject.SetActive(showPowerPercent);
        }

        if (barsTrackPowerLevel)
        {
            UpdateBarsForPower(power);
        }
    }

    private void OnUsageChanged(int usage)
    {
        if (barsTrackPowerLevel)
        {
            return;
        }

        UpdateBarsForUsage(usage);
    }

    private void UpdateBarsForUsage(int usage)
    {
        if (batteryUsageBars == null)
        {
            return;
        }

        for (int i = 0; i < batteryUsageBars.Length; i++)
        {
            if (batteryUsageBars[i] != null)
            {
                batteryUsageBars[i].enabled = true;
                batteryUsageBars[i].color = i < usage ? GetBarColor(i) : inactiveBarColor;
            }

            if (batteryUsageBackplates != null && i < batteryUsageBackplates.Length && batteryUsageBackplates[i] != null)
            {
                batteryUsageBackplates[i].enabled = true;
            }
        }
    }

    private void OnPowerOut()
    {
        if (powerUIPanel != null)
        {
            powerUIPanel.gameObject.SetActive(false);
        }
    }

    public void HideHud()
    {
        hudSuppressed = true;

        if (powerUIPanel != null)
        {
            powerUIPanel.gameObject.SetActive(false);
        }
    }

    public void ShowHud()
    {
        hudSuppressed = false;

        if (powerUIPanel != null && (powerManager == null || !powerManager.IsPowerOut))
        {
            powerUIPanel.gameObject.SetActive(true);
        }
    }

    private void ResolveReferences()
    {
        if (powerManager == null)
        {
            powerManager = FindObjectOfType<PowerManager>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindTargetCanvas();
        }

        if (powerRemainingText == null)
        {
            powerRemainingText = FindText("Text_PowerLeft_Number");
        }

        if (powerRemainingText == null)
        {
            powerRemainingText = FindText("Text_PowerLeftNumber");
        }

        if (powerRemainingText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text.name.Contains("Power") && text.name.Contains("Number"))
                {
                    powerRemainingText = text;
                    break;
                }
            }
        }

        if (usageLabel == null)
        {
            usageLabel = FindText("Text_PowerUsage");
        }

        if (powerUIPanel == null)
        {
            powerUIPanel = GetComponent<Image>();
        }

        if (batteryUsageBars == null || batteryUsageBars.Length == 0)
        {
            batteryUsageBars = FindUsageBars();
        }

        if (createRuntimeHud && (powerUIPanel == null || batteryUsageBars == null || batteryUsageBars.Length == 0))
        {
            EnsureRuntimeHud();
        }
    }

    private TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private Image[] FindUsageBars()
    {
        Image[] images = FindObjectsOfType<Image>(true);
        Image[] bars = new Image[UsageBarCount];
        int foundCount = 0;

        foreach (Image image in images)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                if (image.name == $"Image_Bar{i + 1}")
                {
                    bars[i] = image;
                    foundCount++;
                    break;
                }
            }
        }

        if (foundCount == 0)
        {
            return new Image[0];
        }

        return bars;
    }

    private void EnsureRuntimeHud()
    {
        if (targetCanvas == null)
        {
            return;
        }

        GameObject panelObject = FindChildObject(targetCanvas.transform, "Panel_PowerUsage");

        if (panelObject == null)
        {
            panelObject = CreateUiObject("Panel_PowerUsage", targetCanvas.transform, typeof(CanvasRenderer), typeof(Image));
        }

        RectTransform panelRect = panelObject.transform as RectTransform;
        powerUIPanel = panelObject.GetComponent<Image>();

        if (powerUIPanel == null)
        {
            powerUIPanel = panelObject.AddComponent<Image>();
        }

        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        ConfigurePanel(panelRect);

        if (usageLabel == null)
        {
            GameObject labelObject = FindChildObject(panelObject.transform, "Text_PowerUsage");

            if (labelObject == null)
            {
                labelObject = CreateUiObject("Text_PowerUsage", panelObject.transform, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            }

            usageLabel = labelObject.GetComponent<TMP_Text>();
        }

        ConfigureUsageLabel(usageLabel);

        if (powerRemainingText == null)
        {
            GameObject percentObject = FindChildObject(panelObject.transform, "Text_PowerLeft_Number");

            if (percentObject == null)
            {
                percentObject = CreateUiObject("Text_PowerLeft_Number", panelObject.transform, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            }

            powerRemainingText = percentObject.GetComponent<TMP_Text>();
        }

        ConfigurePowerText(powerRemainingText);
        CreateUsageBars(panelObject.transform);
    }

    private Canvas FindTargetCanvas()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null)
        {
            return parentCanvas;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "Canvas_Background" || canvas.name == "Canvas_NightManager")
            {
                return canvas;
            }
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void ConfigurePanel(RectTransform panelRect)
    {
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = panelOffset;
            panelRect.sizeDelta = panelSize;
            panelRect.localScale = Vector3.one;
        }

        powerUIPanel.color = panelColor;
        powerUIPanel.raycastTarget = false;
    }

    private void ConfigureUsageLabel(TMP_Text label)
    {
        if (label == null)
        {
            return;
        }

        RectTransform rectTransform = label.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(12f, UsageRowY);
            rectTransform.sizeDelta = new Vector2(GetBarStartX() - 16f, 26f);
            rectTransform.localScale = Vector3.one;
        }

        label.text = "USAGE:";
        label.color = labelColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 20f;
        label.raycastTarget = false;
    }

    private void ConfigurePowerText(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        RectTransform rectTransform = text.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(12f, -6f);
            rectTransform.sizeDelta = new Vector2(panelSize.x - 24f, 26f);
            rectTransform.localScale = Vector3.one;
        }

        text.color = labelColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 18f;
        text.raycastTarget = false;
        text.gameObject.SetActive(showPowerPercent);
    }

    private void CreateUsageBars(Transform parent)
    {
        batteryUsageBars = new Image[UsageBarCount];
        batteryUsageBackplates = new Image[UsageBarCount];

        float startX = GetBarStartX();

        for (int i = 0; i < UsageBarCount; i++)
        {
            GameObject backplateObject = FindChildObject(parent, $"Image_Bar{i + 1}_Back");

            if (backplateObject == null)
            {
                backplateObject = CreateUiObject($"Image_Bar{i + 1}_Back", parent, typeof(CanvasRenderer), typeof(Image));
            }

            Image backplate = backplateObject.GetComponent<Image>();
            RectTransform backplateRect = backplateObject.transform as RectTransform;
            ConfigureBarRect(backplateRect, startX + i * (barSize.x + barSpacing), UsageRowY - 1f, barSize + new Vector2(4f, 4f));
            backplate.color = new Color(0f, 0f, 0f, 0.72f);
            backplate.raycastTarget = false;
            batteryUsageBackplates[i] = backplate;

            GameObject barObject = FindChildObject(parent, $"Image_Bar{i + 1}");

            if (barObject == null)
            {
                barObject = CreateUiObject($"Image_Bar{i + 1}", parent, typeof(CanvasRenderer), typeof(Image));
            }

            Image bar = barObject.GetComponent<Image>();
            RectTransform barRect = barObject.transform as RectTransform;
            ConfigureBarRect(barRect, startX + i * (barSize.x + barSpacing), UsageRowY, barSize);
            bar.color = inactiveBarColor;
            bar.raycastTarget = false;
            batteryUsageBars[i] = bar;
        }

        if (barsTrackPowerLevel && powerManager != null)
        {
            UpdateBarsForPower(powerManager.CurrentPowerInteger);
        }
    }

    private float GetBarStartX()
    {
        float totalWidth = UsageBarCount * barSize.x + (UsageBarCount - 1) * barSpacing;
        return panelSize.x - totalWidth - 12f;
    }

    private void ConfigureBarRect(RectTransform rectTransform, float x, float y, Vector2 size)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
    }

    private Color GetBarColor(int index)
    {
        if (activeBarColors != null && index >= 0 && index < activeBarColors.Length)
        {
            return activeBarColors[index];
        }

        if (index >= 4)
        {
            return new Color(1f, 0.04f, 0.02f, 1f);
        }

        if (index >= 2)
        {
            return new Color(1f, 0.94f, 0.02f, 1f);
        }

        return new Color(0.24f, 1f, 0.02f, 1f);
    }

    private void UpdateBarsForPower(int power)
    {
        if (batteryUsageBars == null)
        {
            return;
        }

        int clampedPower = Mathf.Clamp(power, 0, 100);
        int activeBars = Mathf.Clamp(Mathf.CeilToInt(clampedPower / 20f), 0, UsageBarCount);
        Color activeColor = GetPowerBarColor(clampedPower);

        for (int i = 0; i < batteryUsageBars.Length; i++)
        {
            if (batteryUsageBars[i] != null)
            {
                batteryUsageBars[i].enabled = true;
                batteryUsageBars[i].color = i < activeBars ? activeColor : inactiveBarColor;
            }

            if (batteryUsageBackplates != null && i < batteryUsageBackplates.Length && batteryUsageBackplates[i] != null)
            {
                batteryUsageBackplates[i].enabled = true;
            }
        }
    }

    private Color GetPowerTextColor(int power)
    {
        int safeWarningThreshold = Mathf.Max(criticalPowerThreshold + 1, warningPowerThreshold);

        if (power > safeWarningThreshold)
        {
            return labelColor;
        }

        if (power > criticalPowerThreshold)
        {
            float t = Mathf.InverseLerp(criticalPowerThreshold, safeWarningThreshold, power);
            return Color.Lerp(powerWarningColor, labelColor, t);
        }

        float criticalT = criticalPowerThreshold <= 0 ? 0f : Mathf.Clamp01((float)power / criticalPowerThreshold);
        return Color.Lerp(powerCriticalColor, powerWarningColor, criticalT);
    }

    private Color GetPowerBarColor(int power)
    {
        int safeWarningThreshold = Mathf.Max(criticalPowerThreshold + 1, warningPowerThreshold);

        if (power > safeWarningThreshold)
        {
            return powerFullColor;
        }

        if (power > criticalPowerThreshold)
        {
            float t = Mathf.InverseLerp(criticalPowerThreshold, safeWarningThreshold, power);
            return Color.Lerp(powerWarningColor, powerFullColor, t);
        }

        float criticalT = criticalPowerThreshold <= 0 ? 0f : Mathf.Clamp01((float)power / criticalPowerThreshold);
        return Color.Lerp(powerCriticalColor, powerWarningColor, criticalT);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);

        foreach (System.Type componentType in components)
        {
            if (gameObject.GetComponent(componentType) == null)
            {
                gameObject.AddComponent(componentType);
            }
        }

        return gameObject;
    }

    private static GameObject FindChildObject(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
