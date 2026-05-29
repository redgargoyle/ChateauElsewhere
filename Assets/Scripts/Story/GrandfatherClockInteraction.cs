using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GrandfatherClockInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private AudioSource tickingAudioSource;
    [SerializeField] private Sprite clockSprite;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float tickingVolume = 0.28f;

    private Canvas canvas;
    private RectTransform panel;
    private TMP_Text timeText;

    private void Awake()
    {
        ResolveReferences();
        StartTicking();
    }

    private void Update()
    {
        if (panel != null && panel.gameObject.activeSelf)
        {
            RefreshTimeText();
        }
    }

    public void Initialize(ChapterClock clock)
    {
        chapterClock = clock != null ? clock : chapterClock;
        ResolveReferences();
        StartTicking();
    }

    [ContextMenu("Open Clock Close-Up")]
    public void OpenCloseUp()
    {
        ResolveReferences();
        EnsureUI();
        RefreshTimeText();
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Debug.Log("Grandfather clock close-up opened.", this);
    }

    public void CloseCloseUp()
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void RefreshTimeText()
    {
        if (timeText != null)
        {
            timeText.text = chapterClock != null ? chapterClock.CurrentTimeLabel : "5:59 PM";
        }
    }

    private void StartTicking()
    {
        ResolveReferences();

        if (tickingAudioSource == null)
        {
            return;
        }

        if (tickingAudioSource.clip == null)
        {
            tickingAudioSource.clip = CreateTickingClip();
        }

        tickingAudioSource.loop = true;
        tickingAudioSource.playOnAwake = false;
        tickingAudioSource.volume = tickingVolume;

        if (!tickingAudioSource.isPlaying)
        {
            tickingAudioSource.Play();
        }
    }

    private void EnsureUI()
    {
        if (panel != null)
        {
            return;
        }

        canvas = GetOrCreateCanvas();

        GameObject panelObject = new GameObject("Panel_GrandfatherClockCloseUp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(420f, 520f);
        panel.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.018f, 0.015f, 0.96f);

        GameObject clockObject = new GameObject("Image_ClockFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform clockRect = clockObject.GetComponent<RectTransform>();
        clockRect.SetParent(panel, false);
        clockRect.anchorMin = new Vector2(0.5f, 0.5f);
        clockRect.anchorMax = new Vector2(0.5f, 0.5f);
        clockRect.pivot = new Vector2(0.5f, 0.5f);
        clockRect.sizeDelta = new Vector2(250f, 330f);
        clockRect.anchoredPosition = new Vector2(0f, 52f);

        Image clockImage = clockObject.GetComponent<Image>();
        clockImage.color = new Color(0.9f, 0.82f, 0.66f, 1f);
        clockImage.preserveAspect = true;
        clockImage.sprite = clockSprite;

        timeText = CreateText("Text_ClockTime", panel, 42f, FontStyles.Bold);
        RectTransform timeRect = timeText.GetComponent<RectTransform>();
        timeRect.anchorMin = new Vector2(0.5f, 0f);
        timeRect.anchorMax = new Vector2(0.5f, 0f);
        timeRect.sizeDelta = new Vector2(320f, 70f);
        timeRect.anchoredPosition = new Vector2(0f, 88f);

        Button closeButton = CreateButton("Button_CloseClock", panel, "Close");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(150f, 42f);
        closeRect.anchoredPosition = new Vector2(0f, 34f);
        closeButton.onClick.AddListener(CloseCloseUp);

        panel.gameObject.SetActive(false);
    }

    private Canvas GetOrCreateCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas_GrandfatherClockCloseUp");

        if (canvasObject == null)
        {
            canvasObject = new GameObject("Canvas_GrandfatherClockCloseUp", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        Canvas targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.sortingOrder = 11000;
        EnsureEventSystem();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 0.5f;
        return targetCanvas;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.16f, 0.13f, 1f);

        TMP_Text text = CreateText("Text", rect, 22f, FontStyles.Normal);
        text.text = label;
        text.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        text.GetComponent<RectTransform>().anchorMax = Vector2.one;
        text.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        text.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        return buttonObject.GetComponent<Button>();
    }

    private TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (tickingAudioSource == null)
        {
            tickingAudioSource = GetComponent<AudioSource>();
        }

        if (tickingAudioSource == null)
        {
            tickingAudioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private static AudioClip CreateTickingClip()
    {
        const int sampleRate = 44100;
        int samples = sampleRate * 2;
        float[] data = new float[samples];

        AddTick(data, sampleRate, 0f, 860f, 0.22f);
        AddTick(data, sampleRate, 1f, 620f, 0.18f);

        AudioClip clip = AudioClip.Create("RuntimeGrandfatherClockTicking", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void AddTick(float[] data, int sampleRate, float startSeconds, float frequency, float gain)
    {
        int start = Mathf.RoundToInt(startSeconds * sampleRate);
        int length = Mathf.RoundToInt(0.09f * sampleRate);

        for (int i = 0; i < length && start + i < data.Length; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-42f * t);
            data[start + i] += Mathf.Sin(2f * Mathf.PI * frequency * t) * gain * envelope;
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
