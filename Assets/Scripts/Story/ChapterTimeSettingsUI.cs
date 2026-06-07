using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChapterTimeSettingsUI : MonoBehaviour
{
    [SerializeField] private ChapterClock chapterClock;

    private Canvas canvas;
    private TMP_Text clockText;

    private void Awake()
    {
        ResolveReferences();
        EnsureUI();
        RefreshClockText();
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
        RefreshClockText();
    }

    private void RefreshClockText()
    {
        if (clockText != null && chapterClock != null)
        {
            clockText.text = chapterClock.CurrentTimeLabel;
        }
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
        HideLegacyTimeSettingsPanel(canvas.transform);

        clockText = CreateText("Text_CurrentGameTime", canvas.transform, 24f, TextAlignmentOptions.BottomLeft);
        RectTransform clockRect = clockText.GetComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(0f, 0f);
        clockRect.anchorMax = new Vector2(0f, 0f);
        clockRect.pivot = new Vector2(0f, 0f);
        clockRect.anchoredPosition = new Vector2(18f, 18f);
        clockRect.sizeDelta = new Vector2(220f, 36f);

        clockText.textWrappingMode = TextWrappingModes.NoWrap;
        clockText.gameObject.transform.SetAsLastSibling();
        Shadow clockShadow = clockText.GetComponent<Shadow>();

        if (clockShadow == null)
        {
            clockShadow = clockText.gameObject.AddComponent<Shadow>();
        }

        clockShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        clockShadow.effectDistance = new Vector2(2f, -2f);
        clockShadow.useGraphicAlpha = true;
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

    private static void HideLegacyTimeSettingsPanel(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return;
        }

        Transform panel = canvasTransform.Find("Panel_TimeSettings");

        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
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
