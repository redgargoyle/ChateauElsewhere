using UnityEngine;

[CreateAssetMenu(fileName = "DoorOpenSoundCatalog", menuName = "Dreadforge/Audio/Door Open Sound Catalog")]
public class DoorOpenSoundCatalog : ScriptableObject
{
    [SerializeField] private AudioClip[] clips = new AudioClip[0];

    public int ClipCount => clips != null ? clips.Length : 0;

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
