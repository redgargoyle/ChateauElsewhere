using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RuntimeSettingsMenu : MonoBehaviour
{
    private const string MenuObjectName = "RuntimeSettingsMenu";
    private const string MenuCanvasName = "Canvas_RuntimeSettingsMenu";
    private const string SettingsButtonName = "Button_Settings";
    private const string SettingsListName = "List_Settings";
    private const string DebugListName = "List_Debug";
    private const string RoomListName = "List_TeleportRooms";
    private const float ButtonWidth = 150f;
    private const float ButtonHeight = 34f;
    private const int MenuCanvasSortingOrder = 10050;

    private static readonly Color ButtonNormalColor = new Color(0.05f, 0.05f, 0.055f, 0.88f);
    private static readonly Color ButtonHighlightColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    private static readonly Color ButtonPressedColor = new Color(0.02f, 0.02f, 0.025f, 1f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.18f);

    private RoomNavigationManager navigationManager;
    private RectTransform rootRect;
    private Button settingsButton;
    private RectTransform settingsList;
    private RectTransform debugList;
    private RectTransform roomList;
    private ChapterManager chapterManager;
    private bool settingsOpen;
    private bool debugOpen;
    private bool roomListOpen;

    public static RuntimeSettingsMenu FindOrCreate(RoomNavigationManager navigationManager)
    {
        RuntimeSettingsMenu existing = FindAnyObjectByType<RuntimeSettingsMenu>(FindObjectsInactive.Include);

        if (existing != null)
        {
            return existing;
        }

        Canvas canvas = GetOrCreateMenuCanvas();

        if (canvas == null)
        {
            return null;
        }

        GameObject menuObject = new GameObject(MenuObjectName, typeof(RectTransform), typeof(RuntimeSettingsMenu));
        menuObject.transform.SetParent(canvas.transform, false);
        RuntimeSettingsMenu menu = menuObject.GetComponent<RuntimeSettingsMenu>();
        menu.Initialize(navigationManager);
        return menu;
    }

    public void Initialize(RoomNavigationManager navigationManager)
    {
        this.navigationManager = navigationManager;
        EnsureEventSystem();
        EnsureUI();
        RefreshOpenState();
    }

    private static Canvas GetOrCreateMenuCanvas()
    {
        GameObject canvasObject = GameObject.Find(MenuCanvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                MenuCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        int uiLayer = LayerMask.NameToLayer("UI");

        if (uiLayer >= 0)
        {
            canvasObject.layer = uiLayer;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = MenuCanvasSortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        }

        ApplyCanvasScalerDefaults(canvasScaler);

        Canvas sourceCanvas = PostProcessSafeCanvasUtility.GetOrCreateCanvas();
        CanvasScaler sourceScaler = sourceCanvas != null ? sourceCanvas.GetComponent<CanvasScaler>() : null;

        if (sourceScaler != null && sourceScaler != canvasScaler)
        {
            CopyCanvasScalerSettings(canvasScaler, sourceScaler);
        }

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform rectTransform = canvasObject.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
        }

        return canvas;
    }

    private void EnsureUI()
    {
        rootRect = transform as RectTransform;

        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = MenuCanvasSortingOrder;
        }

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(12f, -12f);
        rootRect.sizeDelta = new Vector2(760f, 520f);
        rootRect.localScale = Vector3.one;
        transform.SetAsLastSibling();

        settingsButton = FindOrCreateButton(rootRect, SettingsButtonName, "Settings", ToggleSettings);
        RectTransform settingsButtonRect = settingsButton.GetComponent<RectTransform>();
        settingsButtonRect.anchorMin = new Vector2(0f, 1f);
        settingsButtonRect.anchorMax = new Vector2(0f, 1f);
        settingsButtonRect.pivot = new Vector2(0f, 1f);
        settingsButtonRect.anchoredPosition = Vector2.zero;
        settingsButtonRect.sizeDelta = new Vector2(112f, ButtonHeight);

        settingsList = FindOrCreateList(rootRect, SettingsListName, true);
        settingsList.anchorMin = new Vector2(0f, 1f);
        settingsList.anchorMax = new Vector2(0f, 1f);
        settingsList.pivot = new Vector2(0f, 1f);
        settingsList.anchoredPosition = new Vector2(0f, -ButtonHeight - 6f);

        FindOrCreateButton(settingsList, "Button_Debug", "Debug", ToggleDebug);

        debugList = FindOrCreateList(rootRect, DebugListName, false);
        debugList.anchorMin = new Vector2(0f, 1f);
        debugList.anchorMax = new Vector2(0f, 1f);
        debugList.pivot = new Vector2(0f, 1f);
        debugList.anchoredPosition = new Vector2(ButtonWidth + 10f, -ButtonHeight - 6f);

        FindOrCreateButton(debugList, "Button_SkipToChapter2", "Skip to Chapter 2", SkipToChapter2);
        FindOrCreateButton(debugList, "Button_SkipToChapter3", "Skip to Chapter 3", SkipToChapter3);
        FindOrCreateButton(debugList, "Button_TeleportToRoom", "Teleport to Room", ToggleRoomList);

        roomList = FindOrCreateList(rootRect, RoomListName, true);
        roomList.anchorMin = new Vector2(0f, 1f);
        roomList.anchorMax = new Vector2(0f, 1f);
        roomList.pivot = new Vector2(0f, 1f);
        roomList.anchoredPosition = new Vector2(ButtonWidth + 10f + ((ButtonWidth + 6f) * 2f), -ButtonHeight * 2f - 14f);
    }

    private void ToggleSettings()
    {
        settingsOpen = !settingsOpen;

        if (!settingsOpen)
        {
            debugOpen = false;
            roomListOpen = false;
        }

        RefreshOpenState();
    }

    private void ToggleDebug()
    {
        debugOpen = !debugOpen;

        if (!debugOpen)
        {
            roomListOpen = false;
        }

        RefreshOpenState();
    }

    private void ToggleRoomList()
    {
        roomListOpen = !roomListOpen;
        RefreshOpenState();
    }

    private void SkipToChapter2()
    {
        ChapterManager manager = ResolveChapterManager();

        if (manager == null)
        {
            return;
        }

        roomListOpen = false;
        manager.SkipToChapter2ForTesting();
        RefreshOpenState();
    }

    private void SkipToChapter3()
    {
        ChapterManager manager = ResolveChapterManager();

        if (manager == null)
        {
            return;
        }

        roomListOpen = false;
        manager.SkipToChapter3ForTesting();
        RefreshOpenState();
    }

    private void RefreshOpenState()
    {
        if (settingsList != null)
        {
            settingsList.gameObject.SetActive(settingsOpen);
        }

        if (debugList != null)
        {
            debugList.gameObject.SetActive(settingsOpen && debugOpen);
        }

        if (roomList != null)
        {
            bool showRoomList = settingsOpen && debugOpen && roomListOpen;
            roomList.gameObject.SetActive(showRoomList);

            if (showRoomList)
            {
                RebuildRoomButtons();
            }
        }
    }

    private void RebuildRoomButtons()
    {
        if (roomList == null)
        {
            return;
        }

        List<string> roomNames = navigationManager != null
            ? navigationManager.GetKnownRoomNames()
            : new List<string>();

        for (int i = 0; i < roomList.childCount; i++)
        {
            roomList.GetChild(i).gameObject.SetActive(false);
        }

        for (int i = 0; i < roomNames.Count; i++)
        {
            string roomName = roomNames[i];

            if (string.IsNullOrWhiteSpace(roomName))
            {
                continue;
            }

            string capturedRoomName = roomName;
            FindOrCreateButton(
                roomList,
                "Button_Room_" + SanitizeObjectName(roomName),
                roomName,
                () => TeleportToRoom(capturedRoomName)).gameObject.SetActive(true);
        }
    }

    private void TeleportToRoom(string roomName)
    {
        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (navigationManager == null)
        {
            return;
        }

        navigationManager.DebugTeleportToRoom(roomName);
        roomListOpen = false;
        RefreshOpenState();
    }

    private ChapterManager ResolveChapterManager()
    {
        if (chapterManager == null)
        {
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        }

        return chapterManager;
    }

    private static RectTransform FindOrCreateList(Transform parent, string objectName, bool vertical)
    {
        Transform existing = parent.Find(objectName);
        RectTransform rect = existing != null ? existing as RectTransform : null;

        if (rect == null)
        {
            GameObject listObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = listObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            image.color = PanelColor;
            image.raycastTarget = false;
        }

        HorizontalOrVerticalLayoutGroup layout;

        if (vertical)
        {
            layout = rect.GetComponent<VerticalLayoutGroup>();
        }
        else
        {
            layout = rect.GetComponent<HorizontalLayoutGroup>();
        }

        if (layout == null)
        {
            if (vertical)
            {
                layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            else
            {
                layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            }
        }

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    private static Button FindOrCreateButton(Transform parent, string objectName, string label, Action onClick)
    {
        Transform existing = parent.Find(objectName);
        Button button = existing != null ? existing.GetComponent<Button>() : null;

        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonNormalColor;

            button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormalColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = ButtonHighlightColor;
            button.colors = colors;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        TMP_Text text = FindOrCreateButtonLabel(button.transform);
        text.text = label ?? string.Empty;

        button.onClick.RemoveAllListeners();

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        button.interactable = onClick != null;
        return button;
    }

    private static TMP_Text FindOrCreateButtonLabel(Transform buttonRoot)
    {
        const string labelName = "Text_Label";
        Transform existing = buttonRoot.Find(labelName);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label != null)
        {
            return label;
        }

        GameObject labelObject = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRoot, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        label = labelObject.GetComponent<TMP_Text>();
        label.fontSize = 15f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Room";
        }

        char[] result = new char[value.Length];

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            result[i] = char.IsLetterOrDigit(c) ? c : '_';
        }

        return new string(result).Trim('_');
    }

    private static void ApplyCanvasScalerDefaults(CanvasScaler target)
    {
        if (target == null)
        {
            return;
        }

        target.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        target.referenceResolution = new Vector2(1366f, 768f);
        target.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        target.matchWidthOrHeight = 0.5f;
    }

    private static void CopyCanvasScalerSettings(CanvasScaler target, CanvasScaler source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.uiScaleMode = source.uiScaleMode;
        target.referenceResolution = source.referenceResolution;
        target.screenMatchMode = source.screenMatchMode;
        target.matchWidthOrHeight = source.matchWidthOrHeight;
        target.physicalUnit = source.physicalUnit;
        target.fallbackScreenDPI = source.fallbackScreenDPI;
        target.defaultSpriteDPI = source.defaultSpriteDPI;
        target.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        target.referencePixelsPerUnit = source.referencePixelsPerUnit;
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
