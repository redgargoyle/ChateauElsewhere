using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class JumpScareSequence : MonoBehaviour
{
    public RawImage scareImage;
    public Texture2D[] frames;

    public float frameRate = 30f;
    public int repeat = 1;
    public float minimumDuration = 1.6f;
    public Animator camera_shake;
    public bool shakeDuringScare = true;
    public RectTransform shakeTarget;
    public float shakeMagnitude = 28f;
    public float shakeRotation = 2f;
    public float shakeFrequency = 52f;
    public float shakeZoom = 1.05f;
    public AudioSource audioSource;
    public GameOverController gameOverController;
    public bool triggerGameOverWhenFinished = true;
    public bool stretchToScreen = true;
    public GameObject[] gameplayUiToHide;

    private bool hasPlayed = false;
    private Coroutine playRoutine;
    private Coroutine shakeRoutine;
    private RectTransform activeShakeTarget;
    private Vector2 shakeBasePosition;
    private Quaternion shakeBaseRotation;
    private Vector3 shakeBaseScale;
    private bool[] gameplayUiWasActive;

    private void Reset()
    {
        scareImage = GetComponentInChildren<RawImage>(true);
        audioSource = GetComponent<AudioSource>();
        camera_shake = GetComponent<Animator>();
        shakeTarget = transform as RectTransform;
        gameOverController = FindObjectOfType<GameOverController>();
    }

    private void Awake()
    {
        if (scareImage == null)
        {
            scareImage = GetComponentInChildren<RawImage>(true);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (gameOverController == null)
        {
            gameOverController = FindObjectOfType<GameOverController>();
        }

        if (shakeTarget == null)
        {
            shakeTarget = transform as RectTransform;
        }

        ApplyResponsiveLayout();
        SetScareVisible(false);
    }

    public void Play()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (hasPlayed)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlaySequence());
    }

    public void TriggerJumpScare()
    {
        Play();
    }

    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        hasPlayed = false;
        StopShake();
        SetScareVisible(false);
        RestoreGameplayUi();
    }

    private IEnumerator PlaySequence()
    {
        hasPlayed = true;
        HideGameplayUi();
        CameraManager cameraManager = CancelCameraTransitions();
        ApplyResponsiveLayout();

        if (scareImage == null)
        {
            Debug.LogWarning("JumpScareSequence needs a RawImage assigned.", this);
            FinishSequence();
            yield break;
        }

        SetScareVisible(true);
        transform.SetAsLastSibling();
        scareImage.transform.SetAsLastSibling();

        float delay = 1f / Mathf.Max(1f, frameRate);
        int safeRepeat = Mathf.Max(1, repeat);
        float sequenceStart = Time.unscaledTime;
        float targetDuration = Mathf.Max(0f, minimumDuration, delay * GetFrameCount() * safeRepeat);

        if (audioSource != null)
        {
            audioSource.Play();
        }

        StartShake(cameraManager, targetDuration);

        int completedRepeats = 0;

        while (completedRepeats < safeRepeat || Time.unscaledTime - sequenceStart < targetDuration)
        {
            if (frames != null && frames.Length > 0)
            {
                foreach (Texture2D frame in frames)
                {
                    if (frame != null)
                    {
                        scareImage.texture = frame;
                    }

                    yield return new WaitForSecondsRealtime(delay);
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            completedRepeats++;
        }

        FinishSequence();
    }

    private void FinishSequence()
    {
        playRoutine = null;
        hasPlayed = false;
        StopShake();
        SetScareVisible(false);

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (triggerGameOverWhenFinished && gameOverController != null)
        {
            gameOverController.StartGameOver();
        }
        else
        {
            RestoreGameplayUi();
        }
    }

    private int GetFrameCount()
    {
        return frames != null && frames.Length > 0 ? frames.Length : 1;
    }

    private void StartShake(CameraManager cameraManager, float duration)
    {
        if (camera_shake != null)
        {
            camera_shake.SetTrigger("Shake");
        }

        if (!shakeDuringScare)
        {
            return;
        }

        if (cameraManager != null)
        {
            cameraManager.PlayCameraShake(duration, shakeMagnitude, shakeRotation, shakeFrequency, shakeZoom);
        }

        RectTransform target = shakeTarget;

        if (target == null && scareImage != null)
        {
            target = scareImage.rectTransform;
        }

        if (target == null)
        {
            return;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreShakeTarget();
        }

        shakeRoutine = StartCoroutine(ShakeRect(target, duration));
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        RestoreShakeTarget();

        CameraManager cameraManager = FindObjectOfType<CameraManager>();

        if (cameraManager != null)
        {
            cameraManager.StopCameraShake();
        }
    }

    private IEnumerator ShakeRect(RectTransform target, float duration)
    {
        activeShakeTarget = target;
        shakeBasePosition = target.anchoredPosition;
        shakeBaseRotation = target.localRotation;
        shakeBaseScale = target.localScale;

        float safeDuration = Mathf.Max(0.01f, duration);
        float safeFrequency = Mathf.Max(0.01f, shakeFrequency);
        float safeZoom = Mathf.Max(1f, shakeZoom);
        float seed = Random.value * 100f;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float strength = 1f - Mathf.Clamp01(elapsed / safeDuration);
            float noiseTime = Time.unscaledTime * safeFrequency;
            float offsetX = (Mathf.PerlinNoise(seed, noiseTime) * 2f - 1f) * shakeMagnitude * strength;
            float offsetY = (Mathf.PerlinNoise(seed + 19f, noiseTime) * 2f - 1f) * shakeMagnitude * strength;
            float angle = (Mathf.PerlinNoise(seed + 43f, noiseTime) * 2f - 1f) * shakeRotation * strength;

            target.anchoredPosition = shakeBasePosition + new Vector2(offsetX, offsetY);
            target.localRotation = shakeBaseRotation * Quaternion.Euler(0f, 0f, angle);
            target.localScale = shakeBaseScale * Mathf.Lerp(1f, safeZoom, strength);

            yield return null;
        }

        RestoreShakeTarget();
        shakeRoutine = null;
    }

    private void RestoreShakeTarget()
    {
        if (activeShakeTarget == null)
        {
            return;
        }

        activeShakeTarget.anchoredPosition = shakeBasePosition;
        activeShakeTarget.localRotation = shakeBaseRotation;
        activeShakeTarget.localScale = shakeBaseScale;
        activeShakeTarget = null;
    }

    private void SetScareVisible(bool visible)
    {
        if (scareImage != null)
        {
            scareImage.gameObject.SetActive(visible);
            scareImage.raycastTarget = false;
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!stretchToScreen)
        {
            return;
        }

        Stretch(transform as RectTransform);

        if (scareImage != null)
        {
            Stretch(scareImage.rectTransform);
        }
    }

    private void Stretch(RectTransform rectTransform)
    {
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

    private void HideGameplayUi()
    {
        if (gameplayUiToHide == null)
        {
            return;
        }

        gameplayUiWasActive = new bool[gameplayUiToHide.Length];

        for (int i = 0; i < gameplayUiToHide.Length; i++)
        {
            GameObject item = gameplayUiToHide[i];
            gameplayUiWasActive[i] = item != null && item.activeSelf;

            if (item != null)
            {
                item.SetActive(false);
            }
        }
    }

    private void RestoreGameplayUi()
    {
        if (gameplayUiToHide == null || gameplayUiWasActive == null)
        {
            return;
        }

        for (int i = 0; i < gameplayUiToHide.Length; i++)
        {
            if (gameplayUiToHide[i] != null)
            {
                gameplayUiToHide[i].SetActive(gameplayUiWasActive[i]);
            }
        }
    }

    private CameraManager CancelCameraTransitions()
    {
        CameraManager cameraManager = FindObjectOfType<CameraManager>();

        if (cameraManager != null)
        {
            cameraManager.CancelCameraSwitchTransition();
        }

        return cameraManager;
    }
}
