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
    private const string PanelInteriorName = "Image_SubtitleInterior";
    private const string PortraitFrameName = "Frame_SubtitleSpeakerPortrait";
    private const string PortraitImageName = "Image_SubtitleSpeakerPortrait";
    private const string SpeakerNameplateName = "Image_SubtitleSpeakerNameplate";
    private const string DividerLineName = "Image_SubtitleDivider";
    private const string SpeakerTextName = "Text_SubtitleSpeaker";
    private const string LineTextName = "Text_SubtitleLine";
    private const string SkipButtonName = "Button_SubtitleSkip";
    private const string SkipButtonLabelName = "Text_SubtitleSkip";
    private const string DefaultLineBankResourcePath = "UI/SubtitleLineBank";
    private const float CharactersPerSecond = 24f;

    private struct QueuedSubtitle
    {
        public string LineId;
        public string SpeakerId;
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
    private RectTransform speakerPortraitFrame;
    private Image speakerPortraitImage;
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

        if (!TryResolveLine(
                lineId,
                speakerOverride,
                textOverride,
                out string speakerId,
                out string speaker,
                out string text,
                out float minDuration,
                out float maxDuration,
                out bool lineRequiresAdvance))
        {
            return;
        }

        bool requireAdvance = requireAdvanceOverride ?? lineRequiresAdvance;

        if (requireAdvance)
        {
            ShowPersistent(lineId, speakerId, speaker, text);
            return;
        }

        QueueLine(lineId, speakerId, speaker, text, minDuration, maxDuration);
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

        QueueLine(lineId, ResolveSpeakerId(lineId, speaker), speaker, text, 1.25f, 5f);
    }

    public void ShowPersistentLine(string lineId, string speaker, string text)
    {
        ResolveReferences();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowPersistent(lineId, ResolveSpeakerId(lineId, speaker), speaker, text);
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
        return TryResolveLine(lineId, speakerOverride, textOverride, out _, out speaker, out text, out minDuration, out maxDuration, out _);
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

        ShowNow(lineId, ResolveSpeakerId(lineId, speaker), speaker, text);
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
        if (panelRect != null &&
            speakerPortraitFrame != null &&
            speakerPortraitImage != null &&
            speakerText != null &&
            lineText != null &&
            skipButton != null)
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
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(32f, -150f);
        panelRect.sizeDelta = new Vector2(780f, 225f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.045f, 0.035f, 0.9f);
        panelImage.raycastTarget = false;
        ApplyOutline(panelObject, new Color(0.9f, 0.64f, 0.24f, 0.95f), new Vector2(3f, -3f));

        Image panelInterior = FindOrCreateImage(
            panelObject.transform,
            PanelInteriorName,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-18f, -18f),
            new Color(0.18f, 0.105f, 0.075f, 0.66f));

        panelInterior.raycastTarget = false;

        Image portraitFrame = FindOrCreateImage(
            panelObject.transform,
            PortraitFrameName,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(22f, -10f),
            new Vector2(98f, 206f),
            new Color(1f, 1f, 1f, 0f));
        RemoveOutline(portraitFrame.gameObject);
        speakerPortraitFrame = portraitFrame.GetComponent<RectTransform>();

        speakerPortraitImage = FindOrCreateImage(
            portraitFrame.transform,
            PortraitImageName,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            Color.white);
        speakerPortraitImage.preserveAspect = true;
        speakerPortraitImage.raycastTarget = false;

        Image speakerNameplate = FindOrCreateImage(
            panelObject.transform,
            SpeakerNameplateName,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(146f, -24f),
            new Vector2(598f, 38f),
            new Color(0.36f, 0.16f, 0.13f, 0.94f));
        ApplyOutline(speakerNameplate.gameObject, new Color(0.96f, 0.78f, 0.34f, 0.9f), new Vector2(1.5f, -1.5f));

        Image dividerLine = FindOrCreateImage(
            panelObject.transform,
            DividerLineName,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(146f, -78f),
            new Vector2(598f, 2f),
            new Color(0.86f, 0.61f, 0.27f, 0.88f));
        dividerLine.raycastTarget = false;

        speakerText = FindOrCreateText(
            panelObject.transform,
            SpeakerTextName,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(164f, -29f),
            new Vector2(562f, 28f),
            23f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        speakerText.color = new Color(1f, 0.9f, 0.68f, 1f);

        lineText = FindOrCreateText(
            panelObject.transform,
            LineTextName,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(146f, -94f),
            new Vector2(598f, 82f),
            25f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        lineText.color = new Color(0.96f, 0.9f, 0.78f, 1f);

        skipButton = FindOrCreateSkipButton(panelObject.transform);
        SetVisible(false);
    }

    private Canvas FindSubtitleCanvas()
    {
        GameObject canvasObject = GameObject.Find(CanvasName);
        return canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
    }

    private Image FindOrCreateImage(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null ? existing.gameObject : null;

        if (imageObject == null)
        {
            imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
        }

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        FontStyles fontStyle,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
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
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        return text;
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>();

        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }

        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void RemoveOutline(GameObject target)
    {
        Outline outline = target.GetComponent<Outline>();

        if (outline == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(outline);
        }
        else
        {
            DestroyImmediate(outline);
        }
    }

    private void QueueLine(string lineId, string speakerId, string speaker, string text, float minDuration, float maxDuration)
    {
        queuedSubtitles.Enqueue(new QueuedSubtitle
        {
            LineId = lineId,
            SpeakerId = speakerId,
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
            ShowNow(subtitle.LineId, subtitle.SpeakerId, subtitle.Speaker, subtitle.Text);
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

    private void ShowPersistent(string lineId, string speakerId, string speaker, string text)
    {
        queuedSubtitles.Clear();
        showingPersistentLine = true;

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        ShowNow(lineId, speakerId, speaker, text);
    }

    private void ShowNow(string lineId, string speakerId, string speaker, string text)
    {
        EnsureUI();
        UpdateSpeakerPortrait(speakerId, lineId, speaker);

        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim().ToUpperInvariant();
            speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(speakerText.text));
        }

        if (lineText != null)
        {
            lineText.text = text ?? string.Empty;
        }

        SetVisible(true);
        LogShownLine(lineId, speaker, text);
    }

    private void UpdateSpeakerPortrait(string speakerId, string lineId, string speaker)
    {
        string resolvedSpeakerId = string.IsNullOrWhiteSpace(speakerId)
            ? ResolveSpeakerId(lineId, speaker)
            : speakerId.Trim();
        Sprite portrait = null;
        bool hasPortrait = lineBank != null &&
            lineBank.TryGetSpeakerPortrait(resolvedSpeakerId, out portrait);

        if (speakerPortraitFrame != null)
        {
            speakerPortraitFrame.gameObject.SetActive(hasPortrait);
        }

        if (speakerPortraitImage == null)
        {
            return;
        }

        speakerPortraitImage.sprite = hasPortrait ? portrait : null;
        speakerPortraitImage.enabled = hasPortrait;
        speakerPortraitImage.color = Color.white;
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
        out string speakerId,
        out string speaker,
        out string text,
        out float minDuration,
        out float maxDuration,
        out bool requireAdvance)
    {
        speakerId = string.Empty;
        speaker = string.Empty;
        text = string.Empty;
        minDuration = 1.25f;
        maxDuration = 5f;
        requireAdvance = false;

        SubtitleLine line = null;
        bool hasLine = lineBank != null && lineBank.TryGetLine(lineId, out line);

        if (!hasLine && string.IsNullOrWhiteSpace(textOverride))
        {
            Debug.LogWarning($"[Subtitle] Missing subtitle line '{lineId}'.", this);
            return false;
        }

        if (!hasLine && !string.IsNullOrWhiteSpace(lineId))
        {
            Debug.LogWarning($"[Subtitle] Missing subtitle line '{lineId}'. Showing caller-provided text.", this);
        }

        speakerId = hasLine && !string.IsNullOrWhiteSpace(line.speakerId)
            ? line.speakerId.Trim()
            : string.IsNullOrWhiteSpace(speakerOverride) ? string.Empty : speakerOverride.Trim();
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

    private string ResolveSpeakerId(string lineId, string speaker)
    {
        if (lineBank != null &&
            lineBank.TryGetLine(lineId, out SubtitleLine line) &&
            !string.IsNullOrWhiteSpace(line.speakerId))
        {
            return line.speakerId.Trim();
        }

        return string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim();
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
