using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string newGameSceneName = "SampleScene";
    [SerializeField] private string nightProgressKey = "Night";
    [SerializeField] private int startingNight = 1;
    [SerializeField] private bool resetNightProgressOnNewGame = true;

    [Header("Menu Soundscape")]
    [SerializeField] private AudioSource menuSoundscapeSource;
    [SerializeField] private AudioClip menuSoundscapeClip;
    [Tooltip("AudioSource.pitch changes playback speed and pitch together without time-stretching.")]
    [SerializeField, Range(0.01f, 3f)] private float menuSoundscapePlaybackSpeed = 0.52f;
    [SerializeField] private bool playSoundscapeOnStart = true;

    [Header("Responsive Layout")]
    public bool configureCanvasScaling = true;
    public Vector2 referenceResolution = new Vector2(1366f, 768f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public bool pinMenuLikeFoxyButton = true;
    public RectTransform menuPanel;
    public RectTransform title;
    public RectTransform newGameButton;
    public RectTransform continueButton;
    public RectTransform settingsButton;
    public RectTransform exitButton;
    public Vector2 titlePosition = new Vector2(170f, -68f);
    public Vector2 buttonStartPosition = new Vector2(24f, -305f);
    public Vector2 buttonSize = new Vector2(160f, 30f);
    public float buttonSpacing = 43f;

    private void Reset()
    {
        CacheMenuReferences();
    }

    private void Awake()
    {
        ConfigureCanvasScalers();
        CacheMenuReferences();
        CacheSoundscapeReference();
        ConfigureMenuSoundscape();
        ApplyPinnedMenuLayout();
    }

    private void Start()
    {
        ApplyPinnedMenuLayout();

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
        if (resetNightProgressOnNewGame)
        {
            PlayerPrefs.SetInt(nightProgressKey, Mathf.Max(1, startingNight));
            PlayerPrefs.Save();
        }

        StopMenuSoundscape();
        SceneManager.LoadScene(newGameSceneName, LoadSceneMode.Single);
    }

    public void ContinueGame()
    {
        StopMenuSoundscape();
        SceneManager.LoadScene(newGameSceneName, LoadSceneMode.Single);
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

    private RectTransform FindRectTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform as RectTransform : null;
    }

    private void ConfigureCanvasScalers()
    {
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
        if (!pinMenuLikeFoxyButton)
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
}
