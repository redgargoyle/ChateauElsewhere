using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter2InteractionHUD : MonoBehaviour
{
    private const string CanvasName = "Canvas_Chapter2HUD";
    private const string ObjectiveTextName = "Text_Chapter2Objective";
    private const string StatusTextName = "Text_Chapter2Status";
    private const string FoundListTextName = "Text_Chapter2FoundList";
    private const string DialoguePanelName = "Panel_Chapter2Dialogue";
    private const string DialogueSpeakerTextName = "Text_Chapter2DialogueSpeaker";
    private const string DialogueLineTextName = "Text_Chapter2DialogueLine";
    private const string DialogueSkipButtonName = "Button_Chapter2DialogueSkip";
    private const string DialogueSkipLabelName = "Text_Chapter2DialogueSkip";
    private const string DialogueChoiceButtonNamePrefix = "Button_Chapter2DialogueChoice";
    private const string DialogueChoiceLabelName = "Text_Chapter2DialogueChoice";
    private const string ClockStrikePanelName = "Panel_Chapter2ClockStrike";
    private const string ClockStrikeImageName = "Image_Chapter2ClockStrikeFace";
    private const string ClockStrikeTextName = "Text_Chapter2ClockStrike";
    private const string ClockStrikeHandsRootName = "Rect_Chapter2ClockStrikeHands";
    private const string ClockStrikeHourHighlightName = "Image_Chapter2ClockStrikeHourHighlight";
    private const string ClockStrikeHourHandName = "Image_Chapter2ClockStrikeHourHand";
    private const string ClockStrikeMinuteHighlightName = "Image_Chapter2ClockStrikeMinuteHighlight";
    private const string ClockStrikeMinuteHandName = "Image_Chapter2ClockStrikeMinuteHand";
    private const string ClockStrikeSecondHandName = "Image_Chapter2ClockStrikeSecondHand";
    private const string ClockStrikeSecondTailName = "Image_Chapter2ClockStrikeSecondTail";
    private const string ClockStrikeCenterPinName = "Image_Chapter2ClockStrikeCenterPin";
    private const string SevenOClockClockFaceAssetPath = "Assets/Art/clockcutout7oclock.png";
    private const string SevenOClockClockFaceResourceName = "clockcutout7oclock";
    private const string PrimaryButtonName = "Button_Chapter2PrimaryAction";
    private const string PrimaryButtonLabelName = "Text_Chapter2PrimaryAction";
    private const int DialogueChoiceCount = 3;
    private const float ClockStrikeFaceCenterYOffset = 0.267f;
    private const float ClockStrikeHandsDiameterScale = 0.22f;
    private const float ClockStrikeSecondTicksPerSecond = 6f;

    private Chapter2Controller controller;
    private ChapterClock chapterClock;
    private Canvas canvas;
    private TMP_Text objectiveText;
    private TMP_Text statusText;
    private TMP_Text foundListText;
    private GameObject dialoguePanel;
    private TMP_Text dialogueSpeakerText;
    private TMP_Text dialogueLineText;
    private GameObject clockStrikePanel;
    [SerializeField] private Sprite clockStrikeClockFaceSprite;
    private Image clockStrikeImage;
    private TMP_Text clockStrikeText;
    private RectTransform clockStrikeHandsRoot;
    private RectTransform clockStrikeHourHighlight;
    private RectTransform clockStrikeHourHand;
    private RectTransform clockStrikeMinuteHighlight;
    private RectTransform clockStrikeMinuteHand;
    private RectTransform clockStrikeSecondHand;
    private RectTransform clockStrikeSecondTail;
    private RectTransform clockStrikeCenterPin;
    private readonly Button[] dialogueChoiceButtons = new Button[DialogueChoiceCount];
    private readonly TMP_Text[] dialogueChoiceLabels = new TMP_Text[DialogueChoiceCount];
    private readonly Action[] dialogueChoiceCallbacks = new Action[DialogueChoiceCount];
    private Button dialogueSkipButton;
    private Action dialogueSkipCallback;
    private Button primaryButton;
    private TMP_Text primaryButtonLabel;
    private Action primaryActionCallback;
    private string statusOverrideText;
    private bool dialogueChoicesInteractable = true;
    private float clockStrikeStartedAt;
    private static Sprite clockStrikeHandSprite;
    private static Sprite clockStrikePinSprite;

    private void Update()
    {
        UpdateStatusText();
        RefreshClockStrikeHands();
    }

    public void Initialize(Chapter2Controller controller, ChapterClock clock)
    {
        this.controller = controller;
        chapterClock = clock;
        EnsureUI();
        UpdateStatusText();
    }

    public void SetObjective(string text)
    {
        EnsureUI();

        if (objectiveText != null)
        {
            objectiveText.text = text ?? string.Empty;
            objectiveText.gameObject.SetActive(!string.IsNullOrWhiteSpace(objectiveText.text));
        }
    }

    public void SetStatus(string text)
    {
        EnsureUI();
        statusOverrideText = text;
        UpdateStatusText();
    }

    public void ClearStatus()
    {
        statusOverrideText = null;
        UpdateStatusText();
    }

    public void SetPrimaryAction(string label, Action callback)
    {
        EnsureUI();
        primaryActionCallback = callback;

        if (primaryButtonLabel != null)
        {
            primaryButtonLabel.text = label ?? string.Empty;
        }

        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(HandlePrimaryActionClicked);
            primaryButton.interactable = callback != null;
            primaryButton.gameObject.SetActive(true);
        }
    }

    public void ClearPrimaryAction()
    {
        primaryActionCallback = null;

        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.gameObject.SetActive(false);
        }
    }

    public void SetDialogue(string speaker, string line)
    {
        EnsureUI();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (dialogueSpeakerText != null)
        {
            dialogueSpeakerText.text = speaker ?? string.Empty;
            dialogueSpeakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(dialogueSpeakerText.text));
        }

        if (dialogueLineText != null)
        {
            dialogueLineText.text = line ?? string.Empty;
            dialogueLineText.gameObject.SetActive(!string.IsNullOrWhiteSpace(dialogueLineText.text));
        }
    }

    public void SetDialogueChoices(
        string firstLabel,
        Action firstCallback,
        string secondLabel = null,
        Action secondCallback = null,
        string thirdLabel = null,
        Action thirdCallback = null)
    {
        EnsureUI();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        SetDialogueChoice(0, firstLabel, firstCallback);
        SetDialogueChoice(1, secondLabel, secondCallback);
        SetDialogueChoice(2, thirdLabel, thirdCallback);
    }

    public void SetDialogueChoicesInteractable(bool interactable)
    {
        dialogueChoicesInteractable = interactable;

        for (int i = 0; i < dialogueChoiceButtons.Length; i++)
        {
            Button button = dialogueChoiceButtons[i];

            if (button != null && button.gameObject.activeSelf)
            {
                button.interactable = interactable && dialogueChoiceCallbacks[i] != null;
            }
        }
    }

    public void SetDialogueSkipAction(Action callback)
    {
        EnsureUI();
        dialogueSkipCallback = callback;

        if (dialogueSkipButton == null)
        {
            return;
        }

        dialogueSkipButton.onClick.RemoveAllListeners();

        if (callback != null)
        {
            dialogueSkipButton.onClick.AddListener(HandleDialogueSkipClicked);
        }

        dialogueSkipButton.gameObject.SetActive(callback != null);
    }

    public void ClearDialogue()
    {
        dialogueChoicesInteractable = true;
        dialogueSkipCallback = null;

        for (int i = 0; i < dialogueChoiceCallbacks.Length; i++)
        {
            dialogueChoiceCallbacks[i] = null;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (dialogueSkipButton != null)
        {
            dialogueSkipButton.onClick.RemoveAllListeners();
            dialogueSkipButton.gameObject.SetActive(false);
        }

        for (int i = 0; i < dialogueChoiceButtons.Length; i++)
        {
            if (dialogueChoiceButtons[i] != null)
            {
                dialogueChoiceButtons[i].onClick.RemoveAllListeners();
                dialogueChoiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void ShowClockStrike(string timeLabel)
    {
        EnsureUI();
        Sprite clockFaceSprite = ResolveClockStrikeClockFaceSprite();

        if (clockStrikePanel != null)
        {
            clockStrikePanel.SetActive(true);
            clockStrikePanel.transform.SetAsLastSibling();
        }

        if (clockStrikeImage != null)
        {
            clockStrikeImage.sprite = clockFaceSprite;
            clockStrikeImage.gameObject.SetActive(clockFaceSprite != null);
        }

        if (clockStrikeText != null)
        {
            clockStrikeText.text = string.IsNullOrWhiteSpace(timeLabel) ? "7:00 PM" : timeLabel.Trim();
            ApplyClockStrikeTextLayout(clockFaceSprite != null);
        }

        clockStrikeStartedAt = Time.unscaledTime;
        LayoutClockStrikeHands();
        RefreshClockStrikeHands(true);
    }

    public void ClearClockStrike()
    {
        if (clockStrikePanel != null)
        {
            clockStrikePanel.SetActive(false);
        }
    }

    public void SetFoundGuests(IReadOnlyList<string> names, int foundCount, int totalCount)
    {
        EnsureUI();

        if (foundListText == null)
        {
            return;
        }

        if (totalCount <= 0)
        {
            foundListText.text = string.Empty;
            foundListText.gameObject.SetActive(false);
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Guests Found ");
        builder.Append(Mathf.Clamp(foundCount, 0, totalCount));
        builder.Append('/');
        builder.Append(totalCount);

        if (names != null)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i]))
                {
                    continue;
                }

                builder.AppendLine();
                builder.Append(i + 1);
                builder.Append(". ");
                builder.Append(names[i].Trim());
            }
        }

        foundListText.text = builder.ToString();
        foundListText.gameObject.SetActive(true);
    }

    private void HandlePrimaryActionClicked()
    {
        primaryActionCallback?.Invoke();
    }

    private void HandleDialogueSkipClicked()
    {
        dialogueSkipCallback?.Invoke();
    }

    private void EnsureUI()
    {
        GameObject canvasObject = GameObject.Find(CanvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9200;
        EnsureEventSystem();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;

        objectiveText = FindOrCreateText(root, ObjectiveTextName, 24f, TextAlignmentOptions.Center);
        RectTransform objectiveRect = objectiveText.GetComponent<RectTransform>();
        objectiveRect.anchorMin = new Vector2(0.5f, 0f);
        objectiveRect.anchorMax = new Vector2(0.5f, 0f);
        objectiveRect.pivot = new Vector2(0.5f, 0f);
        objectiveRect.anchoredPosition = new Vector2(0f, 104f);
        objectiveRect.sizeDelta = new Vector2(900f, 76f);

        statusText = FindOrCreateText(root, StatusTextName, 18f, TextAlignmentOptions.Left);
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(18f, -18f);
        statusRect.sizeDelta = new Vector2(500f, 90f);

        foundListText = FindOrCreateText(root, FoundListTextName, 18f, TextAlignmentOptions.TopRight);
        RectTransform foundListRect = foundListText.GetComponent<RectTransform>();
        foundListRect.anchorMin = new Vector2(1f, 1f);
        foundListRect.anchorMax = new Vector2(1f, 1f);
        foundListRect.pivot = new Vector2(1f, 1f);
        foundListRect.anchoredPosition = new Vector2(-24f, -150f);
        foundListRect.sizeDelta = new Vector2(360f, 220f);

        dialoguePanel = FindOrCreatePanel(root, DialoguePanelName, new Color(0.03f, 0.025f, 0.02f, 0.9f));
        RectTransform dialogueRect = dialoguePanel.GetComponent<RectTransform>();
        dialogueRect.anchorMin = new Vector2(0.5f, 0f);
        dialogueRect.anchorMax = new Vector2(0.5f, 0f);
        dialogueRect.pivot = new Vector2(0.5f, 0f);
        dialogueRect.anchoredPosition = new Vector2(0f, 128f);
        dialogueRect.sizeDelta = new Vector2(820f, 190f);

        dialogueSpeakerText = FindOrCreateText(dialogueRect, DialogueSpeakerTextName, 20f, TextAlignmentOptions.Left);
        RectTransform speakerRect = dialogueSpeakerText.GetComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0f, 1f);
        speakerRect.offsetMin = new Vector2(24f, -42f);
        speakerRect.offsetMax = new Vector2(-24f, -12f);

        dialogueLineText = FindOrCreateText(dialogueRect, DialogueLineTextName, 18f, TextAlignmentOptions.TopLeft);
        RectTransform lineRect = dialogueLineText.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.offsetMin = new Vector2(24f, 66f);
        lineRect.offsetMax = new Vector2(-24f, -48f);

        dialogueSkipButton = FindOrCreateDialogueSkipButton(dialogueRect);
        RectTransform skipRect = dialogueSkipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-18f, 18f);
        skipRect.sizeDelta = new Vector2(78f, 30f);
        dialogueSkipButton.gameObject.SetActive(dialogueSkipCallback != null);

        for (int i = 0; i < DialogueChoiceCount; i++)
        {
            dialogueChoiceButtons[i] = FindOrCreateDialogueButton(dialogueRect, i);
            RectTransform choiceRect = dialogueChoiceButtons[i].GetComponent<RectTransform>();
            choiceRect.anchorMin = new Vector2(0f, 0f);
            choiceRect.anchorMax = new Vector2(0f, 0f);
            choiceRect.pivot = new Vector2(0f, 0f);
            choiceRect.anchoredPosition = new Vector2(24f + (i * 256f), 16f);
            choiceRect.sizeDelta = new Vector2(236f, 42f);
            dialogueChoiceButtons[i].gameObject.SetActive(false);
        }

        clockStrikePanel = FindOrCreatePanel(root, ClockStrikePanelName, new Color(0f, 0f, 0f, 0.78f));
        if (clockStrikePanel.GetComponent<RectMask2D>() == null)
        {
            clockStrikePanel.AddComponent<RectMask2D>();
        }

        RectTransform clockStrikeRect = clockStrikePanel.GetComponent<RectTransform>();
        clockStrikeRect.anchorMin = new Vector2(0.5f, 0.5f);
        clockStrikeRect.anchorMax = new Vector2(0.5f, 0.5f);
        clockStrikeRect.pivot = new Vector2(0.5f, 0.5f);
        clockStrikeRect.anchoredPosition = Vector2.zero;
        clockStrikeRect.sizeDelta = new Vector2(880f, 900f);

        clockStrikeImage = FindOrCreateImage(clockStrikeRect, ClockStrikeImageName);
        RectTransform clockStrikeImageRect = clockStrikeImage.rectTransform;
        clockStrikeImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        clockStrikeImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        clockStrikeImageRect.pivot = new Vector2(0.5f, 0.5f);
        clockStrikeImageRect.anchoredPosition = new Vector2(0f, -125f);
        clockStrikeImageRect.sizeDelta = new Vector2(760f, 1350f);
        clockStrikeImage.preserveAspect = true;
        clockStrikeImage.color = Color.white;
        clockStrikeImage.gameObject.SetActive(false);

        clockStrikeHandsRoot = FindOrCreateClockStrikeHandsRoot(clockStrikeRect);
        clockStrikeHourHighlight = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeHourHighlightName, new Color(0.95f, 0.58f, 0.14f, 0.42f), 10f, false);
        clockStrikeHourHand = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeHourHandName, new Color(0.02f, 0.012f, 0.004f, 0.92f), 7f, false);
        clockStrikeMinuteHighlight = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeMinuteHighlightName, new Color(0.95f, 0.58f, 0.14f, 0.38f), 8f, false);
        clockStrikeMinuteHand = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeMinuteHandName, new Color(0.02f, 0.012f, 0.004f, 0.92f), 5f, false);
        clockStrikeSecondHand = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeSecondHandName, new Color(0.52f, 0.03f, 0.02f, 0.96f), 3f, false);
        clockStrikeSecondTail = FindOrCreateClockStrikeHand(clockStrikeHandsRoot, ClockStrikeSecondTailName, new Color(0.9f, 0.58f, 0.12f, 0.88f), 2f, true);
        clockStrikeCenterPin = FindOrCreateClockStrikeCenterPin(clockStrikeHandsRoot);
        LayoutClockStrikeHands();

        clockStrikeText = FindOrCreateText(clockStrikeRect, ClockStrikeTextName, 64f, TextAlignmentOptions.Center);
        ApplyClockStrikeTextLayout(clockStrikeImage.sprite != null);
        clockStrikeText.textWrappingMode = TextWrappingModes.NoWrap;
        clockStrikePanel.SetActive(false);

        primaryButton = FindOrCreatePrimaryButton(root);
        RectTransform buttonRect = primaryButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 40f);
        buttonRect.sizeDelta = new Vector2(240f, 48f);

        if (primaryActionCallback == null)
        {
            primaryButton.gameObject.SetActive(false);
        }

        if (dialogueChoiceCallbacks[0] == null && dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(statusOverrideText))
        {
            statusText.text = statusOverrideText;
            statusText.gameObject.SetActive(true);
            return;
        }

        string phaseLabel = controller != null ? controller.CurrentPhase.ToString() : string.Empty;
        statusText.text = phaseLabel;
        statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(phaseLabel));
    }

    private void ApplyClockStrikeTextLayout(bool hasClockFaceSprite)
    {
        if (clockStrikeText == null)
        {
            return;
        }

        RectTransform textRect = clockStrikeText.GetComponent<RectTransform>();

        if (textRect == null)
        {
            return;
        }

        if (hasClockFaceSprite)
        {
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 24f);
            textRect.sizeDelta = new Vector2(-48f, 58f);
            clockStrikeText.fontSize = 34f;
            clockStrikeText.alignment = TextAlignmentOptions.Center;
            return;
        }

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(24f, 24f);
        textRect.offsetMax = new Vector2(-24f, -24f);
        clockStrikeText.fontSize = 64f;
        clockStrikeText.alignment = TextAlignmentOptions.Center;
    }

    private void LayoutClockStrikeHands()
    {
        if (clockStrikeHandsRoot == null || clockStrikeImage == null)
        {
            return;
        }

        RectTransform imageRect = clockStrikeImage.rectTransform;
        clockStrikeHandsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        clockStrikeHandsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        clockStrikeHandsRoot.pivot = new Vector2(0.5f, 0.5f);
        clockStrikeHandsRoot.anchoredPosition = imageRect.anchoredPosition +
            new Vector2(0f, imageRect.sizeDelta.y * ClockStrikeFaceCenterYOffset);

        float diameter = Mathf.Max(160f, imageRect.sizeDelta.y * ClockStrikeHandsDiameterScale);
        clockStrikeHandsRoot.sizeDelta = new Vector2(diameter, diameter);
    }

    private void RefreshClockStrikeHands(bool force = false)
    {
        if (clockStrikeHandsRoot == null)
        {
            return;
        }

        bool visible = clockStrikePanel != null && clockStrikePanel.activeSelf;

        if (!visible)
        {
            if (clockStrikeHandsRoot.gameObject.activeSelf)
            {
                clockStrikeHandsRoot.gameObject.SetActive(false);
            }

            return;
        }

        if (force || !clockStrikeHandsRoot.gameObject.activeSelf)
        {
            clockStrikeHandsRoot.gameObject.SetActive(true);
        }

        LayoutClockStrikeHands();

        float radius = Mathf.Min(clockStrikeHandsRoot.rect.width, clockStrikeHandsRoot.rect.height) * 0.5f;
        SetClockStrikeHand(clockStrikeHourHand, clockStrikeHourHighlight, 7f / 12f, radius * 0.31f);
        SetClockStrikeHand(clockStrikeMinuteHand, clockStrikeMinuteHighlight, 0f, radius * 0.45f);

        int secondTick = Mathf.FloorToInt(Mathf.Max(0f, Time.unscaledTime - clockStrikeStartedAt) * ClockStrikeSecondTicksPerSecond);
        float secondProgress = Mathf.Repeat(secondTick / 60f, 1f);
        SetClockStrikeHand(clockStrikeSecondHand, null, secondProgress, radius * 0.52f);
        SetClockStrikeHand(clockStrikeSecondTail, null, secondProgress, radius * 0.08f);

        if (clockStrikeCenterPin != null)
        {
            float pinSize = Mathf.Max(12f, radius * 0.055f);
            clockStrikeCenterPin.sizeDelta = new Vector2(pinSize, pinSize);
        }
    }

    private static void SetClockStrikeHand(RectTransform hand, RectTransform highlight, float progress, float length)
    {
        if (hand == null)
        {
            return;
        }

        float angle = -Mathf.Repeat(progress, 1f) * 360f;
        Vector2 size = hand.sizeDelta;
        size.y = Mathf.Max(1f, length);
        hand.sizeDelta = size;
        hand.localEulerAngles = new Vector3(0f, 0f, angle);

        if (highlight == null)
        {
            return;
        }

        Vector2 highlightSize = highlight.sizeDelta;
        highlightSize.y = size.y;
        highlight.sizeDelta = highlightSize;
        highlight.localEulerAngles = hand.localEulerAngles;
    }

    private static RectTransform FindOrCreateClockStrikeHandsRoot(Transform root)
    {
        Transform existing = root.Find(ClockStrikeHandsRootName);
        RectTransform rect = existing != null
            ? existing.GetComponent<RectTransform>()
            : null;

        if (rect != null)
        {
            return rect;
        }

        GameObject rootObject = new GameObject(ClockStrikeHandsRootName, typeof(RectTransform));
        rect = rootObject.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.gameObject.SetActive(false);
        return rect;
    }

    private static RectTransform FindOrCreateClockStrikeHand(
        Transform root,
        string objectName,
        Color color,
        float width,
        bool counterweight)
    {
        Image image = FindOrCreateImage(root, objectName);
        image.sprite = clockStrikeHandSprite ??= CreateClockStrikeSolidSprite();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = counterweight
            ? new Vector2(0.5f, 1f)
            : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, width), 80f);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform FindOrCreateClockStrikeCenterPin(Transform root)
    {
        Image image = FindOrCreateImage(root, ClockStrikeCenterPinName);
        image.sprite = clockStrikePinSprite ??= CreateClockStrikeCircleSprite();
        image.color = new Color(1f, 0.68f, 0.16f, 1f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(28f, 28f);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Sprite CreateClockStrikeSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.name = "RuntimeChapter2ClockStrikeHandSolid";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Sprite CreateClockStrikeCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                texture.SetPixel(x, y, distance <= 1f ? Color.white : clear);
            }
        }

        texture.Apply();
        texture.name = "RuntimeChapter2ClockStrikeCenterPin";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite ResolveClockStrikeClockFaceSprite()
    {
        if (clockStrikeClockFaceSprite != null)
        {
            return clockStrikeClockFaceSprite;
        }

        clockStrikeClockFaceSprite = FindClockStrikeSpriteInResources();

        if (clockStrikeClockFaceSprite != null)
        {
            return clockStrikeClockFaceSprite;
        }

#if UNITY_EDITOR
        clockStrikeClockFaceSprite = FindClockStrikeSpriteInEditorAsset();

        if (clockStrikeClockFaceSprite != null)
        {
            return clockStrikeClockFaceSprite;
        }
#endif

        clockStrikeClockFaceSprite = FindLoadedClockStrikeSprite();
        return clockStrikeClockFaceSprite;
    }

    private static Sprite FindClockStrikeSpriteInResources()
    {
        Sprite directSprite = Resources.Load<Sprite>(SevenOClockClockFaceResourceName);

        if (directSprite != null)
        {
            return directSprite;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(SevenOClockClockFaceResourceName);
        return FindMatchingClockStrikeSprite(sprites);
    }

#if UNITY_EDITOR
    private static Sprite FindClockStrikeSpriteInEditorAsset()
    {
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(SevenOClockClockFaceAssetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && IsSevenOClockClockFaceSprite(sprite))
            {
                return sprite;
            }
        }

        return null;
    }
