using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestFootstepAudio : MonoBehaviour
{
    private const string DefaultAudioObjectName = "Audio_GuestFootsteps";
    private const float DefaultGuestStepIntervalSeconds = 0.54f;
    private const float DefaultStepIntervalJitterSeconds = 0.025f;

    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.28f;
    [SerializeField, Min(0.05f)] private float stepIntervalSeconds = DefaultGuestStepIntervalSeconds;
    [SerializeField, Min(0f)] private float stepIntervalJitterSeconds = DefaultStepIntervalJitterSeconds;
    [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.01f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 9000f;
    [SerializeField] private string audioObjectName = DefaultAudioObjectName;

    private AudioSource source;
    private AudioHighPassFilter highPassFilter;
    private AudioLowPassFilter lowPassFilter;
    private bool isWalking;
    private float nextStepTime;
    private int lastClipIndex = -1;

    public void Configure(
        AudioClip nextClip,
        float nextBaseVolume,
        float nextHighPassCutoffFrequency,
        float nextHighPassResonanceQ,
        float nextLowPassCutoffFrequency = 9000f)
    {
        clip = nextClip;
        clips = nextClip != null ? new[] { nextClip } : Array.Empty<AudioClip>();
        baseVolume = Mathf.Clamp01(nextBaseVolume);
        highPassCutoffFrequency = Mathf.Max(10f, nextHighPassCutoffFrequency);
        highPassResonanceQ = Mathf.Clamp(nextHighPassResonanceQ, 0.1f, 10f);
        lowPassCutoffFrequency = Mathf.Max(10f, nextLowPassCutoffFrequency);
        ApplyConfiguration();
    }

    public void Configure(
        AudioClip[] nextClips,
        float nextBaseVolume,
        float nextHighPassCutoffFrequency,
        float nextHighPassResonanceQ,
        float nextStepIntervalSeconds,
        float nextStepIntervalJitterSeconds,
        float nextLowPassCutoffFrequency = 9000f)
    {
        clips = FilterClips(nextClips);
        clip = clips.Length > 0 ? clips[0] : null;
        baseVolume = Mathf.Clamp01(nextBaseVolume);
        highPassCutoffFrequency = Mathf.Max(10f, nextHighPassCutoffFrequency);
        highPassResonanceQ = Mathf.Clamp(nextHighPassResonanceQ, 0.1f, 10f);
        stepIntervalSeconds = Mathf.Max(0.05f, nextStepIntervalSeconds);
        stepIntervalJitterSeconds = Mathf.Max(0f, nextStepIntervalJitterSeconds);
        lowPassCutoffFrequency = Mathf.Max(10f, nextLowPassCutoffFrequency);
        lastClipIndex = -1;
        ApplyConfiguration();
    }

    public void PlayWalking()
    {
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

    public void SetWalking(bool walking, float movementSpeed = float.PositiveInfinity)
    {
        if (walking && movementSpeed >= minimumMovementSpeed)
        {
            PlayWalking();
        }
        else
        {
            StopWalking();
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
        StopWalking();
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

        source.PlayOneShot(selectedClip, 1f);
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
