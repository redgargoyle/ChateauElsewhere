using UnityEngine;

public enum CharacterPoseKind
{
    Standing,
    Seated,
    Crouching,
    Lying,
    Auto
}

public static class RoomHumanScaleService
{
    private const float DefaultSeatedRatio = 0.68f;
    private const float DefaultCrouchingRatio = 0.75f;
    private const float DefaultLyingRatio = 0.45f;

    public readonly struct HumanScaleSample
    {
        public HumanScaleSample(
            string roomId,
            Vector2 roomLocalFootPoint,
            float depth01,
            float normalizedStandingScale,
            float butlerFinalLocalScaleY,
            float butlerBaseLocalScaleY,
            string source)
        {
            RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            RoomLocalFootPoint = roomLocalFootPoint;
            Depth01 = Mathf.Clamp01(depth01);
            NormalizedStandingScale = Mathf.Max(0.001f, normalizedStandingScale);
            ButlerFinalLocalScaleY = Mathf.Max(0.001f, Mathf.Abs(butlerFinalLocalScaleY));
            ButlerBaseLocalScaleY = Mathf.Max(0.001f, Mathf.Abs(butlerBaseLocalScaleY));
            Source = string.IsNullOrWhiteSpace(source) ? "Butler room human scale calibration" : source;
        }

        public string RoomId { get; }
        public Vector2 RoomLocalFootPoint { get; }
        public float Depth01 { get; }
        public float NormalizedStandingScale { get; }
        public float ButlerFinalLocalScaleY { get; }
        public float ButlerBaseLocalScaleY { get; }
        public string Source { get; }
    }

    public static bool TryEvaluateStandingScale(
        PointClickPlayerMovement butlerSource,
        string roomId,
        Vector2 roomLocalFootPoint,
        out HumanScaleSample sample)
    {
        sample = default;

        if (butlerSource == null ||
            string.IsNullOrWhiteSpace(roomId) ||
            !butlerSource.TryEvaluateButlerCharacterScale(
                roomId,
                roomLocalFootPoint,
                out PointClickPlayerMovement.ButlerCharacterScaleSample butlerSample))
        {
            return false;
        }

        sample = new HumanScaleSample(
            butlerSample.RoomId,
            butlerSample.RoomLocalFootPoint,
            butlerSample.Depth01,
            butlerSample.NormalizedScale,
            butlerSample.ButlerFinalLocalScaleY,
            butlerSample.ButlerBaseLocalScaleY,
            butlerSample.Source);
        return true;
    }

    public static float GetPoseHeightRatio(
        CharacterPoseKind pose,
        CharacterVisualProfile visualProfile,
        ActorRoomState actorState)
    {
        CharacterPoseKind resolvedPose = pose;

        if (resolvedPose == CharacterPoseKind.Auto)
        {
            resolvedPose = actorState != null && actorState.IsSeated
                ? CharacterPoseKind.Seated
                : CharacterPoseKind.Standing;
        }

        switch (resolvedPose)
        {
            case CharacterPoseKind.Seated:
                if (visualProfile != null)
                {
                    return Mathf.Clamp(
                        visualProfile.SittingVisualHeight / Mathf.Max(1f, visualProfile.StandingVisualHeight),
                        0.55f,
                        0.8f);
                }

                return DefaultSeatedRatio;

            case CharacterPoseKind.Crouching:
                return DefaultCrouchingRatio;

            case CharacterPoseKind.Lying:
                return DefaultLyingRatio;

            case CharacterPoseKind.Standing:
            case CharacterPoseKind.Auto:
            default:
                return 1f;
        }
    }
}
