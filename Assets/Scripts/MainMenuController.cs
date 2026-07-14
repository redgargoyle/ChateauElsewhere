using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
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
    private const string ButtonLabelBackdropName = "Button_LabelBackdrop";
    private const string ButtonLabelName = "Text_Label";
    private const string TitlePlaqueObjectName = "Image_TitlePlaque";
    private const string DeveloperCreditObjectName = "Text_DeveloperCredit";
    private const string MenuBackgroundPath = "Assets/Art/MainMenuRedesign/MainMenu_Background.png";
    private const string LegacyMenuBackgroundPath = "Assets/Art/Final Images (DO NOT EDIT)/kitchen 2.png";
    private const string BlankButtonPath = "Assets/Art/MainMenuRedesign/MainMenu_ButtonBlank.png";
    private const string LegacyNewGameButtonPath = "Assets/Art/MainMenuButtons/MainMenu_NewGame.png";
    private const string LegacyContinueButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Continue.png";
    private const string LegacySettingsButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Settings.png";
    private const string LegacyExitButtonPath = "Assets/Art/MainMenuButtons/MainMenu_Exit.png";
    private const string TitlePlaquePath = "Assets/Art/MainMenuRedesign/MainMenu_TitlePlaque.png";
    private const string TitleFontPath = "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf";
    private const string LegacyTitleFontPath = "Assets/Art/UI/Fonts/LiberationSerif-Bold.ttf";
    private const string AudioSettingsPanelName = "Panel_AudioSettings";
    private const string CursorStylePanelName = "Panel_CursorStyleChooser";
    private const string CursorStyleFrameName = "Frame_CursorStyleChooser";
    private const string MenuTitle = "Chantilly";
    private const string DeveloperCredit = "developed by Kadabra Games";

    private static readonly Vector2 RightRailReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Color Plum = new Color(0.25f, 0.075f, 0.16f, 1f);
    private static readonly Color Parchment = new Color(0.89f, 0.80f, 0.65f, 0.98f);
    private static readonly Color Gold = new Color(0.70f, 0.49f, 0.19f, 1f);
    private static readonly Dictionary<Font, TMP_FontAsset> RuntimeFontAssetsBySource =
        new Dictionary<Font, TMP_FontAsset>();

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
    [SerializeField] private Sprite primaryMenuBackgroundSprite;
    [FormerlySerializedAs("menuBackgroundSprite")]
    [SerializeField] private Sprite legacyMenuBackgroundSprite;
    [SerializeField] private Color backgroundTint = new Color(0.52f, 0.48f, 0.58f, 1f);
    [SerializeField] private Sprite sharedButtonFrameSprite;
    [FormerlySerializedAs("newGameButtonSprite")]
    [SerializeField] private Sprite legacyNewGameButtonSprite;
    [FormerlySerializedAs("continueButtonSprite")]
    [SerializeField] private Sprite legacyContinueButtonSprite;
    [FormerlySerializedAs("settingsButtonSprite")]
    [SerializeField] private Sprite legacySettingsButtonSprite;
    [FormerlySerializedAs("exitButtonSprite")]
    [SerializeField] private Sprite legacyExitButtonSprite;
    [SerializeField] private Image titlePlaqueImage;
    [FormerlySerializedAs("titlePlaqueSprite")]
    [SerializeField] private Sprite primaryTitlePlaqueSprite;
    [SerializeField] private Sprite legacyTitlePlaqueSprite;
    [SerializeField] private Color buttonOverlayHoverColor = new Color(0.88f, 0.53f, 0.16f, 0.24f);
    [SerializeField] private Color buttonOverlayPressedColor = new Color(0.06f, 0.035f, 0.02f, 0.50f);

    [Header("Title Visuals")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI developerCreditText;
    [SerializeField] private Font primaryTitleSourceFont;
    [FormerlySerializedAs("titleSourceFont")]
    [SerializeField] private Font legacyTitleSourceFont;
    [SerializeField] private TMP_FontAsset titleFontAsset;
    [SerializeField] private Color titleColor = new Color(0.94f, 0.86f, 0.72f, 1f);
    [SerializeField] private float titleFontSize = 48f;

    [Header("Responsive Layout")]
    public bool configureCanvasScaling = true;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public bool applyRightRailLayout = true;
    public bool applyLayoutEveryFrame = false;
    public RectTransform menuPanel;
    public RectTransform title;
    public RectTransform newGameButton;
    public RectTransform continueButton;
    public RectTransform settingsButton;
    public RectTransform exitButton;
    public Vector2 titlePlaquePosition = new Vector2(-64f, -64f);
    public Vector2 titlePlaqueSize = new Vector2(500f, 395f);
    public Vector2 rightRailButtonStartPosition = new Vector2(-72f, -478f);
    public Vector2 rightRailButtonSize = new Vector2(500f, 128f);
    public float rightRailButtonSpacing = 154f;
    public Vector2 menuSafeMargin = new Vector2(36f, 36f);
    [Range(0.35f, 1f)]
    public float minResponsiveLayoutScale = 0.55f;

    private RectTransform audioSettingsPanel;
    private RectTransform cursorStylePanel;
    private readonly List<AudioSliderBinding> audioSettingsBindings = new List<AudioSliderBinding>();
    private bool audioSettingsVisible;
    private bool cursorStyleChooserVisible;
    private TMP_FontAsset resolvedTitleFontAsset;
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
        ApplyMenuVisuals();
        ApplyRightRailLayout();
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
        ApplyMenuVisuals();
        ConfigureTitleVisual();
        ApplyRightRailLayout();

        if (playSoundscapeOnStart)
        {
            PlayMenuSoundscape();
        }
    }

    private void Update()
    {
        if (applyLayoutEveryFrame || HasMenuLayoutSizeChanged())
        {
            ApplyRightRailLayout();
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

        if (titlePlaqueImage == null && menuPanel != null)
        {
            Transform plaque = menuPanel.Find(TitlePlaqueObjectName);
            titlePlaqueImage = plaque != null ? plaque.GetComponent<Image>() : null;
        }

        if (developerCreditText == null && titlePlaqueImage != null)
        {
            Transform credit = titlePlaqueImage.transform.Find(DeveloperCreditObjectName);
            developerCreditText = credit != null ? credit.GetComponent<TextMeshProUGUI>() : null;
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

        Font sourceFont = ResolveFont(
            ref primaryTitleSourceFont,
            ref legacyTitleSourceFont,
            TitleFontPath,
            LegacyTitleFontPath);
        resolvedTitleFontAsset = ResolveTitleFontAsset(sourceFont);

        if (resolvedTitleFontAsset != null)
        {
            titleText.font = resolvedTitleFontAsset;
        }

        titleText.text = MenuTitle;
        titleText.fontSize = titleFontSize;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 28f;
        titleText.fontSizeMax = Mathf.Max(titleFontSize, 58f);
        titleText.color = Plum;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Normal;
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
        ConfigureMenuPanelVisual();
        ConfigureTitlePlaqueVisual();
        DeactivateContinueButton();
        ConfigureButtonVisual(newGameButton, ref legacyNewGameButtonSprite, LegacyNewGameButtonPath, "Start Game");
        ConfigureButtonVisual(settingsButton, ref legacySettingsButtonSprite, LegacySettingsButtonPath, "Settings");
        ConfigureButtonVisual(exitButton, ref legacyExitButtonSprite, LegacyExitButtonPath, "Exit");
        ConfigureSettingsButtonAction();
        ConfigureMainMenuNavigation();

        if (menuPanel != null)
        {
            menuPanel.SetAsLastSibling();
        }

        if (audioSettingsVisible && audioSettingsPanel != null)
        {
            audioSettingsPanel.SetAsLastSibling();
        }

        if (cursorStyleChooserVisible && cursorStylePanel != null)
        {
            cursorStylePanel.SetAsLastSibling();
        }
    }

    private void ApplyBackgroundVisual()
    {
        CacheVisualReferences();

        if (backgroundImage == null)
        {
            return;
        }

        Sprite menuBackgroundSprite = ResolveSprite(
            ref primaryMenuBackgroundSprite,
            ref legacyMenuBackgroundSprite,
            MenuBackgroundPath,
            LegacyMenuBackgroundPath);

        if (menuBackgroundSprite != null)
        {
            backgroundImage.sprite = menuBackgroundSprite;
        }

        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;

        RectTransform backgroundRect = backgroundImage.transform as RectTransform;

        if (backgroundRect != null)
        {
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.localScale = Vector3.one;

            AspectRatioFitter fitter = backgroundRect.GetComponent<AspectRatioFitter>();

            if (fitter == null)
            {
                fitter = backgroundRect.gameObject.AddComponent<AspectRatioFitter>();
            }

            if (menuBackgroundSprite != null && menuBackgroundSprite.rect.height > 0f)
            {
                fitter.aspectRatio = menuBackgroundSprite.rect.width / menuBackgroundSprite.rect.height;
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundRect.SetAsFirstSibling();
        }
    }

    private void ConfigureMenuPanelVisual()
    {
        if (menuPanel == null)
        {
            return;
        }

        PinStretch(menuPanel);
        Image panelImage = menuPanel.GetComponent<Image>();

        if (panelImage != null)
        {
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
        }
    }

    private void ConfigureTitlePlaqueVisual()
    {
        if (menuPanel == null)
        {
            return;
        }

        if (titlePlaqueImage == null)
        {
            Transform existing = menuPanel.Find(TitlePlaqueObjectName);

            if (existing != null)
            {
                titlePlaqueImage = existing.GetComponent<Image>();

                if (titlePlaqueImage == null)
                {
                    titlePlaqueImage = existing.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                GameObject plaqueObject = new GameObject(TitlePlaqueObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                plaqueObject.layer = menuPanel.gameObject.layer;
                plaqueObject.transform.SetParent(menuPanel, false);
                titlePlaqueImage = plaqueObject.GetComponent<Image>();
            }
        }

        Sprite titlePlaqueSprite = ResolveSprite(
            ref primaryTitlePlaqueSprite,
            ref legacyTitlePlaqueSprite,
            TitlePlaquePath,
            string.Empty);
        titlePlaqueImage.sprite = titlePlaqueSprite;
        titlePlaqueImage.color = Color.white;
        titlePlaqueImage.type = Image.Type.Simple;
        titlePlaqueImage.preserveAspect = true;
        titlePlaqueImage.raycastTarget = false;

        RectTransform plaqueRect = titlePlaqueImage.rectTransform;

        if (title != null && title.parent != plaqueRect)
        {
            title.SetParent(plaqueRect, false);
        }

        ConfigureTitleVisual();
        ConfigurePlaqueTextRect(title, new Vector2(0.12f, 0.43f), new Vector2(0.88f, 0.76f));
        developerCreditText = FindOrCreateTmpText(plaqueRect, DeveloperCreditObjectName, developerCreditText);
        developerCreditText.text = DeveloperCredit;
        ApplyMenuFont(developerCreditText);
        developerCreditText.fontSize = 21f;
        developerCreditText.enableAutoSizing = true;
        developerCreditText.fontSizeMin = 13f;
        developerCreditText.fontSizeMax = 21f;
        developerCreditText.color = Plum;
        developerCreditText.alignment = TextAlignmentOptions.Center;
        developerCreditText.fontStyle = FontStyles.Italic;
        developerCreditText.textWrappingMode = TextWrappingModes.NoWrap;
        developerCreditText.raycastTarget = false;
        ConfigurePlaqueTextRect(developerCreditText.rectTransform, new Vector2(0.10f, 0.27f), new Vector2(0.90f, 0.43f));
    }

    private void ConfigurePlaqueTextRect(RectTransform textRect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (textRect == null)
        {
            return;
        }

        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localScale = Vector3.one;
        textRect.SetAsLastSibling();
    }

    private void ConfigureButtonVisual(RectTransform buttonRect, ref Sprite legacyButtonSprite, string fallbackAssetPath, string labelText)
    {
        if (buttonRect == null)
        {
            return;
        }

        Sprite buttonSprite = ResolveSprite(
            ref sharedButtonFrameSprite,
            ref legacyButtonSprite,
            BlankButtonPath,
            fallbackAssetPath);
        bool usesLegacyBakedSprite = sharedButtonFrameSprite == null && buttonSprite == legacyButtonSprite;

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
        ConfigureButtonLabel(buttonRect, labelText, usesLegacyBakedSprite);
    }

    private void DeactivateContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        ResolveSprite(
            ref sharedButtonFrameSprite,
            ref legacyContinueButtonSprite,
            BlankButtonPath,
            LegacyContinueButtonPath);
        Button button = continueButton.GetComponent<Button>();

        if (button != null)
        {
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.interactable = false;
        }

        continueButton.gameObject.SetActive(false);
    }

    private void ConfigureButtonLabel(RectTransform buttonRect, string value, bool usesLegacyBakedSprite)
    {
        TextMeshProUGUI label = FindOrCreateTmpText(buttonRect, ButtonLabelName, null, true);

        if (label == null)
        {
            return;
        }

        for (int i = 0; i < buttonRect.childCount; i++)
        {
            Transform child = buttonRect.GetChild(i);

            if (child == label.transform)
            {
                continue;
            }

            if (child.GetComponent<TMP_Text>() != null || child.GetComponent<UnityEngine.UI.Text>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        label.gameObject.SetActive(true);
        ConfigureButtonLabelBackdrop(buttonRect, usesLegacyBakedSprite);
        label.text = value;
        ApplyMenuFont(label);
        label.fontSize = 38f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 38f;
        label.color = Plum;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineColor = new Color(0.96f, 0.84f, 0.58f, 0.55f);
        label.outlineWidth = 0.08f;
        label.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(44f, 14f);
        labelRect.offsetMax = new Vector2(-44f, -14f);
        labelRect.localScale = Vector3.one;

        Shadow shadow = label.GetComponent<Shadow>();

        if (shadow == null)
        {
            shadow = label.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0.16f, 0.035f, 0.09f, 0.28f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;
        labelRect.SetAsLastSibling();
    }

    private void ConfigureButtonLabelBackdrop(RectTransform buttonRect, bool visible)
    {
        Transform existing = buttonRect.Find(ButtonLabelBackdropName);

        if (!visible && existing == null)
        {
            return;
        }

        Image backdrop;

        if (existing != null)
        {
            backdrop = existing.GetComponent<Image>();

            if (backdrop == null)
            {
                backdrop = existing.gameObject.AddComponent<Image>();
            }
        }
        else
        {
            GameObject backdropObject = new GameObject(
                ButtonLabelBackdropName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backdropObject.layer = buttonRect.gameObject.layer;
            backdropObject.transform.SetParent(buttonRect, false);
            backdrop = backdropObject.GetComponent<Image>();
        }

        RectTransform backdropRect = backdrop.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.pivot = new Vector2(0.5f, 0.5f);
        backdropRect.offsetMin = new Vector2(66f, 20f);
        backdropRect.offsetMax = new Vector2(-66f, -20f);
        backdropRect.localScale = Vector3.one;
        backdrop.color = visible ? new Color(Parchment.r, Parchment.g, Parchment.b, 0.94f) : Color.clear;
        backdrop.raycastTarget = false;
        backdropRect.SetAsLastSibling();
    }

    private TextMeshProUGUI FindOrCreateTmpText(
        RectTransform parent,
        string objectName,
        TextMeshProUGUI preferred,
        bool reuseExistingChild = false)
    {
        TextMeshProUGUI text = preferred;

        if (text == null)
        {
            Transform existing = parent.Find(objectName);
            text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        }

        if (text == null && reuseExistingChild)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                text = parent.GetChild(i).GetComponent<TextMeshProUGUI>();

                if (text != null)
                {
                    break;
                }
            }
        }

        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else if (text.transform.parent != parent)
        {
            text.transform.SetParent(parent, false);
        }

        text.gameObject.name = objectName;
        return text;
    }

    private void ConfigureMainMenuNavigation()
    {
        Button startButton = newGameButton != null ? newGameButton.GetComponent<Button>() : null;
        Button settingsMenuButton = settingsButton != null ? settingsButton.GetComponent<Button>() : null;
        Button exitMenuButton = exitButton != null ? exitButton.GetComponent<Button>() : null;

        if (startButton == null || settingsMenuButton == null || exitMenuButton == null)
        {
            return;
        }

        SetExplicitNavigation(startButton, null, settingsMenuButton);
        SetExplicitNavigation(settingsMenuButton, startButton, exitMenuButton);
        SetExplicitNavigation(exitMenuButton, settingsMenuButton, null);

        if (EventSystem.current != null &&
            (EventSystem.current.currentSelectedGameObject == null ||
             EventSystem.current.currentSelectedGameObject == continueButton?.gameObject))
        {
            startButton.Select();
        }
    }

    private static void SetExplicitNavigation(Selectable selectable, Selectable up, Selectable down)
    {
        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        navigation.selectOnLeft = null;
        navigation.selectOnRight = null;
        selectable.navigation = navigation;
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

        ApplyMenuFont(text);

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
        RectTransform parent = menuPanel != null && menuPanel.parent is RectTransform menuParent
            ? menuParent
            : transform as RectTransform;

        if (parent == null)
        {
            return;
        }

        if (audioSettingsPanel == null)
        {
            Transform existing = parent.Find(AudioSettingsPanelName);
            audioSettingsPanel = existing as RectTransform;
        }

        if (audioSettingsPanel == null)
        {
            GameObject panelObject = new GameObject(AudioSettingsPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            audioSettingsPanel = panelObject.GetComponent<RectTransform>();
            audioSettingsPanel.SetParent(parent, false);
        }

        float modalScale = Mathf.Clamp(GetResponsiveMenuLayoutScale(), 0.72f, 1f);
        audioSettingsPanel.anchorMin = new Vector2(0.5f, 0.5f);
        audioSettingsPanel.anchorMax = new Vector2(0.5f, 0.5f);
        audioSettingsPanel.pivot = new Vector2(0.5f, 0.5f);
        audioSettingsPanel.anchoredPosition = Vector2.zero;
        audioSettingsPanel.sizeDelta = new Vector2(620f, 380f);
        audioSettingsPanel.localScale = Vector3.one * modalScale;

        Image panelImage = audioSettingsPanel.GetComponent<Image>();

        if (panelImage != null)
        {
            panelImage.color = Parchment;
            panelImage.raycastTarget = true;
        }

        Outline outline = audioSettingsPanel.GetComponent<Outline>();

        if (outline == null)
        {
            outline = audioSettingsPanel.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        VerticalLayoutGroup layout = audioSettingsPanel.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
        {
            layout.padding = new RectOffset(32, 32, 26, 26);
            layout.spacing = 14f;
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
        audioSettingsPanel.SetAsLastSibling();
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
        ApplyMenuFont(titleLabel);
        titleLabel.fontSize = 28f;
        titleLabel.color = Plum;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.raycastTarget = false;

        LayoutElement layoutElement = titleLabel.GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            layoutElement.preferredHeight = 46f;
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
            rowLayout.preferredHeight = 48f;
            rowLayout.minHeight = 42f;
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

        TMP_Text label = FindOrCreateAudioPanelText(row, "Text_Label", GameAudioSettings.GetDisplayName(channel), 18f, TextAlignmentOptions.MidlineLeft, 150f);
        Slider slider = FindOrCreateAudioPanelSlider(row, "Slider_" + channel);
        TMP_Text valueLabel = FindOrCreateAudioPanelText(row, "Text_Value", string.Empty, 17f, TextAlignmentOptions.MidlineRight, 48f);

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
        ApplyMenuFont(text);
        text.fontSize = fontSize;
        text.color = Plum;
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
            layoutElement.preferredWidth = 300f;
            layoutElement.minWidth = 220f;
            layoutElement.preferredHeight = 34f;
        }

        Image hitArea = slider.GetComponent<Image>();

        if (hitArea != null)
        {
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;
        }

        Image background = FindOrCreateSliderPart(sliderRect, "Background", new Color(0.31f, 0.12f, 0.20f, 0.55f), new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image fill = FindOrCreateSliderPart(sliderRect, "Fill", Gold, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image handle = FindOrCreateSliderPart(sliderRect, "Handle", Plum, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

        background.raycastTarget = false;
        fill.raycastTarget = false;
        handle.raycastTarget = false;

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 28f);

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

    private void ApplyMenuFont(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = GetMenuFontAsset();

        if (fontAsset != null)
        {
            text.font = fontAsset;
        }
    }

    private TMP_FontAsset GetMenuFontAsset()
    {
        if (resolvedTitleFontAsset != null)
        {
            return resolvedTitleFontAsset;
        }

        Font sourceFont = ResolveFont(
            ref primaryTitleSourceFont,
            ref legacyTitleSourceFont,
            TitleFontPath,
            LegacyTitleFontPath);
        resolvedTitleFontAsset = ResolveTitleFontAsset(sourceFont);
        return resolvedTitleFontAsset;
    }

    private TMP_FontAsset ResolveTitleFontAsset(Font sourceFont)
    {
        if (titleFontAsset != null &&
            (sourceFont == null || titleFontAsset.sourceFontFile == null || titleFontAsset.sourceFontFile == sourceFont))
        {
            return titleFontAsset;
        }

        if (sourceFont != null)
        {
            if (RuntimeFontAssetsBySource.TryGetValue(sourceFont, out TMP_FontAsset cachedFontAsset))
            {
                if (cachedFontAsset != null)
                {
                    return cachedFontAsset;
                }

                RuntimeFontAssetsBySource.Remove(sourceFont);
            }

            TMP_FontAsset generatedFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);

            if (generatedFontAsset != null)
            {
                generatedFontAsset.name = $"{sourceFont.name} Main Menu Runtime Font";
                generatedFontAsset.hideFlags = HideFlags.HideAndDontSave;
                RuntimeFontAssetsBySource[sourceFont] = generatedFontAsset;
                return generatedFontAsset;
            }
        }

        return titleFontAsset != null ? titleFontAsset : TMP_Settings.defaultFontAsset;
    }

    private Sprite ResolveSprite(
        ref Sprite primarySprite,
        ref Sprite fallbackSprite,
        string editorAssetPath,
        string fallbackAssetPath)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetPath))
        {
            Sprite editorPrimary = AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);

            if (editorPrimary != null)
            {
                primarySprite = editorPrimary;
            }
        }

        if (!string.IsNullOrEmpty(fallbackAssetPath))
        {
            Sprite editorFallback = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackAssetPath);

            if (editorFallback != null)
            {
                fallbackSprite = editorFallback;
            }
        }
#endif
        return primarySprite != null ? primarySprite : fallbackSprite;
    }

    private Font ResolveFont(
        ref Font primaryFont,
        ref Font fallbackFont,
        string editorAssetPath,
        string fallbackAssetPath)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetPath))
        {
            Font editorPrimary = AssetDatabase.LoadAssetAtPath<Font>(editorAssetPath);

            if (editorPrimary != null)
            {
                primaryFont = editorPrimary;
            }
        }

        if (!string.IsNullOrEmpty(fallbackAssetPath))
        {
            Font editorFallback = AssetDatabase.LoadAssetAtPath<Font>(fallbackAssetPath);

            if (editorFallback != null)
            {
                fallbackFont = editorFallback;
            }
        }
#endif
        return primaryFont != null ? primaryFont : fallbackFont;
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
        referenceResolution = RightRailReferenceResolution;

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

    private void ApplyRightRailLayout()
    {
        if (!applyRightRailLayout)
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

        RectTransform plaqueRect = titlePlaqueImage != null ? titlePlaqueImage.rectTransform : null;
        PinTopRight(plaqueRect, ScaleRightRailPosition(titlePlaquePosition, layoutScale), titlePlaqueSize * layoutScale);
        PinTopRight(
            newGameButton,
            ScaleRightRailPosition(rightRailButtonStartPosition, layoutScale),
            rightRailButtonSize * layoutScale);
        PinTopRight(
            settingsButton,
            ScaleRightRailPosition(rightRailButtonStartPosition + new Vector2(0f, -rightRailButtonSpacing), layoutScale),
            rightRailButtonSize * layoutScale);
        PinTopRight(
            exitButton,
            ScaleRightRailPosition(rightRailButtonStartPosition + new Vector2(0f, -rightRailButtonSpacing * 2f), layoutScale),
            rightRailButtonSize * layoutScale);
        ApplyResponsiveTitleFont(layoutScale);

        if (developerCreditText != null)
        {
            developerCreditText.fontSizeMax = Mathf.Max(13f, 21f * layoutScale);
            developerCreditText.fontSizeMin = Mathf.Min(13f, developerCreditText.fontSizeMax);
        }

        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.localScale = Vector3.one * Mathf.Clamp(layoutScale, 0.72f, 1f);
            audioSettingsPanel.anchoredPosition = Vector2.zero;
        }

        lastMenuLayoutSize = GetMenuLayoutViewportSize();
    }

    private float GetResponsiveMenuLayoutScale()
    {
        Vector2 viewportSize = GetMenuLayoutViewportSize();

        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
        {
            return 1f;
        }

        float availableWidth = Mathf.Max(1f, viewportSize.x - Mathf.Max(0f, menuSafeMargin.x) * 2f);
        float availableHeight = Mathf.Max(1f, viewportSize.y - Mathf.Max(0f, menuSafeMargin.y) * 2f);
        float referenceWidth = Mathf.Max(1f, RightRailReferenceResolution.x - Mathf.Max(0f, menuSafeMargin.x) * 2f);
        float referenceHeight = Mathf.Max(1f, RightRailReferenceResolution.y - Mathf.Max(0f, menuSafeMargin.y) * 2f);
        float widthScale = availableWidth / referenceWidth;
        float heightScale = availableHeight / referenceHeight;
        float scale = Mathf.Min(1f, Mathf.Min(widthScale, heightScale));
        return Mathf.Clamp(scale, Mathf.Clamp01(minResponsiveLayoutScale), 1f);
    }

    private Vector2 ScaleRightRailPosition(Vector2 authoredPosition, float layoutScale)
    {
        float x = -Mathf.Max(Mathf.Max(0f, menuSafeMargin.x), Mathf.Abs(authoredPosition.x) * layoutScale);
        float y = -Mathf.Max(Mathf.Max(0f, menuSafeMargin.y), Mathf.Abs(authoredPosition.y) * layoutScale);
        return new Vector2(x, y);
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

        titleText.fontSizeMax = Mathf.Max(24f, Mathf.Max(titleFontSize, 58f) * layoutScale);
        titleText.fontSizeMin = Mathf.Min(28f, titleText.fontSizeMax);
    }

    private void PinTopRight(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = Vector3.one;
        rectTransform.sizeDelta = size;
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
