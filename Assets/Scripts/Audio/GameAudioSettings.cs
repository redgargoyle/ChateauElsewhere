using System;
using UnityEngine;

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
}
