using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DemoCompleteUI : MonoBehaviour
{
    private const string CanvasObjectName = "Canvas_DemoCompleteOverlay";
    private const string RootObjectName = "Panel_DemoCompleteRoot";
    private const string MessageObjectName = "Text_DemoCompleteMessage";
    private const string ActionsObjectName = "Panel_DemoCompleteActions";
    private const string RestartButtonObjectName = "Button_RestartGame";
    private const string MainMenuButtonObjectName = "Button_MainMenu";
    private const string ButtonOverlayObjectName = "Button_StateOverlay";
    private const string ButtonLabelObjectName = "Text_Label";

    private static readonly Color Plum = new Color(0.25f, 0.075f, 0.16f, 1f);
    private static readonly Color Parchment = new Color(0.89f, 0.8f, 0.65f, 0.98f);
    private static readonly Color MessageColor = new Color(0.94f, 0.86f, 0.72f, 1f);
    private static readonly Color ButtonHoverColor = new Color(0.88f, 0.53f, 0.16f, 0.24f);
    private static readonly Color ButtonPressedColor = new Color(0.06f, 0.035f, 0.02f, 0.5f);
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Vector2 ButtonSize = new Vector2(500f, 128f);

    [Header("Main Menu Style")]
    [SerializeField] private TMP_FontAsset menuFontAsset;
    [SerializeField] private Sprite buttonFrameSprite;

    [Header("Content")]
    [SerializeField] private string completionMessage = "Demo Complete!\nTo be continued...";
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Overlay")]
    [SerializeField] private int overlaySortingOrder = 12001;
    [SerializeField] private float messageFontSize = 68f;
    [SerializeField] private float buttonFontSize = 38f;

    private Canvas canvas;
    private RectTransform root;
    private TextMeshProUGUI completionText;
    private RectTransform actionsRoot;
    private Button restartButton;
    private Button mainMenuButton;
    private Coroutine messageFadeRoutine;
    private CompletionState completionState;
    private bool warnedMissingMenuFont;
    private bool warnedMissingButtonFrame;

    private enum CompletionState
    {
        Hidden,
        Fading,
        Revealed
    }

    private void Awake()
    {
        EnsureUI();

        if (root != null)
        {
            root.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopActiveFade();
    }

    public void BeginFade(float duration)
    {
        if (completionState != CompletionState.Hidden)
        {
            return;
        }

        EnsureUI();
        StopActiveFade();

        if (root == null || completionText == null || actionsRoot == null)
        {
            Debug.LogError("Demo completion overlay could not be created.", this);
            return;
        }

        completionState = CompletionState.Fading;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        completionText.gameObject.SetActive(true);
        completionText.alpha = 0f;
        actionsRoot.gameObject.SetActive(false);

        if (!Application.isPlaying)
        {
            return;
        }

        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            completionText.alpha = 1f;
            return;
        }

        messageFadeRoutine = StartCoroutine(FadeMessage(safeDuration));
    }

    public void RevealActions()
    {
        if (completionState == CompletionState.Revealed)
        {
            return;
        }

        EnsureUI();
        StopActiveFade();

        if (root == null || completionText == null || actionsRoot == null)
        {
            Debug.LogError("Demo completion actions could not be revealed.", this);
            return;
        }

        completionState = CompletionState.Revealed;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        completionText.gameObject.SetActive(true);
        completionText.alpha = 1f;
        actionsRoot.gameObject.SetActive(true);
        ConfigureNavigation();
        EnsureEventSystem();

        if (EventSystem.current != null && restartButton != null)
        {
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }
    }

    public void RestartGame()
    {
        LoadScene(gameplaySceneName, "Restart Game");
    }

    public void ReturnToMainMenu()
    {
        LoadScene(mainMenuSceneName, "Main Menu");
    }

    private IEnumerator FadeMessage(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            completionText.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        completionText.alpha = 1f;
        messageFadeRoutine = null;
    }

    private void EnsureUI()
    {
        ValidateStyleAssets();
        canvas = FindOrCreateCanvas();

        if (canvas == null)
        {
            return;
        }

        root = FindOrCreateRect(canvas.transform, RootObjectName);
        StretchToParent(root);

        completionText = FindOrCreateMessage(root);
        actionsRoot = FindOrCreateRect(root, ActionsObjectName);
        ConfigureActionsRoot(actionsRoot);
        restartButton = FindOrCreateButton(actionsRoot, RestartButtonObjectName, "Restart Game", new Vector2(0f, 78f));
        mainMenuButton = FindOrCreateButton(actionsRoot, MainMenuButtonObjectName, "Main Menu", new Vector2(0f, -78f));

        restartButton.onClick.RemoveListener(RestartGame);
        restartButton.onClick.AddListener(RestartGame);
        mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        ConfigureNavigation();
    }

    private Canvas FindOrCreateCanvas()
    {
        GameObject canvasObject = GameObject.Find(CanvasObjectName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        SetUiLayerRecursively(canvasObject);
        return targetCanvas;
    }

    private TextMeshProUGUI FindOrCreateMessage(RectTransform parent)
    {
        Transform existing = parent.Find(MessageObjectName);
        TextMeshProUGUI text;

        if (existing == null)
        {
            GameObject textObject = new GameObject(
                MessageObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = existing.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 170f);
        rect.sizeDelta = new Vector2(1200f, 220f);
        rect.localScale = Vector3.one;

        text.text = completionMessage;
        text.font = ResolveMenuFontAsset();
        text.fontSize = messageFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 34f;
        text.fontSizeMax = Mathf.Max(34f, messageFontSize);
        text.color = MessageColor;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private Button FindOrCreateButton(
        RectTransform parent,
        string objectName,
        string labelValue,
        Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(objectName);
        GameObject buttonObject;

        if (existing == null)
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existing.gameObject;
        }

        RectTransform rect = buttonObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = ButtonSize;
        rect.localScale = Vector3.one;

        Image baseImage = buttonObject.GetComponent<Image>();
        baseImage.sprite = buttonFrameSprite;
        baseImage.color = buttonFrameSprite != null ? Color.white : Parchment;
        baseImage.type = Image.Type.Simple;
        baseImage.preserveAspect = false;
        baseImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        Image stateOverlay = FindOrCreateStateOverlay(rect, buttonFrameSprite);
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = stateOverlay;
        button.colors = CreateButtonColors();
        button.interactable = true;

        TextMeshProUGUI label = FindOrCreateButtonLabel(rect);
        label.text = labelValue;
        label.font = ResolveMenuFontAsset();
        label.fontSize = buttonFontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = Mathf.Max(22f, buttonFontSize);
        label.color = Plum;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineColor = new Color(0.96f, 0.84f, 0.58f, 0.55f);
        label.outlineWidth = 0.08f;
        label.raycastTarget = false;
        ConfigureButtonShadow(label);

        NavigationCursorHoverTarget cursorTarget = buttonObject.GetComponent<NavigationCursorHoverTarget>();

        if (cursorTarget == null)
        {
            cursorTarget = buttonObject.AddComponent<NavigationCursorHoverTarget>();
        }

        cursorTarget.Configure(NavigationCursorController.HoverIcon.Ui, button, true);
        return button;
    }

    private static Image FindOrCreateStateOverlay(RectTransform parent, Sprite buttonSprite)
    {
        Transform existing = parent.Find(ButtonOverlayObjectName);
        Image overlay;

        if (existing == null)
        {
            GameObject overlayObject = new GameObject(
                ButtonOverlayObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(parent, false);
            overlay = overlayObject.GetComponent<Image>();
        }
        else
        {
            overlay = existing.GetComponent<Image>();
        }

        StretchToParent(overlay.rectTransform);
        overlay.sprite = buttonSprite;
        overlay.color = Color.clear;
        overlay.raycastTarget = false;
        overlay.type = Image.Type.Simple;
        overlay.preserveAspect = false;
        overlay.rectTransform.SetAsLastSibling();
        return overlay;
    }

    private static TextMeshProUGUI FindOrCreateButtonLabel(RectTransform parent)
    {
        Transform existing = parent.Find(ButtonLabelObjectName);
        TextMeshProUGUI label;

        if (existing == null)
        {
            GameObject labelObject = new GameObject(
                ButtonLabelObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            label = existing.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(44f, 14f);
        rect.offsetMax = new Vector2(-44f, -14f);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        return label;
    }

    private static void ConfigureButtonShadow(TextMeshProUGUI label)
    {
        Shadow shadow = label.GetComponent<Shadow>();

        if (shadow == null)
        {
            shadow = label.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0.16f, 0.035f, 0.09f, 0.28f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;
    }

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.clear;
        colors.highlightedColor = ButtonHoverColor;
        colors.selectedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0.28f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private void ConfigureNavigation()
    {
        if (restartButton == null || mainMenuButton == null)
        {
            return;
        }

        Navigation restartNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = mainMenuButton,
            selectOnDown = mainMenuButton
        };
        restartButton.navigation = restartNavigation;

        Navigation menuNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = restartButton,
            selectOnDown = restartButton
        };
        mainMenuButton.navigation = menuNavigation;
    }

    private static RectTransform FindOrCreateRect(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);

        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.transform as RectTransform;
    }

    private static void ConfigureActionsRoot(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -165f);
        rect.sizeDelta = new Vector2(600f, 310f);
        rect.localScale = Vector3.one;
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetUiLayerRecursively(GameObject rootObject)
    {
        int uiLayer = LayerMask.NameToLayer("UI");

        if (uiLayer < 0)
        {
            return;
        }

        Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            transforms[index].gameObject.layer = uiLayer;
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void ValidateStyleAssets()
    {
        if (menuFontAsset == null && !warnedMissingMenuFont)
        {
            Debug.LogWarning(
                "DemoCompleteUI is missing the main-menu TMP font. Using the TMP default font.",
                this);
            warnedMissingMenuFont = true;
        }

        if (buttonFrameSprite == null && !warnedMissingButtonFrame)
        {
            Debug.LogWarning(
                "DemoCompleteUI is missing the main-menu button sprite. Using a parchment rectangle fallback.",
                this);
            warnedMissingButtonFrame = true;
        }
    }

    private TMP_FontAsset ResolveMenuFontAsset()
    {
        return menuFontAsset != null ? menuFontAsset : TMP_Settings.defaultFontAsset;
    }

    private void StopActiveFade()
    {
        if (messageFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(messageFadeRoutine);
        messageFadeRoutine = null;
    }

    private void LoadScene(string sceneName, string actionName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{actionName} could not load '{sceneName}'.", this);
            return;
        }

        StopActiveFade();
        GameplayRuntimeState.ResetForNewGame();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
