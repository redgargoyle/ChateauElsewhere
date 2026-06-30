using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class Chapter1CoatPickup : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const float MinimumScreenClickRadius = 38f;

    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private string guestId;
    [SerializeField] private string coatId;

    private bool cursorHoverActive;
    private NavigationCursorController.HoverIcon cursorHoverIcon = NavigationCursorController.HoverIcon.PickUpCoat;

    public string GuestId => guestId;
    public string CoatId => coatId;

    public void Initialize(Chapter1ArrivalController controller, string ownerGuestId, string ownerCoatId)
    {
        arrivalController = controller;
        guestId = ownerGuestId;
        coatId = ownerCoatId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryPickUp();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetCoatCursorHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetCoatCursorHover(false);
    }

    private void OnMouseDown()
    {
        TryPickUp();
    }

    private void OnMouseEnter()
    {
        SetCoatCursorHover(true);
    }

    private void OnMouseExit()
    {
        SetCoatCursorHover(false);
    }

    private void Update()
    {
        if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            SetCoatCursorHover(false);
            return;
        }

        bool pointerOverCoat = IsPointerOverCoat(screenPosition);
        SetCoatCursorHover(pointerOverCoat);

        if (pointerOverCoat && TryGetPrimaryPointerDown())
        {
            TryPickUp();
        }
    }

    private void OnDisable()
    {
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

    private void SetCoatCursorHover(bool active)
    {
        NavigationCursorController.HoverIcon nextIcon = CanTakeThisCoat()
            ? NavigationCursorController.HoverIcon.PickUpCoat
            : NavigationCursorController.HoverIcon.Locked;

        if (cursorHoverActive == active && (!active || cursorHoverIcon == nextIcon))
        {
            return;
        }

        cursorHoverActive = active;
        cursorHoverIcon = nextIcon;

        if (!active)
        {
            NavigationCursorController.SetDoorHover(this, false);
            return;
        }

        NavigationCursorController.SetDoorHover(this, cursorHoverIcon, true);
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

            Vector2 center = ClampScreenPointToSafeViewport((min + max) * 0.5f);
            return Vector2.Distance(screenPosition, center) <= MinimumScreenClickRadius;
        }

        Vector2 fallbackCenter = ClampScreenPointToSafeViewport(worldCamera.WorldToScreenPoint(transform.position));
        return Vector2.Distance(screenPosition, fallbackCenter) <= MinimumScreenClickRadius;
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

    private static Vector2 ClampScreenPointToSafeViewport(Vector2 screenPoint)
    {
        float width = Screen.width;
        float height = Screen.height;

        if (width <= 1f || height <= 1f)
        {
            return screenPoint;
        }

        float horizontalMargin = Mathf.Min(96f, width * 0.08f);
        float bottomMargin = Mathf.Min(120f, height * 0.16f);
        float topMargin = Mathf.Min(96f, height * 0.12f);

        return new Vector2(
            Mathf.Clamp(screenPoint.x, horizontalMargin, width - horizontalMargin),
            Mathf.Clamp(screenPoint.y, bottomMargin, height - topMargin));
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
