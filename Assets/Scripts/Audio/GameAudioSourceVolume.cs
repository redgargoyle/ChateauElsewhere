using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameAudioSourceVolume : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameAudioChannel channel = GameAudioChannel.GameSounds;
    [SerializeField, Min(0f)] private float baseVolume = 1f;

    public GameAudioChannel Channel => channel;
    public float BaseVolume => baseVolume;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            baseVolume = audioSource.volume;
        }
    }

    private void OnEnable()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        GameAudioSettings.VolumeChanged += HandleVolumeChanged;
        Apply();
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumeChanged -= HandleVolumeChanged;
    }

    public void Configure(AudioSource source, GameAudioChannel audioChannel, float sourceBaseVolume)
    {
        audioSource = source != null ? source : GetComponent<AudioSource>();
        channel = audioChannel;
        baseVolume = Mathf.Max(0f, sourceBaseVolume);
        Apply();
    }

    public void SetBaseVolume(float sourceBaseVolume)
    {
        baseVolume = Mathf.Max(0f, sourceBaseVolume);
        Apply();
    }

    public void Apply()
    {
        GameAudioSettings.ApplyVolume(audioSource, channel, baseVolume);
    }

    private void HandleVolumeChanged(GameAudioChannel changedChannel, float volume)
    {
        if (changedChannel == channel)
        {
            Apply();
        }
    }
}
