using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Chapter1InteractionHUD : MonoBehaviour
{
    [SerializeField] private bool showButtonPrompts = true;

    private Chapter1ArrivalController arrivalController;
    private GrandfatherClockInteraction clockInteraction;
    private ChapterClock chapterClock;
    private Canvas canvas;
    private Button hangCoatButton;
    private TMP_Text statusText;

    private void Update()
    {
        if (chapterClock != null && statusText != null)
        {
            statusText.text = arrivalController != null
                ? arrivalController.BuildShortHudState(chapterClock.CurrentTimeLabel)
                : chapterClock.CurrentTimeLabel;
        }
    }

    public void Initialize(
        Chapter1ArrivalController controller,
        ChapterClock clock,
        GrandfatherClockInteraction clockView)
    {
        arrivalController = controller;
        chapterClock = clock;
        clockInteraction = clockView;
        EnsureUI();
        SetHangCoatAvailable(false);
    }

    public void SetHangCoatAvailable(bool value)
    {
        EnsureUI();

        if (hangCoatButton != null)
        {
            hangCoatButton.gameObject.SetActive(showButtonPrompts && value);
        }
    }

    private void EnsureUI()
    {
        GameObject canvasObject = GameObject.Find("Canvas_Chapter1HUD");

        if (canvasObject == null)
        {
            canvasObject = new GameObject("Canvas_Chapter1HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9100;
        EnsureEventSystem();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;

        SyncDebugActionButtons(root);
        RemoveDebugActionButton(root, "Button_AnswerFrontDoor");

        if (showButtonPrompts)
        {
            hangCoatButton = FindOrCreateButton("Button_HangCoat", root, "Hang Coat", new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(170f, 42f));
            hangCoatButton.onClick.AddListener(() =>
            {
                if (arrivalController != null)
                {
                    arrivalController.HandleClosetClicked();
                }
            });
        }

        Transform existingStatus = root.Find("Text_Chapter1Status");
        statusText = existingStatus != null
            ? existingStatus.GetComponent<TMP_Text>()
            : CreateText("Text_Chapter1Status", root, 18f, TextAlignmentOptions.Left);

        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(18f, -18f);
        statusRect.sizeDelta = new Vector2(430f, 80f);
    }

    private Button FindOrCreateButton(string objectName, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Transform existingButton = parent.Find(objectName);

        if (existingButton != null && existingButton.TryGetComponent(out Button button))
        {
            button.onClick.RemoveAllListeners();
            return button;
        }

        return CreateButton(objectName, parent, label, anchor, position, size);
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.075f, 0.065f, 0.92f);

        TMP_Text text = CreateText("Text", rect, 20f, TextAlignmentOptions.Center);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        text.text = label;

        return buttonObject.GetComponent<Button>();
    }

    private static void RemoveDebugActionButton(Transform root, string objectName)
    {
        if (root != null)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = children.Length - 1; i >= 0; i--)
            {
                Transform child = children[i];

                if (child != null && child.name == objectName)
                {
                    DestroyRuntimeOrEditor(child.gameObject);
                }
            }
        }

        GameObject globalObject = GameObject.Find(objectName);

        if (globalObject != null)
        {
            DestroyRuntimeOrEditor(globalObject);
        }
    }

    private static void DestroyRuntimeOrEditor(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void SyncDebugActionButtons(Transform root)
    {
        if (root == null || showButtonPrompts)
        {
            return;
        }

        RemoveDebugActionButton(root, "Button_AnswerFrontDoor");
        RemoveDebugActionButton(root, "Button_HangCoat");
        RemoveDebugActionButton(root, "Button_InspectClock");
        hangCoatButton = null;
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

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
