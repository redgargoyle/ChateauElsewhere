using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RoomNavigationManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TextAsset doorDataFile;
    [SerializeField] private string doorDataResourcePath = "Navigation/doors";
    [SerializeField] private string startingRoom = "StorageCloset";
    [SerializeField] private RoomVisualCatalog roomVisualCatalog;
    [SerializeField] private string roomVisualCatalogResourcePath = "Navigation/RoomVisualCatalog";
    [SerializeField] private DoorCameraSequence doorCameraSequence;
    [SerializeField] private string doorCameraSequenceResourcePath = "Navigation/DoorCameraSequence";

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private MapAnimator mapAnimator;
    [SerializeField] private Transform doorButtonRoot;

    [Header("Behavior")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool hideMapAfterDoorClick;
    [SerializeField] private bool applyStartingRoomVisualOnAwake;
    [SerializeField] private bool logNavigationWarnings = true;

    [Header("Events")]
    [SerializeField] private RoomChangedEvent onCurrentRoomChanged = new RoomChangedEvent();

    private Dictionary<string, RoomDefinition> roomsByName = new Dictionary<string, RoomDefinition>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DoorRoute> routesByDoorId = new Dictionary<string, DoorRoute>(StringComparer.OrdinalIgnoreCase);
    private DoorButton[] cachedDoorButtons = new DoorButton[0];
    private DoorTriggerNavigation[] cachedDoorTriggers = new DoorTriggerNavigation[0];
    private string currentRoom;

    public string CurrentRoom => currentRoom;
    public string StartingRoom => startingRoom;
    public RoomVisualCatalog VisualCatalog => roomVisualCatalog;
    public DoorCameraSequence CameraSequence => doorCameraSequence;
    public IReadOnlyDictionary<string, RoomDefinition> RoomsByName => roomsByName;
    public IReadOnlyDictionary<string, DoorRoute> RoutesByDoorId => routesByDoorId;
    public RoomChangedEvent OnCurrentRoomChanged => onCurrentRoomChanged;

    private void Awake()
    {
        ResolveReferences();
        RefreshDoorButtonCache();
        LoadLegacyDoorDataIfNeeded();
        SetCurrentRoom(GetInitialRoomName(), false, applyStartingRoomVisualOnAwake);
    }

    public bool ReloadDoorData()
    {
        bool loaded = LoadDoorData();

        if (loaded)
        {
            SetCurrentRoom(GetInitialRoomName(), false, applyStartingRoomVisualOnAwake);
        }

        return loaded;
    }

    public bool TryMoveThroughDoor(string doorId)
    {
        string cleanDoorId = Clean(doorId);

        if (string.IsNullOrEmpty(cleanDoorId))
        {
            Warn("Tried to move through an empty door ID.");
            return false;
        }

        if (!routesByDoorId.TryGetValue(cleanDoorId, out DoorRoute route))
        {
            Warn($"Door '{cleanDoorId}' is not present in the loaded door data.");
            return false;
        }

        if (!SameName(route.SourceRoom, currentRoom))
        {
            Warn($"Door '{cleanDoorId}' belongs to '{route.SourceRoom}', but the player is currently in '{currentRoom}'.");
            return false;
        }

        bool moved = SetCurrentRoom(route.DestinationRoom, true, true);

        if (moved && hideMapAfterDoorClick && mapAnimator != null)
        {
            mapAnimator.HideMap();
        }

        return moved;
    }

    public bool MoveToRoom(string roomName)
    {
        return SetCurrentRoom(roomName, false, true);
    }

    public bool OpenDoorFromCurrentRoom(string sourceRoom, string doorName, bool requirePlayerInSourceRoom)
    {
        string cleanSourceRoom = Clean(sourceRoom);
        string cleanDoorName = Clean(doorName);

        if (requirePlayerInSourceRoom &&
            !string.IsNullOrEmpty(cleanSourceRoom) &&
            !SameName(cleanSourceRoom, currentRoom))
        {
            Warn($"Door '{cleanDoorName}' belongs to '{cleanSourceRoom}', but the player is currently in '{currentRoom}'.");
            return false;
        }

        if (doorCameraSequence == null)
        {
            Warn("No DoorCameraSequence is assigned or loadable from Resources/Navigation/DoorCameraSequence.asset.");
            return false;
        }

        if (!doorCameraSequence.TryGetNextRoom(currentRoom, out string nextRoom))
        {
            Warn($"Door '{cleanDoorName}' could not find a next room after '{currentRoom}'.");
            return false;
        }

        bool moved = SetCurrentRoom(nextRoom, false, true);

        if (moved && hideMapAfterDoorClick && mapAnimator != null)
        {
            mapAnimator.HideMap();
        }

        return moved;
    }

    public bool SetCurrentRoomFromCameraArea(CameraAreaController cameraArea, bool applyRoomVisual)
    {
        if (cameraArea == null)
        {
            return false;
        }

        return SetCurrentRoom(ParseRoomNameFromCameraArea(cameraArea.name), false, applyRoomVisual);
    }

    public bool MoveThroughInspectorDoor(string sourceRoom, string doorName, string destinationRoom, bool requirePlayerInSourceRoom)
    {
        string cleanSourceRoom = Clean(sourceRoom);
        string cleanDoorName = Clean(doorName);
        string cleanDestinationRoom = Clean(destinationRoom);

        if (string.IsNullOrEmpty(cleanDestinationRoom))
        {
            Warn($"Door '{cleanDoorName}' has no destination room.");
            return false;
        }

        if (requirePlayerInSourceRoom &&
            !string.IsNullOrEmpty(cleanSourceRoom) &&
            !SameName(cleanSourceRoom, currentRoom))
        {
            Warn($"Door '{cleanDoorName}' belongs to '{cleanSourceRoom}', but the player is currently in '{currentRoom}'.");
            return false;
        }

        bool moved = SetCurrentRoom(cleanDestinationRoom, false, true);

        if (moved && hideMapAfterDoorClick && mapAnimator != null)
        {
            mapAnimator.HideMap();
        }

        return moved;
    }

    public bool HasDoor(string doorId)
    {
        return routesByDoorId.ContainsKey(Clean(doorId));
    }

    public bool HasRoom(string roomName)
    {
        return roomsByName.ContainsKey(Clean(roomName));
    }

    public Texture FindRoomTexture(string roomName)
    {
        Texture texture;
        return TryFindRoomTexture(roomName, out texture) ? texture : null;
    }

    public void RefreshDoorButtonCache()
    {
        if (doorButtonRoot != null)
        {
            cachedDoorButtons = doorButtonRoot.GetComponentsInChildren<DoorButton>(true);
            cachedDoorTriggers = doorButtonRoot.GetComponentsInChildren<DoorTriggerNavigation>(true);
            return;
        }

        cachedDoorButtons = FindObjectsOfType<DoorButton>(true);
        cachedDoorTriggers = FindObjectsOfType<DoorTriggerNavigation>(true);
    }

    private void LoadLegacyDoorDataIfNeeded()
    {
        if (cachedDoorButtons.Length == 0)
        {
            roomsByName = new Dictionary<string, RoomDefinition>(StringComparer.OrdinalIgnoreCase);
            routesByDoorId = new Dictionary<string, DoorRoute>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        LoadDoorData();
    }

    private bool LoadDoorData()
    {
        TextAsset data = doorDataFile != null ? doorDataFile : Resources.Load<TextAsset>(doorDataResourcePath);

        roomsByName = new Dictionary<string, RoomDefinition>(StringComparer.OrdinalIgnoreCase);
        routesByDoorId = new Dictionary<string, DoorRoute>(StringComparer.OrdinalIgnoreCase);

        if (data == null)
        {
            Warn($"Could not load door data. Expected Resources/{doorDataResourcePath}.txt or a directly assigned TextAsset.");
            return false;
        }

        DoorDataParseResult result = DoorDataParser.Parse(data.text);
        roomsByName = result.RoomsByName;
        routesByDoorId = result.RoutesByDoorId;

        for (int i = 0; i < result.Warnings.Count; i++)
        {
            Warn(result.Warnings[i]);
        }

        for (int i = 0; i < result.Errors.Count; i++)
        {
            Debug.LogError(result.Errors[i], this);
        }

        return result.IsValid;
    }

    private bool SetCurrentRoom(string roomName, bool requireKnownRoom, bool applyRoomVisual)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName))
        {
            Warn("Cannot move to an empty room name.");
            return false;
        }

        if (!roomsByName.TryGetValue(cleanRoomName, out RoomDefinition roomDefinition))
        {
            if (requireKnownRoom)
            {
                Warn($"Room '{cleanRoomName}' is not present in the loaded door data.");
                return false;
            }

            if (roomsByName.Count > 0)
            {
                Warn($"Room '{cleanRoomName}' is not present in the loaded door data.");
            }
        }

        currentRoom = roomDefinition != null ? roomDefinition.RoomName : cleanRoomName;

        if (applyRoomVisual)
        {
            ApplyRoomVisual(currentRoom);
        }

        RefreshDoorButtonsForCurrentRoom();
        onCurrentRoomChanged.Invoke(currentRoom);
        return true;
    }

    private void ApplyRoomVisual(string roomName)
    {
        ResolveReferences();

        if (cameraManager == null)
        {
            return;
        }

        Texture texture;

        if (TryFindRoomTexture(roomName, out texture))
        {
            cameraManager.SetRoomBackground(texture);
            return;
        }

        Warn($"No background texture found for room '{roomName}'. Add it to a RoomVisualCatalog.");
    }

    private bool TryFindRoomTexture(string roomName, out Texture texture)
    {
        texture = null;

        CameraAreaController[] legacyAreas = FindObjectsOfType<CameraAreaController>(true);

        for (int i = 0; i < legacyAreas.Length; i++)
        {
            CameraAreaController area = legacyAreas[i];

            if (area == null || area.roomBackgroundTexture == null)
            {
                continue;
            }

            string areaRoomName = ParseRoomNameFromCameraArea(area.name);

            if (SameName(areaRoomName, roomName))
            {
                texture = area.roomBackgroundTexture;
                return true;
            }
        }

        if (roomVisualCatalog != null && roomVisualCatalog.TryGetRoomTexture(roomName, out texture))
        {
            return true;
        }

        return false;
    }

    private void RefreshDoorButtonsForCurrentRoom()
    {
        RefreshDoorButtonCache();
        RefreshRoomGroups();

        for (int i = 0; i < cachedDoorButtons.Length; i++)
        {
            DoorButton doorButton = cachedDoorButtons[i];

            if (doorButton == null)
            {
                continue;
            }

            bool belongsToCurrentRoom = SameName(doorButton.RoomName, currentRoom);
            doorButton.gameObject.SetActive(belongsToCurrentRoom);
        }

        for (int i = 0; i < cachedDoorTriggers.Length; i++)
        {
            DoorTriggerNavigation doorTrigger = cachedDoorTriggers[i];

            if (doorTrigger == null)
            {
                continue;
            }

            if (doorTrigger.UsesCameraSequence)
            {
                bool sequenceHasCurrentRoom = doorCameraSequence == null ||
                    doorCameraSequence.ContainsRoom(currentRoom);
                doorTrigger.gameObject.SetActive(sequenceHasCurrentRoom);
                continue;
            }

            string sourceRoom = doorTrigger.SourceRoom;

            if (string.IsNullOrEmpty(sourceRoom))
            {
                continue;
            }

            bool belongsToCurrentRoom = SameName(sourceRoom, currentRoom);
            doorTrigger.gameObject.SetActive(belongsToCurrentRoom);
        }
    }

    private void RefreshRoomGroups()
    {
        if (doorButtonRoot == null)
        {
            return;
        }

        for (int i = 0; i < doorButtonRoot.childCount; i++)
        {
            Transform child = doorButtonRoot.GetChild(i);

            if (child == null || !child.name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string roomName = child.name.Substring("Room_".Length).Replace('_', ' ').Trim();
            child.gameObject.SetActive(SameName(roomName, currentRoom));
        }
    }

    private string GetInitialRoomName()
    {
        if (!string.IsNullOrWhiteSpace(startingRoom))
        {
            return startingRoom;
        }

        if (doorCameraSequence != null && !string.IsNullOrWhiteSpace(doorCameraSequence.StartingRoom))
        {
            return doorCameraSequence.StartingRoom;
        }

        foreach (KeyValuePair<string, RoomDefinition> pair in roomsByName)
        {
            return pair.Value.RoomName;
        }

        return string.Empty;
    }

    private void ResolveReferences()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }

        if (mapAnimator == null)
        {
            mapAnimator = FindObjectOfType<MapAnimator>();
        }

        if (roomVisualCatalog == null && !string.IsNullOrWhiteSpace(roomVisualCatalogResourcePath))
        {
            roomVisualCatalog = Resources.Load<RoomVisualCatalog>(roomVisualCatalogResourcePath);
        }

        if (doorCameraSequence == null && !string.IsNullOrWhiteSpace(doorCameraSequenceResourcePath))
        {
            doorCameraSequence = Resources.Load<DoorCameraSequence>(doorCameraSequenceResourcePath);
        }

        if (doorButtonRoot == null)
        {
            GameObject rootObject = GameObject.Find("DoorButtons");

            if (rootObject != null)
            {
                doorButtonRoot = rootObject.transform;
            }
        }
    }

    private void Warn(string message)
    {
        if (logNavigationWarnings)
        {
            Debug.LogWarning(message, this);
        }
    }

    private static bool SameName(string left, string right)
    {
        return string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

[Serializable]
public class RoomChangedEvent : UnityEvent<string>
{
}
