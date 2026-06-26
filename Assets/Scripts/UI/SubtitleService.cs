using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SubtitleService : MonoBehaviour
{
    private const string ServiceObjectName = "SubtitleService";
    private const string CanvasName = "Canvas_Subtitles";
    private const string PanelName = "Panel_Subtitle";
    private const string SpeakerTextName = "Text_SubtitleSpeaker";
    private const string LineTextName = "Text_SubtitleLine";
    private const string SkipButtonName = "Button_SubtitleSkip";
    private const string SkipButtonLabelName = "Text_SubtitleSkip";
    private const string DefaultLineBankResourcePath = "UI/SubtitleLineBank";
    private const float CharactersPerSecond = 24f;

    private struct QueuedSubtitle
    {
        public string LineId;
        public string Speaker;
        public string Text;
        public float MinDuration;
        public float MaxDuration;
    }

    [SerializeField] private SubtitleLineBank lineBank;
    [SerializeField] private string lineBankResourcePath = DefaultLineBankResourcePath;
    [SerializeField] private bool subtitleDebugMode;
    [SerializeField] private GuestVoiceLinePlayback voicePlayback;
    [SerializeField] private RoomNavigationManager navigationManager;

    private readonly Queue<QueuedSubtitle> queuedSubtitles = new Queue<QueuedSubtitle>();
    private RectTransform panelRect;
    private TMP_Text speakerText;
    private TMP_Text lineText;
    private Button skipButton;
    private TMP_Text skipButtonLabel;
    private Action skipCallback;
    private Coroutine autoHideRoutine;
    private bool showingPersistentLine;
    private bool skipCurrentLineRequested;
    private bool subscribedToRoomChanges;

    public static SubtitleService FindOrCreate()
    {
        SubtitleService existing = FindAnyObjectByType<SubtitleService>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.ResolveReferences();
            return existing;
        }

        GameObject serviceObject = new GameObject(ServiceObjectName, typeof(SubtitleService));
        SubtitleService service = serviceObject.GetComponent<SubtitleService>();
        service.ResolveReferences();
        return service;
    }

    public void SetDebugMode(bool enabled)
    {
        subtitleDebugMode = enabled;
    }

    public void ShowLine(string lineId)
    {
        ShowLine(lineId, null, null, null);
    }

    public void ShowLine(string lineId, string speakerOverride, string textOverride, bool? requireAdvanceOverride = null)
    {
        ResolveReferences();

        SubtitleLine line = null;
        bool hasLine = lineBank != null && lineBank.TryGetLine(lineId, out line);

        if (!hasLine && string.IsNullOrWhiteSpace(textOverride))
        {
            Debug.LogWarning($"[Subtitle] Missing subtitle line '{lineId}'.", this);
            return;
        }

        if (!hasLine)
        {
            Debug.LogWarning($"[Subtitle] Missing subtitle line '{lineId}'. Showing caller-provided text.", this);
        }

        string speaker = string.IsNullOrWhiteSpace(speakerOverride)
            ? (hasLine ? line.speakerDisplayName : string.Empty)
            : speakerOverride.Trim();
        string text = string.IsNullOrWhiteSpace(textOverride)
            ? (hasLine ? line.text : string.Empty)
            : textOverride.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning($"[Subtitle] Subtitle line '{lineId}' has no text.", this);
            return;
        }

        bool requireAdvance = requireAdvanceOverride ?? (hasLine && line.requireAdvance);

        if (requireAdvance)
        {
            ShowPersistent(lineId, speaker, text);
            return;
        }

        float minDuration = hasLine ? line.minDuration : 1.25f;
        float maxDuration = hasLine ? line.maxDuration : 5f;
        QueueLine(lineId, speaker, text, minDuration, maxDuration);
    }

    public void ShowLine(string speaker, string text)
    {
        ShowLine(string.Empty, speaker, text);
    }

    public void ShowLine(string lineId, string speaker, string text)
    {
        ResolveReferences();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        QueueLine(lineId, speaker, text, 1.25f, 5f);
    }

    public void ShowPersistentLine(string lineId, string speaker, string text)
    {
        ResolveReferences();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowPersistent(lineId, speaker, text);
    }

    public bool TryResolveSpeechLine(
        string lineId,
        string speakerOverride,
        string textOverride,
        out string speaker,
        out string text,
        out float minDuration,
        out float maxDuration)
    {
        ResolveReferences();
        return TryResolveLine(lineId, speakerOverride, textOverride, out speaker, out text, out minDuration, out maxDuration, out _);
    }

    public void ShowSpeechLine(string lineId, string speaker, string text, bool showSkipButton, Action onSkip)
    {
        ResolveReferences();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        queuedSubtitles.Clear();
        showingPersistentLine = true;

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        ShowNow(lineId, speaker, text);
        ConfigureSkipButton(showSkipButton, onSkip);
    }

    public void HideCurrent()
    {
        showingPersistentLine = false;
        ConfigureSkipButton(false, null);

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        SetVisible(false);
    }

    public void ClearAll()
    {
        queuedSubtitles.Clear();
        skipCurrentLineRequested = false;
        HideCurrent();
        SpeakingCharacterIndicator.HideAnyCurrent();
        voicePlayback?.StopCurrentLine();
        GuestVoiceLinePlayback.StopAnyCurrentLine();
    }

    private void Update()
    {
        if (autoHideRoutine != null && !showingPersistentLine && Input.GetKeyDown(KeyCode.Escape))
        {
            skipCurrentLineRequested = true;
        }
    }

    private void ResolveReferences()
    {
        if (lineBank == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(lineBankResourcePath)
                ? DefaultLineBankResourcePath
                : lineBankResourcePath.Trim();
            lineBank = Resources.Load<SubtitleLineBank>(resourcePath);
        }

        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
        EnsureUI();
    }

    private void OnDisable()
    {
        UnregisterRoomChangeHandler();
    }

    private void EnsureUI()
    {
        if (panelRect != null && speakerText != null && lineText != null && skipButton != null)
        {
            return;
        }

        Canvas canvas = FindSubtitleCanvas();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        Transform existingPanel = canvas.transform.Find(PanelName);
        GameObject panelObject = existingPanel != null ? existingPanel.gameObject : null;

        if (panelObject == null)
        {
            panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvas.transform, false);
        }

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 58f);
        panelRect.sizeDelta = new Vector2(1120f, 126f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        speakerText = FindOrCreateText(panelObject.transform, SpeakerTextName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-64f, 32f), 24f, FontStyles.Bold);
        lineText = FindOrCreateText(panelObject.transform, LineTextName, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-64f, 72f), 30f, FontStyles.Normal);
        skipButton = FindOrCreateSkipButton(panelObject.transform);
        SetVisible(false);
    }

    private Canvas FindSubtitleCanvas()
    {
        GameObject canvasObject = GameObject.Find(CanvasName);
        return canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
    }

    private TMP_Text FindOrCreateText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        float fontSize,
        FontStyles fontStyle)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null ? existing.gameObject : null;

        if (textObject == null)
        {
            textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        return text;
    }

    private void QueueLine(string lineId, string speaker, string text, float minDuration, float maxDuration)
    {
        queuedSubtitles.Enqueue(new QueuedSubtitle
        {
            LineId = lineId,
            Speaker = speaker,
            Text = text,
            MinDuration = minDuration,
            MaxDuration = maxDuration
        });

        if (autoHideRoutine == null && !showingPersistentLine)
        {
            autoHideRoutine = StartCoroutine(PlayQueue());
        }
    }

    private IEnumerator PlayQueue()
    {
        while (queuedSubtitles.Count > 0)
        {
            QueuedSubtitle subtitle = queuedSubtitles.Dequeue();
            ShowNow(subtitle.LineId, subtitle.Speaker, subtitle.Text);
            float duration = GetDuration(subtitle.Text, subtitle.MinDuration, subtitle.MaxDuration);
            float elapsed = 0f;

            while (elapsed < duration && !skipCurrentLineRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            skipCurrentLineRequested = false;
            SetVisible(false);
            yield return new WaitForSecondsRealtime(0.08f);
        }

        autoHideRoutine = null;
    }

    private void ShowPersistent(string lineId, string speaker, string text)
    {
        queuedSubtitles.Clear();
        showingPersistentLine = true;

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        ShowNow(lineId, speaker, text);
    }

    private void ShowNow(string lineId, string speaker, string text)
    {
        EnsureUI();

        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim();
            speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(speakerText.text));
        }

        if (lineText != null)
        {
            lineText.text = text ?? string.Empty;
        }

        SetVisible(true);
        LogShownLine(lineId, speaker, text);
    }

    private void SetVisible(bool visible)
    {
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            ConfigureSkipButton(false, null);
        }
    }

    private void ResolveRoomNavigation()
    {
        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void RegisterRoomChangeHandler()
    {
        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        navigationManager.OnCurrentRoomChanged.AddListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnregisterRoomChangeHandler()
    {
        if (!subscribedToRoomChanges || navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        queuedSubtitles.Clear();
        skipCurrentLineRequested = false;
        showingPersistentLine = false;

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        SetVisible(false);
        SpeakingCharacterIndicator.HideAnyCurrent();
        voicePlayback?.StopCurrentLine();
        GuestVoiceLinePlayback.StopAnyCurrentLine();
    }

    private bool TryResolveLine(
        string lineId,
        string speakerOverride,
        string textOverride,
        out string speaker,
        out string text,
        out float minDuration,
        out float maxDuration,
        out bool requireAdvance)
    {
        speaker = string.Empty;
        text = string.Empty;
        minDuration = 1.25f;
        maxDuration = 5f;
        requireAdvance = false;

        SubtitleLine line = null;
        bool hasLine = lineBank != null && lineBank.TryGetLine(lineId, out line);

        if (!hasLine && string.IsNullOrWhiteSpace(textOverride))
        {
            return false;
        }

        if (!hasLine && !string.IsNullOrWhiteSpace(lineId))
        {
            Debug.LogWarning($"[Subtitle] Missing subtitle line '{lineId}'. Showing caller-provided text.", this);
        }

        speaker = string.IsNullOrWhiteSpace(speakerOverride)
            ? (hasLine ? line.speakerDisplayName : string.Empty)
            : speakerOverride.Trim();
        text = hasLine && !string.IsNullOrWhiteSpace(line.text)
            ? line.text.Trim()
            : string.IsNullOrWhiteSpace(textOverride) ? string.Empty : textOverride.Trim();
        minDuration = hasLine ? line.minDuration : minDuration;
        maxDuration = hasLine ? line.maxDuration : maxDuration;
        requireAdvance = hasLine && line.requireAdvance;
        return !string.IsNullOrWhiteSpace(text);
    }

    private void ConfigureSkipButton(bool visible, Action onSkip)
    {
        skipCallback = visible ? onSkip : null;

        if (skipButton == null)
        {
            return;
        }

        skipButton.onClick.RemoveAllListeners();

        if (visible && onSkip != null)
        {
            skipButton.onClick.AddListener(HandleSkipButtonClicked);
        }

        skipButton.gameObject.SetActive(visible && onSkip != null);
    }

    private void HandleSkipButtonClicked()
    {
        skipCallback?.Invoke();
    }

    private Button FindOrCreateSkipButton(Transform parent)
    {
        Transform existing = parent.Find(SkipButtonName);
        GameObject buttonObject = existing != null ? existing.gameObject : null;

        if (buttonObject == null)
        {
            buttonObject = new GameObject(SkipButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-14f, 12f);
        rect.sizeDelta = new Vector2(92f, 30f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.11f, 0.1f, 0.92f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        skipButtonLabel = FindOrCreateText(
            buttonObject.transform,
            SkipButtonLabelName,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            16f,
            FontStyles.Bold);
        skipButtonLabel.text = "Skip";
        skipButtonLabel.alignment = TextAlignmentOptions.Center;
        skipButtonLabel.raycastTarget = false;
        buttonObject.SetActive(false);
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void LogShownLine(string lineId, string speaker, string text)
    {
        if (!subtitleDebugMode)
        {
            return;
        }

        string cleanLineId = string.IsNullOrWhiteSpace(lineId) ? "<inline>" : lineId.Trim();
        string cleanSpeaker = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker.Trim();
        Debug.Log($"[Subtitle] {cleanLineId}: {cleanSpeaker}: {text}", this);
    }

    private static float GetDuration(string text, float minDuration, float maxDuration)
    {
        float low = Mathf.Max(0.1f, minDuration);
        float high = Mathf.Max(low, maxDuration);
        float readableSeconds = low + Mathf.Max(0f, (text ?? string.Empty).Length) / CharactersPerSecond;
        return Mathf.Clamp(readableSeconds, low, high);
    }
}
