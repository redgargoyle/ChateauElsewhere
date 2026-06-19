using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    private const string DefaultAudioObjectName = "Audio_ButlerFootsteps";
    private const string DefaultCatalogResourcePath = "Audio/GuestFootstepCatalog";

    [SerializeField] private GuestFootstepCatalog footstepCatalog;
    [SerializeField] private string footstepCatalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.24f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 9000f;
    [SerializeField] private string audioObjectName = DefaultAudioObjectName;

    private AudioSource source;
    private AudioHighPassFilter highPassFilter;
    private AudioLowPassFilter lowPassFilter;
    private bool isWalking;

    public void SetWalking(bool walking)
    {
        if (isWalking == walking)
        {
            if (isWalking && (source == null || !source.isPlaying))
            {
                PlayWalking();
            }

            return;
        }

        isWalking = walking;

        if (isWalking)
        {
            PlayWalking();
        }
        else
        {
            StopWalking();
        }
    }

    public void PlayWalking()
    {
        ResolveCatalogClip();

        if (clip == null || !EnsureAudioSource())
        {
            return;
        }

        ApplyConfiguration();

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void StopWalking()
    {
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
    }

    private void OnDisable()
    {
        isWalking = false;
        StopWalking();
    }

    private void ResolveCatalogClip()
    {
        if (clip != null)
        {
            return;
        }

        if (footstepCatalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(footstepCatalogResourcePath)
                ? DefaultCatalogResourcePath
                : footstepCatalogResourcePath.Trim();
            footstepCatalog = Resources.Load<GuestFootstepCatalog>(resourcePath);
        }

        if (footstepCatalog != null &&
            footstepCatalog.TryGetFootstepsForButler(
                out AudioClip catalogClip,
                out float catalogVolume,
                out float catalogHighPassCutoffFrequency,
                out float catalogHighPassResonanceQ,
                out float catalogLowPassCutoffFrequency))
        {
            clip = catalogClip;
            baseVolume = catalogVolume;
            highPassCutoffFrequency = catalogHighPassCutoffFrequency;
            highPassResonanceQ = catalogHighPassResonanceQ;
            lowPassCutoffFrequency = catalogLowPassCutoffFrequency;
        }
    }

    private void ApplyConfiguration()
    {
        if (clip == null || !EnsureAudioSource())
        {
            return;
        }

        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerVolume = true;

        if (highPassFilter != null)
        {
            highPassFilter.cutoffFrequency = Mathf.Max(10f, highPassCutoffFrequency);
            highPassFilter.highpassResonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
        }

        if (lowPassFilter != null)
        {
            lowPassFilter.cutoffFrequency = Mathf.Max(10f, lowPassCutoffFrequency);
            lowPassFilter.lowpassResonanceQ = 1f;
        }

        GameAudioSettings.EnsureBinding(source, GameAudioChannel.GameSounds, baseVolume);
    }

    private bool EnsureAudioSource()
    {
        if (source != null && highPassFilter != null && lowPassFilter != null)
        {
            return true;
        }

        string cleanName = string.IsNullOrWhiteSpace(audioObjectName)
            ? DefaultAudioObjectName
            : audioObjectName.Trim();
        Transform existing = transform.Find(cleanName);
        GameObject audioObject = existing != null ? existing.gameObject : null;

        if (audioObject == null)
        {
            audioObject = new GameObject(cleanName);
            audioObject.transform.SetParent(transform, false);
        }

        source = audioObject.GetComponent<AudioSource>();

        if (source == null)
        {
            source = audioObject.AddComponent<AudioSource>();
        }

        highPassFilter = audioObject.GetComponent<AudioHighPassFilter>();

        if (highPassFilter == null)
        {
            highPassFilter = audioObject.AddComponent<AudioHighPassFilter>();
        }

        lowPassFilter = audioObject.GetComponent<AudioLowPassFilter>();

        if (lowPassFilter == null)
        {
            lowPassFilter = audioObject.AddComponent<AudioLowPassFilter>();
        }

        return source != null;
    }
}
