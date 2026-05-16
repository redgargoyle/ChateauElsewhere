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
        DoorButton[] doorButtons = FindSceneObjects<DoorButton>();
        RoomNavigationManager[] navigationManagers = FindSceneObjects<RoomNavigationManager>();
        CameraAreaController[] cameraAreas = FindSceneObjects<CameraAreaController>();
        RoomVisualCatalog[] visualCatalogs = FindVisualCatalogAssets();

        StringBuilder report = new StringBuilder();
        int issueCount = 0;

        AppendHeader(report, "Door Data Validation");
        issueCount += AppendMessages(report, "Parser Errors", parseResult.Errors);
        issueCount += AppendMessages(report, "Parser Warnings", parseResult.Warnings);

        issueCount += ValidateDestinations(report, parseResult);
        issueCount += ValidateDoorButtons(report, parseResult, doorButtons);
        issueCount += ValidateRoomVisuals(report, parseResult, navigationManagers, visualCatalogs, cameraAreas);
        issueCount += ValidateStartingRooms(report, parseResult, navigationManagers);

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

    private static int ValidateDoorButtons(StringBuilder report, DoorDataParseResult parseResult, DoorButton[] doorButtons)
    {
        int issues = 0;
        HashSet<string> buttonDoorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DoorButton> firstButtonByDoorId = new Dictionary<string, DoorButton>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < doorButtons.Length; i++)
        {
            DoorButton doorButton = doorButtons[i];
            string doorId = Clean(doorButton.DoorId);
            string roomName = Clean(doorButton.RoomName);

            if (string.IsNullOrEmpty(doorId))
            {
                AppendIssue(report, $"DoorButton '{doorButton.name}' has no door ID.");
                issues++;
                continue;
            }

            if (string.IsNullOrEmpty(roomName))
            {
                AppendIssue(report, $"DoorButton '{doorButton.name}' has no room name. Put it under Room_<RoomName> or fill the field.");
                issues++;
            }

            if (firstButtonByDoorId.TryGetValue(doorId, out DoorButton firstButton))
            {
                AppendIssue(report, $"Duplicate DoorButton ID '{doorId}' on '{firstButton.name}' and '{doorButton.name}'.");
                issues++;
            }
            else
            {
                firstButtonByDoorId.Add(doorId, doorButton);
                buttonDoorIds.Add(doorId);
            }

            if (!parseResult.RoutesByDoorId.ContainsKey(doorId))
            {
                AppendIssue(report, $"DoorButton '{doorButton.name}' uses '{doorId}', but that door is not in doors.txt.");
                issues++;
            }
            else
            {
                DoorRoute route = parseResult.RoutesByDoorId[doorId];

                if (!string.IsNullOrEmpty(roomName) &&
                    !string.Equals(roomName, route.SourceRoom, StringComparison.OrdinalIgnoreCase))
                {
                    AppendIssue(report, $"DoorButton '{doorButton.name}' is in '{roomName}', but door '{doorId}' belongs to '{route.SourceRoom}'.");
                    issues++;
                }
            }
        }

        foreach (DoorRoute route in parseResult.RoutesByDoorId.Values)
        {
            if (!buttonDoorIds.Contains(route.DoorId))
            {
                AppendIssue(report, $"Door data has '{route.DoorId}' in '{route.SourceRoom}', but no scene DoorButton uses that ID.");
                issues++;
            }
        }

        return issues;
    }

    private static int ValidateRoomVisuals(
        StringBuilder report,
        DoorDataParseResult parseResult,
        RoomNavigationManager[] navigationManagers,
        RoomVisualCatalog[] visualCatalogs,
        CameraAreaController[] cameraAreas)
    {
        int issues = 0;

        foreach (RoomDefinition room in parseResult.RoomsByName.Values)
        {
            if (HasRoomVisual(room.RoomName, navigationManagers, visualCatalogs, cameraAreas))
            {
                continue;
            }

            AppendIssue(report, $"Room '{room.RoomName}' has no background texture in a RoomVisualCatalog or legacy Cam_<RoomName> button.");
            issues++;
        }

        return issues;
    }

    private static int ValidateStartingRooms(
        StringBuilder report,
        DoorDataParseResult parseResult,
        RoomNavigationManager[] navigationManagers)
    {
        int issues = 0;

        if (navigationManagers.Length == 0)
        {
            if (!parseResult.RoomsByName.ContainsKey("StorageCloset"))
            {
                AppendIssue(report, "No scene RoomNavigationManager exists; runtime bootstrap will use default starting room 'StorageCloset', but doors.txt has no StorageCloset section.");
                issues++;
            }

            return issues;
        }

        for (int i = 0; i < navigationManagers.Length; i++)
        {
            RoomNavigationManager manager = navigationManagers[i];

            if (!parseResult.RoomsByName.ContainsKey(manager.StartingRoom))
            {
                AppendIssue(report, $"RoomNavigationManager '{manager.name}' starts in '{manager.StartingRoom}', but that room is not in doors.txt.");
                issues++;
            }
        }

        return issues;
    }

    private static bool HasRoomVisual(
        string roomName,
        RoomNavigationManager[] navigationManagers,
        RoomVisualCatalog[] visualCatalogs,
        CameraAreaController[] cameraAreas)
    {
        for (int i = 0; i < navigationManagers.Length; i++)
        {
            Texture texture = navigationManagers[i].FindRoomTexture(roomName);

            if (texture != null)
            {
                return true;
            }
        }

        for (int i = 0; i < visualCatalogs.Length; i++)
        {
            Texture texture;

            if (visualCatalogs[i] != null && visualCatalogs[i].TryGetRoomTexture(roomName, out texture))
            {
                return true;
            }
        }

        for (int i = 0; i < cameraAreas.Length; i++)
        {
            CameraAreaController cameraArea = cameraAreas[i];

            if (cameraArea == null || cameraArea.roomBackgroundTexture == null)
            {
                continue;
            }

            if (string.Equals(ParseRoomNameFromCameraArea(cameraArea.name), roomName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

        if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }

        return cleanName.Replace('_', ' ').Trim();
    }
}
