using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GuestVoiceLineCatalog", menuName = "Dreadforge/Audio/Guest Voice Line Catalog")]
public sealed class GuestVoiceLineCatalog : ScriptableObject
{
#pragma warning disable 0649
    [Serializable]
    private struct VoiceLineAssignment
    {
        public string lineId;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }
#pragma warning restore 0649

    [SerializeField] private VoiceLineAssignment[] assignments = Array.Empty<VoiceLineAssignment>();
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;

    public bool TryGetVoiceLine(string lineId, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = defaultVolume;

        if (assignments == null || string.IsNullOrWhiteSpace(lineId))
        {
            return false;
        }

        string cleanLineId = lineId.Trim();

        for (int i = 0; i < assignments.Length; i++)
        {
            VoiceLineAssignment assignment = assignments[i];

            if (assignment.clip == null ||
                string.IsNullOrWhiteSpace(assignment.lineId) ||
                !string.Equals(assignment.lineId.Trim(), cleanLineId, StringComparison.OrdinalIgnoreCase))
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
