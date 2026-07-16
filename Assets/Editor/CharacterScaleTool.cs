using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CharacterScaleTool : EditorWindow
{
    private const string CatalogObjectName = "Rooms";
    private const string MarkerRootName = "Character Scale";
    private const string FrontName = "Front";
    private const string BackName = "Back";

    private CharacterScaleCatalog catalog;
    private int selectedRoomIndex;
    private float sampleY;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Dreadforge/Characters/Character Scale Tool")]
    public static void Open()
    {
        CharacterScaleTool window = GetWindow<CharacterScaleTool>("Character Scale Tool");
        window.minSize = new Vector2(430f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
        SceneView.duringSceneGui += DrawSceneHandles;
        FindCatalog();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
    }

    private void OnHierarchyChange()
    {
        if (catalog == null)
        {
            FindCatalog();
        }

        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Character Scale Catalog", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each room owns one Front object and one Back object. Their X/Y positions are manual guides; " +
            "their uniform scales are the character sizes. Runtime uses only Y to smoothly interpolate size.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        catalog = (CharacterScaleCatalog)EditorGUILayout.ObjectField(
            "Catalog",
            catalog,
            typeof(CharacterScaleCatalog),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            selectedRoomIndex = 0;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Catalog"))
            {
                FindCatalog();
            }

            if (GUILayout.Button("Create / Repair Missing Room Setup"))
            {
                catalog = CreateOrRepairMissingRoomSetup();
                selectedRoomIndex = 0;
            }
        }

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                "No CharacterScaleCatalog is present. Use Create / Repair Missing Room Setup in Gameplay.",
                MessageType.Warning);
            DrawStatus();
            return;
        }

        CharacterScaleRoom[] rooms = catalog.Rooms
            .Where(room => room != null)
            .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rooms.Length == 0)
        {
            EditorGUILayout.HelpBox("The catalog has no rooms.", MessageType.Warning);
            DrawStatus();
            return;
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, rooms.Length - 1);
        string[] roomNames = rooms.Select(room => room.RoomName).ToArray();
        selectedRoomIndex = EditorGUILayout.Popup("Room", selectedRoomIndex, roomNames);
        CharacterScaleRoom selectedRoom = rooms[selectedRoomIndex];

        EditorGUILayout.Space(8f);
        DrawRoomEditor(selectedRoom);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shared Y Scale Function", EditorStyles.boldLabel);
        sampleY = EditorGUILayout.FloatField("Sample Room Y", sampleY);

        if (selectedRoom.Front != null && selectedRoom.Back != null)
        {
            float previewScale = CharacterScaleFunction.Evaluate(
                sampleY,
                selectedRoom.GetRoomLocalPosition(selectedRoom.Front).y,
                selectedRoom.GetUniformScale(selectedRoom.Front),
                selectedRoom.GetRoomLocalPosition(selectedRoom.Back).y,
                selectedRoom.GetUniformScale(selectedRoom.Back));
            EditorGUILayout.LabelField("Preview Character Scale", previewScale.ToString("0.####"));
        }

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("Validate Catalog"))
        {
            bool valid = catalog.ValidateCatalog(out string report);
            SetStatus(report, valid ? MessageType.Info : MessageType.Error);
        }

        DrawStatus();
    }

    private void DrawRoomEditor(CharacterScaleRoom room)
    {
        EditorGUILayout.LabelField(room.RoomName, EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Room Object", room.Room, typeof(RoomContentGroup), true);

        DrawMarkerEditor("Front", room, room.Front);
        EditorGUILayout.Space(4f);
        DrawMarkerEditor("Back", room, room.Back);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Reference Stage Scale", room.ReferenceStageScale.ToString("0.########"));

            if (GUILayout.Button("Capture", GUILayout.Width(80f)))
            {
                Undo.RecordObject(room, "Capture Character Scale Room Stage");
                room.CaptureReferenceStageScale();
                MarkDirty(room);
            }
        }

        if (!room.IsConfigured(out string reason))
        {
            EditorGUILayout.HelpBox(reason, MessageType.Error);
        }
    }

    private static void DrawMarkerEditor(string label, CharacterScaleRoom room, Transform marker)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUILayout.ObjectField($"{label} Object", marker, typeof(Transform), true);

        if (marker == null)
        {
            return;
        }

        Vector2 oldPosition = room.GetRoomLocalPosition(marker);
        Vector2 newPosition = EditorGUILayout.Vector2Field("Position X / Y", oldPosition);
        float oldScale = room.GetUniformScale(marker);
        float newScale = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Scale", oldScale));

        if (newPosition != oldPosition || !Mathf.Approximately(newScale, oldScale))
        {
            Undo.RecordObject(marker, $"Edit Character Scale {label}");
            Vector3 worldPosition = room.Room.transform.TransformPoint(new Vector3(newPosition.x, newPosition.y, 0f));
            marker.position = worldPosition;
            marker.localScale = new Vector3(newScale, newScale, 1f);
            MarkDirty(marker);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"Select {label}"))
            {
                Selection.activeTransform = marker;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            if (GUILayout.Button("Use Selected Position") && Selection.activeTransform != null)
            {
                Undo.RecordObject(marker, $"Place Character Scale {label}");
                Vector3 selectedRoomLocal = room.Room.transform.InverseTransformPoint(Selection.activeTransform.position);
                marker.position = room.Room.transform.TransformPoint(new Vector3(
                    selectedRoomLocal.x,
                    selectedRoomLocal.y,
                    0f));
                MarkDirty(marker);
            }
        }
    }

    private void DrawSceneHandles(SceneView sceneView)
    {
        if (catalog == null || catalog.Rooms == null)
        {
            return;
        }

        CharacterScaleRoom[] rooms = catalog.Rooms
            .Where(room => room != null)
            .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rooms.Length == 0)
        {
            return;
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, rooms.Length - 1);
        CharacterScaleRoom room = rooms[selectedRoomIndex];
        DrawPositionHandle(room, room.Front, new Color(0.95f, 0.35f, 0.2f, 1f), $"{room.RoomName} Front");
        DrawPositionHandle(room, room.Back, new Color(0.2f, 0.65f, 1f, 1f), $"{room.RoomName} Back");
    }

    private static void DrawPositionHandle(CharacterScaleRoom room, Transform marker, Color color, string label)
    {
        if (room == null || room.Room == null || marker == null)
        {
            return;
        }

        Handles.color = color;
        Handles.Label(marker.position, $"{label}  scale {Mathf.Abs(marker.localScale.x):0.###}");
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(marker.position, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(marker, $"Move {label}");
            Vector3 roomLocal = room.Room.transform.InverseTransformPoint(newPosition);
            marker.position = room.Room.transform.TransformPoint(new Vector3(roomLocal.x, roomLocal.y, 0f));
            MarkDirty(marker);
        }
    }

    public static CharacterScaleCatalog CreateOrRepairMissingRoomSetup()
    {
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include)
            .Where(room => room != null && room.gameObject.scene.IsValid() && room.gameObject.scene.isLoaded)
            .GroupBy(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roomGroups.Length == 0)
        {
            Debug.LogError("Character Scale Tool could not find any loaded RoomContentGroup objects.");
            return null;
        }

        GameObject catalogObject = GameObject.Find(CatalogObjectName);

        if (catalogObject == null)
        {
            catalogObject = roomGroups[0].transform.parent != null
                ? roomGroups[0].transform.parent.gameObject
                : new GameObject(CatalogObjectName);
        }

        CharacterScaleCatalog catalog = catalogObject.GetComponent<CharacterScaleCatalog>();

        if (catalog == null)
        {
            catalog = Undo.AddComponent<CharacterScaleCatalog>(catalogObject);
        }

        List<CharacterScaleRoom> roomDefinitions = new List<CharacterScaleRoom>(roomGroups.Length);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];
            CharacterScaleRoom roomDefinition = roomGroup.GetComponent<CharacterScaleRoom>();

            if (roomDefinition == null)
            {
                roomDefinition = Undo.AddComponent<CharacterScaleRoom>(roomGroup.gameObject);
            }

            Transform markerRoot = FindOrCreateChild(roomGroup.transform, MarkerRootName, out _);
            Transform front = FindOrCreateChild(markerRoot, FrontName, out bool createdFront);
            Transform back = FindOrCreateChild(markerRoot, BackName, out bool createdBack);

            RectTransform roomRect = roomGroup.transform as RectTransform;
            float halfHeight = roomRect != null && roomRect.rect.height > 1f
                ? roomRect.rect.height * 0.5f
                : 300f;

            if (createdFront)
            {
                InitializeNewMarker(front, new Vector2(0f, -halfHeight * 0.8f));
            }

            if (createdBack)
            {
                InitializeNewMarker(back, new Vector2(0f, halfHeight * 0.8f));
            }

            if (roomDefinition.Front == null || roomDefinition.Back == null)
            {
                Undo.RecordObject(roomDefinition, "Repair Character Scale Room");
                roomDefinition.Configure(
                    roomGroup,
                    front,
                    back,
                    Mathf.Max(0.0001f, Mathf.Abs(roomGroup.transform.localScale.x)));
                MarkDirty(roomDefinition);
            }

            roomDefinitions.Add(roomDefinition);
        }

        Undo.RecordObject(catalog, "Sync Character Scale Catalog Rooms");
        catalog.SetRooms(roomDefinitions.ToArray());
        MarkDirty(catalog);
        Debug.Log($"Character Scale Catalog now contains {roomDefinitions.Count} room definitions.", catalog);
        return catalog;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, out bool created)
    {
        Transform child = parent.Find(childName);

        if (child != null)
        {
            created = false;
            return child;
        }

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        created = true;
        return childObject.transform;
    }

    private static void InitializeNewMarker(Transform marker, Vector2 defaultPosition)
    {
        if (marker == null)
        {
            return;
        }

        marker.localPosition = new Vector3(defaultPosition.x, defaultPosition.y, 0f);
    }

    private void FindCatalog()
    {
        catalog = FindAnyObjectByType<CharacterScaleCatalog>(FindObjectsInactive.Include);
        SetStatus(
            catalog != null ? "Character Scale Catalog found." : "No Character Scale Catalog found.",
            catalog != null ? MessageType.Info : MessageType.Warning);
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        Repaint();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    private static void MarkDirty(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        EditorUtility.SetDirty(target);
        Scene scene = target is Component component
            ? component.gameObject.scene
            : target is GameObject gameObject
                ? gameObject.scene
                : default;

        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
