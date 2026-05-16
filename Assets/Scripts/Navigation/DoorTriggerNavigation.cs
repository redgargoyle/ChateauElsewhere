using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
public class DoorTriggerNavigation : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static event Action<DoorTriggerNavigation> HoveredTriggerChanged;
    public static DoorTriggerNavigation HoveredTrigger { get; private set; }

    [Header("Door Route")]
    [SerializeField] private string sourceRoom;
    [SerializeField] private string doorName;
    [SerializeField] private string destinationRoom;
    [SerializeField] private bool requirePlayerInSourceRoom = true;
    [SerializeField] private bool useCameraSequence = true;

    [Header("References")]
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Image image;

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool bringToFront = true;

    [Header("Background Tracking")]
    [SerializeField] private bool followCameraBackground = true;
    [SerializeField] private bool captureBackgroundAnchorAtRuntime = true;
    [SerializeField] private Rect backgroundShaderUvRect;
    [SerializeField] private bool hasBackgroundShaderUvRect;

    public string SourceRoom => GetEffectiveSourceRoom();
    public string DoorName => Clean(doorName);
    public string DestinationRoom => Clean(destinationRoom);
    public bool UsesCameraSequence => useCameraSequence;
    public bool HasBackgroundShaderAnchor => hasBackgroundShaderUvRect;

    private RectTransform rectTransform;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        FillSourceRoomFromHierarchy();
        ConfigureImage();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureImage();
        BringToFrontIfNeeded();
    }

    private void Start()
    {
        CaptureBackgroundAnchorIfNeeded();
        FollowCameraBackgroundIfNeeded();
    }

    private void LateUpdate()
    {
        FollowCameraBackgroundIfNeeded();
    }

    private void OnValidate()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (string.IsNullOrWhiteSpace(sourceRoom))
        {
            FillSourceRoomFromHierarchy();
        }

        ConfigureImage();
    }

    private void OnTransformParentChanged()
    {
        if (string.IsNullOrWhiteSpace(sourceRoom))
        {
            FillSourceRoomFromHierarchy();
        }
    }

    private void OnDisable()
    {
        if (HoveredTrigger == this)
        {
            SetHoveredTrigger(null);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoveredTrigger(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoveredTrigger == this)
        {
            SetHoveredTrigger(null);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ActivateDoor();
    }

    public void ActivateDoor()
    {
        ResolveReferences();

        if (navigationManager == null)
        {
            Debug.LogWarning($"Door trigger '{name}' could not find a RoomNavigationManager.", this);
            return;
        }

        if (useCameraSequence)
        {
            // The trigger does not load rooms itself. It asks the navigation
            // manager for the next room in the sequence, and the manager changes
            // the single current-room state that every room system reacts to.
            navigationManager.OpenDoorFromCurrentRoom(string.Empty, DoorName, false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(DoorName) && navigationManager.HasDoor(DoorName))
        {
            // Door triggers created from doors.txt should navigate through the
            // loaded door data. The serialized destination is still kept on the
            // trigger so the scene is readable in the Inspector, but doors.txt is
            // the source of truth at runtime.
            navigationManager.TryMoveThroughDoor(DoorName);
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationRoom))
        {
            Debug.LogWarning($"Door trigger '{name}' has no destination room.", this);
            return;
        }

        // Inspector-driven triggers send their destination room to the same central
        // room-change path. Visuals, room objects, doors, and animations should all
        // update from RoomNavigationManager.OnCurrentRoomChanged.
        navigationManager.MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom);
    }

    public void RefreshInferredSourceRoom()
    {
        FillSourceRoomFromHierarchy();
    }

    public bool CaptureCurrentShaderAnchor(CameraManager previewCameraManager = null)
    {
        if (previewCameraManager != null)
        {
            cameraManager = previewCameraManager;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null)
        {
            return false;
        }

        // When a trigger is placed while the room image is panned in the shader,
        // its visible rectangle is not the same as its raw canvas coordinates.
        // Store the shader-space rectangle so future pans can move this trigger
        // with the picture instead of leaving it fixed on the screen.
        if (!cameraManager.TryCaptureShaderAnchoredRect(rectTransform, out Rect capturedRect))
        {
            return false;
        }

        backgroundShaderUvRect = capturedRect;
        hasBackgroundShaderUvRect = true;
        return true;
    }

    public bool ApplyCapturedShaderAnchor(CameraManager previewCameraManager = null)
    {
        if (!hasBackgroundShaderUvRect)
        {
            return false;
        }

        if (previewCameraManager != null)
        {
            cameraManager = previewCameraManager;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null)
        {
            return false;
        }

        return cameraManager.TryApplyShaderAnchoredRect(rectTransform, backgroundShaderUvRect);
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (navigationManager == null)
        {
            navigationManager = FindObjectOfType<RoomNavigationManager>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }
    }

    private void ConfigureImage()
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = true;

        if (makeInvisibleAtRuntime && Application.isPlaying)
        {
            image.color = runtimeColor;
        }
    }

    private void BringToFrontIfNeeded()
    {
        if (!Application.isPlaying || !bringToFront || transform.parent == null)
        {
            return;
        }

        transform.SetAsLastSibling();
    }

    private void CaptureBackgroundAnchorIfNeeded()
    {
        if (!Application.isPlaying || !followCameraBackground || !captureBackgroundAnchorAtRuntime ||
            hasBackgroundShaderUvRect)
        {
            return;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null)
        {
            return;
        }

        // The door trigger starts as an ordinary UI rectangle placed over the door.
        // We capture that rectangle as background shader UV space once, then stop
        // trusting its static canvas position.
        if (cameraManager.TryCaptureDefaultShaderAnchoredRect(rectTransform, out Rect capturedRect))
        {
            backgroundShaderUvRect = capturedRect;
            hasBackgroundShaderUvRect = true;
        }
    }

    private void FollowCameraBackgroundIfNeeded()
    {
        if (!Application.isPlaying || !followCameraBackground)
        {
            return;
        }

        CaptureBackgroundAnchorIfNeeded();

        if (!hasBackgroundShaderUvRect)
        {
            return;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null)
        {
            return;
        }

        // CameraManager knows the same pan/FOV values that are sent into the
        // background shader. Applying the saved shader UV rect here makes the
        // invisible raycast box move with the rendered picture.
        cameraManager.TryApplyShaderAnchoredRect(rectTransform, backgroundShaderUvRect);
        BringToFrontIfNeeded();
    }

    private static void SetHoveredTrigger(DoorTriggerNavigation trigger)
    {
        if (HoveredTrigger == trigger)
        {
            return;
        }

        HoveredTrigger = trigger;
        HoveredTriggerChanged?.Invoke(trigger);
    }

    private string GetEffectiveSourceRoom()
    {
        if (!string.IsNullOrWhiteSpace(sourceRoom))
        {
            return sourceRoom.Trim();
        }

        return InferSourceRoomFromHierarchy(transform);
    }

    private void FillSourceRoomFromHierarchy()
    {
        sourceRoom = InferSourceRoomFromHierarchy(transform);
    }

    private static string InferSourceRoomFromHierarchy(Transform current)
    {
        while (current != null)
        {
            string parsedRoomName = ParseRoomNameFromObject(current.name);

            if (!string.IsNullOrEmpty(parsedRoomName))
            {
                return parsedRoomName;
            }

            current = current.parent;
        }

        return string.Empty;
    }

    private static string ParseRoomNameFromObject(string objectName)
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
        else if (cleanName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Room_".Length);
        }
        else
        {
            return string.Empty;
        }

        return cleanName.Replace('_', ' ').Trim();
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
