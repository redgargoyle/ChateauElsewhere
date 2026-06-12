using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class Chapter3DiningGuestAction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Chapter3DinnerController controller;
    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private string guestId;
    [SerializeField] private bool available = true;

    private int lastClickFrame = -1;

    public void Initialize(Chapter3DinnerController dinnerController, ActorRoomState guestActor)
    {
        controller = dinnerController;
        actorState = guestActor;
        guestId = actorState != null ? actorState.ActorId : guestId;
        available = true;
    }

    public void SetAvailable(bool value)
    {
        available = value;
        enabled = value;

        Collider2D[] colliders2D = GetComponents<Collider2D>();

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
            {
                colliders2D[i].enabled = value;
            }
        }

        Collider[] colliders3D = GetComponents<Collider>();

        for (int i = 0; i < colliders3D.Length; i++)
        {
            if (colliders3D[i] != null)
            {
                colliders3D[i].enabled = value;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }

    private void OnMouseDown()
    {
        TryClick();
    }

    private void TryClick()
    {
        if (lastClickFrame == Time.frameCount || !CanClick())
        {
            return;
        }

        lastClickFrame = Time.frameCount;
        controller.HandleDiningGuestClicked(actorState);
    }

    private bool CanClick()
    {
        return available &&
            controller != null &&
            actorState != null &&
            actorState.IsVisibleInCurrentRoom &&
            actorState.IsInteractable;
    }
}
