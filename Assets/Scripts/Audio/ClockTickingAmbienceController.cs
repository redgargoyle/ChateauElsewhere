using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClockTickingAmbienceController : MonoBehaviour
{
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private ClockTickingAmbienceCatalog catalog;
    [SerializeField] private AudioSource audioSource;

    private Coroutine fadeRoutine;
    private string currentRoomKey = string.Empty;

    public void Initialize(RoomNavigationManager owner)
    {
        if (owner != null)
        {
            navigationManager = owner;
        }

        ConfigureAudioSource();
        SubscribeToRoomChanges();

        if (navigationManager != null)
        {
            HandleRoomChanged(navigationManager.CurrentRoom);
        }
    }

    private void Awake()
    {
        ConfigureAudioSource();
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

    private void ConfigureAudioSource()
    {
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerVolume = true;
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
        ConfigureAudioSource();

        string roomKey = NormalizeRoomKey(roomName);

        if (catalog == null || audioSource == null || !catalog.HasRoom(roomName))
        {
            currentRoomKey = string.Empty;
            FadeOutAndStop();
            return;
        }

        if (!catalog.TryGetClockForRoom(roomName, out AudioClip clip, out float pitch))
        {
            currentRoomKey = string.Empty;
            FadeOutAndStop();
            return;
        }

        if (audioSource.isPlaying &&
            audioSource.clip == clip &&
            string.Equals(currentRoomKey, roomKey, System.StringComparison.Ordinal))
        {
            FadeTo(GameAudioSettings.GetVolume(GameAudioChannel.Atmosphere) * catalog.BaseVolume);
            return;
        }

        currentRoomKey = roomKey;
        audioSource.clip = clip;
        audioSource.pitch = pitch;
        audioSource.time = catalog.GetStablePlaybackTimeForRoom(roomName, clip, Time.unscaledTime);
        audioSource.volume = 0f;
        if (!GameAudioSettings.TryPlay(audioSource))
        {
            return;
        }

        FadeTo(GameAudioSettings.GetVolume(GameAudioChannel.Atmosphere) * catalog.BaseVolume);
    }

    private void HandleAudioVolumeChanged(GameAudioChannel channel, float volume)
    {
        if (channel == GameAudioChannel.Atmosphere && audioSource != null && audioSource.isPlaying)
        {
            FadeTo(volume * (catalog != null ? catalog.BaseVolume : 0f));
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

    private static string NormalizeRoomKey(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            return string.Empty;
        }

        return roomName.Trim().Replace("_", " ").ToLowerInvariant();
    }
}
