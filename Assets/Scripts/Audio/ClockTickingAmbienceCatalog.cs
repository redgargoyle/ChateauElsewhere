using UnityEngine;

[CreateAssetMenu(fileName = "ClockTickingAmbienceCatalog", menuName = "Dreadforge/Audio/Clock Ticking Ambience Catalog")]
public sealed class ClockTickingAmbienceCatalog : ScriptableObject
{
    [SerializeField] private AudioClip[] clips = new AudioClip[0];
    [SerializeField] private string[] roomNames = new string[0];
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.18f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;
    [SerializeField, Range(0.25f, 2f)] private float minPitch = 0.98f;
    [SerializeField, Range(0.25f, 2f)] private float maxPitch = 1.02f;

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

    public bool TryGetStableClipForRoom(string roomName, out AudioClip clip)
    {
        clip = null;

        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        int startIndex = GetStableClipIndex(roomName, clips.Length);

        for (int attempt = 0; attempt < clips.Length; attempt++)
        {
            int index = (startIndex + attempt) % clips.Length;

            if (clips[index] == null)
            {
                continue;
            }

            clip = clips[index];
            return true;
        }

        return false;
    }

    public float GetRandomPitch()
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);
        return UnityEngine.Random.Range(low, high);
    }

    public float GetStablePitchForRoom(string roomName)
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);

        if (Mathf.Approximately(low, high))
        {
            return low;
        }

        return Mathf.Lerp(low, high, GetStableUnitValue(roomName));
    }

    public float GetStablePlaybackTimeForRoom(string roomName, AudioClip clip, float elapsedSeconds)
    {
        if (clip == null || clip.length <= 0f)
        {
            return 0f;
        }

        float roomOffset = GetStableUnitValue(roomName) * clip.length;
        return Mathf.Repeat(roomOffset + Mathf.Max(0f, elapsedSeconds), clip.length);
    }

    private static int GetStableClipIndex(string roomName, int clipCount)
    {
        if (clipCount <= 1)
        {
            return 0;
        }

        return Mathf.Abs(GetStableHash(roomName)) % clipCount;
    }

    private static float GetStableUnitValue(string roomName)
    {
        int hash = GetStableHash(roomName);
        return (hash & 0x7fffffff) / (float)int.MaxValue;
    }

    private static int GetStableHash(string value)
    {
        string normalizedRoom = NormalizeRoomName(value);

        unchecked
        {
            int hash = 23;

            for (int i = 0; i < normalizedRoom.Length; i++)
            {
                hash = hash * 31 + normalizedRoom[i];
            }

            return hash == int.MinValue ? 0 : hash;
        }
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
