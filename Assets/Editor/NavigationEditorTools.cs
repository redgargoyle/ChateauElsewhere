using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class NavigationEditorTools
{
    private const string DoorDataAssetPath = "Assets/Resources/Navigation/doors.txt";
    private const string RoomVisualCatalogAssetPath = "Assets/Resources/Navigation/RoomVisualCatalog.asset";
    private const string AutoPreviewEditorPrefKey = "Dreadforge.Navigation.AutoPreviewSelectedCamera";
    private const string RoomRootName = "Rooms";
    private const string LegacyDoorTriggerEditRootName = "RoomDoorTriggers_Edit";

    [MenuItem("Dreadforge/Navigation/Sync Door Triggers From Door Data")]
    public static void SyncDoorTriggersFromDoorData()
    {
        SyncDoorTriggersFromDoorData(true);
    }

    [MenuItem("Dreadforge/Navigation/Preview Selected Camera For Door Editing")]
    public static void PreviewSelectedCameraForDoorEditing()
    {
        CameraAreaController cameraArea = FindSelectedCameraArea();

        if (cameraArea == null)
        {
            Debug.LogWarning("Select a Button_* object under Map, or one of its children, before previewing a room for door editing.");
            return;
        }

        PreviewCameraForDoorEditing(cameraArea);
    }

    [MenuItem("Dreadforge/Navigation/Preview Selected Camera For Door Editing", true)]
    private static bool CanPreviewSelectedCameraForDoorEditing()
    {
        return FindSelectedCameraArea() != null;
    }

    [MenuItem("Dreadforge/Navigation/Preview Selected Room For Door Editing")]
    public static void PreviewSelectedRoomForDoorEditing()
    {
        RoomContentGroup roomContentGroup = FindSelectedRoomContentGroup();

        if (roomContentGroup == null)
        {
            Debug.LogWarning("Select a Room_* object, its Doors child, or one of its DoorTrigger_* children before previewing a room for door editing.");
            return;
        }

        PreviewRoomForDoorEditing(roomContentGroup);
    }

    [MenuItem("Dreadforge/Navigation/Preview Selected Room For Door Editing", true)]
    private static bool CanPreviewSelectedRoomForDoorEditing()
    {
        return FindSelectedRoomContentGroup() != null;
    }

    [MenuItem("Dreadforge/Navigation/Auto Preview Selected Camera")]
    public static void ToggleAutoPreviewSelectedCamera()
    {
        bool enabled = !AutoPreviewSelectedCamera;
        AutoPreviewSelectedCamera = enabled;
        Debug.Log($"Navigation camera auto preview is now {(enabled ? "ON" : "OFF")}.");
    }

    [MenuItem("Dreadforge/Navigation/Auto Preview Selected Camera", true)]
    private static bool ValidateToggleAutoPreviewSelectedCamera()
    {
        Menu.SetChecked("Dreadforge/Navigation/Auto Preview Selected Camera", AutoPreviewSelectedCamera);
        return true;
    }

    [MenuItem("Dreadforge/Navigation/Show All Cameras For Door Editing")]
    public static void ShowAllCamerasForDoorEditing()
    {
        CameraAreaController[] cameraAreas = FindSceneObjects<CameraAreaController>();

        if (cameraAreas.Length == 0)
        {
            Debug.LogWarning("No CameraAreaController objects were found in the open scene.");
            return;
        }

        int undoGroup = BeginNavigationPreviewUndo("Show All Navigation Cameras");

        for (int i = 0; i < cameraAreas.Length; i++)
        {
            CameraAreaController cameraArea = cameraAreas[i];

            if (cameraArea == null)
            {
                continue;
            }

            EnsureAncestorsActive(cameraArea.transform);
            SetActiveWithUndo(cameraArea.gameObject, true);
            SetDoorTriggersActive(cameraArea.transform, true);
        }

        FinishNavigationPreviewUndo(undoGroup);
        MarkOpenScenesDirty();
        SceneView.RepaintAll();
        Debug.Log($"Activated {cameraAreas.Length} navigation camera object(s) for editing.");
    }

    [MenuItem("Dreadforge/Navigation/Validate Door Data")]
    public static void ValidateDoorData()
    {
        TextAsset doorData = AssetDatabase.LoadAssetAtPath<TextAsset>(DoorDataAssetPath);

        if (doorData == null)
        {
            Debug.LogError($"Door data file not found at {DoorDataAssetPath}.");
            return;
        }

        DoorDataParseResult parseResult = DoorDataParser.Parse(doorData.text);

        StringBuilder report = new StringBuilder();
        int issueCount = 0;

        AppendHeader(report, "Door Data Validation");
        report.AppendLine("Note: doors.txt is legacy/reference data. Scene DoorTriggerNavigation objects use their Inspector destinations and are not synced from this file.");
        report.AppendLine();
        issueCount += AppendMessages(report, "Parser Errors", parseResult.Errors);
        issueCount += AppendMessages(report, "Parser Warnings", parseResult.Warnings);
        issueCount += ValidateDestinations(report, parseResult);

        if (issueCount == 0)
        {
            report.AppendLine("OK: no issues found.");
            Debug.Log(report.ToString());
        }
        else
        {
            report.AppendLine();
            report.AppendLine($"Found {issueCount} issue(s).");
            Debug.LogWarning(report.ToString());
        }
    }

    [MenuItem("Dreadforge/Navigation/Generate Placeholder Door Data From Door Buttons")]
    public static void GeneratePlaceholderDoorDataFromDoorButtons()
    {
        DoorButton[] doorButtons = FindSceneObjects<DoorButton>();
        string text = doorButtons.Length > 0 ? BuildDoorDataFromDoorButtons(doorButtons) : BuildDefaultPlaceholderDoorData();

        bool shouldWrite = !File.Exists(DoorDataAssetPath) ||
            EditorUtility.DisplayDialog(
                "Overwrite Door Data?",
                $"{DoorDataAssetPath} already exists. Replace it with generated placeholder data?",
                "Replace",
                "Cancel");

        if (!shouldWrite)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DoorDataAssetPath));
        File.WriteAllText(DoorDataAssetPath, text);
        AssetDatabase.Refresh();
        Debug.Log($"Wrote placeholder door data to {DoorDataAssetPath}.");
    }

    [MenuItem("Dreadforge/Navigation/Create Room Visual Catalog From Map Buttons")]
    public static void CreateRoomVisualCatalogFromMapButtons()
    {
        bool shouldWrite = !File.Exists(RoomVisualCatalogAssetPath) ||
            EditorUtility.DisplayDialog(
                "Overwrite Room Visual Catalog?",
                $"{RoomVisualCatalogAssetPath} already exists. Replace it with a catalog generated from current map buttons?",
                "Replace",
                "Cancel");

        if (!shouldWrite)
        {
            return;
        }

        CameraAreaController[] cameraAreas = FindSceneObjects<CameraAreaController>();
        List<RoomVisualEntry> entries = new List<RoomVisualEntry>();
        HashSet<string> roomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < cameraAreas.Length; i++)
        {
            CameraAreaController area = cameraAreas[i];

            if (area == null || area.roomBackgroundTexture == null)
            {
                continue;
            }

            string roomName = ParseRoomNameFromCameraArea(area.name);

            if (string.IsNullOrEmpty(roomName) || roomNames.Contains(roomName))
            {
                continue;
            }

            roomNames.Add(roomName);
            entries.Add(new RoomVisualEntry
            {
                roomName = roomName,
                backgroundTexture = area.roomBackgroundTexture
            });
        }

        RoomVisualCatalog catalog = ScriptableObject.CreateInstance<RoomVisualCatalog>();
        catalog.rooms = entries.ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(RoomVisualCatalogAssetPath));

        if (File.Exists(RoomVisualCatalogAssetPath))
        {
            AssetDatabase.DeleteAsset(RoomVisualCatalogAssetPath);
        }

        AssetDatabase.CreateAsset(catalog, RoomVisualCatalogAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {RoomVisualCatalogAssetPath} with {entries.Count} room visual(s).");
    }

    [MenuItem("Dreadforge/Navigation/Create Door Button Placeholders From Door Data")]
    public static void CreateDoorButtonPlaceholdersFromDoorData()
    {
        TextAsset doorData = AssetDatabase.LoadAssetAtPath<TextAsset>(DoorDataAssetPath);

        if (doorData == null)
        {
            Debug.LogError($"Door data file not found at {DoorDataAssetPath}.");
            return;
        }

        DoorDataParseResult parseResult = DoorDataParser.Parse(doorData.text);

        if (!parseResult.IsValid)
        {
            Debug.LogError("Cannot create door button placeholders because doors.txt has parser errors. Run Dreadforge > Navigation > Validate Door Data.");
            return;
        }

        Canvas canvas = FindPreferredCanvas();

        if (canvas == null)
        {
            Debug.LogError("No Canvas found in the open scene. Open the game scene before creating door button placeholders.");
            return;
        }

        RectTransform doorRoot = FindOrCreateRectChild(canvas.transform, "DoorButtons");
        StretchToParent(doorRoot);

        int createdCount = 0;
        int existingCount = 0;

        foreach (RoomDefinition room in parseResult.RoomsByName.Values)
        {
            RectTransform roomRoot = FindOrCreateRectChild(doorRoot, $"Room_{SafeObjectName(room.RoomName)}");
            StretchToParent(roomRoot);

            int doorIndex = 0;

            foreach (DoorRoute route in room.Doors)
            {
                string doorObjectName = $"Door_{SafeObjectName(route.DoorId)}";
                Transform existingDoor = roomRoot.Find(doorObjectName);

                if (existingDoor != null)
                {
                    existingCount++;
                    EnsureDoorButtonComponents(existingDoor.gameObject);
                    doorIndex++;
                    continue;
                }

                GameObject doorObject = new GameObject(
                    doorObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

                Undo.RegisterCreatedObjectUndo(doorObject, "Create Door Button Placeholder");
                doorObject.transform.SetParent(roomRoot, false);

                RectTransform doorRect = doorObject.GetComponent<RectTransform>();
                ApplyPlaceholderDoorRect(doorRect, doorIndex, room.Doors.Count);

                Image image = doorObject.GetComponent<Image>();
                image.color = new Color(1f, 0.88f, 0.08f, 0.28f);
                image.raycastTarget = true;

                Button button = doorObject.GetComponent<Button>();
                button.targetGraphic = image;

                DoorButton doorButton = doorObject.AddComponent<DoorButton>();
                doorButton.RefreshInferredNames();

                createdCount++;
                doorIndex++;
            }
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = doorRoot.gameObject;
        Debug.Log($"Door button placeholders ready. Created {createdCount}, reused {existingCount}. Drag each yellow Door_* rectangle over its real door.");
    }

    public static void SyncDoorTriggersFromDoorData(bool logResult)
    {
        if (logResult)
        {
            Debug.LogWarning("Door trigger sync from doors.txt is disabled. Room_* objects and their DoorTrigger_* children are the source of truth; create and place doors manually in the selected room.");
        }
    }

    public static bool AutoPreviewSelectedCamera
    {
        get => EditorPrefs.GetBool(AutoPreviewEditorPrefKey, true);
        set => EditorPrefs.SetBool(AutoPreviewEditorPrefKey, value);
    }

    public static void PreviewCameraForDoorEditing(CameraAreaController cameraArea)
    {
        PreviewCameraForDoorEditing(cameraArea, true);
    }

    public static void PreviewRoomForDoorEditing(RoomContentGroup roomContentGroup)
    {
        PreviewRoomForDoorEditing(roomContentGroup, true);
    }

    public static void PreviewRoomForDoorEditing(RoomContentGroup roomContentGroup, bool pingRoom)
    {
        if (roomContentGroup == null)
        {
            return;
        }

        string roomName = Clean(roomContentGroup.RoomName);

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning($"Could not infer a room name from '{roomContentGroup.name}'. Set the RoomContentGroup room name before previewing.", roomContentGroup);
            return;
        }

        Texture roomTexture = FindRoomContentEditingTexture(roomContentGroup, roomName);
        int undoGroup = BeginNavigationPreviewUndo($"Preview {roomName} For Door Editing");
        PrepareRoomContentBackgroundForEditing(roomContentGroup, roomTexture);

        EnsureAncestorsActive(roomContentGroup.transform);

        RoomContentGroup[] roomContentGroups = FindRoomContentGroupsInSameEditingGroup(roomContentGroup);

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup otherRoomContentGroup = roomContentGroups[i];

            if (otherRoomContentGroup == null)
            {
                continue;
            }

            SetActiveWithUndo(otherRoomContentGroup.gameObject, otherRoomContentGroup == roomContentGroup);
        }

        int preparedTriggerCount = PrepareDoorTriggersForRoomContentEditing(roomContentGroup, roomName, roomTexture);

        if (pingRoom)
        {
            EditorGUIUtility.PingObject(roomContentGroup.gameObject);
        }

        FinishNavigationPreviewUndo(undoGroup);
        MarkOpenScenesDirty();
        SceneView.RepaintAll();
        Debug.Log($"Previewing '{roomName}' on the full room image. Showing {preparedTriggerCount} door trigger(s) under '{roomContentGroup.name}' for editing.");
    }

    public static void PreviewCameraForDoorEditing(CameraAreaController cameraArea, bool pingCamera)
    {
        if (cameraArea == null)
        {
            return;
        }

        string roomName = ParseRoomNameFromCameraArea(cameraArea.name);

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning($"Could not infer a room name from '{cameraArea.name}'. Use Button_<RoomName> under the Map object.", cameraArea);
            return;
        }

        CameraAreaController[] cameraAreas = FindCameraAreasInSameEditingGroup(cameraArea);
        int undoGroup = BeginNavigationPreviewUndo($"Preview {cameraArea.name} For Door Editing");
        RawImage backgroundImage = PrepareRoomBackgroundForEditing(cameraArea);

        // A camera object can be selected while inactive in the hierarchy.
        // Activating its ancestors first makes the selected room and its trigger
        // rectangles visible in the Scene view.
        EnsureAncestorsActive(cameraArea.transform);

        for (int i = 0; i < cameraAreas.Length; i++)
        {
            CameraAreaController otherCameraArea = cameraAreas[i];

            if (otherCameraArea == null)
            {
                continue;
            }

            bool shouldShow = otherCameraArea == cameraArea;
            SetActiveWithUndo(otherCameraArea.gameObject, shouldShow);
        }

        RoomDoorTriggerEditingInfo editingInfo = PrepareDoorTriggersForRoomEditing(cameraArea, roomName, backgroundImage);

        if (pingCamera)
        {
            EditorGUIUtility.PingObject(cameraArea.gameObject);
        }

        FinishNavigationPreviewUndo(undoGroup);
        MarkOpenScenesDirty();
        SceneView.RepaintAll();
        Debug.Log($"Previewing '{roomName}' on the full room background. Showing {editingInfo.PreparedTriggerCount} existing door trigger(s) for editing.");
    }

    private static int ValidateDestinations(StringBuilder report, DoorDataParseResult parseResult)
    {
        int issues = 0;

        foreach (DoorRoute route in parseResult.RoutesByDoorId.Values)
        {
            if (!parseResult.RoomsByName.ContainsKey(route.DestinationRoom))
            {
                AppendIssue(report, $"Destination room '{route.DestinationRoom}' from door '{route.DoorId}' has no room section.");
                issues++;
            }
        }

        return issues;
    }

    private static RoomVisualCatalog[] FindVisualCatalogAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:RoomVisualCatalog");
        List<RoomVisualCatalog> catalogs = new List<RoomVisualCatalog>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RoomVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<RoomVisualCatalog>(path);

            if (catalog != null)
            {
                catalogs.Add(catalog);
            }
        }

        return catalogs.ToArray();
    }

    private static Canvas FindPreferredCanvas()
    {
        Canvas[] canvases = FindSceneObjects<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == "Canvas_Background")
            {
                return canvases[i];
            }
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == "Canvas_NightManager")
            {
                return canvases[i];
            }
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    public static CameraAreaController FindSelectedCameraArea()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            return null;
        }

        CameraAreaController directCameraArea = selectedObject.GetComponent<CameraAreaController>();

        if (directCameraArea != null)
        {
            return directCameraArea;
        }

        CameraAreaController[] parentCameraAreas = selectedObject.GetComponentsInParent<CameraAreaController>(true);

        if (parentCameraAreas.Length > 0)
        {
            return parentCameraAreas[0];
        }

        DoorTriggerNavigation selectedDoorTrigger = selectedObject.GetComponentInParent<DoorTriggerNavigation>(true);

        if (selectedDoorTrigger != null)
        {
            return FindCameraAreaForRoom(selectedDoorTrigger.SourceRoom);
        }

        return null;
    }

    public static RoomContentGroup FindSelectedRoomContentGroup()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            return null;
        }

        RoomContentGroup directRoomContentGroup = selectedObject.GetComponent<RoomContentGroup>();

        if (directRoomContentGroup != null)
        {
            return directRoomContentGroup;
        }

        RoomContentGroup[] parentRoomContentGroups = selectedObject.GetComponentsInParent<RoomContentGroup>(true);

        if (parentRoomContentGroups.Length > 0)
        {
            return parentRoomContentGroups[0];
        }

        DoorTriggerNavigation selectedDoorTrigger = selectedObject.GetComponentInParent<DoorTriggerNavigation>(true);

        if (selectedDoorTrigger != null)
        {
            return FindRoomContentGroupForRoom(selectedDoorTrigger.SourceRoom);
        }

        return null;
    }

    private static RoomContentGroup[] FindRoomContentGroupsInSameEditingGroup(RoomContentGroup roomContentGroup)
    {
        if (roomContentGroup != null && roomContentGroup.transform.parent != null)
        {
            RoomContentGroup[] siblingRoomContentGroups = roomContentGroup.transform.parent.GetComponentsInChildren<RoomContentGroup>(true);

            if (siblingRoomContentGroups.Length > 0)
            {
                return siblingRoomContentGroups;
            }
        }

        return FindSceneObjects<RoomContentGroup>();
    }

    private static RoomContentGroup FindRoomContentGroupForRoom(string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName))
        {
            return null;
        }

        RoomContentGroup[] roomContentGroups = FindSceneObjects<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (roomContentGroup != null &&
                string.Equals(Clean(roomContentGroup.RoomName), cleanRoomName, StringComparison.OrdinalIgnoreCase))
            {
                return roomContentGroup;
            }
        }

        return null;
    }

    private static CameraAreaController[] FindCameraAreasInSameEditingGroup(CameraAreaController cameraArea)
    {
        if (cameraArea != null && cameraArea.transform.parent != null)
        {
            CameraAreaController[] siblingCameraAreas = cameraArea.transform.parent.GetComponentsInChildren<CameraAreaController>(true);

            if (siblingCameraAreas.Length > 0)
            {
                return siblingCameraAreas;
            }
        }

        return FindSceneObjects<CameraAreaController>();
    }

    private static CameraAreaController FindCameraAreaForRoom(string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName))
        {
            return null;
        }

        CameraAreaController[] cameraAreas = FindSceneObjects<CameraAreaController>();

        for (int i = 0; i < cameraAreas.Length; i++)
        {
            CameraAreaController cameraArea = cameraAreas[i];

            if (cameraArea != null &&
                string.Equals(ParseRoomNameFromCameraArea(cameraArea.name), cleanRoomName, StringComparison.OrdinalIgnoreCase))
            {
                return cameraArea;
            }
        }

        return null;
    }

    private static RawImage PrepareRoomBackgroundForEditing(CameraAreaController cameraArea)
    {
        RawImage backgroundImage = FindCameraBackgroundImage();

        if (backgroundImage == null)
        {
            Debug.LogWarning("Could not find the CameraManager background RawImage. The door triggers were still prepared, but no room image could be shown.", cameraArea);
            return null;
        }

        EnsureAncestorsActive(backgroundImage.transform);
        SetActiveWithUndo(backgroundImage.gameObject, true);
        Undo.RecordObject(backgroundImage, "Preview Room Background");

        CameraManager cameraManager = FindCameraManagerForBackground(backgroundImage);
        Texture roomTexture = cameraArea != null ? cameraArea.GetEffectiveRoomBackgroundTexture() : null;

        if (cameraManager != null && roomTexture != null)
        {
            Undo.RecordObject(cameraManager, "Preview Room Background");
            cameraManager.PreviewRoomBackground(roomTexture);
        }
        else
        {
            backgroundImage.texture = roomTexture;
            backgroundImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        EditorUtility.SetDirty(backgroundImage);

        RectTransform backgroundRect = backgroundImage.transform as RectTransform;

        if (backgroundRect != null)
        {
            FitToTextureWithUndo(backgroundRect, roomTexture, "Fit Room Background To Source Image");
        }

        return backgroundImage;
    }

    private static RawImage PrepareRoomContentBackgroundForEditing(RoomContentGroup roomContentGroup, Texture roomTexture)
    {
        RawImage backgroundImage = FindCameraBackgroundImage();

        if (backgroundImage == null)
        {
            Debug.LogWarning("Could not find the CameraManager background RawImage. The room and door triggers were still shown, but no room image could be previewed.", roomContentGroup);
            return null;
        }

        string roomName = Clean(roomContentGroup.RoomName);
        if (roomTexture == null)
        {
            Debug.LogWarning($"Room '{roomName}' has no background texture assigned.", roomContentGroup);
        }

        EnsureAncestorsActive(backgroundImage.transform);
        SetActiveWithUndo(backgroundImage.gameObject, true);
        Undo.RecordObject(backgroundImage, "Preview Room Background");

        CameraManager cameraManager = FindCameraManagerForBackground(backgroundImage);

        if (cameraManager != null && roomTexture != null)
        {
            Undo.RecordObject(cameraManager, "Preview Full Room Image");
            cameraManager.PreviewFullRoomImageForDoorEditing(roomTexture);
        }
        else
        {
            backgroundImage.texture = roomTexture;
            backgroundImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        EditorUtility.SetDirty(backgroundImage);

        RectTransform backgroundRect = backgroundImage.transform as RectTransform;

        if (backgroundRect != null)
        {
            FitToTextureWithUndo(backgroundRect, roomTexture, "Fit Room Background To Source Image");
        }

        return backgroundImage;
    }

    private static Texture FindRoomContentEditingTexture(RoomContentGroup roomContentGroup, string roomName)
    {
        Texture roomTexture = roomContentGroup != null ? roomContentGroup.RoomBackgroundTexture : null;

        if (roomTexture == null && TryFindRoomBackgroundTexture(roomName, out Texture catalogTexture))
        {
            roomTexture = catalogTexture;
        }

        return roomTexture;
    }

    private static CameraManager FindCameraManagerForBackground(RawImage backgroundImage)
    {
        CameraManager[] cameraManagers = FindSceneObjects<CameraManager>();

        for (int i = 0; i < cameraManagers.Length; i++)
        {
            CameraManager cameraManager = cameraManagers[i];

            if (cameraManager != null && cameraManager.cameraBackground == backgroundImage)
            {
                return cameraManager;
            }
        }

        return cameraManagers.Length > 0 ? cameraManagers[0] : null;
    }

    private static CameraManager FindSceneCameraManager()
    {
        CameraManager[] cameraManagers = FindSceneObjects<CameraManager>();

        for (int i = 0; i < cameraManagers.Length; i++)
        {
            if (cameraManagers[i] != null && cameraManagers[i].cameraBackground != null)
            {
                return cameraManagers[i];
            }
        }

        return cameraManagers.Length > 0 ? cameraManagers[0] : null;
    }

    private static RawImage FindCameraBackgroundImage()
    {
        CameraManager[] cameraManagers = FindSceneObjects<CameraManager>();

        for (int i = 0; i < cameraManagers.Length; i++)
        {
            if (cameraManagers[i] != null && cameraManagers[i].cameraBackground != null)
            {
                return cameraManagers[i].cameraBackground;
            }
        }

        RawImage[] images = FindSceneObjects<RawImage>();

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].name == "RawImage")
            {
                return images[i];
            }
        }

        return images.Length > 0 ? images[0] : null;
    }

    private static RoomDoorTriggerEditingInfo PrepareDoorTriggersForRoomEditing(
        CameraAreaController cameraArea,
        string roomName,
        RawImage backgroundImage)
    {
        RectTransform editRoot = FindOrCreateDoorTriggerEditRoot(backgroundImage, cameraArea);

        if (editRoot == null)
        {
            return new RoomDoorTriggerEditingInfo("(no editing layer)", 0);
        }

        Texture roomTexture = cameraArea != null ? cameraArea.GetEffectiveRoomBackgroundTexture() : null;
        RectTransform roomGroup = FindOrCreateRectChild(editRoot, $"Room_{SafeObjectName(roomName)}");
        FitToTextureWithUndo(roomGroup, roomTexture, "Fit Room Editing Layer To Source Image");
        SetActiveWithUndo(roomGroup.gameObject, true);
        EnsureRoomContentGroup(roomGroup, roomName);
        FindOrCreateDoorsRoot(roomGroup);

        for (int i = 0; i < editRoot.childCount; i++)
        {
            Transform child = editRoot.GetChild(i);

            if (child != null)
            {
                SetActiveWithUndo(child.gameObject, true);
            }
        }

        List<DoorTriggerNavigation> triggers = new List<DoorTriggerNavigation>();
        triggers.AddRange(cameraArea.GetComponentsInChildren<DoorTriggerNavigation>(true));
        DoorTriggerNavigation[] existingTriggers = editRoot.GetComponentsInChildren<DoorTriggerNavigation>(true);

        for (int i = 0; i < existingTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = existingTriggers[i];

            if (trigger != null &&
                string.Equals(trigger.SourceRoom, roomName, StringComparison.OrdinalIgnoreCase) &&
                !triggers.Contains(trigger))
            {
                triggers.Add(trigger);
            }
        }

        for (int i = 0; i < existingTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = existingTriggers[i];

            if (trigger != null && !triggers.Contains(trigger))
            {
                SetActiveWithUndo(trigger.gameObject, false);
            }
        }

        for (int i = 0; i < triggers.Count; i++)
        {
            PrepareDoorTriggerForEditing(triggers[i], roomName);
        }

        return new RoomDoorTriggerEditingInfo(roomGroup.name, triggers.Count);
    }

    private static RectTransform FindOrCreateDoorTriggerEditRoot(RawImage backgroundImage, CameraAreaController cameraArea)
    {
        Transform parent = null;

        if (backgroundImage != null && backgroundImage.transform.parent != null)
        {
            parent = backgroundImage.transform.parent;
        }
        else if (cameraArea != null)
        {
            Canvas canvas = cameraArea.GetComponentInParent<Canvas>(true);
            parent = canvas != null ? canvas.transform : cameraArea.transform.parent;
        }

        if (parent == null)
        {
            parent = FindPreferredCanvas()?.transform;
        }

        if (parent == null)
        {
            Debug.LogWarning("Could not find a Canvas for the room door trigger editing layer.");
            return null;
        }

        RectTransform editRoot = FindOrCreateRectChild(parent, RoomRootName);
        StretchToParentWithUndo(editRoot);
        SetActiveWithUndo(editRoot.gameObject, true);
        return editRoot;
    }

    private static void EnsureRoomContentGroup(RectTransform roomGroup, string roomName)
    {
        if (roomGroup == null)
        {
            return;
        }

        RoomContentGroup contentGroup = roomGroup.GetComponent<RoomContentGroup>();

        if (contentGroup == null)
        {
            contentGroup = Undo.AddComponent<RoomContentGroup>(roomGroup.gameObject);
        }
        else
        {
            Undo.RecordObject(contentGroup, "Configure Room Object");
        }

        contentGroup.SetRoomName(roomName);

        if (contentGroup.RoomBackgroundTexture == null && TryFindRoomBackgroundTexture(roomName, out Texture texture))
        {
            contentGroup.SetRoomBackgroundTexture(texture);
        }

        EditorUtility.SetDirty(contentGroup);
    }

    private static RectTransform FindOrCreateDoorsRoot(RectTransform roomGroup)
    {
        RectTransform doorsRoot = FindOrCreateRectChild(roomGroup, "Doors");
        StretchToParentWithUndo(doorsRoot);
        SetActiveWithUndo(doorsRoot.gameObject, true);
        return doorsRoot;
    }

    private static bool TryFindRoomBackgroundTexture(string roomName, out Texture texture)
    {
        texture = null;

        CameraAreaController cameraArea = FindCameraAreaForRoom(roomName);

        if (cameraArea != null)
        {
            texture = cameraArea.GetEffectiveRoomBackgroundTexture();

            if (texture != null)
            {
                return true;
            }
        }

        RoomVisualCatalog[] visualCatalogs = FindVisualCatalogAssets();

        for (int i = 0; i < visualCatalogs.Length; i++)
        {
            if (visualCatalogs[i] != null && visualCatalogs[i].TryGetRoomTexture(roomName, out texture))
            {
                return texture != null;
            }
        }

        return false;
    }

    private static void PrepareDoorTriggerForEditing(DoorTriggerNavigation trigger, string roomName)
    {
        if (trigger == null)
        {
            return;
        }

        trigger.RefreshInferredSourceRoom();

        SetActiveWithUndo(trigger.gameObject, true);

        RectTransform triggerRect = trigger.transform as RectTransform;

        if (triggerRect != null)
        {
            Undo.RecordObject(triggerRect, "Prepare Door Trigger Rect");

            if (triggerRect.anchorMin == triggerRect.anchorMax)
            {
                triggerRect.anchorMin = new Vector2(0.5f, 0.5f);
                triggerRect.anchorMax = new Vector2(0.5f, 0.5f);
            }

            if (triggerRect.sizeDelta.x <= 1f || triggerRect.sizeDelta.y <= 1f)
            {
                triggerRect.sizeDelta = new Vector2(120f, 120f);
                triggerRect.localScale = Vector3.one;
            }

            EditorUtility.SetDirty(triggerRect);
        }

        Image image = trigger.GetComponent<Image>();

        if (image != null)
        {
            Undo.RecordObject(image, "Make Door Trigger Visible In Editor");
            image.color = new Color(1f, 0f, 0f, 0.35f);
            image.raycastTarget = true;
            EditorUtility.SetDirty(image);
        }

        EditorUtility.SetDirty(trigger);
    }

    private static int PrepareDoorTriggersForRoomContentEditing(
        RoomContentGroup roomContentGroup,
        string roomName,
        Texture roomTexture)
    {
        RectTransform roomRect = roomContentGroup.transform as RectTransform;

        if (roomRect != null)
        {
            FitToTextureWithUndo(roomRect, roomTexture, "Fit Room Editing Layer To Source Image");
        }

        if (roomRect != null)
        {
            FindOrCreateDoorsRoot(roomRect);
        }

        DoorTriggerNavigation[] doorTriggers = roomContentGroup.GetComponentsInChildren<DoorTriggerNavigation>(true);
        int preparedCount = 0;

        for (int i = 0; i < doorTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = doorTriggers[i];

            if (trigger == null)
            {
                continue;
            }

            PrepareDoorTriggerForEditing(trigger, roomName);
            preparedCount++;
        }

        return preparedCount;
    }

    private static int BeginNavigationPreviewUndo(string undoName)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        return Undo.GetCurrentGroup();
    }

    private static void FinishNavigationPreviewUndo(int undoGroup)
    {
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void EnsureAncestorsActive(Transform transform)
    {
        while (transform != null)
        {
            SetActiveWithUndo(transform.gameObject, true);
            transform = transform.parent;
        }
    }

    private static void SetDoorTriggersActive(Transform root, bool active)
    {
        if (root == null)
        {
            return;
        }

        DoorTriggerNavigation[] doorTriggers = root.GetComponentsInChildren<DoorTriggerNavigation>(true);

        for (int i = 0; i < doorTriggers.Length; i++)
        {
            DoorTriggerNavigation doorTrigger = doorTriggers[i];

            if (doorTrigger != null)
            {
                SetActiveWithUndo(doorTrigger.gameObject, active);
            }
        }
    }

    private static void StretchToParentWithUndo(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        Undo.RecordObject(rectTransform, "Stretch Navigation UI Group");
        StretchToParent(rectTransform);
        EditorUtility.SetDirty(rectTransform);
    }

    private static void FitToTextureWithUndo(RectTransform rectTransform, Texture texture, string undoName)
    {
        if (rectTransform == null)
        {
            return;
        }

        if (texture == null || texture.width <= 0 || texture.height <= 0)
        {
            StretchToParentWithUndo(rectTransform);
            return;
        }

        Undo.RecordObject(rectTransform, undoName);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(texture.width, texture.height);
        rectTransform.localScale = Vector3.one;
        EditorUtility.SetDirty(rectTransform);
    }

    private static void SetActiveWithUndo(GameObject gameObject, bool active)
    {
        if (gameObject == null || gameObject.activeSelf == active)
        {
            return;
        }

        Undo.RecordObject(gameObject, active ? "Activate Navigation Object" : "Deactivate Navigation Object");
        gameObject.SetActive(active);
        EditorUtility.SetDirty(gameObject);
    }

    private static void MarkOpenScenesDirty()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));
        }
    }

    private static RectTransform FindOrCreateRectChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);

        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;

            if (existingRect != null)
            {
                return existingRect;
            }
        }

        GameObject childObject = new GameObject(childName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(childObject, "Create Navigation UI Group");
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private static void EnsureDoorButtonComponents(GameObject doorObject)
    {
        Image image = doorObject.GetComponent<Image>();

        if (image == null)
        {
            image = doorObject.AddComponent<Image>();
        }

        image.color = new Color(1f, 0.88f, 0.08f, 0.28f);
        image.raycastTarget = true;

        Button button = doorObject.GetComponent<Button>();

        if (button == null)
        {
            button = doorObject.AddComponent<Button>();
        }

        button.targetGraphic = image;

        DoorButton doorButton = doorObject.GetComponent<DoorButton>();

        if (doorButton == null)
        {
            doorButton = doorObject.AddComponent<DoorButton>();
        }

        doorButton.RefreshInferredNames();
        EditorUtility.SetDirty(doorObject);
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }

    private static void ApplyPlaceholderDoorRect(RectTransform rectTransform, int index, int count)
    {
        int safeCount = Mathf.Max(1, count);
        float width = 0.14f;
        float height = 0.22f;
        float step = 0.72f / safeCount;
        float centerX = 0.14f + step * index + step * 0.5f;
        float centerY = 0.5f;

        rectTransform.anchorMin = new Vector2(centerX - width * 0.5f, centerY - height * 0.5f);
        rectTransform.anchorMax = new Vector2(centerX + width * 0.5f, centerY + height * 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
    }

    private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        List<T> sceneObjects = new List<T>();

        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];

            if (candidate == null || EditorUtility.IsPersistent(candidate))
            {
                continue;
            }

            sceneObjects.Add(candidate);
        }

        return sceneObjects.ToArray();
    }

    private static bool IsNearlyZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f;
    }

    private static string BuildDoorDataFromDoorButtons(DoorButton[] doorButtons)
    {
        SortedDictionary<string, SortedSet<string>> doorsByRoom =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < doorButtons.Length; i++)
        {
            string roomName = Clean(doorButtons[i].RoomName);
            string doorId = Clean(doorButtons[i].DoorId);

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(doorId))
            {
                continue;
            }

            if (!doorsByRoom.TryGetValue(roomName, out SortedSet<string> doorIds))
            {
                doorIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                doorsByRoom.Add(roomName, doorIds);
            }

            doorIds.Add(doorId);
        }

        if (doorsByRoom.Count == 0)
        {
            return BuildDefaultPlaceholderDoorData();
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Generated placeholder data. Replace TODO_DESTINATION with real room names.");
        builder.AppendLine();

        foreach (KeyValuePair<string, SortedSet<string>> pair in doorsByRoom)
        {
            builder.AppendLine($"{pair.Key}:");

            foreach (string doorId in pair.Value)
            {
                builder.AppendLine($"{doorId}: TODO_DESTINATION");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildDefaultPlaceholderDoorData()
    {
        return
            "# Placeholder room data. Replace this file with Hamza's final floorplan data.\n" +
            "# Format:\n" +
            "# Current Room:\n" +
            "# DoorId: Destination Room\n\n" +
            "Kitchen:\n" +
            "K1: Ballroom\n" +
            "K2: Hallway\n\n" +
            "Study:\n" +
            "S1: Ballroom\n" +
            "S2: Kitchen\n\n" +
            "Ballroom:\n" +
            "B1: Kitchen\n" +
            "B2: Study\n\n" +
            "Hallway:\n" +
            "H1: Kitchen\n" +
            "H2: Study\n";
    }

    private static int AppendMessages(StringBuilder report, string title, List<string> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return 0;
        }

        AppendHeader(report, title);

        for (int i = 0; i < messages.Count; i++)
        {
            AppendIssue(report, messages[i]);
        }

        return messages.Count;
    }

    private static void AppendHeader(StringBuilder report, string title)
    {
        report.AppendLine(title);
        report.AppendLine(new string('-', title.Length));
    }

    private static void AppendIssue(StringBuilder report, string message)
    {
        report.AppendLine($"- {message}");
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string SafeObjectName(string value)
    {
        string cleanValue = Clean(value);

        if (string.IsNullOrEmpty(cleanValue))
        {
            return "Unnamed";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            cleanValue = cleanValue.Replace(invalidChar, '_');
        }

        return cleanValue.Replace(' ', '_');
    }

    private static string ParseRoomNameFromCameraArea(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        if (cleanName.StartsWith("Button_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Button_".Length);
        }
        else if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }

        return cleanName.Replace('_', ' ').Trim();
    }

    private readonly struct RoomDoorTriggerEditingInfo
    {
        public RoomDoorTriggerEditingInfo(string roomGroupName, int preparedTriggerCount)
        {
            RoomGroupName = roomGroupName;
            PreparedTriggerCount = preparedTriggerCount;
        }

        public string RoomGroupName { get; }
        public int PreparedTriggerCount { get; }
    }
}

[InitializeOnLoad]
public static class NavigationSelectionAutoPreview
{
    private static bool isPreviewingSelection;

    static NavigationSelectionAutoPreview()
    {
        Selection.selectionChanged += HandleSelectionChanged;
    }

    private static void HandleSelectionChanged()
    {
        if (!NavigationEditorTools.AutoPreviewSelectedCamera ||
            isPreviewingSelection ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RoomContentGroup roomContentGroup = NavigationEditorTools.FindSelectedRoomContentGroup();

        if (roomContentGroup != null)
        {
            EditorApplication.delayCall += () =>
            {
                if (roomContentGroup == null || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                isPreviewingSelection = true;

                try
                {
                    NavigationEditorTools.PreviewRoomForDoorEditing(roomContentGroup, false);
                }
                finally
                {
                    isPreviewingSelection = false;
                }
            };

            return;
        }

        CameraAreaController cameraArea = NavigationEditorTools.FindSelectedCameraArea();

        if (cameraArea == null)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (cameraArea == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            isPreviewingSelection = true;

            try
            {
                NavigationEditorTools.PreviewCameraForDoorEditing(cameraArea, false);
            }
            finally
            {
                isPreviewingSelection = false;
            }
        };
    }
}

[CustomEditor(typeof(CameraAreaController))]
public class CameraAreaControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (Application.isPlaying)
        {
            return;
        }

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("Preview This Camera For Door Editing"))
        {
            NavigationEditorTools.PreviewCameraForDoorEditing((CameraAreaController)target);
        }

        if (GUILayout.Button("Open Room Image Shader Controls"))
        {
            NavigationRoomImageShaderControlsWindow.OpenWindow();
        }

        if (GUILayout.Button("Show All Cameras For Door Editing"))
        {
            NavigationEditorTools.ShowAllCamerasForDoorEditing();
        }
    }
}
