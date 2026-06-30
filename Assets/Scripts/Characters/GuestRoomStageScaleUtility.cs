using System.Collections.Generic;
using UnityEngine;

public static class GuestRoomStageScaleUtility
{
    private const float MinStageScale = 0.0001f;
    private static readonly HashSet<string> MissingReferenceWarnings = new HashSet<string>();

    public static bool TryGetActiveRoomStageScale(out float stageScale)
    {
        stageScale = 1f;

        CameraManager cameraManager = Object.FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);

        if (cameraManager != null &&
            cameraManager.TryGetRoomStageScreenTransform(out _, out _, out float cameraStageScale))
        {
            stageScale = SanitizeRoomStageScale(cameraStageScale);
            return true;
        }

        return TryGetFallbackActiveRoomStageScale(out stageScale);
    }

    public static float CalculateRoomStageZoomRatio(
        float currentRoomStageScale,
        float referenceRoomStageScale)
    {
        return SanitizeRoomStageScale(currentRoomStageScale) /
            SanitizeRoomStageScale(referenceRoomStageScale);
    }

    public static bool TryGetCurrentRoomStageZoomRatio(
        GuestRoomScaleCalibration calibration,
        string roomId,
        out float zoomRatio,
        out string diagnostic)
    {
        zoomRatio = 1f;
        diagnostic = string.Empty;

        if (!TryGetActiveRoomStageScale(out float currentStageScale))
        {
            diagnostic = "No active room-stage scale available; using zoom ratio 1.";
            return false;
        }

        float referenceStageScale = 1f;
        bool hasReference = calibration != null &&
            calibration.TryGetReferenceRoomStageScale(roomId, out referenceStageScale);

        if (!hasReference)
        {
            referenceStageScale = 1f;
            LogMissingReferenceOnce(roomId);
        }

        zoomRatio = CalculateRoomStageZoomRatio(currentStageScale, referenceStageScale);
        diagnostic = hasReference
            ? $"Current stage {currentStageScale:0.####} / reference {referenceStageScale:0.####}."
            : $"Current stage {currentStageScale:0.####}; missing reference for '{roomId}', using 1.";
        return true;
    }

    public static bool TryGetInheritedRoomStageZoomRatio(
        GuestScaleParticipant participant,
        GuestRoomScaleCalibration calibration,
        string roomId,
        out float inheritedZoomRatio,
        out string diagnostic)
    {
        inheritedZoomRatio = 1f;
        diagnostic = string.Empty;

        if (participant == null)
        {
            diagnostic = "No guest participant.";
            return false;
        }

        if (!TryGetCurrentRoomStageZoomRatio(calibration, roomId, out float roomZoomRatio, out string zoomDiagnostic))
        {
            diagnostic = zoomDiagnostic;
            return false;
        }

        if (!TryGetParticipantRoomStage(participant, roomId, out RoomContentGroup roomStage))
        {
            diagnostic = "Guest scale root is not under the selected room stage.";
            return true;
        }

        if (!roomStage.gameObject.activeInHierarchy)
        {
            diagnostic = $"Guest scale root is under inactive room stage '{roomStage.RoomName}'.";
            return true;
        }

        if (TryGetActiveRoomStageScale(out float activeStageScale))
        {
            float roomStageScale = SanitizeRoomStageScale(roomStage.transform.lossyScale.x);
            float tolerance = Mathf.Max(0.001f, activeStageScale * 0.001f);

            if (Mathf.Abs(roomStageScale - activeStageScale) > tolerance)
            {
                diagnostic = $"Guest room stage scale {roomStageScale:0.####} does not match active stage {activeStageScale:0.####}.";
                return true;
            }
        }

        inheritedZoomRatio = roomZoomRatio;
        diagnostic = $"Guest scale root inherits room-stage zoom from '{roomStage.RoomName}'.";
        return true;
    }

    public static bool TryGetParticipantRoomStage(
        GuestScaleParticipant participant,
        string roomId,
        out RoomContentGroup roomStage)
    {
        roomStage = null;

        if (participant == null)
        {
            return false;
        }

        Transform scaleRoot = participant.ResolveScaleRoot();
        roomStage = scaleRoot != null
            ? scaleRoot.GetComponentInParent<RoomContentGroup>(true)
            : null;

        if (roomStage == null)
        {
            roomStage = participant.GetComponentInParent<RoomContentGroup>(true);
        }

        if (roomStage == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(roomId) ||
            string.IsNullOrWhiteSpace(roomStage.RoomName) ||
            GuestRoomScaleCalibration.SameRoom(roomStage.RoomName, roomId);
    }

    public static float SanitizeRoomStageScale(float value)
    {
        return Mathf.Max(MinStageScale, value);
    }

    private static bool TryGetFallbackActiveRoomStageScale(out float stageScale)
    {
        stageScale = 1f;
        RoomContentGroup[] roomStages = Object.FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomStages.Length; i++)
        {
            RoomContentGroup roomStage = roomStages[i];

            if (roomStage == null || !roomStage.gameObject.activeInHierarchy)
            {
                continue;
            }

            stageScale = SanitizeRoomStageScale(roomStage.transform.lossyScale.x);
            return true;
        }

        return false;
    }

    private static void LogMissingReferenceOnce(string roomId)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        string key = GuestRoomScaleCalibration.NormalizeRoomName(roomId);

        if (MissingReferenceWarnings.Add(key))
        {
            Debug.LogWarning($"[GuestScale] Missing reference room-stage scale for '{roomId}'. Using 1 until the room guest size is saved.");
        }
    }
}
