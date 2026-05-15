using UnityEngine;

[CreateAssetMenu(menuName = "Noise/Static Frame Group", fileName = "SFG_StaticFrames")]
public class StaticFrameGroup : ScriptableObject
{
    public string groupName = "Static Frames";

    [Tooltip("Individual static frames for this group.")]
    public Texture2D[] frames;

    [Tooltip("How long each frame stays on screen.")]
    [Min(0.001f)]
    public float frameDuration = 0.05f;

    [Tooltip("Randomize frame order each pass.")]
    public bool shuffle = true;

    [Header("Noise Intensity")]
    [Tooltip("Lowest overlay opacity this group can use.")]
    [Range(0f, 1f)]
    public float minIntensity = 0.65f;

    [Tooltip("Highest overlay opacity this group can use.")]
    [Range(0f, 1f)]
    public float maxIntensity = 1f;

    [Tooltip("Pick a new random opacity for each frame.")]
    public bool jitterIntensity = true;

    public bool IsValid()
    {
        return frames != null && frames.Length > 0;
    }

    public bool isValid()
    {
        return IsValid();
    }

    public Texture2D GetFrame(int index)
    {
        if (!IsValid())
        {
            return null;
        }

        int safeIndex = Mathf.Abs(index) % frames.Length;
        return frames[safeIndex];
    }

    public float GetIntensity()
    {
        float low = Mathf.Min(minIntensity, maxIntensity);
        float high = Mathf.Max(minIntensity, maxIntensity);

        if (!jitterIntensity)
        {
            return high;
        }

        return Random.Range(low, high);
    }

    private void OnValidate()
    {
        frameDuration = Mathf.Max(0.001f, frameDuration);
    }
}
