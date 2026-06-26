using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PointClickPlayerMovement))]
[CanEditMultipleObjects]
public sealed class PointClickPlayerMovementEditor : Editor
{
    private const string PlayModeWarning =
        "PLAY MODE: preview only. Calibration changes will not be saved to the scene. Exit Play Mode to save FRONT/BACK room scale.";
    private const string MatchingEndpointWarning =
        "Front and Back positions are the same. Move the player to the front and save FRONT, then move to the back and save BACK.";

    private static readonly string[] HiddenButlerScaleFields =
    {
        "m_Script",
        "editorSelectedButlerScaleRoomId",
        "butlerRoomScaleOverrides"
    };

    private static readonly Dictionary<string, float> DraftScales = new Dictionary<string, float>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenButlerScaleFields);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        DrawButlerRoomScaleCalibration();
    }

    private void DrawButlerRoomScaleCalibration()
    {
        EditorGUILayout.LabelField("Butler Room Scale Calibration", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(PlayModeWarning, MessageType.Warning);
        }

        if (targets.Length != 1)
        {
            EditorGUILayout.HelpBox("Select one Butler at a time to edit per-room scale calibration.", MessageType.Info);
            return;
        }

        PointClickPlayerMovement movement = (PointClickPlayerMovement)target;
        ButlerRoomScaleCalibrationWindow.DrawSelectedPlayerWarnings(movement);

        string[] roomOptions = BuildRoomOptions(movement);

        if (roomOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("No rooms were found. Add RoomContentGroup objects or select a room during Play Mode.", MessageType.Warning);
            return;
        }

        string selectedRoom = ResolveSelectedRoom(movement, roomOptions);
        int selectedIndex = Array.FindIndex(roomOptions, room => SameRoom(room, selectedRoom));
        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomOptions.Length - 1);

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup("Room", selectedIndex, roomOptions);

        if (EditorGUI.EndChangeCheck())
        {
            RecordMovementAndTransform(movement, "Select Butler Scale Room");
            selectedRoom = roomOptions[nextIndex];
            movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
            movement.RefreshPerspectiveScaleNow(true);
            MarkDirty(movement);
        }

        movement.TryGetCurrentButlerRoomLocalFootPoint(out Vector2 footPoint);
        bool hasOverride = movement.TryGetButlerRoomScaleOverride(selectedRoom, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data);
        float currentScale = movement.CaptureCurrentButlerScaleMultiplier();
        bool safePlayerForSaving = ButlerRoomScaleCalibrationWindow.IsSafePlayerObjectForSaving(movement);
        bool canSaveCalibration = !Application.isPlaying && safePlayerForSaving;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector2Field("Current Room-Local Foot Point", footPoint);
            EditorGUILayout.Vector3Field("Current Transform localScale", movement.transform.localScale);
            EditorGUILayout.Toggle("Stored Override", hasOverride);
            EditorGUILayout.Toggle("Complete Override", hasOverride && data.IsComplete);
        }

        if (HasMatchingSavedEndpoints(data))
        {
            EditorGUILayout.HelpBox(MatchingEndpointWarning, MessageType.Warning);
        }

        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, true, canSaveCalibration);
        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, false, canSaveCalibration);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Saved Room Scaling Here"))
            {
                PreviewScale(movement, movement.GetButlerScaleForRoomAtY(selectedRoom, footPoint.y, currentScale), "Preview Butler Current Depth Scale");
            }

            using (new EditorGUI.DisabledScope(!canSaveCalibration))
            {
                if (GUILayout.Button("Initialize Room From Existing Perspective"))
                {
                    RecordMovementAndTransform(movement, "Initialize Butler Room Scale");
                    movement.InitializeButlerScaleOverrideForRoomFromCurrentPerspective(selectedRoom);
                    ClearEndpointDrafts(movement, selectedRoom);
                    MarkDirty(movement);
                }
            }
        }

        using (new EditorGUI.DisabledScope(!hasOverride || !canSaveCalibration))
        {
            if (GUILayout.Button("Clear Saved Scale For This Room"))
            {
                RecordMovementAndTransform(movement, "Remove Butler Room Scale");
                movement.RemoveButlerScaleOverrideForRoom(selectedRoom);
                ClearEndpointDrafts(movement, selectedRoom);
                MarkDirty(movement);
            }
        }

        EditorGUILayout.HelpBox(
            "For each room, place the Butler near the front/closest part of the floor and tune Front Scale until he reads around 3/4 of a comparable door height or about 1.5x a chair. Then place him near the back/farthest part of the floor and tune Back Scale. The Butler interpolates between these two saved endpoints while walking.",
            MessageType.None);
    }

    private static void DrawEndpointControls(
        PointClickPlayerMovement movement,
        string selectedRoom,
        PointClickPlayerMovement.ButlerRoomScaleOverrideData data,
        Vector2 footPoint,
        float currentScale,
        bool front,
        bool canSaveCalibration)
    {
        string label = front ? "Front" : "Back";
        bool hasEndpoint = front ? data.HasFront : data.HasBack;
        float endpointY = hasEndpoint ? (front ? data.FrontFootY : data.BackFootY) : footPoint.y;
        float storedScale = hasEndpoint ? (front ? data.FrontScale : data.BackScale) : currentScale;
        string draftKey = GetDraftScaleKey(movement, selectedRoom, front);
        float endpointScale = GetDraftScale(draftKey, storedScale);
        string previewButtonLabel = front ? "Preview FRONT Size" : "Preview BACK Size";
        string saveButtonLabel = front ? "Save FRONT: Current Position + Scale" : "Save BACK: Current Position + Scale";

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField($"{label} saved", hasEndpoint ? "Yes" : "No");
            EditorGUILayout.FloatField($"{label} Y", endpointY);
            EditorGUILayout.FloatField($"{label} Scale", storedScale);
        }

        endpointScale = ButlerRoomScaleCalibrationWindow.DrawScaleSliderNumericNudge($"{label} Scale", endpointScale, out bool scaleChanged);

        if (scaleChanged)
        {
            DraftScales[draftKey] = endpointScale;
            PreviewScale(movement, endpointScale, $"Preview Butler {label} Scale");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(previewButtonLabel))
            {
                PreviewScale(movement, endpointScale, $"Preview Butler {label} Scale");
            }

            using (new EditorGUI.DisabledScope(!canSaveCalibration))
            {
                if (GUILayout.Button(saveButtonLabel))
                {
                    RecordMovementAndTransform(movement, $"Set Butler {label} Scale");

                    if (front)
                    {
                        movement.SetButlerFrontScaleForRoom(selectedRoom, footPoint.y, endpointScale);
                    }
                    else
                    {
                        movement.SetButlerBackScaleForRoom(selectedRoom, footPoint.y, endpointScale);
                    }

                    DraftScales[draftKey] = endpointScale;
                    MarkDirty(movement);
                }
            }
        }
    }

    private static float GetDraftScale(string key, float fallbackScale)
    {
        return DraftScales.TryGetValue(key, out float draftScale)
            ? Mathf.Max(0.001f, draftScale)
            : Mathf.Max(0.001f, fallbackScale);
    }

    private static string GetDraftScaleKey(PointClickPlayerMovement movement, string roomId, bool front)
    {
        return $"{movement.GetEntityId()}:{NormalizeRoomName(roomId)}:{(front ? "front" : "back")}";
    }

    private static void ClearEndpointDrafts(PointClickPlayerMovement movement, string roomId)
    {
        DraftScales.Remove(GetDraftScaleKey(movement, roomId, true));
        DraftScales.Remove(GetDraftScaleKey(movement, roomId, false));
    }

    private static void PreviewScale(PointClickPlayerMovement movement, float scale, string actionName)
    {
        RecordMovementAndTransform(movement, actionName);
        movement.ApplyButlerScalePreview(scale);

        if (!Application.isPlaying)
        {
            MarkDirty(movement);
        }
    }

    private static bool HasMatchingSavedEndpoints(PointClickPlayerMovement.ButlerRoomScaleOverrideData data)
    {
        return data.HasFront &&
            data.HasBack &&
            Mathf.Abs(data.FrontFootY - data.BackFootY) < PointClickPlayerMovement.ButlerRoomScaleEndpointEpsilon;
    }

    private static string ResolveSelectedRoom(PointClickPlayerMovement movement, string[] roomOptions)
    {
        string selectedRoom = movement.EditorSelectedButlerScaleRoomId;

        if (string.IsNullOrWhiteSpace(selectedRoom))
        {
            selectedRoom = movement.CurrentButlerScaleRoomId;
        }

        if (!string.IsNullOrWhiteSpace(selectedRoom))
        {
            for (int i = 0; i < roomOptions.Length; i++)
            {
                if (SameRoom(roomOptions[i], selectedRoom))
                {
                    return roomOptions[i];
                }
            }
        }

        return roomOptions.Length > 0 ? roomOptions[0] : string.Empty;
    }

    private static string[] BuildRoomOptions(PointClickPlayerMovement movement)
    {
        List<string> rooms = new List<string>();

        AddRoom(rooms, movement.EditorSelectedButlerScaleRoomId);
        AddRoom(rooms, movement.CurrentButlerScaleRoomId);

        RoomNavigationManager navigationManager = FindSceneNavigationManager();
        AddRoom(rooms, navigationManager != null ? navigationManager.CurrentRoom : string.Empty);
        movement.GetButlerScaleOverrideRoomIds(rooms);

        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (roomContentGroup != null &&
                roomContentGroup.gameObject != null &&
                roomContentGroup.gameObject.scene.IsValid())
            {
                AddRoom(rooms, roomContentGroup.RoomName);
            }
        }

        rooms.Sort(StringComparer.OrdinalIgnoreCase);
        return rooms.ToArray();
    }

    private static RoomNavigationManager FindSceneNavigationManager()
    {
        RoomNavigationManager[] navigationManagers = Resources.FindObjectsOfTypeAll<RoomNavigationManager>();

        for (int i = 0; i < navigationManagers.Length; i++)
        {
            RoomNavigationManager navigationManager = navigationManagers[i];

            if (navigationManager != null &&
                navigationManager.gameObject != null &&
                navigationManager.gameObject.scene.IsValid())
            {
                return navigationManager;
            }
        }

        return null;
    }

    private static void AddRoom(List<string> rooms, string roomId)
    {
        if (rooms == null || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        string cleanRoomId = roomId.Trim();

        for (int i = 0; i < rooms.Count; i++)
        {
            if (SameRoom(rooms[i], cleanRoomId))
            {
                return;
            }
        }

        rooms.Add(cleanRoomId);
    }

    private static void RecordMovementAndTransform(PointClickPlayerMovement movement, string actionName)
    {
        Undo.RecordObject(movement, actionName);

        if (movement != null)
        {
            Undo.RecordObject(movement.transform, actionName);
        }
    }

    private static void MarkDirty(PointClickPlayerMovement movement)
    {
        if (movement == null || Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(movement.transform);

        if (movement.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        }
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }
}
