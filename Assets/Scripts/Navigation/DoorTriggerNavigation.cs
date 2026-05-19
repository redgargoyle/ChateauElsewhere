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
    [SerializeField] private AudioSource doorOpenAudioSource;

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool bringToFront = true;

    [Header("Audio")]
    [SerializeField] private bool playDoorOpenSound = true;
    [SerializeField] private string doorOpenAudioObjectName = "Audio_DoorOpen";

    [Header("Runtime Projection")]
    [SerializeField] private bool followCameraBackground = true;

    public string SourceRoom => GetEffectiveSourceRoom();
    public string DoorName => Clean(doorName);
    public string DestinationRoom => Clean(destinationRoom);
    public bool UsesCameraSequence => useCameraSequence;

    private RectTransform rectTransform;
    private Rect runtimeSourceImageUvRect;
    private bool hasRuntimeSourceImageUvRect;
    private readonly Vector3[] authoringCorners = new Vector3[4];

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
        CaptureAuthoredSourceImageRectIfNeeded();
        BringToFrontIfNeeded();
    }

    private void OnEnable()
    {
        CaptureAuthoredSourceImageRectIfNeeded();
        FollowCameraBackgroundIfNeeded();
    }

    private void Start()
    {
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

        NavigationCursorController.ClearDoorHover(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoveredTrigger(this);
        NavigationCursorController.SetDoorHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoveredTrigger == this)
        {
            SetHoveredTrigger(null);
        }

        NavigationCursorController.SetDoorHover(this, false);
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
            PlayDoorOpenSoundIfSuccessful(navigationManager.OpenDoorFromCurrentRoom(string.Empty, DoorName, false));
            return;
        }

        if (!string.IsNullOrWhiteSpace(DoorName) && navigationManager.HasDoor(DoorName))
        {
            // Door triggers created from doors.txt should navigate through the
            // loaded door data. The serialized destination is still kept on the
            // trigger so the scene is readable in the Inspector, but doors.txt is
            // the source of truth at runtime.
            PlayDoorOpenSoundIfSuccessful(navigationManager.TryMoveThroughDoor(DoorName));
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
        PlayDoorOpenSoundIfSuccessful(navigationManager.MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom));
    }

    public void RefreshInferredSourceRoom()
    {
        FillSourceRoomFromHierarchy();
    }

    public bool ApplyAuthoredRectToCamera(CameraManager previewCameraManager = null)
    {
        if (previewCameraManager != null)
        {
            cameraManager = previewCameraManager;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null || !CaptureAuthoredSourceImageRectIfNeeded())
        {
            return false;
        }

        return cameraManager.TryApplySourceImageRect(rectTransform, runtimeSourceImageUvRect);
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
            navigationManager = FindObjectOfType<RoomNavigationManager>(true);
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>(true);
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

    private void PlayDoorOpenSoundIfSuccessful(bool didNavigate)
    {
        if (!didNavigate || !playDoorOpenSound)
        {
            return;
        }

        ResolveDoorOpenAudioSource();

        if (doorOpenAudioSource == null)
        {
            return;
        }

        if (doorOpenAudioSource.clip != null)
        {
            doorOpenAudioSource.PlayOneShot(doorOpenAudioSource.clip);
            return;
        }

        doorOpenAudioSource.Play();
    }

    private void ResolveDoorOpenAudioSource()
    {
        if (doorOpenAudioSource != null)
        {
            return;
        }

        GameObject audioObject = GameObject.Find(doorOpenAudioObjectName);

        if (audioObject != null)
        {
            doorOpenAudioSource = audioObject.GetComponent<AudioSource>();
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

    private void FollowCameraBackgroundIfNeeded()
    {
        if (!Application.isPlaying || !followCameraBackground)
        {
            return;
        }

        ResolveReferences();

        if (cameraManager == null || rectTransform == null)
        {
            return;
        }

        ApplyAuthoredRectToCamera(cameraManager);
        BringToFrontIfNeeded();
    }

    private bool CaptureAuthoredSourceImageRectIfNeeded()
    {
        if (hasRuntimeSourceImageUvRect)
        {
            return true;
        }

        ResolveReferences();

        if (rectTransform == null)
        {
            return false;
        }

        if (!TryCaptureAuthoredSourceImageRect(out runtimeSourceImageUvRect))
        {
            return false;
        }

        hasRuntimeSourceImageUvRect = true;
        return true;
    }

    private bool TryCaptureAuthoredSourceImageRect(out Rect sourceImageUvRect)
    {
        sourceImageUvRect = new Rect();

        RectTransform sourceSpace = FindAuthoringSourceSpace();

        if (sourceSpace == null || !RectIsUsable(sourceSpace))
        {
            Canvas.ForceUpdateCanvases();
            sourceSpace = FindAuthoringSourceSpace();
        }

        if (sourceSpace == null || !RectIsUsable(sourceSpace))
        {
            return false;
        }

        rectTransform.GetWorldCorners(authoringCorners);

        Rect sourceRect = sourceSpace.rect;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < authoringCorners.Length; i++)
        {
            Vector2 localPoint = sourceSpace.InverseTransformPoint(authoringCorners[i]);
            Vector2 uv = new Vector2(
                SafeInverseLerp(sourceRect.xMin, sourceRect.xMax, localPoint.x),
                SafeInverseLerp(sourceRect.yMin, sourceRect.yMax, localPoint.y));

            min = Vector2.Min(min, uv);
            max = Vector2.Max(max, uv);
        }

        sourceImageUvRect = NormalizeRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        return sourceImageUvRect.width > 0f && sourceImageUvRect.height > 0f;
    }

    private RectTransform FindAuthoringSourceSpace()
    {
        if (transform.parent is RectTransform parentRect && RectIsUsable(parentRect))
        {
            return parentRect;
        }

        RoomContentGroup roomContentGroup = GetComponentInParent<RoomContentGroup>(true);

        if (roomContentGroup != null && roomContentGroup.transform is RectTransform roomRect && RectIsUsable(roomRect))
        {
            return roomRect;
        }

        return cameraManager != null && cameraManager.cameraBackground != null
            ? cameraManager.cameraBackground.rectTransform
            : null;
    }

    private static bool RectIsUsable(RectTransform rectTransform)
    {
        Rect rect = rectTransform.rect;
        return rect.width > 0f && rect.height > 0f;
    }

    private static Rect NormalizeRect(Rect rect)
    {
        if (rect.width < 0f)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }

        if (rect.height < 0f)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }

        return rect;
    }

    private static float SafeInverseLerp(float from, float to, float value)
    {
        if (Mathf.Approximately(from, to))
        {
            return 0f;
        }

        return (value - from) / (to - from);
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
