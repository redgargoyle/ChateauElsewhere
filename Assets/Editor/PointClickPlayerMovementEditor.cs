using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PointClickPlayerMovement))]
[CanEditMultipleObjects]
public sealed class PointClickPlayerMovementEditor : Editor
{
    private static readonly string[] HiddenButlerScaleFields =
    {
        "m_Script",
        "editorSelectedButlerScaleRoomId",
        "butlerRoomScaleOverrides"
    };

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

        if (targets.Length != 1)
        {
            EditorGUILayout.HelpBox("Select one Butler at a time to edit per-room scale calibration.", MessageType.Info);
            return;
        }

        PointClickPlayerMovement movement = (PointClickPlayerMovement)target;
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

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector2Field("Current Room-Local Foot Point", footPoint);
            EditorGUILayout.Vector3Field("Current Transform localScale", movement.transform.localScale);
            EditorGUILayout.Toggle("Stored Override", hasOverride);
            EditorGUILayout.Toggle("Complete Override", hasOverride && data.IsComplete);
        }

        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, true);
        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Current Depth Preview"))
            {
                RecordMovementAndTransform(movement, "Preview Butler Current Depth Scale");
                float scale = movement.GetButlerScaleForRoomAtY(selectedRoom, footPoint.y, currentScale);
                movement.ApplyButlerScalePreview(scale);
                MarkDirty(movement);
            }

            if (GUILayout.Button("Initialize Selected Room From Current Perspective"))
            {
                RecordMovementAndTransform(movement, "Initialize Butler Room Scale");
                movement.InitializeButlerScaleOverrideForRoomFromCurrentPerspective(selectedRoom);
                MarkDirty(movement);
            }
        }

        using (new EditorGUI.DisabledScope(!hasOverride))
        {
            if (GUILayout.Button("Remove Room Override"))
            {
                RecordMovementAndTransform(movement, "Remove Butler Room Scale");
                movement.RemoveButlerScaleOverrideForRoom(selectedRoom);
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
        bool front)
    {
        string label = front ? "Front" : "Back";
        bool hasEndpoint = front ? data.HasFront : data.HasBack;
        float endpointY = hasEndpoint ? (front ? data.FrontFootY : data.BackFootY) : footPoint.y;
        float endpointScale = hasEndpoint ? (front ? data.FrontScale : data.BackScale) : currentScale;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        endpointY = EditorGUILayout.FloatField($"{label} Y", endpointY);
        endpointScale = Mathf.Max(0.001f, EditorGUILayout.FloatField($"{label} Scale", endpointScale));

        if (EditorGUI.EndChangeCheck())
        {
            RecordMovementAndTransform(movement, $"Edit Butler {label} Scale");

            if (front)
            {
                movement.SetButlerFrontScaleForRoom(selectedRoom, endpointY, endpointScale, false);
            }
            else
            {
                movement.SetButlerBackScaleForRoom(selectedRoom, endpointY, endpointScale, false);
            }

            MarkDirty(movement);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            string previewButtonLabel = front ? "Apply Front Preview" : "Apply Back Preview";
            string setButtonLabel = front ? "Set Front Here" : "Set Back Here";

            if (GUILayout.Button(previewButtonLabel))
            {
                RecordMovementAndTransform(movement, $"Preview Butler {label} Scale");
                movement.ApplyButlerScalePreview(endpointScale);
                MarkDirty(movement);
            }

            if (GUILayout.Button(setButtonLabel))
            {
                RecordMovementAndTransform(movement, $"Set Butler {label} Scale");
                float capturedScale = movement.CaptureCurrentButlerScaleMultiplier();

                if (front)
                {
                    movement.SetButlerFrontScaleForRoom(selectedRoom, footPoint.y, capturedScale);
                }
                else
                {
                    movement.SetButlerBackScaleForRoom(selectedRoom, footPoint.y, capturedScale);
                }

                MarkDirty(movement);
            }
        }
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
        if (movement == null)
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
