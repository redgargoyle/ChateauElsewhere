using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FireplaceAmbienceCatalog", menuName = "Dreadforge/Audio/Fireplace Ambience Catalog")]
public sealed class FireplaceAmbienceCatalog : ScriptableObject
{
#pragma warning disable 0649
    [Serializable]
    private struct RoomClipAssignment
    {
        public string roomName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volumeMultiplier;
    }
#pragma warning restore 0649

    [SerializeField] private AudioClip[] clips = new AudioClip[0];
    [SerializeField] private AudioClipVolumeAdjustment[] clipVolumeAdjustments = new AudioClipVolumeAdjustment[0];
    [SerializeField] private string[] roomNames = new string[0];
    [SerializeField] private RoomClipAssignment[] roomClipAssignments = new RoomClipAssignment[0];
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.42f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.65f;
    [SerializeField, Range(0.25f, 2f)] private float minPitch = 0.96f;
    [SerializeField, Range(0.25f, 2f)] private float maxPitch = 1.04f;

    public float BaseVolume => baseVolume;
    public float FadeSeconds => fadeSeconds;

    public float GetClipVolumeMultiplier(AudioClip clip)
    {
        return GameAudioSettings.GetClipVolumeMultiplier(clip, clipVolumeAdjustments);
    }

    public bool HasRoom(string roomName)
    {
        string normalizedRoom = NormalizeRoomName(roomName);

        if (string.IsNullOrEmpty(normalizedRoom) || roomNames == null)
        {
            return false;
        }

        for (int i = 0; i < roomNames.Length; i++)
        {
            if (NormalizeRoomName(roomNames[i]) == normalizedRoom)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetClipForRoom(string roomName, ref int lastClipIndex, out AudioClip clip)
    {
        if (TryGetAssignedRoomClip(roomName, out clip))
        {
            return true;
        }

        return TryGetRandomClip(ref lastClipIndex, out clip);
    }

    public float GetVolumeMultiplierForRoom(string roomName)
    {
        if (TryGetRoomClipAssignment(roomName, out RoomClipAssignment assignment))
        {
            return assignment.volumeMultiplier > 0f ? Mathf.Clamp01(assignment.volumeMultiplier) : 1f;
        }

        return 1f;
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
            int index = UnityEngine.Random.Range(0, clips.Length);

            if (index == lastClipIndex)
            {
                index = (index + UnityEngine.Random.Range(1, clips.Length)) % clips.Length;
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

    public float GetRandomPitch()
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);
        return UnityEngine.Random.Range(low, high);
    }

    private bool TryGetAssignedRoomClip(string roomName, out AudioClip clip)
    {
        clip = null;

        if (TryGetRoomClipAssignment(roomName, out RoomClipAssignment assignment) && assignment.clip != null)
        {
            clip = assignment.clip;
            return true;
        }

        return false;
    }

    private bool TryGetRoomClipAssignment(string roomName, out RoomClipAssignment assignment)
    {
        assignment = default;
        string normalizedRoom = NormalizeRoomName(roomName);

        if (string.IsNullOrEmpty(normalizedRoom) || roomClipAssignments == null)
        {
            return false;
        }

        for (int i = 0; i < roomClipAssignments.Length; i++)
        {
            RoomClipAssignment candidate = roomClipAssignments[i];

            if (NormalizeRoomName(candidate.roomName) == normalizedRoom)
            {
                assignment = candidate;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Replace("_", " ").ToLowerInvariant();
    }
}
