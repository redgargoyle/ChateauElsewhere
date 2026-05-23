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

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool bringToFront = true;

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
