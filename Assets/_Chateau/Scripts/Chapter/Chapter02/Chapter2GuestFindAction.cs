using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class Chapter2GuestFindAction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const string DiagnosticPrefix = "[Ch2ClickDiag]";

    [SerializeField] private string guestId;
    [SerializeField] private Chapter2GuestSearchController searchController;
    [SerializeField] private bool isAvailable = true;

    private int lastClickFrame = -1;
    private bool cursorHoverActive;

    public string GuestId => guestId;
    public bool IsAvailable => isAvailable;

    public void Initialize(string id, Chapter2GuestSearchController controller)
    {
        guestId = string.IsNullOrWhiteSpace(id) ? name : id.Trim();
        searchController = controller;
        isAvailable = true;
        enabled = true;
    }

    public void SetAvailable(bool value)
    {
        isAvailable = value;

        if (!isAvailable)
        {
            SetTalkCursorHover(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogPointerEvent("OnPointerClick", eventData);
        TryStartGuestConversation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        LogPointerEvent("OnPointerEnter", eventData);
        SetTalkCursorHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LogPointerEvent("OnPointerExit", eventData);
        SetTalkCursorHover(false);
    }

    private void OnMouseDown()
    {
        LogDiagnostic("OnMouseDown");
        TryStartGuestConversation();
    }

    private void OnMouseEnter()
    {
        LogDiagnostic("OnMouseEnter");
        SetTalkCursorHover(true);
    }

    private void OnMouseExit()
    {
        LogDiagnostic("OnMouseExit");
        SetTalkCursorHover(false);
    }

    private void OnDisable()
    {
        SetTalkCursorHover(false);
    }

    private void TryStartGuestConversation()
    {
        LogDiagnostic("TryStartGuestConversation start");

        if (!isAvailable)
        {
            LogDiagnostic("TryStartGuestConversation fail", "reason=unavailable");
            return;
        }

        if (lastClickFrame == Time.frameCount)
        {
            LogDiagnostic("TryStartGuestConversation fail", "reason=duplicate-frame");
            return;
        }

        lastClickFrame = Time.frameCount;

        if (searchController == null)
        {
            searchController = FindAnyObjectByType<Chapter2GuestSearchController>(FindObjectsInactive.Include);
        }

        if (searchController == null)
        {
            LogDiagnostic("TryStartGuestConversation fail", "reason=missing-search-controller");
            return;
        }

        if (searchController.TryStartGuestConversation(guestId))
        {
            LogDiagnostic("TryStartGuestConversation success");
            SetTalkCursorHover(false);
            return;
        }

        LogDiagnostic("TryStartGuestConversation fail", "reason=search-controller-rejected");
    }

    private void SetTalkCursorHover(bool active)
    {
        bool nextActive = active && isAvailable && isActiveAndEnabled;

        if (cursorHoverActive == nextActive)
        {
            return;
        }

        cursorHoverActive = nextActive;

        if (nextActive)
        {
            NavigationCursorController.SetDoorHover(this, NavigationCursorController.HoverIcon.Talk, true);
        }
        else
        {
            NavigationCursorController.SetDoorHover(this, false);
        }
    }

    private void LogPointerEvent(string eventName, PointerEventData eventData)
    {
        string pointerPosition = eventData != null
            ? FormatVector(eventData.position)
            : FormatMouseScreenPosition();
        LogDiagnostic(eventName, $"eventMouse={pointerPosition}");
    }

    private void LogDiagnostic(string eventName, string extra = null)
    {
        string message =
            $"{DiagnosticPrefix} GuestAction {eventName} frame={Time.frameCount} " +
            $"guest={FormatGuestId()} object={name} available={isAvailable} " +
            $"enabled={enabled} active={gameObject.activeInHierarchy} " +
            $"mouse={FormatMouseScreenPosition()} currentRoom={GetCurrentRoomForLog()}";

        if (!string.IsNullOrWhiteSpace(extra))
        {
            message += $" {extra}";
        }

        Debug.Log(message, this);
    }

    private string FormatGuestId()
    {
        return string.IsNullOrWhiteSpace(guestId) ? "<empty>" : guestId;
    }

    private static string GetCurrentRoomForLog()
    {
        RoomNavigationManager navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        return navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom)
            ? "<none>"
            : navigationManager.CurrentRoom;
    }

    private static string FormatMouseScreenPosition()
    {
        return TryGetMouseScreenPosition(out Vector2 position) ? FormatVector(position) : "<unavailable>";
    }

    private static bool TryGetMouseScreenPosition(out Vector2 position)
    {
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            position = Input.mousePosition;
            return true;
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static string FormatVector(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }
}
