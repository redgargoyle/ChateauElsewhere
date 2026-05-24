using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
public class DoorTriggerNavigation : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum NavigationTriggerKind
    {
        Door,
        Stairway
    }

    private const string DefaultDoorOpenSoundCatalogResourcePath = "Audio/DoorOpenSoundCatalog";
    private const string DefaultStairwaySoundCatalogResourcePath = "Audio/StairwaySoundCatalog";

    public static event Action<DoorTriggerNavigation> HoveredTriggerChanged;
    public static DoorTriggerNavigation HoveredTrigger { get; private set; }

    [Header("Door Route")]
    [SerializeField] private string sourceRoom;
    [SerializeField] private string doorName;
    [SerializeField] private string destinationRoom;
    [SerializeField] private bool requirePlayerInSourceRoom = true;
    [SerializeField] private bool useCameraSequence = true;
    [SerializeField] private NavigationTriggerKind triggerKind = NavigationTriggerKind.Door;

    [Header("References")]
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private Image image;
    [SerializeField] private AudioSource doorOpenAudioSource;
    [SerializeField] private Transform player;

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool bringToFront = true;

    [Header("Player Proximity")]
    [SerializeField] private bool requirePlayerProximity = true;
    [SerializeField] private bool walkPlayerToTriggerWhenFar = true;
    [SerializeField] private bool autoActivateAfterApproach = true;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private float maxPlayerScreenDistance = 145f;

    [Header("Audio")]
    [SerializeField] private bool playDoorOpenSound = true;
    [SerializeField] private string doorOpenAudioObjectName = "Audio_DoorOpen";
    [SerializeField] private DoorOpenSoundCatalog doorOpenSoundCatalog;
    [SerializeField] private string doorOpenSoundCatalogResourcePath = DefaultDoorOpenSoundCatalogResourcePath;
    [SerializeField] private DoorOpenSoundCatalog stairwaySoundCatalog;
    [SerializeField] private string stairwaySoundCatalogResourcePath = DefaultStairwaySoundCatalogResourcePath;

    public string SourceRoom => GetEffectiveSourceRoom();
    public string DoorName => Clean(doorName);
    public string DestinationRoom => Clean(destinationRoom);
    public bool UsesCameraSequence => useCameraSequence;
    public bool IsStairway => GetEffectiveTriggerKind() == NavigationTriggerKind.Stairway;
    public string InteractionLabel => IsStairway ? "Stairway" : "Door";

    private RectTransform rectTransform;
    private readonly Vector3[] triggerWorldCorners = new Vector3[4];
    private PointClickPlayerMovement pendingApproachPlayer;
    private static DoorTriggerNavigation pendingApproachTrigger;
    private static AudioSource activeNavigationAudioSource;
    private static int lastDoorOpenClipIndex = -1;
    private static int lastStairwayClipIndex = -1;

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
        ResolveDoorOpenAudioSource();
        ResolveDoorOpenSoundCatalog();
        ConfigureImage();
        BringToFrontIfNeeded();
    }

    private void OnEnable()
    {
        BringToFrontIfNeeded();
    }

    private void OnValidate()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        FillSourceRoomFromHierarchy();
        maxPlayerScreenDistance = Mathf.Max(1f, maxPlayerScreenDistance);

        ConfigureImage();
    }

    private void OnTransformParentChanged()
    {
        FillSourceRoomFromHierarchy();
    }

    private void OnDisable()
    {
        if (HoveredTrigger == this)
        {
            SetHoveredTrigger(null);
        }

        CancelPendingPlayerApproach();
        NavigationCursorController.ClearDoorHover(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoveredTrigger(this);
        NavigationCursorController.SetDoorHover(this, GetNavigationCursorIcon(), true);
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
        ActivateDoor(true);
    }

    private void ActivateDoor(bool allowPlayerApproach)
    {
        ResolveReferences();

        if (navigationManager == null)
        {
            Debug.LogWarning($"Door trigger '{name}' could not find a RoomNavigationManager.", this);
            return;
        }

        if (!IsPlayerCloseEnough())
        {
            if (allowPlayerApproach && TryStartPlayerApproach())
            {
                return;
            }

            Debug.Log($"Move closer to the {InteractionLabel.ToLowerInvariant()} before using it.", this);
            return;
        }

        CancelPendingPlayerApproach();

        if (useCameraSequence)
        {
            // The trigger does not load rooms itself. It asks the navigation
            // manager for the next room in the sequence, and the manager changes
            // the single current-room state that every room system reacts to.
            bool soundStarted = TryPlayNavigationSoundNow();
            bool didNavigate = navigationManager.OpenDoorFromCurrentRoom(string.Empty, DoorName, false);
            StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(destinationRoom))
        {
            // Manual room objects and Inspector fields are the source of truth.
            bool soundStarted = TryPlayNavigationSoundNow();
            bool didNavigate = navigationManager.MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom);
            StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);
            return;
        }

        Debug.LogWarning($"Door trigger '{name}' has no destination room.", this);
    }

    private bool TryStartPlayerApproach()
    {
        if (!walkPlayerToTriggerWhenFar)
        {
            return false;
        }

        ResolvePlayerReference();
        PointClickPlayerMovement playerMovement = player != null ? player.GetComponent<PointClickPlayerMovement>() : null;
        if (playerMovement == null)
        {
            return false;
        }

        CancelAnyPendingApproach();

        Vector2 approachScreenPoint = GetClosestTriggerScreenPoint(GetPlayerScreenPosition());
        if (!playerMovement.TrySetDestinationFromScreenPoint(approachScreenPoint, true))
        {
            return false;
        }

        if (!playerMovement.HasDestination)
        {
            if (!IsPlayerCloseEnough())
            {
                return false;
            }

            ActivateDoor(false);
            return true;
        }

        pendingApproachTrigger = this;
        pendingApproachPlayer = playerMovement;
        pendingApproachPlayer.MovementStopped += HandlePlayerApproachStopped;
        return true;
    }

    private void HandlePlayerApproachStopped()
    {
        CancelPendingPlayerApproach();

        if (autoActivateAfterApproach && isActiveAndEnabled && IsPlayerCloseEnough())
        {
            ActivateDoor(false);
        }
    }

    private bool IsPlayerCloseEnough()
    {
        if (!requirePlayerProximity)
        {
            return true;
        }

        ResolvePlayerReference();

        if (player == null)
        {
            Debug.LogWarning($"Door trigger '{name}' requires player proximity but could not find a player transform.", this);
            return true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null || rectTransform == null)
        {
            return true;
        }

        Vector2 playerScreenPosition = GetPlayerScreenPosition();
        Vector2 triggerScreenPosition = GetClosestTriggerScreenPoint(playerScreenPosition);
        return Vector2.Distance(playerScreenPosition, triggerScreenPosition) <= Mathf.Max(1f, maxPlayerScreenDistance);
    }

    private Vector2 GetPlayerScreenPosition()
    {
        Camera mainCamera = Camera.main;
        return mainCamera != null && player != null
            ? RectTransformUtility.WorldToScreenPoint(mainCamera, player.position)
            : Vector2.zero;
    }

    private Vector2 GetClosestTriggerScreenPoint(Vector2 screenPosition)
    {
        Camera canvasCamera = GetCanvasCamera();
        rectTransform.GetWorldCorners(triggerWorldCorners);

        Vector2 firstCorner = RectTransformUtility.WorldToScreenPoint(canvasCamera, triggerWorldCorners[0]);
        float minX = firstCorner.x;
        float maxX = firstCorner.x;
        float minY = firstCorner.y;
        float maxY = firstCorner.y;

        for (int i = 1; i < triggerWorldCorners.Length; i++)
        {
            Vector2 corner = RectTransformUtility.WorldToScreenPoint(canvasCamera, triggerWorldCorners[i]);
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
        }

        return new Vector2(
            Mathf.Clamp(screenPosition.x, minX, maxX),
            Mathf.Clamp(screenPosition.y, minY, maxY));
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            return;
        }

        PointClickPlayerMovement playerMovement = FindAnyObjectByType<PointClickPlayerMovement>();
        if (playerMovement != null)
        {
            player = playerMovement.transform;
            return;
        }

        string cleanPlayerObjectName = Clean(playerObjectName);
        if (string.IsNullOrEmpty(cleanPlayerObjectName))
        {
            return;
        }

        GameObject playerObject = GameObject.Find(cleanPlayerObjectName);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private static void CancelAnyPendingApproach()
    {
        if (pendingApproachTrigger != null)
        {
            pendingApproachTrigger.CancelPendingPlayerApproach();
        }
    }

    private void CancelPendingPlayerApproach()
    {
        if (pendingApproachPlayer != null)
        {
            pendingApproachPlayer.MovementStopped -= HandlePlayerApproachStopped;
            pendingApproachPlayer = null;
        }

        if (pendingApproachTrigger == this)
        {
            pendingApproachTrigger = null;
        }
    }

    public void RefreshInferredSourceRoom()
    {
        FillSourceRoomFromHierarchy();
    }

    private NavigationCursorController.HoverIcon GetNavigationCursorIcon()
    {
        return IsStairway
            ? NavigationCursorController.HoverIcon.Stairway
            : NavigationCursorController.HoverIcon.Door;
    }

    private NavigationTriggerKind GetEffectiveTriggerKind()
    {
        if (triggerKind == NavigationTriggerKind.Stairway)
        {
            return NavigationTriggerKind.Stairway;
        }

        if (name.StartsWith("StairwayTrigger_", StringComparison.OrdinalIgnoreCase) ||
            DoorName.IndexOf("Stairway", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return NavigationTriggerKind.Stairway;
        }

        return NavigationTriggerKind.Door;
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
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
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

    private bool TryPlayNavigationSoundNow()
    {
        if (!playDoorOpenSound)
        {
            return false;
        }

        ResolveDoorOpenAudioSource();

        if (doorOpenAudioSource == null)
        {
            return false;
        }

        StopCurrentNavigationSound();
        activeNavigationAudioSource = doorOpenAudioSource;
        doorOpenAudioSource.Stop();

        if (TryGetNavigationClip(out AudioClip randomClip))
        {
            doorOpenAudioSource.PlayOneShot(randomClip);
            return true;
        }

        if (!IsStairway && doorOpenAudioSource.clip != null)
        {
            doorOpenAudioSource.PlayOneShot(doorOpenAudioSource.clip);
            return true;
        }

        activeNavigationAudioSource = null;
        return false;
    }

    private void StopNavigationSoundIfNavigationFailed(bool soundStarted, bool didNavigate)
    {
        if (soundStarted && !didNavigate && activeNavigationAudioSource == doorOpenAudioSource)
        {
            StopCurrentNavigationSound();
        }
    }

    private static void StopCurrentNavigationSound()
    {
        if (activeNavigationAudioSource != null)
        {
            activeNavigationAudioSource.Stop();
            activeNavigationAudioSource = null;
        }
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

    private bool TryGetNavigationClip(out AudioClip clip)
    {
        clip = null;

        if (IsStairway)
        {
            ResolveStairwaySoundCatalog();
            return stairwaySoundCatalog != null && stairwaySoundCatalog.TryGetRandomClip(ref lastStairwayClipIndex, out clip);
        }

        ResolveDoorOpenSoundCatalog();

        return doorOpenSoundCatalog != null && doorOpenSoundCatalog.TryGetRandomClip(ref lastDoorOpenClipIndex, out clip);
    }

    private void ResolveDoorOpenSoundCatalog()
    {
        if (doorOpenSoundCatalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(doorOpenSoundCatalogResourcePath)
                ? DefaultDoorOpenSoundCatalogResourcePath
                : doorOpenSoundCatalogResourcePath.Trim();

            doorOpenSoundCatalog = Resources.Load<DoorOpenSoundCatalog>(resourcePath);
        }
    }

    private void ResolveStairwaySoundCatalog()
    {
        if (stairwaySoundCatalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(stairwaySoundCatalogResourcePath)
                ? DefaultStairwaySoundCatalogResourcePath
                : stairwaySoundCatalogResourcePath.Trim();

            stairwaySoundCatalog = Resources.Load<DoorOpenSoundCatalog>(resourcePath);
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
        string hierarchySourceRoom = InferSourceRoomFromHierarchy(transform);

        if (!string.IsNullOrWhiteSpace(hierarchySourceRoom))
        {
            return hierarchySourceRoom;
        }

        return Clean(sourceRoom);
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