#endif

    private static Sprite FindLoadedClockStrikeSprite()
    {
        Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        return FindMatchingClockStrikeSprite(loadedSprites);
    }

    private static Sprite FindMatchingClockStrikeSprite(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return null;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (IsSevenOClockClockFaceSprite(sprites[i]))
            {
                return sprites[i];
            }
        }

        return null;
    }

    private static bool IsSevenOClockClockFaceSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return false;
        }

        string spriteName = sprite.name ?? string.Empty;
        string textureName = sprite.texture != null ? sprite.texture.name : string.Empty;
        return ContainsIgnoreCase(spriteName, SevenOClockClockFaceResourceName) ||
            ContainsIgnoreCase(textureName, SevenOClockClockFaceResourceName);
    }

    private static bool ContainsIgnoreCase(string value, string expected)
    {
        return !string.IsNullOrEmpty(value) &&
            !string.IsNullOrEmpty(expected) &&
            value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static TMP_Text FindOrCreateText(Transform root, string objectName, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = root.Find(objectName);
        TMP_Text text = existing != null
            ? existing.GetComponent<TMP_Text>()
            : null;

        if (text != null)
        {
            return text;
        }

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(root, false);

        text = textObject.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Image FindOrCreateImage(Transform root, string objectName)
    {
        Transform existing = root.Find(objectName);
        Image image = existing != null
            ? existing.GetComponent<Image>()
            : null;

        if (image != null)
        {
            return image;
        }

        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(root, false);

        image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Button FindOrCreatePrimaryButton(Transform root)
    {
        Transform existing = root.Find(PrimaryButtonName);
        Button button = existing != null
            ? existing.GetComponent<Button>()
            : null;

        if (button == null)
        {
            GameObject buttonObject = new GameObject(PrimaryButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(root, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.07f, 0.07f, 0.08f, 0.88f);

            button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.07f, 0.07f, 0.08f, 0.88f);
            colors.highlightedColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            colors.pressedColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        primaryButtonLabel = FindOrCreateButtonLabel(button.transform);
        return button;
    }

    private Button FindOrCreateDialogueButton(Transform root, int index)
    {
        string objectName = DialogueChoiceButtonNamePrefix + (index + 1);
        Transform existing = root.Find(objectName);
        Button button = existing != null
            ? existing.GetComponent<Button>()
            : null;

        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(root, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);

            button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.1f, 0.08f, 0.95f);
            colors.highlightedColor = new Color(0.2f, 0.16f, 0.12f, 1f);
            colors.pressedColor = new Color(0.05f, 0.04f, 0.03f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        dialogueChoiceLabels[index] = FindOrCreateDialogueButtonLabel(button.transform);
        return button;
    }

    private Button FindOrCreateDialogueSkipButton(Transform root)
    {
        Transform existing = root.Find(DialogueSkipButtonName);
        Button button = existing != null
            ? existing.GetComponent<Button>()
            : null;

        if (button == null)
        {
            GameObject buttonObject = new GameObject(DialogueSkipButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(root, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);

            button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.08f, 0.07f, 0.06f, 0.94f);
            colors.highlightedColor = new Color(0.18f, 0.14f, 0.1f, 1f);
            colors.pressedColor = new Color(0.03f, 0.025f, 0.02f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        FindOrCreateSkipButtonLabel(button.transform);
        return button;
    }

    private TMP_Text FindOrCreateDialogueButtonLabel(Transform buttonRoot)
    {
        Transform existing = buttonRoot.Find(DialogueChoiceLabelName);
        TMP_Text label = existing != null
            ? existing.GetComponent<TMP_Text>()
            : null;

        if (label != null)
        {
            return label;
        }

        GameObject labelObject = new GameObject(DialogueChoiceLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRoot, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        label = labelObject.GetComponent<TMP_Text>();
        label.fontSize = 16f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_Text FindOrCreateSkipButtonLabel(Transform buttonRoot)
    {
        Transform existing = buttonRoot.Find(DialogueSkipLabelName);
        TMP_Text label = existing != null
            ? existing.GetComponent<TMP_Text>()
            : null;

        if (label != null)
        {
            return label;
        }

        GameObject labelObject = new GameObject(DialogueSkipLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRoot, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        label = labelObject.GetComponent<TMP_Text>();
        label.text = "Skip";
        label.fontSize = 14f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject FindOrCreatePanel(Transform root, string objectName, Color color)
    {
        Transform existing = root.Find(objectName);

        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(root, false);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panelObject;
    }

    private void SetDialogueChoice(int index, string label, Action callback)
    {
        if (index < 0 || index >= dialogueChoiceButtons.Length)
        {
            return;
        }

        dialogueChoiceCallbacks[index] = callback;
        Button button = dialogueChoiceButtons[index];

        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();

        if (callback == null || string.IsNullOrWhiteSpace(label))
        {
            button.gameObject.SetActive(false);
            return;
        }

        int callbackIndex = index;
        button.onClick.AddListener(() => dialogueChoiceCallbacks[callbackIndex]?.Invoke());
        button.interactable = dialogueChoicesInteractable;
        button.gameObject.SetActive(true);

        if (dialogueChoiceLabels[index] != null)
        {
            dialogueChoiceLabels[index].text = label.Trim();
        }
    }

    private static TMP_Text FindOrCreateButtonLabel(Transform buttonRoot)
    {
        Transform existing = buttonRoot.Find(PrimaryButtonLabelName);
        TMP_Text label = existing != null
            ? existing.GetComponent<TMP_Text>()
            : null;

        if (label != null)
        {
            return label;
        }

        GameObject labelObject = new GameObject(PrimaryButtonLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRoot, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);

        label = labelObject.GetComponent<TMP_Text>();
        label.fontSize = 20f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
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
