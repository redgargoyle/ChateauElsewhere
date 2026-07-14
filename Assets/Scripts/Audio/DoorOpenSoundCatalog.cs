using UnityEngine;

[CreateAssetMenu(fileName = "DoorOpenSoundCatalog", menuName = "Dreadforge/Audio/Door Open Sound Catalog")]
public class DoorOpenSoundCatalog : ScriptableObject
{
    [SerializeField] private AudioClip[] clips = new AudioClip[0];
    [SerializeField] private AudioClipVolumeAdjustment[] clipVolumeAdjustments = new AudioClipVolumeAdjustment[0];
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.36f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 7800f;
    [SerializeField, Range(0.1f, 10f)] private float lowPassResonanceQ = 1f;

    public int ClipCount => clips != null ? clips.Length : 0;

    public float GetClipVolumeMultiplier(AudioClip clip)
    {
        return GameAudioSettings.GetClipVolumeMultiplier(clip, clipVolumeAdjustments);
    }

    public void ApplyMixTo(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        GameAudioSettings.EnsureBinding(source, GameAudioChannel.GameSounds, Mathf.Clamp01(baseVolume));
        GameAudioSettings.EnsureSafetyFilters(
            source,
            highPassCutoffFrequency,
            highPassResonanceQ,
            lowPassCutoffFrequency,
            lowPassResonanceQ,
            out _,
            out _);
    }

    public bool TryGetRandomClip(ref int lastClipIndex, out AudioClip clip)
    {
        clip = null;

        if (clips == null || clips.Length == 0)
        {
            lastClipIndex = -1;
            return false;
        }

        if (clips.Length == 1)
        {
            lastClipIndex = 0;
            clip = clips[0];
            return clip != null;
        }

        for (int attempt = 0; attempt < clips.Length; attempt++)
        {
            int index = Random.Range(0, clips.Length);

            if (index == lastClipIndex)
            {
                index = (index + Random.Range(1, clips.Length)) % clips.Length;
            }

            if (clips[index] == null)
            {
                continue;
            }

            lastClipIndex = index;
            clip = clips[index];
            return true;
        }

        lastClipIndex = -1;
        return false;
    }
}
