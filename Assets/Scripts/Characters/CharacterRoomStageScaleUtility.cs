using UnityEngine;

/// <summary>
/// Compensates only for the existing room-stage zoom so catalog sizes remain visually stable.
/// This helper never writes transforms or changes gameplay state.
/// </summary>
public static class CharacterRoomStageScaleUtility
{
    private const float MinimumStageScale = 0.0001f;

    public static bool TryGetActiveRoomStageScale(out float stageScale)
    {
        stageScale = 1f;
        CameraManager cameraManager = Object.FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);

        if (cameraManager != null &&
            cameraManager.TryGetRoomStageScreenTransform(out _, out _, out float cameraStageScale))
        {
            stageScale = SanitizeStageScale(cameraStageScale);
            return true;
        }

        RoomContentGroup[] roomStages =
            Object.FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomStages.Length; i++)
        {
            RoomContentGroup roomStage = roomStages[i];

            if (roomStage == null || !roomStage.gameObject.activeInHierarchy)
            {
                continue;
            }

            stageScale = SanitizeStageScale(roomStage.transform.lossyScale.x);
            return true;
        }

        return false;
    }

    public static float GetCurrentZoomRatio(
        CharacterRoomScaleCatalog catalog,
        string roomId)
    {
        if (!TryGetActiveRoomStageScale(out float currentStageScale) ||
            catalog == null ||
            !catalog.TryGetReferenceRoomStageScale(roomId, out float referenceStageScale))
        {
            return 1f;
        }

        return SanitizeStageScale(currentStageScale) /
            SanitizeStageScale(referenceStageScale);
    }

    public static float GetInheritedZoomRatio(
        CharacterRoomScaleTarget target,
        string roomId,
        float currentZoomRatio)
    {
        if (!TryGetTargetRoomStage(target, roomId, out RoomContentGroup roomStage) ||
            !roomStage.gameObject.activeInHierarchy)
        {
            return 1f;
        }

        // Preserve the previous guest system's protection against treating an inactive/different
        // room stage as though it inherited the active camera stage zoom.
        if (TryGetActiveRoomStageScale(out float activeStageScale))
        {
            float targetStageScale = SanitizeStageScale(roomStage.transform.lossyScale.x);
            float tolerance = Mathf.Max(0.001f, activeStageScale * 0.001f);

            if (Mathf.Abs(targetStageScale - activeStageScale) > tolerance)
            {
                return 1f;
            }
        }

        return Mathf.Max(MinimumStageScale, currentZoomRatio);
    }

    public static bool TryGetTargetRoomStage(
        CharacterRoomScaleTarget target,
        string roomId,
        out RoomContentGroup roomStage)
    {
        roomStage = null;

        if (target == null)
        {
            return false;
        }

        Transform scaleRoot = target.ResolveScaleRoot();
        roomStage = scaleRoot != null
            ? scaleRoot.GetComponentInParent<RoomContentGroup>(true)
            : null;

        if (roomStage == null)
        {
            roomStage = target.GetComponentInParent<RoomContentGroup>(true);
        }

        if (roomStage == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(roomId) ||
            string.IsNullOrWhiteSpace(roomStage.RoomName) ||
            CharacterRoomScaleCatalog.SameRoom(roomStage.RoomName, roomId);
    }

    public static float CalculateTargetLocalScale(
        float calibratedLocalScale,
        float currentZoomRatio,
        float inheritedZoomRatio)
    {
        return Mathf.Max(0.001f, calibratedLocalScale) *
            Mathf.Max(MinimumStageScale, currentZoomRatio) /
            Mathf.Max(MinimumStageScale, inheritedZoomRatio);
    }

    public static float SanitizeStageScale(float value)
    {
        return Mathf.Max(MinimumStageScale, value);
    }
}
