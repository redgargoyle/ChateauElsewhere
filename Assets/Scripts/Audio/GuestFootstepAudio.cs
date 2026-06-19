using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestFootstepAudio : MonoBehaviour
{
    private const string DefaultAudioObjectName = "Audio_GuestFootsteps";

    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.28f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField] private string audioObjectName = DefaultAudioObjectName;

    private AudioSource source;
    private AudioHighPassFilter highPassFilter;

    public void Configure(
        AudioClip nextClip,
        float nextBaseVolume,
        float nextHighPassCutoffFrequency,
        float nextHighPassResonanceQ)
    {
        clip = nextClip;
        baseVolume = Mathf.Clamp01(nextBaseVolume);
        highPassCutoffFrequency = Mathf.Max(10f, nextHighPassCutoffFrequency);
        highPassResonanceQ = Mathf.Clamp(nextHighPassResonanceQ, 0.1f, 10f);
        ApplyConfiguration();
    }

    public void PlayWalking()
    {
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
        StopWalking();
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

        GameAudioSettings.EnsureBinding(source, GameAudioChannel.GameSounds, baseVolume);
    }

    private bool EnsureAudioSource()
    {
        if (source != null && highPassFilter != null)
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

        return source != null;
    }
}
