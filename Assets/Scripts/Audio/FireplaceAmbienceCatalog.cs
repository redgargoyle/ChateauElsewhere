using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FireplaceAmbienceCatalog", menuName = "Dreadforge/Audio/Fireplace Ambience Catalog")]
public sealed class FireplaceAmbienceCatalog : ScriptableObject
{
    [SerializeField] private AudioClip[] clips = new AudioClip[0];
    [SerializeField] private string[] roomNames = new string[0];
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.42f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.65f;
    [SerializeField, Range(0.25f, 2f)] private float minPitch = 0.96f;
    [SerializeField, Range(0.25f, 2f)] private float maxPitch = 1.04f;

    public float BaseVolume => baseVolume;
    public float FadeSeconds => fadeSeconds;

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

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Replace("_", " ").ToLowerInvariant();
    }
}
