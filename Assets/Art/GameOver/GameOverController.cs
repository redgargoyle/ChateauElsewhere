using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverController : MonoBehaviour
{
    [Header("Static")]
    public GameObject staticPanel;
    public CanvasGroup staticGroup;
    public StaticNoisePlayer staticPlayer;
    public AudioSource staticAudio;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public CanvasGroup gameOverGroup;
    public RawImage gameOverImage;

    [Header("Gameplay UI")]
    public GameObject mapToggleButton;

    [Header("Power Outage Cleanup")]
    public BlackOutController blackOutController;

    [Header("Timing")]
    public float staticDuration = 5f;
    public float fadeDuration = 1.25f;
    public float restartDelay = 4f;

    [Header("After Game Over")]
    public bool returnToMainMenuAfterGameOver = true;
    public string mainMenuSceneName = "MainMenu";

    [Header("Debug Trigger")]
    public bool keyboardTriggerEnabled = true;
    public KeyCode gameOverKey = KeyCode.K;

    private Coroutine gameOverRoutine;
    private bool gameOverActive;
    private bool mapToggleWasActive;

    private void Reset()
    {
        staticAudio = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (staticAudio == null)
        {
            staticAudio = GetComponent<AudioSource>();
        }

        PrepareInitialState();
    }

    private void Start()
    {
        PrepareInitialState();
    }

    private void Update()
    {
        if (keyboardTriggerEnabled && !gameOverActive && Input.GetKeyDown(gameOverKey))
        {
            StartGameOver();
        }
    }

    public void TriggerGameOver()
    {
        StartGameOver();
    }

    public void StartGameOver()
    {
        if (gameOverActive && gameOverRoutine == null)
        {
            return;
        }

        gameOverActive = true;
        NightManager.StopActiveNight();
        HidePowerOutagePug();

        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
        }

        gameOverRoutine = StartCoroutine(Co_GameOver());
    }

    public void ShowGameOver()
    {
        StartGameOver();
    }

    public void ResetGameOver()
    {
        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
            gameOverRoutine = null;
        }

        gameOverActive = false;
        ApplyResponsiveLayout();
        RestoreGameplayUi();
        StopStatic();
        SetPanel(staticPanel, staticGroup, false, false);
        SetPanel(gameOverPanel, gameOverGroup, false, false);
    }

    private IEnumerator Co_GameOver()
    {
        ApplyResponsiveLayout();
        CancelCameraTransitions();
        HideGameplayUi();

        SetPanel(gameOverPanel, gameOverGroup, false, true);
        SetPanel(staticPanel, staticGroup, true, false);
        PlayStatic();

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, staticDuration));

        StopStatic();
        SetPanel(staticPanel, staticGroup, false, false);
        SetPanel(gameOverPanel, gameOverGroup, false, true);

        yield return FadeGameOver();

        if (returnToMainMenuAfterGameOver)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelay));
            ReturnToMainMenu();
            yield break;
        }

        gameOverRoutine = null;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        string targetScene = string.IsNullOrEmpty(mainMenuSceneName) ? "MainMenu" : mainMenuSceneName;
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }

    private IEnumerator FadeGameOver()
    {
        if (gameOverGroup == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, fadeDuration);

        if (duration <= 0f)
        {
            gameOverGroup.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            gameOverGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        gameOverGroup.alpha = 1f;
    }

    private void PrepareInitialState()
    {
        if (gameOverActive)
        {
            return;
        }

        ApplyResponsiveLayout();
        SetPanel(staticPanel, staticGroup, false, false);
        SetPanel(gameOverPanel, gameOverGroup, false, false);

        if (gameOverImage != null)
        {
            gameOverImage.enabled = true;
            gameOverImage.raycastTarget = false;
        }
    }

    private void PlayStatic()
    {
        if (staticPlayer != null && staticPlayer.CanPlay)
        {
            staticPlayer.Play();
        }

        if (staticAudio != null)
        {
            staticAudio.loop = true;
            staticAudio.Play();
        }
    }

    private void StopStatic()
    {
        if (staticPlayer != null)
        {
            staticPlayer.Stop();
        }

        if (staticAudio != null)
        {
            staticAudio.Stop();
        }
    }

    private void SetPanel(GameObject panel, CanvasGroup group, bool visible, bool blocksRaycasts)
    {
        if (panel != null && !panel.activeSelf)
        {
            panel.SetActive(true);
        }

        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = blocksRaycasts;
            group.blocksRaycasts = blocksRaycasts;
        }
    }

    private void HideGameplayUi()
    {
        mapToggleWasActive = mapToggleButton != null && mapToggleButton.activeSelf;

        if (mapToggleButton != null)
        {
            mapToggleButton.SetActive(false);
        }
    }

    private void RestoreGameplayUi()
    {
        if (mapToggleButton != null)
        {
            mapToggleButton.SetActive(mapToggleWasActive);
        }
    }

    private void ApplyResponsiveLayout()
    {
        Stretch(staticPanel);
        Stretch(gameOverPanel);

        if (gameOverImage != null)
        {
            Stretch(gameOverImage.gameObject);
        }
    }

    private void Stretch(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rectTransform = target.transform as RectTransform;

        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }

    private void CancelCameraTransitions()
    {
        CameraManager cameraManager = FindObjectOfType<CameraManager>();

        if (cameraManager != null)
        {
            cameraManager.CancelCameraSwitchTransition();
        }
    }

    private void ResolveBlackOutController()
    {
        if (blackOutController == null)
        {
            blackOutController = FindObjectOfType<BlackOutController>();
        }
    }

    private void HidePowerOutagePug()
    {
        ResolveBlackOutController();

        if (blackOutController != null)
        {
            blackOutController.StopPowerOutagePugAnimation();
        }

        GameObject pugObject = GameObject.Find("Image_PowerOutagePugAnimation");

        if (pugObject != null)
        {
            pugObject.SetActive(false);
        }
    }
}
