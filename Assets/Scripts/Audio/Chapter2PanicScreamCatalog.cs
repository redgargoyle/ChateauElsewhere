using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Chapter2PanicScreamCatalog", menuName = "Dreadforge/Audio/Chapter 2 Panic Scream Catalog")]
public sealed class Chapter2PanicScreamCatalog : ScriptableObject
{
    public const float MaximumSafeVolume = 0.08f;

#pragma warning disable 0649
    [Serializable]
    private struct GuestScreamAssignment
    {
        public int guestNumber;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }
#pragma warning restore 0649

    [SerializeField] private GuestScreamAssignment[] assignments = Array.Empty<GuestScreamAssignment>();
    [SerializeField, Range(0f, MaximumSafeVolume)] private float defaultVolume = 0.06f;
    [SerializeField, Min(10f)] private float highPassCutoffFrequency = 180f;
    [SerializeField, Range(0.1f, 10f)] private float highPassResonanceQ = 0.8f;
    [SerializeField, Min(10f)] private float lowPassCutoffFrequency = 6000f;
    [SerializeField, Range(0.1f, 10f)] private float lowPassResonanceQ = 0.8f;

    public float HighPassCutoffFrequency => Mathf.Clamp(highPassCutoffFrequency, 10f, 22000f);
    public float HighPassResonanceQ => Mathf.Clamp(highPassResonanceQ, 0.1f, 10f);
    public float LowPassCutoffFrequency => Mathf.Clamp(lowPassCutoffFrequency, HighPassCutoffFrequency, 22000f);
    public float LowPassResonanceQ => Mathf.Clamp(lowPassResonanceQ, 0.1f, 10f);

    public bool TryGetScreamForGuest(int guestNumber, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = ClampSafeVolume(defaultVolume);

        if (assignments == null || guestNumber <= 0)
        {
            return false;
        }

        for (int i = 0; i < assignments.Length; i++)
        {
            GuestScreamAssignment assignment = assignments[i];

            if (assignment.guestNumber != guestNumber || assignment.clip == null)
            {
                continue;
            }

            clip = assignment.clip;
            volume = ClampSafeVolume(assignment.volume > 0f ? assignment.volume : defaultVolume);
            return true;
        }

        return false;
    }

    public static float ClampSafeVolume(float volume)
    {
        return Mathf.Clamp(volume, 0f, MaximumSafeVolume);
    }
}
