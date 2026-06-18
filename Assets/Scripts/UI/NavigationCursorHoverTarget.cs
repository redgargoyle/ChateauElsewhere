using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NavigationCursorHoverTarget : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private NavigationCursorController.HoverIcon hoverIcon = NavigationCursorController.HoverIcon.Ui;
    [SerializeField] private Selectable selectable;
    [SerializeField] private bool requireInteractable = true;

    private bool pointerInside;
    private bool pointerPressed;
    private bool cursorActive;

    public void Configure(NavigationCursorController.HoverIcon icon, Selectable targetSelectable, bool onlyWhenInteractable)
    {
        hoverIcon = icon;
        selectable = targetSelectable;
        requireInteractable = onlyWhenInteractable;
        RefreshCursor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        RefreshCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
        RefreshCursor();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerPressed = true;
        RefreshCursor();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPressed = false;
        RefreshCursor();
    }

    private void Update()
    {
        RefreshCursor();
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerPressed = false;
        SetCursorActive(false);
    }

    private void OnDestroy()
    {
        SetCursorActive(false);
    }

    private void RefreshCursor()
    {
        SetCursorActive(ShouldShowCursor());
    }

    private bool ShouldShowCursor()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (requireInteractable && selectable != null && !selectable.IsInteractable())
        {
            return false;
        }

        return pointerInside || pointerPressed;
    }

    private void SetCursorActive(bool active)
    {
        if (cursorActive == active)
        {
            return;
        }

        cursorActive = active;
        NavigationCursorController.SetDoorHover(this, hoverIcon, active);
    }
}
