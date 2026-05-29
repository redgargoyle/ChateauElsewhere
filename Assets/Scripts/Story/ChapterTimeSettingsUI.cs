using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChapterTimeSettingsUI : MonoBehaviour
{
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private float minSecondsPerGameMinute = 1f;
    [SerializeField] private float maxSecondsPerGameMinute = 120f;

    private Canvas canvas;
    private Slider speedSlider;
    private TMP_InputField speedInput;
    private TMP_Text clockText;
    private bool isUpdatingUi;

    private void Awake()
    {
        ResolveReferences();
        EnsureUI();
        RefreshUIFromClock();
    }

    private void Update()
    {
        if (clockText != null && chapterClock != null)
        {
            clockText.text = chapterClock.CurrentTimeLabel;
        }
    }

    public void Initialize(ChapterClock clock)
    {
        chapterClock = clock != null ? clock : chapterClock;
        ResolveReferences();
        EnsureUI();
        RefreshUIFromClock();
    }

    private void ApplySpeed(float secondsPerGameMinute)
    {
        if (chapterClock == null)
        {
            return;
        }

        chapterClock.SetSecondsPerGameMinute(Mathf.Clamp(secondsPerGameMinute, minSecondsPerGameMinute, maxSecondsPerGameMinute));
        RefreshUIFromClock();
    }

    private void RefreshUIFromClock()
    {
        if (chapterClock == null)
        {
            return;
        }

        isUpdatingUi = true;
        float value = chapterClock.SecondsPerGameMinute;

        if (speedSlider != null)
        {
            speedSlider.SetValueWithoutNotify(value);
        }

        if (speedInput != null)
        {
            speedInput.SetTextWithoutNotify(value.ToString("0.##"));
        }

        isUpdatingUi = false;
    }

    private void EnsureUI()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("Canvas_ChapterTimeSettings");

        if (canvasObject == null)
        {
            canvasObject = new GameObject("Canvas_ChapterTimeSettings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        EnsureEventSystem();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Panel_TimeSettings", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.sizeDelta = new Vector2(300f, 116f);
        panel.anchoredPosition = new Vector2(-16f, 16f);
        panelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

        TMP_Text label = CreateText("Text_TimeSpeedLabel", panel, 17f, TextAlignmentOptions.Left);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(10f, -8f);
        labelRect.sizeDelta = new Vector2(-20f, 24f);
        label.text = "Seconds Per Game Minute";

        speedSlider = CreateSlider(panel);
        speedSlider.minValue = minSecondsPerGameMinute;
        speedSlider.maxValue = maxSecondsPerGameMinute;
        speedSlider.onValueChanged.AddListener(value =>
        {
            if (!isUpdatingUi)
            {
                ApplySpeed(value);
            }
        });

        speedInput = CreateInput(panel);
        speedInput.onEndEdit.AddListener(value =>
        {
            if (!isUpdatingUi && float.TryParse(value, out float parsed))
            {
                ApplySpeed(parsed);
            }
        });

        clockText = CreateText("Text_CurrentGameTime", panel, 22f, TextAlignmentOptions.Center);
        RectTransform clockRect = clockText.GetComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(0f, 0f);
        clockRect.anchorMax = new Vector2(1f, 0f);
        clockRect.pivot = new Vector2(0.5f, 0f);
        clockRect.anchoredPosition = new Vector2(0f, 6f);
        clockRect.sizeDelta = new Vector2(-20f, 28f);
    }

    private Slider CreateSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("Slider_SecondsPerGameMinute", typeof(RectTransform), typeof(Slider));
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(-50f, -42f);
        rect.sizeDelta = new Vector2(-110f, 20f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.SetParent(rect, false);
        backgroundRect.anchorMin = new Vector2(0f, 0.25f);
        backgroundRect.anchorMax = new Vector2(1f, 0.75f);
        backgroundRect.sizeDelta = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(rect, false);
        fillRect.anchorMin = new Vector2(0f, 0.25f);
        fillRect.anchorMax = new Vector2(1f, 0.75f);
        fillRect.sizeDelta = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.73f, 0.66f, 0.48f, 1f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.SetParent(rect, false);
        handleRect.sizeDelta = new Vector2(14f, 24f);
        handle.GetComponent<Image>().color = new Color(0.9f, 0.86f, 0.74f, 1f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private TMP_InputField CreateInput(Transform parent)
    {
        GameObject inputObject = new GameObject("Input_SecondsPerGameMinute", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-10f, -34f);
        rect.sizeDelta = new Vector2(78f, 34f);
        inputObject.GetComponent<Image>().color = new Color(0.08f, 0.075f, 0.065f, 1f);

        TMP_Text text = CreateText("Text", rect, 18f, TextAlignmentOptions.Center);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-10f, -4f);
        textRect.anchoredPosition = Vector2.zero;

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        return input;
    }

    private TMP_Text CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
