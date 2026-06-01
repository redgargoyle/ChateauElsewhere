using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class Chapter2GuestFindAction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
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
        TryStartGuestConversation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetTalkCursorHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetTalkCursorHover(false);
    }

    private void OnMouseDown()
    {
        TryStartGuestConversation();
    }

    private void OnMouseEnter()
    {
        SetTalkCursorHover(true);
    }

    private void OnMouseExit()
    {
        SetTalkCursorHover(false);
    }

    private void OnDisable()
    {
        SetTalkCursorHover(false);
    }

    private void TryStartGuestConversation()
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

        if (searchController != null && searchController.TryStartGuestConversation(guestId))
        {
            SetTalkCursorHover(false);
        }
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
}
