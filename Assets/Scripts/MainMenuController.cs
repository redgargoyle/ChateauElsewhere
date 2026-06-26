using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    private const string DefaultGameplaySceneName = "Gameplay";
    private const string BackgroundImageObjectName = "Panel_Background";
    private const string ButtonOverlayName = "Button_StateOverlay";
    private const string MenuBackgroundPath = "Assets/Art/Final Images (DO NOT EDIT)/kitchen 2.png";
    private const string NewGameButtonPath = "Assets/Art/MainMenuButtons/MainMenu_NewGame.png";
    private const string ContinueButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Continue.png";
    private const string SettingsButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Settings.png";
    private const string ExitButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Exit.png";
    private const string TitleFontPath = "Assets/Art/UI/Fonts/LiberationSerif-Bold.ttf";
    private const string AudioSettingsPanelName = "Panel_AudioSettings";
    private const string CursorStylePanelName = "Panel_CursorStyleChooser";
    private const string CursorStyleFrameName = "Frame_CursorStyleChooser";

    [SerializeField] private string newGameSceneName = DefaultGameplaySceneName;

    [Header("Menu Soundscape")]
    [SerializeField] private AudioSource menuSoundscapeSource;
    [SerializeField] private AudioClip menuSoundscapeClip;
    [Tooltip("AudioSource.pitch changes playback speed and pitch together without time-stretching.")]
    [SerializeField, Range(0.01f, 3f)] private float menuSoundscapePlaybackSpeed = 0.52f;
    [SerializeField] private bool playSoundscapeOnStart = true;
    [SerializeField] private float menuSoundscapeBaseVolume = -1f;

    [Header("Menu Visuals")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite menuBackgroundSprite;
    [SerializeField] private Color backgroundTint = new Color(0.52f, 0.48f, 0.58f, 1f);
    [SerializeField] private Sprite newGameButtonSprite;
    [SerializeField] private Sprite continueButtonSprite;
    [SerializeField] private Sprite settingsButtonSprite;
    [SerializeField] private Sprite exitButtonSprite;
    [SerializeField] private Color buttonOverlayHoverColor = new Color(0.88f, 0.53f, 0.16f, 0.24f);
    [SerializeField] private Color buttonOverlayPressedColor = new Color(0.06f, 0.035f, 0.02f, 0.50f);

    [Header("Title Visuals")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Font titleSourceFont;
    [SerializeField] private TMP_FontAsset titleFontAsset;
    [SerializeField] private Color titleColor = new Color(0.94f, 0.86f, 0.72f, 1f);
    [SerializeField] private float titleFontSize = 48f;

    [Header("Responsive Layout")]
    public bool configureCanvasScaling = true;
    public Vector2 referenceResolution = new Vector2(1366f, 768f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public bool pinMenuToTopLeft = true;
    public bool applyLayoutEveryFrame = false;
    public RectTransform menuPanel;
    public RectTransform title;
    public RectTransform newGameButton;
    public RectTransform continueButton;
    public RectTransform settingsButton;
    public RectTransform exitButton;
    public Vector2 titlePosition = new Vector2(104f, -38f);
    public Vector2 titleSize = new Vector2(430f, 112f);
    public Vector2 buttonStartPosition = new Vector2(16f, -116f);
    public Vector2 buttonSize = new Vector2(960f, 240f);
    public float buttonSpacing = 128f;
    public Vector2 menuSafeMargin = new Vector2(16f, 16f);
    [Range(0.35f, 1f)]
    public float minResponsiveLayoutScale = 0.55f;

    private RectTransform audioSettingsPanel;
    private RectTransform cursorStylePanel;
    private readonly List<AudioSliderBinding> audioSettingsBindings = new List<AudioSliderBinding>();
    private bool audioSettingsVisible;
    private bool cursorStyleChooserVisible;
    private Vector2 lastMenuLayoutSize = new Vector2(-1f, -1f);

    private void Reset()
    {
        CacheMenuReferences();
        CacheVisualReferences();
    }

    private void Awake()
    {
        ConfigureCanvasScalers();
        CacheMenuReferences();
        CacheVisualReferences();
        ConfigureTitleVisual();
        CacheSoundscapeReference();
        ConfigureMenuSoundscape();
        ApplyPinnedMenuLayout();
        ApplyMenuVisuals();
    }

    private void OnEnable()
    {
        GameAudioSettings.VolumeChanged += HandleAudioSettingChanged;
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumeChanged -= HandleAudioSettingChanged;
    }

    private void Start()
    {
        ApplyPinnedMenuLayout();
        ApplyMenuVisuals();
        ConfigureTitleVisual();

        if (playSoundscapeOnStart)
        {
            PlayMenuSoundscape();
        }
    }

    private void Update()
    {
        if (applyLayoutEveryFrame || HasMenuLayoutSizeChanged())
        {
            ApplyPinnedMenuLayout();
        }

        if (cursorStyleChooserVisible && WasCancelPressed())
        {
            HideCursorStyleChooser();
        }
    }

    public void NewGame()
    {
        ShowCursorStyleChooser();
    }

    public void ContinueGame()
    {
        LoadGameScene("Continue");
    }

    public void ExitGame()
    {
        StopMenuSoundscape();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CacheMenuReferences()
    {
        if (menuPanel == null)
        {
            menuPanel = FindRectTransform("Panel_StartMenu");
        }

        if (title == null)
        {
            title = FindRectTransform("Text_Title");
        }

        if (titleText == null && title != null)
        {
            titleText = title.GetComponent<TextMeshProUGUI>();
        }

        if (newGameButton == null)
        {
            newGameButton = FindRectTransform("Button_NewGame");
        }

        if (continueButton == null)
        {
            continueButton = FindRectTransform("Button_Continue");
        }

        if (settingsButton == null)
        {
            settingsButton = FindRectTransform("Button_Settings");
        }

        if (exitButton == null)
        {
            exitButton = FindRectTransform("Button_Exit");
        }
    }

    private void CacheSoundscapeReference()
    {
        if (menuSoundscapeSource != null)
        {
            return;
        }

        GameObject musicObject = GameObject.Find("Audio_Music");

        if (musicObject != null)
        {
            menuSoundscapeSource = musicObject.GetComponent<AudioSource>();
        }

        if (menuSoundscapeSource == null)
        {
            menuSoundscapeSource = GetComponent<AudioSource>();
        }
    }

    private void CacheVisualReferences()
    {
        if (backgroundImage == null)
        {
            GameObject backgroundObject = GameObject.Find(BackgroundImageObjectName);

            if (backgroundObject != null)
            {
                backgroundImage = backgroundObject.GetComponent<Image>();
            }
        }
    }

    private void ConfigureTitleVisual()
    {
        if (titleText == null && title != null)
        {
            titleText = title.GetComponent<TextMeshProUGUI>();
        }

        if (titleText == null)
        {
            return;
        }

        titleSourceFont = ResolveFont(titleSourceFont, TitleFontPath);

        if (titleFontAsset == null && titleSourceFont != null)
        {
            titleFontAsset = TMP_FontAsset.CreateFontAsset(titleSourceFont);
        }

        if (titleFontAsset != null)
        {
            titleText.font = titleFontAsset;
        }

        titleText.fontSize = titleFontSize;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 30f;
        titleText.fontSizeMax = titleFontSize;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.raycastTarget = false;
        ApplyResponsiveTitleFont(GetResponsiveMenuLayoutScale());
    }

    private void ConfigureMenuSoundscape()
    {
        if (menuSoundscapeSource == null)
        {
            return;
        }

        if (menuSoundscapeClip != null)
        {
            menuSoundscapeSource.clip = menuSoundscapeClip;
        }

        menuSoundscapeSource.playOnAwake = false;
        menuSoundscapeSource.loop = true;
        menuSoundscapeSource.pitch = menuSoundscapePlaybackSpeed;
        menuSoundscapeSource.spatialBlend = 0f;

        if (menuSoundscapeBaseVolume < 0f)
        {
            menuSoundscapeBaseVolume = menuSoundscapeSource.volume;
        }

        GameAudioSettings.EnsureBinding(menuSoundscapeSource, GameAudioChannel.Music, menuSoundscapeBaseVolume);
    }

    private void ApplyMenuVisuals()
    {
        ApplyBackgroundVisual();
        ConfigureButtonVisual(newGameButton, ref newGameButtonSprite, NewGameButtonPath);
        ConfigureButtonVisual(continueButton, ref continueButtonSprite, ContinueButtonPath);
        ConfigureButtonVisual(settingsButton, ref settingsButtonSprite, SettingsButtonPath);
        ConfigureButtonVisual(exitButton, ref exitButtonSprite, ExitButtonPath);
        ConfigureSettingsButtonAction();

        if (menuPanel != null)
        {
            menuPanel.SetAsLastSibling();
        }
    }

    private void ApplyBackgroundVisual()
    {
        CacheVisualReferences();

        if (backgroundImage == null)
        {
            return;
        }

        menuBackgroundSprite = ResolveSprite(menuBackgroundSprite, MenuBackgroundPath);

        if (menuBackgroundSprite != null)
        {
            backgroundImage.sprite = menuBackgroundSprite;
        }

        backgroundImage.color = backgroundTint;
        backgroundImage.raycastTarget = false;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;

        RectTransform backgroundRect = backgroundImage.transform as RectTransform;

        if (backgroundRect != null)
        {
            PinStretch(backgroundRect);
            backgroundRect.SetAsFirstSibling();
        }
    }

    private void ConfigureButtonVisual(RectTransform buttonRect, ref Sprite buttonSprite, string editorAssetPath)
    {
        if (buttonRect == null)
        {
            return;
        }

        buttonSprite = ResolveSprite(buttonSprite, editorAssetPath);

        Button button = buttonRect.GetComponent<Button>();
        Image baseImage = buttonRect.GetComponent<Image>();

        if (baseImage != null && buttonSprite != null)
        {
            baseImage.sprite = buttonSprite;
            baseImage.color = Color.white;
            baseImage.type = Image.Type.Simple;
            baseImage.preserveAspect = false;
            baseImage.raycastTarget = true;
        }

        Image overlayImage = FindOrCreateButtonOverlay(buttonRect, buttonSprite);

        if (button != null && overlayImage != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = overlayImage;
            button.colors = CreateOverlayColorBlock();
        }

        ConfigureButtonCursor(buttonRect, button);
        HideLegacyButtonText(buttonRect);
    }

    private void ConfigureSettingsButtonAction()
    {
        if (settingsButton == null)
        {
            return;
        }

        Button button = settingsButton.GetComponent<Button>();

        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(ToggleAudioSettingsPanel);
    }

    private void ToggleAudioSettingsPanel()
    {
        if (cursorStyleChooserVisible)
        {
            HideCursorStyleChooser();
        }

        EnsureAudioSettingsPanel();
        audioSettingsVisible = !audioSettingsVisible;

        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.gameObject.SetActive(audioSettingsVisible);
            audioSettingsPanel.SetAsLastSibling();
        }

        RefreshAudioSettingsPanel();
    }

    private void ShowCursorStyleChooser()
    {
        if (audioSettingsPanel != null)
        {
            audioSettingsVisible = false;
            audioSettingsPanel.gameObject.SetActive(false);
        }

        EnsureCursorStyleChooserPanel();
        cursorStyleChooserVisible = true;

        if (cursorStylePanel != null)
        {
            cursorStylePanel.gameObject.SetActive(true);
            cursorStylePanel.SetAsLastSibling();
            SelectFirstCursorStyleCard();
        }
    }

    private void HideCursorStyleChooser()
    {
        cursorStyleChooserVisible = false;

        if (cursorStylePanel != null)
        {
            cursorStylePanel.gameObject.SetActive(false);
        }
    }

    private void SelectCursorStyleAndStart(int styleIndex)
    {
        NavigationCursorController.SetCursorStyle(styleIndex);
        HideCursorStyleChooser();
        LoadGameScene("New Game");
    }

    private void EnsureCursorStyleChooserPanel()
    {
        if (cursorStylePanel != null)
        {
            RefreshCursorStyleChooserPreviews();
            return;
        }

        RectTransform parent = menuPanel != null && menuPanel.parent is RectTransform menuParent
            ? menuParent
            : transform as RectTransform;

        if (parent == null)
        {
            return;
        }

        Transform existing = parent.Find(CursorStylePanelName);
        cursorStylePanel = existing as RectTransform;

        if (cursorStylePanel == null)
        {
            GameObject panelObject = new GameObject(CursorStylePanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cursorStylePanel = panelObject.GetComponent<RectTransform>();
            cursorStylePanel.SetParent(parent, false);
        }

        PinStretch(cursorStylePanel);

        Image panelImage = cursorStylePanel.GetComponent<Image>();

        if (panelImage != null)
        {
            panelImage.color = new Color(0.015f, 0.022f, 0.014f, 0.92f);
            panelImage.raycastTarget = true;
        }

        RectTransform frame = FindOrCreateCursorStyleFrame(cursorStylePanel);
        CreateCursorStyleHeader(frame);
        CreateCursorStyleGrid(frame);
        CreateCursorStyleBackButton(frame);
        cursorStylePanel.gameObject.SetActive(cursorStyleChooserVisible);
    }

    private RectTransform FindOrCreateCursorStyleFrame(RectTransform parent)
    {
        Transform existing = parent.Find(CursorStyleFrameName);
        RectTransform frame = existing as RectTransform;

        if (frame == null)
        {
            GameObject frameObject = new GameObject(CursorStyleFrameName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            frame = frameObject.GetComponent<RectTransform>();
            frame.SetParent(parent, false);
        }

        frame.anchorMin = new Vector2(0.5f, 0.5f);
        frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(1120f, 690f);
        frame.anchoredPosition = Vector2.zero;

        Image frameImage = frame.GetComponent<Image>();

        if (frameImage != null)
        {
            frameImage.color = new Color(0.055f, 0.105f, 0.07f, 0.97f);
            frameImage.raycastTarget = true;
        }

        Outline outline = frame.GetComponent<Outline>();

        if (outline != null)
        {
            outline.effectColor = new Color(0.82f, 0.64f, 0.27f, 0.95f);
            outline.effectDistance = new Vector2(4f, -4f);
        }

        return frame;
    }

    private void CreateCursorStyleHeader(RectTransform frame)
    {
        TMP_Text titleLabel = FindOrCreateChooserText(frame, "Text_CursorStyleTitle", 36f, TextAlignmentOptions.Center, FontStyles.Bold);
        titleLabel.text = "Choose Cursor Style";
        titleLabel.color = titleColor;

        RectTransform titleRect = titleLabel.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-64f, 46f);

        TMP_Text subtitleLabel = FindOrCreateChooserText(frame, "Text_CursorStyleSubtitle", 18f, TextAlignmentOptions.Center, FontStyles.Normal);
        subtitleLabel.text = "Select the cursor set you want to use.";
        subtitleLabel.color = new Color(0.92f, 0.82f, 0.58f, 1f);

        RectTransform subtitleRect = subtitleLabel.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -72f);
        subtitleRect.sizeDelta = new Vector2(-96f, 30f);
    }

    private void CreateCursorStyleGrid(RectTransform frame)
    {
        Transform existing = frame.Find("Grid_CursorStyles");
        RectTransform grid = existing as RectTransform;

        if (grid == null)
        {
            GameObject gridObject = new GameObject("Grid_CursorStyles", typeof(RectTransform), typeof(GridLayoutGroup));
            grid = gridObject.GetComponent<RectTransform>();
            grid.SetParent(frame, false);
        }

        grid.anchorMin = new Vector2(0.5f, 0.5f);
        grid.anchorMax = new Vector2(0.5f, 0.5f);
        grid.pivot = new Vector2(0.5f, 0.5f);
        grid.sizeDelta = new Vector2(1030f, 470f);
        grid.anchoredPosition = new Vector2(0f, -22f);

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();

        if (layout != null)
        {
            layout.cellSize = new Vector2(190f, 220f);
            layout.spacing = new Vector2(20f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        int[] styles = NavigationCursorController.GetAvailableCursorStyles();

        for (int i = 0; i < styles.Length; i++)
        {
            CreateOrRefreshCursorStyleCard(grid, styles[i]);
        }
    }

    private void CreateOrRefreshCursorStyleCard(RectTransform grid, int styleIndex)
    {
        string cardName = $"Button_CursorStyle_{styleIndex:00}";
        Transform existing = grid.Find(cardName);
        RectTransform card = existing as RectTransform;

        if (card == null)
        {
            GameObject cardObject = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            card = cardObject.GetComponent<RectTransform>();
            card.SetParent(grid, false);
        }

        Button button = card.GetComponent<Button>();
        Image cardImage = card.GetComponent<Image>();

        if (cardImage != null)
        {
            cardImage.color = new Color(0.105f, 0.14f, 0.08f, 0.98f);
            cardImage.raycastTarget = true;
        }

        if (button != null)
        {
            int capturedStyle = styleIndex;
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = cardImage;
            button.colors = CreateCursorStyleCardColors();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCursorStyleAndStart(capturedStyle));
        }

        ConfigureControlCursor(card, button);

        TMP_Text label = FindOrCreateChooserText(card, "Text_Label", 17f, TextAlignmentOptions.Center, FontStyles.Bold);
        label.text = $"Style {styleIndex}";
        label.color = new Color(0.95f, 0.78f, 0.34f, 1f);

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(-16f, 24f);

        RectTransform iconGrid = FindOrCreateCursorStyleIconGrid(card);
        CursorStyleCatalog.CursorAction[] previewActions = CursorStyleCatalog.ChooserPreviewActions;

        for (int i = 0; i < previewActions.Length; i++)
        {
            CreateOrRefreshCursorStylePreviewIcon(iconGrid, styleIndex, previewActions[i], i);
        }
    }

    private RectTransform FindOrCreateCursorStyleIconGrid(RectTransform card)
    {
        Transform existing = card.Find("Grid_PreviewIcons");
        RectTransform iconGrid = existing as RectTransform;

        if (iconGrid == null)
        {
            GameObject iconGridObject = new GameObject("Grid_PreviewIcons", typeof(RectTransform), typeof(GridLayoutGroup));
            iconGrid = iconGridObject.GetComponent<RectTransform>();
            iconGrid.SetParent(card, false);
        }

        iconGrid.anchorMin = new Vector2(0.5f, 0f);
        iconGrid.anchorMax = new Vector2(0.5f, 1f);
        iconGrid.pivot = new Vector2(0.5f, 0.5f);
        iconGrid.sizeDelta = new Vector2(156f, -44f);
        iconGrid.anchoredPosition = new Vector2(0f, -12f);

        GridLayoutGroup layout = iconGrid.GetComponent<GridLayoutGroup>();

        if (layout != null)
        {
            layout.cellSize = new Vector2(34f, 34f);
            layout.spacing = new Vector2(6f, 7f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        return iconGrid;
    }

    private void CreateOrRefreshCursorStylePreviewIcon(
        RectTransform iconGrid,
        int styleIndex,
        CursorStyleCatalog.CursorAction action,
        int actionIndex)
    {
        string objectName = $"Icon_{actionIndex:00}_{CursorStyleCatalog.GetActionResourceName(action)}";
        Transform existing = iconGrid.Find(objectName);
        RawImage image;

        if (existing == null)
        {
            GameObject iconObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            iconObject.transform.SetParent(iconGrid, false);
            image = iconObject.GetComponent<RawImage>();
        }
        else
        {
            image = existing.GetComponent<RawImage>();
        }

        if (image == null)
        {
            return;
        }

        image.texture = NavigationCursorController.LoadCursorStylePreview(styleIndex, action);
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private void CreateCursorStyleBackButton(RectTransform frame)
    {
        Transform existing = frame.Find("Button_CursorStyleBack");
        RectTransform backRect = existing as RectTransform;

        if (backRect == null)
        {
            GameObject backObject = new GameObject("Button_CursorStyleBack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backRect = backObject.GetComponent<RectTransform>();
            backRect.SetParent(frame, false);
        }

        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.sizeDelta = new Vector2(150f, 38f);
        backRect.anchoredPosition = new Vector2(0f, 22f);

        Image image = backRect.GetComponent<Image>();

        if (image != null)
        {
            image.color = new Color(0.18f, 0.14f, 0.075f, 1f);
            image.raycastTarget = true;
        }

        Button button = backRect.GetComponent<Button>();

        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            button.colors = CreateCursorStyleCardColors();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HideCursorStyleChooser);
        }

        ConfigureControlCursor(backRect, button);

        TMP_Text label = FindOrCreateChooserText(backRect, "Text_Label", 18f, TextAlignmentOptions.Center, FontStyles.Bold);
        label.text = "Back";
        label.color = titleColor;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private TMP_Text FindOrCreateChooserText(
        RectTransform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles fontStyle)
    {
        Transform existing = parent.Find(objectName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TMP_Text>();
        }

        if (titleFontAsset != null)
        {
            text.font = titleFontAsset;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private ColorBlock CreateCursorStyleCardColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.105f, 0.14f, 0.08f, 0.98f);
        colors.highlightedColor = new Color(0.31f, 0.25f, 0.09f, 1f);
        colors.selectedColor = new Color(0.36f, 0.29f, 0.11f, 1f);
        colors.pressedColor = new Color(0.07f, 0.045f, 0.02f, 1f);
        colors.disabledColor = new Color(0.06f, 0.06f, 0.05f, 0.82f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private void RefreshCursorStyleChooserPreviews()
    {
        if (cursorStylePanel == null)
        {
            return;
        }

        RectTransform frame = cursorStylePanel.Find(CursorStyleFrameName) as RectTransform;

        if (frame == null)
        {
            return;
        }

        RectTransform grid = frame.Find("Grid_CursorStyles") as RectTransform;

        if (grid == null)
        {
            return;
        }

        int[] styles = NavigationCursorController.GetAvailableCursorStyles();

        for (int i = 0; i < styles.Length; i++)
        {
            CreateOrRefreshCursorStyleCard(grid, styles[i]);
        }
    }

    private void SelectFirstCursorStyleCard()
    {
        if (EventSystem.current == null || cursorStylePanel == null)
        {
            return;
        }

        Button firstButton = cursorStylePanel.GetComponentInChildren<Button>(true);

        if (firstButton != null)
        {
            firstButton.Select();
        }
    }

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private void EnsureAudioSettingsPanel()
    {
        if (audioSettingsPanel != null)
        {
            return;
        }

        RectTransform parent = menuPanel != null && menuPanel.parent is RectTransform menuParent
            ? menuParent
            : transform as RectTransform;

        if (parent == null)
        {
            return;
        }

        Transform existing = parent.Find(AudioSettingsPanelName);
        audioSettingsPanel = existing as RectTransform;

        if (audioSettingsPanel == null)
        {
            GameObject panelObject = new GameObject(AudioSettingsPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            audioSettingsPanel = panelObject.GetComponent<RectTransform>();
            audioSettingsPanel.SetParent(parent, false);
        }

        audioSettingsPanel.anchorMin = new Vector2(0f, 1f);
        audioSettingsPanel.anchorMax = new Vector2(0f, 1f);
        audioSettingsPanel.pivot = new Vector2(0f, 1f);
        audioSettingsPanel.anchoredPosition = new Vector2(610f, -132f);
        audioSettingsPanel.sizeDelta = new Vector2(360f, 236f);

        Image panelImage = audioSettingsPanel.GetComponent<Image>();

        if (panelImage != null)
        {
            panelImage.color = new Color(0.02f, 0.018f, 0.014f, 0.9f);
            panelImage.raycastTarget = true;
        }

        VerticalLayoutGroup layout = audioSettingsPanel.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
        {
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        audioSettingsBindings.Clear();
        CreateAudioSettingsTitle(audioSettingsPanel);
        CreateAudioSettingsSlider(audioSettingsPanel, GameAudioChannel.Dialogue);
        CreateAudioSettingsSlider(audioSettingsPanel, GameAudioChannel.GameSounds);
        CreateAudioSettingsSlider(audioSettingsPanel, GameAudioChannel.Atmosphere);
        CreateAudioSettingsSlider(audioSettingsPanel, GameAudioChannel.Music);
        audioSettingsPanel.gameObject.SetActive(audioSettingsVisible);
    }

    private void CreateAudioSettingsTitle(RectTransform parent)
    {
        Transform existing = parent.Find("Text_AudioSettingsTitle");
        TMP_Text titleLabel = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (titleLabel == null)
        {
            GameObject titleObject = new GameObject("Text_AudioSettingsTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleObject.transform.SetParent(parent, false);
            titleLabel = titleObject.GetComponent<TMP_Text>();
        }

        titleLabel.text = "Audio";
        titleLabel.fontSize = 20f;
        titleLabel.color = titleColor;
        titleLabel.alignment = TextAlignmentOptions.Left;
        titleLabel.raycastTarget = false;

        LayoutElement layoutElement = titleLabel.GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            layoutElement.preferredHeight = 28f;
        }
    }

    private void CreateAudioSettingsSlider(RectTransform parent, GameAudioChannel channel)
    {
        string rowName = "Row_" + channel;
        Transform existing = parent.Find(rowName);
        RectTransform row = existing as RectTransform;

        if (row == null)
        {
            GameObject rowObject = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row = rowObject.GetComponent<RectTransform>();
            row.SetParent(parent, false);
        }

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();

        if (rowLayout != null)
        {
            rowLayout.preferredHeight = 32f;
            rowLayout.minHeight = 32f;
        }

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();

        if (layout != null)
        {
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        TMP_Text label = FindOrCreateAudioPanelText(row, "Text_Label", GameAudioSettings.GetDisplayName(channel), 14f, TextAlignmentOptions.MidlineLeft, 108f);
        Slider slider = FindOrCreateAudioPanelSlider(row, "Slider_" + channel);
        TMP_Text valueLabel = FindOrCreateAudioPanelText(row, "Text_Value", string.Empty, 13f, TextAlignmentOptions.MidlineRight, 40f);

        AudioSliderBinding binding = new AudioSliderBinding(channel, slider, valueLabel);
        binding.Refresh();
        audioSettingsBindings.Add(binding);
    }

    private TMP_Text FindOrCreateAudioPanelText(RectTransform parent, string objectName, string value, float fontSize, TextAlignmentOptions alignment, float width)
    {
        Transform existing = parent.Find(objectName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TMP_Text>();
        }

        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        LayoutElement layoutElement = text.GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
        }

        return text;
    }

    private Slider FindOrCreateAudioPanelSlider(RectTransform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        Slider slider = existing != null ? existing.GetComponent<Slider>() : null;
        RectTransform sliderRect;

        if (slider == null)
        {
            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider), typeof(LayoutElement));
            sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.SetParent(parent, false);
            slider = sliderObject.GetComponent<Slider>();
        }
        else
        {
            sliderRect = slider.GetComponent<RectTransform>();
        }

        LayoutElement layoutElement = slider.GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            layoutElement.preferredWidth = 154f;
            layoutElement.minWidth = 154f;
            layoutElement.preferredHeight = 26f;
        }

        Image hitArea = slider.GetComponent<Image>();

        if (hitArea != null)
        {
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;
        }

        Image background = FindOrCreateSliderPart(sliderRect, "Background", new Color(0.18f, 0.16f, 0.13f, 1f), new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image fill = FindOrCreateSliderPart(sliderRect, "Fill", new Color(0.72f, 0.54f, 0.28f, 1f), new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image handle = FindOrCreateSliderPart(sliderRect, "Handle", new Color(0.96f, 0.82f, 0.55f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

        background.raycastTarget = false;
        fill.raycastTarget = false;
        handle.raycastTarget = false;

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12f, 22f);

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        ConfigureControlCursor(sliderRect, slider);
        return slider;
    }

    private Image FindOrCreateSliderPart(RectTransform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(objectName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        RectTransform rect;

        if (image == null)
        {
            GameObject partObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = partObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            image = partObject.GetComponent<Image>();
        }
        else
        {
            rect = image.GetComponent<RectTransform>();
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.color = color;
        return image;
    }

    private void RefreshAudioSettingsPanel()
    {
        for (int i = 0; i < audioSettingsBindings.Count; i++)
        {
            audioSettingsBindings[i]?.Refresh();
        }
    }

    private void HandleAudioSettingChanged(GameAudioChannel channel, float volume)
    {
        RefreshAudioSettingsPanel();
    }

    private void ConfigureButtonCursor(RectTransform buttonRect, Button button)
    {
        ConfigureControlCursor(buttonRect, button);
    }

    private void ConfigureControlCursor(RectTransform buttonRect, Selectable selectable)
    {
        if (buttonRect == null)
        {
            return;
        }

        NavigationCursorHoverTarget cursorTarget = buttonRect.GetComponent<NavigationCursorHoverTarget>();

        if (cursorTarget == null)
        {
            cursorTarget = buttonRect.gameObject.AddComponent<NavigationCursorHoverTarget>();
        }

        cursorTarget.Configure(NavigationCursorController.HoverIcon.Ui, selectable, selectable != null);
    }

    private Image FindOrCreateButtonOverlay(RectTransform buttonRect, Sprite buttonSprite)
    {
        Transform existingOverlay = buttonRect.Find(ButtonOverlayName);
        Image overlayImage;

        if (existingOverlay != null)
        {
            overlayImage = existingOverlay.GetComponent<Image>();
        }
        else
        {
            GameObject overlayObject = new GameObject(ButtonOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.layer = buttonRect.gameObject.layer;
            overlayObject.transform.SetParent(buttonRect, false);
            overlayImage = overlayObject.GetComponent<Image>();
        }

        if (overlayImage == null)
        {
            return null;
        }

        RectTransform overlayRect = overlayImage.transform as RectTransform;

        if (overlayRect != null)
        {
            PinStretch(overlayRect);
            overlayRect.SetAsLastSibling();
        }

        overlayImage.sprite = buttonSprite;
        overlayImage.color = Color.clear;
        overlayImage.raycastTarget = false;
        overlayImage.type = Image.Type.Simple;
        overlayImage.preserveAspect = false;
        return overlayImage;
    }

    private ColorBlock CreateOverlayColorBlock()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.clear;
        colors.highlightedColor = buttonOverlayHoverColor;
        colors.selectedColor = buttonOverlayHoverColor;
        colors.pressedColor = buttonOverlayPressedColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0.28f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private void HideLegacyButtonText(RectTransform buttonRect)
    {
        for (int i = 0; i < buttonRect.childCount; i++)
        {
            Transform child = buttonRect.GetChild(i);

            if (child != null && child.name.StartsWith("Text", System.StringComparison.OrdinalIgnoreCase))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private Sprite ResolveSprite(Sprite sprite, string editorAssetPath)
    {
#if UNITY_EDITOR
        if (sprite == null && !string.IsNullOrEmpty(editorAssetPath))
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
        }
#endif
        return sprite;
    }

    private Font ResolveFont(Font font, string editorAssetPath)
    {
#if UNITY_EDITOR
        if (font == null && !string.IsNullOrEmpty(editorAssetPath))
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);
        }
#endif
        return font;
    }

    private void PlayMenuSoundscape()
    {
        CacheSoundscapeReference();
        ConfigureMenuSoundscape();

        if (menuSoundscapeSource == null || menuSoundscapeSource.clip == null || menuSoundscapeSource.isPlaying)
        {
            return;
        }

        if (!GameAudioSettings.TryPlay(menuSoundscapeSource))
        {
            Debug.LogWarning("Main menu soundscape did not start. Check the Audio_Music source and soundscape clip.", this);
        }
    }

    private void StopMenuSoundscape()
    {
        if (menuSoundscapeSource != null && menuSoundscapeSource.isPlaying)
        {
            menuSoundscapeSource.Stop();
        }
    }

    private void LoadGameScene(string menuAction)
    {
        string targetSceneName = string.IsNullOrWhiteSpace(newGameSceneName)
            ? DefaultGameplaySceneName
            : newGameSceneName.Trim();

        // The menu used to point at "SampleScene", which does not exist in this
        // project. If the serialized scene value ever gets stale again, fall back
        // to the real gameplay scene instead of making the button look dead.
        if (!Application.CanStreamedLevelBeLoaded(targetSceneName) &&
            !string.Equals(targetSceneName, DefaultGameplaySceneName, System.StringComparison.Ordinal) &&
            Application.CanStreamedLevelBeLoaded(DefaultGameplaySceneName))
        {
            Debug.LogWarning(
                $"{menuAction} was configured to load '{targetSceneName}', but that scene is not in Build Settings. Loading '{DefaultGameplaySceneName}' instead.",
                this);
            targetSceneName = DefaultGameplaySceneName;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError(
                $"{menuAction} could not load '{targetSceneName}'. Add the scene to File > Build Settings, or set MainMenuController to '{DefaultGameplaySceneName}'.",
                this);
            return;
        }

        GameplayRuntimeState.ResetForNewGame();
        StopMenuSoundscape();
        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    private RectTransform FindRectTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform as RectTransform : null;
    }

    private void ConfigureCanvasScalers()
    {
        transform.localScale = Vector3.one;

        if (!configureCanvasScaling)
        {
            return;
        }

        CanvasScaler[] canvasScalers = GetComponentsInChildren<CanvasScaler>(true);

        foreach (CanvasScaler canvasScaler in canvasScalers)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = matchWidthOrHeight;

            RectTransform canvasRect = canvasScaler.transform as RectTransform;

            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
            }
        }
    }

    private void ApplyPinnedMenuLayout()
    {
        if (!pinMenuToTopLeft)
        {
            return;
        }

        float layoutScale = GetResponsiveMenuLayoutScale();

        if (menuPanel != null)
        {
            menuPanel.anchorMin = Vector2.zero;
            menuPanel.anchorMax = Vector2.one;
            menuPanel.offsetMin = Vector2.zero;
            menuPanel.offsetMax = Vector2.zero;
            menuPanel.pivot = new Vector2(0.5f, 0.5f);
            menuPanel.localScale = Vector3.one;
        }

        PinTopLeft(title, titlePosition * layoutScale, titleSize * layoutScale);
        PinTopLeft(newGameButton, buttonStartPosition * layoutScale, buttonSize * layoutScale);
        PinTopLeft(continueButton, (buttonStartPosition + new Vector2(0f, -buttonSpacing)) * layoutScale, buttonSize * layoutScale);
        PinTopLeft(settingsButton, (buttonStartPosition + new Vector2(0f, -buttonSpacing * 2f)) * layoutScale, buttonSize * layoutScale);
        PinTopLeft(exitButton, (buttonStartPosition + new Vector2(0f, -buttonSpacing * 3f)) * layoutScale, buttonSize * layoutScale);
        ApplyResponsiveTitleFont(layoutScale);
        lastMenuLayoutSize = GetMenuLayoutViewportSize();
    }

    private float GetResponsiveMenuLayoutScale()
    {
        Vector2 viewportSize = GetMenuLayoutViewportSize();

        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
        {
            return 1f;
        }

        Vector2 referenceExtents = GetReferenceMenuLayoutExtents();

        if (referenceExtents.x <= 0f || referenceExtents.y <= 0f)
        {
            return 1f;
        }

        float availableWidth = Mathf.Max(1f, viewportSize.x - Mathf.Max(0f, menuSafeMargin.x) * 2f);
        float availableHeight = Mathf.Max(1f, viewportSize.y - Mathf.Max(0f, menuSafeMargin.y) * 2f);
        float widthScale = availableWidth / referenceExtents.x;
        float heightScale = availableHeight / referenceExtents.y;
        float scale = Mathf.Min(1f, Mathf.Min(widthScale, heightScale));
        return Mathf.Clamp(scale, Mathf.Clamp01(minResponsiveLayoutScale), 1f);
    }

    private Vector2 GetReferenceMenuLayoutExtents()
    {
        Vector2 extents = Vector2.zero;
        EncapsulateTopLeftLayout(ref extents, titlePosition, titleSize);
        EncapsulateTopLeftLayout(ref extents, buttonStartPosition, buttonSize);
        EncapsulateTopLeftLayout(ref extents, buttonStartPosition + new Vector2(0f, -buttonSpacing), buttonSize);
        EncapsulateTopLeftLayout(ref extents, buttonStartPosition + new Vector2(0f, -buttonSpacing * 2f), buttonSize);
        EncapsulateTopLeftLayout(ref extents, buttonStartPosition + new Vector2(0f, -buttonSpacing * 3f), buttonSize);
        return extents;
    }

    private void EncapsulateTopLeftLayout(ref Vector2 extents, Vector2 anchoredPosition, Vector2 size)
    {
        extents.x = Mathf.Max(extents.x, anchoredPosition.x + size.x);
        extents.y = Mathf.Max(extents.y, -anchoredPosition.y + size.y);
    }

    private Vector2 GetMenuLayoutViewportSize()
    {
        RectTransform layoutRoot = menuPanel != null && menuPanel.parent is RectTransform menuParent
            ? menuParent
            : transform as RectTransform;

        if (layoutRoot != null)
        {
            Vector2 size = layoutRoot.rect.size;

            if (size.x > 0f && size.y > 0f)
            {
                return size;
            }
        }

        Canvas.ForceUpdateCanvases();

        if (layoutRoot != null)
        {
            Vector2 size = layoutRoot.rect.size;

            if (size.x > 0f && size.y > 0f)
            {
                return size;
            }
        }

        return referenceResolution;
    }

    private bool HasMenuLayoutSizeChanged()
    {
        Vector2 currentSize = GetMenuLayoutViewportSize();
        const float layoutPixelTolerance = 0.5f;
        return Mathf.Abs(currentSize.x - lastMenuLayoutSize.x) > layoutPixelTolerance ||
               Mathf.Abs(currentSize.y - lastMenuLayoutSize.y) > layoutPixelTolerance;
    }

    private void ApplyResponsiveTitleFont(float layoutScale)
    {
        if (titleText == null)
        {
            return;
        }

        titleText.fontSizeMax = Mathf.Max(18f, titleFontSize * layoutScale);
        titleText.fontSizeMin = Mathf.Min(30f, titleText.fontSizeMax);
    }

    private void PinTopLeft(RectTransform rectTransform, Vector2 anchoredPosition, Vector2? size)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = Vector3.one;

        if (size.HasValue)
        {
            rectTransform.sizeDelta = size.Value;
        }
    }

    private void PinStretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private sealed class AudioSliderBinding
    {
        private readonly GameAudioChannel channel;
        private readonly Slider slider;
        private readonly TMP_Text valueLabel;
        private bool updating;

        public AudioSliderBinding(GameAudioChannel channel, Slider slider, TMP_Text valueLabel)
        {
            this.channel = channel;
            this.slider = slider;
            this.valueLabel = valueLabel;

            if (this.slider != null)
            {
                this.slider.onValueChanged.RemoveAllListeners();
                this.slider.onValueChanged.AddListener(HandleSliderValueChanged);
            }
        }

        public void Refresh()
        {
            float volume = GameAudioSettings.GetVolume(channel);
            updating = true;

            if (slider != null)
            {
                slider.SetValueWithoutNotify(volume);
            }

            if (valueLabel != null)
            {
                valueLabel.text = Mathf.RoundToInt(volume * 100f).ToString();
            }

            updating = false;
        }

        private void HandleSliderValueChanged(float value)
        {
            if (updating)
            {
                return;
            }

            GameAudioSettings.SetVolume(channel, value);
            Refresh();
        }
    }
}
