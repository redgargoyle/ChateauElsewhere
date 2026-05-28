using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GuestArrivalConfig
{
    [Header("Guest")]
    [SerializeField] private string guestId;
    [SerializeField] private string guestDisplayName;
    [SerializeField] private GameObject guestObject;
    [SerializeField] private ActorRoomState actorState;

    [Header("Pacing")]
    [SerializeField] private float arrivalIntervalSeconds = 15f;

    [Header("Waypoints")]
    [SerializeField] private Transform frontDoorArrivalPoint;
    [SerializeField] private Transform drawingRoomEntryPoint;
    [SerializeField] private Transform assignedSeat;

    [Header("Dialogue")]
    [TextArea]
    [SerializeField] private string greetingLine;
    [TextArea]
    [SerializeField] private List<string> ambientLines = new List<string>();

    [Header("Coat")]
    [SerializeField] private string coatId;
    [SerializeField] private string coatDisplayName;

    public string GuestId => string.IsNullOrWhiteSpace(guestId) ? GuestDisplayName : guestId.Trim();
    public string GuestDisplayName => string.IsNullOrWhiteSpace(guestDisplayName) ? "Guest" : guestDisplayName.Trim();
    public GameObject GuestObject => guestObject;
    public ActorRoomState ActorState => actorState;
    public float ArrivalIntervalSeconds => Mathf.Max(0f, arrivalIntervalSeconds);
    public Transform FrontDoorArrivalPoint => frontDoorArrivalPoint;
    public Transform DrawingRoomEntryPoint => drawingRoomEntryPoint;
    public Transform AssignedSeat => assignedSeat;
    public string GreetingLine => greetingLine;
    public IReadOnlyList<string> AmbientLines => ambientLines;
    public string CoatId => string.IsNullOrWhiteSpace(coatId) ? CoatDisplayName : coatId.Trim();
    public string CoatDisplayName => string.IsNullOrWhiteSpace(coatDisplayName) ? $"{GuestDisplayName} Coat" : coatDisplayName.Trim();

    public ActorRoomState ResolveActorState()
    {
        if (actorState == null && guestObject != null)
        {
            actorState = guestObject.GetComponent<ActorRoomState>();
        }

        if (actorState == null && guestObject != null)
        {
            actorState = guestObject.AddComponent<ActorRoomState>();
        }

        if (actorState != null)
        {
            actorState.SetActorId(GuestId);
        }

        return actorState;
    }

    public GameObject ResolveGuestObject()
    {
        if (guestObject == null && actorState != null)
        {
            guestObject = actorState.gameObject;
        }

        return guestObject;
    }

    public Transform GetFrontDoorArrivalPoint(Transform fallback)
    {
        return frontDoorArrivalPoint != null ? frontDoorArrivalPoint : fallback;
    }

    public Transform GetDrawingRoomEntryPoint(Transform fallback)
    {
        return drawingRoomEntryPoint != null ? drawingRoomEntryPoint : fallback;
    }

    public Transform GetAssignedSeat(Transform fallback)
    {
        return assignedSeat != null ? assignedSeat : fallback;
    }
}

public enum GuestArrivalState
{
    Hidden,
    WaitingTurn,
    Arriving,
    AwaitingGreeting,
    GreetingComplete,
    CoatTaken,
    MovingToDrawingRoom,
    Seated,
    AmbientIdle,
    Handled
}
