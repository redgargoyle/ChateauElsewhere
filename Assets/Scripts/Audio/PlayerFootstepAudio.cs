using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    private const string DefaultAudioObjectName = "Audio_ButlerFootsteps";
    private const string DefaultCatalogResourcePath = "Audio/GuestFootstepCatalog";
    private const float DefaultButlerStepIntervalSeconds = 0.6f;
    private const float DefaultStepIntervalJitterSeconds = 0.025f;

    [SerializeField] private GuestFootstepCatalog footstepCatalog;
    [SerializeField] private string footstepCatalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.24f;
    [SerializeField, Min(0.05f)] private float stepIntervalSeconds = DefaultButlerStepIntervalSeconds;
    [SerializeField, Min(0f)] private float stepIntervalJitterSeconds = DefaultStepIntervalJitterSeconds;
    [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.01f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 200f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 9000f;
    [SerializeField] private string audioObjectName = DefaultAudioObjectName;

    private AudioSource source;
    private AudioHighPassFilter highPassFilter;
    private AudioLowPassFilter lowPassFilter;
    private bool isWalking;
    private float nextStepTime;
    private int lastClipIndex = -1;

    public void SetWalking(bool walking, float movementSpeed = float.PositiveInfinity)
    {
        bool shouldWalk = walking && movementSpeed >= minimumMovementSpeed;
        if (isWalking == shouldWalk)
        {
            if (isWalking && Time.time >= nextStepTime)
            {
                PlayWalking();
            }

            return;
        }

        isWalking = shouldWalk;

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

        if (!HasClips() || !EnsureAudioSource())
        {
            return;
        }

        ApplyConfiguration();
        isWalking = true;

        if (Time.time >= nextStepTime)
        {
            PlayStep();
        }
    }

    public void StopWalking()
    {
        isWalking = false;
        nextStepTime = 0f;

        if (source != null)
        {
            source.Stop();
        }
    }

    private void Update()
    {
        if (!isWalking || !HasClips() || Time.time < nextStepTime)
        {
            return;
        }

        PlayStep();
    }

    private void OnDisable()
    {
        isWalking = false;
        StopWalking();
    }

    private void ResolveCatalogClip()
    {
        if ((clips != null && clips.Length > 0) || clip != null)
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
            footstepCatalog.TryGetButlerFootstepVariants(
                out AudioClip[] catalogClips,
                out float catalogVolume,
                out float catalogHighPassCutoffFrequency,
                out float catalogHighPassResonanceQ,
                out float catalogLowPassCutoffFrequency,
                out float catalogStepInterval,
                out float catalogStepJitter))
        {
            clips = FilterClips(catalogClips);
            clip = clips.Length > 0 ? clips[0] : null;
            baseVolume = catalogVolume;
            highPassCutoffFrequency = catalogHighPassCutoffFrequency;
            highPassResonanceQ = catalogHighPassResonanceQ;
            lowPassCutoffFrequency = catalogLowPassCutoffFrequency;
            stepIntervalSeconds = catalogStepInterval;
            stepIntervalJitterSeconds = catalogStepJitter;
        }
    }

    private void ApplyConfiguration()
    {
        if (!HasClips() || !EnsureAudioSource())
        {
            return;
        }

        source.clip = null;
        source.playOnAwake = false;
        source.loop = false;
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

    private void PlayStep()
    {
        AudioClip selectedClip = SelectClip();
        if (selectedClip == null || source == null)
        {
            StopWalking();
            return;
        }

        if (!GameAudioSettings.TryPlayOneShot(source, selectedClip, 1f))
        {
            StopWalking();
            return;
        }

        float delay = Mathf.Max(stepIntervalSeconds, selectedClip.length * 0.85f);
        float jitter = stepIntervalJitterSeconds > 0f
            ? UnityEngine.Random.Range(-stepIntervalJitterSeconds, stepIntervalJitterSeconds)
            : 0f;
        nextStepTime = Time.time + Mathf.Max(0.05f, delay + jitter);
    }

    private AudioClip SelectClip()
    {
        AudioClip[] variants = GetClipVariants();
        if (variants.Length == 0)
        {
            return null;
        }

        if (variants.Length == 1)
        {
            lastClipIndex = 0;
            return variants[0];
        }

        int index = UnityEngine.Random.Range(0, variants.Length);
        if (index == lastClipIndex)
        {
            index = (index + UnityEngine.Random.Range(1, variants.Length)) % variants.Length;
        }

        lastClipIndex = index;
        return variants[index];
    }

    private bool HasClips()
    {
        return GetClipVariants().Length > 0;
    }

    private AudioClip[] GetClipVariants()
    {
        if (clips != null && clips.Length > 0)
        {
            return clips;
        }

        return clip != null ? new[] { clip } : Array.Empty<AudioClip>();
    }

    private static AudioClip[] FilterClips(AudioClip[] nextClips)
    {
        if (nextClips == null || nextClips.Length == 0)
        {
            return Array.Empty<AudioClip>();
        }

        int validCount = 0;
        for (int i = 0; i < nextClips.Length; i++)
        {
            if (nextClips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return Array.Empty<AudioClip>();
        }

        AudioClip[] filtered = new AudioClip[validCount];
        int outputIndex = 0;
        for (int i = 0; i < nextClips.Length; i++)
        {
            if (nextClips[i] != null)
            {
                filtered[outputIndex] = nextClips[i];
                outputIndex++;
            }
        }

        return filtered;
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
