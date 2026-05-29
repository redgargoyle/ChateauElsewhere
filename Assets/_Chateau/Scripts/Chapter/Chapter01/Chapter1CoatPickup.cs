using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class Chapter1CoatPickup : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private string guestId;
    [SerializeField] private string coatId;

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

    private void OnMouseDown()
    {
        TryPickUp();
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
}
