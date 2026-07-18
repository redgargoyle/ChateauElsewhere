using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class Chapter1CoatPickup : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const float MinimumScreenClickRadius = 38f;
    private static readonly System.Collections.Generic.List<Chapter1CoatPickup> ActivePickups =
        new System.Collections.Generic.List<Chapter1CoatPickup>();

    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private string guestId;
    [SerializeField] private string coatId;

    private bool cursorHoverActive;
    private NavigationCursorController.HoverIcon cursorHoverIcon = NavigationCursorController.HoverIcon.PickUpCoat;
    private int lastPointerActionFrame = -1;

    public string GuestId => guestId;
    public string CoatId => coatId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetActivePickupsForPlayMode()
    {
        ActivePickups.Clear();
    }

    public void Initialize(Chapter1ArrivalController controller, string ownerGuestId, string ownerCoatId)
    {
        arrivalController = controller;
        guestId = ownerGuestId;
        coatId = ownerCoatId;
        Chapter1PointerPriority.InvalidateCache();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RefreshPointerHover();
    }

    private void OnMouseDown()
    {
        if (TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            TryHandlePointerAction(screenPosition, false);
        }
    }

    private void OnEnable()
    {
        if (!ActivePickups.Contains(this))
        {
            ActivePickups.Add(this);
        }

        Chapter1PointerPriority.InvalidateCache();
    }

    private void OnMouseEnter()
    {
        RefreshPointerHover();
    }

    private void OnMouseExit()
    {
        RefreshPointerHover();
    }

    private void Update()
    {
        if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            SetCoatCursorHover(false);
            return;
        }

        TryHandlePointerAction(screenPosition, TryGetPrimaryPointerDown());
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
        Chapter1PointerPriority.InvalidateCache();
        SetCoatCursorHover(false);
    }

    private void TryPickUp()
    {
        if (arrivalController == null)
        {
            arrivalController = FindAnyObjectByType<Chapter1ArrivalController>(FindObjectsInactive.Include);
        }

        if (arrivalController != null)
        {
            arrivalController.HandleCoatClicked(this);
        }
    }

    private void RefreshPointerHover()
    {
        if (TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            TryHandlePointerAction(screenPosition, false);
            return;
        }

        SetCoatCursorHover(false);
    }

    private void TryHandlePointerAction(Vector2 screenPosition, bool activate)
    {
        if (PointClickPlayerMovement.IsPointerOverBlockingUi(screenPosition))
        {
            SetCoatCursorHover(false);
            return;
        }

        bool isSelectedTarget = Chapter1PointerPriority.TryGetTarget(
            screenPosition,
            out MonoBehaviour target) && target == this;
        SetCoatCursorHover(isSelectedTarget);

        if (!activate ||
            !isSelectedTarget ||
            !NavigationCursorController.IsPrimaryHoverOwner(this) ||
            lastPointerActionFrame == Time.frameCount)
        {
            return;
        }

        lastPointerActionFrame = Time.frameCount;
        TryPickUp();
    }

    private void SetCoatCursorHover(bool active)
    {
        if (!active)
        {
            if (!cursorHoverActive)
            {
                return;
            }

            cursorHoverActive = false;
            NavigationCursorController.ClearDoorHover(this);
            return;
        }

        NavigationCursorController.HoverIcon nextIcon = CanTakeThisCoat()
            ? NavigationCursorController.HoverIcon.PickUpCoat
            : NavigationCursorController.HoverIcon.Locked;

        if (cursorHoverActive && cursorHoverIcon == nextIcon)
        {
            return;
        }

        cursorHoverActive = true;
        cursorHoverIcon = nextIcon;

        NavigationCursorController.SetDoorHover(
            this,
            cursorHoverIcon,
            NavigationCursorController.GuestActionHoverPriority,
            true);
    }

    public static void ApplyPointerSelection(Chapter1CoatPickup selectedPickup)
    {
        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            Chapter1CoatPickup candidate = ActivePickups[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                ActivePickups.RemoveAt(i);
                continue;
            }

            candidate.SetCoatCursorHover(candidate == selectedPickup);
        }
    }

    private bool CanTakeThisCoat()
    {
        if (arrivalController == null)
        {
            arrivalController = FindAnyObjectByType<Chapter1ArrivalController>(FindObjectsInactive.Include);
        }

        return arrivalController != null && arrivalController.CanTakeCoat(coatId);
    }

    private bool IsPointerOverCoat(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return false;
        }

        if (TryGetScreenBounds(worldCamera, out Vector2 min, out Vector2 max))
        {
            if (screenPosition.x >= min.x &&
                screenPosition.x <= max.x &&
                screenPosition.y >= min.y &&
                screenPosition.y <= max.y)
            {
                return true;
            }

            Vector2 center = (min + max) * 0.5f;
            return Vector2.Distance(screenPosition, center) <= MinimumScreenClickRadius;
        }

        Vector2 fallbackCenter = worldCamera.WorldToScreenPoint(transform.position);
        return Vector2.Distance(screenPosition, fallbackCenter) <= MinimumScreenClickRadius;
    }

    public static bool TryGetCoatAtScreenPosition(
        Vector2 screenPosition,
        out Chapter1CoatPickup coat)
    {
        coat = null;
        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            Chapter1CoatPickup candidate = ActivePickups[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                ActivePickups.RemoveAt(i);
                continue;
            }

            if (!candidate.IsPointerOverCoat(screenPosition))
            {
                continue;
            }

            if (coat == null || candidate.GetInstanceID() < coat.GetInstanceID())
            {
                coat = candidate;
            }
        }

        return coat != null;
    }

    private bool TryGetScreenBounds(Camera worldCamera, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            AddWorldBoundsToScreenBounds(spriteRenderer.bounds, worldCamera, ref min, ref max);
            hasBounds = true;
        }

        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null && collider.enabled)
        {
            AddWorldBoundsToScreenBounds(collider.bounds, worldCamera, ref min, ref max);
            hasBounds = true;
        }

        return hasBounds;
    }

    private static void AddWorldBoundsToScreenBounds(Bounds bounds, Camera worldCamera, ref Vector2 min, ref Vector2 max)
    {
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)), ref min, ref max);
    }

    private static void AddScreenPointToBounds(Vector2 point, ref Vector2 min, ref Vector2 max)
    {
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    private static bool TryGetPrimaryPointerPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            screenPosition = Input.mousePosition;
            return true;
        }
        catch (InvalidOperationException)
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
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }
}
