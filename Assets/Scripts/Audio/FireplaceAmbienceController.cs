using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireplaceAmbienceController : MonoBehaviour
{
    private const string ControllerObjectName = "Audio_FireplaceAmbience";
    private const string DefaultCatalogResourcePath = "Audio/FireplaceAmbienceCatalog";

    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private FireplaceAmbienceCatalog catalog;
    [SerializeField] private string catalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private AudioSource audioSource;

    private int lastClipIndex = -1;
    private float activeBaseVolume;
    private Coroutine fadeRoutine;

    public static FireplaceAmbienceController FindOrCreate(RoomNavigationManager navigationManager)
    {
        FireplaceAmbienceController existing = FindAnyObjectByType<FireplaceAmbienceController>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.Initialize(navigationManager);
            return existing;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName, typeof(AudioSource), typeof(FireplaceAmbienceController));
        FireplaceAmbienceController controller = controllerObject.GetComponent<FireplaceAmbienceController>();
        controller.Initialize(navigationManager);
        return controller;
    }

    public void Initialize(RoomNavigationManager owner)
    {
        navigationManager = owner != null
            ? owner
            : FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);

        ResolveReferences();
        SubscribeToRoomChanges();

        if (navigationManager != null)
        {
            HandleRoomChanged(navigationManager.CurrentRoom);
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        GameAudioSettings.VolumeChanged += HandleAudioVolumeChanged;
        SubscribeToRoomChanges();

        if (navigationManager != null)
        {
            HandleRoomChanged(navigationManager.CurrentRoom);
        }
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumeChanged -= HandleAudioVolumeChanged;
        UnsubscribeFromRoomChanges();
    }

    private void ResolveReferences()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerVolume = true;

        if (catalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(catalogResourcePath)
                ? DefaultCatalogResourcePath
                : catalogResourcePath.Trim();

            catalog = Resources.Load<FireplaceAmbienceCatalog>(resourcePath);
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void SubscribeToRoomChanges()
    {
        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleRoomChanged);
        navigationManager.OnCurrentRoomChanged.AddListener(HandleRoomChanged);
    }

    private void UnsubscribeFromRoomChanges()
    {
        if (navigationManager != null)
        {
            navigationManager.OnCurrentRoomChanged.RemoveListener(HandleRoomChanged);
        }
    }

    private void HandleRoomChanged(string roomName)
    {
        ResolveReferences();

        if (catalog == null || audioSource == null || !catalog.HasRoom(roomName))
        {
            FadeOutAndStop();
            return;
        }

        if (!catalog.TryGetClipForRoom(roomName, ref lastClipIndex, out AudioClip clip))
        {
            FadeOutAndStop();
            return;
        }

        activeBaseVolume = catalog.BaseVolume * catalog.GetVolumeMultiplierForRoom(roomName);
        audioSource.clip = clip;
        audioSource.pitch = catalog.GetRandomPitch();
        audioSource.time = 0f;
        audioSource.volume = 0f;
        audioSource.Play();
        FadeTo(GameAudioSettings.GetVolume(GameAudioChannel.Atmosphere) * activeBaseVolume);
    }

    private void HandleAudioVolumeChanged(GameAudioChannel channel, float volume)
    {
        if (channel == GameAudioChannel.Atmosphere && audioSource != null && audioSource.isPlaying)
        {
            FadeTo(volume * activeBaseVolume);
        }
    }

    private void FadeOutAndStop()
    {
        if (audioSource == null)
        {
            return;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Stop();
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeVolume(audioSource.volume, 0f, GetFadeSeconds(), true));
    }

    private void FadeTo(float targetVolume)
    {
        if (audioSource == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeVolume(audioSource.volume, Mathf.Max(0f, targetVolume), GetFadeSeconds(), false));
    }

    private IEnumerator FadeVolume(float startVolume, float targetVolume, float duration, bool stopWhenDone)
    {
        if (audioSource == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            audioSource.volume = targetVolume;

            if (stopWhenDone)
            {
                audioSource.Stop();
            }

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration && audioSource != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (audioSource != null)
        {
            audioSource.volume = targetVolume;

            if (stopWhenDone)
            {
                audioSource.Stop();
            }
        }

        fadeRoutine = null;
    }

    private float GetFadeSeconds()
    {
        return catalog != null ? Mathf.Max(0f, catalog.FadeSeconds) : 0f;
    }
}
