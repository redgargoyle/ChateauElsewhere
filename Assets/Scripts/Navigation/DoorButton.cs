using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class DoorButton : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private string roomName;
    [SerializeField] private string doorId;

    [Header("References")]
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private Button button;

    [Header("Display")]
    [SerializeField] private bool makeInvisibleAtRuntime = true;
    [SerializeField] private Color runtimeColor = new Color(1f, 1f, 1f, 0f);

    public string RoomName => GetEffectiveRoomName();
    public string DoorId => GetEffectiveDoorId();

    private void Reset()
    {
        button = GetComponent<Button>();
        FillNamesFromHierarchy();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureGraphic();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void RefreshInferredNames()
    {
        FillNamesFromHierarchy();
    }

    private void HandleClick()
    {
        ResolveReferences();

        if (navigationManager == null)
        {
            Debug.LogWarning($"DoorButton '{name}' could not find a RoomNavigationManager.", this);
            return;
        }

        navigationManager.TryMoveThroughDoor(DoorId);
    }

    private void ResolveReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (navigationManager == null)
        {
            navigationManager = FindObjectOfType<RoomNavigationManager>();
        }
    }

    private void ConfigureGraphic()
    {
        Image image = GetComponent<Image>();

        if (image == null)
        {
            return;
        }

        image.raycastTarget = true;

        if (makeInvisibleAtRuntime)
        {
            image.color = runtimeColor;
        }

        if (button != null && button.targetGraphic == null)
        {
            button.targetGraphic = image;
        }
    }

    private string GetEffectiveDoorId()
    {
        if (!string.IsNullOrWhiteSpace(doorId))
        {
            return doorId.Trim();
        }

        return ParseDoorIdFromName(gameObject.name);
    }

    private string GetEffectiveRoomName()
    {
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            return roomName.Trim();
        }

        Transform current = transform.parent;

        while (current != null)
        {
            string parsed = ParseRoomNameFromName(current.name);

            if (!string.IsNullOrEmpty(parsed))
            {
                return parsed;
            }

            current = current.parent;
        }

        return string.Empty;
    }

    private void FillNamesFromHierarchy()
    {
        if (string.IsNullOrWhiteSpace(doorId))
        {
            doorId = ParseDoorIdFromName(gameObject.name);
        }

        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = GetEffectiveRoomName();
        }
    }

    private static string ParseDoorIdFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        if (cleanName.StartsWith("Door_", StringComparison.OrdinalIgnoreCase))
        {
            return cleanName.Substring("Door_".Length).Trim();
        }

        if (cleanName.StartsWith("Button_Door_", StringComparison.OrdinalIgnoreCase))
        {
            return cleanName.Substring("Button_Door_".Length).Trim();
        }

        return cleanName;
    }

    private static string ParseRoomNameFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        if (!cleanName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return cleanName.Substring("Room_".Length).Replace('_', ' ').Trim();
    }
}
