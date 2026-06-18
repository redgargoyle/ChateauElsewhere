using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Chapter2PanicScreamCatalog", menuName = "Dreadforge/Audio/Chapter 2 Panic Scream Catalog")]
public sealed class Chapter2PanicScreamCatalog : ScriptableObject
{
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
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.9f;

    public bool TryGetScreamForGuest(int guestNumber, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = defaultVolume;

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
            volume = assignment.volume > 0f ? Mathf.Clamp01(assignment.volume) : defaultVolume;
            return true;
        }

        return false;
    }
}
