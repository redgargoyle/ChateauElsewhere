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
    private const string PrimaryButtonName = "Button_Chapter2PrimaryAction";
    private const string PrimaryButtonLabelName = "Text_Chapter2PrimaryAction";

    private Chapter2Controller controller;
    private ChapterClock chapterClock;
    private Canvas canvas;
    private TMP_Text objectiveText;
    private TMP_Text statusText;
    private TMP_Text foundListText;
    private Button primaryButton;
    private TMP_Text primaryButtonLabel;
    private Action primaryActionCallback;
    private string statusOverrideText;

    private void Update()
    {
        UpdateStatusText();
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
