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
    private const string SettingsOverlayName = "Panel_SettingsOverlay";
    private const string SettingsPanelName = "Panel_SettingsModal";
    private const string SettingsTitleName = "Text_SettingsTitle";
    private const string SettingsButtonName = "Button_Settings";
    private const string SettingsListName = "List_Settings";
    private const string SettingsAudioListName = "List_AudioControls";
    private const string DebugListName = "List_Debug";
    private const string RoomListName = "List_TeleportRooms";
    private const string DebugTimeControlName = "Control_DebugGameTimeSpeed";
    private const string DebugButtonRowName = "List_DebugButtons";
    private const string DebugControlRowName = "List_DebugControls";
    private const string DialogueAudioControlName = "Control_AudioDialogue";
    private const string GameSoundsAudioControlName = "Control_AudioGameSounds";
    private const string AtmosphereAudioControlName = "Control_AudioAtmosphere";
    private const string MusicAudioControlName = "Control_AudioMusic";
    private const string ExplorationMusicObjectName = "Audio_ExplorationMusic";
    private const string ExplorationMusicClipName = "unity_dreadforge_soundscape";
    private const float ButtonWidth = 150f;
    private const float ButtonHeight = 34f;
    private const float SettingsPanelWidth = 640f;
    private const float SettingsPanelHeight = 560f;
    private const float DebugControlWidth = 360f;
    private const float DebugControlLabelWidth = 116f;
    private const float DebugControlInputWidth = 54f;
    private const float DebugControlSliderLeft = 132f;
    private const float MinSecondsPerGameMinute = 1f;
    private const float MaxSecondsPerGameMinute = 120f;
    private const int MenuCanvasSortingOrder = 10050;

    private static readonly Color ButtonNormalColor = new Color(0.05f, 0.05f, 0.055f, 0.88f);
    private static readonly Color ButtonHighlightColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    private static readonly Color ButtonPressedColor = new Color(0.02f, 0.02f, 0.025f, 1f);
    private static readonly Color DebugTimeControlColor = new Color(0.08f, 0.08f, 0.085f, 1f);
    private static readonly Color DebugTimeInputColor = new Color(0.02f, 0.02f, 0.025f, 1f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.18f);

    private enum DebugSliderKind
    {
        Time,
        Dialogue,
        GameSounds,
        Atmosphere,
        Music
    }

    private RoomNavigationManager navigationManager;
    private RectTransform rootRect;
    private RectTransform settingsOverlay;
    private RectTransform settingsPanel;
    private TMP_Text settingsTitle;
    private Button settingsButton;
    private RectTransform settingsList;
    private RectTransform settingsAudioList;
    private RectTransform debugList;
    private RectTransform debugButtonRow;
    private RectTransform debugControlRow;
    private RectTransform roomList;
    private RectTransform debugTimeControl;
    private RectTransform debugTimeSlider;
    private RectTransform debugTimeSliderFill;
    private RectTransform debugTimeSliderHandle;
    private TMP_InputField debugTimeInput;
    private TMP_Text debugTimeLabel;
    private RectTransform dialogueAudioControl;
    private RectTransform dialogueAudioSliderFill;
    private RectTransform dialogueAudioSliderHandle;
    private TMP_InputField dialogueAudioInput;
    private TMP_Text dialogueAudioLabel;
    private RectTransform gameSoundsAudioControl;
    private RectTransform gameSoundsAudioSliderFill;
    private RectTransform gameSoundsAudioSliderHandle;
    private TMP_InputField gameSoundsAudioInput;
    private TMP_Text gameSoundsAudioLabel;
    private RectTransform atmosphereAudioControl;
    private RectTransform atmosphereAudioSliderFill;
    private RectTransform atmosphereAudioSliderHandle;
    private TMP_InputField atmosphereAudioInput;
    private TMP_Text atmosphereAudioLabel;
    private RectTransform musicAudioControl;
    private RectTransform musicAudioSliderFill;
    private RectTransform musicAudioSliderHandle;
    private TMP_InputField musicAudioInput;
    private TMP_Text musicAudioLabel;
    private ChapterManager chapterManager;
    private ChapterClock chapterClock;
    private AudioSource explorationMusicSource;
    private float explorationMusicBaseVolume = -1f;
    private float timeScaleBeforeSettings = 1f;
    private bool settingsOpen;
    private bool debugOpen;
    private bool roomListOpen;
    private bool isUpdatingDebugTimeControl;
    private bool isUpdatingAudioControl;
    private bool timeScalePausedForSettings;

    public static bool BlocksGameInput { get; private set; }

    public static RuntimeSettingsMenu FindOrCreate(RoomNavigationManager navigationManager)
    {
        RuntimeSettingsMenu existing = FindAnyObjectByType<RuntimeSettingsMenu>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.Initialize(navigationManager);
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

    private void OnDisable()
    {
        ClearModalGameState();
    }

    private void OnDestroy()
    {
        ClearModalGameState();
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

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;
        transform.SetAsLastSibling();

        settingsOverlay = FindOrCreateSettingsOverlay(rootRect);
        settingsPanel = FindOrCreateSettingsPanel(rootRect);
        settingsTitle = FindOrCreateSettingsTitle(settingsPanel);

        settingsButton = FindOrCreateButton(rootRect, SettingsButtonName, "Settings", ToggleSettings);
        UpdateSettingsButtonLayout();

        settingsList = FindOrCreateList(settingsPanel, SettingsListName, true);
        settingsList.anchorMin = new Vector2(0f, 1f);
        settingsList.anchorMax = new Vector2(0f, 1f);
        settingsList.pivot = new Vector2(0f, 1f);
        settingsList.anchoredPosition = new Vector2(28f, -80f);

        FindOrCreateButton(settingsList, "Button_Debug", "Debug", ToggleDebug);
        settingsAudioList = FindOrCreateList(settingsList, SettingsAudioListName, true);
        dialogueAudioControl = FindOrCreateAudioVolumeControl(settingsAudioList, DialogueAudioControlName, GameAudioChannel.Dialogue, DebugSliderKind.Dialogue);
        gameSoundsAudioControl = FindOrCreateAudioVolumeControl(settingsAudioList, GameSoundsAudioControlName, GameAudioChannel.GameSounds, DebugSliderKind.GameSounds);
        atmosphereAudioControl = FindOrCreateAudioVolumeControl(settingsAudioList, AtmosphereAudioControlName, GameAudioChannel.Atmosphere, DebugSliderKind.Atmosphere);
        musicAudioControl = FindOrCreateAudioVolumeControl(settingsAudioList, MusicAudioControlName, GameAudioChannel.Music, DebugSliderKind.Music);

        debugList = FindOrCreateList(settingsPanel, DebugListName, true);
        debugList.anchorMin = new Vector2(0f, 1f);
        debugList.anchorMax = new Vector2(0f, 1f);
        debugList.pivot = new Vector2(0f, 1f);
        debugList.anchoredPosition = new Vector2(28f, -390f);

        debugButtonRow = FindOrCreateList(debugList, DebugButtonRowName, false);
        FindOrCreateButton(debugButtonRow, "Button_SkipToChapter2", "Skip to Chapter 2", SkipToChapter2);
        FindOrCreateButton(debugButtonRow, "Button_SkipToChapter3", "Skip to Chapter 3", SkipToChapter3);
        FindOrCreateButton(debugButtonRow, "Button_TeleportToRoom", "Teleport to Room", ToggleRoomList);

        debugControlRow = FindOrCreateList(debugList, DebugControlRowName, false);
        debugTimeControl = FindOrCreateDebugTimeControl(debugControlRow);
        DisableLegacyDebugChildren();

        roomList = FindOrCreateList(settingsPanel, RoomListName, true);
        roomList.anchorMin = new Vector2(0f, 1f);
        roomList.anchorMax = new Vector2(0f, 1f);
        roomList.pivot = new Vector2(0f, 1f);
        roomList.anchoredPosition = new Vector2(SettingsPanelWidth - ButtonWidth - 28f, -80f);

        RefreshAudioControls();
        EnsureRuntimeAudioBindings();
    }

    private void DisableLegacyDebugChildren()
    {
        if (debugList == null)
        {
            return;
        }

        for (int i = 0; i < debugList.childCount; i++)
        {
            Transform child = debugList.GetChild(i);

            if (child != debugButtonRow && child != debugControlRow)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        EnsureRuntimeAudioBindings();

        if (settingsOpen)
        {
            RefreshAudioControls();
        }

        if (settingsOpen && debugOpen)
        {
            RefreshDebugTimeControl();
        }
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
        CloseSettingsForGameplayCommand();
        manager.SkipToChapter2ForTesting();
    }

    private void SkipToChapter3()
    {
        ChapterManager manager = ResolveChapterManager();

        if (manager == null)
        {
            return;
        }

        roomListOpen = false;
        CloseSettingsForGameplayCommand();
        manager.SkipToChapter3ForTesting();
    }

    private void CloseSettingsForGameplayCommand()
    {
        if (!settingsOpen)
        {
            RefreshOpenState();
            return;
        }

        settingsOpen = false;
        debugOpen = false;
        roomListOpen = false;
        RefreshOpenState();
    }

    private void RefreshOpenState()
    {
        BlocksGameInput = settingsOpen;
        NavigationCursorController.SetGameplayHoverBlocked(settingsOpen);
        ApplySettingsPauseState();
        UpdateSettingsButtonLayout();

        if (settingsOverlay != null)
        {
            settingsOverlay.gameObject.SetActive(settingsOpen);
            settingsOverlay.SetAsFirstSibling();
        }

        if (settingsPanel != null)
        {
            settingsPanel.gameObject.SetActive(settingsOpen);
            settingsPanel.SetAsLastSibling();
        }

        if (settingsButton != null)
        {
            settingsButton.transform.SetAsLastSibling();
        }

        if (settingsList != null)
        {
            settingsList.gameObject.SetActive(settingsOpen);
        }

        if (debugList != null)
        {
            debugList.gameObject.SetActive(settingsOpen && debugOpen);

            if (settingsOpen && debugOpen)
            {
                RefreshDebugTimeControl();
            }
        }

        if (settingsOpen)
        {
            RefreshAudioControls();
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

    private void UpdateSettingsButtonLayout()
    {
        if (settingsButton == null)
        {
            return;
        }

        RectTransform buttonRect = settingsButton.GetComponent<RectTransform>();

        if (settingsOpen)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(SettingsPanelWidth * 0.5f - 24f, SettingsPanelHeight * 0.5f - 22f);
            buttonRect.sizeDelta = new Vector2(92f, ButtonHeight);
            SetButtonLabel(settingsButton, "Close");
        }
        else
        {
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(12f, -12f);
            buttonRect.sizeDelta = new Vector2(112f, ButtonHeight);
            SetButtonLabel(settingsButton, "Settings");
        }
    }

    private void ApplySettingsPauseState()
    {
        if (settingsOpen)
        {
            if (!timeScalePausedForSettings)
            {
                timeScaleBeforeSettings = Time.timeScale;
                Time.timeScale = 0f;
                timeScalePausedForSettings = true;
            }

            return;
        }

        RestoreSettingsPauseState();
    }

    private void RestoreSettingsPauseState()
    {
        if (!timeScalePausedForSettings)
        {
            return;
        }

        Time.timeScale = timeScaleBeforeSettings;
        timeScalePausedForSettings = false;
    }

    private void ClearModalGameState()
    {
        if (settingsOpen)
        {
            settingsOpen = false;
            debugOpen = false;
            roomListOpen = false;
        }

        BlocksGameInput = false;
        NavigationCursorController.SetGameplayHoverBlocked(false);
        RestoreSettingsPauseState();
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

    private ChapterClock ResolveChapterClock()
    {
        if (chapterClock == null)
        {
            ChapterManager manager = ResolveChapterManager();
            chapterClock = manager != null
                ? manager.GetComponent<ChapterClock>()
                : FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        return chapterClock;
    }

    private void ApplyDebugGameTimeSpeed(float secondsPerGameMinute)
    {
        ChapterClock clock = ResolveChapterClock();

        if (clock == null)
        {
            RefreshDebugTimeControl();
            return;
        }

        clock.SetSecondsPerGameMinute(Mathf.Clamp(secondsPerGameMinute, MinSecondsPerGameMinute, MaxSecondsPerGameMinute));
        RefreshDebugTimeControl();
    }

    private void ApplyAudioVolume(GameAudioChannel channel, float normalizedVolume)
    {
        GameAudioSettings.SetVolume(channel, normalizedVolume);
        EnsureRuntimeAudioBindings();
        RefreshAudioControls();
    }

    private void EnsureRuntimeAudioBindings()
    {
        AudioSource musicSource = ResolveExplorationMusicSource();

        if (musicSource == null)
        {
            return;
        }

        GameAudioSettings.EnsureBinding(musicSource, GameAudioChannel.Music, Mathf.Max(0f, explorationMusicBaseVolume));
    }

    private AudioSource ResolveExplorationMusicSource()
    {
        if (explorationMusicSource != null)
        {
            return explorationMusicSource;
        }

        GameObject musicObject = GameObject.Find(ExplorationMusicObjectName);

        if (musicObject != null)
        {
            explorationMusicSource = musicObject.GetComponent<AudioSource>();
        }

        if (explorationMusicSource == null)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (IsExplorationMusicSource(source))
                {
                    explorationMusicSource = source;
                    break;
                }
            }
        }

        if (explorationMusicSource != null && explorationMusicBaseVolume < 0f)
        {
            explorationMusicBaseVolume = explorationMusicSource.volume;
        }

        return explorationMusicSource;
    }

    private static bool IsExplorationMusicSource(AudioSource source)
    {
        if (source == null)
        {
            return false;
        }

        if (source.gameObject.name == ExplorationMusicObjectName)
        {
            return true;
        }

        AudioClip clip = source.clip;
        return clip != null && clip.name == ExplorationMusicClipName;
    }

    private void RefreshDebugTimeControl()
    {
        ChapterClock clock = ResolveChapterClock();
        bool hasClock = clock != null;
        float value = hasClock ? clock.SecondsPerGameMinute : MinSecondsPerGameMinute;

        isUpdatingDebugTimeControl = true;

        RefreshDebugTimeSliderVisual(value, hasClock);

        if (debugTimeInput != null)
        {
            debugTimeInput.interactable = hasClock;
            debugTimeInput.SetTextWithoutNotify(hasClock ? value.ToString("0.##") : "--");
        }

        if (debugTimeLabel != null)
        {
            debugTimeLabel.text = "Game Time";
        }

        isUpdatingDebugTimeControl = false;
    }

    private void RefreshAudioControls()
    {
        isUpdatingAudioControl = true;

        RefreshAudioVolumeControl(
            dialogueAudioLabel,
            dialogueAudioInput,
            dialogueAudioSliderFill,
            dialogueAudioSliderHandle,
            GameAudioChannel.Dialogue,
            true);

        RefreshAudioVolumeControl(
            gameSoundsAudioLabel,
            gameSoundsAudioInput,
            gameSoundsAudioSliderFill,
            gameSoundsAudioSliderHandle,
            GameAudioChannel.GameSounds,
            true);

        RefreshAudioVolumeControl(
            atmosphereAudioLabel,
            atmosphereAudioInput,
            atmosphereAudioSliderFill,
            atmosphereAudioSliderHandle,
            GameAudioChannel.Atmosphere,
            true);

        RefreshAudioVolumeControl(
            musicAudioLabel,
            musicAudioInput,
            musicAudioSliderFill,
            musicAudioSliderHandle,
            GameAudioChannel.Music,
            true);

        isUpdatingAudioControl = false;
    }

    private static void RefreshAudioVolumeControl(
        TMP_Text label,
        TMP_InputField input,
        RectTransform fill,
        RectTransform handle,
        GameAudioChannel channel,
        bool interactable)
    {
        float normalizedVolume = GameAudioSettings.GetVolume(channel);
        normalizedVolume = Mathf.Clamp01(normalizedVolume);
        RefreshDebugSliderVisual(fill, handle, normalizedVolume, interactable);

        if (input != null)
        {
            input.interactable = interactable;
            input.SetTextWithoutNotify(interactable ? Mathf.RoundToInt(normalizedVolume * 100f).ToString() : "--");
        }

        if (label != null)
        {
            label.text = GameAudioSettings.GetDisplayName(channel);
        }
    }

    private void SetDebugGameTimeFromPointer(RectTransform sliderRect, PointerEventData eventData)
    {
        SetDebugSliderFromPointer(DebugSliderKind.Time, sliderRect, eventData);
    }

    private void SetDebugSliderFromPointer(DebugSliderKind kind, RectTransform sliderRect, PointerEventData eventData)
    {
        ChapterClock clock = kind == DebugSliderKind.Time ? ResolveChapterClock() : null;

        if (sliderRect == null || eventData == null)
        {
            return;
        }

        if (kind == DebugSliderKind.Time && clock == null)
        {
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null
            ? eventData.pressEventCamera
            : eventData.enterEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(sliderRect, eventData.position, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        Rect rect = sliderRect.rect;
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x));

        switch (kind)
        {
            case DebugSliderKind.Time:
                float secondsPerGameMinute = Mathf.Lerp(MinSecondsPerGameMinute, MaxSecondsPerGameMinute, normalized);
                ApplyDebugGameTimeSpeed(secondsPerGameMinute);
                break;
            case DebugSliderKind.Dialogue:
                ApplyAudioVolume(GameAudioChannel.Dialogue, normalized);
                break;
            case DebugSliderKind.GameSounds:
                ApplyAudioVolume(GameAudioChannel.GameSounds, normalized);
                break;
            case DebugSliderKind.Atmosphere:
                ApplyAudioVolume(GameAudioChannel.Atmosphere, normalized);
                break;
            case DebugSliderKind.Music:
                ApplyAudioVolume(GameAudioChannel.Music, normalized);
                break;
        }
    }

    private void RefreshDebugTimeSliderVisual(float value, bool hasClock)
    {
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(MinSecondsPerGameMinute, MaxSecondsPerGameMinute, value));
        RefreshDebugSliderVisual(debugTimeSliderFill, debugTimeSliderHandle, normalized, hasClock);
    }

    private static void RefreshDebugSliderVisual(RectTransform fill, RectTransform handle, float normalized, bool interactable)
    {
        if (fill == null || handle == null)
        {
            return;
        }

        normalized = Mathf.Clamp01(normalized);
        fill.anchorMin = new Vector2(0f, 0.32f);
        fill.anchorMax = new Vector2(normalized, 0.68f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        handle.anchorMin = new Vector2(normalized, 0.5f);
        handle.anchorMax = new Vector2(normalized, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(12f, 22f);

        Image fillImage = fill.GetComponent<Image>();
        Image handleImage = handle.GetComponent<Image>();

        ConfigureSolidImage(fillImage, interactable ? new Color(0.48f, 0.48f, 0.48f, 1f) : new Color(0.24f, 0.24f, 0.24f, 1f));
        ConfigureSolidImage(handleImage, interactable ? new Color(0.72f, 0.72f, 0.72f, 1f) : new Color(0.36f, 0.36f, 0.36f, 1f));
    }

    private static RectTransform FindOrCreateSettingsOverlay(Transform parent)
    {
        Transform existing = parent.Find(SettingsOverlayName);
        RectTransform rect = existing != null ? existing as RectTransform : null;

        if (rect == null)
        {
            GameObject overlayObject = new GameObject(SettingsOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = overlayObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            ConfigureSolidImage(image, new Color(0f, 0f, 0f, 0.82f));
            image.raycastTarget = true;
        }

        return rect;
    }

    private static RectTransform FindOrCreateSettingsPanel(Transform parent)
    {
        Transform existing = parent.Find(SettingsPanelName);
        RectTransform rect = existing != null ? existing as RectTransform : null;

        if (rect == null)
        {
            GameObject panelObject = new GameObject(SettingsPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            rect = panelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(SettingsPanelWidth, SettingsPanelHeight);

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            ConfigureSolidImage(image, new Color(0.02f, 0.02f, 0.024f, 0.96f));
            image.raycastTarget = true;
        }

        Outline outline = rect.GetComponent<Outline>();

        if (outline != null)
        {
            outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        return rect;
    }

    private static TMP_Text FindOrCreateSettingsTitle(Transform parent)
    {
        Transform existing = parent.Find(SettingsTitleName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (text == null)
        {
            GameObject titleObject = new GameObject(SettingsTitleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = titleObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            text = titleObject.GetComponent<TMP_Text>();
        }

        RectTransform titleRect = text.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(28f, -24f);
        titleRect.sizeDelta = new Vector2(360f, 38f);

        text.text = "Settings";
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
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
            HorizontalLayoutGroup horizontalLayout = rect.GetComponent<HorizontalLayoutGroup>();

            if (horizontalLayout != null)
            {
                DestroyComponent(horizontalLayout, true);
            }

            layout = rect.GetComponent<VerticalLayoutGroup>();
        }
        else
        {
            VerticalLayoutGroup verticalLayout = rect.GetComponent<VerticalLayoutGroup>();

            if (verticalLayout != null)
            {
                DestroyComponent(verticalLayout, true);
            }

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

    private RectTransform FindOrCreateDebugTimeControl(Transform parent)
    {
        Transform existing = parent.Find(DebugTimeControlName);
        RectTransform rect = existing != null ? existing as RectTransform : null;

        if (rect == null)
        {
            GameObject controlObject = new GameObject(DebugTimeControlName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            rect = controlObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.sizeDelta = new Vector2(DebugControlWidth, ButtonHeight);

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = DebugControlWidth;
        layoutElement.minWidth = DebugControlWidth;
        layoutElement.preferredHeight = ButtonHeight;
        layoutElement.minHeight = ButtonHeight;

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            ConfigureSolidImage(image, DebugTimeControlColor);
            image.raycastTarget = false;
        }

        debugTimeLabel = FindOrCreateControlText(rect, "Text_GameTimeLabel", "Game Time", 13f, TextAlignmentOptions.MidlineLeft);
        RectTransform labelRect = debugTimeLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(10f, 0f);
        labelRect.sizeDelta = new Vector2(DebugControlLabelWidth, 0f);

        debugTimeSlider = FindOrCreateDebugSlider(rect, "Slider_DebugSecondsPerGameMinute", DebugSliderKind.Time, out debugTimeSliderFill, out debugTimeSliderHandle);

        debugTimeInput = FindOrCreateDebugInput(rect, "Input_DebugSecondsPerGameMinute");
        debugTimeInput.onEndEdit.RemoveAllListeners();
        debugTimeInput.onEndEdit.AddListener(value =>
        {
            if (!isUpdatingDebugTimeControl && float.TryParse(value, out float parsed))
            {
                ApplyDebugGameTimeSpeed(parsed);
            }
        });

        RefreshDebugTimeControl();
        return rect;
    }

    private RectTransform FindOrCreateAudioVolumeControl(
        Transform parent,
        string controlName,
        GameAudioChannel channel,
        DebugSliderKind kind)
    {
        Transform existing = parent.Find(controlName);
        RectTransform rect = existing != null ? existing as RectTransform : null;

        if (rect == null)
        {
            GameObject controlObject = new GameObject(controlName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            rect = controlObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.sizeDelta = new Vector2(DebugControlWidth, ButtonHeight);

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = DebugControlWidth;
        layoutElement.minWidth = DebugControlWidth;
        layoutElement.preferredHeight = ButtonHeight;
        layoutElement.minHeight = ButtonHeight;

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            ConfigureSolidImage(image, DebugTimeControlColor);
            image.raycastTarget = false;
        }

        string label = GameAudioSettings.GetDisplayName(channel);
        TMP_Text labelText = FindOrCreateControlText(rect, $"Text_{kind}Label", label, 13f, TextAlignmentOptions.MidlineLeft);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(10f, 0f);
        labelRect.sizeDelta = new Vector2(DebugControlLabelWidth, 0f);

        string sliderName = $"Slider_Audio{kind}";
        RectTransform slider = FindOrCreateDebugSlider(rect, sliderName, kind, out RectTransform sliderFill, out RectTransform sliderHandle);

        string inputName = $"Input_Audio{kind}";
        TMP_InputField input = FindOrCreateDebugInput(rect, inputName);
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.RemoveAllListeners();
        input.onEndEdit.AddListener(value =>
        {
            if (isUpdatingAudioControl)
            {
                return;
            }

            if (float.TryParse(value, out float parsed))
            {
                ApplyAudioVolume(channel, Mathf.Clamp01(parsed / 100f));
            }
        });

        if (kind == DebugSliderKind.Dialogue)
        {
            dialogueAudioLabel = labelText;
            dialogueAudioSliderFill = sliderFill;
            dialogueAudioSliderHandle = sliderHandle;
            dialogueAudioInput = input;
        }
        else if (kind == DebugSliderKind.GameSounds)
        {
            gameSoundsAudioLabel = labelText;
            gameSoundsAudioSliderFill = sliderFill;
            gameSoundsAudioSliderHandle = sliderHandle;
            gameSoundsAudioInput = input;
        }
        else if (kind == DebugSliderKind.Atmosphere)
        {
            atmosphereAudioLabel = labelText;
            atmosphereAudioSliderFill = sliderFill;
            atmosphereAudioSliderHandle = sliderHandle;
            atmosphereAudioInput = input;
        }
        else if (kind == DebugSliderKind.Music)
        {
            musicAudioLabel = labelText;
            musicAudioSliderFill = sliderFill;
            musicAudioSliderHandle = sliderHandle;
            musicAudioInput = input;
        }

        return rect;
    }

    private RectTransform FindOrCreateDebugSlider(RectTransform parent, string sliderName, DebugSliderKind kind, out RectTransform sliderFill, out RectTransform sliderHandle)
    {
        Transform existing = parent.Find(sliderName);
        RectTransform rect = existing as RectTransform;

        if (rect == null)
        {
            GameObject sliderObject = new GameObject(sliderName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(DebugControlSliderLeft, 0f);
        rect.sizeDelta = new Vector2(DebugControlWidth - DebugControlSliderLeft - DebugControlInputWidth - 20f, 22f);

        Slider oldSlider = rect.GetComponent<Slider>();

        if (oldSlider != null)
        {
            oldSlider.enabled = false;
            oldSlider.fillRect = null;
            oldSlider.handleRect = null;
            oldSlider.targetGraphic = null;
            DestroyComponent(oldSlider);
        }

        Image hitTarget = rect.GetComponent<Image>();

        if (hitTarget == null)
        {
            hitTarget = rect.gameObject.AddComponent<Image>();
        }

        ConfigureSolidImage(hitTarget, new Color(0f, 0f, 0f, 0f));
        hitTarget.raycastTarget = true;

        Image backgroundImage = FindOrCreateSliderImage(rect, "Background", new Color(0.18f, 0.18f, 0.18f, 1f), new Vector2(0f, 0.32f), new Vector2(1f, 0.68f));
        Image fillImage = FindOrCreateSliderImage(rect, "Fill", new Color(0.48f, 0.48f, 0.48f, 1f), new Vector2(0f, 0.32f), new Vector2(1f, 0.68f));
        Image handleImage = FindOrCreateSliderImage(rect, "Handle", new Color(0.72f, 0.72f, 0.72f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        RectTransform handleRect = handleImage.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12f, 22f);
        backgroundImage.raycastTarget = false;
        fillImage.raycastTarget = false;
        handleImage.raycastTarget = false;

        sliderFill = fillImage.GetComponent<RectTransform>();
        sliderHandle = handleRect;

        DebugSliderDragTarget dragTarget = rect.GetComponent<DebugSliderDragTarget>();

        if (dragTarget == null)
        {
            dragTarget = rect.gameObject.AddComponent<DebugSliderDragTarget>();
        }

        dragTarget.Initialize(this, rect, kind);
        return rect;
    }

    private static TMP_InputField FindOrCreateDebugInput(RectTransform parent, string inputName)
    {
        Transform existing = parent.Find(inputName);
        TMP_InputField input = existing != null ? existing.GetComponent<TMP_InputField>() : null;
        RectTransform rect;

        if (input == null)
        {
            GameObject inputObject = new GameObject(inputName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            rect = inputObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            input = inputObject.GetComponent<TMP_InputField>();
        }
        else
        {
            rect = input.GetComponent<RectTransform>();
        }

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        rect.sizeDelta = new Vector2(DebugControlInputWidth, 26f);

        Image image = rect.GetComponent<Image>();

        if (image != null)
        {
            ConfigureSolidImage(image, DebugTimeInputColor);
        }

        TMP_Text text = FindOrCreateControlText(rect, "Text_Value", string.Empty, 13f, TextAlignmentOptions.Center);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5f, 1f);
        textRect.offsetMax = new Vector2(-5f, -1f);
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        return input;
    }

    private static Image FindOrCreateSliderImage(RectTransform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(objectName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        RectTransform rect;

        if (image == null)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            image = imageObject.GetComponent<Image>();
        }
        else
        {
            rect = image.GetComponent<RectTransform>();
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        ConfigureSolidImage(image, color);
        return image;
    }

    private static void ConfigureSolidImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        image.overrideSprite = null;
        image.material = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
    }

    private static void DestroyComponent(Component component)
    {
        DestroyComponent(component, false);
    }

    private static void DestroyComponent(Component component, bool immediate)
    {
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying && !immediate)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }
    }

    private static TMP_Text FindOrCreateControlText(Transform parent, string objectName, string label, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(objectName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            text = textObject.GetComponent<TMP_Text>();
        }

        text.text = label ?? string.Empty;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
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

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        FindOrCreateButtonLabel(button.transform).text = label ?? string.Empty;
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

    private sealed class DebugSliderDragTarget : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private RuntimeSettingsMenu owner;
        private RectTransform sliderRect;
        private DebugSliderKind kind;

        public void Initialize(RuntimeSettingsMenu owner, RectTransform sliderRect, DebugSliderKind kind)
        {
            this.owner = owner;
            this.sliderRect = sliderRect;
            this.kind = kind;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            owner?.SetDebugSliderFromPointer(kind, sliderRect, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.SetDebugSliderFromPointer(kind, sliderRect, eventData);
        }
    }
}
