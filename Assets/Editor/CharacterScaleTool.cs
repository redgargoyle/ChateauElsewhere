using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CharacterScaleTool : EditorWindow
{
    public const string DefaultCatalogAssetPath = "Assets/Resources/CharacterScaleCatalog.asset";

    private const string MarkerRootName = "Character Scale";
    private const string FrontName = "Front";
    private const string BackName = "Back";

    private CharacterScaleCatalog catalog;
    private CharacterScaleRoom[] roomHandles = Array.Empty<CharacterScaleRoom>();
    private int selectedRoomIndex;
    private float sampleY;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Dreadforge/Characters/Character Scale Tool")]
    public static void Open()
    {
        CharacterScaleTool window = GetWindow<CharacterScaleTool>("Character Scale Tool");
        window.minSize = new Vector2(470f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
        SceneView.duringSceneGui += DrawSceneHandles;
        FindCatalogAsset();
        RefreshRoomHandles();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
    }

    private void OnHierarchyChange()
    {
        RefreshRoomHandles();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Character Scale Catalog Asset", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Runtime reads only the saved ScriptableObject values. Front and Back scene objects are editor handles: " +
            "moving or scaling them does not change gameplay until Save Handles To Asset is pressed. " +
            "Only handle Y and uniform scale are saved; X is a Scene-view placement aid.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        catalog = (CharacterScaleCatalog)EditorGUILayout.ObjectField(
            "Catalog Asset",
            catalog,
            typeof(CharacterScaleCatalog),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            selectedRoomIndex = 0;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Catalog Asset"))
            {
                FindCatalogAsset();
            }

            if (GUILayout.Button("Create / Repair Editor Handles"))
            {
                catalog = CreateOrRepairMissingRoomSetup();
                RefreshRoomHandles();
                selectedRoomIndex = 0;
            }
        }

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                $"No CharacterScaleCatalog asset exists at {DefaultCatalogAssetPath}.",
                MessageType.Warning);
            DrawStatus();
            return;
        }

        RefreshRoomHandlesIfNeeded();

        if (roomHandles.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No loaded rooms have CharacterScaleRoom editor handles. Open Gameplay and repair the handles.",
                MessageType.Warning);
            DrawValidationAndStatus();
            return;
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, roomHandles.Length - 1);
        string[] roomNames = roomHandles.Select(room => room.RoomName).ToArray();
        selectedRoomIndex = EditorGUILayout.Popup("Room", selectedRoomIndex, roomNames);
        CharacterScaleRoom selectedRoom = roomHandles[selectedRoomIndex];

        EditorGUILayout.Space(8f);
        DrawRoomEditor(selectedRoom);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shared Y Scale Function", EditorStyles.boldLabel);
        sampleY = EditorGUILayout.FloatField("Sample Room Y", sampleY);

        if (catalog.TryEvaluateScaleAtRoomY(selectedRoom.RoomName, sampleY, out float savedPreviewScale))
        {
            EditorGUILayout.LabelField("Saved Asset Preview", savedPreviewScale.ToString("0.####"));
        }

        if (TryReadHandleDefinition(selectedRoom, out CharacterScaleRoomDefinition handleDraft))
        {
            float handlePreviewScale = CharacterScaleFunction.Evaluate(
                sampleY,
                handleDraft.FrontY,
                handleDraft.FrontScale,
                handleDraft.BackY,
                handleDraft.BackScale);
            EditorGUILayout.LabelField("Unsaved Handle Preview", handlePreviewScale.ToString("0.####"));
        }

        DrawValidationAndStatus();
    }

    private void DrawRoomEditor(CharacterScaleRoom roomHandlesForRoom)
    {
        EditorGUILayout.LabelField(roomHandlesForRoom.RoomName, EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Room Object", roomHandlesForRoom.Room, typeof(RoomContentGroup), true);
        EditorGUILayout.LabelField("Current Stage Zoom", roomHandlesForRoom.CurrentStageScale.ToString("0.########"));

        if (catalog.TryGetRoom(roomHandlesForRoom.RoomName, out CharacterScaleRoomDefinition saved))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Saved Front Y", saved.FrontY);
                EditorGUILayout.FloatField("Saved Front Scale", saved.FrontScale);
                EditorGUILayout.FloatField("Saved Back Y", saved.BackY);
                EditorGUILayout.FloatField("Saved Back Scale", saved.BackScale);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "This loaded room has no saved catalog record. Saving its handles will create one.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4f);
        DrawMarkerEditor("Front", roomHandlesForRoom, roomHandlesForRoom.FrontHandle);
        EditorGUILayout.Space(4f);
        DrawMarkerEditor("Back", roomHandlesForRoom, roomHandlesForRoom.BackHandle);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(saved == null))
            {
                if (GUILayout.Button("Load Asset Into Handles"))
                {
                    LoadAssetRoomIntoHandles(catalog, roomHandlesForRoom);
                    SetStatus($"Loaded saved {roomHandlesForRoom.RoomName} values into the editor handles.", MessageType.Info);
                }
            }

            if (GUILayout.Button("Save Handles To Asset"))
            {
                if (SaveRoomHandlesToCatalog(catalog, roomHandlesForRoom, true, out string report))
                {
                    SetStatus(report, MessageType.Info);
                }
                else
                {
                    SetStatus(report, MessageType.Error);
                }
            }
        }

        if (catalog.TryGetRoom(roomHandlesForRoom.RoomName, out saved) &&
            TryReadHandleDefinition(roomHandlesForRoom, out CharacterScaleRoomDefinition draft))
        {
            bool hasUnsavedChanges =
                !Mathf.Approximately(saved.FrontY, draft.FrontY) ||
                !Mathf.Approximately(saved.FrontScale, draft.FrontScale) ||
                !Mathf.Approximately(saved.BackY, draft.BackY) ||
                !Mathf.Approximately(saved.BackScale, draft.BackScale);

            EditorGUILayout.HelpBox(
                hasUnsavedChanges
                    ? "Handles differ from the saved asset. Runtime still uses the saved values."
                    : "Handles match the saved asset.",
                hasUnsavedChanges ? MessageType.Warning : MessageType.Info);
        }

        if (!roomHandlesForRoom.AreHandlesConfigured(out string reason))
        {
            EditorGUILayout.HelpBox(reason, MessageType.Error);
        }
    }

    private static void DrawMarkerEditor(string label, CharacterScaleRoom room, Transform marker)
    {
        EditorGUILayout.LabelField($"{label} Editor Handle", EditorStyles.miniBoldLabel);
        EditorGUILayout.ObjectField($"{label} Object", marker, typeof(Transform), true);

        if (marker == null)
        {
            return;
        }

        Vector2 oldPosition = room.GetHandleRoomLocalPosition(marker);
        Vector2 newPosition = EditorGUILayout.Vector2Field("Handle Position X / Y", oldPosition);
        float oldScale = room.GetHandleUniformScale(marker);
        float newScale = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Handle Scale", oldScale));

        if (newPosition != oldPosition || !Mathf.Approximately(newScale, oldScale))
        {
            Undo.RecordObject(marker, $"Edit Character Scale {label} Handle");
            marker.position = room.Room.transform.TransformPoint(new Vector3(newPosition.x, newPosition.y, 0f));
            marker.localScale = new Vector3(newScale, newScale, 1f);
            MarkSceneObjectDirty(marker);
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
                Undo.RecordObject(marker, $"Place Character Scale {label} Handle");
                Vector3 selectedRoomLocal = room.Room.transform.InverseTransformPoint(Selection.activeTransform.position);
                marker.position = room.Room.transform.TransformPoint(new Vector3(
                    selectedRoomLocal.x,
                    selectedRoomLocal.y,
                    0f));
                MarkSceneObjectDirty(marker);
            }
        }
    }

    private void DrawSceneHandles(SceneView sceneView)
    {
        if (catalog == null || roomHandles.Length == 0)
        {
            return;
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, roomHandles.Length - 1);
        CharacterScaleRoom room = roomHandles[selectedRoomIndex];
        DrawPositionHandle(room, room.FrontHandle, new Color(0.95f, 0.35f, 0.2f, 1f), $"{room.RoomName} Front");
        DrawPositionHandle(room, room.BackHandle, new Color(0.2f, 0.65f, 1f, 1f), $"{room.RoomName} Back");
    }

    private static void DrawPositionHandle(CharacterScaleRoom room, Transform marker, Color color, string label)
    {
        if (room == null || room.Room == null || marker == null)
        {
            return;
        }

        Handles.color = color;
        Handles.Label(marker.position, $"{label} handle  scale {Mathf.Abs(marker.localScale.x):0.###}");
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(marker.position, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(marker, $"Move {label} Handle");
            Vector3 roomLocal = room.Room.transform.InverseTransformPoint(newPosition);
            marker.position = room.Room.transform.TransformPoint(new Vector3(roomLocal.x, roomLocal.y, 0f));
            MarkSceneObjectDirty(marker);
        }
    }

    public static CharacterScaleCatalog CreateOrRepairMissingRoomSetup()
    {
        CharacterScaleCatalog catalogAsset = LoadOrCreateCatalogAsset();
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include)
            .Where(room => room != null && room.gameObject.scene.IsValid() && room.gameObject.scene.isLoaded)
            .GroupBy(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roomGroups.Length == 0)
        {
            Debug.LogError("Character Scale Tool could not find any loaded RoomContentGroup objects.");
            return catalogAsset;
        }

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];
            CharacterScaleRoom roomHandles = roomGroup.GetComponent<CharacterScaleRoom>();

            if (roomHandles == null)
            {
                roomHandles = Undo.AddComponent<CharacterScaleRoom>(roomGroup.gameObject);
            }

            Transform markerRoot = FindOrCreateChild(roomGroup.transform, MarkerRootName, out _);

            if (!markerRoot.CompareTag("EditorOnly"))
            {
                Undo.RecordObject(markerRoot.gameObject, "Mark Character Scale Handles Editor Only");
                markerRoot.gameObject.tag = "EditorOnly";
                MarkSceneObjectDirty(markerRoot.gameObject);
            }

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

            if (roomHandles.Room != roomGroup ||
                roomHandles.FrontHandle != front ||
                roomHandles.BackHandle != back)
            {
                Undo.RecordObject(roomHandles, "Repair Character Scale Editor Handles");
                roomHandles.ConfigureHandles(roomGroup, front, back);
                MarkSceneObjectDirty(roomHandles);
            }

        }

        Debug.Log(
            $"Character Scale editor handles now cover {roomGroups.Length} loaded rooms. " +
            "Runtime calibration remains in the catalog asset.",
            catalogAsset);
        return catalogAsset;
    }

    public static bool LoadAssetRoomIntoHandles(
        CharacterScaleCatalog catalogAsset,
        CharacterScaleRoom roomHandlesForRoom)
    {
        if (catalogAsset == null ||
            roomHandlesForRoom == null ||
            !catalogAsset.TryGetRoom(roomHandlesForRoom.RoomName, out CharacterScaleRoomDefinition definition) ||
            roomHandlesForRoom.FrontHandle == null ||
            roomHandlesForRoom.BackHandle == null)
        {
            return false;
        }

        Transform front = roomHandlesForRoom.FrontHandle;
        Transform back = roomHandlesForRoom.BackHandle;
        Undo.RecordObjects(new UnityEngine.Object[] { front, back }, "Load Character Scale Asset Into Handles");
        SetHandleValues(roomHandlesForRoom, front, definition.FrontY, definition.FrontScale);
        SetHandleValues(roomHandlesForRoom, back, definition.BackY, definition.BackScale);
        MarkSceneObjectDirty(front);
        MarkSceneObjectDirty(back);
        return true;
    }

    public static bool SaveRoomHandlesToCatalog(
        CharacterScaleCatalog catalogAsset,
        CharacterScaleRoom roomHandlesForRoom,
        bool persistAsset,
        out string report)
    {
        if (catalogAsset == null)
        {
            report = "Character Scale Catalog asset is missing.";
            return false;
        }

        if (!TryReadHandleDefinition(roomHandlesForRoom, out CharacterScaleRoomDefinition definition))
        {
            if (roomHandlesForRoom == null)
            {
                report = "Character scale room handles are missing.";
            }
            else if (!roomHandlesForRoom.AreHandlesConfigured(out string reason))
            {
                report = reason;
            }
            else
            {
                report = "Character scale handles could not be read.";
            }

            return false;
        }

        if (!definition.IsConfigured(out string definitionReason))
        {
            report = definitionReason;
            return false;
        }

        Undo.RecordObject(catalogAsset, $"Save {definition.RoomName} Character Scale");
        catalogAsset.SetRoom(definition);

        if (persistAsset)
        {
            SaveCatalogAsset(catalogAsset);
        }

        report = $"Saved {definition.RoomName} Front/Back calibration to {AssetDatabase.GetAssetPath(catalogAsset)}.";
        return true;
    }

    private static bool TryReadHandleDefinition(
        CharacterScaleRoom roomHandlesForRoom,
        out CharacterScaleRoomDefinition definition)
    {
        definition = null;

        if (roomHandlesForRoom == null || !roomHandlesForRoom.AreHandlesConfigured(out _))
        {
            return false;
        }

        definition = new CharacterScaleRoomDefinition(
            roomHandlesForRoom.RoomName,
            roomHandlesForRoom.GetHandleRoomLocalPosition(roomHandlesForRoom.FrontHandle).y,
            roomHandlesForRoom.GetHandleUniformScale(roomHandlesForRoom.FrontHandle),
            roomHandlesForRoom.GetHandleRoomLocalPosition(roomHandlesForRoom.BackHandle).y,
            roomHandlesForRoom.GetHandleUniformScale(roomHandlesForRoom.BackHandle));
        return true;
    }

    private static CharacterScaleCatalog LoadOrCreateCatalogAsset()
    {
        CharacterScaleCatalog existing = AssetDatabase.LoadAssetAtPath<CharacterScaleCatalog>(DefaultCatalogAssetPath);

        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        CharacterScaleCatalog created = CreateInstance<CharacterScaleCatalog>();
        AssetDatabase.CreateAsset(created, DefaultCatalogAssetPath);
        AssetDatabase.SaveAssetIfDirty(created);
        return created;
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
        if (marker != null)
        {
            marker.localPosition = new Vector3(defaultPosition.x, defaultPosition.y, 0f);
        }
    }

    private static void SetHandleValues(
        CharacterScaleRoom roomHandlesForRoom,
        Transform handle,
        float roomY,
        float scale)
    {
        Vector3 roomLocal = roomHandlesForRoom.Room.transform.InverseTransformPoint(handle.position);
        handle.position = roomHandlesForRoom.Room.transform.TransformPoint(new Vector3(roomLocal.x, roomY, 0f));
        handle.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SaveCatalogAsset(CharacterScaleCatalog catalogAsset)
    {
        if (catalogAsset == null)
        {
            return;
        }

        EditorUtility.SetDirty(catalogAsset);
        AssetDatabase.SaveAssetIfDirty(catalogAsset);
    }

    private void FindCatalogAsset()
    {
        catalog = AssetDatabase.LoadAssetAtPath<CharacterScaleCatalog>(DefaultCatalogAssetPath);
        SetStatus(
            catalog != null
                ? $"Character Scale Catalog asset found at {DefaultCatalogAssetPath}."
                : $"No Character Scale Catalog asset found at {DefaultCatalogAssetPath}.",
            catalog != null ? MessageType.Info : MessageType.Warning);
    }

    private void RefreshRoomHandles()
    {
        roomHandles = FindObjectsByType<CharacterScaleRoom>(FindObjectsInactive.Include)
            .Where(room => room != null && room.gameObject.scene.IsValid() && room.gameObject.scene.isLoaded)
            .GroupBy(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RefreshRoomHandlesIfNeeded()
    {
        if (roomHandles == null || roomHandles.Any(room => room == null))
        {
            RefreshRoomHandles();
        }
    }

    private void DrawValidationAndStatus()
    {
        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Catalog Asset"))
            {
                bool valid = catalog.ValidateCatalog(out string report);
                SetStatus(report, valid ? MessageType.Info : MessageType.Error);
            }

            if (GUILayout.Button("Save All Loaded Handles To Asset"))
            {
                List<CharacterScaleRoomDefinition> drafts = new List<CharacterScaleRoomDefinition>();
                List<string> failures = new List<string>();

                for (int i = 0; i < roomHandles.Length; i++)
                {
                    CharacterScaleRoom room = roomHandles[i];

                    if (!TryReadHandleDefinition(room, out CharacterScaleRoomDefinition draft))
                    {
                        string handleReason = "Character scale handles could not be read.";

                        if (room == null)
                        {
                            handleReason = "Character scale room handles are missing.";
                        }
                        else if (!room.AreHandlesConfigured(out string configuredReason))
                        {
                            handleReason = configuredReason;
                        }

                        failures.Add($"{(room != null ? room.RoomName : "Missing room")}: {handleReason}");
                        continue;
                    }

                    if (!draft.IsConfigured(out string definitionReason))
                    {
                        failures.Add($"{room.RoomName}: {definitionReason}");
                        continue;
                    }

                    drafts.Add(draft);
                }

                if (failures.Count == 0)
                {
                    Undo.RecordObject(catalog, "Save All Character Scale Rooms");

                    for (int i = 0; i < drafts.Count; i++)
                    {
                        catalog.SetRoom(drafts[i]);
                    }

                    SaveCatalogAsset(catalog);
                    SetStatus($"Saved {drafts.Count} loaded room calibrations to the catalog asset.", MessageType.Info);
                }
                else
                {
                    SetStatus(string.Join("\n", failures), MessageType.Error);
                }
            }
        }

        DrawStatus();
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

    private static void MarkSceneObjectDirty(UnityEngine.Object target)
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
