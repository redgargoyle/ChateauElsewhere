using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BlackOutController : MonoBehaviour
{
    [Header("Managers & Controllers")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private GameOverController gameOverController;
    [SerializeField] private PowerManager powerManager;
    [SerializeField] private SecurityRoomManager securityRoomManager;

    [Header("Optional Scene Hooks")]
    [SerializeField] private GameObject blackoutPanel;
    [SerializeField] private Graphic blackoutOverlay;
    [SerializeField] private CanvasGroup blackoutGroup;
    [SerializeField] private GameObject litFaceObject;
    [SerializeField] private GameObject[] disableOnPowerOut;
    [SerializeField] private Animator[] stopAnimatorsOnPowerOut;

    [Header("Optional Room Textures")]
    [SerializeField] private Texture2D texSecurityRoomNoPower;
    [SerializeField] private Texture2D texSecurityRoomBlackOut;
    [SerializeField] private Texture2D texSecurityRoomLitFace;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource breakerSwitch;
    [SerializeField] private AudioSource generatorSlowDie;
    [SerializeField] private AudioSource powerOutageMusic;

    [Header("Power Outage Pug Animation")]
    [SerializeField] private StaticFrameGroup powerOutagePugFrames;
    [SerializeField] private RawImage powerOutagePugImage;
    [SerializeField] private float powerOutagePugDelay = 5f;
    [SerializeField] private bool loopPowerOutagePugAnimation = true;
    [SerializeField] private bool stretchPowerOutagePugToScreen;
    [SerializeField] private bool preservePowerOutagePugAspect = true;
    [SerializeField] private Vector2 powerOutagePugAnchoredPosition = new Vector2(24f, 0f);
    [SerializeField] private Vector2 powerOutagePugSize = new Vector2(300f, 450f);

    [Header("Power Outage Static Overlay")]
    [SerializeField] private StaticFrameGroup powerOutageStaticFrames;
    [SerializeField] private RawImage powerOutageStaticImage;
    [SerializeField] private bool enablePowerOutageStaticFlicker = true;
    [SerializeField] private Vector2 powerOutageStaticBurstInterval = new Vector2(0.7f, 3.2f);
    [SerializeField] private Vector2 powerOutageStaticBurstDuration = new Vector2(0.05f, 0.22f);
    [SerializeField, Range(0f, 1f)] private float powerOutageStaticMinAlpha = 0.06f;
    [SerializeField, Range(0f, 1f)] private float powerOutageStaticMaxAlpha = 0.22f;

    [Header("Sequence Timing")]
    [SerializeField] private float initialStateHold = 0.4f;
    [SerializeField] private bool startGameOverAfterPowerOut = true;
    [SerializeField] private float powerOutageGameOverDelay = 10f;
    [SerializeField] private float lowPitchedWhineDuration = 15f;
    [SerializeField] private float carnivalMusicDuration = 18f;
    [SerializeField] private float faceFlickerDuration = 3f;
    [SerializeField] private float faceFlickerMinInterval = 0.06f;
    [SerializeField] private float faceFlickerMaxInterval = 0.28f;
    [SerializeField] private float roomFlickerDuration = 4f;
    [SerializeField] private float roomFlickerInterval = 0.18f;

    [Header("Overlay Colors")]
    [SerializeField] private Color roomPowerOutOverlay = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color blackOutOverlay = new Color(0f, 0f, 0f, 1f);

    [Header("Events")]
    [SerializeField] private UnityEvent onPowerOutStarted;
    [SerializeField] private UnityEvent onDoorsOpenRequested;
    [SerializeField] private UnityEvent onFaceFlickerStarted;
    [SerializeField] private UnityEvent onRoomFlickerStarted;

    private Coroutine blackoutRoutine;
    private Coroutine powerOutagePugRoutine;
    private Coroutine powerOutageStaticRoutine;
    private bool hasResolvedOverlay;

    private void Awake()
    {
        ResolveReferences();
        SetLitFaceVisible(false);
        SetOverlayVisible(false);
        SetPowerOutagePugVisible(false);
        SetPowerOutageStaticVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (powerManager != null)
        {
            powerManager.OnPowerOut += OnPowerOut;
        }
    }

    private void OnDisable()
    {
        StopPowerOutagePugAnimation();
        StopPowerOutageStaticFlicker();

        if (powerManager != null)
        {
            powerManager.OnPowerOut -= OnPowerOut;
        }
    }

    public void OnPowerOut()
    {
        if (blackoutRoutine != null)
        {
            return;
        }

        blackoutRoutine = StartCoroutine(C_BlackOutSequence());
    }

    public void StopPowerOutagePugAnimation()
    {
        if (powerOutagePugRoutine != null)
        {
            StopCoroutine(powerOutagePugRoutine);
            powerOutagePugRoutine = null;
        }

        SetPowerOutagePugVisible(false);
    }

    public IEnumerator C_BlackOutSequence()
    {
        ResolveReferences();
        StopNightTimer();
        StopSceneMotion();
        DisableConfiguredObjects();
        securityRoomManager?.HandlePowerOutStarted();
        onPowerOutStarted?.Invoke();

        SetRoomTexture(texSecurityRoomNoPower);
        SetOverlay(roomPowerOutOverlay, true);
        PlayIfAssigned(breakerSwitch);
        StartPowerOutagePugAnimationAfterDelay();
        StartPowerOutageStaticFlicker();

        if (startGameOverAfterPowerOut)
        {
            yield return WaitForPowerOutageGameOver();
            blackoutRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, initialStateHold));

        securityRoomManager?.OpenBothDoorsForPowerOut();
        onDoorsOpenRequested?.Invoke();
        PlayIfAssigned(generatorSlowDie);
        yield return WaitForAudioOrFallback(generatorSlowDie, lowPitchedWhineDuration);

        PlayIfAssigned(powerOutageMusic);
        yield return WaitForAudioOrFallback(powerOutageMusic, carnivalMusicDuration);

        onFaceFlickerStarted?.Invoke();
        yield return RandomFaceFlicker();

        onRoomFlickerStarted?.Invoke();
        yield return FlickerBetweenBlueAndBlack();

        StopPowerOutagePugAnimation();
        StopPowerOutageStaticFlicker();
        StopSequenceAudio();
        SetOverlayVisible(false);
        SetRoomTexture(texSecurityRoomLitFace);

        // The blackout sequence now hands off directly
        // to the normal game-over controller so the night still ends cleanly.
        if (gameOverController != null)
        {
            gameOverController.StartGameOver();
        }

        blackoutRoutine = null;
    }

    private IEnumerator WaitForPowerOutageGameOver()
    {
        float delay = Mathf.Max(0f, powerOutageGameOverDelay);
        float firstHold = Mathf.Min(delay, Mathf.Max(0f, initialStateHold));

        if (firstHold > 0f)
        {
            yield return new WaitForSecondsRealtime(firstHold);
        }

        securityRoomManager?.OpenBothDoorsForPowerOut();
        onDoorsOpenRequested?.Invoke();
        PlayIfAssigned(generatorSlowDie);

        float remainingDelay = delay - firstHold;

        if (remainingDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingDelay);
        }

        StartGameOverFromPowerOutage();
    }

    private void StartGameOverFromPowerOutage()
    {
        StopPowerOutagePugAnimation();
        StopPowerOutageStaticFlicker();
        StopSequenceAudio();
        SetLitFaceVisible(false);
        SetOverlayVisible(false);

        if (gameOverController != null)
        {
            gameOverController.StartGameOver();
        }
    }

    public IEnumerator RandomFaceFlicker()
    {
        float endTime = Time.unscaledTime + Mathf.Max(0f, faceFlickerDuration);

        while (Time.unscaledTime < endTime)
        {
            bool showFace = Random.value > 0.45f;
            SetLitFaceVisible(showFace);
            SetRoomTexture(showFace ? texSecurityRoomLitFace : texSecurityRoomNoPower);

            float delay = Random.Range(
                Mathf.Max(0.01f, faceFlickerMinInterval),
                Mathf.Max(faceFlickerMinInterval, faceFlickerMaxInterval)
            );

            yield return new WaitForSecondsRealtime(delay);
        }

        SetLitFaceVisible(false);
        SetRoomTexture(texSecurityRoomNoPower);
    }

    private IEnumerator FlickerBetweenBlueAndBlack()
    {
        float endTime = Time.unscaledTime + Mathf.Max(0f, roomFlickerDuration);
        bool black = false;

        while (Time.unscaledTime < endTime)
        {
            black = !black;
            SetRoomTexture(black ? texSecurityRoomBlackOut : texSecurityRoomNoPower);
            SetOverlay(black ? blackOutOverlay : roomPowerOutOverlay, true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, roomFlickerInterval));
        }

        SetRoomTexture(texSecurityRoomBlackOut);
        SetOverlay(blackOutOverlay, true);
    }

    private void ResolveReferences()
    {
        if (powerManager == null)
        {
            powerManager = FindAnyObjectByType<PowerManager>();
        }

        if (securityRoomManager == null)
        {
            securityRoomManager = FindAnyObjectByType<SecurityRoomManager>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>();
        }

        if (gameOverController == null)
        {
            gameOverController = FindAnyObjectByType<GameOverController>();
        }

        ResolveOverlay();
    }

    private void ResolveOverlay()
    {
        if (hasResolvedOverlay)
        {
            return;
        }

        if (blackoutPanel == null)
        {
            blackoutPanel = GameObject.Find("BlackoutPanel");
        }

        if (blackoutPanel == null)
        {
            blackoutPanel = GameObject.Find("BlackOutPanel");
        }

        if (blackoutPanel == null)
        {
            blackoutPanel = GameObject.Find("Blackout");
        }

        if (blackoutPanel == null)
        {
            return;
        }

        if (blackoutOverlay == null)
        {
            blackoutOverlay = blackoutPanel.GetComponent<Graphic>();
        }

        if (blackoutOverlay == null && blackoutPanel.transform is RectTransform)
        {
            blackoutOverlay = blackoutPanel.AddComponent<Image>();
        }

        if (blackoutGroup == null)
        {
            blackoutGroup = blackoutPanel.GetComponent<CanvasGroup>();
        }

        if (blackoutGroup == null)
        {
            blackoutGroup = blackoutPanel.AddComponent<CanvasGroup>();
        }

        StretchOverlay();
        hasResolvedOverlay = true;
    }

    private void StretchOverlay()
    {
        RectTransform rectTransform = blackoutPanel != null ? blackoutPanel.transform as RectTransform : null;

        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetAsLastSibling();
    }

    private void StartPowerOutagePugAnimationAfterDelay()
    {
        StopPowerOutagePugAnimation();

        if (powerOutagePugFrames == null || !powerOutagePugFrames.IsValid())
        {
            return;
        }

        powerOutagePugRoutine = StartCoroutine(PlayPowerOutagePugAnimation());
    }

    private IEnumerator PlayPowerOutagePugAnimation()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, powerOutagePugDelay));

        RawImage image = EnsurePowerOutagePugImage();

        if (image == null || powerOutagePugFrames == null || !powerOutagePugFrames.IsValid())
        {
            powerOutagePugRoutine = null;
            yield break;
        }

        image.gameObject.SetActive(true);
        image.transform.SetAsLastSibling();

        do
        {
            Texture2D[] frames = powerOutagePugFrames.frames;

            for (int i = 0; i < frames.Length; i++)
            {
                Texture2D frame = frames[i];

                if (frame != null)
                {
                    image.texture = frame;
                    ConfigurePowerOutagePugImage(frame);
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, powerOutagePugFrames.frameDuration));
            }
        }
        while (loopPowerOutagePugAnimation && powerOutagePugFrames != null && powerOutagePugFrames.IsValid());

        powerOutagePugRoutine = null;
    }

    private RawImage EnsurePowerOutagePugImage()
    {
        if (powerOutagePugImage == null)
        {
            GameObject existing = GameObject.Find("Image_PowerOutagePugAnimation");

            if (existing != null)
            {
                powerOutagePugImage = existing.GetComponent<RawImage>();
            }
        }

        if (powerOutagePugImage == null)
        {
            ResolveOverlay();
            Transform parent = blackoutPanel != null ? blackoutPanel.transform : FindTargetCanvasTransform();

            if (parent == null)
            {
                return null;
            }

            GameObject imageObject = new GameObject("Image_PowerOutagePugAnimation", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(parent, false);
            powerOutagePugImage = imageObject.GetComponent<RawImage>();
        }

        ConfigurePowerOutagePugImage(powerOutagePugImage.texture);
        powerOutagePugImage.raycastTarget = false;
        powerOutagePugImage.color = Color.white;
        return powerOutagePugImage;
    }

    private void ConfigurePowerOutagePugImage(Texture texture)
    {
        if (powerOutagePugImage == null)
        {
            return;
        }

        RectTransform rectTransform = powerOutagePugImage.transform as RectTransform;

        if (rectTransform != null)
        {
            if (stretchPowerOutagePugToScreen)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                rectTransform.anchorMin = new Vector2(0f, 0.5f);
                rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rectTransform.pivot = new Vector2(0f, 0.5f);
                rectTransform.anchoredPosition = powerOutagePugAnchoredPosition;
                rectTransform.sizeDelta = GetPowerOutagePugSize(texture);
            }

            rectTransform.localScale = Vector3.one;
        }

        AspectRatioFitter fitter = powerOutagePugImage.GetComponent<AspectRatioFitter>();

        if (!stretchPowerOutagePugToScreen || !preservePowerOutagePugAspect)
        {
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            return;
        }

        if (fitter == null)
        {
            fitter = powerOutagePugImage.gameObject.AddComponent<AspectRatioFitter>();
        }

        fitter.enabled = true;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        if (texture != null && texture.height > 0)
        {
            fitter.aspectRatio = (float)texture.width / texture.height;
        }
    }

    private Vector2 GetPowerOutagePugSize(Texture texture)
    {
        Vector2 size = new Vector2(
            Mathf.Max(1f, powerOutagePugSize.x),
            Mathf.Max(1f, powerOutagePugSize.y)
        );

        if (!preservePowerOutagePugAspect || texture == null || texture.height <= 0)
        {
            return size;
        }

        float textureAspect = (float)texture.width / texture.height;
        float targetAspect = size.x / size.y;

        if (textureAspect > targetAspect)
        {
            size.y = size.x / textureAspect;
        }
        else
        {
            size.x = size.y * textureAspect;
        }

        return size;
    }

    private void StartPowerOutageStaticFlicker()
    {
        StopPowerOutageStaticFlicker();

        if (!enablePowerOutageStaticFlicker || powerOutageStaticFrames == null || !powerOutageStaticFrames.IsValid())
        {
            return;
        }

        powerOutageStaticRoutine = StartCoroutine(PlayPowerOutageStaticFlicker());
    }

    private IEnumerator PlayPowerOutageStaticFlicker()
    {
        while (enablePowerOutageStaticFlicker && powerOutageStaticFrames != null && powerOutageStaticFrames.IsValid())
        {
            yield return new WaitForSecondsRealtime(RandomRange(powerOutageStaticBurstInterval, 0.01f));

            RawImage image = EnsurePowerOutageStaticImage();

            if (image == null)
            {
                continue;
            }

            float endTime = Time.unscaledTime + RandomRange(powerOutageStaticBurstDuration, 0.01f);
            image.gameObject.SetActive(true);

            while (Time.unscaledTime < endTime && powerOutageStaticFrames != null && powerOutageStaticFrames.IsValid())
            {
                Texture2D frame = powerOutageStaticFrames.GetFrame(Random.Range(0, powerOutageStaticFrames.frames.Length));

                if (frame != null)
                {
                    image.texture = frame;
                }

                float alpha = Random.Range(
                    Mathf.Min(powerOutageStaticMinAlpha, powerOutageStaticMaxAlpha),
                    Mathf.Max(powerOutageStaticMinAlpha, powerOutageStaticMaxAlpha)
                );

                image.color = new Color(1f, 1f, 1f, alpha);
                image.transform.SetAsLastSibling();

                yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, powerOutageStaticFrames.frameDuration));
            }

            image.gameObject.SetActive(false);
        }

        powerOutageStaticRoutine = null;
    }

    private void StopPowerOutageStaticFlicker()
    {
        if (powerOutageStaticRoutine != null)
        {
            StopCoroutine(powerOutageStaticRoutine);
            powerOutageStaticRoutine = null;
        }

        SetPowerOutageStaticVisible(false);
    }

    private RawImage EnsurePowerOutageStaticImage()
    {
        if (powerOutageStaticImage == null)
        {
            GameObject existing = GameObject.Find("Image_PowerOutageStaticOverlay");

            if (existing != null)
            {
                powerOutageStaticImage = existing.GetComponent<RawImage>();
            }
        }

        if (powerOutageStaticImage == null)
        {
            ResolveOverlay();
            Transform parent = blackoutPanel != null ? blackoutPanel.transform : FindTargetCanvasTransform();

            if (parent == null)
            {
                return null;
            }

            GameObject imageObject = new GameObject("Image_PowerOutageStaticOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(parent, false);
            powerOutageStaticImage = imageObject.GetComponent<RawImage>();
        }

        ConfigurePowerOutageStaticImage();
        powerOutageStaticImage.raycastTarget = false;
        return powerOutageStaticImage;
    }

    private void ConfigurePowerOutageStaticImage()
    {
        if (powerOutageStaticImage == null)
        {
            return;
        }

        RectTransform rectTransform = powerOutageStaticImage.transform as RectTransform;

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

    private float RandomRange(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float high = Mathf.Max(low, Mathf.Max(range.x, range.y));
        return Random.Range(low, high);
    }

    private Transform FindTargetCanvasTransform()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name == "Canvas_Background" || canvas.name == "Canvas_NightManager")
            {
                return canvas.transform;
            }
        }

        return canvases.Length > 0 ? canvases[0].transform : null;
    }

    private void SetRoomTexture(Texture texture)
    {
        if (texture != null && cameraManager != null)
        {
            cameraManager.SetRoomBackground(texture);
        }
    }

    private void SetOverlay(Color color, bool visible)
    {
        ResolveOverlay();

        if (blackoutPanel == null)
        {
            return;
        }

        blackoutPanel.SetActive(visible);

        if (blackoutOverlay != null)
        {
            blackoutOverlay.color = color;
        }

        if (blackoutGroup != null)
        {
            blackoutGroup.alpha = visible ? 1f : 0f;
            blackoutGroup.interactable = false;
            blackoutGroup.blocksRaycasts = visible;
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        SetOverlay(visible ? blackOutOverlay : Color.clear, visible);
    }

    private void SetLitFaceVisible(bool visible)
    {
        if (litFaceObject != null)
        {
            litFaceObject.SetActive(visible);
        }
    }

    private void SetPowerOutagePugVisible(bool visible)
    {
        if (powerOutagePugImage != null)
        {
            powerOutagePugImage.gameObject.SetActive(visible);
        }
    }

    private void SetPowerOutageStaticVisible(bool visible)
    {
        if (powerOutageStaticImage != null)
        {
            powerOutageStaticImage.gameObject.SetActive(visible);
        }
    }

    private void StopNightTimer()
    {
        NightTimer timer = FindAnyObjectByType<NightTimer>();

        if (timer != null)
        {
            timer.StopNight(true);
        }
    }

    private void StopSceneMotion()
    {
        if (stopAnimatorsOnPowerOut == null)
        {
            return;
        }

        foreach (Animator animator in stopAnimatorsOnPowerOut)
        {
            if (animator != null)
            {
                animator.speed = 0f;
            }
        }
    }

    private void DisableConfiguredObjects()
    {
        if (disableOnPowerOut == null)
        {
            return;
        }

        foreach (GameObject target in disableOnPowerOut)
        {
            if (target != null)
            {
                target.SetActive(false);
            }
        }
    }

    private void PlayIfAssigned(AudioSource source)
    {
        if (source != null)
        {
            source.Play();
        }
    }

    private void StopSequenceAudio()
    {
        StopIfAssigned(generatorSlowDie);
        StopIfAssigned(powerOutageMusic);
    }

    private void StopIfAssigned(AudioSource source)
    {
        if (source != null)
        {
            source.Stop();
        }
    }

    private IEnumerator WaitForAudioOrFallback(AudioSource source, float fallbackSeconds)
    {
        if (source != null && source.clip != null)
        {
            yield return new WaitForSecondsRealtime(source.clip.length);
        }
        else
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, fallbackSeconds));
        }
    }
}
