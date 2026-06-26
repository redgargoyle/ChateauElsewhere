using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ButlerRoomScaleCalibrationWindow : EditorWindow
{
    private PointClickPlayerMovement selectedButler;
    private int selectedRoomIndex;
    private Vector2 scroll;

    [MenuItem("Tools/Butler/Room Scale Calibration")]
    public static void Open()
    {
        GetWindow<ButlerRoomScaleCalibrationWindow>("Butler Scale");
    }

    private void OnEnable()
    {
        selectedButler = FindSelectedOrSceneButler();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Butler Room Scale Calibration", EditorStyles.boldLabel);
        selectedButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find/Select Active Butler"))
            {
                selectedButler = FindSelectedOrSceneButler();

                if (selectedButler != null)
                {
                    Selection.activeGameObject = selectedButler.gameObject;
                    EditorGUIUtility.PingObject(selectedButler);
                }
            }

            using (new EditorGUI.DisabledScope(selectedButler == null))
            {
                if (GUILayout.Button("Save Scene"))
                {
                    SaveButlerScene(selectedButler);
                }
            }
        }

        if (selectedButler == null)
        {
            EditorGUILayout.HelpBox("Select or find the active PointClickPlayerMovement Butler to calibrate room scale.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        string[] rooms = BuildRoomOptions(selectedButler);

        if (rooms.Length == 0)
        {
            EditorGUILayout.HelpBox("No scene RoomContentGroup rooms were found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        string selectedRoom = ResolveSelectedRoom(selectedButler, rooms, ref selectedRoomIndex);
        DrawRoomSelection(selectedButler, rooms, ref selectedRoomIndex, ref selectedRoom);
        DrawButlerSummaryAndControls(selectedButler, selectedRoom);

        EditorGUILayout.EndScrollView();
    }

    private static void DrawRoomSelection(
        PointClickPlayerMovement movement,
        string[] rooms,
        ref int selectedIndex,
        ref string selectedRoom)
    {
        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Room", selectedIndex, rooms);

        if (EditorGUI.EndChangeCheck())
        {
            selectedRoom = rooms[selectedIndex];
            RecordMovementAndTransform(movement, "Select Butler Scale Room");
            movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
            movement.RefreshPerspectiveScaleNow(true);
            MarkDirty(movement);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous Room"))
            {
                selectedIndex = (selectedIndex - 1 + rooms.Length) % rooms.Length;
                selectedRoom = rooms[selectedIndex];
                RecordMovementAndTransform(movement, "Select Previous Butler Scale Room");
                movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
                movement.RefreshPerspectiveScaleNow(true);
                MarkDirty(movement);
            }

            if (GUILayout.Button("Next Room"))
            {
                selectedIndex = (selectedIndex + 1) % rooms.Length;
                selectedRoom = rooms[selectedIndex];
                RecordMovementAndTransform(movement, "Select Next Butler Scale Room");
                movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
                movement.RefreshPerspectiveScaleNow(true);
                MarkDirty(movement);
            }

            if (GUILayout.Button("Ping RoomContentGroup"))
            {
                PingRoom(selectedRoom);
            }
        }
    }

    private static void DrawButlerSummaryAndControls(PointClickPlayerMovement movement, string selectedRoom)
    {
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

        using (new EditorGUI.DisabledScope(!hasOverride))
        {
            if (GUILayout.Button("Remove Override"))
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

    private static PointClickPlayerMovement FindSelectedOrSceneButler()
    {
        PointClickPlayerMovement selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<PointClickPlayerMovement>(true)
            : null;

        if (selected != null)
        {
            return selected;
        }

        PointClickPlayerMovement[] candidates = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null && candidate.gameObject != null && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveSelectedRoom(PointClickPlayerMovement movement, string[] rooms, ref int selectedIndex)
    {
        string selectedRoom = movement.EditorSelectedButlerScaleRoomId;

        if (string.IsNullOrWhiteSpace(selectedRoom))
        {
            selectedRoom = movement.CurrentButlerScaleRoomId;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (SameRoom(rooms[i], selectedRoom))
            {
                selectedIndex = i;
                return rooms[i];
            }
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, rooms.Length - 1);
        return rooms[selectedIndex];
    }

    private static string[] BuildRoomOptions(PointClickPlayerMovement movement)
    {
        List<string> rooms = new List<string>();

        AddRoom(rooms, movement.EditorSelectedButlerScaleRoomId);
        AddRoom(rooms, movement.CurrentButlerScaleRoomId);
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

    private static void PingRoom(string roomId)
    {
        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (roomContentGroup == null || !SameRoom(roomContentGroup.RoomName, roomId))
            {
                continue;
            }

            Selection.activeGameObject = roomContentGroup.gameObject;
            EditorGUIUtility.PingObject(roomContentGroup.gameObject);
            return;
        }
    }

    private static void SaveButlerScene(PointClickPlayerMovement movement)
    {
        if (movement == null || !movement.gameObject.scene.IsValid())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        EditorSceneManager.SaveScene(movement.gameObject.scene);
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
