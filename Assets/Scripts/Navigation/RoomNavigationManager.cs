using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RoomNavigationManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TextAsset doorDataFile;
    [SerializeField] private string doorDataResourcePath = "Navigation/doors";
    [SerializeField] private string startingRoom = "Kitchen";
    [SerializeField] private RoomVisualCatalog roomVisualCatalog;
    [SerializeField] private string roomVisualCatalogResourcePath = "Navigation/RoomVisualCatalog";

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
    private string currentRoom;

    public string CurrentRoom => currentRoom;
    public string StartingRoom => startingRoom;
    public RoomVisualCatalog VisualCatalog => roomVisualCatalog;
    public IReadOnlyDictionary<string, RoomDefinition> RoomsByName => roomsByName;
    public IReadOnlyDictionary<string, DoorRoute> RoutesByDoorId => routesByDoorId;
    public RoomChangedEvent OnCurrentRoomChanged => onCurrentRoomChanged;

    private void Awake()
    {
        ResolveReferences();
        LoadDoorData();
        RefreshDoorButtonCache();
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
            return;
        }

        cachedDoorButtons = FindObjectsOfType<DoorButton>(true);
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
            Warn($"Room '{cleanRoomName}' is not present in the loaded door data.");

            if (requireKnownRoom)
            {
                return false;
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
