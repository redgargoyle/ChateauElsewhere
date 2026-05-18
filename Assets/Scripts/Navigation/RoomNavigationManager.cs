using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoomNavigationManager : MonoBehaviour
{
    private const string DoorButtonsRootName = "DoorButtons";
    private const string DoorTriggerEditRootName = "RoomDoorTriggers_Edit";
    private const string LegacyDoorTriggerRootName = "DoorTriggerParent";

    [Header("Data")]
    [SerializeField] private TextAsset doorDataFile;
    [SerializeField] private string doorDataResourcePath = "Navigation/doors";
    [SerializeField] private string startingRoom = "Music";
    [SerializeField] private RoomVisualCatalog roomVisualCatalog;
    [SerializeField] private string roomVisualCatalogResourcePath = "Navigation/RoomVisualCatalog";
    [SerializeField] private DoorCameraSequence doorCameraSequence;
    [SerializeField] private string doorCameraSequenceResourcePath = "Navigation/DoorCameraSequence";

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private MapAnimator mapAnimator;
    [SerializeField] private Transform doorButtonRoot;
    [SerializeField] private Transform roomContentRoot;

    [Header("Behavior")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool hideMapAfterDoorClick;
    [SerializeField] private bool applyStartingRoomVisualOnAwake;
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
    private bool hideMapForNextRoomChange;
    private TMP_Text currentRoomHudText;

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
        return SetCurrentRoom(route.DestinationRoom, true, true, true);
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

        // Camera-sequence doors compute the next room from the sequence, then use
        // the same state-change path as every other room transition.
        return SetCurrentRoom(nextRoom, false, true, true);
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

        // Inspector-driven doors already know their destination room, so they pass
        // that room directly into the central room-state change.
        return SetCurrentRoom(cleanDestinationRoom, false, true, true);
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

    public void RefreshRoomContentCache()
    {
        if (roomContentRoot != null)
        {
            cachedRoomContentGroups = roomContentRoot.GetComponentsInChildren<RoomContentGroup>(true);
            return;
        }

        cachedRoomContentGroups = FindObjectsOfType<RoomContentGroup>(true);
    }

    private void LoadLegacyDoorDataIfNeeded()
    {
        // The original navigation system only had DoorButton objects, but the
        // current scene uses DoorTriggerNavigation hitboxes. Either kind of door
        // control means we need to load doors.txt before the first room is chosen.
        if (cachedDoorButtons.Length == 0 && cachedDoorTriggers.Length == 0)
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

    private bool SetCurrentRoom(string roomName, bool requireKnownRoom, bool applyRoomVisual, bool hideMapAfterRoomChange = false)
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

        // This assignment is the actual room change. Door triggers, buttons, music,
        // animations, and visuals should not each maintain their own current-room
        // state; they should react to this one value changing.
        currentRoom = roomDefinition != null ? roomDefinition.RoomName : cleanRoomName;

        // These flags describe what this transition wants the built-in room systems
        // to do when the room-changed event fires. Future systems can subscribe to
        // OnCurrentRoomChanged instead of adding more work inside SetCurrentRoom.
        applyVisualForNextRoomChange = applyRoomVisual;
        hideMapForNextRoomChange = hideMapAfterRoomChange && hideMapAfterDoorClick;

        try
        {
            onCurrentRoomChanged.Invoke(currentRoom);
        }
        finally
        {
            applyVisualForNextRoomChange = false;
            hideMapForNextRoomChange = false;
        }

        return true;
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

        if (applyVisualForNextRoomChange)
        {
            ApplyRoomVisual(roomName);
        }

        RefreshRoomContentForCurrentRoom();
        RefreshDoorButtonsForCurrentRoom();
        UpdateCurrentRoomHud(roomName);
        RunNavigationSelfCheckForCurrentRoom();

        if (hideMapForNextRoomChange && mapAnimator != null)
        {
            mapAnimator.HideMap();
        }
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

            if (area == null)
            {
                continue;
            }

            string areaRoomName = ParseRoomNameFromCameraArea(area.name);

            if (SameName(areaRoomName, roomName))
            {
                texture = area.GetEffectiveRoomBackgroundTexture();

                if (texture != null)
                {
                    return true;
                }
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
        GameObject backgroundCanvasObject = GameObject.Find("Canvas_Background");

        if (backgroundCanvasObject != null && backgroundCanvasObject.TryGetComponent(out Canvas backgroundCanvas))
        {
            return backgroundCanvas;
        }

        return FindObjectOfType<Canvas>(true);
    }

    private static T FindNamedComponent<T>(string objectName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] components = FindObjectsOfType<T>(true);

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

        int activeRouteTriggerCount = 0;

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

            if (belongsToCurrentRoom)
            {
                activeRouteTriggerCount++;
            }
        }

        if (runNavigationSelfCheck && activeRouteTriggerCount == 0 && roomsByName.TryGetValue(currentRoom, out RoomDefinition roomDefinition) &&
            roomDefinition.Doors.Count > 0)
        {
            Debug.LogError($"Navigation setup problem: room '{currentRoom}' has {roomDefinition.Doors.Count} door route(s) in doors.txt, but no DoorTriggerNavigation for that room was activated.", this);
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

        // The door trigger root is the raycast layer. It must stay active and in
        // front of the map/buttons so clicks land on the current room's trigger
        // rectangles instead of unrelated UI from edit mode.
        doorButtonRoot.gameObject.SetActive(true);

        if (Application.isPlaying)
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

        // The trigger stores its rectangle in the same shader-space coordinates
        // as the room image. Re-applying it immediately keeps the clickable area
        // lined up with the visible door after room changes and preview edits.
        doorTrigger.ApplyCapturedShaderAnchor(cameraManager);
    }

    private void RunNavigationSelfCheckForCurrentRoom()
    {
        if (!runNavigationSelfCheck || !Application.isPlaying)
        {
            return;
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            Debug.LogError("Navigation setup problem: no EventSystem exists in the scene, so UI door trigger clicks cannot be received.", this);
        }

        if (doorButtonRoot == null && cachedDoorTriggers.Length > 0)
        {
            Debug.LogWarning($"Navigation found {cachedDoorTriggers.Length} door trigger(s), but no '{DoorTriggerEditRootName}' or '{DoorButtonsRootName}' root. Runtime can still search globally, but the scene is more fragile.", this);
        }

        ValidateDoorRoutesHaveTriggers();
        ValidateCurrentRoomHasReachableTriggers();
    }

    private void ValidateDoorRoutesHaveTriggers()
    {
        foreach (DoorRoute route in routesByDoorId.Values)
        {
            bool found = false;

            for (int i = 0; i < cachedDoorTriggers.Length; i++)
            {
                DoorTriggerNavigation trigger = cachedDoorTriggers[i];

                if (trigger != null && SameName(trigger.DoorName, route.DoorId))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError($"Navigation setup problem: doors.txt has '{route.DoorId}' in '{route.SourceRoom}', but the scene has no DoorTriggerNavigation with that door ID.", this);
            }
        }
    }

    private void ValidateCurrentRoomHasReachableTriggers()
    {
        if (!roomsByName.TryGetValue(currentRoom, out RoomDefinition roomDefinition) || roomDefinition.Doors.Count == 0)
        {
            return;
        }

        int reachableCount = 0;

        for (int i = 0; i < cachedDoorTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = cachedDoorTriggers[i];

            if (trigger == null || trigger.UsesCameraSequence || !SameName(trigger.SourceRoom, currentRoom))
            {
                continue;
            }

            if (trigger.gameObject.activeInHierarchy)
            {
                reachableCount++;
            }
        }

        if (reachableCount == 0)
        {
            Debug.LogError($"Navigation setup problem: current room '{currentRoom}' has door data, but none of its DoorTriggerNavigation objects are active in the hierarchy. Check '{DoorTriggerEditRootName}' and Room_{currentRoom}.", this);
        }
    }

    private string GetInitialRoomName()
    {
        string cleanStartingRoom = Clean(startingRoom);

        if (!string.IsNullOrEmpty(cleanStartingRoom))
        {
            if (roomsByName.Count == 0 || roomsByName.ContainsKey(cleanStartingRoom))
            {
                return cleanStartingRoom;
            }

            Warn($"Starting room '{cleanStartingRoom}' is not in doors.txt. Falling back to the first room in the loaded door data.");
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
            cameraManager = FindObjectOfType<CameraManager>(true);
        }

        if (mapAnimator == null)
        {
            mapAnimator = FindObjectOfType<MapAnimator>(true);
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
            GameObject rootObject = GameObject.Find(DoorTriggerEditRootName);

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
            GameObject rootObject = GameObject.Find("Rooms");

            if (rootObject == null)
            {
                rootObject = GameObject.Find("RoomObjects");
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
        return string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool IsNearlyZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f;
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
