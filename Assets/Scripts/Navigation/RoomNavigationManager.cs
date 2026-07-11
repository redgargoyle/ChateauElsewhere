using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoomNavigationManager : Chateau.Architecture.GameServiceBase
{
    private const string RoomRootName = "Rooms";
    private const string LegacyRoomObjectsRootName = "RoomObjects";
    private const string DoorButtonsRootName = "DoorButtons";
    private const string LegacyDoorTriggerEditRootName = "RoomDoorTriggers_Edit";
    private const string LegacyDoorTriggerRootName = "DoorTriggerParent";

    [Header("Data")]
    [SerializeField] private TextAsset doorDataFile;
    [SerializeField] private string doorDataResourcePath = "Navigation/doors";
    [SerializeField] private string startingRoom = "Grand Entrance Hall";
    [SerializeField] private RoomVisualCatalog roomVisualCatalog;
    [SerializeField] private string roomVisualCatalogResourcePath = "Navigation/RoomVisualCatalog";
    [SerializeField] private DoorCameraSequence doorCameraSequence;
    [SerializeField] private string doorCameraSequenceResourcePath = "Navigation/DoorCameraSequence";

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform doorButtonRoot;
    [SerializeField] private Transform roomContentRoot;

    [Header("Behavior")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool applyStartingRoomVisualOnAwake = true;
    [SerializeField] private bool logNavigationWarnings = true;
    [SerializeField] private bool runNavigationSelfCheck = true;

    [Header("Status HUD")]
    [SerializeField] private bool showCurrentRoomHud = true;
    [SerializeField] private string currentRoomHudName = "Text_CurrentRoom";
    [SerializeField] private string currentRoomHudPrefix = "Current Room: ";
    [SerializeField] private Vector2 currentRoomHudSize = new Vector2(360f, 44f);
    [SerializeField] private Vector2 currentRoomHudOffset = new Vector2(-22f, 18f);
    [SerializeField] private float currentRoomHudFontSize = 24f;
    [SerializeField] private Color currentRoomHudColor = Color.white;

    [Header("Events")]
    [SerializeField] private RoomChangedEvent onCurrentRoomChanged = new RoomChangedEvent();

    private Dictionary<string, RoomDefinition> roomsByName = new Dictionary<string, RoomDefinition>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DoorRoute> routesByDoorId = new Dictionary<string, DoorRoute>(StringComparer.OrdinalIgnoreCase);
    private DoorButton[] cachedDoorButtons = new DoorButton[0];
    private DoorTriggerNavigation[] cachedDoorTriggers = new DoorTriggerNavigation[0];
    private RoomContentGroup[] cachedRoomContentGroups = new RoomContentGroup[0];
    private string currentRoom;
    private bool applyVisualForNextRoomChange;
    private TMP_Text currentRoomHudText;
    [SerializeField] private RuntimeSettingsMenu runtimeSettingsMenu;
    private FireplaceAmbienceController fireplaceAmbienceController;
    private ClockTickingAmbienceController clockTickingAmbienceController;

    public string CurrentRoom => currentRoom;
    public string StartingRoom => startingRoom;
    public RoomVisualCatalog VisualCatalog => roomVisualCatalog;
    public DoorCameraSequence CameraSequence => doorCameraSequence;
    public IReadOnlyDictionary<string, RoomDefinition> RoomsByName => roomsByName;

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (runtimeSettingsMenu == null)
        {
            report.AddError("RoomNavigationManager requires its serialized RuntimeSettingsMenu owner.", this);
        }
    }
    public IReadOnlyDictionary<string, DoorRoute> RoutesByDoorId => routesByDoorId;
    public RoomChangedEvent OnCurrentRoomChanged => onCurrentRoomChanged;

    private void Awake()
    {
        ResolveReferences();
        RegisterBuiltInRoomChangeResponses();
        RefreshDoorButtonCache();
        RefreshRoomContentCache();
        LoadLegacyDoorDataIfNeeded();
        SetCurrentRoom(GetInitialRoomName(), false, applyStartingRoomVisualOnAwake);
    }

    private void Start()
    {
        // Awake performs the first room change, but Start is a safer final pass
        // for scene objects that were inactive during Awake. This makes the
        // current room's trigger rectangles reachable before the player clicks.
        ResolveReferences();
        RefreshDoorButtonsForCurrentRoom();
        UpdateCurrentRoomHud(currentRoom);
        EnsureRuntimeSettingsMenu();
        EnsureFireplaceAmbienceController();
        EnsureClockTickingAmbienceController();
        RunNavigationSelfCheckForCurrentRoom();
    }

    private void OnDestroy()
    {
        onCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
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

        // The door data only translates a clicked door ID into a destination room.
        // SetCurrentRoom owns the actual room-state change and broadcasts it.
        if (!SetCurrentRoom(route.DestinationRoom, true, true))
        {
            return false;
        }

        PlacePlayerAtDestinationDoor(route.SourceRoom, cleanDoorId, route.DestinationRoom);
        return true;
    }

    public bool MoveToRoom(string roomName)
    {
        return SetCurrentRoom(roomName, false, true);
    }

    public List<string> GetKnownRoomNames()
    {
        ResolveReferences();
        RefreshRoomContentCache();
        RefreshDoorButtonCache();

        SortedSet<string> roomNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, RoomDefinition> pair in roomsByName)
        {
            AddRoomName(roomNames, pair.Value != null ? pair.Value.RoomName : pair.Key);
        }

        if (roomVisualCatalog != null && roomVisualCatalog.rooms != null)
        {
            for (int i = 0; i < roomVisualCatalog.rooms.Length; i++)
            {
                RoomVisualEntry entry = roomVisualCatalog.rooms[i];
                AddRoomName(roomNames, entry != null ? entry.roomName : null);
            }
        }

        if (doorCameraSequence != null && doorCameraSequence.roomOrder != null)
        {
            for (int i = 0; i < doorCameraSequence.roomOrder.Length; i++)
            {
                AddRoomName(roomNames, doorCameraSequence.roomOrder[i]);
            }
        }

        for (int i = 0; i < cachedRoomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = cachedRoomContentGroups[i];
            AddRoomName(roomNames, roomContentGroup != null ? roomContentGroup.RoomName : null);
        }

        for (int i = 0; i < cachedDoorTriggers.Length; i++)
        {
            DoorTriggerNavigation doorTrigger = cachedDoorTriggers[i];

            if (doorTrigger == null)
            {
                continue;
            }

            AddRoomName(roomNames, doorTrigger.SourceRoom);
            AddRoomName(roomNames, doorTrigger.DestinationRoom);
        }

        return new List<string>(roomNames);
    }

    public bool DebugTeleportToRoom(string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName))
        {
            Warn("Debug teleport ignored an empty room name.");
            return false;
        }

        if (SameName(cleanRoomName, currentRoom))
        {
            return true;
        }

        string previousRoom = currentRoom;

        if (!SetCurrentRoom(cleanRoomName, false, true))
        {
            return false;
        }

        PlacePlayerAtDebugArrival(previousRoom, cleanRoomName);
        return true;
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

        string transitionSourceRoom = string.IsNullOrEmpty(cleanSourceRoom) ? currentRoom : cleanSourceRoom;

        // Camera-sequence doors compute the next room from the sequence, then use
        // the same state-change path as every other room transition.
        if (!SetCurrentRoom(nextRoom, false, true))
        {
            return false;
        }

        PlacePlayerAtDestinationDoor(transitionSourceRoom, cleanDoorName, nextRoom);
        return true;
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

        string transitionSourceRoom = string.IsNullOrEmpty(cleanSourceRoom) ? currentRoom : cleanSourceRoom;

        // Inspector-driven doors already know their destination room, so they pass
        // that room directly into the central room-state change.
        if (!SetCurrentRoom(cleanDestinationRoom, false, true))
        {
            return false;
        }

        PlacePlayerAtDestinationDoor(transitionSourceRoom, cleanDoorName, cleanDestinationRoom);
        return true;
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
        Transform doorSearchRoot = doorButtonRoot != null ? doorButtonRoot : roomContentRoot;

        if (doorSearchRoot != null)
        {
            cachedDoorButtons = doorSearchRoot.GetComponentsInChildren<DoorButton>(true);
            cachedDoorTriggers = doorSearchRoot.GetComponentsInChildren<DoorTriggerNavigation>(true);
            return;
        }

        cachedDoorButtons = FindObjectsByType<DoorButton>(FindObjectsInactive.Include);
        cachedDoorTriggers = FindObjectsByType<DoorTriggerNavigation>(FindObjectsInactive.Include);
    }

    public void RefreshRoomContentCache()
    {
        if (roomContentRoot != null)
        {
            cachedRoomContentGroups = roomContentRoot.GetComponentsInChildren<RoomContentGroup>(true);
            return;
        }

        cachedRoomContentGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);
    }

    private void LoadLegacyDoorDataIfNeeded()
    {
        // DoorTriggerNavigation hitboxes use their Inspector destination. Only
        // legacy DoorButton objects still need doors.txt as a lookup table.
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

            // Manual Room_* objects are allowed to exist before doors.txt knows
            // about them. The scene hierarchy is the room list; doors.txt is only
            // a legacy lookup when a trigger has no Inspector destination.
        }

        // This assignment is the actual room change. Door triggers, buttons, music,
        // animations, and visuals should not each maintain their own current-room
        // state; they should react to this one value changing.
        currentRoom = roomDefinition != null ? roomDefinition.RoomName : cleanRoomName;

        // These flags describe what this transition wants the built-in room systems
        // to do when the room-changed event fires. Future systems can subscribe to
        // OnCurrentRoomChanged instead of adding more work inside SetCurrentRoom.
        applyVisualForNextRoomChange = applyRoomVisual;

        try
        {
            onCurrentRoomChanged.Invoke(currentRoom);
        }
        finally
        {
            applyVisualForNextRoomChange = false;
        }

        return true;
    }

    private void PlacePlayerAtDestinationDoor(string sourceRoom, string doorName, string destinationRoom)
    {
        string cleanSourceRoom = Clean(sourceRoom);
        string cleanDestinationRoom = Clean(destinationRoom);

        if (string.IsNullOrEmpty(cleanDestinationRoom))
        {
            return;
        }

        PointClickPlayerMovement playerMovement = FindPlayerMovement();

        if (playerMovement == null)
        {
            return;
        }

        playerMovement.RefreshWalkableFloorForCurrentRoom();

        DoorTriggerNavigation arrivalTrigger = FindArrivalDoorTrigger(cleanSourceRoom, doorName, cleanDestinationRoom);

        if (arrivalTrigger == null)
        {
            Warn($"No arrival trigger in '{cleanDestinationRoom}' points back to '{cleanSourceRoom}', so the player could not be placed at the destination door.");
            return;
        }

        if (!arrivalTrigger.TryFindArrivalDestination(playerMovement, out Vector2 arrivalDestination))
        {
            Warn($"Arrival trigger '{arrivalTrigger.name}' could not find a reachable floor point for the player.");
            return;
        }

        if (!playerMovement.TryWarpTo(arrivalDestination, false) &&
            !playerMovement.TryWarpTo(arrivalDestination, true))
        {
            Warn($"Player rejected the arrival point selected by '{arrivalTrigger.name}'.");
        }
    }

    private void PlacePlayerAtDebugArrival(string sourceRoom, string destinationRoom)
    {
        string cleanDestinationRoom = Clean(destinationRoom);

        if (string.IsNullOrEmpty(cleanDestinationRoom))
        {
            return;
        }

        PointClickPlayerMovement playerMovement = FindPlayerMovement();

        if (playerMovement == null)
        {
            return;
        }

        playerMovement.RefreshWalkableFloorForCurrentRoom();

        DoorTriggerNavigation arrivalTrigger = FindArrivalDoorTrigger(Clean(sourceRoom), string.Empty, cleanDestinationRoom);

        if (arrivalTrigger == null)
        {
            arrivalTrigger = FindAnyDoorTriggerInRoom(cleanDestinationRoom);
        }

        if (arrivalTrigger == null)
        {
            Warn($"Debug teleport could not find an arrival door trigger in '{cleanDestinationRoom}'. The room state changed normally, but the player was not repositioned.");
            return;
        }

        if (!arrivalTrigger.TryFindArrivalDestination(playerMovement, out Vector2 arrivalDestination))
        {
            Warn($"Debug teleport arrival trigger '{arrivalTrigger.name}' could not find a reachable floor point for the player.");
            return;
        }

        if (!playerMovement.TryWarpTo(arrivalDestination, false) &&
            !playerMovement.TryWarpTo(arrivalDestination, true))
        {
            Warn($"Player rejected the debug teleport arrival point selected by '{arrivalTrigger.name}'.");
        }
    }

    private DoorTriggerNavigation FindArrivalDoorTrigger(string sourceRoom, string doorName, string destinationRoom)
    {
        RefreshDoorButtonCache();

        DoorTriggerNavigation namedFallback = null;
        DoorTriggerNavigation onlyDestinationRoomTrigger = null;
        int destinationRoomTriggerCount = 0;

        for (int i = 0; i < cachedDoorTriggers.Length; i++)
        {
            DoorTriggerNavigation candidate = cachedDoorTriggers[i];

            if (candidate == null || !SameName(candidate.SourceRoom, destinationRoom))
            {
                continue;
            }

            destinationRoomTriggerCount++;
            onlyDestinationRoomTrigger = candidate;

            string candidateDestinationRoom = candidate.DestinationRoom;

            if (!string.IsNullOrEmpty(sourceRoom) &&
                !string.IsNullOrEmpty(candidateDestinationRoom) &&
                SameName(candidateDestinationRoom, sourceRoom))
            {
                return candidate;
            }

            if (namedFallback == null && DoorNameReferencesRoom(candidate.DoorName, sourceRoom))
            {
                namedFallback = candidate;
            }
        }

        if (namedFallback != null)
        {
            return namedFallback;
        }

        return destinationRoomTriggerCount == 1 ? onlyDestinationRoomTrigger : null;
    }

    private DoorTriggerNavigation FindAnyDoorTriggerInRoom(string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName))
        {
            return null;
        }

        RefreshDoorButtonCache();

        for (int i = 0; i < cachedDoorTriggers.Length; i++)
        {
            DoorTriggerNavigation candidate = cachedDoorTriggers[i];

            if (candidate != null && SameName(candidate.SourceRoom, cleanRoomName))
            {
                return candidate;
            }
        }

        return null;
    }

    private static PointClickPlayerMovement FindPlayerMovement()
    {
        GameObject playerObject = GameObject.Find("Player");

        if (TryGetUsablePlayerMovement(playerObject, out PointClickPlayerMovement namedPlayerMovement))
        {
            return namedPlayerMovement;
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                string.Equals(candidate.gameObject.name, "Player", StringComparison.OrdinalIgnoreCase) &&
                IsUsablePlayerMovement(candidate))
            {
                return candidate;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (IsUsablePlayerMovement(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryGetUsablePlayerMovement(GameObject candidateObject, out PointClickPlayerMovement playerMovement)
    {
        playerMovement = candidateObject != null ? candidateObject.GetComponent<PointClickPlayerMovement>() : null;
        return IsUsablePlayerMovement(playerMovement);
    }

    private static bool IsUsablePlayerMovement(PointClickPlayerMovement candidate)
    {
        return candidate != null &&
            candidate.enabled &&
            candidate.gameObject.activeInHierarchy &&
            !IsLikelyChapterGuest(candidate.gameObject);
    }

    private static bool IsLikelyChapterGuest(GameObject candidateObject)
    {
        if (candidateObject == null)
        {
            return false;
        }

        return candidateObject.name.Trim().StartsWith("Guest", StringComparison.OrdinalIgnoreCase);
    }

    private void RegisterBuiltInRoomChangeResponses()
    {
        // Keep registration idempotent so Reload/Bootstrap paths cannot add the
        // same listener twice. This listener is the automatic "Option 2" response:
        // one currentRoom change fans out to visuals, room objects, doors, and map UI.
        onCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        onCurrentRoomChanged.AddListener(HandleCurrentRoomChanged);
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        ResolveReferences();
        EnsureFireplaceAmbienceController();
        EnsureClockTickingAmbienceController();
        RefreshRoomContentForCurrentRoom();
        SyncCameraRoomContent(roomName, false);

        if (applyVisualForNextRoomChange)
        {
            ApplyRoomVisual(roomName);
        }

        RefreshDoorButtonsForCurrentRoom();
        UpdateCurrentRoomHud(roomName);
        RunNavigationSelfCheckForCurrentRoom();

    }

    private void EnsureFireplaceAmbienceController()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        fireplaceAmbienceController = FireplaceAmbienceController.FindOrCreate(this);
    }

    private void EnsureClockTickingAmbienceController()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        clockTickingAmbienceController = ClockTickingAmbienceController.FindOrCreate(this);
    }

    private void ApplyRoomVisual(string roomName)
    {
        ResolveReferences();

        if (cameraManager == null)
        {
            return;
        }

        RoomContentGroup roomContentGroup = FindRoomContentGroup(roomName);

        if (roomContentGroup != null)
        {
            if (roomContentGroup.TryGetRoomBackgroundTexture(out Texture roomContentTexture) && roomContentTexture != null)
            {
                cameraManager.SetActiveRoomContent(roomContentGroup);
                return;
            }

            cameraManager.SetActiveRoomContent(roomContentGroup, false);
        }

        Texture texture;

        if (TryFindRoomTexture(roomName, out texture))
        {
            if (roomContentGroup == null)
            {
                cameraManager.SetActiveRoomContent(null, false);
            }

            cameraManager.SetRoomBackground(texture);
            return;
        }

        Warn($"No background texture found for room '{roomName}'. Assign it on Room_{roomName} or add it to a RoomVisualCatalog.");
    }

    private void SyncCameraRoomContent(string roomName, bool updateBackground)
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.SetActiveRoomContent(FindRoomContentGroup(roomName), updateBackground);
    }

    private RoomContentGroup FindRoomContentGroup(string roomName)
    {
        RefreshRoomContentCache();

        for (int i = 0; i < cachedRoomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = cachedRoomContentGroups[i];

            if (roomContentGroup != null && SameName(roomContentGroup.RoomName, roomName))
            {
                return roomContentGroup;
            }
        }

        return null;
    }

    private bool TryFindRoomTexture(string roomName, out Texture texture)
    {
        texture = null;

        RefreshRoomContentCache();

        for (int i = 0; i < cachedRoomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = cachedRoomContentGroups[i];

            if (roomContentGroup == null || !SameName(roomContentGroup.RoomName, roomName))
            {
                continue;
            }

            if (roomContentGroup.TryGetRoomBackgroundTexture(out texture))
            {
                return true;
            }
        }

        if (roomVisualCatalog != null && roomVisualCatalog.TryGetRoomTexture(roomName, out texture))
        {
            return true;
        }

        return false;
    }

    private void UpdateCurrentRoomHud(string roomName)
    {
        if (!showCurrentRoomHud || !Application.isPlaying)
        {
            return;
        }

        EnsureCurrentRoomHud();

        if (currentRoomHudText == null)
        {
            return;
        }

        // This text is a readout of the one true current-room value. Keeping it
        // here prevents the HUD from drifting away from navigation state.
        currentRoomHudText.text = $"{currentRoomHudPrefix}{Clean(roomName)}";
        currentRoomHudText.gameObject.SetActive(true);
        currentRoomHudText.transform.SetAsLastSibling();
    }

    private void EnsureCurrentRoomHud()
    {
        if (currentRoomHudText != null)
        {
            ConfigureCurrentRoomHudText();
            return;
        }

        TMP_Text existingText = FindNamedComponent<TMP_Text>(currentRoomHudName);

        if (existingText != null)
        {
            currentRoomHudText = existingText;
            PostProcessSafeCanvasUtility.MoveToSafeCanvas(currentRoomHudText);
            ConfigureCurrentRoomHudText();
            return;
        }

        Canvas canvas = FindPreferredStatusCanvas();

        if (canvas == null)
        {
            Warn("Could not create the current-room HUD because no Canvas exists in the Gameplay scene.");
            return;
        }

        GameObject textObject = new GameObject(currentRoomHudName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(canvas.transform, false);
        currentRoomHudText = textObject.GetComponent<TMP_Text>();
        ConfigureCurrentRoomHudText();
    }

    private void ConfigureCurrentRoomHudText()
    {
        if (currentRoomHudText == null)
        {
            return;
        }

        RectTransform rectTransform = currentRoomHudText.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = currentRoomHudOffset;
            rectTransform.sizeDelta = currentRoomHudSize;
            rectTransform.localScale = Vector3.one;
        }

        currentRoomHudText.fontSize = currentRoomHudFontSize;
        currentRoomHudText.color = currentRoomHudColor;
        currentRoomHudText.alignment = TextAlignmentOptions.BottomRight;
        currentRoomHudText.raycastTarget = false;

        Shadow shadow = currentRoomHudText.GetComponent<Shadow>();

        if (shadow != null)
        {
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
        }
    }

    private Canvas FindPreferredStatusCanvas()
    {
        Canvas safeCanvas = PostProcessSafeCanvasUtility.GetOrCreateCanvas();
        return safeCanvas != null ? safeCanvas : FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    private static T FindNamedComponent<T>(string objectName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];

            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private void RefreshDoorButtonsForCurrentRoom()
    {
        RefreshDoorButtonCache();
        PrepareDoorControlLayerForRuntime();
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
                string sequenceSourceRoom = doorTrigger.SourceRoom;
                bool sequenceTriggerBelongsToCurrentRoom = string.IsNullOrEmpty(sequenceSourceRoom)
                    ? doorCameraSequence == null || doorCameraSequence.ContainsRoom(currentRoom)
                    : SameName(sequenceSourceRoom, currentRoom);
                SetDoorTriggerActiveForRuntime(doorTrigger, sequenceTriggerBelongsToCurrentRoom);
                continue;
            }

            string sourceRoom = doorTrigger.SourceRoom;

            if (string.IsNullOrEmpty(sourceRoom))
            {
                continue;
            }

            bool belongsToCurrentRoom = SameName(sourceRoom, currentRoom);
            SetDoorTriggerActiveForRuntime(doorTrigger, belongsToCurrentRoom);
        }
    }

    private void RefreshRoomContentForCurrentRoom()
    {
        RefreshRoomContentCache();

        for (int i = 0; i < cachedRoomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = cachedRoomContentGroups[i];

            if (roomContentGroup == null)
            {
                continue;
            }

            // RoomContentGroup belongs on the individual room objects, not on the
            // navigation manager or the root that holds all rooms.
            if (roomContentGroup.gameObject == gameObject || roomContentGroup.transform == roomContentRoot)
            {
                continue;
            }

            string roomName = roomContentGroup.RoomName;

            if (string.IsNullOrEmpty(roomName))
            {
                continue;
            }

            // A RoomContentGroup is the optional "room object" layer: put room
            // images, animators, audio sources, or other room-only objects under it,
            // and the navigation manager will activate only the current room's group.
            roomContentGroup.gameObject.SetActive(SameName(roomName, currentRoom));
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

    private void PrepareDoorControlLayerForRuntime()
    {
        if (doorButtonRoot == null)
        {
            return;
        }

        // The room root is the playable scene layer. It must stay active for the
        // current room, but it should keep its authored sibling order because it
        // now contains both the room image and its hitboxes.
        doorButtonRoot.gameObject.SetActive(true);

        if (Application.isPlaying && !string.Equals(doorButtonRoot.name, RoomRootName, StringComparison.OrdinalIgnoreCase))
        {
            doorButtonRoot.SetAsLastSibling();
        }

        RectTransform rootRect = doorButtonRoot as RectTransform;

        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
        }

        Canvas canvas = doorButtonRoot.GetComponentInParent<Canvas>(true);

        if (canvas == null)
        {
            return;
        }

        canvas.gameObject.SetActive(true);

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect != null && IsNearlyZeroScale(canvasRect.localScale))
        {
            Debug.LogWarning($"Navigation repaired '{canvas.name}' because its scale was zero. A zero-scale canvas breaks UI raycasts.", canvas);
            canvasRect.localScale = Vector3.one;
        }
    }

    private void SetDoorTriggerActiveForRuntime(DoorTriggerNavigation doorTrigger, bool active)
    {
        if (doorTrigger == null)
        {
            return;
        }

        doorTrigger.gameObject.SetActive(active);

        if (!active)
        {
            return;
        }

        // Door hitboxes are children of the active room stage, so the same
        // RectTransform that moves the painted room also moves every trigger.
    }

    private void RunNavigationSelfCheckForCurrentRoom()
    {
        if (!runNavigationSelfCheck || !Application.isPlaying)
        {
            return;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            Debug.LogError("Navigation setup problem: no EventSystem exists in the scene, so UI door trigger clicks cannot be received.", this);
        }

        if (doorButtonRoot == null && cachedDoorTriggers.Length > 0)
        {
            Debug.LogWarning($"Navigation found {cachedDoorTriggers.Length} door trigger(s), but no '{RoomRootName}' or '{DoorButtonsRootName}' root. Runtime can still search globally, but the scene is more fragile.", this);
        }

    }

    private void EnsureRuntimeSettingsMenu()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        runtimeSettingsMenu?.Initialize(this);
    }

    private string GetInitialRoomName()
    {
        string cleanStartingRoom = Clean(startingRoom);

        if (!string.IsNullOrEmpty(cleanStartingRoom))
        {
            return cleanStartingRoom;
        }

        string sequenceStartingRoom = doorCameraSequence != null ? Clean(doorCameraSequence.StartingRoom) : string.Empty;

        if (!string.IsNullOrEmpty(sequenceStartingRoom) &&
            (roomsByName.Count == 0 || roomsByName.ContainsKey(sequenceStartingRoom)))
        {
            return sequenceStartingRoom;
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
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
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
            GameObject rootObject = GameObject.Find(RoomRootName);

            if (rootObject == null)
            {
                rootObject = GameObject.Find(LegacyDoorTriggerEditRootName);
            }

            if (rootObject == null)
            {
                rootObject = GameObject.Find(DoorButtonsRootName);
            }

            if (rootObject == null)
            {
                rootObject = GameObject.Find(LegacyDoorTriggerRootName);
            }

            if (rootObject != null)
            {
                doorButtonRoot = rootObject.transform;
            }
        }

        if (roomContentRoot == null)
        {
            GameObject rootObject = GameObject.Find(RoomRootName);

            if (rootObject == null)
            {
                rootObject = GameObject.Find(LegacyRoomObjectsRootName);
            }

            if (rootObject == null)
            {
                rootObject = GameObject.Find(LegacyDoorTriggerEditRootName);
            }

            if (rootObject != null)
            {
                roomContentRoot = rootObject.transform;
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
        return string.Equals(NormalizeComparableName(left), NormalizeComparableName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoorNameReferencesRoom(string doorName, string roomName)
    {
        string cleanDoorName = NormalizeComparableName(doorName);
        string cleanRoomName = NormalizeComparableName(roomName);

        if (string.IsNullOrEmpty(cleanDoorName) || string.IsNullOrEmpty(cleanRoomName))
        {
            return false;
        }

        return cleanDoorName.IndexOf(cleanRoomName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddRoomName(ISet<string> roomNames, string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (!string.IsNullOrEmpty(cleanRoomName))
        {
            roomNames.Add(cleanRoomName);
        }
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeComparableName(string value)
    {
        string cleanValue = Clean(value);

        if (string.IsNullOrEmpty(cleanValue))
        {
            return string.Empty;
        }

        char[] normalized = new char[cleanValue.Length];
        int length = 0;

        for (int i = 0; i < cleanValue.Length; i++)
        {
            char c = cleanValue[i];

            if (char.IsLetterOrDigit(c))
            {
                normalized[length] = char.ToUpperInvariant(c);
                length++;
            }
        }

        return new string(normalized, 0, length);
    }

    private static bool IsNearlyZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f;
    }

}

[Serializable]
public class RoomChangedEvent : UnityEvent<string>
{
}
