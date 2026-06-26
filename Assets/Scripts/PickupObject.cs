using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PickupObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private bool cursorHoverActive;

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetPickupCursorHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetPickupCursorHover(false);
    }

    private void OnMouseEnter()
    {
        SetPickupCursorHover(true);
    }

    private void OnMouseExit()
    {
        SetPickupCursorHover(false);
    }

    private void OnDisable()
    {
        SetPickupCursorHover(false);
    }

    private void SetPickupCursorHover(bool active)
    {
        if (cursorHoverActive == active)
        {
            return;
        }

        cursorHoverActive = active;
        NavigationCursorController.SetDoorHover(this, NavigationCursorController.HoverIcon.PickUpTake, active);
    }
}
