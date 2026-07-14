using System;
using UnityEngine;

[Serializable]
public struct AudioClipVolumeAdjustment
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volumeMultiplier;

    public AudioClip Clip => clip;
    public float VolumeMultiplier => Mathf.Clamp01(volumeMultiplier);
}

public static class GameAudioSettings
{
    private const string PlayerPrefsPrefix = "Dreadforge.Audio.";
    private static readonly bool[] cached = new bool[AudioChannelCount];
    private static readonly float[] volumes = new float[AudioChannelCount];

    public static event Action<GameAudioChannel, float> VolumeChanged;

    private static int AudioChannelCount => Enum.GetValues(typeof(GameAudioChannel)).Length;

    public static void ResetUnityAudioState()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }

    public static float GetVolume(GameAudioChannel channel)
    {
        int index = GetIndex(channel);

        if (!cached[index])
        {
            volumes[index] = PlayerPrefs.GetFloat(GetPrefsKey(channel), 1f);
            cached[index] = true;
        }

        return Mathf.Clamp01(volumes[index]);
    }

    public static void SetVolume(GameAudioChannel channel, float value)
    {
        float normalized = Mathf.Clamp01(value);
        int index = GetIndex(channel);

        if (cached[index] && Mathf.Approximately(volumes[index], normalized))
        {
            return;
        }

        cached[index] = true;
        volumes[index] = normalized;
        PlayerPrefs.SetFloat(GetPrefsKey(channel), normalized);
        PlayerPrefs.Save();
        VolumeChanged?.Invoke(channel, normalized);
    }

    public static void ApplyVolume(AudioSource source, GameAudioChannel channel, float baseVolume)
    {
        if (source == null)
        {
            return;
        }

        ResetUnityAudioState();
        source.enabled = true;
        source.mute = false;
        source.ignoreListenerPause = false;
        source.ignoreListenerVolume = true;
        source.volume = Mathf.Max(0f, baseVolume) * GetVolume(channel);
    }

    public static bool CanPlay(AudioSource source)
    {
        return source != null &&
            source.enabled &&
            source.gameObject != null &&
            source.gameObject.activeInHierarchy;
    }

    public static bool TryPlay(AudioSource source)
    {
        if (!PrepareForPlayback(source, source != null ? source.clip : null) || source.clip == null)
        {
            return false;
        }

        source.Play();
        return true;
    }

    public static bool TryPlayOneShot(AudioSource source, AudioClip clip)
    {
        return TryPlayOneShot(source, clip, 1f);
    }

    public static bool TryPlayOneShot(AudioSource source, AudioClip clip, float volumeScale)
    {
        if (clip == null || !PrepareForPlayback(source, clip))
        {
            return false;
        }

        source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
        return true;
    }

    public static GameAudioSourceVolume EnsureBinding(AudioSource source, GameAudioChannel channel, float baseVolume)
    {
        if (source == null)
        {
            return null;
        }

        GameAudioSourceVolume binding = source.GetComponent<GameAudioSourceVolume>();

        if (binding == null)
        {
            binding = source.gameObject.AddComponent<GameAudioSourceVolume>();
        }

        binding.Configure(source, channel, baseVolume);
        return binding;
    }

    public static void EnsureSafetyFilters(
        AudioSource source,
        float highPassCutoffFrequency,
        float highPassResonanceQ,
        float lowPassCutoffFrequency,
        float lowPassResonanceQ,
        out AudioHighPassFilter highPassFilter,
        out AudioLowPassFilter lowPassFilter)
    {
        highPassFilter = null;
        lowPassFilter = null;

        if (source == null || source.gameObject == null)
        {
            return;
        }

        source.bypassEffects = false;
        highPassFilter = source.GetComponent<AudioHighPassFilter>();

        if (highPassFilter == null)
        {
            highPassFilter = source.gameObject.AddComponent<AudioHighPassFilter>();
        }

        lowPassFilter = source.GetComponent<AudioLowPassFilter>();

        if (lowPassFilter == null)
        {
            lowPassFilter = source.gameObject.AddComponent<AudioLowPassFilter>();
        }

        float safeHighPassCutoff = Mathf.Clamp(highPassCutoffFrequency, 10f, 22000f);
        float safeLowPassCutoff = Mathf.Clamp(lowPassCutoffFrequency, safeHighPassCutoff, 22000f);

        highPassFilter.enabled = true;
        highPassFilter.cutoffFrequency = safeHighPassCutoff;
        highPassFilter.highpassResonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
        lowPassFilter.enabled = true;
        lowPassFilter.cutoffFrequency = safeLowPassCutoff;
        lowPassFilter.lowpassResonanceQ = Mathf.Clamp(lowPassResonanceQ, 0.1f, 10f);
    }

    public static float GetClipVolumeMultiplier(
        AudioClip clip,
        AudioClipVolumeAdjustment[] adjustments)
    {
        if (clip == null || adjustments == null)
        {
            return 1f;
        }

        for (int i = 0; i < adjustments.Length; i++)
        {
            if (adjustments[i].Clip == clip)
            {
                return adjustments[i].VolumeMultiplier;
            }
        }

        return 1f;
    }

    public static float GetCurrentOrBoundBaseVolume(AudioSource source, GameAudioChannel channel)
    {
        if (source == null)
        {
            return 0f;
        }

        GameAudioSourceVolume binding = source.GetComponent<GameAudioSourceVolume>();

        if (binding != null && binding.Channel == channel)
        {
            return binding.BaseVolume;
        }

        return source.volume;
    }

    public static string GetDisplayName(GameAudioChannel channel)
    {
        switch (channel)
        {
            case GameAudioChannel.Dialogue:
                return "Dialogue";
            case GameAudioChannel.GameSounds:
                return "Game Sounds";
            case GameAudioChannel.Atmosphere:
                return "Atmosphere";
            case GameAudioChannel.Music:
                return "Music";
            default:
                return channel.ToString();
        }
    }

    private static int GetIndex(GameAudioChannel channel)
    {
        int index = (int)channel;
        return Mathf.Clamp(index, 0, AudioChannelCount - 1);
    }

    private static string GetPrefsKey(GameAudioChannel channel)
    {
        return PlayerPrefsPrefix + channel;
    }

    private static bool PrepareForPlayback(AudioSource source, AudioClip clip)
    {
        if (source == null)
        {
            return false;
        }

        ResetUnityAudioState();
        source.enabled = true;
        source.mute = false;
        source.ignoreListenerPause = false;
        source.spatialBlend = 0f;

        if (!EnsureClipLoaded(clip))
        {
            return false;
        }

        return CanPlay(source);
    }

    private static bool EnsureClipLoaded(AudioClip clip)
    {
        if (clip == null)
        {
            return true;
        }

        if (clip.loadState == AudioDataLoadState.Loaded)
        {
            return true;
        }

        if (clip.loadState == AudioDataLoadState.Loading)
        {
            return true;
        }

        return clip.LoadAudioData() || clip.loadState == AudioDataLoadState.Loaded;
    }
}
