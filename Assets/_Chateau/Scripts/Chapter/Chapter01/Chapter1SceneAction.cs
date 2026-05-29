using UnityEngine;
using UnityEngine.EventSystems;

public enum Chapter1SceneActionType
{
    FrontDoor,
    CoatCloset,
    GrandfatherClock,
    DrawingRoomExit
}

[DisallowMultipleComponent]
public class Chapter1SceneAction : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Chapter1SceneActionType actionType;
    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private GrandfatherClockInteraction clockInteraction;
    [SerializeField] private bool useManualWorldClickFallback = true;
    [SerializeField] private float manualClickRadius = 220f;
    [SerializeField] private bool isActionAvailable = true;

    public void Initialize(
        Chapter1SceneActionType nextActionType,
        Chapter1ArrivalController controller,
        GrandfatherClockInteraction clock)
    {
        actionType = nextActionType;
        arrivalController = controller;
        clockInteraction = clock;
        manualClickRadius = GetDefaultClickRadius(nextActionType);
    }

    public void SetAvailable(bool value)
    {
        isActionAvailable = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PerformAction();
    }

    private void OnMouseDown()
    {
        PerformAction();
    }

    private void Update()
    {
        if (actionType != Chapter1SceneActionType.FrontDoor ||
            !useManualWorldClickFallback ||
            !Input.GetMouseButtonDown(0))
        {
            return;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return;
        }

        Vector3 mousePosition = Input.mousePosition;
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Mathf.Abs(worldCamera.transform.position.z - transform.position.z)));

        if (Vector2.Distance(worldPosition, transform.position) <= manualClickRadius)
        {
            PerformAction();
        }
    }

    private void PerformAction()
    {
        if (!isActionAvailable)
        {
            return;
        }

        ResolveReferences();

        switch (actionType)
        {
            case Chapter1SceneActionType.FrontDoor:
                if (arrivalController != null)
                {
                    arrivalController.AnswerFrontDoor();
                }
                break;
            case Chapter1SceneActionType.CoatCloset:
                if (arrivalController != null)
                {
                    arrivalController.HandleClosetClicked();
                }
                break;
            case Chapter1SceneActionType.GrandfatherClock:
                if (clockInteraction != null)
                {
                    clockInteraction.OpenCloseUp();
                }
                break;
            case Chapter1SceneActionType.DrawingRoomExit:
                if (arrivalController != null)
                {
                    arrivalController.TryCompleteChapterFromDrawingRoomExit();
                }
                break;
        }
    }

    private void ResolveReferences()
    {
        if (arrivalController == null)
        {
            arrivalController = FindAnyObjectByType<Chapter1ArrivalController>(FindObjectsInactive.Include);
        }

        if (clockInteraction == null)
        {
            clockInteraction = FindAnyObjectByType<GrandfatherClockInteraction>(FindObjectsInactive.Include);
        }
    }

    private static float GetDefaultClickRadius(Chapter1SceneActionType type)
    {
        switch (type)
        {
            case Chapter1SceneActionType.FrontDoor:
                return 180f;
            case Chapter1SceneActionType.CoatCloset:
                return 220f;
            case Chapter1SceneActionType.GrandfatherClock:
                return 180f;
            case Chapter1SceneActionType.DrawingRoomExit:
                return 240f;
            default:
                return 220f;
        }
    }
}
