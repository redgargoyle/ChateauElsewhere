using UnityEngine;

/// <summary>
/// The one Y-to-scale rule used by every Butler and guest animation display.
/// </summary>
public static class CharacterScaleFunction
{
    private const float MinimumScale = 0.0001f;

    public static float Evaluate(
        float characterRoomY,
        float frontY,
        float frontScale,
        float backY,
        float backScale)
    {
        float safeFrontScale = Mathf.Max(MinimumScale, frontScale);
        float safeBackScale = Mathf.Max(MinimumScale, backScale);

        if (Mathf.Approximately(frontY, backY))
        {
            return safeFrontScale;
        }

        float progressFromFrontToBack = Mathf.Clamp01(
            (characterRoomY - frontY) / (backY - frontY));
        return Mathf.Lerp(safeFrontScale, safeBackScale, progressFromFrontToBack);
    }
}
