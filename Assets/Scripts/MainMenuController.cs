using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [SerializeField] private string newGameSceneName = DefaultGameplaySceneName;

    [Header("Menu Soundscape")]
    [SerializeField] private AudioSource menuSoundscapeSource;
    [SerializeField] private AudioClip menuSoundscapeClip;
    [Tooltip("AudioSource.pitch changes playback speed and pitch together without time-stretching.")]
    [SerializeField, Range(0.01f, 3f)] private float menuSoundscapePlaybackSpeed = 0.52f;
    [SerializeField] private bool playSoundscapeOnStart = true;

    [Header("Menu Visuals")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite menuBackgroundSprite;
    [SerializeField] private Color backgroundTint = new Color(0.52f, 0.48f, 0.58f, 1f);
    [SerializeField] private Sprite newGameButtonSprite;
    [SerializeField] private Sprite continueButtonSprite;
    [SerializeField] private Sprite settingsButtonSprite;
    [SerializeField] private Sprite exitButtonSprite;
    [SerializeField] private Color buttonOverlayHoverColor = new Color(0.88f, 0.53f, 0.16f, 0.24f);
    [SerializeField] private Color buttonOverlayPressedColor = new Color(0.28f, 0.05f, 0.02f, 0.42f);

    [Header("Responsive Layout")]
    public bool configureCanvasScaling = true;
    public Vector2 referenceResolution = new Vector2(1366f, 768f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public bool pinMenuToTopLeft = true;
    public RectTransform menuPanel;
    public RectTransform title;
    public RectTransform newGameButton;
    public RectTransform continueButton;
    public RectTransform settingsButton;
    public RectTransform exitButton;
    public Vector2 titlePosition = new Vector2(170f, -68f);
    public Vector2 buttonStartPosition = new Vector2(26f, -178f);
    public Vector2 buttonSize = new Vector2(700f, 175f);
    public float buttonSpacing = 132f;

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
        CacheSoundscapeReference();
        ConfigureMenuSoundscape();
        ApplyPinnedMenuLayout();
        ApplyMenuVisuals();
    }

    private void Start()
    {
        ApplyPinnedMenuLayout();
        ApplyMenuVisuals();

        if (playSoundscapeOnStart)
        {
            PlayMenuSoundscape();
        }
    }

    private void Update()
    {
        ApplyPinnedMenuLayout();
    }

    public void NewGame()
    {
        LoadGameScene("New Game");
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
    }

    private void ApplyMenuVisuals()
    {
        ApplyBackgroundVisual();
        ConfigureButtonVisual(newGameButton, ref newGameButtonSprite, NewGameButtonPath);
        ConfigureButtonVisual(continueButton, ref continueButtonSprite, ContinueButtonPath);
        ConfigureButtonVisual(settingsButton, ref settingsButtonSprite, SettingsButtonPath);
        ConfigureButtonVisual(exitButton, ref exitButtonSprite, ExitButtonPath);

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

        HideLegacyButtonText(buttonRect);
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

    private void PlayMenuSoundscape()
    {
        CacheSoundscapeReference();
        ConfigureMenuSoundscape();

        if (menuSoundscapeSource == null || menuSoundscapeSource.clip == null || menuSoundscapeSource.isPlaying)
        {
            return;
        }

        menuSoundscapeSource.Play();
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

        if (menuPanel != null)
        {
            menuPanel.anchorMin = Vector2.zero;
            menuPanel.anchorMax = Vector2.one;
            menuPanel.offsetMin = Vector2.zero;
            menuPanel.offsetMax = Vector2.zero;
            menuPanel.pivot = new Vector2(0.5f, 0.5f);
            menuPanel.localScale = Vector3.one;
        }

        PinTopLeft(title, titlePosition, null);
        PinTopLeft(newGameButton, buttonStartPosition, buttonSize);
        PinTopLeft(continueButton, buttonStartPosition + new Vector2(0f, -buttonSpacing), buttonSize);
        PinTopLeft(settingsButton, buttonStartPosition + new Vector2(0f, -buttonSpacing * 2f), buttonSize);
        PinTopLeft(exitButton, buttonStartPosition + new Vector2(0f, -buttonSpacing * 3f), buttonSize);
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
}
