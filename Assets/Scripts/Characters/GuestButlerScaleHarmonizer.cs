using System;
using UnityEngine;

[Obsolete("GuestButlerScaleHarmonizer has been replaced by GuestRoomScaleApplier.")]
[DisallowMultipleComponent]
public sealed class GuestButlerScaleHarmonizer : MonoBehaviour
{
    public void SetButlerScaleSource(PointClickPlayerMovement source)
    {
    }

    public void SetDebugGuestScaleMultiplier(float multiplier)
    {
    }

    public GuestScaleApplySummary RefreshNow()
    {
        return GuestScaleApplySummary.Empty;
    }

    [Serializable]
    public readonly struct GuestScaleApplySummary
    {
        public GuestScaleApplySummary(
            int projectedFound,
            int walkersFound,
            int actorStatesFound,
            int scaled,
            int skipped,
            int missingCalibration,
            float minScale,
            float maxScale,
            string sourceName)
        {
            ProjectedFound = projectedFound;
            WalkersFound = walkersFound;
            ActorStatesFound = actorStatesFound;
            Scaled = scaled;
            Skipped = skipped;
            MissingCalibration = missingCalibration;
            MinScale = minScale;
            MaxScale = maxScale;
            SourceName = sourceName;
        }

        public int ProjectedFound { get; }
        public int WalkersFound { get; }
        public int ActorStatesFound { get; }
        public int Scaled { get; }
        public int Skipped { get; }
        public int MissingCalibration { get; }
        public float MinScale { get; }
        public float MaxScale { get; }
        public string SourceName { get; }

        public static GuestScaleApplySummary Empty =>
            new GuestScaleApplySummary(0, 0, 0, 0, 0, 0, 0f, 0f, string.Empty);
    }
}
