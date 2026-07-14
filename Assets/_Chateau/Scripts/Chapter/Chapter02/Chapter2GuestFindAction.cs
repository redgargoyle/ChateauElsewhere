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

    private int lastSuccessfulClickFrame = -1;
    private bool cursorHoverActive;
    private static Chapter2GuestFindAction manualHoveredAction;
    private static int lastManualPointerFrame = -1;
    private static int lastManualClickFrame = -1;
    private static int lastPhysicsSyncFrame = -1;

    public string GuestId => guestId;
    public bool IsAvailable => isAvailable;

    public static bool IsPointerOverAvailableGuestAction(Vector2 screenPosition)
    {
        return TryGetAvailableGuestActionAtScreenPosition(screenPosition, out _);
    }

    private static bool TryGetAvailableGuestActionAtScreenPosition(Vector2 screenPosition, out Chapter2GuestFindAction action)
    {
        Camera mainCamera = Camera.main;
        action = null;

        if (mainCamera == null)
        {
            return false;
        }

        SyncPhysicsTransformsForPointerQuery();

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            GetScreenToWorldDepth(mainCamera)));
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
            {
                continue;
            }

            if (TryUseAvailableAction(hit.GetComponent<Chapter2GuestFindAction>(), out action) ||
                TryUseAvailableAction(hit.GetComponentInParent<Chapter2GuestFindAction>(), out action) ||
                TryUseAvailableAction(hit.GetComponentInChildren<Chapter2GuestFindAction>(true), out action))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUseAvailableAction(Chapter2GuestFindAction candidate, out Chapter2GuestFindAction action)
    {
        action = null;

        if (candidate == null ||
            !candidate.enabled ||
            !candidate.gameObject.activeInHierarchy ||
            !candidate.IsAvailable)
        {
            return false;
        }

        action = candidate;
        return true;
    }

    private static void SyncPhysicsTransformsForPointerQuery()
    {
        if (lastPhysicsSyncFrame == Time.frameCount)
        {
            return;
        }

        lastPhysicsSyncFrame = Time.frameCount;
        Physics2D.SyncTransforms();
    }

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

    private void Update()
    {
        if (!Application.isPlaying || !isAvailable)
        {
            return;
        }

        UpdateManualPointerHandling();
    }

    private void OnMouseDown()
    {
        if (TryGetMouseScreenPosition(out Vector2 screenPosition) &&
            PointClickPlayerMovement.IsPointerOverBlockingUi(screenPosition))
        {
            return;
        }

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
        if (manualHoveredAction == this)
        {
            manualHoveredAction = null;
        }

        SetTalkCursorHover(false);
    }

    private static void UpdateManualPointerHandling()
    {
        if (lastManualPointerFrame == Time.frameCount)
        {
            return;
        }

        lastManualPointerFrame = Time.frameCount;

        if (!TryGetMouseScreenPosition(out Vector2 screenPosition))
        {
            SetManualHoveredAction(null);
            return;
        }

        if (PointClickPlayerMovement.IsPointerOverBlockingUi(screenPosition))
        {
            SetManualHoveredAction(null);
            return;
        }

        if (!TryGetAvailableGuestActionAtScreenPosition(screenPosition, out Chapter2GuestFindAction action))
        {
            SetManualHoveredAction(null);
            return;
        }

        SetManualHoveredAction(action);

        if (lastManualClickFrame == Time.frameCount || !TryGetPrimaryPointerDown())
        {
            return;
        }

        lastManualClickFrame = Time.frameCount;
        action.LogDiagnostic("ManualPointerClick", $"eventMouse={FormatVector(screenPosition)}");
        action.TryStartGuestConversation();
    }

    private static void SetManualHoveredAction(Chapter2GuestFindAction action)
    {
        if (manualHoveredAction == action)
        {
            return;
        }

        if (manualHoveredAction != null)
        {
            manualHoveredAction.SetTalkCursorHover(false);
        }

        manualHoveredAction = action;

        if (manualHoveredAction != null)
        {
            manualHoveredAction.SetTalkCursorHover(true);
        }
    }

    private void TryStartGuestConversation()
    {
        LogDiagnostic("TryStartGuestConversation start");

        if (!isAvailable)
        {
            LogDiagnostic("TryStartGuestConversation fail", "reason=unavailable");
            return;
        }

        if (lastSuccessfulClickFrame == Time.frameCount)
        {
            LogDiagnostic("TryStartGuestConversation fail", "reason=duplicate-frame");
            return;
        }

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
            lastSuccessfulClickFrame = Time.frameCount;
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

    private static float GetScreenToWorldDepth(Camera mainCamera)
    {
        return mainCamera.orthographic
            ? 0f
            : Mathf.Abs(mainCamera.transform.position.z);
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

    private static bool TryGetPrimaryPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return Input.GetMouseButtonDown(0);
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
