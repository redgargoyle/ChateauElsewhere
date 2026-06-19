using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GuestFootstepCatalog", menuName = "Dreadforge/Audio/Guest Footstep Catalog")]
public sealed class GuestFootstepCatalog : ScriptableObject
{
#pragma warning disable 0649
    [Serializable]
    private struct GuestFootstepAssignment
    {
        public int guestNumber;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }
#pragma warning restore 0649

    [SerializeField] private GuestFootstepAssignment[] assignments = Array.Empty<GuestFootstepAssignment>();
    [SerializeField] private AudioClip butlerClip;
    [SerializeField, Range(0f, 1f)] private float butlerVolume = 0.24f;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.28f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 1.1f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 9000f;

    public bool TryGetFootstepsForGuest(
        int guestNumber,
        out AudioClip clip,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ)
    {
        clip = null;
        volume = defaultVolume;
        cutoffFrequency = Mathf.Max(10f, highPassCutoffFrequency);
        resonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);

        if (assignments == null || guestNumber <= 0)
        {
            return false;
        }

        for (int i = 0; i < assignments.Length; i++)
        {
            GuestFootstepAssignment assignment = assignments[i];

            if (assignment.guestNumber != guestNumber || assignment.clip == null)
            {
                continue;
            }

            clip = assignment.clip;
            volume = assignment.volume > 0f ? Mathf.Clamp01(assignment.volume) : defaultVolume;
            return true;
        }

        return false;
    }

    public bool TryGetFootstepsForButler(
        out AudioClip clip,
        out float volume,
        out float cutoffFrequency,
        out float resonanceQ,
        out float lowPassFrequency)
    {
        clip = butlerClip;
        volume = butlerVolume > 0f ? Mathf.Clamp01(butlerVolume) : defaultVolume;
        cutoffFrequency = Mathf.Max(10f, highPassCutoffFrequency);
        resonanceQ = Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
        lowPassFrequency = Mathf.Max(10f, lowPassCutoffFrequency);
        return clip != null;
    }
}
