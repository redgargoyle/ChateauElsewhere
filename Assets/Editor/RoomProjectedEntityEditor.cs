using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomProjectedEntity))]
[CanEditMultipleObjects]
public sealed class RoomProjectedEntityEditor : Editor
{
    private static readonly string[] HiddenRoomScaleFields =
    {
        "m_Script",
        "useSharedCharacterRoomScale",
        "sharedCharacterScaleSource",
        "ignoreRoomVisualScaleOverridesWhenUsingSharedCharacterScale",
        "editorSelectedVisualScaleRoomId",
        "roomVisualScaleOverrides"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenRoomScaleFields);

        EditorGUILayout.Space(8f);
        DrawSharedCharacterScaleControls();

        bool serializedChanged = serializedObject.ApplyModifiedProperties();

        if (serializedChanged)
        {
            RefreshProjectionTargets();
        }

        EditorGUILayout.Space(8f);
        DrawRoomVisualScaleControls();
    }

    private void DrawSharedCharacterScaleControls()
    {
        EditorGUILayout.LabelField("Shared Character Scale", EditorStyles.boldLabel);

        SerializedProperty useSharedScale = serializedObject.FindProperty("useSharedCharacterRoomScale");
        SerializedProperty scaleSource = serializedObject.FindProperty("sharedCharacterScaleSource");
        SerializedProperty ignoreOldOverride = serializedObject.FindProperty("ignoreRoomVisualScaleOverridesWhenUsingSharedCharacterScale");

        if (useSharedScale != null)
        {
            EditorGUILayout.PropertyField(useSharedScale, new GUIContent("Use Shared Character Room Scale"));
        }

        if (scaleSource != null)
        {
            EditorGUILayout.PropertyField(scaleSource, new GUIContent("Scale Source"));
        }

        if (ignoreOldOverride != null)
        {
            EditorGUILayout.PropertyField(ignoreOldOverride, new GUIContent("Ignoring Old Room Visual Override"));
        }

        using (new EditorGUI.DisabledScope(true))
        {
            if (targets.Length == 1 && target is RoomProjectedEntity entity)
            {
                EditorGUILayout.Toggle("Using Shared Scale Now", entity.IsUsingSharedCharacterRoomScale);
                EditorGUILayout.FloatField("Shared Depth", entity.CurrentSharedCharacterDepth01);
                EditorGUILayout.FloatField("Shared Room Scale", entity.CurrentSharedCharacterRoomScale);
            }
            else
            {
                EditorGUILayout.TextField("Using Shared Scale Now", "-");
                EditorGUILayout.TextField("Shared Depth", "-");
                EditorGUILayout.TextField("Shared Room Scale", "-");
            }
        }
    }

    private void DrawRoomVisualScaleControls()
    {
        EditorGUILayout.LabelField("Room Visual Scale", EditorStyles.boldLabel);

        if (targets.Length != 1)
        {
            EditorGUILayout.HelpBox("Select one projected guest at a time to edit room-specific visual scale.", MessageType.Info);
            return;
        }

        RoomProjectedEntity entity = (RoomProjectedEntity)target;
        string[] roomOptions = BuildRoomOptions(entity);

        if (roomOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("No rooms were found. Add RoomContentGroup objects or assign a room perspective profile first.", MessageType.Warning);
            return;
        }

        string selectedRoom = ResolveSelectedRoom(entity, roomOptions);
        int selectedIndex = Array.FindIndex(roomOptions, room => SameRoom(room, selectedRoom));
        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomOptions.Length - 1);

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup("Room", selectedIndex, roomOptions);

        if (EditorGUI.EndChangeCheck())
        {
            RecordEntityAndVisualRoot(entity, "Select Room Visual Scale");
            selectedRoom = roomOptions[nextIndex];
            entity.SetEditorSelectedVisualScaleRoomId(selectedRoom);
            entity.ApplyProjection();
            MarkDirty(entity);
        }

        bool hasOverride = entity.HasVisualRootScaleForRoom(selectedRoom);
        Vector3 currentScale = entity.GetVisualRootScaleForRoom(selectedRoom);

        EditorGUI.BeginChangeCheck();
        Vector3 nextScale = EditorGUILayout.Vector3Field("Visual Root Scale", currentScale);

        if (EditorGUI.EndChangeCheck())
        {
            RecordEntityAndVisualRoot(entity, "Edit Room Visual Scale");
            entity.SetVisualRootScaleForRoom(selectedRoom, nextScale);
            MarkDirty(entity);
            hasOverride = true;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Stored Override", hasOverride);
            Transform visualRoot = entity.VisualRoot;
            EditorGUILayout.Vector3Field("Current Visual Scale", visualRoot != null ? visualRoot.localScale : Vector3.one);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Capture Current"))
        {
            RecordEntityAndVisualRoot(entity, "Capture Room Visual Scale");
            entity.CaptureCurrentVisualRootScaleForRoom(selectedRoom);
            MarkDirty(entity);
        }

        if (GUILayout.Button("Apply Preview"))
        {
            RecordEntityAndVisualRoot(entity, "Preview Room Visual Scale");
            entity.SetEditorSelectedVisualScaleRoomId(selectedRoom);
            entity.ApplyProjection();
            MarkDirty(entity);
        }

        using (new EditorGUI.DisabledScope(!hasOverride))
        {
            if (GUILayout.Button("Remove Override"))
            {
                RecordEntityAndVisualRoot(entity, "Remove Room Visual Scale");
                entity.RemoveVisualRootScaleForRoom(selectedRoom);
                MarkDirty(entity);
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Pick a room, edit Visual Root Scale, then use Apply Preview. The stored scale is only used when this actor is projected in that room.",
            MessageType.None);
    }

    private static string ResolveSelectedRoom(RoomProjectedEntity entity, string[] roomOptions)
    {
        string selectedRoom = entity.EditorSelectedVisualScaleRoomId;

        if (string.IsNullOrWhiteSpace(selectedRoom))
        {
            selectedRoom = entity.CurrentVisualScaleRoomId;
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

    private static string[] BuildRoomOptions(RoomProjectedEntity entity)
    {
        List<string> rooms = new List<string>();

        AddRoom(rooms, entity.EditorSelectedVisualScaleRoomId);
        AddRoom(rooms, entity.CurrentVisualScaleRoomId);
        AddRoom(rooms, entity.RoomProfile != null ? entity.RoomProfile.RoomId : string.Empty);

        ActorRoomState actorState = entity.GetComponentInParent<ActorRoomState>(true);
        AddRoom(rooms, actorState != null ? actorState.CurrentRoomId : string.Empty);
        entity.GetVisualScaleOverrideRoomIds(rooms);

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

    private static void RecordEntityAndVisualRoot(RoomProjectedEntity entity, string actionName)
    {
        Undo.RecordObject(entity, actionName);

        if (entity != null && entity.VisualRoot != null)
        {
            Undo.RecordObject(entity.VisualRoot, actionName);
        }
    }

    private static void MarkDirty(RoomProjectedEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        EditorUtility.SetDirty(entity);

        if (entity.VisualRoot != null)
        {
            EditorUtility.SetDirty(entity.VisualRoot);
        }
    }

    private void RefreshProjectionTargets()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not RoomProjectedEntity entity)
            {
                continue;
            }

            entity.ApplyProjection();
            MarkDirty(entity);
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
