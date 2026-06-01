using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class Chapter2GuestFindAction : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string guestId;
    [SerializeField] private Chapter2GuestSearchController searchController;
    [SerializeField] private bool isAvailable = true;

    private int lastClickFrame = -1;

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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryMarkGuestFound();
    }

    private void OnMouseDown()
    {
        TryMarkGuestFound();
    }

    private void TryMarkGuestFound()
    {
        if (!isAvailable || lastClickFrame == Time.frameCount)
        {
            return;
        }

        lastClickFrame = Time.frameCount;

        if (searchController == null)
        {
            searchController = FindAnyObjectByType<Chapter2GuestSearchController>(FindObjectsInactive.Include);
        }

        if (searchController != null && searchController.MarkGuestFound(guestId))
        {
            isAvailable = false;
        }
    }
}
